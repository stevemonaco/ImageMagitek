using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FF5MonsterSprites.Imaging;
using FF5MonsterSprites.Models;
using FF5MonsterSprites.Serialization;
using ImageMagitek;
using Stylet;

namespace FF5MonsterSprites;

public class SpriteViewModel : Screen
{
    private int _monsterID;
    public int MonsterID
    {
        get => _monsterID;
        set => SetAndNotify(ref _monsterID, value);
    }

    private TileColorDepth _colorDepth;
    public TileColorDepth ColorDepth
    {
        get => _colorDepth;
        set => SetAndNotify(ref _colorDepth, value);
    }

    private int _tileSetID;
    public int TileSetID
    {
        get => _tileSetID;
        set => SetAndNotify(ref _tileSetID, value);
    }

    private TileSetSize _tileSetSize;
    public TileSetSize TileSetSize
    {
        get => _tileSetSize;
        set => SetAndNotify(ref _tileSetSize, value);
    }

    private bool _hasShadow;
    public bool HasShadow
    {
        get => _hasShadow;
        set => SetAndNotify(ref _hasShadow, value);
    }

    private int _paletteID;
    public int PaletteID
    {
        get => _paletteID;
        set => SetAndNotify(ref _paletteID, value);
    }

    private int _formID;
    public int FormID
    {
        get => _formID;
        set => SetAndNotify(ref _formID, value);
    }

    private int _unused;
    public int Unused
    {
        get => _unused;
        set => SetAndNotify(ref _unused, value);
    }

    private IndexedBitmapAdapter? _adapter;

    public IndexedBitmapAdapter? Adapter
    {
        get => _adapter;
        set => SetAndNotify(ref _adapter, value);
    }

    private readonly MonsterMetadata _metadata;
    private SpriteResourceContext _context;

    public SpriteViewModel(MonsterMetadata metadata)
    {
        _metadata = metadata;

        ColorDepth = metadata.ColorDepth;
        TileSetID = metadata.TileSetID;
        TileSetSize = metadata.TileSetSize;
        HasShadow = metadata.hasShadow;
        PaletteID = metadata.PaletteID;
        FormID = metadata.FormID;
        Unused = metadata.Unused;
    }

    protected override async void OnInitialActivate()
    {
        await LoadSprite();
        base.OnActivate();
    }

    public async Task LoadSprite()
    {
        var serializer = new MonsterSerializer();

        _context = await serializer.DeserializeSprite(@"D:\Emulation\ff5.sfc", _metadata);
        var image = new IndexedImage(_context.Arranger);
        image.Render();
        Adapter = new IndexedBitmapAdapter(image);
        Adapter.Invalidate();
    }

    protected override void OnClose()
    {
        _context.DataFile.Stream.Close();
        base.OnClose();
    }
}
