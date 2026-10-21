using System.Windows;

namespace RogueTrader.App.Views;

/// <summary>
/// Окно с кратким руководством по созданию персонажа: порядок шагов,
/// из чего складывается характеристика и правила по книгам.
/// </summary>
public partial class GuideWindow : Window
{
    public GuideWindow()
    {
        InitializeComponent();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
