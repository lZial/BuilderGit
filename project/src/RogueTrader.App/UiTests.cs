using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace RogueTrader.App;

/// <summary>
/// Автопроверки интерфейса: окно открывается, разделы переключаются,
/// руководство показывается, масштаб меняется, колесо катит плавно.
///
/// Проверки идут по настоящему окну WPF: элементы находятся в дереве
/// визуальных элементов, события вызываются как от мыши, раскладка
/// прорабатывается через DispatcherFrame. Поэтому здесь ловятся ошибки,
/// которых не видно в проверках правил и данных (--walkthrough).
///
/// Запуск: <c>RogueTraderBuilder.exe --uitest</c>.
/// Код возврата 0 — все проверки пройдены, 1 — есть ошибки.
/// </summary>
internal static class UiTests
{
    private static readonly List<string> Log = new();
    private static int _bad;

    public static int Run()
    {
        // Масштаб, который выбрал игрок, проверки не трогают: в конце
        // возвращаем прежний.
        var savedZoom = Services.UiSettings.LoadZoom();

        var unhandled = new List<Exception>();
        Dispatcher.CurrentDispatcher.UnhandledException += (_, e) =>
        {
            unhandled.Add(e.Exception);
            e.Handled = true;
        };

        var app = (Application)Application.Current;
        var win = new MainWindow { Width = 1280, Height = 820 };
        try
        {
            Step("Окно");
            win.Show();
            Pump();
            Try("окно открылось и разместилось", () =>
            {
                if (!win.IsLoaded) Bad("окно не загрузилось");
                else if (win.ActualWidth <= 0 || win.ActualHeight <= 0) Bad("окно нулевого размера");
                else Ok($"окно {win.ActualWidth:0}×{win.ActualHeight:0}");
            });

            Step("Разделы");
            CheckTabs(win);

            Step("Руководство");
            CheckGuide(win);

            Step("Масштаб");
            CheckZoom(win);

            Step("Колесо");
            CheckWheel(win);

            Step("Переходы по разделам листа");
            CheckJumps(win);

            Step("Психические приёмы");
            CheckPsy(win);

            Step("Исключения");
            Try("в обработчиках интерфейса ошибок нет", () =>
            {
                if (unhandled.Count > 0)
                {
                    Bad("исключения в интерфейсе: " + string.Join(" | ", unhandled.Take(3).Select(e => e.Message)));
                }
                else Ok("исключений нет");
            });
        }
        catch (Exception ex)
        {
            _bad++;
            Log.Add($"  [!!]   проверки упали: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            try { win.Close(); } catch { }
            try { Services.UiSettings.SaveZoom(savedZoom); } catch { }
        }

        Console.WriteLine();
        Console.WriteLine("═══ Автопроверки интерфейса ═══");
        foreach (var l in Log) Console.WriteLine(l);
        Console.WriteLine();
        Console.WriteLine(_bad == 0
            ? $"  все проверки пройдены ({Log.Count(l => l.Contains("[ок]"))})"
            : $"  ошибок: {_bad}");
        return _bad == 0 ? 0 : 1;
    }

    // ─────────────────────────── проверки ───────────────────────────

    /// <summary>Все разделы переключаются, содержимое каждого показывается.</summary>
    private static void CheckTabs(MainWindow win)
    {
        var names = new List<string>();
        var slow = new List<string>();
        for (int i = 0; i < win.Tabs.Items.Count; i++)
        {
            var tab = win.Tabs.Items[i] as TabItem;
            var name = tab?.Header as string ?? i.ToString();
            var sw = Stopwatch.StartNew();
            win.Tabs.SelectedIndex = i;
            Pump();
            sw.Stop();
            names.Add($"{name} — {sw.ElapsedMilliseconds} мс");

            var host = win.Tabs.Template.FindName("ContentHost", win.Tabs) as ContentPresenter;
            var content = host?.Content as FrameworkElement;
            if (content is null)
            {
                _bad++;
                Log.Add($"  [!!]   раздел «{name}»: содержимое не показано");
                continue;
            }
            if (content.ActualHeight <= 0)
            {
                _bad++;
                Log.Add($"  [!!]   раздел «{name}»: высота содержимого {content.ActualHeight:0}");
            }
            if (sw.ElapsedMilliseconds > 1500) slow.Add($"{name} ({sw.ElapsedMilliseconds} мс)");
        }
        Ok($"разделов переключено: {win.Tabs.Items.Count} ({string.Join("; ", names)})");
        if (slow.Count > 0)
            Log.Add($"  [·]    медленные разделы: {string.Join("; ", slow)}");
    }

    /// <summary>Окно руководства открывается и показывает текст.</summary>
    private static void CheckGuide(MainWindow win)
    {
        var guide = new Views.GuideWindow { Owner = win, Zoom = 1.5 };
        try
        {
            guide.Show();
            Pump();
            Try("руководство открывается и показывает текст", () =>
            {
                if (!guide.IsLoaded) Bad("окно руководства не загрузилось");
                else if (guide.ActualWidth <= 0) Bad("окно руководства нулевого размера");
                else Ok($"окно руководства {guide.ActualWidth:0}×{guide.ActualHeight:0}");
            });

            Try("руководство наследует масштаб окна", () =>
            {
                var scale = guide.FindName("ZoomScale") as ScaleTransform;
                if (scale is null) Bad("у окна руководства нет масштаба содержимого");
                else if (Math.Abs(scale.ScaleX - 1.5) > 0.01) Bad($"масштаб окна {scale.ScaleX:0.00}, а ждали 1.50");
                else if (guide.Width < 780 || guide.Height < 660)
                    Bad($"окно уменьшилось под масштабом: {guide.Width:0}×{guide.Height:0}");
                else Ok($"масштаб окна руководства {scale.ScaleX:0.00}, окно {guide.Width:0}×{guide.Height:0}");
            });
        }
        finally
        {
            try { guide.Close(); } catch { }
            Pump();
        }
    }

    /// <summary>Кнопки масштаба меняют масштаб и подпись в процентах.</summary>
    private static void CheckZoom(MainWindow win)
    {
        Try("кнопка «+» увеличивает масштаб", () =>
        {
            var before = win.CurrentZoom;
            win.ZoomInButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
            if (win.CurrentZoom <= before) Bad($"масштаб не вырос: {before:0.00} → {win.CurrentZoom:0.00}");
            else if (win.ZoomLevelText.Text != $"{Math.Round(win.CurrentZoom * 100):0}%")
                Bad($"подпись не совпала: «{win.ZoomLevelText.Text}»");
            else Ok($"масштаб {before:0.00} → {win.CurrentZoom:0.00}, подпись «{win.ZoomLevelText.Text}»");
        });

        Try("надпись со значением возвращает 100 %", () =>
        {
            win.ZoomLevelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
            if (Math.Abs(win.CurrentZoom - 1.0) > 0.001) Bad($"масштаб не сброшен: {win.CurrentZoom:0.00}");
            else Ok("масштаб сброшен к 100 %");
        });
    }

    /// <summary>Колесо прокручивает список плавно, а не прыжком.</summary>
    private static void CheckWheel(MainWindow win)
    {
        // Выбираем вкладку «Умения» по заголовку, а не по индексу — порядок вкладок
        // может меняться (например, «Улучшения» перемещено между «Навигатором»
        // и «Путём»).
        if (win.Tabs.Items.OfType<TabItem>().FirstOrDefault(t => t.Header as string == "Умения") is { } skillsTab)
            win.Tabs.SelectedItem = skillsTab;
        Pump();

        var list = FindFirst<ListBox>(win);
        if (list is null)
        {
            _bad++;
            Log.Add("  [!!]   список умений не найден");
            return;
        }
        var scroller = FindFirst<ScrollViewer>(list);
        if (scroller is null)
        {
            _bad++;
            Log.Add("  [!!]   прокручиваемый контейнер списка не найден");
            return;
        }

        var before = scroller.VerticalOffset;
        var samples = new List<double>();
        var args = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120)
        {
            RoutedEvent = UIElement.PreviewMouseWheelEvent
        };
        scroller.RaiseEvent(args);
        for (int i = 0; i < 12; i++)
        {
            Wait(30);
            samples.Add(scroller.VerticalOffset);
        }
        var after = scroller.VerticalOffset;

        Try("колесо прокручивает список плавно", () =>
        {
            if (after <= before) Bad($"список не прокрутился: {before:0} → {after:0}");
            else
            {
                // Плавность: между началом и концом должны быть промежуточные
                // положения, а не один прыжок. Считаем уникальные позиции, которые
                // успела принять прокрутка между заборами (кадр отрисовки может не
                // совпасть с моментом забора, поэтому не полагаемся на соседние).
                var levels = new HashSet<double>();
                foreach (var s in samples)
                {
                    if (s - before > 0.5) levels.Add(Math.Round(s, 1));
                }
                if (levels.Count < 3) Bad($"прокрутка выглядит прыжком: промежуточных позиций {levels.Count}, а должно быть ≥ 3");
                else Ok($"список прокрутился с {before:0} до {after:0} за {levels.Count} промежуточных позиций");
            }
        });
    }

