using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using RogueTrader.App.ViewModels;
using RogueTrader.App.Views;
using RogueTrader.Core.Models;

namespace RogueTrader.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DataContext = new MainViewModel();

        // Список снаряжения сгруппирован по главе «Арсенал», и новый предмет
        // часто оказывается внизу — без прокрутки его просто не видно.
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm) vm.ItemAddedToArsenal += ScrollArsenalTo;
        };

        // Размер задаём после создания окна: до этого Location/Size перезапишет
        // механизм WindowStartupLocation, и подгонка не сработает.
        Loaded += (_, _) =>
        {
            FitToWorkArea();
            // Масштаб применяем после первой раскладки окна. Если задать его
            // заранее, списки при первом показе прокручиваются не с начала:
            // прокрутка считается по размерам, которые ещё не рассчитаны.
            ApplyZoom(_zoom, save: false);
        };

        // Значок средней кнопки меняется вместе с состоянием окна.
        StateChanged += (_, _) => UpdateMaximizeIcon();

        // Масштаб интерфейса: восстанавливаем выбранный ранее (как в браузере).
        // В служебных режимах всегда 100% — иначе проверки зависят от настроек.
        _zoom = App.ServiceMode ? 1.0 : Services.UiSettings.LoadZoom();
    }

    // ─────────────────────── Масштаб интерфейса ───────────────────────
    // Принцип как в браузере и в Microsoft Office: меняется не размер окна,
    // а масштаб содержимого. При уменьшении в окно помещается больше деталей,
    // при увеличении — крупнее читаются описания.

    private const double MinZoom = 0.5;
    private const double MaxZoom = 2.0;
    private const double ZoomStep = 0.1;

    private double _zoom = 1.0;

    /// <summary>Текущий масштаб интерфейса (1 — обычный размер).</summary>
    public double CurrentZoom => _zoom;

    /// <summary>Текущий масштаб содержимого окна.</summary>
    private ScaleTransform? UiScale => RootGrid.LayoutTransform as ScaleTransform;

    private void OnZoomIn(object sender, RoutedEventArgs e) => ApplyZoom(_zoom + ZoomStep);

    private void OnZoomOut(object sender, RoutedEventArgs e) => ApplyZoom(_zoom - ZoomStep);

    private void OnZoomReset(object sender, RoutedEventArgs e) => ApplyZoom(1.0);

    /// <summary>
    /// Задаёт масштаб содержимого окна и обновляет подпись в процентах.
    /// Границы те же, что и в браузере: 50–200%.
    /// </summary>
    private void ApplyZoom(double value, bool save = true)
    {
        _zoom = Math.Round(Math.Clamp(value, MinZoom, MaxZoom), 2);

        if (UiScale is { } scale)
        {
            scale.ScaleX = _zoom;
            scale.ScaleY = _zoom;
        }

        // Округление раскладки включаем только при «ровном» масштабе.
        // При 110 %, 90 % и т. п. WPF округляет размеры в одном масштабе,
        // а рисует в другом, и каждый кадр прокрутки списка стоит вдвое
        // дороже — список начинает дёргаться. Без округления при таких
        // масштабах прокрутка вдвое дешевле, а разница в резкости незаметна.
        UseLayoutRounding = IsRoundZoom(_zoom);

        ZoomLevelText.Text = $"{Math.Round(_zoom * 100):0}%";
        ZoomOutButton.IsEnabled = _zoom > MinZoom + 0.001;
        ZoomInButton.IsEnabled = _zoom < MaxZoom - 0.001;

        if (save) Services.UiSettings.SaveZoom(_zoom);
    }

    /// <summary>
    /// Масштаб, при котором размеры остаются целыми или половинными:
    /// 50, 100, 150, 200 %. Остальные шаги дают дробные размеры.
    /// </summary>
    private static bool IsRoundZoom(double zoom) =>
        Math.Abs(zoom * 2 - Math.Round(zoom * 2)) < 0.001;

    /// <summary>
    /// Масштаб с клавиатуры: Ctrl+«+», Ctrl+«−», Ctrl+0 — как в браузере.
    /// Событие перехватывается на окне, поэтому сочетание не попадает
    /// в текстовое поле, в котором вводится имя персонажа.
    /// </summary>
    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
        switch (e.Key)
        {
            case Key.OemPlus:
            case Key.Add:
                ApplyZoom(_zoom + ZoomStep);
                e.Handled = true;
                break;
            case Key.OemMinus:
            case Key.Subtract:
                ApplyZoom(_zoom - ZoomStep);
                e.Handled = true;
                break;
            case Key.D0:
            case Key.NumPad0:
                ApplyZoom(1.0);
                e.Handled = true;
                break;
        }
    }

    /// <summary>Ctrl+колесо мыши меняет масштаб, как в браузере.</summary>
    private void OnWindowPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
        ApplyZoom(_zoom + (e.Delta > 0 ? ZoomStep : -ZoomStep));
        e.Handled = true;
    }

    private const double DesiredWidth = 1280;
    /// <summary>Высота окна: полоса с кнопками (34) плюс прежние 820 на содержимое.</summary>
    private const double DesiredHeight = 854;

    private MainViewModel Vm => (MainViewModel)DataContext;

    /// <summary>Показывает выбранный раздел, остальные прячет.</summary>

    /// <summary>Разделы, уже открытые: страница и её вкладка.</summary>
    /// <summary>Открывает окно с кратким руководством по созданию персонажа.</summary>
    private void OnShowGuide(object sender, RoutedEventArgs e)
    {
        // Руководство открывается с тем же масштабом, что и главное окно.
        var win = new Views.GuideWindow { Owner = this, Zoom = CurrentZoom };
        win.ShowDialog();
    }

    /// <summary>
    /// Прокручивает лист к разделу с указанным заголовком.
    ///
    /// Лист длинный (двадцать блоков), и без подсказки приходилось прокручивать
    /// его целиком. Метка раздела ищется в дереве страницы — так не приходится
    /// переименовывать блоки при правках разметки.
    /// </summary>
    private void OnJumpToSection(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string title }) return;
        if (FindTextBlock(this, title) is not FrameworkElement target) return;
        target.BringIntoView();
    }

    /// <summary>Ищет в дереве элемент с заданным текстом.</summary>
    private static TextBlock? FindTextBlock(DependencyObject root, string text)
    {
        if (root is TextBlock tb && tb.Text == text) return tb;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            if (FindTextBlock(VisualTreeHelper.GetChild(root, i), text) is { } found)
                return found;
        }
        return null;
    }

    /// <summary>Показывает добавленный предмет в списке снаряжения.</summary>
    private void ScrollArsenalTo(ItemEntry entry)
    {
        if (ArsenalList is null) return;
        var vm = DataContext as MainViewModel;
        if (vm is null) return;

        // Переключиться на вкладку «Снаряжение», если открыта другая.
        foreach (var tab in Tabs.Items.OfType<TabItem>())
            if (tab.Header as string == "Снаряжение") { Tabs.SelectedItem = tab; break; }

        var row = vm.Items.FirstOrDefault(r => ReferenceEquals(r.Entry, entry));
        if (row is null) return;
        ArsenalList.SelectedItem = row;
        ArsenalList.ScrollIntoView(row);
        // В сгруппированном списке ScrollIntoView не всегда срабатывает —
        // помогает прямой вызов контейнера элемента.
        if (ArsenalList.ItemContainerGenerator.ContainerFromItem(row) is ListBoxItem box)
            box.BringIntoView();
    }

    #region Позиционирование окна

    /// <summary>
    /// Подгоняет окно под рабочую область монитора (без панели задач).
    ///
    /// Важно: <see cref="SystemParameters.WorkArea"/> в .NET (Core) WPF отдаёт
    /// размеры в СЫРЫХ пикселях, а не в логических единицах. При масштабе 125–150 %
    /// окно из-за этого получалось шире экрана и уезжало за левый край.
    /// Поэтому берём физические пиксели у Win32 и делим на тот же коэффициент
    /// масштаба, которым WPF верстает (VisualTreeHelper.GetDpi).
    /// </summary>
    private void FitToWorkArea()
    {
        var wa = WorkAreaInDips();
        if (wa.Width <= 0 || wa.Height <= 0)
        {
            // Фолбэк на минимально безопасный размер, если HWND ещё недоступен
            Width = Math.Min(DesiredWidth, SystemParameters.VirtualScreenWidth);
            Height = Math.Min(DesiredHeight, SystemParameters.VirtualScreenHeight);
            return;
        }

        var w = Math.Max(480, Math.Min(DesiredWidth, wa.Width * 0.96));
        var h = Math.Max(380, Math.Min(DesiredHeight, wa.Height * 0.96));

        MinWidth = Math.Min(MinWidth, w);
        MinHeight = Math.Min(MinHeight, h);

        // MaxWidth/MaxHeight намеренно НЕ ставим: окно можно перетащить на
        // второй монитор побольше, и жёсткий MaxWidth из первого монитора
        // не дал бы там нормально развернуться.

        Width = w;
        Height = h;

        // При WindowStartupLocation="Manual" позицию никто не выставит —
        // без этого окно может открыться за нижним или левым краем экрана.
        Left = Math.Round(wa.Left + (wa.Width - w) / 2);
        Top = Math.Round(wa.Top + (wa.Height - h) / 2);
    }

    #region Своя рамка окна: полоса с кнопками

    /// <summary>Перетаскивание окна за полосу; двойной клик — развернуть/восстановить.</summary>
    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Клик по самим кнопкам окна не должен запускать перетаскивание:
        // иначе кнопка не получит «отпускание» и не сработает.
        if (IsInsideButton(e.OriginalSource)) return;
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            e.Handled = true;
            return;
        }
        try { DragMove(); }
        catch (InvalidOperationException) { /* окно уже максимизировано — перетаскивание не нужно */ }
    }

    /// <summary>Правый клик по полосе — системное меню окна (как Alt+Space).</summary>
    private void OnTitleBarContextMenu(object sender, MouseButtonEventArgs e)
    {
        if (IsInsideButton(e.OriginalSource)) return;
        SystemCommands.ShowSystemMenu(this, e.GetPosition(this));
        e.Handled = true;
    }

    /// <summary>Находится ли источник события внутри кнопки окна.</summary>
    private static bool IsInsideButton(object? source)
    {
        DependencyObject? cur = source as DependencyObject;
        while (cur is not null)
        {
            if (cur is Button) return true;
            cur = VisualTreeHelper.GetParent(cur);
        }
        return false;
    }

    private void OnMinimizeWindow(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void OnToggleMaximize(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void OnCloseWindow(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    /// <summary>Значок средней кнопки: «развернуть» или «восстановить».</summary>
    private void UpdateMaximizeIcon()
    {
        if (MaxIcon is null || RestoreIcon is null) return;
        var maximized = WindowState == WindowState.Maximized;
        MaxIcon.Visibility = maximized ? Visibility.Collapsed : Visibility.Visible;
        RestoreIcon.Visibility = maximized ? Visibility.Visible : Visibility.Collapsed;
    }

    #endregion

    /// <summary>Рабочая область монитора окна в логических единицах WPF.</summary>
    private Rect WorkAreaInDips()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return Rect.Empty;

        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero) return Rect.Empty;

        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref mi)) return Rect.Empty;

        // Масштаб берём у WPF: он гарантированно совпадает с тем, которым
        // приложение верстает. GetDpiForMonitor() здесь не годится —
        // для Per-Monitor-V2 процесса он возвращает СИСТЕМНЫЙ DPI.
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        if (scale <= 0 || double.IsNaN(scale) || double.IsInfinity(scale)) scale = 1.0;

        return new Rect(
            mi.rcWork.Left / scale,
            mi.rcWork.Top / scale,
            (mi.rcWork.Right - mi.rcWork.Left) / scale,
            (mi.rcWork.Bottom - mi.rcWork.Top) / scale);
    }

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    #endregion

    // ── характеристики ──
    private void OnRollCharacteristics(object sender, RoutedEventArgs e) => Vm.RollCharacteristics();

    // ── Характеристики: шаги «−» и «+» у «Базы» и «Правок» ──
    private void OnBaseInc(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MainViewModel.CharacteristicRow row) Vm.AdjustCharacteristicBase(row, 1);
    }

    private void OnBaseDec(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MainViewModel.CharacteristicRow row) Vm.AdjustCharacteristicBase(row, -1);
    }

    private void OnAdvanceInc(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MainViewModel.CharacteristicRow row) row.Inc();
    }

    private void OnAdvanceDec(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MainViewModel.CharacteristicRow row) row.Dec();
    }

    private void OnAdjustmentInc(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MainViewModel.CharacteristicRow row) Vm.AdjustCharacteristicAdjustment(row, 1);
    }

    private void OnAdjustmentDec(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MainViewModel.CharacteristicRow row) Vm.AdjustCharacteristicAdjustment(row, -1);
    }

    // ── опыт ──
    private void OnAddXp(object sender, RoutedEventArgs e) => Vm.AddXp();

    private void OnResetXp(object sender, RoutedEventArgs e)
    {
        if (Vm.EarnedXp == 0) return;
        var res = MessageBox.Show(
            $"Сбросить весь начисленный опыт ({Vm.EarnedXp} ОО)?\n\nОбщий опыт, ранг и свободный остаток пересчитаются.",
            "Сброс опыта", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res == MessageBoxResult.Yes) Vm.ResetEarnedXp();
    }

    private void OnXpKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        Vm.AddXp();
        e.Handled = true;
    }

    // ── улучшения ──
    private void OnBuyAdvance(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MainViewModel.AdvanceRow row) Vm.BuyAdvance(row);
    }

    private void OnUnbuyAdvance(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MainViewModel.AdvanceRow row) Vm.UnbuyAdvance(row);
    }

    // ── умения ──
    private void OnSkillToggle(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not MainViewModel.SkillSheetRow row) return;
        if (row.Learned) Vm.UnlearnSkill(row);
        else Vm.LearnSkill(row);
    }

    private void OnRemoveSkill(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MainViewModel.SkillSheetRow row) Vm.UnlearnSkill(row);
    }

    // ── таланты ──
    private void OnRemoveTalent(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MainViewModel.TalentRow row) Vm.RemoveTalent(row.Entry);
    }

    // ── предметы ──
    private void OnItemUp(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MainViewModel.ItemRow row)
        {
            row.Entry.Quantity += 1;
            Vm.MarkDirty();
            Vm.Refresh();
        }
    }

    private void OnItemDown(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MainViewModel.ItemRow row)
        {
            row.Entry.Quantity = Math.Max(1, row.Entry.Quantity - 1);
            Vm.MarkDirty();
            Vm.Refresh();
        }
    }

    private void OnRemoveItem(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MainViewModel.ItemRow row) Vm.RemoveItem(row.Entry);
    }

    // ── стартовое снаряжение ──
    private void OnChooseStartingEquipment(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MainViewModel.StartingEquipmentVariant v) v.Choose();
    }

    private void OnPickStartingEquipmentManually(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MainViewModel.StartingEquipmentRow row) row.PickManual();
    }

    private void OnCareerChanged(object sender, SelectionChangedEventArgs e) { }

    private void OnHomeworldChanged(object sender, SelectionChangedEventArgs e) { }

    // ── награды жизненного пути ──
    private void OnTogglePathGrant(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MainViewModel.PathGrantRow row) Vm.TogglePathGrant(row);
    }

    // ── психические приёмы ──
    private void OnPsyToggle(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not MainViewModel.PsyRow row) return;
        if (row.Learned) Vm.ForgetPsy(row);
        else Vm.LearnPsy(row);
    }

    // ── силы навигатора ──
    private void OnNavToggle(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not MainViewModel.NavPowerRow row) return;
        if (row.Learned) Vm.RemoveNav(row);
        else Vm.LearnNav(row);
    }

    private void OnNavUp(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MainViewModel.NavPowerRow row) row.Up();
    }

    private void OnNavDown(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MainViewModel.NavPowerRow row) row.Down();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // В служебном режиме (--walkthrough, --selftest) вопрос о сохранении
        // не задаётся: иначе приложение ждёт ответа и не завершает работу.
        if (!App.ServiceMode && Vm.HasUnsavedChanges)
        {
            var res = MessageBox.Show("Есть несохранённые изменения. Сохранить?", "Выход",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (res == MessageBoxResult.Cancel) { e.Cancel = true; return; }
            if (res == MessageBoxResult.Yes)
            {
                if (string.IsNullOrEmpty(Vm.FileName))
                {
                    var sfd = new Microsoft.Win32.SaveFileDialog
                    {
                        Filter = "Персонаж RogueTrader (*.rtchar.json)|*.rtchar.json",
                        FileName = Services.CharacterRepository.SafeFileName(Vm.Character.Meta.Name),
                    };
                    if (sfd.ShowDialog() != true) { e.Cancel = true; return; }
                    Services.CharacterRepository.Save(Vm.Character, sfd.FileName);
                }
                else Services.CharacterRepository.Save(Vm.Character);
            }
        }
        base.OnClosing(e);
    }
}

