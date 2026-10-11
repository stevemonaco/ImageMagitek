using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ImageMagitek;
using ImageMagitek.Colors;
using TileShop.Shared.Models;

namespace TileShop.UI.Features.Palettes;

/// <summary>
/// Pending edits to one palette. Edits apply in place to the shared <see cref="Palette"/> so every view previews them,
/// and are written to the data source only by <see cref="Commit"/>.
/// </summary>
public sealed class PaletteEditSession
{
    private readonly Palette _palette;
    private readonly IColorFactory _colorFactory;
    private readonly ObservableCollection<HistoryAction> _undo;
    private readonly ObservableCollection<HistoryAction> _redo;
    private PaletteSnapshot _saved;

    public event EventHandler? Changed;

    public bool IsModified => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>
    /// State last written by <see cref="Commit"/>, or the palette's state when the session started
    /// </summary>
    public PaletteSnapshot SavedState => _saved;

    private PaletteSnapshot LatestState => _undo.Count > 0 ? ((PaletteHistoryAction)_undo[^1]).After : _saved;

    public PaletteEditSession(Palette palette, IColorFactory colorFactory, ObservableCollection<HistoryAction> undo,
        ObservableCollection<HistoryAction> redo)
    {
        _palette = palette;
        _colorFactory = colorFactory;
        _undo = undo;
        _redo = redo;
        _saved = PaletteSnapshot.Capture(palette, colorFactory);
    }

    public void SetColor(int index, IColor color) =>
        Record($"Edit color {index}", () => WriteColor(index, color));

    public void SetColors(int start, IReadOnlyList<IColor> colors)
    {
        int count = Math.Min(colors.Count, _palette.Entries - start);
        if (start < 0 || count <= 0)
            return;

        Record("Paste colors", () =>
        {
            for (int i = 0; i < count; i++)
                WriteColor(start + i, colors[i]);
        });
    }

    public void SwapColors(int a, int b)
    {
        if (a == b)
            return;

        var colorA = _colorFactory.CloneColor(_palette.GetForeignColor(a));
        var colorB = _colorFactory.CloneColor(_palette.GetForeignColor(b));

        Record($"Swap colors {a} and {b}", () =>
        {
            WriteColor(a, colorB);
            WriteColor(b, colorA);
        });
    }

    /// <summary>
    /// Interpolates the colors between the first and last of <paramref name="sortedIndices"/>, weighted by palette index
    /// </summary>
    public void FillGradient(IReadOnlyList<int> sortedIndices)
    {
        if (sortedIndices.Count < 3)
            return;

        int first = sortedIndices[0];
        int last = sortedIndices[^1];
        var start = _colorFactory.ToNative(_palette.GetForeignColor(first));
        var end = _colorFactory.ToNative(_palette.GetForeignColor(last));

        Record("Fill gradient", () =>
        {
            for (int i = 1; i < sortedIndices.Count - 1; i++)
            {
                int index = sortedIndices[i];
                double t = (double)(index - first) / (last - first);
                var native = new ColorRgba32(Lerp(start.R, end.R, t), Lerp(start.G, end.G, t), Lerp(start.B, end.B, t), Lerp(start.A, end.A, t));
                WriteColor(index, _colorFactory.ToForeign(native, _palette.ColorModel));
            }
        });
    }

    /// <summary>
    /// Replaces the sources. Pending colors carry over to file entries whose offset and endian are unchanged
    /// and to scattered entries whose fields are unchanged.
    /// Sources equal to the current ones are ignored so re-committed text fields don't add history.
    /// </summary>
    /// <returns>False if a file or scattered source extends past the end of the data source</returns>
    public bool SetSources(IReadOnlyList<IColorSource> sources, string name = "Edit sources")
    {
        if (!FitsDataSource(sources))
            return false;

        if (SourcesEqual(_palette.ColorSources, sources))
            return true;

        var pending = new List<(IColorSource Source, IColor Color)>();
        for (int i = 0; i < _palette.Entries; i++)
        {
            if (Palette.IsFileBacked(_palette.ColorSources[i]))
                pending.Add((_palette.ColorSources[i], _colorFactory.CloneColor(_palette.GetForeignColor(i))));
        }

        Record(name, () =>
        {
            _palette.SetColorSources(sources);

            for (int i = 0; i < sources.Count; i++)
            {
                if (!Palette.IsFileBacked(sources[i]))
                    continue;

                var match = pending.FindIndex(x => SameSource(x.Source, sources[i]));
                if (match >= 0)
                    _palette.SetForeignColor(i, _colorFactory.CloneColor(pending[match].Color));
            }
        });
        return true;
    }

