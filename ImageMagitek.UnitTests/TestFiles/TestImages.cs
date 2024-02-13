using System;
using System.IO;
using System.Linq;

namespace ImageMagitek.UnitTests;
public static class TestImages
{
    private static string _projectFileName = "ImageMagitek.UnitTests.csproj";
    private static Lazy<string> TestRootLazy { get; } = new Lazy<string>(LocateRoot);
    public static string TestRoot => TestRootLazy.Value;

    public static string Pattern1bpp = Locate(@"1bpp/pattern_1bpp.png");
    public static string Bubbles = Locate(@"2bpp/bubbles_font_2bpp.png");
    public static string Lightning = Locate(@"2bpp/lightning_2bpp.png");
    public static string NetTrap = Locate(@"2bpp/net_trap_2bpp.png");
    public static string Teleportation = Locate(@"2bpp/teleportation_2bpp.png");
    public static string EnsorcelledHiberation = Locate(@"3bpp/ensorcelled_hibernation_3bpp.png");
    public static string TabSelected = Locate(@"3bpp/tab_selected_3bpp.png");

    public static string DungeonEntrance = Locate(@"4bpp/dngn_enter_gehenna_4bpp.png");
    public static string Fireball = Locate(@"4bpp/fireball_4bpp.png");
    public static string Ice = Locate(@"4bpp/ice_4bpp.png");

    public static string Alienships = Locate(@"8bpp/alienships_preview_8bpp.png");
    public static string CloudMutagenic = Locate(@"8bpp/cloud_mutagenic_large2_8bpp.png");
    public static string CyanPotion = Locate(@"8bpp/cyan_potion_8bpp.png");
    public static string DungeonShop = Locate(@"8bpp/dngn_abandoned_shop_8bpp.png");
    public static string DragonForm = Locate(@"8bpp/dragon_from_8bpp.png");
    public static string Torment = Locate(@"8bpp/symbol_of_torment_8bpp.png");

    private static string Locate(string relative) => Path.Combine(TestRoot, "TestFiles", relative);

    private static string LocateRoot()
    {
        var start = new DirectoryInfo(Directory.GetCurrentDirectory());
        var visitor = start;

        while (!visitor.EnumerateFiles(_projectFileName).Any())
        {
            visitor = visitor.Parent;

            if (visitor == null)
            {
                throw new DirectoryNotFoundException($"Could not locate '{_projectFileName}' from '{start}'");
            }
        }

        return visitor.FullName;
    }
}
