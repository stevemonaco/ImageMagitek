using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Caliburn.Micro;
using Microsoft.Xaml.Behaviors;
using System.Diagnostics;
using TileShop.Shared.Services;
using TileShop.WPF.Keybinding;
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
            _container.Instance<IPaletteService>(paletteService);

            var codecService = new CodecService(paletteService.DefaultPalette);
            codecService.LoadXmlCodecs(@"F:\Projects\ImageMagitek\codecs");

            _container.Instance<ICodecService>(codecService);

            var projectService = new ProjectTreeService(codecService);
            _container.Instance<IProjectTreeService>(projectService);

            _container.PerRequest<IFileSelectService, FileSelectService>()
                .PerRequest<IUserPromptService, UserPromptService>()
                .PerRequest<IDialogService, DialogService>();
        }

        protected override void Configure()
        {
            _container.Instance(_container);

            ConfigureServices();
            ConfigureKeybindTrigger();

            _container.Singleton<IWindowManager, WindowManager>()
                .Singleton<IEventAggregator, EventAggregator>();

            var viewModelTypes = GetType().Assembly.GetTypes()
                .Where(x => x.IsClass)
                .Where(x => x.Name.EndsWith("ViewModel"));

            foreach (var type in viewModelTypes)
                _container.RegisterPerRequest(type, type.ToString(), type);

            var originalInvoke = ActionMessage.InvokeAction;
            ActionMessage.InvokeAction = context =>
            {
                Debug.WriteLine($"Message: {context.Message} Target: {context.Target} View: {context.View}");
                originalInvoke(context);
            };
        }

        private void ConfigureKeybindTrigger()
        {
            var defaultCreateTrigger = Parser.CreateTrigger;

            Parser.CreateTrigger = (target, triggerText) =>
            {
                if (triggerText == null)
                {
                    return defaultCreateTrigger(target, null);
                }

                var triggerDetail = triggerText
                    .Replace("[", string.Empty)
                    .Replace("]", string.Empty);

                var splits = triggerDetail.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

                TriggerBase<UIElement> trigger = null;

                switch (splits[0])
                {
                    case "Key":
                        var key = (Key)Enum.Parse(typeof(Key), splits[1], true);
                        trigger = new KeyTrigger { Key = key };
                        break;

                    case "Gesture":
                        var mkg = (MultiKeyGesture)(new MultiKeyGestureConverter()).ConvertFrom(splits[1]);
                        trigger = new KeyTrigger { Modifiers = mkg.KeySequences[0].Modifiers, Key = mkg.KeySequences[0].Keys[0] };
                        break;
                }

                if (trigger is null)
                    return defaultCreateTrigger(target, triggerText);
                else
                    return trigger;
            };
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
