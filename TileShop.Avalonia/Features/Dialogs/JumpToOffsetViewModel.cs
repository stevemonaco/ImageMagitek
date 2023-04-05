using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.UI.Windowing;

namespace TileShop.UI.ViewModels;

public enum NumericBase { Decimal, Hexadecimal }

public partial class JumpToOffsetViewModel : DialogViewModel<long?>
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(JumpToOffsetViewModel), nameof(ValidateModel))]
    private string? _offsetText;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(JumpToOffsetViewModel), nameof(ValidateModel))]
    private NumericBase _numericBase;

    [ObservableProperty] private string _validationError = string.Empty;

    private bool _canJump;

    public JumpToOffsetViewModel()
    {
        Title = "Jump to Offset";
        AcceptName = "✓";
        CancelName = "x";
    }

    protected override void Accept()
    {
        if (OffsetText is null)
            return;

        if (NumericBase == NumericBase.Decimal)
            RequestResult = long.Parse(OffsetText);
        else if (NumericBase == NumericBase.Hexadecimal)
            RequestResult = long.Parse(OffsetText, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    protected override bool CanAccept() => _canJump;

    public static ValidationResult ValidateModel(string input, ValidationContext context)
    {
        var model = (JumpToOffsetViewModel)context.ObjectInstance;
        model._canJump = false;

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

        model._canJump = true;
        return ValidationResult.Success!;
    }
}
