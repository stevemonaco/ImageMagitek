using Stylet;
using System.Globalization;

namespace TileShop.WPF.ViewModels;

public enum NumericBase { Decimal = 0, Hexadecimal = 1 }

public class JumpToOffsetViewModel : Screen
{
    private string _offset;
    public string Offset
    {
        get => _offset;
        set => SetAndNotify(ref _offset, value);
    }

    private NumericBase numericBase;
    public NumericBase NumericBase
    {
        get => numericBase;
        set => SetAndNotify(ref numericBase, value);
    }

    private bool _canJump;
    public bool CanJump
    {
        get => _canJump;
        set => SetAndNotify(ref _canJump, value);
    }

    private string _validationError;
    public string ValidationError
    {
        get => _validationError;
        set => SetAndNotify(ref _validationError, value);
    }

    public long Result { get; set; }

    public void Jump()
    {
        RequestClose(true);
    }

    public void Cancel() => RequestClose(false);

    public void ValidateModel()
    {
        if (NumericBase == NumericBase.Decimal)
        {
            if (long.TryParse(Offset, out var result))
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
            if (long.TryParse(Offset, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
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
