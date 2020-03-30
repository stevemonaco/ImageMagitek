using System.IO;
using System.Linq;
using Stylet;
using Autofac;
using TileShop.Shared.Services;
using TileShop.WPF.Services;
using TileShop.WPF.ViewModels;
using Serilog;
using TileShop.WPF.Configuration;
using System.Collections.Generic;
using System.Text.Json;
using System;
using Jot;
using ImageMagitek;
using System.Windows;

namespace TileShop.WPF
{
    public class TileShopBootstrapper : AutofacBootstrapper<ShellViewModel>
    {
        private Tracker _tracker = new Tracker();

        protected override void ConfigureIoC(ContainerBuilder builder)
        {
            ConfigureLogging(builder);
            ReadConfiguration("appsettings.json", builder);
            ConfigureServices(builder);
            ConfigureJotTracker(builder);
        }

        private void ConfigureServices(ContainerBuilder builder)
        {
            var paletteService = new PaletteService();
            paletteService.LoadJsonPalettes("pal");
            paletteService.DefaultPalette = paletteService.Palettes.Where(x => x.Name.Contains("DefaultRgba32")).First();
            builder.RegisterInstance<IPaletteService>(paletteService);

            var codecService = new CodecService(paletteService.DefaultPalette);
            codecService.LoadXmlCodecs("codecs");
            builder.RegisterInstance<ICodecService>(codecService);

            var projectService = new ProjectTreeService(codecService);
            builder.RegisterInstance<IProjectTreeService>(projectService);

            builder.RegisterType<FileSelectService>().As<IFileSelectService>();
            builder.RegisterType<UserPromptService>().As<IUserPromptService>();

            builder.RegisterType<MessageBoxView>().AsSelf();
        }

        protected override void ConfigureViewModels(ContainerBuilder builder)
        {
            var vmTypes = GetType().Assembly.GetTypes().Where(x => x.Name.EndsWith("ViewModel"));

            foreach (var vmType in vmTypes)
                builder.RegisterType(vmType).OnActivated(x => _tracker.Track(x));
        }

        private void ConfigureLogging(ContainerBuilder builder)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Error()
                .WriteTo.File("crashlog.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            Application.Dispatcher.UnhandledException += Dispatcher_UnhandledException;
        }

        private void ConfigureJotTracker(ContainerBuilder builder)
        {
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

                var settings = JsonSerializer.Deserialize<AppSettings>(json, options);
                builder.RegisterInstance(settings);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, $"Failed to read the configuration file '{jsonFileName}'");
                throw;
            }
        }

        private void Dispatcher_UnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Fatal(e.Exception, "Unhandled exception");
        }
    }
}
