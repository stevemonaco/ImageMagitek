using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.UI.Windowing;
using TileShop.Shared.Interactions;

namespace TileShop.UI.ViewModels;

public partial class ResizeTiledScatteredArrangerViewModel : DialogViewModel<ResizeTiledScatteredArrangerViewModel>
{
    private readonly IInteractionService _interactions;

    [ObservableProperty] private int _width;
    [ObservableProperty] private int _height;
    [ObservableProperty] private int _originalWidth;
    [ObservableProperty] private int _originalHeight;

    /// <param name="interactionService"></param>
    /// <param name="originalWidth">Width of the original arranger in elements</param>
    /// <param name="originalHeight">Height of the original arranger in elements</param>
    public ResizeTiledScatteredArrangerViewModel(IInteractionService interactionService, int originalWidth, int originalHeight)
    {
        _interactions = interactionService;

        OriginalWidth = originalWidth;
        OriginalHeight = originalHeight;
        Width = originalWidth;
        Height = originalHeight;
        Title = "Resize Scattered Arranger";
        AcceptName = "Resize";
    }

    protected override async void Accept()
    {
        if (Width < OriginalWidth || Height < OriginalHeight)
        {
            var boxResult = await _interactions.PromptAsync(PromptChoices.YesNo, "The specified dimensions will shrink the arranger. Elements outside of the new arranger dimensions will be lost. Continue?");

            if (boxResult == PromptResult.Reject)
                return;
        }

        _requestResult = this;
        OnPropertyChanged(nameof(RequestResult));
    }
}
