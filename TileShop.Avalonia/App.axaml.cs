using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = new ServiceCollection();
            var bootstrapper = new TileShopBootstrapper();
            bootstrapper.ConfigureIoC(services);
            bootstrapper.ConfigureServices(services);
            bootstrapper.ConfigureViews(services);
            bootstrapper.ConfigureViewModels(services);
            await bootstrapper.LoadConfigurations();

            var provider = services.BuildServiceProvider();

            var mainView = provider.GetService<ShellView>();
            mainView!.DataContext = provider.GetService<ShellViewModel>();
            desktop.MainWindow = mainView;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
