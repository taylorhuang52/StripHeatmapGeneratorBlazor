namespace StripHeatmapGeneratorBlazor.Services;

public static class HeatmapConfig
{
    // ── Fixed Grid: 4 cols × 6 rows = 24 strip slots ─────────────────────
    public const int GridCols      = 4;
    public const int GridRows      = 6;
    public const int TotalStrips   = GridCols * GridRows;
    public const int StripDieTotal = 169;

    public static readonly int[][] StripGrid = BuildGrid();
    private static int[][] BuildGrid()
    {
        var g = new int[GridCols][];
        for (int c = 0; c < GridCols; c++)
        {
            g[c] = new int[GridRows];
            for (int r = 0; r < GridRows; r++)
                g[c][r] = c * GridRows + 1 + (GridRows - 1 - r);
        }
        return g;
    }

    // ── Spec limits ───────────────────────────────────────────────────────
    public static readonly Dictionary<string, (double Lo, double Hi)> ParamLimits
        = new(StringComparer.OrdinalIgnoreCase)
    {
        { "VTH",     (1.5,   6.0)  },
        { "VFSD",    (0.4,   0.9)  },
        { "BVDSS_2", (19.65, 23.0) },
        { "BVDSS_1", (19.65, 22.5) },
        { "Delta3",  (0.02,  2.0)  },
        { "IPD_10V", (20.0,  40.0) },
        { "IDSS1",   (0.0,   10.0) },
        { "IDSS2",   (0.0,   10.0) },
        { "IDSS3",   (0.0,   10.0) },
    };

    public static readonly HashSet<string> YieldParams
        = new(ParamLimits.Keys, StringComparer.OrdinalIgnoreCase);

    // ── Bin colours as plain (R,G,B) — no SkiaSharp dependency ───────────
    public static readonly Dictionary<int, (byte R, byte G, byte B)> BinColors = new()
    {
        { 1, (  0, 200,   0) },   // Pass    → green
        { 2, ( 50, 180,  50) },   // Pass2   → dark green
        { 3, (200, 200,   0) },   // Warn    → yellow
        { 4, (255, 128,   0) },   // Warn2   → orange
        { 5, (255,   0,   0) },   // Fail    → red
        { 6, (180,   0, 180) },   // Fail2   → purple
    };

    public static readonly Dictionary<int, string> BinLabels = new()
    {
        { 1, "Pass" }, { 2, "Pass2" }, { 3, "Warn" },
        { 4, "Warn2"}, { 5, "Fail" }, { 6, "Fail2" },
    };

    // CSS colour strings (used by renderer and legend)
    public static string BinCss(int bin)
        => BinColors.TryGetValue(bin, out var c) ? $"rgb({c.R},{c.G},{c.B})" : "#a0a0a0";

    public const string CssOutOfSpec = "rgb(210,210,210)";
    public const string CssOosText   = "rgb(160,0,0)";
    public const string CssTitleFg   = "#0050c8";
    public const string CssBlack     = "#000000";
    public const string CssWhite     = "#ffffff";
    public const string CssDefault   = "#a0a0a0";

    // ── Render defaults ───────────────────────────────────────────────────
    public const int DefaultCellW  = 28;
    public const int DefaultCellH  = 18;
    public const int DefaultFontSz = 8;
    public const int TitleH        = 40;
    public const int StripLabelH   = 18;
    public const int StripPadX     = 10;
    public const int StripPadY     = 14;
    public const int Margin        = 20;
}
