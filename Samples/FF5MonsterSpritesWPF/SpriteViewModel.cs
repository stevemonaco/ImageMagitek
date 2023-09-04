using System.Threading.Tasks;
using FF5MonsterSprites.Imaging;
using FF5MonsterSprites.Models;
using FF5MonsterSprites.Serialization;
using ImageMagitek;
using Stylet;

namespace FF5MonsterSprites;

public class SpriteViewModel : Screen
{
    private int _monsterId;
    public int MonsterId
    {
        get => _monsterId;
        set => SetAndNotify(ref _monsterId, value);
    }

    private TileColorDepth _colorDepth;
    public TileColorDepth ColorDepth
    {
        get => _colorDepth;
        set => SetAndNotify(ref _colorDepth, value);
    }

    private int _tileSetId;
    public int TileSetId
    {
        get => _tileSetId;
        set => SetAndNotify(ref _tileSetId, value);
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

    private int _paletteId;
    public int PaletteId
    {
        get => _paletteId;
        set => SetAndNotify(ref _paletteId, value);
    }

    private int _formId;
    public int FormId
    {
        get => _formId;
        set => SetAndNotify(ref _formId, value);
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
    private SpriteResourceContext? _context;

    public SpriteViewModel(MonsterMetadata metadata)
    {
        _metadata = metadata;

        ColorDepth = metadata.ColorDepth;
        TileSetId = metadata.TileSetId;
        TileSetSize = metadata.TileSetSize;
        HasShadow = metadata.HasShadow;
        PaletteId = metadata.PaletteId;
        FormId = metadata.FormId;
        Unused = metadata.Unused;
    }

    protected override async void OnInitialActivate()
    {
        await LoadSprite();
        base.OnActivate();
    }

    private async Task LoadSprite()
    {
        var serializer = new MonsterSerializer();

        _context = await serializer.DeserializeSprite("ff5.sfc", _metadata);
        var image = new IndexedImage(_context.Arranger);
        image.Render();
        Adapter = new IndexedBitmapAdapter(image);
        Adapter.Invalidate();
    }

    protected override void OnClose()
    {
        _context?.DataFile.Dispose();
        base.OnClose();
    }
}
