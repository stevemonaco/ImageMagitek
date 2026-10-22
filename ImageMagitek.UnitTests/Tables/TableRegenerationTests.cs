using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagitek.Project.Serialization;
using ImageMagitek.Tables;
using ImageMagitek.UnitTests.Tilemaps;
using Xunit;

namespace ImageMagitek.UnitTests.Tables;

public sealed class TableRegenerationTests : IDisposable
{
    private const string MapA = "/Generated/Maps/Pallet Town/Pallet Town";
    private const string MapB = "/Generated/Maps/Viridian City/Viridian City 1";
    private const string Border = "/Generated/Borders/Pallet Town/Pallet Town Border";
    private const string General = "/Generated/Tilesets/General";
    private const string PalletSet = "/Generated/Tilesets/Pallet Town";
    private const string ViridianSet = "/Generated/Tilesets/Viridian City";

    private readonly TableTestContext _context = new();

    public TableRegenerationTests()
    {
        _context.CopyFixtures();
    }

    public void Dispose() => _context.Dispose();

    private GenerationPlan Plan(Action<byte[]>? editRom = null, Func<string, string>? editDefinition = null)
    {
        var definitionText = File.ReadAllText(Path.Combine(TableTestContext.FixturesDirectory, "Synthetic.table.xml"));
        _context.Write("Synthetic.table.xml", editDefinition?.Invoke(definitionText) ?? definitionText);
        var result = _context.Load("Synthetic.table.xml");
        Assert.True(result.HasSucceeded, result.HasFailed ? string.Join("\n", result.AsError.Reasons) : null);

        var rom = FireRedShapedData.Create();
        editRom?.Invoke(rom);
        var source = _context.Files.Open("Rom", rom);
        return new TableGenerator(result.AsSuccess.Result, source, "/Rom", "/Generated", MapFormatTests.LoadShippedFormats())
            .Plan(Path.Combine(_context.Directory, "project.xml"));
    }

    private static void Repoint(byte[] rom) => FireRedShapedData.RepointLayoutBlocks(rom, FireRedShapedData.LayoutA, FireRedShapedData.FreeSpace);

    // Entries as a generation of the plan would leave them, with each resource at its key and unedited
    private static List<RegenerationEntry> EntriesOf(GenerationPlan plan) =>
        plan.Resources
            .Zip(plan.ManifestGeneration.Resources)
            .Select(x => new RegenerationEntry(x.Second, x.First.Key, x.First.Model, false))
            .ToList();

    private static RegenerationEntry ExtraPalette(string key, long offset) =>
        new(new TableManifestResource(key, "palette", "tilesetPalettes", "palette", offset, 16, 0, "mapGroups[9][0]"), key,
            new PaletteModel { Name = RegenerationPlan.NameOf(key), DataFileKey = "/Rom" }, false);

    private static RegeneratedResource Find(RegenerationPlan regeneration, string plannedKey) =>
        Assert.Single(regeneration.Resources, x => x.Planned.Key == plannedKey);

    private static List<RegenerationEntry> Replace(List<RegenerationEntry> entries, string key, Func<RegenerationEntry, RegenerationEntry> replace) =>
        entries.Select(x => x.Entry.Key == key ? replace(x) : x).ToList();

    [Fact]
    public void MatchByPath_RepointedRecordIsChanged()
    {
        var entries = EntriesOf(Plan());

        var regeneration = RegenerationPlan.Match(Plan(Repoint), entries, false);

        var map = Find(regeneration, MapA);
        Assert.Equal(RegenerationKind.Changed, map.Kind);
        Assert.Same(entries.Single(x => x.Entry.Key == MapA), map.Entry);
        Assert.Equal(["repointed"], map.ChangeKinds);
        Assert.Equal(FireRedShapedData.FreeSpace, ((MetatileMapModel)map.Model).MapOffset);
        Assert.All(regeneration.Resources.Where(x => x != map), x => Assert.Equal(RegenerationKind.Unchanged, x.Kind));
        Assert.Empty(regeneration.Removed);
        Assert.Empty(regeneration.Kept);
    }

    [Fact]
    public void EntryWithoutPath_MatchesByOffset()
    {
        var plan = Plan();
        var entries = EntriesOf(plan).Select(x => x with { Entry = x.Entry with { Path = null } }).ToList();
        entries = Replace(entries, Border, x => x with { Entry = x.Entry with { Path = "mapGroups[1][1].layout" } });

        var regeneration = RegenerationPlan.Match(plan, entries, false);

        Assert.All(regeneration.Resources.Where(x => x.Planned.Key != Border), x =>
        {
            Assert.Equal(RegenerationKind.Unchanged, x.Kind);
            Assert.Equal(x.Planned.Key, x.Entry!.Entry.Key);
        });
        Assert.Equal(RegenerationKind.Added, Find(regeneration, Border).Kind);
        Assert.Equal(Border, Assert.Single(regeneration.Removed).CurrentKey);
    }

