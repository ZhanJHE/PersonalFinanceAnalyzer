using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PersonalFinanceAnalyzer.Helpers;

public class IntToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value is int count && count > 0) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class TypeDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "Income" => "收入",
            "Expense" => "支出",
            _ => value?.ToString() ?? ""
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class ColorCodeBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string colorCode)
        {
            try
            {
                var c = System.Windows.Media.ColorConverter.ConvertFromString(colorCode);
                if (c != null) return new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)c);
            }
            catch { }
        }
        return System.Windows.Media.Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class CategoryColorConverter : IValueConverter
{
    // 类别名 → 颜色映射，在 App 启动时从 Categories 表加载
    public static Dictionary<string, string> ColorMap { get; set; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string categoryName && ColorMap.TryGetValue(categoryName, out var color))
        {
            var c = System.Windows.Media.ColorConverter.ConvertFromString(color);
            if (c != null)
            {
                var clr = (System.Windows.Media.Color)c;
                if (parameter is string alphaStr && double.TryParse(alphaStr, out var alpha))
                {
                    clr = System.Windows.Media.Color.FromArgb((byte)(alpha * 255), clr.R, clr.G, clr.B);
                }
                return new System.Windows.Media.SolidColorBrush(clr);
            }
        }
        return System.Windows.Media.Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
