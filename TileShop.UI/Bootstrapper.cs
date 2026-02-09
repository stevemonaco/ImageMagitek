using System;
using System.Linq;
using System.Threading.Tasks;
using ImageMagitek.Codec;
using ImageMagitek.Project.Serialization;
using ImageMagitek.Services;
using Jot;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Serilog;
using TileShop.UI.Models;
using TileShop.UI.Services;
using TileShop.UI.ViewModels;
using TileShop.Shared.Interactions;
using TileShop.Shared.Services;
using TileShop.UI.Controls.Dialogs;
using TileShop.UI.Features.Graphics;
using TileShop.UI.Views;

namespace TileShop.UI;

public interface IAppBootstrapper<TViewModel> where TViewModel : class
{
    void ConfigureServices(IServiceCollection services);
    void ConfigureViews(IServiceCollection services);
    void ConfigureViewModels(IServiceCollection services);
    bool LoadConfigurations();
}

public class TileShopBootstrapper : IAppBootstrapper<ShellViewModel>
{
    private readonly Tracker _tracker = new Tracker();
    private LoggerFactory? _loggerFactory;

    public async Task ConfigureIoc(IServiceCollection services)
    {
        _loggerFactory = CreateLoggerFactory(BootstrapService.DefaultLogFileName);

        await ConfigureImageMagitek(services);
        ConfigureJotTracker(_tracker, services);
    }

    private async Task ConfigureImageMagitek(IServiceCollection services)
    {
        var bootstrapper = new BootstrapService(_loggerFactory!.CreateLogger<BootstrapService>());

        var settingsService = bootstrapper.CreateSettingsService();
        var settings = await bootstrapper.ReadConfiguration(settingsService, BootstrapService.DefaultConfigurationFileName);
        if (settings is null)
            throw new InvalidOperationException($"'{BootstrapService.DefaultConfigurationFileName}' could not be read during startup");
        services.AddSingleton(settingsService);
        services.AddSingleton(settings);

        var colorFactory = bootstrapper.CreateColorFactory();
        var paletteService = bootstrapper.CreatePaletteService(colorFactory);
        services.AddSingleton(paletteService);

        var paletteStore = bootstrapper.CreatePaletteStore(paletteService, BootstrapService.DefaultPalettePath, settings);
        if (paletteStore.NesPalette is not null)
            colorFactory.SetNesPalette(paletteStore.NesPalette);
        services.AddSingleton(paletteStore);
        services.AddSingleton(colorFactory);

        var codecFactory = new CodecFactory(paletteStore.DefaultPalette, new());
        var codecService = bootstrapper.CreateCodecService(BootstrapService.DefaultCodecPath, BootstrapService.DefaultCodecSchemaFileName, codecFactory);
        services.AddSingleton(codecService);

        var pluginService = bootstrapper.CreatePluginService(BootstrapService.DefaultPluginPath, codecService);
        services.AddSingleton(pluginService);

        var layoutService = bootstrapper.CreateElementLayoutService();
        services.AddSingleton(layoutService);

        var elementStore = bootstrapper.CreateElementStore(layoutService, BootstrapService.DefaultLayoutsPath);
        services.AddSingleton(elementStore);

        var defaultResources = paletteStore.GlobalPalettes;
        var serializerFactory = new XmlProjectSerializerFactory(BootstrapService.DefaultResourceSchemaFileName,
            codecService.CodecFactory, colorFactory, defaultResources);
        var projectService = bootstrapper.CreateProjectService(serializerFactory, colorFactory);
        services.AddSingleton(projectService);
    }

    public void ConfigureServices(IServiceCollection services)
    {
        var viewLocator = new ViewLocator();
        ConfigureViewLocator(viewLocator);
        services.AddSingleton(viewLocator);
        
        var interactionService = new InteractionService(viewLocator);
        services.AddSingleton<IInteractionService>(interactionService);
        services.AddSingleton<IAsyncFileRequestService, AsyncFileRequestService>();
        services.AddSingleton<IExploreService, ExploreService>();
        services.AddSingleton<IThemeService, ThemeService>();
    }

    public void ConfigureViews(IServiceCollection services)
    {
        var assemblyNames = new[] { "TileShop.UI", "TileShop.UI.Controls" };
        
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => assemblyNames.Contains(a.GetName().Name));
        
        var viewTypes = assemblies.SelectMany(x => x.ExportedTypes)
            .Where(x => x.Name.EndsWith("View"));