    [Fact]
    public void AddedRemovedChangedUnchanged()
    {
        var entries = EntriesOf(Plan()).Where(x => x.Entry.Key != MapB).Append(ExtraPalette("/Generated/Gone", 0x7F00)).ToList();

        var regeneration = RegenerationPlan.Match(Plan(Repoint), entries, false);

        Assert.Equal(RegenerationKind.Added, Find(regeneration, MapB).Kind);
        Assert.Null(Find(regeneration, MapB).Entry);
        Assert.Equal(RegenerationKind.Changed, Find(regeneration, MapA).Kind);
        Assert.Equal("/Generated/Gone", Assert.Single(regeneration.Removed).CurrentKey);
        Assert.Equal(18, regeneration.Resources.Count(x => x.Kind == RegenerationKind.Unchanged));
        Assert.Equal([MapA, MapB], regeneration.Written.Select(x => x.TargetKey).Order());
    }

    [Fact]
    public void ReferenceToMovedResource_UsesCurrentKey()
    {
        var plan = Plan();
        var entries = Replace(EntriesOf(plan), General, x => x with { CurrentKey = "/Moved/General" });

        var regeneration = RegenerationPlan.Match(plan, entries, false);

        var set = Find(regeneration, General);
        Assert.Equal((RegenerationKind.Unchanged, "/Moved/General"), (set.Kind, set.TargetKey));
        Assert.Equal("/Moved/General", ((MetatileMapModel)Find(regeneration, MapA).Model).Sets[0].SetKey);
        Assert.Equal("/Moved/General", ((MetatileMapModel)Find(regeneration, Border).Model).Sets[0].SetKey);
        Assert.Contains(RegenerationPlan.ReferencedKeys(Find(regeneration, MapB).Model), x => x == "/Moved/General");
        Assert.Equal(General, ((MetatileMapModel)plan.Find(MapA)!.Model).Sets[0].SetKey);
    }

    [Fact]
    public void ChangeKinds_Described()
    {
        var entries = EntriesOf(Plan());
        entries = Replace(entries, PalletSet, x =>
        {
            var set = (MetatileSetModel)x.CurrentModel!;
            return x with { CurrentModel = set with { PaletteSlots = set.PaletteSlots.ToDictionary(slot => slot.Key, slot => "/Other") } };
        });
        entries = Replace(entries, ViridianSet, x => x with { CurrentModel = (MetatileSetModel)x.CurrentModel! with { MetatileCount = 99 } });

        var regeneration = RegenerationPlan.Match(Plan(Repoint, x => x.Replace("{name:layoutMap} Border", "{name:layoutMap} Edge")), entries, false);

        Assert.Equal(["repointed"], Find(regeneration, MapA).ChangeKinds);
        Assert.Equal(["re-paletted"], Find(regeneration, PalletSet).ChangeKinds);
        Assert.Equal(["resized"], Find(regeneration, ViridianSet).ChangeKinds);

        var border = Assert.Single(regeneration.Resources, x => x.Entry?.Entry.Key == Border);
        Assert.Equal((RegenerationKind.Changed, "/Generated/Borders/Pallet Town/Pallet Town Edge"), (border.Kind, border.TargetKey));
        Assert.Equal(["changed"], border.ChangeKinds);
        Assert.Equal(4, regeneration.Written.Count());
    }

    [Fact]
    public void Report_ListsEveryCategory()
    {
        var entries = EntriesOf(Plan())
            .Where(x => x.Entry.Key != MapB)
            .Append(ExtraPalette("/Generated/Gone", 0x7F00))
            .Append(ExtraPalette("/Generated/Used", 0x7F20))
            .ToList();
        entries = Replace(entries, ViridianSet, x => x with { CurrentModel = (MetatileSetModel)x.CurrentModel! with { MetatileCount = 99 }, IsEdited = true });
        entries = Replace(entries, Border, x => x with { CurrentKey = null, CurrentModel = null });
        var plan = Plan(Repoint);

        var matched = RegenerationPlan.Match(plan, entries, false);
        var regeneration = matched.KeepReferenced(matched.Removed.Single(x => x.CurrentKey == "/Generated/Used"), ["/User"]);

        Assert.Equal([
            "Unchanged 1 compresseddata",
            "Unchanged 13 palette",
            "Removed 1 palette",
            "Unchanged 2 metatileset",
            "Added 1 metatilemap",
            "Changed 1 metatilemap",
            $"Changed {MapA}: repointed",
            "Removed /Generated/Gone",
            $"Kept edited {ViridianSet}",
            $"Kept missing {Border}",
            "Kept referenced /Generated/Used: used by /User",
            .. plan.Report.IssueLines()], regeneration.ReportLines(plan.Report, false));
        Assert.NotEmpty(plan.Report.IssueLines());

        var dryRun = regeneration.ReportLines(plan.Report, true).ToList();
        Assert.Contains("Would add 1 metatilemap", dryRun);
        Assert.Contains("Would change 1 metatilemap", dryRun);
        Assert.Contains("Would remove 1 palette", dryRun);
        Assert.Contains($"Would change {MapA}: repointed", dryRun);
        Assert.Contains("Would remove /Generated/Gone", dryRun);
        Assert.Contains("Unchanged 13 palette", dryRun);
        Assert.DoesNotContain(dryRun, x => x.StartsWith("Added") || x.StartsWith("Changed") || x.StartsWith("Removed"));

        var generation = regeneration.ToManifestGeneration(plan.ManifestGeneration, regeneration.Resources.ToDictionary(x => x.TargetKey, _ => new string('a', 64)));
        Assert.Equal(regeneration.Resources.Select(x => x.TargetKey).Concat([ViridianSet, Border, "/Generated/Used"]), generation.Resources.Select(x => x.Key));
        Assert.DoesNotContain(generation.Resources, x => x.Key == "/Generated/Gone");
        Assert.Equal("mapGroups[0][0].layout", generation.Resources.Single(x => x.Key == MapA).Path);
    }

