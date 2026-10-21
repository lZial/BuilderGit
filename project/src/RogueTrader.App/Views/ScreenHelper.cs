using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace RogueTrader.App.Views;

/// <summary>
/// Экранные размеры в логических единицах WPF.
///
/// SystemParameters.WorkArea в .NET (Core) WPF отдаёт размеры в логических
/// единицах, а Win32 — в сырых пикселях. Чтобы окно точно помещалось на
/// экране при любом масштабе, берём рабочую область у Win32 и делим на тот
/// же коэффициент масштаба, которым WPF верстает (VisualTreeHelper.GetDpi).
/// </summary>
internal static class ScreenHelper
{
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

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    /// <summary>
    /// Поднять окно поверх остальных.
    ///
    /// Окна со своей рамкой (WindowStyle.None + WindowChrome) WPF показывает
    /// позади владельца: нажимаешь кнопку — а диалога не видно. Приём с
    /// Topmost работает, только если снять его чуть позже: иначе менеджёр
    /// окон обрабатывает оба сообщения подряд и окно остаётся на прежнем
    /// уровне. Поэтому поднимаем через Win32 и снимаем Topmost по таймеру.
    /// </summary>
    public static void BringToFront(Window window)
    {
        window.Activate();

        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            ShowWindow(hwnd, 5);           // SW_SHOW
            SetForegroundWindow(hwnd);
        }

        window.Topmost = true;
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150),
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            window.Topmost = false;
        };
        timer.Start();
        window.Focus();
    }

    /// <summary>Рабочая область монитора окна в логических единицах.</summary>
    public static Rect WorkAreaInDips(Window window)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return Rect.Empty;

        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero) return Rect.Empty;

        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref mi)) return Rect.Empty;

        // Масштаб берём у WPF: он гарантированно совпадает с тем, которым
        // приложение верстает.
        var scale = VisualTreeHelper.GetDpi(window).DpiScaleX;
        if (scale <= 0 || double.IsNaN(scale) || double.IsInfinity(scale)) scale = 1.0;

        return new Rect(
            mi.rcWork.Left / scale,
            mi.rcWork.Top / scale,
            (mi.rcWork.Right - mi.rcWork.Left) / scale,
            (mi.rcWork.Bottom - mi.rcWork.Top) / scale);
    }
}
