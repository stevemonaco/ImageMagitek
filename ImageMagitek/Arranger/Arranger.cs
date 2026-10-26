using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CommunityToolkit.Diagnostics;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Project;

namespace ImageMagitek;

/// <summary>
/// Mode for the Arranger
/// <para>SequentialArrangers are used for simple sequential file access</para>
/// <para>ScatteredArrangers are capable of accessing many files, file offsets, palettes, and codecs in a single arranger</para>
/// <para>TilemapArrangers derive their elements from the entries of a tilemap</para>
/// </summary>
public enum ArrangerMode { Sequential, Scattered };

/// <summary>
/// Layout of graphics for the arranger
/// <para>Each layout directs Arranger element selection and Arranger cloning to perform differently</para>
/// <para>Tiled will snap selection rectangles to tile boundaries</para>
/// <para>Single will snap selection rectangles to pixel boundaries</para>
/// </summary>
public enum ElementLayout { Tiled, Single };

/// <summary>
/// Specifies how the pixels' colors are determined for the graphic
/// <para>Indexed graphics have their full color determined by a palette</para>
/// <para>Direct graphics have their full color determined by the pixel image data alone</para>
/// </summary>
public enum PixelColorType { Indexed, Direct }

/// <summary>
/// Move operations for sequential arrangers
/// </summary>
public enum ArrangerMoveType { ByteDown, ByteUp, RowDown, RowUp, ColRight, ColLeft, PageDown, PageUp, Home, End, Absolute };

/// <summary>
/// Arranger base class for graphical screen elements
/// </summary>
public abstract class Arranger : IProjectResource, IArrangerView, IUnavailableResource
{
    /// <summary>
    /// Individual Elements that compose the Arranger in [y, x] ordering
    /// </summary>
    protected abstract ArrangerElement?[,] ElementGrid { get; set; }
    public abstract string Name { get; set; }

    /// <summary>
    /// Gets the size of the entire Arranger in Element coordinates
    /// </summary>
    public Size ArrangerElementSize { get; protected set; }

    /// <summary>
    /// Gets the Size of the entire Arranger in pixel coordinates
    /// </summary>
    public Size ArrangerPixelSize =>
        new(ArrangerElementSize.Width * ElementPixelSize.Width, ArrangerElementSize.Height * ElementPixelSize.Height);

    /// <summary>
    /// Gets the size of an individual Element in pixels
    /// </summary>
    public Size ElementPixelSize { get; protected set; }

    /// <summary>
    /// Gets the Mode of the Arranger
    /// </summary>
    public ArrangerMode Mode { get; init; }

    /// <summary>
    /// Gets the ArrangerLayout of the Arranger
    /// </summary>
    public ElementLayout Layout { get; protected set; }

    /// <summary>
    /// Gets the ColorType of the Arranger
    /// </summary>
    public PixelColorType ColorType { get; protected set; }

    public bool CanContainChildResources => false;

    public abstract bool ShouldBeSerialized { get; set; }

    FileDataSource? IUnavailableResource.FindMissingDataSource() => ArrangerExtensions.FindMissingDataSource(this);

    public abstract void Resize(int arrangerWidth, int arrangerHeight);

    /// <summary>
    /// Clones the Arranger
    /// </summary>
    /// <returns></returns>
    public virtual Arranger CloneArranger()
    {
        if (Layout is ElementLayout.Tiled or ElementLayout.Single)
            return CloneArranger(0, 0, ArrangerPixelSize.Width, ArrangerPixelSize.Height);
        else
            throw new NotSupportedException($"{nameof(CloneArranger)} with {nameof(ElementLayout)} '{Layout}' is not supported");
    }

