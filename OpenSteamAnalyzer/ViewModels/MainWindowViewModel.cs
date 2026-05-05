using Prism.Mvvm;

namespace OpenSteamAnalyzer.ViewModels;

public sealed class MainWindowViewModel : BindableBase
{
    private bool _isDashboardSelected = true;
    private bool _isDiscountsSelected;

    public MainWindowViewModel(DashboardViewModel dashboard, DiscountsViewModel discounts, SettingsViewModel settings)
    {
        Dashboard = dashboard;
        Discounts = discounts;
        Settings = settings;
        CurrentPage = dashboard;
        ShowDashboardCommand = new DelegateCommand(ShowDashboard);
        ShowDiscountsCommand = new DelegateCommand(ShowDiscounts);
        ShowSettingsCommand = new DelegateCommand(ShowSettings);
    }

    public DashboardViewModel Dashboard { get; }

    public DiscountsViewModel Discounts { get; }

    public SettingsViewModel Settings { get; }

    public object CurrentPage { get; private set; }

    public DelegateCommand ShowDashboardCommand { get; }

    public DelegateCommand ShowDiscountsCommand { get; }

    public DelegateCommand ShowSettingsCommand { get; }

    public bool IsDashboardSelected
    {
        get => _isDashboardSelected;
        private set
        {
            if (SetProperty(ref _isDashboardSelected, value))
            {
                RaisePropertyChanged(nameof(IsSettingsSelected));
            }
        }
    }

    public bool IsDiscountsSelected
    {
        get => _isDiscountsSelected;
        private set
        {
            if (SetProperty(ref _isDiscountsSelected, value))
            {
                RaisePropertyChanged(nameof(IsSettingsSelected));
            }
        }
    }

    public bool IsSettingsSelected => !IsDashboardSelected && !IsDiscountsSelected;

    private void ShowDashboard()
    {
        if (IsDashboardSelected)
        {
            return;
        }

        IsDashboardSelected = true;
        IsDiscountsSelected = false;
        CurrentPage = Dashboard;
        RaisePropertyChanged(nameof(CurrentPage));
    }

    private void ShowDiscounts()
    {
        if (IsDiscountsSelected)
        {
            return;
        }

        IsDashboardSelected = false;
        IsDiscountsSelected = true;
        CurrentPage = Discounts;
        RaisePropertyChanged(nameof(CurrentPage));
    }

    private void ShowSettings()
    {
        if (IsSettingsSelected)
        {
            return;
        }

        IsDashboardSelected = false;
        IsDiscountsSelected = false;
        CurrentPage = Settings;
        RaisePropertyChanged(nameof(CurrentPage));
    }
}
