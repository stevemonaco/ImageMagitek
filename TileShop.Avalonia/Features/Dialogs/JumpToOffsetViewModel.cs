using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.AvaloniaUI.Windowing;

namespace TileShop.AvaloniaUI.ViewModels;

public enum NumericBase { Decimal = 0, Hexadecimal = 1 }

public partial class JumpToOffsetViewModel : DialogViewModel<long?>
{
    private string? _offsetText;
    public string? OffsetText
    {
        get => _offsetText;
        set
        {
            if (SetProperty(ref _offsetText, value))
                ValidateModel();
        }
    }

    [ObservableProperty] private NumericBase numericBase;
    [ObservableProperty] private bool _canJump;
    [ObservableProperty] private string _validationError = string.Empty;

    public long Result { get; set; }

    public JumpToOffsetViewModel()
    {
        Title = "Jump to Offset";
    }

    public void ValidateModel()
    {
        if (NumericBase == NumericBase.Decimal)
        {
            if (long.TryParse(OffsetText, out var result))
            {
                if (result >= 0)
                    Success(result);
                else
                    Fail("Offset cannot be negative");
            }
            else
                Fail("Could not parse as decimal");
        }
        else if (NumericBase == NumericBase.Hexadecimal)
        {
            if (long.TryParse(OffsetText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
            {
                if (result >= 0)
                    Success(result);
                else
                    Fail("Offset cannot be negative");
            }
            else
                Fail("Could not parse as hexadecimal");
        }

        void Success(long result)
        {
            CanJump = true;
            ValidationError = string.Empty;
            Result = result;
        }

        void Fail(string validationError)
        {
            CanJump = false;
            ValidationError = validationError;
        }
    }
}