    /// <summary>
    /// Clones a subsection of the Arranger
    /// </summary>
    /// <param name="pixelX">Left edge of Arranger in pixel coordinates</param>
    /// <param name="pixelY">Top edge of Arranger in pixel coordinates</param>
    /// <param name="width">Width of Arranger in pixels</param>
    /// <param name="height">Height of Arranger in pixels</param>
    /// <returns></returns>
    public virtual Arranger CloneArranger(int pixelX, int pixelY, int width, int height)
    {
        if (pixelX < 0 || pixelX + width > ArrangerPixelSize.Width || pixelY < 0 || pixelY + height > ArrangerPixelSize.Height)
            throw new ArgumentOutOfRangeException($"{nameof(CloneArranger)} parameters ({nameof(pixelX)}: {pixelX}, {nameof(pixelY)}: {pixelY}, {nameof(width)}: {width}, {nameof(height)}: {height})" +
                $" were outside of the bounds of arranger '{Name}' of size (width: {ArrangerPixelSize.Width}, height: {ArrangerPixelSize.Height})");

        if (Layout == ElementLayout.Single)
        {
            if (pixelX != 0 || pixelY != 0 || width != ArrangerPixelSize.Width || height != ArrangerPixelSize.Height)
                throw new InvalidOperationException($"{nameof(CloneArranger)} of an Arranger with ArrangerLayout of Single must have the same dimensions as the original");
            return CloneArrangerCore(pixelX, pixelY, width, height);
        }

        return CloneArrangerCore(pixelX, pixelY, width, height);
    }

    protected abstract Arranger CloneArrangerCore(int posX, int posY, int width, int height);

    /// <summary>
    /// Sets Element to a position in the Arranger ElementGrid
    /// </summary>
    /// <param name="element">Element to be placed into the ElementGrid</param>
    /// <param name="elemX">x-coordinate in Element coordinates</param>
    /// <param name="elemY">y-coordinate in Element coordinates</param>
    public virtual void SetElement(in ArrangerElement? element, int elemX, int elemY)
    {
        Guard.IsInRange(elemX, 0, ArrangerElementSize.Width+1);
        Guard.IsInRange(elemY, 0, ArrangerElementSize.Height+1);

        if (element is not null)
        {
            if (element.Value.Codec.ColorType != ColorType)
                throw new ArgumentException($"{nameof(SetElement)} parameter '{nameof(element)}' did not match the Arranger's {nameof(PixelColorType)}");

            var relocatedElement = element.Value.WithLocation(elemX * ElementPixelSize.Width, elemY * ElementPixelSize.Height);
            ElementGrid[elemY, elemX] = relocatedElement;
        }
        else
            ElementGrid[elemY, elemX] = null;
    }

    /// <summary>
    /// Resets the Element to the undefined state at the given position in the Arranger ElementGrid
    /// </summary>
    /// <param name="elemX">Element position in element coordinates</param>
    /// <param name="elemY">Element position in element coordinates</param>
    public virtual void ResetElement(int elemX, int elemY) => ElementGrid[elemY, elemX] = null;

    /// <summary>
    /// Gets an Element from a position in the Arranger ElementGrid in Element coordinates
    /// </summary>
    /// <param name="elemX">x-coordinate in Element coordinates</param>
    /// <param name="elemY">y-coordinate in Element coordinates</param>
    public ArrangerElement? GetElement(int elemX, int elemY) => ElementGrid[elemY, elemX];

    /// <summary>
    /// A DropElements edge per distinct element source and a FallbackPalette edge per distinct indexed codec palette.
    /// </summary>
    public virtual IEnumerable<ResourceEdge> Dependencies
    {
        get
        {
            var edges = new HashSet<ResourceEdge>();

            foreach (var el in this.EnumerateElements().OfType<ArrangerElement>())
            {
                if (el.Source is not null)
                    edges.Add(new(el.Source, ResourceEdgeKind.DropElements));

                if (el.Codec is IIndexedCodec codec && codec.Palette is not null)
                    edges.Add(new(codec.Palette, ResourceEdgeKind.FallbackPalette));
            }

            return edges;
        }
    }
}
