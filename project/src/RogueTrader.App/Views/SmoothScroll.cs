using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace RogueTrader.App.Views;

/// <summary>
/// Плавная прокрутка колесом мыши.
///
/// Штатный ScrollViewer прокручивает сразу на всю «щелчок» колеса, из-за чего
/// длинные списки (предметы, таланты, умения) читаются рывками. Здесь колесо
/// не меняет смещение сразу, а запускает короткую анимацию к целевой позиции.
///
/// Два правила, без которых прокрутка получается «скачущей»:
///
///  1. Прокручивает **самый внутренний** контейнер под курсором. Событие
///     <see cref="UIElement.PreviewMouseWheelEvent"/> идёт от корня вниз, и внешняя
///     страница видела его раньше вложенного списка — прокручивался весь лист
///     вместо списка под мышью.
///  2. Один щелчок ограничен долей экрана. <see cref="MouseWheelEventArgs.Delta"/>
///     равен 120, и при наивном «Delta × 2» это 240 точек — половина экрана,
///     то есть визуально «одно движение колеса = страница».
///
/// Подключается так:
/// <c>&lt;ScrollViewer Style="{StaticResource SmoothScroll}"&gt;</c>
/// либо <c>SmoothScroll.SetEnabled(sv, true)</c>.
/// </summary>
public static class SmoothScroll
{
    private static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(SmoothScroll), new PropertyMetadata(false, OnEnabledChanged));

    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(ScrollState), typeof(SmoothScroll), new PropertyMetadata(null));

    public static void SetEnabled(DependencyObject o, bool v) => o.SetValue(EnabledProperty, v);
    public static bool GetEnabled(DependencyObject o) => (bool)o.GetValue(EnabledProperty);

    private static void SetState(DependencyObject o, ScrollState? s) => o.SetValue(StateProperty, s);
    private static ScrollState? GetState(DependencyObject o) => (ScrollState?)o.GetValue(StateProperty);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer sv) return;
        GetState(sv)?.Stop();
        SetState(sv, null);
        sv.PreviewMouseWheel -= OnWheel;
        if (e.NewValue is true) sv.PreviewMouseWheel += OnWheel;
    }

    /// <summary>Длительность «доводки» до целевой позиции, мс.</summary>
    private const double DurationMs = 200;

    /// <summary>
    /// Наименьший промежуток между кадрами анимации, мс. 45 кадров в секунду:
    /// каждый кадр — это перерисовка окна целиком, и на слабой видеокарте
    /// 60 кадров в секунду съедали процессор так, что окно отзывалось с
    /// задержкой. На глаз 45 кадров неотличимы от 60.
    /// </summary>
    private const double MinFrameMs = 1000.0 / 45;

    /// <summary>
    /// Большая доля высоты окна, которую нельзя прокрутить за один щелчок.
    /// Страховка от «перелёта»: даже если система пришлёт огромный Delta,
    /// список не уйдёт с места, которое игрок собирался рассмотреть.
    /// </summary>
    private const double MaxViewportFraction = 0.35;

    /// <summary>
    /// Умножитель скорости прокрутки колесом. Раньше шаг умножался дважды
    /// (1.4 × 1.4 = 1.96 к системному шагу); теперь 2.55 — на 30 % быстрее
    /// прежнего, но по-прежнему намного мягче штатного «щелчка на страницу».
    /// </summary>
    private const double SpeedMultiplier = 2.55;

    /// <summary>Множитель потолка «за один щелчок» (доля высоты окна).</summary>
    private const double CapMultiplier = 1.4;

    /// <summary>Высота строки текста, если системная настройка недоступна.</summary>
    private const double FallbackLineHeight = 16;

    private static void OnWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;
        if (e.Delta == 0) return;

        // Событие спускается от корня к курсору: если внутри нас есть свой
        // прокручиваемый ScrollViewer, прокрутит он — мы не вмешиваемся
        // и не помечаем событие обработанным, чтобы до него дошло событие.
        if (HasInnerScroller(sv, e.OriginalSource as DependencyObject)) return;

        if (sv.ScrollableHeight <= 0) return;

        // Шаг считаем в тех же единицах, в которых measured сам список:
        // по элементам или по пикселям.
        var step = StepFor(sv, e.Delta, ScrollsByItems(sv));
        if (step == 0) return;

        var st = GetState(sv);
        // Новый щелчок во время анимации продолжает её от текущей точки,
        // а не прыгает заново: иначе быстрые щелчки складывались в рывок.
        // step уже со знаком «прокрутка вниз», поэтому именно прибавляем.
        var from = sv.VerticalOffset;
        var target = Clamp(sv, from + step);
        if (Math.Abs(target - from) < 0.5) return;

        e.Handled = true;

        if (st is null)
        {
            st = new ScrollState { Owner = sv };
            SetState(sv, st);
        }
        st.Retarget(from, target);
        st.Play();
    }

    /// <summary>
    /// Есть ли между <paramref name="sv"/> и элементом под курсором вложенный
    /// прокручиваемый ScrollViewer.
    /// </summary>
    private static bool HasInnerScroller(ScrollViewer sv, DependencyObject? source)
    {
        if (source is not Visual visual || ReferenceEquals(visual, sv)) return false;
        DependencyObject? node = visual;
        while (node is not null && !ReferenceEquals(node, sv))
        {
            if (node is ScrollViewer inner && inner.ScrollableHeight > 0) return true;
            node = node is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(node)
                : null;
        }
        return false;
    }

    /// <summary>
    /// Смещение списка измеряется в элементах или в пикселях?
    ///
    /// Виртуализация в WPF требует CanContentScroll="True", и кажется, что
    /// смещение всегда идёт по элементам. Но VirtualizingStackPanel умеет
    /// считать смещение в пикселях (ScrollUnit="Pixel") — тогда список
    /// катится плавно, а виртуализация работает.
    ///
    /// Поэтому единицы определяем по самой панели элементов, а не по
    /// CanContentScroll: иначе шаг в пикселях ушёл бы в список, который
    /// считает смещение в элементах, и прыжок стал бы в сто раз больше.
    /// </summary>
    private static bool ScrollsByItems(ScrollViewer sv)
    {
        if (!sv.CanContentScroll) return false;

        var panel = FindItemsPanel(sv);
        if (panel is null) return true;

        // Единицы прокрутки задаёт сам список: панель читает свойство
        // со своего ItemsControl (так же сделано в WPF).
        var items = FindItemsControl(panel);
        var unit = items is not null
            ? VirtualizingPanel.GetScrollUnit(items)
            : VirtualizingPanel.GetScrollUnit(panel);
        return unit == ScrollUnit.Item;
    }

    /// <summary>Ищет список (ItemsControl), которому принадлежит панель.</summary>
    private static ItemsControl? FindItemsControl(DependencyObject node)
    {
        while (node is not null)
        {
            if (node is ItemsPresenter presenter && presenter.TemplatedParent is ItemsControl ic)
                return ic;
            node = node is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node);
        }
        return null;
    }

    /// <summary>Ищет панель элементов под прокручиваемым контейнером.</summary>
    private static VirtualizingStackPanel? FindItemsPanel(DependencyObject root)
    {
        if (root is VirtualizingStackPanel vsp) return vsp;

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            if (FindItemsPanel(VisualTreeHelper.GetChild(root, i)) is { } found)
                return found;
        }
        return null;
    }

    /// <summary>
    /// На сколько точек сместится прокрутка за один щелчок.
    /// За единицу берём системную настройку «строк за щелчок» (обычно 3), а
    /// результат ограничиваем долей окна, чтобы одно движение колеса никогда
    /// не перемахивало пол-экрана.
    ///
    /// Знак важен: в WPF положительный <see cref="MouseWheelEventArgs.Delta"/>
    /// означает вращение колеса ОТ пользователя (вверх), и прокрутка при этом
    /// должна уменьшать смещение. Поэтому здесь знак инвертируется, а уже
    /// «плюс» означает прокрутку вниз.
    /// </summary>
    private static double StepFor(ScrollViewer sv, int delta, bool byItem)
    {
        var lines = SystemParameters.WheelScrollLines;
        if (lines <= 0) lines = 3;

        var notches = Math.Abs(delta) / 120.0;

        // Список: смещение в элементах. За щелчок — «строк за щелчок»
        // (обычно три), но не больше трети экрана.
        if (byItem)
        {
            var stepItems = notches * lines;
            var itemCap = sv.ViewportHeight * MaxViewportFraction * CapMultiplier;
            if (itemCap > 0) stepItems = Math.Min(stepItems, itemCap);
            return -Math.Sign(delta) * Math.Max(1, Math.Round(stepItems));
        }

        var fontSize = sv.FontSize;
        var line = fontSize > 0 ? fontSize * 1.35 : FallbackLineHeight;
        line = Math.Clamp(line, 8, 40);

        // |Delta| = 120 на щелчок у обычных мышей; у точных и тачпадов меньше.
        var step = notches * lines * line * SpeedMultiplier;

        var cap = sv.ViewportHeight * MaxViewportFraction * CapMultiplier;
        if (cap > 0) step = Math.Min(step, cap);

        return -Math.Sign(delta) * step;
    }

    private static double Clamp(ScrollViewer sv, double value) =>
        Math.Max(0, Math.Min(sv.ScrollableHeight, value));

    private sealed class ScrollState
    {
        public ScrollViewer Owner = null!;

        /// <summary>Смещение, с которого началась текущая доводка.</summary>
        public double From;
        /// <summary>Куда доводим.</summary>
        public double To;

        /// <summary>
        /// Часы идут по реальному времени: если кадр задержался (окно занято
        /// раскладкой), анимация всё равно закончится за отведённое время,
        /// а не растянется на столько же.
        /// </summary>
        private readonly Stopwatch _clock = new();

        /// <summary>Подписка на кадры отрисовки: один шаг на кадр, не чаще 60 в секунду.</summary>
        private EventHandler? _onFrame;
        private double _lastFrameMs;

        public void Retarget(double from, double to)
        {
            From = from;
            To = to;
        }

        public void Play()
        {
            _clock.Restart();
            _lastFrameMs = -MinFrameMs;
            if (_onFrame is not null) return;
            _onFrame = (_, _) => Frame();
            CompositionTarget.Rendering += _onFrame;
        }

        private void Frame()
        {
            if (!Owner.IsLoaded || Owner.ScrollableHeight <= 0)
            {
                Stop();
                return;
            }

            // Без вертикальной синхронизации (удалённый рабочий стол) кадры
            // могут идти чаще экрана — лишние пропускаем.
            var now = _clock.Elapsed.TotalMilliseconds;
            if (now - _lastFrameMs < MinFrameMs) return;
            _lastFrameMs = now;

            var t = Math.Min(1.0, now / DurationMs);
            // Плавное затухание: чем ближе к концу, тем мельче шаг
            var eased = 1 - Math.Pow(1 - t, 3);
            var to = Clamp(Owner, To);
            var value = From + (to - From) * eased;
            if (Math.Abs(Owner.VerticalOffset - value) >= 0.5) Owner.ScrollToVerticalOffset(value);

            if (t >= 1) Stop();
        }

        public void Stop()
        {
            if (_onFrame is not null)
            {
                CompositionTarget.Rendering -= _onFrame;
                _onFrame = null;
            }
            SmoothScroll.SetState(Owner, null);
        }
    }
}
