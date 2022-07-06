using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.AvaloniaUI.Windowing;

namespace TileShop.AvaloniaUI.ViewModels;

public enum NumericBase { Decimal = 0, Hexadecimal = 1 }

public partial class JumpToOffsetViewModel : DialogViewModel<long?>
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(JumpToOffsetViewModel), nameof(ValidateModel))]
    private string _offsetText;

    private NumericBase _numericBase;
    public NumericBase NumericBase
    {
        get => _numericBase;
        set
        {
            if (SetProperty(ref _numericBase, value))
                ValidateAllProperties();
        }
    }

    [ObservableProperty] private bool _canJump;
    [ObservableProperty] private string _validationError = string.Empty;
    [ObservableProperty] private long _result;

    public JumpToOffsetViewModel()
    {
        Title = "Jump to Offset";
    }

    public static ValidationResult ValidateModel(string input, ValidationContext context)
    {
        var model = (JumpToOffsetViewModel)context.ObjectInstance;

        if (model.NumericBase == NumericBase.Decimal)
        {
            if (long.TryParse(model.OffsetText, out var result))
            {
                if (result < 0)
                    return new("Offset cannot be negative");
            }
            else
            {
                return new("Could not parse as decimal");
            }
        }
        else if (model.NumericBase == NumericBase.Hexadecimal)
        {
            if (long.TryParse(model.OffsetText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
            {
                if (result < 0)
                    return new("Offset cannot be negative");
            }
            else
            {
                return new("Could not parse as hexadecimal");
            }
        }

        return ValidationResult.Success;
    }
}
