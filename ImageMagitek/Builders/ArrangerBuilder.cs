using System.Drawing;
using ImageMagitek.Codec;
using ImageMagitek.Colors;

namespace ImageMagitek.Builders;

public interface IArrangerBuilderArrangerElementSize
{
    IArrangerBuilderElementPixelSize WithArrangerElementSize(int x, int y);
}

public interface IArrangerBuilderElementPixelSize
{
    IArrangerBuilderPixelColorType WithElementPixelSize(int x, int y);
}

public interface IArrangerBuilderPixelColorType
{
    IArrangerBuilderName WithPixelColorType(PixelColorType pixelColorType);
}

public interface IArrangerBuilderName
{
    IArrangerBuilderType WithName(string name);
}

public interface IArrangerBuilderType
{
    ISequentialArrangerBuilderSources AsSequentialArranger(ICodecFactory codecFactory);
    IScatteredArrangerBuilder AsScatteredArranger();
}

public interface ISequentialArrangerBuilderSources
{
    ISequentialArrangerBuilderSources WithDataFile(DataFile df);
    ISequentialArrangerBuilderSources WithCodecName(string codecName);
    ISequentialArrangerBuilderSources WithPalette(Palette palette);
    ISequentialArrangerBuilderSources WithElementLayout(ElementLayout layout);
    SequentialArranger Build();
}

public interface IScatteredArrangerBuilder
{
    ScatteredArranger Build();
}

public class ArrangerBuilder :
    IArrangerBuilderArrangerElementSize,
    IArrangerBuilderElementPixelSize,
    IArrangerBuilderPixelColorType,
    IArrangerBuilderName,
    IArrangerBuilderType,
    ISequentialArrangerBuilderSources,
    IScatteredArrangerBuilder
{
    private ArrangerMode _mode;
    private ICodecFactory _codecFactory;
    private PixelColorType _pixelColorType;
    private ElementLayout _elementLayout;
    private Size _arrangerElementSize;
    private Size _elementPixelSize;
    private string _name;
    private DataFile _dataFile;
    private string _codecName;
    private Palette _palette;

    private ArrangerBuilder()
    {
    }

    public static IArrangerBuilderElementPixelSize WithSingleLayout()
    {
        var builder = new ArrangerBuilder();
        builder._elementLayout = ElementLayout.Single;
        builder._arrangerElementSize = new(1, 1);
        return builder;
    }

    public static IArrangerBuilderArrangerElementSize WithTiledLayout()
    {
        var builder = new ArrangerBuilder();
        builder._elementLayout = ElementLayout.Tiled;
        return builder;
    }

    public IArrangerBuilderElementPixelSize WithArrangerElementSize(int x, int y)
    {
        _arrangerElementSize = new(x, y);
        return this;
    }

    public IArrangerBuilderPixelColorType WithElementPixelSize(int x, int y)
    {
        _elementPixelSize = new(x, y);
        return this;
    }

    public IArrangerBuilderName WithPixelColorType(PixelColorType pixelColorType)
    {
        _pixelColorType = pixelColorType;
        return this;
    }

    public IArrangerBuilderType WithName(string name)
    {
        _name = name;
        return this;
    }

    public ISequentialArrangerBuilderSources AsSequentialArranger(ICodecFactory codecFactory)
    {
        _mode = ArrangerMode.Sequential;
        _codecFactory = codecFactory;

        return this;
    }

    public IScatteredArrangerBuilder AsScatteredArranger()
    {
        _mode = ArrangerMode.Scattered;

        return this;
    }

    public ISequentialArrangerBuilderSources WithDataFile(DataFile dataFile)
    {
        _dataFile = dataFile;
        return this;
    }

    public ISequentialArrangerBuilderSources WithCodecName(string codecName)
    {
        _codecName = codecName;
        return this;
    }

    public ISequentialArrangerBuilderSources WithPalette(Palette palette)
    {
        _palette = palette;
        return this;
    }

    public ISequentialArrangerBuilderSources WithElementLayout(ElementLayout elementLayout)
    {
        _elementLayout = elementLayout;
        return this;
    }

    SequentialArranger ISequentialArrangerBuilderSources.Build()
    {
        var arranger = new SequentialArranger(_arrangerElementSize.Width, _arrangerElementSize.Height, _dataFile, _palette, _codecFactory, _codecName);
        arranger.Name = _name;
        var codec = _codecFactory.GetCodec(arranger.ActiveCodec.Name, _elementPixelSize);
        arranger.ChangeCodec(codec);
        return arranger;
    }

    ScatteredArranger IScatteredArrangerBuilder.Build()
    {
        return new ScatteredArranger(_name, _pixelColorType, _elementLayout, _arrangerElementSize.Width, _arrangerElementSize.Height, _elementPixelSize.Width, _elementPixelSize.Height);
    }
}
