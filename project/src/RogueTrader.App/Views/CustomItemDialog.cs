using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using RogueTrader.Core.Models;

namespace RogueTrader.App.Views;

/// <summary>
/// Диалог добавления своего предмета — лута, талисмана, подарка: того, чего
/// нет в официальном снаряжении. Заполненные поля сохраняются в персонаже
/// и учитываются в весе и защите.
/// </summary>
public sealed class CustomItemDialog : Window
{
    private readonly TextBox _name = null!;
    private readonly TextBox _desc = null!;
    private readonly ComboBox _category = null!;
    private readonly TextBox _weight = null!;
    private readonly TextBox _protection = null!;
    private readonly TextBox _note = null!;
    private readonly TextBlock _error = null!;

    /// <summary>Виды снаряжения, которые можно выбрать для своего предмета.</summary>
    private static readonly (string Id, string Name)[] Categories =
    [
        (ItemCategory.General, "Снаряжение (прочее)"),
        (ItemCategory.Personal, "Личные вещи"),
        (ItemCategory.Clothing, "Одежда и амуниция"),
        (ItemCategory.Tool, "Инструменты"),
        (ItemCategory.Consumable, "Препараты и вещества"),
        (ItemCategory.Armor, "Броня"),
        (ItemCategory.MeleeWeapon, "Холодное оружие"),
        (ItemCategory.RangedWeapon, "Стрелковое оружие"),
        (ItemCategory.WeaponUpgrade, "Улучшения оружия"),
        (ItemCategory.ArmorUpgrade, "Улучшения брони"),
        (ItemCategory.Drug, "Наркотики и яды"),
    ];

