using System.Windows;
using System.Windows.Controls;
using OpenSteamAnalyzer.ViewModels;

namespace OpenSteamAnalyzer.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        viewModel.UpdateApiKeyInput(ApiKeyPasswordBox.Password);
        if (viewModel.SaveCommand.CanExecute(null))
        {
            viewModel.SaveCommand.Execute(null);
            ApiKeyPasswordBox.Clear();
        }
    }
}
