using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Serilog;
using Stylet;
using Autofac;
using Jot;
using ModernWpf;
using ImageMagitek;
using ImageMagitek.Services;
using TileShop.WPF.Services;
using TileShop.WPF.ViewModels;
using TileShop.WPF.Views;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using ImageMagitek.Project.Serialization;
using TileShop.Shared.Services;

namespace TileShop.WPF;

public class TileShopBootstrapper : AutofacBootstrapper<ShellViewModel>
{
    private readonly Tracker _tracker = new Tracker();
    private LoggerFactory _loggerFactory;
    private bool _isStarting = true;

    protected override void ConfigureIoC(ContainerBuilder builder)
    {
        _loggerFactory = CreateLoggerFactory(BootstrapService.DefaultLogFileName);

        ConfigureImageMagitek(builder);
        ConfigureServices(builder);
        ConfigureJotTracker(_tracker, builder);

        ToolTipService.ShowOnDisabledProperty.OverrideMetadata(typeof(Control), new FrameworkPropertyMetadata(true));
        _isStarting = false;
    }

    private void ConfigureImageMagitek(ContainerBuilder builder)
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

        builder.RegisterInstance(settings);
        builder.RegisterInstance(paletteService);
        builder.RegisterInstance(codecService);
        builder.RegisterInstance(pluginService);
        builder.RegisterInstance(layoutService);
        builder.RegisterInstance(projectService);
    }

    private static void ConfigureServices(ContainerBuilder builder)
    {
        builder.RegisterType<FileSelectService>().As<IFileSelectService>();
        builder.RegisterType<ViewModels.MessageBoxViewModel>().As<IMessageBoxViewModel>();
        builder.RegisterType<DiskExploreService>().As<IDiskExploreService>();
    }

    protected override void ConfigureViews(ContainerBuilder builder)
    {
        var viewTypes = GetType().Assembly.GetTypes().Where(x => x.Name.EndsWith("View"));

        foreach (var viewType in viewTypes)
            builder.RegisterType(viewType);

        builder.RegisterType<ShellView>().OnActivated(x => _tracker.Track(x.Instance));
    }

    protected override void ConfigureViewModels(ContainerBuilder builder)
    {
        var vmTypes = GetType()
            .Assembly
            .GetTypes()
            .Where(x => x.Name.EndsWith("ViewModel"))
            .Where(x => !x.IsAbstract && !x.IsInterface);

        foreach (var vmType in vmTypes)
            builder.RegisterType(vmType);

        builder.RegisterType<ShellViewModel>().SingleInstance().OnActivated(x => _tracker.Track(x.Instance));
        builder.RegisterType<EditorsViewModel>().SingleInstance();
        builder.RegisterType<ProjectTreeViewModel>().SingleInstance();
        builder.RegisterType<MenuViewModel>().SingleInstance();
        builder.RegisterType<StatusBarViewModel>().SingleInstance();
    }

    private static void ConfigureJotTracker(Tracker tracker, ContainerBuilder builder)
    {
        tracker.Configure<ShellView>()
            .Id(w => w.Name)
            .Properties(w => new { w.Top, w.Width, w.Height, w.Left, w.WindowState })
            .PersistOn(nameof(Window.Closing))
            .StopTrackingOn(nameof(Window.Closing));

        tracker.Configure<ShellViewModel>()
            .Property(p => p.Theme, ApplicationTheme.Light);

        tracker.Configure<AddScatteredArrangerViewModel>()
            .Property(p => p.ArrangerElementWidth, 8)
            .Property(p => p.ArrangerElementHeight, 16)
            .Property(p => p.ElementPixelWidth, 8)
            .Property(p => p.ElementPixelHeight, 8)
            .Property(p => p.ColorType, PixelColorType.Indexed)
            .Property(p => p.Layout, ElementLayout.Tiled);

        tracker.Configure<AddPaletteViewModel>()
            .Property(p => p.PaletteName)
            .Property(p => p.SelectedColorModel, "RGBA32")
            .Property(p => p.ZeroIndexTransparent, true);

        tracker.Configure<JumpToOffsetViewModel>()
            .Property(p => p.NumericBase, NumericBase.Decimal)
            .Property(p => p.Offset, string.Empty);

        tracker.Configure<MenuViewModel>()
            .Property(p => p.RecentProjectFiles);

        tracker.Configure<CustomElementLayoutViewModel>()
            .Property(p => p.Width, 1)
            .Property(p => p.Height, 1)
            .Property(p => p.FlowDirection, ElementLayoutFlowDirection.RowLeftToRight);

        builder.RegisterInstance(tracker);
    }

    private static LoggerFactory CreateLoggerFactory(string logName)
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

    protected override void OnUnhandledException(DispatcherUnhandledExceptionEventArgs e)
    {
        base.OnUnhandledException(e);

        Log.Error(e.Exception, "Unhandled exception");
        
        if (!_isStarting)
        {
            _container?.Resolve<IWindowManager>()?.ShowMessageBox($"{e.Exception.Message}", "Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
