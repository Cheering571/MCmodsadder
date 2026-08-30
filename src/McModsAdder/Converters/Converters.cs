using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using McModsAdder.Models;
using McModsAdder.Services;

namespace McModsAdder.Converters;

public class LoaderToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ModLoader loader
            ? loader switch
            {
                ModLoader.Fabric => new SolidColorBrush(Color.FromRgb(0xDB, 0xD0, 0xA8)),
                ModLoader.Forge => new SolidColorBrush(Color.FromRgb(0x5F, 0x8F, 0xD8)),
                ModLoader.Quilt => new SolidColorBrush(Color.FromRgb(0x9B, 0x6D, 0xD3)),
                ModLoader.NeoForge => new SolidColorBrush(Color.FromRgb(0xE0, 0x86, 0x46)),
                _ => new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80))
            }
            : new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class LoaderToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ModLoader loader ? loader.ToDisplay() : "未知";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class GroupHeaderTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ModLoader loader ? loader.ToDisplay() : value?.ToString() ?? "未知";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class ComparisonStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ComparisonStatus status
            ? status switch
            {
                ComparisonStatus.Installed => new SolidColorBrush(Color.FromRgb(0x46, 0xD3, 0x69)),
                ComparisonStatus.Missing => new SolidColorBrush(Color.FromRgb(0xF0, 0xB2, 0x32)),
                _ => new SolidColorBrush(Color.FromRgb(0xE8, 0x11, 0x23))
            }
            : Brushes.Gray;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class ComparisonStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ComparisonStatus status
            ? status switch
            {
                ComparisonStatus.Installed => "已安装",
                ComparisonStatus.Missing => "缺失",
                _ => "无可用版本"
            }
            : "?";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class InstallStateToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int state
            ? state switch
            {
                1 => new SolidColorBrush(Color.FromRgb(0x3B, 0x9C, 0xDF)),
                2 => new SolidColorBrush(Color.FromRgb(0x46, 0xD3, 0x69)),
                3 => new SolidColorBrush(Color.FromRgb(0xE8, 0x11, 0x23)),
                _ => new SolidColorBrush(Color.FromRgb(0x8A, 0x90, 0x99))
            }
            : Brushes.Gray;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>bool ↔ ComboBox SelectedIndex（false=0 官方，true=1 镜像）</summary>
public class BoolToIndexConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b && b ? 1 : 0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int i && i == 1;
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b && !b;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b && !b;
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b && b ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class FriendlyNumberConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var n = value switch
        {
            long l => l,
            int i => (long)i,
            _ => 0L
        };
        return n switch
        {
            >= 100_000_000 => $"{n / 100_000_000.0:0.#} 亿",
            >= 10_000 => $"{n / 10_000.0:0.#} 万",
            _ => n.ToString()
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class AddedToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b && b ? "✓ 已添加" : "＋ 添加";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>int 计数 > 0 时可见</summary>
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int n && n > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class IdentifyMethodToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ModIdentifyMethod m
            ? m switch
            {
                ModIdentifyMethod.Hash => "精确",
                ModIdentifyMethod.Metadata => "元数据",
                _ => "未知"
            }
            : "未知";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
