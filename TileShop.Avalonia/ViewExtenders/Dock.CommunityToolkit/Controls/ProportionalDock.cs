using System.Runtime.Serialization;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace TileShop.AvaloniaUI.ViewExtenders.Dock;

/// <summary>
/// Proportional dock.
/// </summary>
[DataContract(IsReference = true)]
public class ProportionalDock : DockBase, IProportionalDock
{
    private Orientation _orientation;

    /// <inheritdoc/>
    [DataMember(IsRequired = false, EmitDefaultValue = true)]
    public Orientation Orientation
    {
        get => _orientation;
        set => SetProperty(ref _orientation, value);
    }
}