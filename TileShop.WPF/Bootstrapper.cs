using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using Caliburn.Micro;
using TileShop.Shared.Services;
using TileShop.WPF.Services;
using TileShop.WPF.ViewModels;

namespace TileShop.WPF
{
    public class Bootstrapper : BootstrapperBase
    {
        private SimpleContainer _container = new SimpleContainer();

        public Bootstrapper()
        {
            Initialize();
        }

        private void ConfigureServices()
        {
            var paletteService = new PaletteService();
            paletteService.LoadJsonPalettes(@"F:\Projects\ImageMagitek\pal");
            paletteService.DefaultPalette = paletteService.Palettes.Where(x => x.Name.Contains("DefaultRgba32")).First();

            var codecService = new CodecService(paletteService.DefaultPalette);
            codecService.LoadXmlCodecs(@"F:\Projects\ImageMagitek\codecs");

            _container.Instance<ICodecService>(codecService);

            var projectService = new ProjectTreeService(codecService);
            _container.Instance<IProjectTreeService>(projectService);

            _container.PerRequest<IFileSelectService, FileSelectService>();
        }

        protected override void Configure()
        {
            _container.Instance(_container);

            ConfigureServices();

            _container.Singleton<IWindowManager, WindowManager>()
                .Singleton<IEventAggregator, EventAggregator>();

            var viewModelTypes = GetType().Assembly.GetTypes()
                .Where(x => x.IsClass)
                .Where(x => x.Name.EndsWith("ViewModel"));

            foreach (var type in viewModelTypes)
                _container.RegisterPerRequest(type, type.ToString(), type);
        }

        protected override void OnStartup(object sender, StartupEventArgs e)
        {
            DisplayRootViewFor<ShellViewModel>();
        }

        protected override object GetInstance(Type service, string key)
        {
            return _container.GetInstance(service, key);
        }

        protected override IEnumerable<object> GetAllInstances(Type service)
        {
            return _container.GetAllInstances(service);
        }

        protected override void BuildUp(object instance)
        {
            _container.BuildUp(instance);
        }
    }
}