    public void SetZeroIndexTransparent(bool value)
    {
        if (_palette.ZeroIndexTransparent == value)
            return;

        Record(value ? "Make index 0 transparent" : "Make index 0 opaque", () => _palette.ZeroIndexTransparent = value);
    }

    /// <summary>
    /// Reinterprets the palette under <paramref name="model"/>. Pending file colors are re-read since they cannot be carried across models.
    /// </summary>
    public void ChangeColorModel(ColorModel model, IReadOnlyList<IColorSource> sources) =>
        Record($"Change color model to {model}", () => _palette.SetColorModel(model, sources));

    public void Undo()
    {
        if (_undo.Count == 0)
            return;

        var undone = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(undone);

        LatestState.Restore(_palette, _colorFactory);
        OnChanged();
    }

    public void Redo()
    {
        if (!CanRedo)
            return;

        var action = (PaletteHistoryAction)_redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(action);

        action.After.Restore(_palette, _colorFactory);
        OnChanged();
    }

    /// <summary>
    /// Writes the working colors to the data source and project sources, then makes them the saved state
    /// </summary>
    /// <returns>False if the palette's storage cannot be written</returns>
    public bool Commit()
    {
        if (!_palette.SavePalette())
            return false;

        _saved = PaletteSnapshot.Capture(_palette, _colorFactory);
        ClearHistory();
        return true;
    }

    public void Discard()
    {
        _saved.Restore(_palette, _colorFactory);
        ClearHistory();
    }

    private void Record(string name, Action edit)
    {
        PaletteSnapshot after;
        try
        {
            edit();
            after = PaletteSnapshot.Capture(_palette, _colorFactory);
        }
        catch
        {
            LatestState.Restore(_palette, _colorFactory);
            throw;
        }

        _undo.Add(new PaletteHistoryAction(name, after));
        _redo.Clear();
        OnChanged();
    }

    private void ClearHistory()
    {
        _undo.Clear();
        _redo.Clear();
        OnChanged();
    }

    /// <summary>
    /// Assigns a working color and keeps project sources in step, so they always hold the working state
    /// </summary>
    private void WriteColor(int index, IColor color)
    {
        switch (_palette.ColorSources[index])
        {
            case ProjectNativeColorSource native:
                native.Value = _colorFactory.ToNative(color);
                _palette.SetForeignColor(index, native.Value);
                break;
            case ProjectForeignColorSource foreign:
                foreign.Value = ConformToModel(color);
                _palette.SetForeignColor(index, _colorFactory.CloneColor(foreign.Value));
                break;
            default:
                _palette.SetForeignColor(index, ConformToModel(color));
                break;
        }
    }

    /// <summary>
    /// Converts colors of another model, such as the Rgba32 colors of native entries, so file writes use the palette's color size
    /// </summary>
    private IColor ConformToModel(IColor color)
    {
        var model = _palette.ColorModel;
        return color.GetType() == _colorFactory.CreateColor(model).GetType()
            ? _colorFactory.CloneColor(color)
            : _colorFactory.ToForeign(_colorFactory.ToNative(color), model);
    }

    private bool FitsDataSource(IReadOnlyList<IColorSource> sources)
    {
        if (!sources.Any(Palette.IsFileBacked))
            return true;

        if (_palette.DataSource is not { } dataSource)
            return false;

        var size = _colorFactory.CreateColor(_palette.ColorModel).Size;
        return sources.All(x => x switch
        {
            FileColorSource file => file.Offset + size / 8 <= dataSource.Length,
            ScatteredColorSource scattered => scattered.Fields.All(f => f.Offset < dataSource.Length),
            _ => true
        });
    }

    private static bool SourcesEqual(IReadOnlyList<IColorSource> a, IReadOnlyList<IColorSource> b) =>
        a.Count == b.Count && a.Zip(b).All(x => SameSource(x.First, x.Second));

    private static bool SameSource(IColorSource a, IColorSource b) => (a, b) switch
    {
        (FileColorSource x, FileColorSource y) => x.Offset == y.Offset && x.Endian == y.Endian,
        (ScatteredColorSource x, ScatteredColorSource y) => x.Fields.SequenceEqual(y.Fields),
        (ProjectNativeColorSource x, ProjectNativeColorSource y) => x.Value.Color == y.Value.Color,
        (ProjectForeignColorSource x, ProjectForeignColorSource y) => x.Value.GetType() == y.Value.GetType() && x.Value.Color == y.Value.Color,
        _ => false
    };

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private static byte Lerp(byte a, byte b, double t) => (byte)Math.Round(a + (b - a) * t);
}