    /// <summary>Кнопки перехода к разделам листа работают.</summary>
    private static void CheckJumps(MainWindow win)
    {
        win.Tabs.SelectedIndex = 0; // «Лист»
        Pump();
        var buttons = FindAll<Button>(win).Where(b => b.Tag is string).ToList();
        if (buttons.Count == 0)
        {
            _bad++;
            Log.Add("  [!!]   кнопки перехода по разделам листа не найдены");
            return;
        }
        var failed = new List<string>();
        foreach (var b in buttons)
        {
            try
            {
                b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump();
            }
            catch (Exception ex)
            {
                failed.Add($"{b.Tag}: {ex.Message}");
            }
        }
        Try("переход к разделу листа работает", () =>
        {
            if (failed.Count > 0) Bad("переходы с ошибкой: " + string.Join("; ", failed.Take(3)));
            else Ok($"кнопок перехода отработано: {buttons.Count}");
        });
    }

    /// <summary>Свойства приёма — книжные, без «древа приёмов».</summary>
    private static void CheckPsy(MainWindow win)
    {
        win.Tabs.SelectedIndex = 6; // «Психика»
        Pump();
        var vm = win.DataContext as ViewModels.MainViewModel;
        Try("свойства приёма — как в книге", () =>
        {
            if (vm is null) { Bad("нет модели представления"); return; }
            if (vm.PsyAbilities.Count == 0) { Bad("список приёмов пуст"); return; }
            var wrong = vm.PsyAbilities
                .SelectMany(p => p.StatPairs)
                .Where(p => p.Name == "Проверка" || p.Name.Contains("древо", StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Name)
                .Distinct()
                .ToList();
            if (wrong.Count > 0) Bad("в свойствах приёма остались старые подписи: " + string.Join(", ", wrong));
            else if (!vm.PsyAbilities[0].StatPairs.Any(p => p.Name.Contains("Тест")))
                Bad("нет подписи теста сотворения психосилы");
            else Ok($"приёмов: {vm.PsyAbilities.Count}, подписи: "
                   + string.Join(", ", vm.PsyAbilities[0].StatPairs.Select(p => p.Name)));
        });
    }

    // ─────────────────────────── помощники ───────────────────────────

    private static void Step(string title)
    {
        Log.Add("");
        Log.Add($"═══ {title} ═══");
    }

    private static void Ok(string message) => Log.Add($"  [ок]   {message}");

    private static void Bad(string message)
    {
        _bad++;
        Log.Add($"  [!!]   {message}");
    }

    private static void Try(string name, Action check)
    {
        try { check(); }
        catch (Exception ex)
        {
            _bad++;
            Log.Add($"  [!!]   {name}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>Прорабатывает очередь раскладки (DispatcherFrame).</summary>
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    /// <summary>Ждёт, пока отработают анимации и таймеры.</summary>
    private static void Wait(int ms)
    {
        var end = Environment.TickCount + ms;
        while (Environment.TickCount < end)
        {
            Pump();
            Thread.Sleep(10);
        }
    }

    private static T? FindFirst<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T found) return found;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            if (FindFirst<T>(VisualTreeHelper.GetChild(root, i)) is { } hit) return hit;
        }
        return null;
    }

    private static IEnumerable<T> FindAll<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T found) yield return found;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            foreach (var x in FindAll<T>(VisualTreeHelper.GetChild(root, i))) yield return x;
        }
    }
}
