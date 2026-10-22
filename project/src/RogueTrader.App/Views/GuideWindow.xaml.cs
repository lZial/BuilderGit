using System.Windows;
using System.Windows.Media;

namespace RogueTrader.App.Views;

/// <summary>
/// Окно с кратким руководством по созданию персонажа: порядок шагов,
/// из чего складывается характеристика и правила по книгам.
///
/// Масштаб содержимого совпадает с главным окном: если интерфейс увеличен,
/// руководство читается тем же шрифтом, а само окно растёт пропорционально —
/// иначе в кадр поместилось бы вдвое меньше текста.
/// </summary>
public partial class GuideWindow : Window
{
    private double _zoom = 1.0;

    /// <summary>
    /// Масштаб содержимого: 1 — обычный размер. Задаётся до показа окна:
    /// масштаб применяется сразу, и окно центрируется уже в нужном размере.
    /// </summary>
    public double Zoom
    {
        get => _zoom;
        set
        {
            _zoom = Math.Clamp(value, 0.5, 2.0);
            ApplyZoom();
        }
    }

    public GuideWindow()
    {
        InitializeComponent();
        ApplyZoom();
    }

    /// <summary>
    /// Применяет масштаб к содержимому и подгоняет размер окна:
    /// окно не должно вылезти за рабочую область экрана.
    /// </summary>
    private void ApplyZoom()
    {
        var zoom = Math.Clamp(Zoom, 0.5, 2.0);
        ZoomScale.ScaleX = zoom;
        ZoomScale.ScaleY = zoom;

        if (Math.Abs(zoom - 1.0) < 0.001) return;

        // Окно растёт вместе с масштабом, но никогда не меньше обычного
        // размера: даже если рабочая область невелика, окно не должно
        // превратиться в щель.
        var work = WorkAreaInDips();
        var w = Math.Max(Width, Math.Min(Width * zoom, work.Width * 0.95));
        var h = Math.Max(Height, Math.Min(Height * zoom, work.Height * 0.95));
        Width = w;
        Height = h;
    }

    /// <summary>
    /// Рабочая область монитора в логических единицах.
    /// SystemParameters.WorkArea в .NET отдаёт сырые пиксели, поэтому
    /// делим на коэффициент масштаба, которым WPF верстает.
    /// </summary>
    private Rect WorkAreaInDips()
    {
        var wa = SystemParameters.WorkArea;
        var dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        if (dpi <= 0 || double.IsNaN(dpi)) dpi = 1.0;
        return new Rect(0, 0, wa.Width / dpi, wa.Height / dpi);
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
