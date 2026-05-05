namespace StripHeatmapGeneratorBlazor.Models;

public class DieRecord
{
    public int    Bin      { get; set; }
    public int    X        { get; set; }
    public int    Y        { get; set; }
    public double Value    { get; set; }
    public bool   HasValue { get; set; }
}
