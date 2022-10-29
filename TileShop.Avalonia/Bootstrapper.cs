using System.Linq;
using Serilog;
using Jot;
using Microsoft.Extensions.Logging;
using ImageMagitek.Services;
using ImageMagitek.Project.Serialization;
using TileShop.Shared.Services;
using TileShop.AvaloniaUI.ViewModels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TileShop.AvaloniaUI.Services;
using TileShop.Shared.Interactions;

namespace TileShop.AvaloniaUI;

public interface IAppBootstrapper<TViewModel> where TViewModel : class
{
    void ConfigureServices(IServiceCollection services);
    void ConfigureViews(IServiceCollection services);
    void ConfigureViewModels(IServiceCollection services);
    Task<bool> LoadConfigurations();
}

public class TileShopBootstrapper : IAppBootstrapper<ShellViewModel>
{
    private readonly Tracker _tracker = new Tracker();
    private LoggerFactory _loggerFactory;
    private bool _isStarting = true;

    public void ConfigureIoc(IServiceCollection services)
    {
        _loggerFactory = CreateLoggerFactory(BootstrapService.DefaultLogFileName);

        ConfigureImageMagitek(services);
        ConfigureJotTracker(_tracker, services);

        _isStarting = false;
    }

    private void ConfigureImageMagitek(IServiceCollection services)
    {
        var bootstrapper = new BootstrapService(_loggerFactory.CreateLogger<BootstrapService>());
        var settings = bootstrapper.ReadConfiguration(BootstrapService.DefaultConfigurationFileName);
        var paletteService = bootstrapper.CreatePaletteService(BootstrapService.DefaultPalettePath, settings);
        var codecService = bootstrapper.CreateCodecService(BootstrapService.DefaultCodecPath, BootstrapService.DefaultCodecSchemaFileName);
        var pluginService = bootstrapper.CreatePluginService(BootstrapService.DefaultPluginPath, codecService);
        var layoutService = bootstrapper.CreateTileLayoutService(BootstrapService.DefaultLayoutsPath);

        var defaultResources = paletteService.GlobalPalettes;
        var serializerFactory = new XmlProjectSerializerFactory(BootstrapService.DefaultResourceSchemaFileName,
            codecService.CodecFactory, paletteService.ColorFactory, defaultResources);
        var projectService = bootstrapper.CreateProjectService(serializerFactory, paletteService.ColorFactory);

        services.AddSingleton(settings);
        services.AddSingleton(paletteService);
        services.AddSingleton(codecService);
        services.AddSingleton(pluginService);
        services.AddSingleton(layoutService);
        services.AddSingleton(projectService);
    }

    public void ConfigureServices(IServiceCollection services)
    {
        var interactionService = new InteractionService(new ViewLocator());

        services.AddSingleton<IInteractionService>(interactionService);
        services.AddSingleton<IAsyncFileRequestService, AsyncFileRequestService>();
        services.AddSingleton<IDiskExploreService, DiskExploreService>();
        services.AddSingleton<IThemeService, ThemeService>();
    }

    public void ConfigureViews(IServiceCollection services)
    {
        var viewTypes = GetType().Assembly.GetTypes().Where(x => x.Name.EndsWith("View"));

        foreach (var viewType in viewTypes)
            services.AddTransient(viewType);

        //builder.RegisterType<ShellView>().OnActivated(x => _tracker.Track(x.Instance));
    }

    public void ConfigureViewModels(IServiceCollection services)
    {
        var vmTypes = GetType()
            .Assembly
            .GetTypes()
            .Where(x => x.Name.EndsWith("ViewModel"))
            .Where(x => !x.IsAbstract && !x.IsInterface);

        foreach (var vmType in vmTypes)
            services.AddTransient(vmType);

        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<EditorsViewModel>();
        services.AddSingleton<ProjectTreeViewModel>();
        services.AddSingleton<MenuViewModel>();
        services.AddSingleton<StatusViewModel>();
    }

    private void ConfigureJotTracker(Tracker tracker, IServiceCollection services)
    {
        //tracker.Configure<ShellView>()
        //    .Id(w => w.Name)
        //    .Properties(w => new { w.Top, w.Width, w.Height, w.Left, w.WindowState })
        //    .PersistOn(nameof(Window.Closing))
        //    .StopTrackingOn(nameof(Window.Closing));

        //tracker.Configure<ShellViewModel>()
        //    .Property(p => p.Theme, ApplicationTheme.Light);

        //tracker.Configure<AddScatteredArrangerViewModel>()
        //    .Property(p => p.ArrangerElementWidth, 8)
        //    .Property(p => p.ArrangerElementHeight, 16)
        //    .Property(p => p.ElementPixelWidth, 8)
        //    .Property(p => p.ElementPixelHeight, 8)
        //    .Property(p => p.ColorType, PixelColorType.Indexed)
        //    .Property(p => p.Layout, ElementLayout.Tiled);

        tracker.Configure<AddPaletteViewModel>()
            .Property(p => p.PaletteName)
            .Property(p => p.SelectedColorModel, "RGBA32")
            .Property(p => p.ZeroIndexTransparent, true);

        tracker.Configure<JumpToOffsetViewModel>()
            .Property(p => p.NumericBase, NumericBase.Decimal)
            .Property(p => p.OffsetText, string.Empty);

        tracker.Configure<MenuViewModel>()
            .Property(p => p.RecentProjectFiles);

        tracker.Configure<CustomElementLayoutViewModel>()
            .Property(p => p.Width, 1)
            .Property(p => p.Height, 1)
            .Property(p => p.FlowDirection, ElementLayoutFlowDirection.RowLeftToRight);

        services.AddSingleton(tracker);
    }

    private LoggerFactory CreateLoggerFactory(string logName)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Error()
            .WriteTo.File(logName, rollingInterval: RollingInterval.Month,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}{NewLine}")
            .CreateLogger();

        var factory = new LoggerFactory();
        factory.AddSerilog(Log.Logger);
        return factory;
    }

    public async Task<bool> LoadConfigurations() => true;

    //protected override void OnUnhandledException(DispatcherUnhandledExceptionEventArgs e)
    //{
    //    base.OnUnhandledException(e);

    //    Log.Error(e.Exception, "Unhandled exception");

    //    if (!_isStarting)
    //    {
    //        _container?.Resolve<IWindowManager>()?.ShowMessageBox($"{e.Exception.Message}", "Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
    //        e.Handled = true;
    //    }
    //}
}
