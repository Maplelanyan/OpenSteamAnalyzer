using System.Windows;
using OpenSteamAnalyzer.ViewModels;

namespace OpenSteamAnalyzer;

public partial class MainWindow : Window
{
    public MainWindow(DashboardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
