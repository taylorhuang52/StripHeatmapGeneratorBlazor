using System.Globalization;
using System.Text;

namespace StripHeatmapGeneratorBlazor.Services;

/// <summary>
/// XLS → CSV 轉換器（純 C# OLE/BIFF8，無需 SkiaSharp 或 Interop）
///
/// 轉換規則（從 LoggerData sheet，第 10 行起）：
///   XLS 欄位           → CSV 欄位
///   Serial (col 0)     → Serial
///   Bin    (col 1)     → Bin
///   X      (col 2)     → X_old（原始 die 序號 1~169）
///                         CSV X = (X_old-1) / 13 + 1
///                         CSV Y = 13 - ((X_old-1) % 13)
///   Y      (col 3)     → Y_old = 0（固定）
///   VTH    (col 4)     → VTH
///   ...其餘欄位直接對應...
///   Remark (col 16)    → Wafer_ID（取最後 2 位數字）
/// </summary>
public class XlsConverterService
{
    public record ConvertResult(string Csv, string Log, int Converted, int Skipped);

    // ── Public entry point ────────────────────────────────────────────────
    public Task<ConvertResult> ConvertAsync(byte[] xlsBytes)
        => Task.Run(() => DoConvert(xlsBytes));

    // ── Main conversion logic ─────────────────────────────────────────────
    private ConvertResult DoConvert(byte[] raw)
    {
        var log = new StringBuilder();
        void Log(string msg) { log.AppendLine(msg); }

        Log("步驟 1/3：解析 OLE Compound Document…");
        byte[]? wbStream = ExtractWorkbookStream(raw);
        if (wbStream is null)
            return new ConvertResult("", "無法解析 OLE Compound Document", 0, 0);
        Log($"  ✓ Workbook stream：{wbStream.Length:N0} bytes");

        Log("步驟 2/3：解析 LoggerData 分頁…");
        var sst   = new List<string>();
        var sheets = new List<(string Name, int Offset)>();
        ScanBiff8(wbStream, sst, sheets);
        Log($"  ✓ SST：{sst.Count} 字串，分頁：{string.Join(", ", sheets.Select(s => s.Name))}");

        var ldSheet = sheets.FirstOrDefault(s =>
            s.Name.Equals("LoggerData", StringComparison.OrdinalIgnoreCase));
        if (ldSheet.Name is null)
            return new ConvertResult("", "找不到 LoggerData 分頁", 0, 0);

        var allRows = ParseSheetRows(wbStream, ldSheet.Offset, sst);
        Log($"  ✓ 讀到 {allRows.Count} 行");

        Log("步驟 3/3：產生 CSV…");
        var csv = new StringBuilder();
        csv.AppendLine(
            "Serial,Bin,X,Y,X_old,Y_old," +
            "VTH,GET VTH <1.5V,GET VTH >6V," +
            "VFSD,BVDSS_2,BVDSS_1,BVDSS bin2,Delta3,IPD_10V," +
            "IDSS1,IDSS2,IDSS3,Wafer_ID");

        int converted = 0, skipped = 0;

        // Data rows start at index 10 (0-based), rows 0-8 = stats/limits, row 9 = header
        foreach (var rowIdx in allRows.Keys.OrderBy(k => k))
        {
            if (rowIdx < 10) continue;
            var row = allRows[rowIdx];

            // Remark (col 16) must exist and contain wafer info
            string remark = GetStr(row, 16).Trim();
            if (string.IsNullOrWhiteSpace(remark)) { skipped++; continue; }

            // Wafer_ID = last 2 numeric chars of remark
            // e.g. "T126043028A 0103" → "03" → 3
            string waferPart = remark.Length >= 2 ? remark[^2..] : remark;
            if (!int.TryParse(waferPart, out int waferId)) { skipped++; continue; }

            int serial  = (int)GetDbl(row, 0);
            int bin     = (int)GetDbl(row, 1);
            int xOld    = (int)GetDbl(row, 2);   // die position 1..169
            int yOld    = 0;

            if (xOld <= 0) { skipped++; continue; }

            // Coordinate conversion: 13×13 grid, column-major
            int csvX = (xOld - 1) / 13 + 1;        // 1..13
            int csvY = 13 - ((xOld - 1) % 13);      // 13..1

            string vth    = FmtD(row, 4);
            string gtvLo  = FmtD(row, 5);
            string gtvHi  = FmtD(row, 6);
            string vfsd   = FmtD(row, 7);
            string bv2    = FmtD(row, 8);
            string bv1    = FmtD(row, 9);
            string bvBin2 = FmtD(row, 10);
            string delta  = FmtD(row, 11);
            string ipd    = FmtD(row, 12);
            string idss1  = FmtD(row, 13);
            string idss2  = FmtD(row, 14);
            string idss3  = FmtD(row, 15);

            csv.AppendLine(
                $"{serial},{bin},{csvX},{csvY},{xOld},{yOld}," +
                $"{vth},{gtvLo},{gtvHi}," +
                $"{vfsd},{bv2},{bv1},{bvBin2},{delta},{ipd}," +
                $"{idss1},{idss2},{idss3},{waferId}");

            converted++;
        }

        Log($"✅ 完成！共 {converted} 筆，略過 {skipped} 筆");
        return new ConvertResult(csv.ToString(), log.ToString(), converted, skipped);
    }

