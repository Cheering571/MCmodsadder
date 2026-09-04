using System.Windows;
using System.Windows.Media;

namespace MCModPlus.Controls;

public enum PixelIconKind
{
    GrassBlock,
    Workbench,
    Bookshelf,
    Lectern
}

public sealed class PixelIcon : FrameworkElement
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(PixelIconKind), typeof(PixelIcon), new FrameworkPropertyMetadata(PixelIconKind.GrassBlock, FrameworkPropertyMetadataOptions.AffectsRender));

    public PixelIconKind Kind { get => (PixelIconKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var unit = Math.Max(3, Math.Floor(Math.Min(ActualWidth, ActualHeight) / 12));
        var ox = (ActualWidth - unit * 12) / 2;
        var oy = (ActualHeight - unit * 12) / 2;
        var face = Kind switch
        {
            PixelIconKind.GrassBlock => new[] { "000888000000", "008888880000", "088888888000", "888888888800", "222222222220", "222222222220", "222222222220", "222222222220", "222222222220", "222222222220", "222222222220", "222222222220" },
            PixelIconKind.Workbench => new[] { "111111111111", "122121212121", "111111111111", "133331333331", "133331333331", "111111111111", "144441444441", "144441444441", "111111111111", "155551555551", "155551555551", "111111111111" },
            PixelIconKind.Bookshelf => new[] { "111111111111", "122233322233", "122233322233", "122233322233", "111111111111", "144455544455", "144455544455", "144455544455", "111111111111", "166677766677", "166677766677", "111111111111" },
            _ => new[] { "000111110000", "000111110000", "000111110000", "111111111111", "122222222221", "122222222221", "111111111111", "000111110000", "000111110000", "000111110000", "000111110000", "000111110000" }
        };
        foreach (var row in face.Select((line, y) => (line, y)))
        foreach (var cell in row.line.Select((key, x) => (key, x)))
            if (cell.key != '0') dc.DrawRectangle(new SolidColorBrush(ColorFor(cell.key)), null, new Rect(ox + cell.x * unit, oy + row.y * unit, unit, unit));
    }

    private static Color ColorFor(char key) => key switch
    {
        '1' => Color.FromRgb(0x5A, 0x3B, 0x27),
        '2' => Color.FromRgb(0x78, 0x4A, 0x2B),
        '3' => Color.FromRgb(0xC2, 0x86, 0x4D),
        '4' => Color.FromRgb(0x45, 0x2D, 0x4F),
        '5' => Color.FromRgb(0x88, 0x5C, 0x39),
        '6' => Color.FromRgb(0x31, 0x5C, 0x3A),
        '7' => Color.FromRgb(0xA3, 0x7B, 0x4D),
        _ => Color.FromRgb(0x6D, 0xB0, 0x4C)
    };
}
