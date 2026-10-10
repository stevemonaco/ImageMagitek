using CommunityToolkit.Mvvm.ComponentModel;
using FF5MonsterSprites.Serialization;

namespace FF5MonsterSprites;

public partial class ShellViewModel : ObservableObject
{
    [ObservableProperty] private IReadOnlyList<SpriteViewModel>? _sprites;
    [ObservableProperty] private SpriteViewModel? _selectedSprite;
    [ObservableProperty] private string? _errorMessage;

    /// <summary>
    /// Loads the monster list from ff5.sfc in the application directory, or from the ROM chosen by <paramref name="pickRom"/> when it is missing.
    /// </summary>
    public async Task LoadMonstersAsync(Func<Task<string?>> pickRom)
    {
        var defaultPath = Path.Combine(AppContext.BaseDirectory, "ff5.sfc");
        var romPath = File.Exists(defaultPath) ? defaultPath : await pickRom();

        if (romPath is null)
        {
            ErrorMessage = $"No Final Fantasy V ROM selected. Place it at '{defaultPath}' and restart.";
            return;
        }

        try
        {
            var monsters = await new MonsterSerializer().DeserializeMonsters(romPath);
            Sprites = monsters.Select((x, i) => new SpriteViewModel(i, x, romPath)).ToList();
            SelectedSprite = Sprites.FirstOrDefault();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not read '{romPath}': {ex.Message}";
        }
    }

    partial void OnSelectedSpriteChanged(SpriteViewModel? value)
    {
        _ = value?.LoadAsync();
    }
}
