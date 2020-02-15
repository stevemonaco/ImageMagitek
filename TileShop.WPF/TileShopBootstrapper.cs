using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;
using Stylet;
using Autofac;
using TileShop.Shared.Services;
using TileShop.WPF.Keybinding;
using TileShop.WPF.Services;
using TileShop.WPF.ViewModels;

namespace TileShop.WPF
{
    public class TileShopBootstrapper : AutofacBootstrapper<ShellViewModel>
    {
        protected override void ConfigureIoC(ContainerBuilder builder)
        {
            ConfigureServices(builder);
        }

        private void ConfigureServices(ContainerBuilder builder)
        {
            var paletteService = new PaletteService();
            paletteService.LoadJsonPalettes(@"D:\ImageMagitek\pal");
            paletteService.DefaultPalette = paletteService.Palettes.Where(x => x.Name.Contains("DefaultRgba32")).First();
            builder.RegisterInstance<IPaletteService>(paletteService);

            var codecService = new CodecService(paletteService.DefaultPalette);
            codecService.LoadXmlCodecs(@"D:\ImageMagitek\codecs");
            builder.RegisterInstance<ICodecService>(codecService);

            var projectService = new ProjectTreeService(codecService);
            builder.RegisterInstance<IProjectTreeService>(projectService);

            builder.RegisterType<FileSelectService>().As<IFileSelectService>();
            builder.RegisterType<UserPromptService>().As<IUserPromptService>();
        }

        /*
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
        }*/
    }
}
