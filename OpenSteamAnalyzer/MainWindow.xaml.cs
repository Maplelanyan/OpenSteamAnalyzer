using System.Windows;
using OpenSteamAnalyzer.ViewModels;

namespace OpenSteamAnalyzer;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
