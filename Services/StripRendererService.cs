using System.Globalization;
using System.Text.Json;
using StripHeatmapGeneratorBlazor.Models;

namespace StripHeatmapGeneratorBlazor.Services;

/// <summary>
/// SkiaSharp-free renderer.
/// BuildSpec() produces a JSON-serialisable description of every rect and text
/// to draw.  The JS function stripHeatmap.render(spec, canvasId) executes the
/// draw commands on an HTML canvas and returns a base64 PNG via toDataURL().
/// </summary>
public class StripRendererService
{
    // DTOs – must be JSON-serialisable for JS interop
    public record RectCmd(float X, float Y, float W, float H, string Fill);
    public record TextCmd(float X, float Y, string Text, string Fill,
                          float Size, bool Bold, bool Center);
    public record CanvasSpec(int Width, int Height,
                             List<RectCmd> Rects, List<TextCmd> Texts);

    public CanvasSpec BuildSpec(
        Dictionary<int, List<DieRecord>> strips,
        string paramName,
        string fileBase,
        double? loLimit,
        double? hiLimit,
        int cellW    = HeatmapConfig.DefaultCellW,
        int cellH    = HeatmapConfig.DefaultCellH,
        int fontSize = HeatmapConfig.DefaultFontSz)
    {
        int maxX = strips.Count > 0
            ? strips.Values.SelectMany(l => l).Max(d => d.X) : 13;
        int maxY = strips.Count > 0
            ? strips.Values.SelectMany(l => l).Max(d => d.Y) : 13;

        int stripW = maxX * cellW;
        int stripH = maxY * cellH;
        int rows = HeatmapConfig.GridRows;
        int cols = HeatmapConfig.GridCols;

        int totalW = HeatmapConfig.Margin * 2
                   + cols * stripW + (cols - 1) * HeatmapConfig.StripPadX;
        int totalH = HeatmapConfig.Margin * 2
                   + HeatmapConfig.TitleH
                   + rows * (HeatmapConfig.StripLabelH + stripH)
                   + (rows - 1) * HeatmapConfig.StripPadY;

        var rects = new List<RectCmd>();
        var texts = new List<TextCmd>();

        // White background
        rects.Add(new RectCmd(0, 0, totalW, totalH, HeatmapConfig.CssWhite));

        // Title (centred)
        string limitStr = (loLimit.HasValue || hiLimit.HasValue)
            ? $"  [{loLimit?.ToString("G4", CultureInfo.InvariantCulture) ?? "-∞"}" +
              $" ~ {hiLimit?.ToString("G4", CultureInfo.InvariantCulture) ?? "+∞"}]"
            : "";
        texts.Add(new TextCmd(totalW / 2f, HeatmapConfig.Margin + 22,
            $"{fileBase} {paramName} Strip Heatmap{limitStr}",
            HeatmapConfig.CssTitleFg, 15, Bold: true, Center: true));

        // Strips
        for (int col = 0; col < cols; col++)
        {
            for (int row = 0; row < rows; row++)
            {
                int sid = HeatmapConfig.StripGrid[col][row];
                int ox  = HeatmapConfig.Margin + col * (stripW + HeatmapConfig.StripPadX);
                int oy  = HeatmapConfig.Margin + HeatmapConfig.TitleH
                        + row * (HeatmapConfig.StripLabelH + stripH + HeatmapConfig.StripPadY);

                // Strip label
                texts.Add(new TextCmd(ox, oy + 11, $"#{sid}",
                    HeatmapConfig.CssBlack, 11, Bold: true, Center: false));
                oy += HeatmapConfig.StripLabelH;

                if (!strips.ContainsKey(sid)) continue;

                var lookup = strips[sid].ToDictionary(d => (d.X, d.Y));

                for (int dy = maxY; dy >= 1; dy--)
                {
                    for (int dx = 1; dx <= maxX; dx++)
                    {
                        float px = ox + (dx - 1) * cellW;
                        float py = oy + (maxY - dy) * cellH;
                        float rw = cellW - 1, rh = cellH - 1;

                        if (!lookup.TryGetValue((dx, dy), out var die))
                        {
                            rects.Add(new RectCmd(px, py, rw, rh, HeatmapConfig.CssWhite));
                            continue;
                        }

                        bool oos = die.HasValue
                            && ((loLimit.HasValue && die.Value < loLimit.Value)
                             || (hiLimit.HasValue && die.Value > hiLimit.Value));

                        string bg = oos ? HeatmapConfig.CssOutOfSpec
                                        : HeatmapConfig.BinCss(die.Bin);
                        rects.Add(new RectCmd(px, py, rw, rh, bg));

                        if (die.HasValue)
                        {
                            string val  = die.Value.ToString("G4", CultureInfo.InvariantCulture);
                            string fill = oos ? HeatmapConfig.CssOosText : HeatmapConfig.CssBlack;
                            // Centre horizontally in cell; JS will use measureText
                            texts.Add(new TextCmd(
                                px + rw / 2f,
                                py + rh / 2f + fontSize * 0.38f,
                                val, fill, fontSize, Bold: false, Center: true));
                        }
                    }
                }
            }
        }

        return new CanvasSpec(totalW, totalH, rects, texts);
    }
}
