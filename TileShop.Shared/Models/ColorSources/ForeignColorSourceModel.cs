using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek.Colors;
using ImageMagitek.Utility.Parsing;

namespace TileShop.Shared.Models;

public partial class ForeignColorSourceModel : ColorSourceModel
{
    public ColorModel ColorModel { get; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(ForeignColorSourceModel), nameof(ValidateHexColor))]
    private string _foreignHexColor;

    public ForeignColorSourceModel(string foreignHexColor, ColorModel colorModel)
    {
        _foreignHexColor = foreignHexColor;
        ColorModel = colorModel;
        ValidateAllProperties();
    }

    public static ValidationResult ValidateHexColor(string hexColor, ValidationContext context)
    {
        var model = ((ForeignColorSourceModel)context.ObjectInstance).ColorModel;

        if (ColorParser.TryParse(hexColor, model, out _))
            return ValidationResult.Success!;

        return new ValidationResult($"Invalid {model} color string");
    }
}