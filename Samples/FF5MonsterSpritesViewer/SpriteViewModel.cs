using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using FF5MonsterSprites.Imaging;
using FF5MonsterSprites.Models;
using FF5MonsterSprites.Serialization;
using ImageMagitek;

namespace FF5MonsterSprites;

public partial class SpriteViewModel : ObservableObject
{
    private readonly string _romPath;

    public int MonsterId { get; }
    public MonsterMetadata Metadata { get; }

    [ObservableProperty] private WriteableBitmap? _bitmap;
    [ObservableProperty] private string? _loadError;

    public SpriteViewModel(int monsterId, MonsterMetadata metadata, string romPath)
    {
        MonsterId = monsterId;
        Metadata = metadata;
        _romPath = romPath;
    }

    /// <summary>
    /// Opens the ROM and renders this monster's sprite. Failures are reported through <see cref="LoadError"/>.
    /// </summary>
    public async Task LoadAsync()
    {
        if (Bitmap is not null)
            return;

        LoadError = null;
        try
        {
            var context = await new MonsterSerializer().DeserializeSprite(_romPath, Metadata);
            // ToBitmap reads the lazily loaded palette, so it must run before the data file closes
            using (context.DataFile)
            {
                var image = new IndexedImage(context.Arranger);
                image.Render();
                Bitmap = IndexedBitmapAdapter.ToBitmap(image);
            }
        }
        catch (Exception ex)
        {
            LoadError = $"Could not load sprite: {ex.Message}";
        }
    }
}
