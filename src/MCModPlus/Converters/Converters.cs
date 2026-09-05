using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MCModPlus.Models;
using MCModPlus.Services;

namespace MCModPlus.Converters;

public class LoaderToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ModLoader loader
            ? loader switch
            {
                ModLoader.Fabric => new SolidColorBrush(Color.FromRgb(0xF3, 0xD0, 0x5A)),
                ModLoader.Forge => new SolidColorBrush(Color.FromRgb(0xF0, 0x78, 0x32)),
                ModLoader.Quilt => new SolidColorBrush(Color.FromRgb(0xB5, 0x7A, 0xED)),
                ModLoader.NeoForge => new SolidColorBrush(Color.FromRgb(0x55, 0xCF, 0x8A)),
                _ => new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80))
            }
            : new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class LoaderToBackgroundBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ModLoader loader
            ? loader switch
            {
                ModLoader.Fabric => new SolidColorBrush(Color.FromArgb(0x66, 0xF3, 0xD0, 0x5A)),
                ModLoader.Forge => new SolidColorBrush(Color.FromArgb(0x66, 0xF0, 0x78, 0x32)),
                ModLoader.Quilt => new SolidColorBrush(Color.FromArgb(0x66, 0xB5, 0x7A, 0xED)),
                ModLoader.NeoForge => new SolidColorBrush(Color.FromArgb(0x66, 0x55, 0xCF, 0x8A)),
                _ => new SolidColorBrush(Color.FromArgb(0x66, 0x6B, 0x72, 0x80))
            }
            : new SolidColorBrush(Color.FromArgb(0x66, 0x6B, 0x72, 0x80));

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

public class InstanceLoaderToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ModLoader loader
            ? loader == ModLoader.Unknown ? "原版" : loader.ToDisplay()
            : "原版";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class GroupHeaderTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ModLoader loader
            ? loader == ModLoader.Unknown ? "原版" : loader.ToDisplay()
            : value?.ToString() ?? "未知";

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

public class LocalThumbnailConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return DependencyProperty.UnsetValue;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = new MemoryStream(File.ReadAllBytes(path));
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return DependencyProperty.UnsetValue;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
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

