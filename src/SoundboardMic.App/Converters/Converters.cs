using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SoundboardMic.App.Converters;

/// <summary>bool → Visibility (true = Visible). Parâmetro "invert" inverte.</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object? p, CultureInfo c)
    {
        var b = value is true;
        if (p as string == "invert") b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type t, object? p, CultureInfo c) =>
        value is Visibility.Visible;
}

/// <summary>string vazia/null → Collapsed; com conteúdo → Visible. Parâmetro "invert" inverte.</summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object? p, CultureInfo c)
    {
        var temConteudo = !string.IsNullOrWhiteSpace(value as string);
        if (p as string == "invert") temConteudo = !temConteudo;
        return temConteudo ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type t, object? p, CultureInfo c) =>
        Binding.DoNothing;
}

/// <summary>null → Collapsed; não-null → Visible. "invert" troca.</summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        var visible = value is not null;
        if (p as string == "invert") visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Inverte um bool.</summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type t, object? p, CultureInfo c) => value is not true;
    public object ConvertBack(object value, Type t, object? p, CultureInfo c) => value is not true;
}

/// <summary>Volume linear (0–2) → texto percentual ("120%").</summary>
public class VolumeToPercentConverter : IValueConverter
{
    public object Convert(object value, Type t, object? p, CultureInfo c) =>
        $"{System.Convert.ToDouble(value, c) * 100:0}%";

    public object ConvertBack(object value, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Code-point hex ("E8D6") → caractere do glifo do Segoe Fluent (null → padrão).</summary>
public class CodepointToGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        Services.IconCatalog.GlyphChar(value as string);

    public object ConvertBack(object value, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>"#RRGGBB" → SolidColorBrush congelado (cacheado); null/inválido → cor padrão do catálogo.</summary>
public class HexToBrushConverter : IValueConverter
{
    private static readonly Dictionary<string, SolidColorBrush> Cache = new(StringComparer.OrdinalIgnoreCase);

    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        var hex = value as string;
        if (string.IsNullOrWhiteSpace(hex))
            hex = Services.IconCatalog.CorPadrao;

        lock (Cache)
        {
            if (Cache.TryGetValue(hex, out var cached))
                return cached;
            try
            {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                brush.Freeze();
                Cache[hex] = brush;
                return brush;
            }
            catch (FormatException)
            {
                return Brushes.Gray;
            }
        }
    }

    public object ConvertBack(object value, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>bool → um de dois brushes ("BrushTrue|BrushFalse" nos recursos). Usado em status.</summary>
public class BoolToBrushConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type t, object? p, CultureInfo c)
    {
        var on = values.Length > 0 && values[0] is true;
        if (values.Length >= 3 && values[1] is Brush b1 && values[2] is Brush b2)
            return on ? b1 : b2;
        return Brushes.Gray;
    }

    public object[] ConvertBack(object value, Type[] t, object? p, CultureInfo c) =>
        throw new NotSupportedException();
}

/// <summary>true se os dois valores bindados forem iguais (strings, case-insensitive).</summary>
public class IsEqualConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type t, object? p, CultureInfo c)
    {
        if (values.Length < 2) return false;
        if (values[0] is string a && values[1] is string b)
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        return Equals(values[0], values[1]);
    }

    public object[] ConvertBack(object value, Type[] t, object? p, CultureInfo c) =>
        throw new NotSupportedException();
}

/// <summary>Largura do preenchimento do slider = (Value/Max) * TrackWidth.</summary>
public class SliderFillConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type t, object? p, CultureInfo c)
    {
        if (values.Length < 3
            || values[0] is not double value
            || values[1] is not double max
            || values[2] is not double width
            || max <= 0)
            return 0d;
        return Math.Clamp(value / max, 0, 1) * width;
    }

    public object[] ConvertBack(object value, Type[] t, object? p, CultureInfo c) =>
        throw new NotSupportedException();
}
