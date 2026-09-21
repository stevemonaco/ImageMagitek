using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TileShop.Shared.Interactions;
using TileShop.Shared.Models;

namespace TileShop.UI.ViewModels;

public partial class JumpToOffsetViewModel : RequestViewModel<long?>
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TryAcceptCommand))]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(JumpToOffsetViewModel), nameof(ValidateOffsetText))]
    private string? _offsetText;

    [ObservableProperty] private NumericBase _numericBase;

    public JumpToOffsetViewModel(long currentOffset, NumericBase numericBase)
    {
        Title = "Jump to Offset";
        AcceptName = "Jump";
        _numericBase = numericBase;
        _offsetText = FormatOffset(currentOffset, numericBase);
    }

    [RelayCommand]
    private void ToggleNumericBase() =>
        NumericBase = NumericBase == NumericBase.Decimal ? NumericBase.Hexadecimal : NumericBase.Decimal;

    partial void OnNumericBaseChanged(NumericBase oldValue, NumericBase newValue)
    {
        if (TryParseOffset(OffsetText, oldValue, out var offset))
            OffsetText = FormatOffset(offset, newValue);
        else
            ValidateProperty(OffsetText, nameof(OffsetText));

        TryAcceptCommand.NotifyCanExecuteChanged();
    }

    protected override bool CanAccept() => TryParseOffset(OffsetText, NumericBase, out _);

    public override long? ProduceResult() =>
        TryParseOffset(OffsetText, NumericBase, out var offset) ? offset : null;

    public static ValidationResult ValidateOffsetText(string? text, ValidationContext context)
    {
        var model = (JumpToOffsetViewModel)context.ObjectInstance;

        if (string.IsNullOrWhiteSpace(text) || TryParseOffset(text, model.NumericBase, out _))
            return ValidationResult.Success!;

        return new(model.NumericBase == NumericBase.Hexadecimal
            ? "Not a valid hexadecimal offset"
            : "Not a valid decimal offset");
    }

    private static bool TryParseOffset(string? text, NumericBase numericBase, out long offset)
    {
        var span = text.AsSpan().Trim();

        if (numericBase == NumericBase.Hexadecimal)
        {
            if (span.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                span = span[2..];

            return long.TryParse(span, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out offset) && offset >= 0;
        }

        return long.TryParse(span, NumberStyles.None, CultureInfo.InvariantCulture, out offset);
    }

    private static string FormatOffset(long offset, NumericBase numericBase) =>
        numericBase == NumericBase.Hexadecimal ? offset.ToString("X") : offset.ToString(CultureInfo.InvariantCulture);
}
