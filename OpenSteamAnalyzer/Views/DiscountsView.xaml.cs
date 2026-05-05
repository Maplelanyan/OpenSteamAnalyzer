using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OpenSteamAnalyzer.Models;
using OpenSteamAnalyzer.ViewModels;

namespace OpenSteamAnalyzer.Views;

public partial class DiscountsView : UserControl
{
    public DiscountsView()
    {
        InitializeComponent();
        Loaded += DiscountsView_OnLoaded;
    }

    private void DiscountsView_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is DiscountsViewModel viewModel && viewModel.Games.Count == 0 && !viewModel.IsBusy)
        {
            viewModel.RefreshCommand.Execute(null);
        }
    }

    private void DiscountsDataGrid_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        PageScrollViewer.ScrollToVerticalOffset(PageScrollViewer.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private void OpenStoreButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: StoreDiscountGame game }
            || (game.AppId is null && string.IsNullOrWhiteSpace(game.StoreUrl)))
        {
            return;
        }

        if (game.AppId is not null && TryOpenUri($"steam://store/{game.AppId.Value}"))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(game.StoreUrl))
        {
            TryOpenUri(game.StoreUrl);
        }
    }

    private static bool TryOpenUri(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri)
            {
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
