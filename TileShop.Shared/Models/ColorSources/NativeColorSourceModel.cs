using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek.Utility.Parsing;

namespace TileShop.Shared.Models;

public partial class NativeColorSourceModel : ColorSourceModel
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(NativeColorSourceModel), nameof(ValidateHexColor))]
    private string _nativeHexColor;

    public NativeColorSourceModel(string nativeHexColor)
    {
        _nativeHexColor = nativeHexColor;
    }

    public static ValidationResult ValidateHexColor(string hexColor, ValidationContext context)
    {
        if (ColorParser.TryParse(hexColor, ImageMagitek.Colors.ColorModel.Rgba32, out _))
        {
            return ValidationResult.Success!;
        }
        else
        {
            return new ValidationResult("Invalid color string");
        }
    }
}
