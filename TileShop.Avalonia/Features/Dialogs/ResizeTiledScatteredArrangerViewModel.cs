using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.AvaloniaUI.ViewExtenders;
using TileShop.Shared.Dialogs;

namespace TileShop.AvaloniaUI.ViewModels;

public partial class ResizeTiledScatteredArrangerViewModel : DialogViewModel<ResizeTiledScatteredArrangerViewModel>
{
    private readonly IWindowManager _windowManager;

    [ObservableProperty] private int _width;
    [ObservableProperty] private int _height;
    [ObservableProperty] private int _originalWidth;
    [ObservableProperty] private int _originalHeight;

    /// <param name="originalWidth">Width of the original arranger in elements</param>
    /// <param name="originalHeight">Height of the original arranger in elements</param>
    public ResizeTiledScatteredArrangerViewModel(IWindowManager windowManager, int originalWidth, int originalHeight)
    {
        _windowManager = windowManager;

        OriginalWidth = originalWidth;
        OriginalHeight = originalHeight;
        Width = originalWidth;
        Height = originalHeight;
        Title = "Resize Scattered Arranger";
    }
    
    public override async void Ok(ResizeTiledScatteredArrangerViewModel? result)
    {
        if (Width < OriginalWidth || Height < OriginalHeight)
        {
            var boxResult = await _windowManager.ShowMessageBox("The specified dimensions will shrink the arranger. Elements outside of the new arranger dimensions will be lost. Continue?", PromptChoice.YesNo);

            if (boxResult == PromptResult.No)
                return;
        }

        base.Ok(result);
    }
}
