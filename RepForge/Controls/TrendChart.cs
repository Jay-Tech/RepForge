using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace RepForge.Controls;

public record ChartPoint(DateTime Date, double Value);

/// <summary>
/// Minimal themed line chart: gridlines, value/date labels, area fill, and dots.
/// Kept dependency-free on purpose — trend lines are all RepForge needs.
/// </summary>
public class TrendChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<ChartPoint>?> PointsProperty =
        AvaloniaProperty.Register<TrendChart, IReadOnlyList<ChartPoint>?>(nameof(Points));

    public static readonly StyledProperty<IBrush> LineBrushProperty =
        AvaloniaProperty.Register<TrendChart, IBrush>(nameof(LineBrush),
            new SolidColorBrush(Color.FromRgb(0xFF, 0x6A, 0x1A)));

    public static readonly StyledProperty<string> ValueFormatProperty =
        AvaloniaProperty.Register<TrendChart, string>(nameof(ValueFormat), "0.#");

    static TrendChart()
    {
        AffectsRender<TrendChart>(PointsProperty, LineBrushProperty, ValueFormatProperty);
    }

    public IReadOnlyList<ChartPoint>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public IBrush LineBrush
    {
        get => GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public string ValueFormat
    {
        get => GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.FromArgb(0x90, 0xFF, 0xFF, 0xFF));
    private static readonly Pen GridPen = new(new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)));

    public override void Render(DrawingContext ctx)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0)
            return;

        var points = Points;
        if (points is null || points.Count == 0)
        {
            DrawCentered(ctx, "No data yet", width, height);
            return;
        }

        const double left = 46, right = 10, top = 10, bottom = 24;
        var plotW = width - left - right;
        var plotH = height - top - bottom;
        if (plotW <= 10 || plotH <= 10)
            return;

        // Value range with padding; expand when flat so the line isn't glued to an edge.
        var minV = points.Min(p => p.Value);
        var maxV = points.Max(p => p.Value);
        if (Math.Abs(maxV - minV) < 0.0001)
        {
            minV -= 1;
            maxV += 1;
        }
        var pad = (maxV - minV) * 0.10;
        minV -= pad;
        maxV += pad;

        var minD = points.Min(p => p.Date);
        var maxD = points.Max(p => p.Date);
        var span = (maxD - minD).TotalDays;
        if (span < 0.001)
            span = 1; // single day: dot lands mid-plot

        double X(DateTime d) => span <= 0
            ? left + plotW / 2
            : left + plotW * ((d - minD).TotalDays / span);
        double Y(double v) => top + plotH * (1 - (v - minV) / (maxV - minV));

        // Horizontal gridlines + value labels
        for (var i = 0; i <= 3; i++)
        {
            var v = minV + (maxV - minV) * i / 3.0;
            var y = Y(v);
            ctx.DrawLine(GridPen, new Point(left, y), new Point(width - right, y));
            var label = Text(v.ToString(ValueFormat, CultureInfo.InvariantCulture));
            ctx.DrawText(label, new Point(left - label.Width - 6, y - label.Height / 2));
        }

        // Date labels: first and last
        var first = Text(minD.ToLocalTime().ToString("MMM d"));
        ctx.DrawText(first, new Point(left, height - bottom + 6));
        if (span > 0.5)
        {
            var last = Text(maxD.ToLocalTime().ToString("MMM d"));
            ctx.DrawText(last, new Point(width - right - last.Width, height - bottom + 6));
        }

        var ordered = points.OrderBy(p => p.Date).ToList();
        var px = ordered.Select(p => new Point(X(p.Date), Y(p.Value))).ToList();

        if (px.Count >= 2)
        {
            // Area fill under the line
            var area = new StreamGeometry();
            using (var g = area.Open())
            {
                g.BeginFigure(new Point(px[0].X, top + plotH), true);
                foreach (var p in px)
                    g.LineTo(p);
                g.LineTo(new Point(px[^1].X, top + plotH));
                g.EndFigure(true);
            }
            var lineColor = (LineBrush as ISolidColorBrush)?.Color ?? Color.FromRgb(0xFF, 0x6A, 0x1A);
            var fill = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0x50, lineColor.R, lineColor.G, lineColor.B), 0),
                    new GradientStop(Color.FromArgb(0x00, lineColor.R, lineColor.G, lineColor.B), 1),
                },
            };
            ctx.DrawGeometry(fill, null, area);

            // The line itself
            var line = new StreamGeometry();
            using (var g = line.Open())
            {
                g.BeginFigure(px[0], false);
                for (var i = 1; i < px.Count; i++)
                    g.LineTo(px[i]);
                g.EndFigure(false);
            }
            ctx.DrawGeometry(null, new Pen(LineBrush, 2, lineJoin: PenLineJoin.Round), line);
        }

        // Dots (skipped when dense)
        if (px.Count <= 40)
        {
            foreach (var p in px)
                ctx.DrawEllipse(LineBrush, null, p, 3, 3);
        }
    }

    private static FormattedText Text(string s) =>
        new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 11, LabelBrush);

    private static void DrawCentered(DrawingContext ctx, string message, double width, double height)
    {
        var text = Text(message);
        ctx.DrawText(text, new Point((width - text.Width) / 2, (height - text.Height) / 2));
    }
}
