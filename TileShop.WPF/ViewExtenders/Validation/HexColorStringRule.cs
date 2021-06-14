using ImageMagitek.Colors;
using ImageMagitek.Utility.Parsing;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace TileShop.WPF.ViewExtenders.Validation
{
    public class HexColorStringRule : ValidationRule
    {
        public HexColorStringWrapper Wrapper { get; set; }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (value is not string hexString)
                return new ValidationResult(false, "Input is null");

            if (ColorParser.TryParse(hexString, Wrapper.ColorModel, out _))
            {
                return ValidationResult.ValidResult;
            }

            return new ValidationResult(false, "Color is invalid");
        }
    }

    public class HexColorStringWrapper : DependencyObject
    {
        public ColorModel ColorModel
        {
            get { return (ColorModel)GetValue(ColorModelProperty); }
            set { SetValue(ColorModelProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ColorModel.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ColorModelProperty =
            DependencyProperty.Register(nameof(ColorModel), typeof(ColorModel), typeof(HexColorStringWrapper), new PropertyMetadata(ColorModel.Rgba32));
    }
}
