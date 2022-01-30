using ImageMagitek;
using TileShop.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TileShop.WPF.Models;

public partial class ArrangerSelection : ObservableObject
{
    public Arranger Arranger { get; private set; }

    [ObservableProperty] private SnappedRectangle _selectionRect;
    [ObservableProperty] private SnapMode _snapMode;
    [ObservableProperty] private bool _hasSelection;

    public ArrangerSelection(Arranger arranger, SnapMode snapMode)
    {
        Arranger = arranger;
        SelectionRect = new SnappedRectangle(Arranger.ArrangerPixelSize, Arranger.ElementPixelSize, snapMode, ElementSnapRounding.Expand);
        SnapMode = snapMode;
    }

    /// <summary>
    /// Starts a new selection
    /// </summary>
    /// <param name="x">X-coordinate of selection end point in pixel coordinates</param>
    /// <param name="y">Y-coordinate of selection end point in pixel coordinates</param>
    public void StartSelection(double x, double y)
    {
        SelectionRect.SetBounds(x, x, y, y);
        HasSelection = true;
    }

    /// <summary>
    /// Updates the endpoint for the selection
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    public void UpdateSelectionEndpoint(double x, double y)
    {
        if (HasSelection)
            SelectionRect.SetEndpoint(x, y);
    }

    /// <summary>
    /// Cancels any selection
    /// </summary>
    public void Cancel()
    {
        HasSelection = false;
    }
}
