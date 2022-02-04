using System.Runtime.Serialization;
using Dock.Model.Controls;

namespace TileShop.AvaloniaUI.ViewExtenders.Dock;

/// <summary>
/// Document.
/// </summary>
[DataContract(IsReference = true)]
public class Document : DockableBase, IDocument
{
}