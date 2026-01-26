namespace TileShop.Shared.Interactions;

public enum PromptResult { Cancel, Accept, Reject }
public record PromptChoice(string? Accept = null, string? Reject = null, string? Cancel = null);

public static class PromptChoices
{
    public static PromptChoice Ok { get; } = new("Ok");
    public static PromptChoice OkCancel { get; } = new("Ok", null, "Cancel");
    public static PromptChoice YesNo { get; } = new("Yes", "No");
    public static PromptChoice YesNoCancel { get; } = new("Yes", "No", "Cancel");
}
