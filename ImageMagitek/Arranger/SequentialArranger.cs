﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ImageMagitek.Project;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using System.Diagnostics.CodeAnalysis;

namespace ImageMagitek;

public sealed class SequentialArranger : Arranger
{
    /// <inheritdoc/>
    protected override ArrangerElement?[,] ElementGrid { get; set; }

    /// <inheritdoc/>
    public override string Name { get; set; }

    /// <summary>
    /// Gets the filesize of the DataSource associated with a <see cref="SequentialArranger"/>
    /// </summary>
    public long FileSize { get; private set; }

    /// <summary>
    /// Gets the current address associated with a <see cref="SequentialArranger"/>
    /// </summary>
    public BitAddress Address { get; private set; }

    /// <summary>
    /// Number of bits required to be read from file sequentially to fully display the <see cref="SequentialArranger"/>
    /// </summary>
    public long ArrangerBitSize { get; private set; }

    /// <inheritdoc/>
    public override bool ShouldBeSerialized { get; set; } = true;

    /// <summary>
    /// Codec that is assigned to each <see cref="ArrangerElement"/>
    /// </summary>
    public IGraphicsCodec ActiveCodec { get; private set; }

    /// <summary>
    /// DataSource that is assigned to each <see cref="ArrangerElement"/>
    /// </summary>
    public DataSource ActiveDataSource { get; private set; }

    /// <summary>
    /// Palette that is assigned to each <see cref="ArrangerElement"/>
    /// </summary>
    public Palette ActivePalette { get; private set; }

    /// <summary>
    /// Layout used to arrange elements when <see cref="ElementLayout"/> is <see cref="ElementLayout.Tiled"/>
    /// </summary>
    public TileLayout TileLayout { get; private set; }

    private readonly ICodecFactory _codecFactory;

    /// <summary>
    /// Constructs a new SequentialArranger
    /// </summary>
    /// <param name="arrangerWidth">Width of arranger in elements</param>
    /// <param name="arrangerHeight">Height of arranger in elements</param>
    /// <param name="dataSource"><see cref="DataSource"/> assigned to each <see cref="ArrangerElement"/></param>
    /// <param name="palette"><see cref="Palette"/> assigned to each <see cref="ArrangerElement"/></param>
    /// <param name="codecFactory">Factory responsible for creating new codecs</param>
    /// <param name="codecName">Name of codec each Element will be initialized to</param>
    public SequentialArranger(int arrangerWidth, int arrangerHeight, DataSource dataSource, Palette palette, ICodecFactory codecFactory, IGraphicsCodec codec)
    {
        Mode = ArrangerMode.Sequential;
        FileSize = dataSource.Length;
        Name = dataSource.Name;
        ActiveDataSource = dataSource;
        ActivePalette = palette;
        _codecFactory = codecFactory;

        ActiveCodec = _codecFactory.CloneCodec(codec);
        ColorType = ActiveCodec.ColorType;

        Layout = ActiveCodec.Layout switch
        {
            ImageLayout.Tiled => ElementLayout.Tiled,
            ImageLayout.Single => ElementLayout.Single,
            _ => throw new InvalidOperationException($"{nameof(SequentialArranger)}.ctor was called with an invalid {nameof(ImageLayout)}")
        };

        ElementPixelSize = new Size(ActiveCodec.Width, ActiveCodec.Height);
        TileLayout = TileLayout.Default;

        Resize(arrangerWidth, arrangerHeight);
    }

    /// <summary>
    /// Lays out ArrangerElements
    /// </summary>
    private void PerformLayout()
    {
        var address = Address;

        var patternsX = ArrangerElementSize.Width / TileLayout.Width;
        var patternsY = ArrangerElementSize.Height / TileLayout.Height;

        for (int y = 0; y < patternsY; y++)
        {
            for (int x = 0; x < patternsX; x++)
            {
                foreach (var pos in TileLayout.Pattern)
                {
                    var posX = x * TileLayout.Width + pos.X;
                    var posY = y * TileLayout.Height + pos.Y;

                    var el = new ArrangerElement(posX * ElementPixelSize.Width,
                        posY * ElementPixelSize.Height, ActiveDataSource, address, ActiveCodec);

                    if (el.Codec.Layout == ImageLayout.Tiled)
                        address += ActiveCodec.StorageSize;
                    else if (el.Codec.Layout == ImageLayout.Single)
                        address += ElementPixelSize.Width * ActiveCodec.ColorDepth / 4; // TODO: Fix sequential arranger offsets to be bit-wise
                    else
                        throw new NotSupportedException();

                    ElementGrid[posY, posX] = el;
                }
            }
        }

        Move(Address);
    }

