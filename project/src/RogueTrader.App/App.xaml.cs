using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace RogueTrader.App;

public partial class App : Application
{
    /// <summary>
    /// Служебный запуск (--walkthrough, --selftest): окна закрываются без
    /// вопроса о сохранении, приложение завершается само.
    /// </summary>
    public static bool ServiceMode { get; private set; }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    /// <summary>Диапазон осознанности процесса по DPI десктопного окна.</summary>
    private static string DpiAwarenessOfProcess()
    {
        try
        {
            var hwnd = GetDesktopWindow();
            if (hwnd == IntPtr.Zero) return "неизвестно (нет HWND)";
            var dpi = GetDpiForWindow(hwnd);
            return dpi == 0 ? "Unaware (GetDpiForWindow=0)" : $"{dpi} dpi";
        }
        catch (Exception ex) { return "ошибка: " + ex.GetType().Name; }
    }

    private static void LogCrash(string where, Exception? ex)
    {
        try
        {
            var dir = Services.AppPaths.AppDirectory;
            File.AppendAllText(Path.Combine(dir, "crash.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{where}]\n{ex}\n\n");
        }
        catch { }
    }

    /// <summary>
    /// Всплывающий список (вид, род занятий, разделы пути) живёт в отдельном
    /// окне WPF, и при прокрутке страницы он не пересчитывает своё положение:
    /// список остаётся на экране, а прокручивается лист. Поэтому при прокрутке
    /// мы пересчитываем положение списка сами — он идёт за своей плашкой.
    /// </summary>
    private static ComboBox? _openChoice;

    private void OnRichChoicePopupOpened(object sender, EventArgs e)
    {
        if (sender is not Popup popup) return;
        if (popup.PlacementTarget is not ComboBox combo) return;
        _openChoice = combo;
        var viewer = FindAncestor<ScrollViewer>(combo);
        if (viewer is not null) viewer.ScrollChanged += OnRichChoiceScrolled;
    }

    private void OnRichChoicePopupClosed(object sender, EventArgs e)
    {
        if (sender is not Popup popup) return;
        if (popup.PlacementTarget is ComboBox combo)
        {
            var viewer = FindAncestor<ScrollViewer>(combo);
            if (viewer is not null) viewer.ScrollChanged -= OnRichChoiceScrolled;
        }
        if (ReferenceEquals(_openChoice, popup.PlacementTarget)) _openChoice = null;
    }

    private static void OnRichChoiceScrolled(object sender, ScrollChangedEventArgs e)
    {
        if (_openChoice is null) return;
        if (_openChoice.Template.FindName("PART_Popup", _openChoice) is not Popup popup) return;
        if (!popup.IsOpen) return;
        // Список живёт в отдельном окне и сам не пересчитывает положение:
        // при прокрутке он остаётся на месте. Пересоздаём привязку — тогда
        // список переезжает вместе со своей плашкой.
        var target = popup.PlacementTarget;
        if (target is null) return;
        popup.PlacementTarget = null;
        popup.PlacementTarget = target;
    }

    /// <summary>Ищет предка заданного типа в визуальном или логическом дереве.</summary>
    private static T? FindAncestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node is not null)
        {
            if (node is T found) return found;
            node = node is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node);
        }
        return null;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Служебные режимы (--selftest и т. п.) печатают по-русски: без UTF-8
        // консоль Windows (кодовая страница 866/1251) превращает текст в мусор.
        if (e.Args.Any(a => a.StartsWith("--", StringComparison.Ordinal) && a != "--demo"))
        {
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
        }

        // Служебный запуск (--walkthrough, --selftest, --uitest): окна закрываются без
        // вопроса о сохранении, приложение завершается само.
        ServiceMode = e.Args.Contains("--walkthrough") || e.Args.Contains("--selftest")
                      || e.Args.Contains("--uitest");

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            LogCrash("AppDomain", args.ExceptionObject as Exception);
        DispatcherUnhandledException += (_, args) =>
        {
            LogCrash("Dispatcher", args.Exception);
            MessageBox.Show(
                $"Произошла ошибка:\n\n{args.Exception.Message}\n\nПодробности записаны в crash.log рядом с программой.",
                "Rogue Trader Builder", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        if (e.Args.Contains("--walkthrough"))
        {
            Shutdown(Walkthrough.Run() == 0 ? 0 : 1);
            return;
        }

        if (e.Args.Contains("--uitest"))
        {
            // Проверки интерфейса: настоящее окно, настоящие события.
            Shutdown(UiTests.Run());
            return;
        }

        if (e.Args.Contains("--selftest"))
        {
            Services.DataPackService svc = new();
            svc.Load();
            Console.WriteLine(svc.Report.Summary);
            foreach (var p in svc.Report.LoadedPacks) Console.WriteLine("  + " + p);
            foreach (var warn in svc.Report.Warnings) Console.WriteLine("  ! " + warn);
            foreach (var er in svc.Report.Errors) Console.WriteLine("  X " + er);
            foreach (var cf in svc.Report.Conflicts) Console.WriteLine("  = " + cf);
            Console.WriteLine($"Характеристик: {svc.Pack.OrderedCharacteristics.Count}");
            Console.WriteLine($"Умений: {svc.Pack.Skills.Count} · Талантов: {svc.Pack.Talents.Count} · Родов занятий: {svc.Pack.Careers.Count}");
            Console.WriteLine($"Предметов: {svc.Pack.Items.Count} · Видов: {svc.Pack.Species.Count} · Каст: {svc.Pack.Castes.Count}");
            Console.WriteLine($"Психических приёмов: {svc.Pack.PsyAbilities.Count} · Сил навигатора: {svc.Pack.NavigatorPowers.Count}");
            Console.WriteLine($"Разделов арсенала: {svc.Pack.ItemSections.Count} · Подразделов: {svc.Pack.ItemGroups.Count}");
            // Ни один предмет не должен потеряться при раскладке по главе «Арсенал»
            var withoutGroup = svc.Pack.Items.Values
                .Count(i => Core.Data.ItemGroups.Find(i, svc.Pack.ItemGroups) is null);
            if (withoutGroup == 0) Console.WriteLine("   OK весь арсенал разложен по главе «Арсенал»");
            else
            {
                Console.WriteLine($"   ! без раздела: {withoutGroup}");
                foreach (var i in svc.Pack.Items.Values
                             .Where(x => Core.Data.ItemGroups.Find(x, svc.Pack.ItemGroups) is null)
                             .Take(12))
                    Console.WriteLine($"     — {i.Name} ({i.Category}/{i.WeaponClass})");
            }
            Console.WriteLine($"Родных миров: {svc.Pack.Homeworlds.Count} · Судьб: {svc.Pack.Destiny.Count} · Устремлений: {svc.Pack.Aspiration.Count}");

            var ch = new Core.Models.Character { CareerId = "free_trader" };
            foreach (var d in svc.Pack.OrderedCharacteristics)
                ch.Characteristics.Add(new Core.Models.CharacteristicEntry { CharacteristicId = d.Id, Base = 35 });
            var engine = new Core.Rules.RulesEngine(svc.Pack);
            var st = engine.Compute(ch);
            Console.WriteLine($"Проверка: ранг {st.Rank}, всего ОО {st.TotalXp}, порог ранений {st.WoundThreshold}");

            var opts = engine.AdvancesFor(ch, 2);
            Console.WriteLine($"Улучшений на ранге 2 у «Вольного торговца»: {opts.Count}");
            foreach (var o in opts.Take(5))
            {
                var ok = engine.CheckAdvance(ch, o);
                Console.WriteLine($"   {(ok.Ok ? "OK " : "-- ")}{o.Name} | {o.Type} | {o.Cost} ОО | {ok.Summary}");
            }

            // Покупка улучшений и проверка пересчёта ранга.
            // Покупаем только то, на что хватает свободного опыта: программа не
            // должна позволять уйти в минус.
            int bought = 0, refused = 0;
            foreach (var o in opts.Where(x => x.Type == "Умение").Take(2))
            {
                if (!engine.CheckAdvance(ch, o).Ok) { refused++; continue; }
                engine.ApplyAdvance(ch, o); bought++;
            }
            foreach (var o in engine.AdvancesFor(ch, 2).Where(x => x.Type == "Талант").Take(1))
            {
                if (!engine.CheckAdvance(ch, o).Ok) { refused++; continue; }
                engine.ApplyAdvance(ch, o); bought++;
            }
            var st2 = engine.Compute(ch);
            Console.WriteLine($"После покупок: ранг {st2.Rank}, всего ОО {st2.TotalXp}, умений {ch.Skills.Count}, талантов {ch.Talents.Count}");
            Console.WriteLine($"Куплено улучшений: {bought}, отклонено из-за нехватки опыта: {refused}");

            // Покупка сверх бюджета должна отклоняться.
            {
                var probe = opts.FirstOrDefault(x => x.Type == "Умение" && ch.AdvanceCount(x.Id) == 0);
                if (probe is not null)
                {
                    var chk = engine.CheckAdvance(ch, probe);
                    Console.WriteLine($"Проверка бюджета: «{probe.Name}» ({probe.Cost} ОО) — "
                        + (chk.Ok ? "можно взять" : "отклонено: " + string.Join("; ", chk.Missing)));
                }
            }

            // Улучшение характеристики — только если на него хватает свободного опыта.
            var wil = ch.FindCharacteristic("wil")!;
            var st2b = engine.Compute(ch);
            var charCost = engine.CharacteristicXpSpent(ch.CareerId, "wil", 1);
            if (charCost <= st2b.XpSpendableLeft)
            {
                wil.AdvancePoints = 5;
                Console.WriteLine($"Воля: 35 + 1 уровень = 40, ОО на характеристики: {charCost}");
            }
            else
            {
                Console.WriteLine($"Воля: улучшение за {charCost} ОО пропущено — свободно {st2b.XpSpendableLeft} ОО");
            }
            var st3 = engine.Compute(ch);
            Console.WriteLine($"Опыт: всего {st3.TotalXp} (врождённых {st3.XpLocked}, потрачено {st3.XpSpent}, свободно {st3.XpSpendableLeft} из {st3.XpSpendableTotal}), ранг {st3.Rank}");
            if (st3.XpSpendableLeft < 0)
                Console.WriteLine("   ! свободный опыт ушёл в минус — бюджет не соблюдён");
            else
                Console.WriteLine($"   OK свободный опыт не отрицательный: {st3.XpSpendableLeft} ОО");

            // Печатный лист
            try
            {
                ch.Meta.Name = "Тестовый Авантюрист";
                Services.AppPaths.EnsureDirectories();
                var pdf = System.IO.Path.Combine(Services.AppPaths.ExportDirectory, "selftest.pdf");
                Services.PdfExportService.Export(ch, svc, pdf);
                var info = new System.IO.FileInfo(pdf);
                Console.WriteLine($"PDF: {info.Length / 1024} КБ — {pdf}");
            }
            catch (Exception ex) { Console.WriteLine("PDF ОШИБКА: " + ex.Message); }

            Shutdown();
            return;
        }

        if (e.Args.Contains("--dpiprobe"))
        {
            var probe = new MainWindow { Width = 600, Height = 400, WindowStartupLocation = WindowStartupLocation.Manual };
            probe.Show();
            var dpi = VisualTreeHelper.GetDpi(probe);
            Console.WriteLine($"WPF DpiScaleX      = {dpi.DpiScaleX}");
            Console.WriteLine($"WPF PixelsPerInchX = {dpi.PixelsPerInchX}");
            Console.WriteLine($"WorkArea           = {SystemParameters.WorkArea}");
            Console.WriteLine($"PrimaryScreen      = {SystemParameters.PrimaryScreenWidth}x{SystemParameters.PrimaryScreenHeight}");
            Console.WriteLine($"ActualSize         = {probe.ActualWidth}x{probe.ActualHeight}");
            Console.WriteLine($"DpiAware           = {DpiAwarenessOfProcess()}");
            Shutdown();
            return;
        }

        // Конвертеры и кисти живут в App.xaml — здесь ничего регистрировать не нужно:
        // перетирать Resources["OkBrush"] конвертером нельзя, это ломает Foreground.

        if (e.Args.Any(a => a.StartsWith("--demo", StringComparison.Ordinal)))
        {
            var demo = new ViewModels.MainViewModel();
            // --demo=tau — персонаж-тау из дополнения «Руководство по Тау».
            if (e.Args.Any(a => a.Equals("--demo=tau", StringComparison.OrdinalIgnoreCase)))
                ViewModels.MainViewModel.BuildTauDemo(demo);
            else
                ViewModels.MainViewModel.BuildDemo(demo);
            // --species=долговязые — показать персонажа другого вида.
            var spArg = e.Args.FirstOrDefault(a => a.StartsWith("--species=", StringComparison.OrdinalIgnoreCase));
            if (spArg is not null)
            {
                var id = spArg[(spArg.IndexOf('=') + 1)..].Trim();
                var sp = demo.Svc.AllSpecies.FirstOrDefault(s =>
                    s.Id.Equals(id, StringComparison.OrdinalIgnoreCase)
                    || s.Name.Contains(id, StringComparison.OrdinalIgnoreCase));
                if (sp is not null) demo.SelectedSpecies = sp;
            }
            var winDemo = new MainWindow { DataContext = demo };
            MainWindow = winDemo;
            SelectTab(winDemo, e.Args);
            winDemo.Show();
            return;
        }

        var win = new MainWindow();
        MainWindow = win;
        SelectTab(win, e.Args);
        win.Show();
    }

    /// <summary>Переключает вкладку из командной строки: --tab=2 (Лист=0, Улучшения=1, …).</summary>
    private static void SelectTab(MainWindow win, string[] args)
    {
        var a = args.FirstOrDefault(x => x.StartsWith("--tab=", StringComparison.OrdinalIgnoreCase));
        if (a is null) return;
        if (int.TryParse(a.AsSpan(6), out var idx) && idx >= 0 && idx < win.Tabs.Items.Count)
            win.Tabs.SelectedIndex = idx;
    }
}