namespace StripHeatmapGeneratorBlazor.Models;

public class CsvLoadResult
{
    public Dictionary<int, List<DieRecord>> Strips      { get; set; } = new();
    public List<FullDieRecord>              FullDies    { get; set; } = new();
    public List<string>                     Headers     { get; set; } = new();
    public List<string>                     ParamColumns{ get; set; } = new();
}
