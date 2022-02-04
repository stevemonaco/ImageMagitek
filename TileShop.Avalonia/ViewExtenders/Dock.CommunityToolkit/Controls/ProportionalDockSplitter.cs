using System.Runtime.Serialization;
using Dock.Model.Controls;

namespace TileShop.AvaloniaUI.ViewExtenders.Dock;

/// <summary>
/// Proportional dock splitter.
/// </summary>
[DataContract(IsReference = true)]
public class ProportionalDockSplitter : DockableBase, IProportionalDockSplitter
{
}