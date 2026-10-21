using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows;

namespace RogueTrader.App.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    /// <summary>Обёртка для коллекций — вызывает OnPropertyChanged.</summary>
    protected ObservableCollection<T> New<T>() => [];
}

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _exec;
    private readonly Func<object?, bool>? _can;

    public RelayCommand(Action<object?> exec, Func<object?, bool>? can = null)
    {
        _exec = exec;
        _can = can;
    }

    public RelayCommand(Action exec, Func<bool>? can = null)
    {
        _exec = _ => exec();
        _can = can is null ? null : _ => can();
    }

    public bool CanExecute(object? parameter) => _can?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _exec(parameter);

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}

/// <summary>
/// Пункт для выпадающих списков: значение + подпись + оригинальный объект.
/// Дополнительно — описание и игромеханические бонусы: они показываются
/// прямо в раскрытом списке, чтобы вариант можно было оценить до выбора.
/// </summary>
public sealed class Choice<T>
{
    public required T Value { get; init; }
    public required string Label { get; init; }
    public string? Hint { get; init; }
    /// <summary>Описание варианта (как в книге).</summary>
    public string? Description { get; init; }
    /// <summary>Игромеханические бонусы — короткой строкой.</summary>
    public string? Bonuses { get; init; }
    /// <summary>Описание для раскрытого списка — обрезанное, чтобы пункт не вырастал.</summary>
    public string? ShortDescription => Trim(Description, 300);
    /// <summary>Бонусы для раскрытого списка — обрезанные.</summary>
    public string? ShortBonuses => Trim(Bonuses, 240);
    /// <summary>Есть ли что показать в списке.</summary>
    public bool HasDetails =>
        !string.IsNullOrWhiteSpace(Description) || !string.IsNullOrWhiteSpace(Bonuses);

    /// <summary>Обрезает текст по словам и добавляет многоточие.</summary>
    private static string? Trim(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim();
        if (t.Length <= max) return t;
        var cut = t.LastIndexOf(' ', Math.Min(max, t.Length - 1));
        if (cut < max / 2) cut = max;
        var head = t[..cut];
        // Не оставляем «хвост» из одной-двух букв («…варианты. В…») —
        // в том числе после переноса строки.
        int p = head.Length;
        while (p > 0 && !char.IsWhiteSpace(head[p - 1])) p--;
        if (p > 0 && head.Length - p < 3) head = head[..p];
        return head.TrimEnd(' ', ',', ';', '—', '-', '.', '\n', '\r') + "…";
    }

    public override string ToString() => Label;
}