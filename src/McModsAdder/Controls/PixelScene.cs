using System.Windows;
using System.Windows.Media;

namespace MCModPlus.Controls;

public sealed class PixelScene : FrameworkElement
{
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0) return;

        const double unit = 8;
        var sky = new SolidColorBrush(Color.FromRgb(0x1A, 0x2B, 0x35));
        drawingContext.DrawRectangle(sky, null, new Rect(0, 0, width, height));

        DrawRect(drawingContext, Color.FromRgb(0x27, 0x46, 0x4A), 0, 0, width, height * .48, unit);
        DrawCloud(drawingContext, width * .10, height * .18, unit);
        DrawCloud(drawingContext, width * .68, height * .10, unit);

        var horizon = height * .50;
        DrawRect(drawingContext, Color.FromRgb(0x3E, 0x69, 0x3B), 0, horizon, width, unit * 3, unit);
        DrawRect(drawingContext, Color.FromRgb(0x2B, 0x3A, 0x2C), 0, horizon + unit * 3, width, height - horizon, unit);

        DrawMountain(drawingContext, width * .05, horizon, unit, 12, Color.FromRgb(0x1C, 0x2A, 0x31));
        DrawMountain(drawingContext, width * .58, horizon, unit, 16, Color.FromRgb(0x20, 0x31, 0x35));
        DrawTree(drawingContext, width * .78, horizon - unit * 7, unit);
        DrawTree(drawingContext, width * .25, horizon - unit * 5, unit);
        DrawWorkbench(drawingContext, width * .44, horizon - unit * 5, unit);

        DrawRect(drawingContext, Color.FromRgb(0x73, 0x9B, 0x4B), 0, horizon, width, unit, unit);
        DrawRect(drawingContext, Color.FromRgb(0x5A, 0x7D, 0x3A), 0, horizon + unit, width, unit, unit);
    }

    private static void DrawRect(DrawingContext dc, Color color, double x, double y, double w, double h, double unit)
    {
        var brush = new SolidColorBrush(color);
        dc.DrawRectangle(brush, null, new Rect(Snap(x, unit), Snap(y, unit), Math.Max(unit, Snap(w, unit)), Math.Max(unit, Snap(h, unit))));
    }

    private static double Snap(double value, double unit) => Math.Floor(value / unit) * unit;

    private static void DrawCloud(DrawingContext dc, double x, double y, double u)
    {
        DrawRect(dc, Color.FromRgb(0xB3, 0xD0, 0xC6), x, y, u * 8, u * 2, u);
        DrawRect(dc, Color.FromRgb(0xB3, 0xD0, 0xC6), x + u * 2, y - u, u * 4, u, u);
        DrawRect(dc, Color.FromRgb(0xD2, 0xE2, 0xD5), x + u * 3, y - u, u * 2, u, u);
    }

    private static void DrawMountain(DrawingContext dc, double x, double baseY, double u, int peak, Color color)
    {
        for (var row = 0; row < peak; row++)
        {
            var span = (peak - row) * u * 2;
            DrawRect(dc, color, x + row * u * 1.4, baseY - row * u, span, u, u);
        }
    }

    private static void DrawTree(DrawingContext dc, double x, double baseY, double u)
    {
        DrawRect(dc, Color.FromRgb(0x6B, 0x4A, 0x31), x + u * 3, baseY + u * 5, u * 2, u * 8, u);
        DrawRect(dc, Color.FromRgb(0x2D, 0x62, 0x3B), x, baseY + u * 2, u * 8, u * 5, u);
        DrawRect(dc, Color.FromRgb(0x3E, 0x7A, 0x43), x + u * 2, baseY, u * 5, u * 3, u);
        DrawRect(dc, Color.FromRgb(0x23, 0x50, 0x35), x + u * 5, baseY + u * 4, u * 3, u * 2, u);
    }

    private static void DrawWorkbench(DrawingContext dc, double x, double y, double u)
    {
        DrawRect(dc, Color.FromRgb(0x9B, 0x6B, 0x3E), x, y, u * 8, u * 6, u);
        DrawRect(dc, Color.FromRgb(0xD0, 0x9A, 0x5B), x + u, y + u, u * 6, u, u);
        DrawRect(dc, Color.FromRgb(0x5B, 0x3A, 0x28), x + u * 3, y + u * 2, u * 2, u * 4, u);
        DrawRect(dc, Color.FromRgb(0x5B, 0x3A, 0x28), x + u, y + u * 3, u * 6, u, u);
    }
}