    /// <summary>
    /// Moves the sequential arranger's top-left element to the specified address with the remaining elements following
    /// If the arranger will overflow the file, then seek only to the furthest offset
    /// </summary>
    /// <param name="absoluteAddress">Specified address to move the arranger to</param>
    /// <returns></returns>
    public BitAddress Move(BitAddress absoluteAddress)
    {
        if (Mode != ArrangerMode.Sequential)
            throw new InvalidOperationException($"{nameof(Move)}: Arranger {Name} is not in sequential mode");

        var testaddress = absoluteAddress + ArrangerBitSize; // Tests the bounds of the arranger vs the file size

        if (FileSize * 8 < ArrangerBitSize) // Arranger needs more bits than the entire file
            Address = new BitAddress(0, 0);
        else if (testaddress.Offset > FileSize * 8) // Clamp arranger to edge of viewable file
            Address = new BitAddress(FileSize * 8 - ArrangerBitSize);
        else
            Address = absoluteAddress;

        var relativeChange = Address - GetInitialSequentialFileAddress();

        for (int posY = 0; posY < ArrangerElementSize.Height; posY++)
        {
            for (int posX = 0; posX < ArrangerElementSize.Width; posX++)
            {
                var el = GetElement(posX, posY);
                if (el is ArrangerElement element)
                {
                    element = element.WithAddress(element.SourceAddress + relativeChange);
                    ElementGrid[posY, posX] = element;
                }
            }
        }

        return Address;
    }

    /// <summary>
    /// Resizes a <see cref="SequentialArranger"/> to the specified number of elements
    /// </summary>
    /// <param name="arrangerWidth">Width of Arranger in Elements</param>
    /// <param name="arrangerHeight">Height of Arranger in Elements</param>
    [MemberNotNull(nameof(ElementGrid))]
    public override void Resize(int arrangerWidth, int arrangerHeight)
    {
        if (Mode != ArrangerMode.Sequential)
            throw new InvalidOperationException($"{nameof(Resize)} property '{nameof(Mode)}' is in invalid {nameof(ArrangerMode)} ({Mode})");

        var address = Address;

        ElementPixelSize = new Size(ActiveCodec.Width, ActiveCodec.Height);

        if (ElementGrid is null || ArrangerElementSize != new Size(arrangerWidth, arrangerHeight))
        {
            ElementGrid = new ArrangerElement?[arrangerHeight, arrangerWidth];
            ArrangerElementSize = new Size(arrangerWidth, arrangerHeight);
            ArrangerBitSize = arrangerWidth * arrangerHeight * ActiveCodec.StorageSize;
        }

        PerformLayout();
    }

    /// <summary>
    /// Sets Element to an absolute position in the Arranger's ElementGrid
    /// </summary>
    /// <param name="element">Element to be placed into the ElementGrid</param>
    /// <param name="posX">x-coordinate in Element coordinates</param>
    /// <param name="posY">y-coordinate in Element coordinates</param>
    public override void SetElement(in ArrangerElement? element, int posX, int posY)
    {
        if (posX > ArrangerElementSize.Width || posY > ArrangerElementSize.Height || posX < 0 || posY < 0)
            throw new ArgumentOutOfRangeException($"{nameof(SetElement)} parameter was out of range: ({posX}, {posY})");

        if (element is ArrangerElement el)
        {
            if (el.Codec.ColorType != ColorType || el.Codec.Name != ActiveCodec.Name)
                throw new ArgumentException($"{nameof(SetElement)} parameter '{nameof(element)}' cannot be assigned to SequentialArranger '{Name}'");

            if (!ReferenceEquals(ActiveDataSource, el.Source))
                throw new ArgumentException($"{nameof(SetElement)} parameter '{nameof(element)}' cannot be assigned because its DataFile '{el.Source.Name}' does not match the SequentialArranger '{ActiveDataSource.Name}'");

            if (el.Codec.Name != ActiveCodec.Name)
                throw new ArgumentException($"{nameof(SetElement)} parameter '{nameof(element)}' cannot be assigned because its Codec '{el.Codec.Name}' does not match the SequentialArranger '{ActiveCodec.Name}'");

            if (el.Codec is IIndexedCodec codec)
            {
                if (!ReferenceEquals(ActivePalette, codec.Palette))
                    throw new ArgumentException($"{nameof(SetElement)} parameter '{nameof(element)}' cannot be assigned because its Palette '{codec.Palette.Name}' does not match the SequentialArranger '{ActivePalette.Name}'");
            }

            var codecSize = new Size(el.Codec.Width, el.Codec.Height);
            if (ElementPixelSize != codecSize)
                throw new ArgumentException($"{nameof(SetElement)} parameter '{nameof(element)}' cannot be assigned because its Codec dimensions '{codecSize}' does not match the SequentialArranger '{ElementPixelSize}'");

            ElementGrid[posY, posX] = element;
        }
        else
            ElementGrid[posY, posX] = default;
    }

    /// <summary>
    /// Changes the arranger's element layout
    /// <para>The arranger is resized if necessary</para>
    /// </summary>
    public void ChangeElementLayout(TileLayout layout)
    {
        TileLayout = layout;

        var width = ArrangerElementSize.Width - (ArrangerElementSize.Width % layout.Width);
        width = Math.Max(width, layout.Width);

        var height = ArrangerElementSize.Height - (ArrangerElementSize.Height % layout.Height);
        height = Math.Max(height, layout.Height);

        if (ArrangerElementSize.Width != width || ArrangerElementSize.Height != height)
        {
            Resize(width, height);
        }
        else
        {
            PerformLayout();
        }
    }

