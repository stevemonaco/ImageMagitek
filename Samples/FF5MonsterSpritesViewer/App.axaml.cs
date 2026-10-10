using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;

namespace FF5MonsterSprites;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new ShellViewModel();
            var view = new ShellView { DataContext = viewModel };

            view.Opened += async (_, _) => await viewModel.LoadMonstersAsync(() => PickRomAsync(view));

            desktop.MainWindow = view;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task<string?> PickRomAsync(TopLevel topLevel)
    {
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Final Fantasy V ROM",
            FileTypeFilter =
            [
                new FilePickerFileType("SNES ROM") { Patterns = ["*.sfc", "*.smc"] },
                FilePickerFileTypes.All
            ]
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }
}