    // ── Cell helpers ──────────────────────────────────────────────────────
    private static double GetDbl(Dictionary<int, object> row, int col)
        => row.TryGetValue(col, out var v) && v is double d ? d : 0;

    private static string GetStr(Dictionary<int, object> row, int col)
        => row.TryGetValue(col, out var v) ? v?.ToString() ?? "" : "";

    private static string FmtD(Dictionary<int, object> row, int col)
        => row.TryGetValue(col, out var v) && v is double d
            ? d.ToString("G7", CultureInfo.InvariantCulture) : "";

    // ══════════════════════════════════════════════════════════════════════
    //  OLE2 Compound Document → Workbook stream
    // ══════════════════════════════════════════════════════════════════════
    private static byte[]? ExtractWorkbookStream(byte[] raw)
    {
        if (raw.Length < 512) return null;
        // Magic check
        if (raw[0] != 0xD0 || raw[1] != 0xCF) return null;

        int secSz = 1 << BitConverter.ToInt16(raw, 30);

        // Build FAT
        var fatSecs = new List<int>();
        for (int i = 0; i < 109; i++)
        {
            int sec = BitConverter.ToInt32(raw, 76 + i * 4);
            if (sec < 0) break;
            fatSecs.Add(sec);
        }
        int fatLen = fatSecs.Count * secSz / 4;
        var fat = new int[fatLen];
        for (int fi = 0; fi < fatSecs.Count; fi++)
        {
            int off = 512 + fatSecs[fi] * secSz;
            for (int j = 0; j < secSz / 4 && fi * secSz / 4 + j < fatLen; j++)
                fat[fi * secSz / 4 + j] = BitConverter.ToInt32(raw, off + j * 4);
        }

        // Find Workbook entry in directory
        int dirSec = BitConverter.ToInt32(raw, 48);
        int wbStart = -1;
        for (int sec = dirSec; sec >= 0 && sec < 0x7FFFFFFF; )
        {
            int secOff = 512 + sec * secSz;
            for (int e = 0; e < secSz / 128; e++)
            {
                int entOff  = secOff + e * 128;
                if (entOff + 128 > raw.Length) break;
                int nameLen = BitConverter.ToInt16(raw, entOff + 64);
                int entType = raw[entOff + 66];
                if (entType == 0 || nameLen <= 0 || nameLen > 64) continue;
                string name = Encoding.Unicode.GetString(raw, entOff, nameLen).TrimEnd('\0');
                if (name.Equals("Workbook", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Book",     StringComparison.OrdinalIgnoreCase))
                {
                    wbStart = BitConverter.ToInt32(raw, entOff + 116);
                    break;
                }
            }
            if (wbStart >= 0) break;
            if (sec >= fat.Length) break;
            sec = fat[sec];
        }
        if (wbStart < 0) return null;

        // Read sector chain
        var ms = new MemoryStream();
        var visited = new HashSet<int>();
        for (int cur = wbStart; cur >= 0 && cur < 0x7FFFFFFF; )
        {
            if (!visited.Add(cur)) break;
            int off = 512 + cur * secSz;
            if (off + secSz > raw.Length) break;
            ms.Write(raw, off, secSz);
            if (cur >= fat.Length) break;
            cur = fat[cur];
        }
        return ms.ToArray();
    }

    // ══════════════════════════════════════════════════════════════════════
    //  BIFF8 scanner – builds SST and sheet directory
    // ══════════════════════════════════════════════════════════════════════
    private static void ScanBiff8(byte[] wb,
        List<string> sst, List<(string Name, int Offset)> sheets)
    {
        int pos = 0;
        while (pos + 4 <= wb.Length)
        {
            int rt = BitConverter.ToUInt16(wb, pos);
            int rl = BitConverter.ToUInt16(wb, pos + 2);

            // SST (0x00FC)
            if (rt == 0x00FC && rl >= 8)
            {
                int unique = BitConverter.ToInt32(wb, pos + 4 + 4);
                int sp = pos + 4 + 8;
                int end = pos + 4 + rl;
                while (sst.Count < unique && sp + 3 <= end)
                {
                    int cc  = BitConverter.ToUInt16(wb, sp);
                    byte fl = sp + 2 < wb.Length ? wb[sp + 2] : (byte)0;
                    sp += 3;
                    int richCount = 0;
                    if ((fl & 0x08) != 0 && sp + 2 <= wb.Length)
                    { richCount = BitConverter.ToUInt16(wb, sp); sp += 2; }
                    if ((fl & 0x04) != 0 && sp + 4 <= wb.Length)
                    { int phSz = BitConverter.ToInt32(wb, sp); sp += 4 + phSz; }

                    int bytesPerChar = ((fl & 0x01) == 0) ? 1 : 2;
                    int needed = cc * bytesPerChar;
                    if (sp + needed > wb.Length) break;
                    byte[] buf = new byte[needed];
                    Buffer.BlockCopy(wb, sp, buf, 0, needed);
                    sst.Add(bytesPerChar == 1
                        ? Encoding.Latin1.GetString(buf)
                        : Encoding.Unicode.GetString(buf));
                    sp += needed + richCount * 4;
                }
            }

            // BOUNDSHEET (0x0085)
            if (rt == 0x0085 && rl >= 8)
            {
                int shOff   = BitConverter.ToInt32(wb, pos + 4);
                int nameLen = pos + 4 + 6 < wb.Length ? wb[pos + 4 + 6] : 0;
                byte enc    = pos + 4 + 7 < wb.Length ? wb[pos + 4 + 7] : (byte)0;
                int nameStart = pos + 4 + 8;
                string shName = "";
                if (nameLen > 0 && nameStart + nameLen * (enc == 0 ? 1 : 2) <= wb.Length)
                    shName = enc == 0
                        ? Encoding.Latin1.GetString(wb, nameStart, nameLen)
                        : Encoding.Unicode.GetString(wb, nameStart, nameLen * 2);
                sheets.Add((shName.TrimEnd('\0'), shOff));
            }

            pos += 4 + rl;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  BIFF8 sheet row parser
    //  Returns: rowIndex → { colIndex → object (double or string) }
    // ══════════════════════════════════════════════════════════════════════
    private static Dictionary<int, Dictionary<int, object>> ParseSheetRows(
        byte[] wb, int startOff, List<string> sst)
    {
        var rows = new Dictionary<int, Dictionary<int, object>>();
        void Set(int r, int c, object v)
        {
            if (!rows.TryGetValue(r, out var d)) rows[r] = d = new Dictionary<int, object>();
            d[c] = v;
        }

        int pos = startOff;
        bool started = false;
        while (pos + 4 <= wb.Length)
        {
            int rt = BitConverter.ToUInt16(wb, pos);
            int rl = BitConverter.ToUInt16(wb, pos + 2);

            if (rt == 0x0809) started = true;   // BOF
            else if (rt == 0x000A) break;        // EOF

            if (started)
            {
                // NUMBER (0x0203): row(2) col(2) xf(2) value(8) → value at pos+10
                if (rt == 0x0203 && pos + 4 + rl <= wb.Length && rl >= 14)
                    Set(BitConverter.ToUInt16(wb, pos + 4),
                        BitConverter.ToUInt16(wb, pos + 6),
                        BitConverter.ToDouble(wb, pos + 10));

                // LABELSST (0x00FD)
                else if (rt == 0x00FD && pos + 4 + rl <= wb.Length && rl >= 10)
                {
                    int si = BitConverter.ToInt32(wb, pos + 4 + 6);
                    Set(BitConverter.ToUInt16(wb, pos + 4),
                        BitConverter.ToUInt16(wb, pos + 6),
                        si < sst.Count ? (object)sst[si] : "");
                }

                // RK (0x027E)
                else if (rt == 0x027E && pos + 4 + rl <= wb.Length && rl >= 10)
                    Set(BitConverter.ToUInt16(wb, pos + 4),
                        BitConverter.ToUInt16(wb, pos + 6),
                        DecodeRK(BitConverter.ToInt32(wb, pos + 10)));

                // MULRK (0x00BD)
                else if (rt == 0x00BD && pos + 4 + rl <= wb.Length && rl >= 6)
                {
                    int r      = BitConverter.ToUInt16(wb, pos + 4);
                    int cFirst = BitConverter.ToUInt16(wb, pos + 6);
                    int n      = (rl - 6) / 6;
                    for (int i = 0; i < n; i++)
                    {
                        int rkOff = pos + 4 + 4 + i * 6 + 2;
                        if (rkOff + 4 <= wb.Length)
                            Set(r, cFirst + i, DecodeRK(BitConverter.ToInt32(wb, rkOff)));
                    }
                }
            }

            pos += 4 + rl;
        }
        return rows;
    }

    private static double DecodeRK(int rkv)
    {
        double v = (rkv & 0x02) != 0
            ? (rkv >> 2)
            : BitConverter.Int64BitsToDouble(((long)(rkv & 0xFFFFFFFC)) << 32);
        if ((rkv & 0x01) != 0) v /= 100.0;
        return v;
    }
}
