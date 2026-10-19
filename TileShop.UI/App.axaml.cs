using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TileShop.Shared.Interactions;
using TileShop.UI.ViewModels;
using TileShop.UI.Views;

namespace TileShop.UI;
public class App : Application
{
    private ShellView? _shellView;
    private IInteractionService? _interactions;
    private bool _isShowingUnhandledError;

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

        _interactions = provider.GetRequiredService<IInteractionService>();
        Dispatcher.UIThread.UnhandledException += Dispatcher_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

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

    private void Dispatcher_UnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        ReportUnhandledException(e.Exception);
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        Dispatcher.UIThread.Post(() => ReportUnhandledException(e.Exception));
    }

    private async void ReportUnhandledException(Exception ex)
    {
        Log.Error(ex, "Unhandled exception");

        // An exception that recurs every frame would otherwise stack alerts endlessly
        if (_isShowingUnhandledError || _interactions is null)
            return;

        _isShowingUnhandledError = true;
        try
        {
            await _interactions.AlertAsync("Unexpected Error",
                $"TileShop hit an unexpected error and kept running. Save your work, and restart if anything looks wrong.\n\n{ex.Message}\n\nThe full error is in the log in {TileShopBootstrapper.LogDirectory}");
        }
        catch (Exception alertEx)
        {
            Log.Error(alertEx, "Could not show the unhandled exception alert");
        }
        finally
        {
            _isShowingUnhandledError = false;
        }
    }
}
