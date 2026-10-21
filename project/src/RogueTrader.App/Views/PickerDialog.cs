using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using RogueTrader.App.ViewModels;

namespace RogueTrader.App.Views;

/// <summary>
/// Элемент поискового диалога.
///
/// Тип намеренно НЕ обобщённый: <c>PickerItem&lt;T&gt;</c> инвариантен, и
/// <c>items.Cast&lt;PickerItem&lt;object?&gt;&gt;()</c> на списке <c>PickerItem&lt;SkillDef&gt;</c>
/// падал с «Specified cast is not valid» — то есть добавление умения,
/// таланта или предмета было невозможно.
/// </summary>
public sealed class PickerItem
{
    public object? Value { get; init; }
    public string Title { get; init; } = "";
    public string? Subtitle { get; init; }
    public string? Description { get; init; }
    public bool Advanced { get; init; }
    public bool Available { get; init; } = true;
    /// <summary>Идентификатор предмета в паке — по нему запоминается избранное.</summary>
    public string Id { get; init; } = "";
    /// <summary>Отмечен ли предмет как избранный (знак «★» в списке).</summary>
    public bool IsFavorite { get; set; }
    /// <summary>Раздел арсенала — по нему можно отсортировать список.</summary>
    public string SortGroup { get; init; } = "";
    /// <summary>Числовой ключ сортировки (вес, доступность и т. п.).</summary>
    public double SortNumber { get; init; }
}

/// <summary>
/// Фильтр окна подбора: подпись и варианты (первый вариант — «все»).
/// </summary>
public sealed class PickerFilter
{
    public string Label { get; init; } = "";
    public List<(string Name, Func<PickerItem, bool> Test)> Options { get; init; } = [];
}

/// <summary>Диалог выбора из большого списка с поиском — аналог выпадающих списков DnDBeyond.</summary>
public partial class PickerDialog : Window
{
    private readonly ListBox _list = null!;
    private readonly TextBox _search = null!;
    private readonly TextBlock _desc = null!;
    private readonly TextBlock _count = null!;
    private readonly ComboBox _sortBox = null!;
    private readonly CheckBox _favOnly = null!;
    private readonly Button _favButton = null!;
    private readonly List<PickerItem> _all;
    private readonly List<PickerFilter> _filters;
    private readonly List<ComboBox> _filterBoxes = [];


