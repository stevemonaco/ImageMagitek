using System.Linq;
using FF5MonsterSprites.Serialization;
using Stylet;

namespace FF5MonsterSprites;

internal class ShellViewModel : Conductor<SpriteViewModel>
{
    private BindableCollection<SpriteViewModel>? _sprites;
    public BindableCollection<SpriteViewModel>? Sprites
    {
        get => _sprites;
        set => SetAndNotify(ref _sprites, value);
    }

    public ShellViewModel()
    {
    }

    protected override async void OnInitialActivate()
    {
        var monsterSerializer = new MonsterSerializer();
        var monsters = await monsterSerializer.DeserializeMonsters("ff5.sfc");

        var sprites = monsters.Select((x, i) => new SpriteViewModel(x)
        {
            MonsterId = i,
        });

        Sprites = new BindableCollection<SpriteViewModel>(sprites);
        ActiveItem = Sprites.First();

        base.OnInitialActivate();
    }
}