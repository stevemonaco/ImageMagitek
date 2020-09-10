using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using Serilog;
using Stylet;
using Autofac;
using Jot;
using ImageMagitek;
using ImageMagitek.Services;
using TileShop.WPF.Configuration;
using TileShop.WPF.Services;
using TileShop.WPF.ViewModels;
using ModernWpf;
using System.Windows.Threading;

namespace TileShop.WPF
{
    public class TileShopBootstrapper : AutofacBootstrapper<ShellViewModel>
    {
        private AppSettings _settings;
        private Tracker _tracker = new Tracker();
        private IPaletteService _paletteService;
        private ICodecService _codecService;

        private readonly string _logFileName = "errorlog.txt";
        private readonly string _configName = "appsettings.json";
        private readonly string _palPath = "pal";
        private readonly string _codecPath = "codecs";
        private readonly string _projectSchemaName = Path.Combine("schema", "GameDescriptorSchema.xsd");
        private readonly string _codecSchemaName = Path.Combine("schema", "CodecSchema.xsd");

        protected override void ConfigureIoC(ContainerBuilder builder)
        {
            ConfigureLogging(_logFileName, builder);
            ReadConfiguration(_configName, builder);
            ReadPalettes(_palPath, _settings, builder);
            ReadCodecs(_codecPath, _codecSchemaName, builder);
            ConfigureSolutionService(_projectSchemaName, builder);
            ConfigureServices(builder);
            ConfigureJotTracker(builder);
        }

        private void ConfigureServices(ContainerBuilder builder)
        {
            builder.RegisterType<FileSelectService>().As<IFileSelectService>();
            builder.RegisterType<ViewModels.MessageBoxViewModel>().As<IMessageBoxViewModel>();
        }

        protected override void ConfigureViewModels(ContainerBuilder builder)
        {
            var vmTypes = GetType().Assembly.GetTypes().Where(x => x.Name.EndsWith("ViewModel"));

            foreach (var vmType in vmTypes)
                builder.RegisterType(vmType).OnActivated(x => _tracker.Track(x));

            builder.RegisterType<EditorsViewModel>().SingleInstance();
            builder.RegisterType<ShellViewModel>().SingleInstance();
            builder.RegisterType<ProjectTreeViewModel>().SingleInstance();
            builder.RegisterType<MenuViewModel>().SingleInstance();
            builder.RegisterType<StatusBarViewModel>().SingleInstance();
        }

        private void ConfigureSolutionService(string schemaFileName, ContainerBuilder builder)
        {
            var solutionService = new ProjectService(_codecService);
            solutionService.LoadSchemaDefinition(schemaFileName);
            builder.RegisterInstance<IProjectService>(solutionService);
        }

        private void ConfigureLogging(string logName, ContainerBuilder builder)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Error()
                .WriteTo.File(logName, rollingInterval: RollingInterval.Day)
                .CreateLogger();
        }

        private void ConfigureJotTracker(ContainerBuilder builder)
        {
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
            _paletteService = new PaletteService();

            foreach (var paletteName in settings.DefaultPalettes)
            {
                var paletteFileName = Path.Combine(palettesPath, $"{paletteName}.json");
                _paletteService.LoadDefaultPalette(paletteFileName);
            }
            _paletteService.SetDefaultPalette(_paletteService.DefaultPalettes.First());

            var nesPaletteFileName = Path.Combine(palettesPath, $"{settings.NesPalette}.json");
            _paletteService.LoadNesPalette(nesPaletteFileName);

            builder.RegisterInstance(_paletteService);
        }

        private void ReadCodecs(string codecsPath, string schemaFileName, ContainerBuilder builder)
        {
            _codecService = new CodecService(schemaFileName, _paletteService.DefaultPalette);
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
