using System;

namespace TileShop.UI.ViewModels;

/// <summary>
/// How <see cref="EditorsViewModel.ResolveUnsavedChangesAsync"/> asks about each modified editor, in the caller's words
/// </summary>
/// <param name="DiscardOnNo">Whether No discards the editor's changes; otherwise No keeps them and the operation proceeds</param>
/// <param name="DiscardOnly">Asks Ok/Cancel instead of Yes/No/Cancel, discarding the changes only once every prompt is accepted</param>
/// <param name="SaveProject">Whether Yes also saves the editor's containing project</param>
public sealed record UnsavedChangesPrompt(string Title, Func<ResourceEditorBaseViewModel, string> Message, bool DiscardOnNo = true, bool DiscardOnly = false,
    bool SaveProject = false)
{
    public static UnsavedChangesPrompt Closing(string closing = "will be closed") =>
        new("Save changes", x => $"'{x.DisplayName}' has been modified and {closing}. Save changes?");

    public static UnsavedChangesPrompt Before(string verb, bool discardOnNo) =>
        new("Save Changes", x => $"'{x.DisplayName}' has unsaved changes. Save them before {verb}?", discardOnNo);

    public static UnsavedChangesPrompt DiscardForDeletion { get; } =
        new("Discard Changes", x => $"'{x.DisplayName}' has unsaved changes that use resources being deleted. These changes will be discarded.", DiscardOnly: true);
}
