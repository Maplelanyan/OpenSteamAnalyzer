using System.Net.Http;
using System.Windows;
using OpenSteamAnalyzer.Repositories;
using OpenSteamAnalyzer.Services;
using OpenSteamAnalyzer.ViewModels;
using Prism.DryIoc;
using Prism.Ioc;

namespace OpenSteamAnalyzer;

public partial class App : PrismApplication
{
    protected override Window CreateShell()
    {
        return Container.Resolve<MainWindow>();
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        var settingsRepository = new AppSettingsRepository();
        var settings = settingsRepository.Load();

        containerRegistry.RegisterInstance<IAppSettingsRepository>(settingsRepository);
        containerRegistry.RegisterInstance(settings);
        containerRegistry.RegisterInstance(SteamApiOptions.FromSettings(settings));
        containerRegistry.RegisterInstance(new HttpClient());

        containerRegistry.RegisterSingleton<ISteamCacheRepository, SteamCacheRepository>();
        containerRegistry.RegisterSingleton<IAnalyzerService, AnalyzerService>();
        containerRegistry.RegisterSingleton<ISteamApiService, SteamApiService>();
        containerRegistry.RegisterSingleton<ISteamIdResolverService, SteamIdResolverService>();
        containerRegistry.RegisterSingleton<IStoreDealsService, SteamStoreDealsService>();
        containerRegistry.RegisterSingleton<IImageCacheService, ImageCacheService>();
        containerRegistry.RegisterSingleton<DashboardViewModel>();
        containerRegistry.RegisterSingleton<DiscountsViewModel>();
        containerRegistry.RegisterSingleton<SettingsViewModel>();
        containerRegistry.RegisterSingleton<MainWindowViewModel>();
    }
}
