using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TileShop.UI.ViewModels;
using TileShop.UI.Views;

namespace TileShop.UI;
public class App : Application
{
    private ShellView? _shellView;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        ServiceProvider provider;
        try
        {
            var services = new ServiceCollection();
            var bootstrapper = new TileShopBootstrapper();
            bootstrapper.ConfigureIoc(services);
            bootstrapper.ConfigureServices(services);
            bootstrapper.ConfigureViews(services);
            bootstrapper.ConfigureViewModels(services);
            bootstrapper.LoadConfigurations();

            provider = services.BuildServiceProvider();
            Ioc.Default.ConfigureServices(provider);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "TileShop failed to start");

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime errorDesktop)
                errorDesktop.MainWindow = new StartupErrorWindow(ex.Message, TileShopBootstrapper.LogDirectory);

            base.OnFrameworkInitializationCompleted();
            return;
        }

        var viewLocator = provider.GetRequiredService<ViewLocator>();
        DataTemplates.Add(viewLocator);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _shellView = provider.GetRequiredService<ShellView>();
            _shellView.DataContext = provider.GetRequiredService<ShellViewModel>();

            desktop.MainWindow = _shellView;
            desktop.ShutdownRequested += Desktop_ShutdownRequested;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void Desktop_ShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        e.Cancel = _shellView?.HandleCloseRequest() ?? false;
    }
}
