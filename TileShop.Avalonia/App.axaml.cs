using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using TileShop.AvaloniaUI.ViewModels;
using TileShop.AvaloniaUI.Views;

namespace TileShop.AvaloniaUI;
public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        var bootstrapper = new TileShopBootstrapper();
        bootstrapper.ConfigureIoC(services);
        bootstrapper.ConfigureServices(services);
        bootstrapper.ConfigureViews(services);
        bootstrapper.ConfigureViewModels(services);
        await bootstrapper.LoadConfigurations();

        var provider = services.BuildServiceProvider();

        Ioc.Default.ConfigureServices(provider);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var shellViewModel = provider.GetService<ShellViewModel>();
            var shellView = provider.GetService<ShellView>();
            shellView!.DataContext = shellViewModel;
            desktop.MainWindow = shellView;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