    /// <summary>
    /// Changes each Element's codec
    /// </summary>
    /// <param name="codec">New codec</param>
    public void ChangeCodec(IGraphicsCodec codec) => ChangeCodec(codec, ArrangerElementSize.Width, ArrangerElementSize.Height);

    /// <summary>
    /// Changes each Element's codec and resizes the Arranger accordingly
    /// </summary>
    /// <param name="codec">New codec</param>
    /// <param name="arrangerWidth">Arranger Width in Elements</param>
    /// <param name="arrangerHeight">Arranger Height in Elements</param>
    public void ChangeCodec(IGraphicsCodec codec, int arrangerWidth, int arrangerHeight)
    {
        ElementPixelSize = new Size(codec.Width, codec.Height);

        ActiveCodec = codec;
        ColorType = ActiveCodec.ColorType;

        if (codec.Layout == ImageLayout.Single)
            Layout = ElementLayout.Single;
        else if (codec.Layout == ImageLayout.Tiled)
            Layout = ElementLayout.Tiled;

        if (ArrangerElementSize.Width != arrangerWidth || ArrangerElementSize.Height != arrangerHeight)
            Resize(arrangerWidth, arrangerHeight);

        ArrangerBitSize = ArrangerElementSize.Width * ArrangerElementSize.Height * codec.StorageSize;
        PerformLayout();
    }

    /// <summary>
    /// Changes the palette of all elements to the provided palette
    /// </summary>
    /// <param name="pal">New palette</param>
    public void ChangePalette(Palette pal)
    {
        ActivePalette = pal;
        for (int posY = 0; posY < ArrangerElementSize.Height; posY++)
        {
            for (int posX = 0; posX < ArrangerElementSize.Width; posX++)
            {
                if (ElementGrid[posY, posX] is ArrangerElement { Codec: IIndexedCodec codec } el)
                {
                    codec.Palette = pal;
                }
            }
        }
    }

    /// <summary>
    /// Private method for cloning an Arranger
    /// </summary>
    /// <param name="posX">Left edge of Arranger in pixel coordinates</param>
    /// <param name="posY">Top edge of Arranger in pixel coordinates</param>
    /// <param name="width">Width of Arranger in pixels</param>
    /// <param name="height">Height of Arranger in pixels</param>
    /// <returns></returns>
    protected override Arranger CloneArrangerCore(int posX, int posY, int width, int height)
    {
        var elemX = posX / ElementPixelSize.Width;
        var elemY = posY / ElementPixelSize.Height;
        var elemsWidth = (width + ElementPixelSize.Width - 1) / ElementPixelSize.Width;
        var elemsHeight = (height + ElementPixelSize.Height - 1) / ElementPixelSize.Height;

        var arranger = new ScatteredArranger(Name, ColorType, Layout, elemsWidth, elemsHeight, ElementPixelSize.Width, ElementPixelSize.Height);

        for (int y = 0; y < elemsHeight; y++)
        {
            for (int x = 0; x < elemsWidth; x++)
            {
                var elX = x * ElementPixelSize.Width;
                var elY = y * ElementPixelSize.Height;
                if (ElementGrid[y + elemY, x + elemX] is ArrangerElement el)
                {
                    var codec = _codecFactory.CloneCodec(el.Codec);

                    el = el.WithCodec(codec, elX, elY);
                    arranger.SetElement(el, x, y);
                }
            }
        }

        return arranger;
    }

    /// <summary>
    /// Gets the file address from the first element in the sequential arranger
    /// </summary>
    /// <returns></returns>
    private BitAddress GetInitialSequentialFileAddress()
    {
        if (ElementGrid is null)
            throw new NullReferenceException($"{nameof(GetInitialSequentialFileAddress)} property '{nameof(ElementGrid)}' was null");

        if (Mode != ArrangerMode.Sequential)
            throw new InvalidOperationException($"{nameof(GetInitialSequentialFileAddress)} property '{nameof(Mode)}' " +
                $"is in invalid {nameof(ArrangerMode)} ({Mode})");

        if (TileLayout is null)
        {
            return ElementGrid[0, 0]?.SourceAddress ?? BitAddress.Zero;
        }
        else
        {
            var layoutElement = TileLayout.Pattern.First();
            return ElementGrid[layoutElement.X, layoutElement.Y]?.SourceAddress ?? BitAddress.Zero;
        }
    }

    /// <inheritdoc/>
    public override IEnumerable<IProjectResource> LinkedResources
    {
        get
        {
            var set = new HashSet<IProjectResource>();

            foreach (var el in EnumerateElements().OfType<ArrangerElement>())
            {
                if (el.Codec is IIndexedCodec codec && codec.Palette is not null)
                    set.Add(codec.Palette);

                if (el.Source is not null)
                    set.Add(el.Source);
            }

            return set;
        }
    }
}
