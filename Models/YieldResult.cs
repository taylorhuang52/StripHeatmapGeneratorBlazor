namespace StripHeatmapGeneratorBlazor.Models;

public class YieldResult
{
    public int    StripId      { get; set; }
    public int    PassCount    { get; set; }
    public int    FailCount    { get; set; }
    public int    MeasuredDies { get; set; }
    public double Yield        { get; set; }
}