    public PickerDialog(string title, IEnumerable<PickerItem> items, IEnumerable<PickerFilter>? filters = null,
        string? initialSearch = null)
    {
        Title = title;
        // Окно центрируется по владельцу (главному окну) — WPF сам считает
        // положение в логических единицах, поэтому при любом масштабе
        // диалог попадает на экран и оказывается поверх главного окна.
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MinWidth = 560; MinHeight = 420;
        // Размер задаём сразу: иначе WPF меряет содержимое (а в списке
        // сотни предметов) и окно получается больше экрана — его видно
        // только краем, а открытие подвисает на разметке. Точный размер
        // под рабочую область уточняется при загрузке окна.
        Width = 880;
        Height = 630;
        Background = (Brush)Application.Current.Resources["BgBrush"];
        Foreground = (Brush)Application.Current.Resources["TextBrush"];
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 13;

        // Своя тёмная рамка окна — как у главного окна приложения:
        // системная белая плашка с заголовком не нужна.
        WindowStyle = WindowStyle.None;
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false,
        });

        _all = items.ToList();
        _filters = filters?.ToList() ?? [];

        // DockPanel: кнопки и описание прижаты к низу и всегда видны,
        // список занимает всё остальное место.
        var dock = new DockPanel { Margin = new Thickness(12, 10, 12, 12) };

        _search = new TextBox { FontSize = 14, Padding = new Thickness(8, 6, 8, 6) };
        _search.TextChanged += (_, _) => Filter();
        _search.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Down) { _list.SelectedIndex = Math.Min(_list.SelectedIndex + 1, _list.Items.Count - 1); _list.Focus(); e.Handled = true; }
            else if (e.Key == Key.Escape) { DialogResult = false; }
            else if (e.Key == Key.Enter && _list.SelectedItem is not null) { Accept(); }
        };
        DockPanel.SetDock(_search, Dock.Top);
        dock.Children.Add(_search);

        // Фильтры по виду: комбобоксы слева направо, каждый со своим списком.
        if (_filters.Count > 0)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            foreach (var f in _filters)
            {
                var box = new ComboBox { MinWidth = 190, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
                box.Items.Add("Все");
                foreach (var o in f.Options) box.Items.Add(o.Name);
                box.SelectedIndex = 0;
                box.SelectionChanged += (_, _) => Filter();
                _filterBoxes.Add(box);
                row.Children.Add(new TextBlock
                {
                    Text = f.Label, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center,
                    Foreground = (Brush)Application.Current.Resources["MutedBrush"], FontSize = 12,
                });
                row.Children.Add(box);
            }
            DockPanel.SetDock(row, Dock.Top);
            dock.Children.Add(row);
        }

        // Сортировка и «только избранное» — рядом с фильтрами.
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            row.Children.Add(new TextBlock
            {
                Text = "Сортировка", Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["MutedBrush"], FontSize = 12,
            });
            _sortBox = new ComboBox { MinWidth = 190, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
            _sortBox.Items.Add("По названию");
            _sortBox.Items.Add("По разделу арсенала");
            _sortBox.Items.Add("По весу");
            _sortBox.SelectedIndex = 0;
            _sortBox.SelectionChanged += (_, _) => Filter();
            row.Children.Add(_sortBox);
            _favOnly = new CheckBox
            {
                Content = "Только избранное",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                Foreground = (Brush)Application.Current.Resources["TextBrush"],
            };
            _favOnly.Checked += (_, _) => Filter();
            _favOnly.Unchecked += (_, _) => Filter();
            row.Children.Add(_favOnly);

            DockPanel.SetDock(row, Dock.Top);
            dock.Children.Add(row);
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        var ok = new Button { Content = "Выбрать", Style = (Style)Application.Current.Resources["GoldButton"], MinWidth = 110, Margin = new Thickness(0, 0, 8, 0) };
        ok.Click += (_, _) => Accept();
        var cancel = new Button { Content = "Отмена", MinWidth = 100 };
        cancel.Click += (_, _) => DialogResult = false;
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        DockPanel.SetDock(buttons, Dock.Bottom);
        dock.Children.Add(buttons);

        // Описание предмета: в окошке помещается не весь текст из книги,
        // поэтому его можно прокручивать.
        _desc = new TextBlock
        {
            Text = "", Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["MutedBrush"],
        };
        var descScroll = new ScrollViewer
        {
            MaxHeight = 92,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        // Прокрутка описания — плавная, как в остальных списках программы.
        SmoothScroll.SetEnabled(descScroll, true);

        // Описание кладём в панель сразу: элемент нельзя переносить из одного
        // контейнера в другой, если он уже стал его ребёнком.
        descScroll.Content = _desc;

        DockPanel.SetDock(descScroll, Dock.Bottom);
        dock.Children.Add(descScroll);

        _count = new TextBlock { Margin = new Thickness(0, 6, 0, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)Application.Current.Resources["MutedBrush"] };

        // Кнопка избранного — рядом со счётчиком: так она всегда на виду.
        _favButton = new Button
        {
            Content = "☆ В избранное",
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(12, 3, 0, 0),
            Padding = new Thickness(10, 3, 10, 3),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _favButton.Click += (_, _) => ToggleFavorite();

        var countRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 6, 0, 0),
        };
        countRow.Children.Add(_count);
        countRow.Children.Add(_favButton);
        DockPanel.SetDock(countRow, Dock.Bottom);
        dock.Children.Add(countRow);

        _list = new ListBox
        {
            Margin = new Thickness(0, 10, 0, 0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            // Границы обязательны: без них список меряется «в бесконечность»,
            // WPF выключает виртуализацию и создаёт контейнер для каждого
            // пункта — на полном арсенале (647 предметов) окно не открывается.
            MaxHeight = 430,
        };
        VirtualizingPanel.SetIsVirtualizing(_list, true);
        VirtualizingPanel.SetVirtualizationMode(_list, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(_list, true);
        _list.ItemTemplate = BuildTemplate();
        _list.SelectionChanged += (_, _) =>
        {
            if (_list.SelectedItem is PickerItem it)
            {
                _desc.Text = it.Description ?? "";
                UpdateFavButton(it);
            }
        };
        _list.MouseDoubleClick += (_, _) => { if (_list.SelectedItem is not null) Accept(); };
        dock.Children.Add(_list);

        Content = BuildShell(dock, title);
        if (!string.IsNullOrWhiteSpace(initialSearch)) _search.Text = initialSearch;
        Filter();
        Loaded += (_, _) =>
        {
            try
            {
                FitToWorkArea();
            }
            catch (Exception ex)
            {
                // Не срываем открытие окна из-за подгонки размера.
                System.Diagnostics.Debug.WriteLine(ex);
            }
            // Окно со своей рамкой WPF показывает позади владельца: поднимаем
            // его явно, иначе нажимаешь кнопку — а диалога не видно.
            ScreenHelper.BringToFront(this);
            _search.Focus();
        };
    }

    /// <summary>
    /// Подгоняет окно под рабочую область монитора.
    ///
    /// Кнопки «Выбрать» прижаты к низу окна, поэтому само окно должно
    /// помещаться в рабочую область (без панели задач) — иначе их не видно.
    /// </summary>
    private void FitToWorkArea()
    {
        var wa = ScreenHelper.WorkAreaInDips(this);
        if (wa.Width <= 0 || wa.Height <= 0) return;
        // Только размер: положение — по владельцу (CenterOwner).
        Width = Math.Min(880, Math.Max(560, wa.Width - 60));
        Height = Math.Min(660, Math.Max(420, wa.Height - 50));

        // WPF центрирует окно по размеру, который был задан ДО показа, а мы
        // задаём размер уже после. Поэтому ставим положение сами — иначе
        // диалог уезжает в угол экрана и наполовину уходит за его край.
        if (Owner is Window owner)
        {
            Left = owner.Left + (owner.ActualWidth - ActualWidth) / 2;
            Top = owner.Top + (owner.ActualHeight - ActualHeight) / 2;
        }
    }

    /// <summary>
    /// Рамка окна: своя тёмная полоса с заголовком и кнопкой закрытия
    /// (как у главного окна) + содержимое диалога.
    /// </summary>
    private UIElement BuildShell(UIElement content, string title)
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
        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["AccentBrush"],
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0),
        };
        captionGrid.Children.Add(titleText);
        captionGrid.Children.Add(close);

        var caption = new Border
        {
            Height = 34,
            Background = (Brush)Application.Current.Resources["PanelBrush"],
            BorderBrush = (Brush)Application.Current.Resources["BorderSoftBrush"],
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = captionGrid,
        };
        // Перетаскивание окна за полосу.
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

    private static DataTemplate BuildTemplate()
    {
        var xaml = """
        <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
          <Border Margin="0,1" Padding="8,6" CornerRadius="3"
                  Background="{StaticResource PanelBrush}">
            <!-- Подсказки с описанием здесь нет: описание и так показано
                 под списком, а всплывающее окно на каждый пункт (а их
                 сотни) заставляет WPF создавать сотни окон — из-за этого
                 открытие диалога подвисало на разметке. -->
            <Grid>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="Auto"/>
              </Grid.ColumnDefinitions>
              <StackPanel Grid.Column="0">
                <TextBlock Text="{Binding Title}" FontWeight="SemiBold"/>
                <TextBlock Text="{Binding Subtitle}" Foreground="{StaticResource MutedBrush}"
                           FontSize="11.5" TextWrapping="NoWrap"/>
              </StackPanel>
              <TextBlock Grid.Column="1" Text="★" FontSize="14" VerticalAlignment="Center"
                         Margin="0,0,8,0" Foreground="{StaticResource GoldBrush}"
                         Visibility="{Binding IsFavorite, Converter={StaticResource BoolVis}}"
                         ToolTip="В избранном: этот предмет всегда сверху"/>
              <Border Grid.Column="2" VerticalAlignment="Center" Margin="10,0,0,0"
                      CornerRadius="3" Padding="6,2"
                      Background="{StaticResource Panel2Brush}">
                <TextBlock Text="{Binding Advanced, Converter={StaticResource BoolToBadge}}"
                           FontSize="11" Foreground="{StaticResource AccentBrush}"/>
              </Border>
            </Grid>
          </Border>
        </DataTemplate>
        """;
        return (DataTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
    }

    /// <summary>
    /// Применяет поисковую строку и выбранные фильтры (вид снаряжения,
    /// класс оружия и т. п.). Фильтр с индексом 0 — «все».
    /// </summary>
    private void Filter()
    {
        // События сортировки и фильтров могут сработать раньше, чем создан
        // список (например, при добавлении первого пункта в выпадающий список).
        if (_list is null) return;
        var q = _search.Text.Trim();
        var tests = new List<Func<PickerItem, bool>>();
        for (var i = 0; i < _filterBoxes.Count && i < _filters.Count; i++)
        {
            var idx = _filterBoxes[i].SelectedIndex;
            if (idx <= 0) continue;
            var opt = _filters[i].Options[idx - 1];
            tests.Add(opt.Test);
        }
        IEnumerable<PickerItem> src = _all.Where(it =>
            (string.IsNullOrEmpty(q) ||
             it.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
             (it.Subtitle?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
             (it.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)) &&
            tests.All(t => t(it))
        );
        if (_favOnly.IsChecked == true) src = src.Where(it => it.IsFavorite);

        // Избранные — сверху, дальше порядок по выбранной сортировке.
        var items = src.OrderByDescending(it => it.IsFavorite).ToList();
        items = (_sortBox.SelectedIndex) switch
        {
            1 => items.OrderBy(it => it.SortGroup).ThenBy(it => it.Title).ToList(),
            2 => items.OrderBy(it => it.SortNumber).ThenBy(it => it.Title).ToList(),
            _ => items.OrderBy(it => it.Title).ToList(),
        };
        _list.ItemsSource = items;
        _count.Text = $"Показано {items.Count} из {_all.Count}";
        if (_list.Items.Count > 0) _list.SelectedIndex = 0;
    }

    /// <summary>Подпись кнопки избранного — по выбранному предмету.</summary>
    private void UpdateFavButton(PickerItem it)
    {
        var can = !string.IsNullOrEmpty(it.Id);
        _favButton.IsEnabled = can;
        _favButton.Content = can
            ? (it.IsFavorite ? "★ Убрать из избранного" : "☆ В избранное")
            : "☆ В избранное";
    }

    /// <summary>Отметить предмет избранным или убрать отметку.</summary>
    private void ToggleFavorite()
    {
        if (_list.SelectedItem is not PickerItem it || string.IsNullOrEmpty(it.Id)) return;
        var now = Services.Favorites.Toggle(it.Id);
        it.IsFavorite = now;
        UpdateFavButton(it);
        var keep = it;
        Filter();
        _list.SelectedItem = keep;   // выбор сохраняется после перерисовки
        UpdateFavButton(keep);
    }

    private void Accept()
    {
        if (_list.SelectedItem is PickerItem it) { Chosen = it.Value; DialogResult = true; }
    }

    public object? Chosen { get; private set; }

    public static bool Show(string title, IEnumerable<PickerItem> items, out object? picked)
    {
        return Show(title, items, out picked, null);
    }

    public static bool Show(string title, IEnumerable<PickerItem> items, out object? picked,
        IEnumerable<PickerFilter>? filters, string? initialSearch = null)
    {
        var dlg = new PickerDialog(title, items, filters, initialSearch);
        // Владелец — главное окно: без него диалог открывается позади него.
        var owner = Application.Current?.MainWindow;
        if (owner is not null && !ReferenceEquals(owner, dlg)) dlg.Owner = owner;
        var ok = dlg.ShowDialog() == true;
        picked = ok ? dlg.Chosen : null;
        return ok;
    }
}