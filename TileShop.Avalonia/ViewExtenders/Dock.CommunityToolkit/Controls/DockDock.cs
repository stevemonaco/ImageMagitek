using System.Runtime.Serialization;
using Dock.Model.Controls;

namespace TileShop.AvaloniaUI.ViewExtenders.Dock;

/// <summary>
/// Docking panel dock.
/// </summary>
[DataContract(IsReference = true)]
public class DockDock : DockBase, IDockDock
{
    private bool _lastChildFill = true;

    /// <inheritdoc/>
    [DataMember(IsRequired = false, EmitDefaultValue = true)]
    public bool LastChildFill
    {
        get => _lastChildFill;
        set => SetProperty(ref _lastChildFill, value);
    }
}