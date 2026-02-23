using System;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using TileShop.UI.Converters;
using TileShop.UI.ViewModels;

namespace TileShop.UI.MarkupExtensions;

public class IsDisplayMode : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return new Binding(nameof(GraphicsEditorViewModel.EditMode))
        {
            Converter = AppConverters.EnumToBoolean,
            ConverterParameter = nameof(GraphicsEditMode.Display),
            Mode = BindingMode.OneWay
        };
    }
}

public class IsArrangeMode : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return new Binding(nameof(GraphicsEditorViewModel.EditMode))
        {
            Converter = AppConverters.EnumToBoolean,
            ConverterParameter = nameof(GraphicsEditMode.Arrange),
            Mode = BindingMode.OneWay
        };
    }
}

public class IsDrawMode : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return new Binding(nameof(GraphicsEditorViewModel.EditMode))
        {
            Converter = AppConverters.EnumToBoolean,
            ConverterParameter = nameof(GraphicsEditMode.Draw),
            Mode = BindingMode.OneWay
        };
    }
}
