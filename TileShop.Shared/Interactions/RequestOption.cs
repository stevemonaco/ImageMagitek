using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TileShop.Shared.Interactions;

/// <summary>
/// A single option presented to the user in a request interaction.
/// </summary>
public partial class RequestOption : ObservableObject
{
    [ObservableProperty] private string _optionText;
    [ObservableProperty] private bool _isDefault;
    [ObservableProperty] private bool _isCancel;
    
    public IAsyncRelayCommand OptionCommand { get; }

    public RequestOption(string optionText, IAsyncRelayCommand optionCommand)
    {
        _optionText = optionText;
        OptionCommand = optionCommand;
    }
}