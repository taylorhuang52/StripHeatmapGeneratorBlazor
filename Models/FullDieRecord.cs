namespace StripHeatmapGeneratorBlazor.Models;

public class FullDieRecord
{
    public int WaferId { get; set; }
    public int X       { get; set; }
    public int Y       { get; set; }

    /// <summary>key = 欄位名稱, value = (hasValue, value)</summary>
    public Dictionary<string, (bool HasValue, double Value)> Params { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
}
