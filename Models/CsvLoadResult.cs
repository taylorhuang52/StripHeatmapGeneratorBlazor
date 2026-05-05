namespace StripHeatmapGeneratorBlazor.Models;

public class CsvLoadResult
{
    public Dictionary<int, List<DieRecord>> Strips   { get; set; } = new();
    public List<FullDieRecord>              FullDies  { get; set; } = new();
    public List<string>                     Headers   { get; set; } = new();

    /// <summary>可供選擇的參數欄位（排除 Serial/Bin/X/Y 等基本欄）</summary>
    public List<string> ParamColumns { get; set; } = new();
}