    private static string PaletteOfPalletSet(GenerationPlan plan) => ((MetatileSetModel)plan.Find(PalletSet)!.Model).PaletteSlots.Values.Last();

    private static List<RegenerationEntry> DeletePalette(List<RegenerationEntry> entries, string palette) =>
        Replace(entries, palette, x => x with { CurrentKey = null, CurrentModel = null });

    [Fact]
    public void AddedReferencingDeleted_SkippedWithDependents()
    {
        var plan = Plan();
        var palette = PaletteOfPalletSet(plan);
        var entries = DeletePalette(EntriesOf(plan), palette).Where(x => x.Entry.Key is not (PalletSet or MapA)).ToList();

        var regeneration = RegenerationPlan.Match(plan, entries, false);

        Assert.Equal([(PalletSet, palette), (MapA, palette)], regeneration.Skipped.Select(x => (x.Planned.Key, x.Deleted)));
        Assert.DoesNotContain(regeneration.Resources, x => x.Planned.Key is PalletSet or MapA);
        var generation = regeneration.ToManifestGeneration(plan.ManifestGeneration, new Dictionary<string, string>());
        Assert.DoesNotContain(generation.Resources, x => x.Key is PalletSet or MapA);
        var lines = regeneration.SummaryLines(false).ToList();
        Assert.Contains($"Skipped {PalletSet}: references deleted {palette}", lines);
        Assert.Contains($"Skipped {MapA}: references deleted {palette}", lines);
        Assert.Contains($"Kept missing {palette}", lines);
    }

    [Fact]
    public void ChangedReferencingDeleted_KeptAsIs()
    {
        const string moved = "/Moved/Pallet Town";
        var plan = Plan();
        var palette = PaletteOfPalletSet(plan);
        var entries = Replace(DeletePalette(EntriesOf(plan), palette), PalletSet, x => x with
        {
            CurrentKey = moved,
            CurrentModel = (MetatileSetModel)x.CurrentModel! with { MetatileCount = 99 }
        });
        var setEntry = entries.Single(x => x.Entry.Key == PalletSet).Entry;

        var regeneration = RegenerationPlan.Match(plan, entries, false);

        var kept = Assert.Single(regeneration.Kept, x => x.Entry == setEntry);
        Assert.Equal((moved, "references deleted", palette), (kept.Key, kept.Reason, kept.Deleted));
        Assert.Contains($"Kept {moved}: references deleted {palette}", regeneration.SummaryLines(false));
        Assert.DoesNotContain(regeneration.Resources, x => x.Planned.Key == PalletSet);
        Assert.Empty(regeneration.Skipped);
        Assert.Contains(((MetatileMapModel)Find(regeneration, MapA).Model).Sets, x => x.SetKey == moved);
        var generation = regeneration.ToManifestGeneration(plan.ManifestGeneration, new Dictionary<string, string>());
        Assert.Equal(setEntry with { Key = moved }, Assert.Single(generation.Resources, x => x.Key == moved));
    }

    public static TheoryData<Type> RegenerableTypes => new(ModelSamples.RegenerableTypes);

    [Theory]
    [MemberData(nameof(RegenerableTypes))]
    public void WithKeys_RoundTripPreservesEveryProperty(Type type)
    {
        var model = (ResourceModel)ModelSamples.Create(type);

        var remapped = RegenerationPlan.WithKeys(model, "Other", key => key + "'");
        var back = RegenerationPlan.WithKeys(remapped, model.Name, key => key[..^1]);

        Assert.Equal("Other", remapped.Name);
        Assert.NotEmpty(RegenerationPlan.ReferencedKeys(remapped));
        Assert.All(RegenerationPlan.ReferencedKeys(remapped), x => Assert.EndsWith("'", x));
        Assert.Equal(model.Name, back.Name);
        Assert.Equal(model, back);
    }
}
