namespace TileShop.UI.ViewExtenders.Dialogs;

/// <summary>
/// Specifies the visual mode of a dialog, affecting icon and button styling.
/// </summary>
public enum DialogMode
{
    /// <summary>No icon displayed.</summary>
    None,

    /// <summary>Informational dialog with blue icon.</summary>
    Info,

    /// <summary>Warning dialog with orange icon.</summary>
    Warning,

    /// <summary>Error dialog with red icon.</summary>
    Error,

    /// <summary>Question dialog with blue icon.</summary>
    Question,

    /// <summary>Success dialog with green icon.</summary>
    Success
}