    public CustomItemDialog()
    {
        Title = "Свой предмет";
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 520;
        MinWidth = 460;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 760;
        Background = (Brush)Application.Current.Resources["BgBrush"];
        Foreground = (Brush)Application.Current.Resources["TextBrush"];
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 13;

        // Своя тёмная рамка окна — как у главного окна и окна подбора.
        WindowStyle = WindowStyle.None;
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false,
        });

        _name = new TextBox { FontSize = 13.5 };
        _desc = new TextBox
        {
            FontSize = 12.5,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 90,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        _category = new ComboBox { FontSize = 13 };
        foreach (var c in Categories) _category.Items.Add(c.Name);
        _category.SelectedIndex = 0;
        _weight = new TextBox { FontSize = 13 };
        _protection = new TextBox { FontSize = 13 };
        _note = new TextBox { FontSize = 12.5, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 58 };
        _error = new TextBlock
        {
            Foreground = (Brush)Application.Current.Resources["ErrorBrush"],
            FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0),
        };

        var form = new StackPanel { Margin = new Thickness(14, 12, 14, 12) };

        form.Children.Add(Header("Название"));
        form.Children.Add(_name);
        form.Children.Add(Header("Описание"));
        form.Children.Add(_desc);

        var row = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        var weightBox = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
        weightBox.Children.Add(Header("Вес, кг"));
        weightBox.Children.Add(_weight);
        Grid.SetColumn(weightBox, 0);
        row.Children.Add(weightBox);
        var protBox = new StackPanel();
        protBox.Children.Add(Header("Защита"));
        protBox.Children.Add(_protection);
        Grid.SetColumn(protBox, 1);
        row.Children.Add(protBox);
        form.Children.Add(row);

        form.Children.Add(Header("Вид снаряжения"));
        form.Children.Add(_category);

        form.Children.Add(Header("Заметка (что это даёт)"));
        form.Children.Add(_note);
        form.Children.Add(_error);

        var hint = new TextBlock
        {
            Text = "Свой предмет хранится в персонаже: он учитывается в весе и защите, "
                 + "но не берёт характеристик из книжных таблиц.",
            Foreground = (Brush)Application.Current.Resources["MutedBrush"],
            FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0),
        };
        form.Children.Add(hint);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
        };
        var ok = new Button
        {
            Content = "Добавить", Style = (Style)Application.Current.Resources["GoldButton"],
            MinWidth = 110, Margin = new Thickness(0, 0, 8, 0),
        };
        ok.Click += (_, _) => Accept();
        var cancel = new Button { Content = "Отмена", MinWidth = 100 };
        cancel.Click += (_, _) => DialogResult = false;
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        form.Children.Add(buttons);

        Content = BuildShell(form);
        Loaded += (_, _) => { FitToWorkArea(); ScreenHelper.BringToFront(this); _name.Focus(); };
    }

    private static TextBlock Header(string text) => new()
    {
        Text = text,
        Foreground = (Brush)Application.Current.Resources["MutedBrush"],
        FontSize = 11.5, Margin = new Thickness(0, 10, 0, 3),
    };

    /// <summary>Описание предмета, если пользователь нажал «Добавить».</summary>
    public ItemDef? Result { get; private set; }

    private void Accept()
    {
        var name = _name.Text.Trim();
        if (name.Length == 0)
        {
            _error.Text = "У предмета должно быть название.";
            _name.Focus();
            return;
        }
        var cat = Categories[Math.Max(0, _category.SelectedIndex)];
        Result = new ItemDef
        {
            Id = "",
            Name = name,
            Description = _desc.Text.Trim(),
            Note = _note.Text.Trim(),
            Category = cat.Id,
            Source = "custom",
            Encumbrance = ParseInt(_weight.Text),
            Protection = ParseInt(_protection.Text),
            Availability = "",
        };
        DialogResult = true;
    }

    private static int ParseInt(string text)
    {
        var t = text.Trim().Replace(',', '.');
        if (t.Length == 0) return 0;
        // Дробный вес («0,5») округляем вниз: груз считается целыми килограммами.
        if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return (int)Math.Floor(d);
        return 0;
    }

    /// <summary>Подгоняет окно под рабочую область монитора.</summary>
    private void FitToWorkArea()
    {
        var wa = ScreenHelper.WorkAreaInDips(this);
        if (wa.Width <= 0 || wa.Height <= 0) return;
        // Только размер: положение — по владельцу (CenterOwner).
        Height = Math.Min(ActualHeight > 0 ? ActualHeight : 600, Math.Max(300, wa.Height - 60));

        // Положение считаем сами: WPF центрирует окно по размеру, заданному
        // до показа, а размер здесь меняется уже после. Иначе диалог уезжает
        // в угол и наполовину уходит за край экрана.
        if (Owner is Window owner)
        {
            Left = owner.Left + (owner.ActualWidth - ActualWidth) / 2;
            Top = owner.Top + (owner.ActualHeight - ActualHeight) / 2;
        }
    }

    /// <summary>Рамка окна: своя тёмная полоса с заголовком и кнопкой закрытия.</summary>
    private UIElement BuildShell(UIElement content)
    {
        var close = new Button
        {
            Style = (Style)Application.Current.Resources["CaptionCloseButton"],
            ToolTip = "Закрыть",
            Content = new TextBlock { Text = "✕", FontSize = 13 },
        };
        close.Click += (_, _) => { DialogResult = false; };

        var captionGrid = new Grid();
        captionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        captionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        captionGrid.Children.Add(new TextBlock
        {
            Text = "Свой предмет",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["AccentBrush"],
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0),
        });
        captionGrid.Children.Add(close);

        var caption = new Border
        {
            Height = 34,
            Background = (Brush)Application.Current.Resources["PanelBrush"],
            BorderBrush = (Brush)Application.Current.Resources["BorderSoftBrush"],
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = captionGrid,
        };
        caption.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ChangedButton != MouseButton.Left || e.ClickCount == 2) return;
            try { DragMove(); } catch (InvalidOperationException) { }
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(caption, 0);
        root.Children.Add(caption);
        Grid.SetRow(content, 1);
        root.Children.Add(content);

        return new Border
        {
            BorderBrush = (Brush)Application.Current.Resources["BorderBrush"],
            BorderThickness = new Thickness(1),
            Child = root,
        };
    }

    /// <summary>Показывает диалог и возвращает заполненный предмет.</summary>
    public static ItemDef? Ask()
    {
        var dlg = new CustomItemDialog();
        // Владелец — главное окно: без него диалог открывается позади него.
        var owner = Application.Current?.MainWindow;
        if (owner is not null && !ReferenceEquals(owner, dlg)) dlg.Owner = owner;
        return dlg.ShowDialog() == true ? dlg.Result : null;
    }
}
