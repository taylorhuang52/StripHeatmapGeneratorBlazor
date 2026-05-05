using StripHeatmapGeneratorBlazor.Models;

namespace StripHeatmapGeneratorBlazor.Services;

public class YieldCalculatorService
{
    public List<YieldResult> Calculate(List<FullDieRecord> fullDies)
    {
        var byStrip = fullDies
            .GroupBy(d => d.WaferId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var results = new List<YieldResult>();

        foreach (var kv in byStrip.OrderBy(k => k.Key))
        {
            int sid  = kv.Key;
            var dies = kv.Value;

            int failCount = dies.Count(die => IsFail(die));
            int missing   = Math.Max(0, HeatmapConfig.StripDieTotal - dies.Count);
            int totalFail = failCount + missing;
            int pass      = Math.Max(0, HeatmapConfig.StripDieTotal - totalFail);
            double yield  = (double)pass / HeatmapConfig.StripDieTotal * 100.0;

            results.Add(new YieldResult
            {
                StripId      = sid,
                PassCount    = pass,
                FailCount    = totalFail,
                MeasuredDies = dies.Count,
                Yield        = yield,
            });
        }
        return results;
    }

    private static bool IsFail(FullDieRecord die)
    {
        foreach (var yp in HeatmapConfig.YieldParams)
        {
            if (!die.Params.TryGetValue(yp, out var pv) || !pv.HasValue)
                return true;
            if (HeatmapConfig.ParamLimits.TryGetValue(yp, out var lim))
                if (pv.Value < lim.Lo || pv.Value > lim.Hi)
                    return true;
        }
        return false;
    }
}
