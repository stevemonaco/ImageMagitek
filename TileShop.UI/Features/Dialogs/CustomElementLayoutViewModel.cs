using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek;
using TileShop.Shared.Interactions;

namespace TileShop.UI.ViewModels;

public enum ElementLayoutFlowDirection { RowLeftToRight, ColumnTopToBottom }

public sealed partial class CustomElementLayoutViewModel : RequestViewModel<TileLayout>
{
    [ObservableProperty] private ElementLayoutFlowDirection _flowDirection;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TryAcceptCommand))]
    private int _width = 2;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TryAcceptCommand))]
    private int _height = 2;

    public CustomElementLayoutViewModel()
    {
        Title = "Create Custom Element Layout";
        AcceptName = "Create";
    }

    protected override bool CanAccept() => Width > 0 && Height > 0;

    public override TileLayout? ProduceResult()
    {
        var columnMajor = FlowDirection == ElementLayoutFlowDirection.ColumnTopToBottom;
        return TileLayout.Create($"Custom {Width}x{Height} {(columnMajor ? "V" : "H")}", Width, Height, columnMajor);
    }
}
