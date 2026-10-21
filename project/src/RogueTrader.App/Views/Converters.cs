using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RogueTrader.App.Views;

public sealed class BoolToBadgeConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        value is true ? (string?)p ?? "спец." : "";

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Показывает элемент только когда значение истинно (или наоборот).</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        var b = value is bool v && v;
        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

public sealed class BoolToBrushConverter : IValueConverter
{
    public string TrueColor { get; set; } = "#3F8F4F";
    public string FalseColor { get; set; } = "#C0392B";

    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        var ok = value is bool v && v;
        var hex = ok ? TrueColor : FalseColor;
        return new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!);
    }

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

public sealed class CountToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        var n = value is int i ? i : (value is System.Collections.ICollection col ? col.Count : 0);
        var vis = n > 0;
        if (Invert) vis = !vis;
        return vis ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) => value is not bool b || !b;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => v is not bool b || !b;
}

/// <summary>
/// Показывает элемент, только если строка не пустая. Пустая строка и null
/// прячут подпись (например, описание или бонусы варианта пути).
/// </summary>
public sealed class StringVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        value is string s && !string.IsNullOrWhiteSpace(s) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>
/// Обрезает длинный текст до заданного числа символов (параметр — предел),
/// чтобы описание из книги не занимало весь экран. Обрезанный текст
/// помечается многоточием.
/// </summary>
public sealed class ShortTextConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        var text = value as string ?? "";
        var max = 300;
        if (p is not null && int.TryParse(p.ToString(), out var m) && m > 20) max = m;
        text = text.Trim();
        if (text.Length <= max) return text;
        var cut = text.LastIndexOf(' ', Math.Min(max, text.Length - 1));
        if (cut < max / 2) cut = max;
        return text[..cut].TrimEnd(' ', ',', ';', '—', '-') + "…";
    }

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}