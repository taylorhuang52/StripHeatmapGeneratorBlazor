namespace StripHeatmapGeneratorBlazor.Models;

public class FullDieRecord
{
    public int WaferId { get; set; }
    public int X       { get; set; }
    public int Y       { get; set; }

    public Dictionary<string, (bool HasValue, double Value)> Params { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
}
