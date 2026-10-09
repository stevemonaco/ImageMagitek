using System;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek;
using TileShop.Shared.Interactions;

namespace TileShop.UI.ViewModels;

public partial class NameResourceViewModel : RequestViewModel<string?>
{
    private readonly Func<string, MagitekResult> _validate;

    [ObservableProperty] private string? _resourceName;
    [ObservableProperty] private string? _errorText;

    public NameResourceViewModel(string title, string initialName, Func<string, MagitekResult> validate)
    {
        _validate = validate;
        _resourceName = initialName;
        Title = title;
        AcceptName = "✓";
        CancelName = "x";
        UpdateError();
    }

    partial void OnResourceNameChanged(string? value) => UpdateError();

    private void UpdateError()
    {
        var result = _validate(ResourceName ?? "");
        ErrorText = result.HasFailed ? result.AsError.Reason : null;
        TryAcceptCommand.NotifyCanExecuteChanged();
    }

    protected override bool CanAccept() => ErrorText is null;

    public override string? ProduceResult() => ResourceName;
}
