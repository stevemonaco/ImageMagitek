using System.IO;
using System.Linq;
using Stylet;
using Autofac;
using TileShop.Shared.Services;
using TileShop.WPF.Services;
using TileShop.WPF.ViewModels;
using Serilog;

namespace TileShop.WPF
{
    public class TileShopBootstrapper : AutofacBootstrapper<ShellViewModel>
    {
        protected override void ConfigureIoC(ContainerBuilder builder)
        {
            ConfigureLogging(builder);
            ConfigureServices(builder);
        }

        private void ConfigureServices(ContainerBuilder builder)
        {
            var paletteService = new PaletteService();
            paletteService.LoadJsonPalettes($"{Directory.GetCurrentDirectory()}\\pal");
            paletteService.DefaultPalette = paletteService.Palettes.Where(x => x.Name.Contains("DefaultRgba32")).First();
            builder.RegisterInstance<IPaletteService>(paletteService);

            var codecService = new CodecService(paletteService.DefaultPalette);
            codecService.LoadXmlCodecs($"{Directory.GetCurrentDirectory()}\\codecs");
            builder.RegisterInstance<ICodecService>(codecService);

            var projectService = new ProjectTreeService(codecService);
            builder.RegisterInstance<IProjectTreeService>(projectService);

            builder.RegisterType<FileSelectService>().As<IFileSelectService>();
            builder.RegisterType<UserPromptService>().As<IUserPromptService>();

            builder.RegisterType<MessageBoxView>().AsSelf();
        }

        private void ConfigureLogging(ContainerBuilder builder)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Error()
                .WriteTo.File("crashlog.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            Application.Dispatcher.UnhandledException += Dispatcher_UnhandledException;
        }

        private void Dispatcher_UnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Fatal(e.Exception, "Unhandled exception");
        }
    }
}
