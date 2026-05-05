using System.Globalization;
using StripHeatmapGeneratorBlazor.Models;

namespace StripHeatmapGeneratorBlazor.Services;

public class CsvParserService
{
    private static readonly HashSet<string> SkipCols = new(StringComparer.OrdinalIgnoreCase)
        { "Serial", "Bin", "X", "Y", "X_old", "Y_old", "Wafer_ID" };

    // ── 從 Stream 解析 CSV（Blazor InputFile 提供的 stream）────────────────
    public async Task<CsvLoadResult> ParseAsync(Stream stream)
    {
        var result = new CsvLoadResult();
        using var reader = new StreamReader(stream);

        // Header
        string? headerLine = await reader.ReadLineAsync();
        if (headerLine is null) return result;
        headerLine = headerLine.TrimStart('\uFEFF');

        result.Headers = headerLine.Split(',').Select(h => h.Trim()).ToList();
        result.ParamColumns = result.Headers
            .Where(h => !SkipCols.Contains(h) && h.Length > 0)
            .ToList();

        int idxBin   = Idx(result.Headers, "Bin");
        int idxX     = Idx(result.Headers, "X");
        int idxY     = Idx(result.Headers, "Y");
        int idxStrip = Idx(result.Headers, "Wafer_ID");
        if (idxBin < 0 || idxX < 0 || idxY < 0 || idxStrip < 0)
            throw new Exception("CSV 缺少必要欄位：Bin / X / Y / Wafer_ID");

        // 良率測項索引
        var yieldIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var yp in HeatmapConfig.YieldParams)
        {
            int i = Idx(result.Headers, yp);
            if (i >= 0) yieldIdx[yp] = i;
        }

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = line.Split(',');

            if (!int.TryParse(Get(cols, idxBin),   out int bin))  continue;
            if (!int.TryParse(Get(cols, idxX),     out int x))    continue;
            if (!int.TryParse(Get(cols, idxY),     out int y))    continue;
            if (!int.TryParse(Get(cols, idxStrip), out int sid))  continue;

            if (!result.Strips.ContainsKey(sid))
                result.Strips[sid] = new List<DieRecord>();
            result.Strips[sid].Add(new DieRecord { Bin = bin, X = x, Y = y });

            var fd = new FullDieRecord { WaferId = sid, X = x, Y = y };
            foreach (var kv in yieldIdx)
            {
                bool ok = double.TryParse(Get(cols, kv.Value),
                    NumberStyles.Any, CultureInfo.InvariantCulture, out double v);
                fd.Params[kv.Key] = (ok, ok ? v : 0);
            }
            result.FullDies.Add(fd);
        }

        return result;
    }

    // ── 補充單一參數的數值（Render 前呼叫）───────────────────────────────────
    public async Task LoadParamValuesAsync(
        Stream stream,
        string paramName,
        Dictionary<int, List<DieRecord>> strips,
        List<string> headers)
    {
        int idxParam = Idx(headers, paramName);
        int idxX     = Idx(headers, "X");
        int idxY     = Idx(headers, "Y");
        int idxStrip = Idx(headers, "Wafer_ID");
        if (idxParam < 0) return;

        // 建立快速查找表
        var lookups = strips.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.ToDictionary(d => (d.X, d.Y)));

        using var reader = new StreamReader(stream);
        await reader.ReadLineAsync(); // skip header

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = line.Split(',');
            if (!int.TryParse(Get(cols, idxX),     out int x))   continue;
            if (!int.TryParse(Get(cols, idxY),     out int y))   continue;
            if (!int.TryParse(Get(cols, idxStrip), out int sid)) continue;
            if (!lookups.TryGetValue(sid, out var lu))            continue;
            if (!lu.TryGetValue((x, y), out var die))             continue;

            if (double.TryParse(Get(cols, idxParam),
                NumberStyles.Any, CultureInfo.InvariantCulture, out double v))
            { die.Value = v; die.HasValue = true; }
        }
    }

    private static int Idx(List<string> h, string name) =>
        h.FindIndex(s => s.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static string Get(string[] cols, int i) =>
        i >= 0 && i < cols.Length ? cols[i].Trim() : "";
}
