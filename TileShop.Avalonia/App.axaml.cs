using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.DependencyInjection;
using ImageMagitek.Services;
using Microsoft.Extensions.DependencyInjection;
using TileShop.AvaloniaUI.ViewModels;
using TileShop.AvaloniaUI.Views;
using TileShop.Shared.Services;

namespace TileShop.AvaloniaUI;
public class App : Application
{
    private ShellView? _shellView;
    private ShellViewModel? _shellViewModel;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Remove Avalonia data validation so that Mvvm Toolkit's data validation works
        ExpressionObserver.DataValidators.RemoveAll(x => x is DataAnnotationsValidationPlugin);

        var services = new ServiceCollection();
        var bootstrapper = new TileShopBootstrapper();
        bootstrapper.ConfigureIoc(services);
        bootstrapper.ConfigureServices(services);
        bootstrapper.ConfigureViews(services);
        bootstrapper.ConfigureViewModels(services);
        bootstrapper.LoadConfigurations();
        
        var provider = services.BuildServiceProvider();

        Ioc.Default.ConfigureServices(provider);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _shellViewModel = provider.GetService<ShellViewModel>();
            _shellView = provider.GetService<ShellView>();
            _shellView!.DataContext = _shellViewModel;

            desktop.MainWindow = _shellView;
            desktop.ShutdownRequested += Desktop_ShutdownRequested;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void Desktop_ShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (_shellViewModel is not null)
        {
            var canClose = await _shellViewModel.PrepareApplicationExit();
            e.Cancel = !canClose;
        }
        else
        {
            e.Cancel = false;
        }
    }
}
