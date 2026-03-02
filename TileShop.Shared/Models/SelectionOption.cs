using CommunityToolkit.Mvvm.ComponentModel;

namespace TileShop.Shared.Models;

public partial class SelectionOption : ObservableObject
{
    public object Value { get; }
    public string? DisplayText { get; }
    public string? Description { get; }

    [ObservableProperty] private bool _isSelected;

    public SelectionOption(object value, string? displayText = null, string? description = null)
    {
        Value = value;
        DisplayText = displayText;
        Description = description;
    }

    public override string ToString() => (DisplayText ?? Value.ToString()) ?? "null";
}

public class SelectionOption<T> : SelectionOption
    where T : notnull
{
    public new T Value => (T)base.Value;

    public SelectionOption(T value, string? displayText = null, string? description = null) :
        base(value, displayText, description)
    {
    }
}