        foreach (var viewType in viewTypes)
            services.AddTransient(viewType);

        //builder.RegisterType<ShellView>().OnActivated(x => _tracker.Track(x.Instance));
    }

    public void ConfigureViewModels(IServiceCollection services)
    {
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<EditorsViewModel>();
        services.AddSingleton<ProjectTreeViewModel>();
        services.AddSingleton<MenuViewModel>();
        services.AddSingleton<StatusViewModel>();

        var vmTypes = GetType()
            .Assembly
            .GetTypes()
            .Where(x => x.Name.EndsWith("ViewModel"))
            .Where(x => !x.IsAbstract && !x.IsInterface);

        foreach (var vmType in vmTypes)
            services.TryAddTransient(vmType);
    }

    public void ConfigureViewLocator(ViewLocator locator)
    {
        locator.RegisterViewFactory<ScatteredArrangerEditorViewModel, ScatteredArrangerEditorView>();
        locator.RegisterViewFactory<SequentialArrangerEditorViewModel, SequentialArrangerEditorView>();
        locator.RegisterViewFactory<GraphicsEditorViewModel, GraphicsEditorView>();
        locator.RegisterViewFactory<PaletteEditorViewModel, PaletteEditorView>();
        locator.RegisterViewFactory<TableColorViewModel, TableColorView>();
        locator.RegisterViewFactory<DirectPixelEditorViewModel, DirectPixelEditorView>();
        locator.RegisterViewFactory<IndexedPixelEditorViewModel, IndexedPixelEditorView>();
        locator.RegisterViewFactory<ProjectTreeViewModel, ProjectTreeView>();
        locator.RegisterViewFactory<DockableEditorViewModel, DockableEditorView>();
        locator.RegisterViewFactory<DockableToolViewModel, DockableToolView>();
        locator.RegisterViewFactory<MenuViewModel, MenuView>();
        locator.RegisterViewFactory<ShellViewModel, ShellView>();
        locator.RegisterViewFactory<StatusViewModel, StatusView>();
        locator.RegisterViewFactory<AddPaletteViewModel, AddPaletteView>();
        locator.RegisterViewFactory<AddScatteredArrangerViewModel, AddScatteredArrangerView>();
        locator.RegisterViewFactory<AssociatePaletteViewModel, AssociatePaletteView>();
        locator.RegisterViewFactory<ColorRemapViewModel, ColorRemapView>();
        locator.RegisterViewFactory<CustomElementLayoutViewModel, CustomElementLayoutView>();
        locator.RegisterViewFactory<ImportImageViewModel, ImportImageView>();
        locator.RegisterViewFactory<JumpToOffsetViewModel, JumpToOffsetView>();
        locator.RegisterViewFactory<ModifyGridSettingsViewModel, ModifyGridSettingsView>();
        locator.RegisterViewFactory<NameResourceViewModel, NameResourceView>();
        locator.RegisterViewFactory<RenameNodeViewModel, RenameNodeView>();
        locator.RegisterViewFactory<ResizeTiledScatteredArrangerViewModel, ResizeTiledScatteredArrangerView>();
        locator.RegisterViewFactory<ResourceRemovalChangesViewModel, ResourceRemovalChangesView>();
        locator.RegisterViewFactory<AlertViewModel, AlertView>();
        locator.RegisterViewFactory<PromptViewModel, PromptView>();
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
            .Property(p => p.ActiveTheme, ThemeStyle.Dark)
            .Property(p => p.RecentProjectFiles);

        tracker.Configure<CustomElementLayoutViewModel>()
            .Property(p => p.Width, 1)
            .Property(p => p.Height, 1)
            .Property(p => p.FlowDirection, ElementLayoutFlowDirection.RowLeftToRight);

        tracker.Configure<ModifyGridSettingsViewModel>()
            .Property(p => p.ShiftX, 0)
            .Property(p => p.ShiftY, 0)
            .Property(p => p.WidthSpacing, 8)
            .Property(p => p.HeightSpacing, 8)
            .Property(p => p.PrimaryColor, GridSettingsViewModel.DefaultPrimaryColor)
            .Property(p => p.SecondaryColor, GridSettingsViewModel.DefaultSecondaryColor)
            .Property(p => p.LineColor, GridSettingsViewModel.DefaultLineColor);

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

    public bool LoadConfigurations() => true;

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
