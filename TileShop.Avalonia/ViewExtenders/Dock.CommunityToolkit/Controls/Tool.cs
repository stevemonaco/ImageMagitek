using System.Runtime.Serialization;
using Dock.Model.Controls;

namespace TileShop.AvaloniaUI.ViewExtenders.Dock;

/// <summary>
/// Tool.
/// </summary>
[DataContract(IsReference = true)]
public class Tool : DockableBase, ITool, IDocument
{
}