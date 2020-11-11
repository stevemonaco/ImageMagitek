using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Serilog;
using Stylet;
using Autofac;
using Jot;
using ModernWpf;
using ImageMagitek;
using ImageMagitek.Services;
using TileShop.WPF.Configuration;
using TileShop.WPF.Services;
using TileShop.WPF.ViewModels;
using TileShop.WPF.Views;
using System.Windows.Controls;
using ImageMagitek.Colors;

namespace TileShop.WPF
{
    public class TileShopBootstrapper : AutofacBootstrapper<ShellViewModel>
    {
        private AppSettings _settings;
        private readonly Tracker _tracker = new Tracker();
        private IPaletteService _paletteService;
        private ICodecService _codecService;
        private IColorFactory _colorFactory;
        private IPluginService _pluginService;

        private readonly string _logFileName = "errorlog.txt";
        private readonly string _configName = "appsettings.json";
        private readonly string _palPath = "_palettes";
        private readonly string _codecPath = "_codecs";
        private readonly string _pluginPath = "_plugins";
        private readonly string _projectSchemaName = Path.Combine("_schemas", "GameDescriptorSchema.xsd");
        private readonly string _codecSchemaName = Path.Combine("_schemas", "CodecSchema.xsd");

        protected override void ConfigureIoC(ContainerBuilder builder)
        {
            ConfigureLogging(_logFileName, builder);
            ReadConfiguration(_configName, builder);
            ReadPalettes(_palPath, _settings, builder);
            ReadCodecs(_codecPath, _codecSchemaName, builder);
            LoadPlugins(_pluginPath, _codecService, builder);
            ConfigureSolutionService(_projectSchemaName, builder);
            ConfigureServices(builder);
            ConfigureJotTracker(builder);

            ToolTipService.ShowOnDisabledProperty.OverrideMetadata(typeof(Control), new FrameworkPropertyMetadata(true));
        }

        private void LoadPlugins(string pluginPath, ICodecService codecService, ContainerBuilder builder)
        {
            _pluginService = new PluginService();
            var fullPath = Path.GetFullPath(pluginPath);
            _pluginService.LoadCodecPlugins(fullPath);
            foreach (var codecPlugin in _pluginService.CodecPlugins)
            {
                codecService.AddOrUpdateCodec(codecPlugin.Value);
            }

            builder.RegisterInstance(_pluginService);
        }

        private static void ConfigureServices(ContainerBuilder builder)
        {
            builder.RegisterType<FileSelectService>().As<IFileSelectService>();
            builder.RegisterType<ViewModels.MessageBoxViewModel>().As<IMessageBoxViewModel>();
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

        private void ConfigureSolutionService(string schemaFileName, ContainerBuilder builder)
        {
            var defaultResources = _paletteService.GlobalPalettes;
            var solutionService = new ProjectService(_codecService, _colorFactory, defaultResources);
            solutionService.LoadSchemaDefinition(schemaFileName);
            builder.RegisterInstance<IProjectService>(solutionService);
        }

        private static void ConfigureLogging(string logName, ContainerBuilder builder)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Error()
                .WriteTo.File(logName, rollingInterval: RollingInterval.Month,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}{NewLine}")
                .CreateLogger();
        }

        private void ConfigureJotTracker(ContainerBuilder builder)
        {
            _tracker.Configure<ShellView>()
                .Id(w => w.Name)
                .Properties(w => new { w.Top, w.Width, w.Height, w.Left, w.WindowState })
                .PersistOn(nameof(Window.Closing))
                .StopTrackingOn(nameof(Window.Closing));

            _tracker.Configure<ShellViewModel>()
                .Property(p => p.Theme, ApplicationTheme.Light);

            _tracker.Configure<AddScatteredArrangerViewModel>()
                .Property(p => p.ArrangerElementWidth, 8)
                .Property(p => p.ArrangerElementHeight, 16)
                .Property(p => p.ElementPixelWidth, 8)
                .Property(p => p.ElementPixelHeight, 8)
                .Property(p => p.ColorType, PixelColorType.Indexed)
                .Property(p => p.Layout, ArrangerLayout.Tiled);

            _tracker.Configure<AddPaletteViewModel>()
                .Property(p => p.PaletteName)
                .Property(p => p.SelectedColorModel, "RGBA32")
                .Property(p => p.Entries, 2)
                .Property(p => p.FileOffset, 0)
                .Property(p => p.ZeroIndexTransparent, true);

            _tracker.Configure<JumpToOffsetViewModel>()
                .Property(p => p.NumericBase, NumericBase.Decimal)
                .Property(p => p.Offset, string.Empty);

            builder.RegisterInstance(_tracker);
        }

        private void ReadConfiguration(string jsonFileName, ContainerBuilder builder)
        {
            try
            {
                var json = File.ReadAllText(jsonFileName);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                _settings = JsonSerializer.Deserialize<AppSettings>(json, options);
                builder.RegisterInstance(_settings);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, $"Failed to read the configuration file '{jsonFileName}'");
                throw;
            }
        }

        private void ReadPalettes(string palettesPath, AppSettings settings, ContainerBuilder builder)
        {
            _colorFactory = new ColorFactory();
            _paletteService = new PaletteService(_colorFactory);

            foreach (var paletteName in settings.GlobalPalettes)
            {
                var paletteFileName = Path.Combine(palettesPath, $"{paletteName}.json");
                var palette = _paletteService.ReadJsonPalette(paletteFileName);
                _paletteService.GlobalPalettes.Add(palette);
            }
            _paletteService.SetDefaultPalette(_paletteService.GlobalPalettes.First());

            var nesPaletteFileName = Path.Combine(palettesPath, $"{settings.NesPalette}.json");
            var nesPalette = _paletteService.ReadJsonPalette(nesPaletteFileName);
            (_colorFactory as ColorFactory).SetNesPalette(nesPalette);
            (_paletteService as PaletteService).SetNesPalette(nesPalette);

            builder.RegisterInstance(_paletteService);
        }

        private void ReadCodecs(string codecsPath, string schemaFileName, ContainerBuilder builder)
        {
            _codecService = new CodecService(schemaFileName);
            var result = _codecService.LoadXmlCodecs(codecsPath);

            if (result.Value is MagitekResults.Failed fail)
            {
                Log.Error(string.Join(Environment.NewLine, fail.Reasons));
            }

            builder.RegisterInstance(_codecService);
        }

        protected override void OnUnhandledException(DispatcherUnhandledExceptionEventArgs e)
        {
            base.OnUnhandledException(e);

            Log.Error(e.Exception, "Unhandled exception");
            _container?.Resolve<IWindowManager>()?.ShowMessageBox($"{e.Exception.Message}", "Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
