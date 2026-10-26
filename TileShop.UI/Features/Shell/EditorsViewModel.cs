using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Services;
using TileShop.Shared.Messages;
using Microsoft.Extensions.Logging;
using Serilog;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using TileShop.Shared.Interactions;
using ImageMagitek.Services.Stores;
using ImageMagitek.Codec;
using ImageMagitek.Metatiles;
using ImageMagitek.Scenes;
using ImageMagitek.Tilemaps;
using TileShop.UI.Features.Graphics;
using TileShop.Shared.Services;
using TileShop.Shared.Tools;
using TileShop.UI.Models;
using TileShop.UI.Services;
using Monaco.PathTree;
using Avalonia.Threading;

namespace TileShop.UI.ViewModels;

public enum UserSaveAction { Save, Discard, Cancel, Unmodified }

public partial class EditorsViewModel : ObservableRecipient
{
    private readonly IInteractionService _interactions;
    private readonly UserPreferencesStore _preferencesStore;
    private readonly ICodecService _codecService;
    private readonly IColorFactory _colorFactory;
    private readonly PaletteStore _paletteStore;
    private readonly IProjectService _projectService;
    private readonly ElementStore _elementStore;
    private readonly AppSettings _settings;
    private readonly ILoggerFactory _loggerFactory;
    private readonly HotkeyService _hotkeys;
    private readonly IAsyncFileRequestService _fileRequests;
    private readonly ClipboardService _clipboard;
    private readonly HashSet<IProjectResource> _pendingResourceChanges = new(ReferenceEqualityComparer.Instance);

    public ObservableCollection<ResourceEditorBaseViewModel> Editors { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveGraphicsEditor))]
    [NotifyPropertyChangedFor(nameof(ActiveEditCommands))]
    private ResourceEditorBaseViewModel? _activeEditor;

    [ObservableProperty] private ShellViewModel? _shell;

    public GraphicsEditorViewModel? ActiveGraphicsEditor => ActiveEditor as GraphicsEditorViewModel;

    public EditCommands ActiveEditCommands => ActiveEditor?.EditCommands ?? EditCommands.None;

    partial void OnActiveEditorChanged(ResourceEditorBaseViewModel? value)
    {
        if (value is null)
            _hotkeys.ClearScope();
        else
            _hotkeys.SetScope(value.Hotkeys);
    }

    public EditorsViewModel(AppSettings settings, IInteractionService interactionService, UserPreferencesStore preferencesStore, ICodecService codecService,
        IColorFactory colorFactory, PaletteStore paletteStore, IProjectService projectService, ElementStore elementStore,
        ILoggerFactory loggerFactory, HotkeyService hotkeys, IAsyncFileRequestService fileRequests, ClipboardService clipboard)
    {
        _settings = settings;
        _interactions = interactionService;
        _preferencesStore = preferencesStore;
        _codecService = codecService;
        _colorFactory = colorFactory;
        _paletteStore = paletteStore;
        _projectService = projectService;
        _elementStore = elementStore;
        _loggerFactory = loggerFactory;
        _hotkeys = hotkeys;
        _fileRequests = fileRequests;
        _clipboard = clipboard;

        Messenger.Register<PaletteColorAssignedMessage>(this, (r, m) => Receive(m));
        Messenger.Register<EditTileSetMessage>(this, (r, m) => Receive(m));

        _projectService.TreeChanged += OnTreeChanged;
        _projectService.ResourceChanged += OnResourceChanged;
        Editors.CollectionChanged += Editors_CollectionChanged;
    }

    public bool HasModifiedEditors => Editors.Any(x => x.IsModified);

    private void Editors_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var editor in e.OldItems?.OfType<ResourceEditorBaseViewModel>() ?? [])
            editor.PropertyChanged -= Editor_PropertyChanged;

        foreach (var editor in e.NewItems?.OfType<ResourceEditorBaseViewModel>() ?? [])
            editor.PropertyChanged += Editor_PropertyChanged;

        OnPropertyChanged(nameof(HasModifiedEditors));
    }

    private void Editor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ResourceEditorBaseViewModel.IsModified))
            OnPropertyChanged(nameof(HasModifiedEditors));
    }

    private void OnTreeChanged(object? sender, ProjectTreeChange change)
    {
        if (change.Kind == ProjectTreeChangeKind.Renamed)
        {
            var renamed = ResourceSet([change.Node.Item]);
            foreach (var editor in Editors.Where(x => x.EditsAny(renamed)))
                editor.DisplayName = change.Node.Name;

            if (change.Node.Item is Palette palette)
            {
                var models = Editors.OfType<GraphicsEditorViewModel>()
                    .SelectMany(x => x.Palettes)
                    .Where(x => ReferenceEquals(x.Palette, palette));

                foreach (var model in models)
                    model.Name = change.Node.Name;
            }
        }
        else if (change.Kind == ProjectTreeChangeKind.Removed)
        {
            var removedResources = ResourceSet(change.Node.SelfAndDescendantsDepthFirst<ResourceNode, IProjectResource>().Select(x => x.Item));

            RemoveEditors(Editors.Where(x => x.EditsAny(removedResources)).ToList());
            ClearClipboardIfReferencesAny(removedResources);
        }
        else if (change.Kind == ProjectTreeChangeKind.Replaced)
        {
            var replaced = ResourceSet([change.OldNode!.Item]);

            foreach (var editor in Editors.Where(x => x.EditsAny(replaced)).ToList())
                ReplaceEditor(editor, change.Node.Item);

            ClearClipboardIfReferencesAny(replaced);
        }
    }

    private static HashSet<IProjectResource> ResourceSet(IEnumerable<IProjectResource> resources) =>
        resources.ToHashSet<IProjectResource>(ReferenceEqualityComparer.Instance);

    private void ClearClipboardIfReferencesAny(IReadOnlySet<IProjectResource> resources)
    {
        if (!GraphicsEditorViewModel.ClearClipboardIfReferencesAny(resources))
            return;

        foreach (var editor in Editors.OfType<GraphicsEditorViewModel>())
            editor.NotifyClipboardChanged();
    }

    private void ReplaceEditor(ResourceEditorBaseViewModel editor, IProjectResource resource)
    {
        var index = Editors.IndexOf(editor);

        if (CreateEditor(resource).Editor is not { } replacement)
        {
            RemoveEditors([editor]);
            return;
        }

        var wasActive = ReferenceEquals(ActiveEditor, editor);
        editor.OnClosing();
        Editors[index] = replacement;

        if (wasActive)
            ActiveEditor = replacement;
    }

    /// <summary>
    /// Replaces each of <paramref name="editors"/> still open with a new editor on its resource, in the same tab, or closes it when its resource left the tree
    /// </summary>
    public void ReopenEditors(IReadOnlyCollection<ResourceEditorBaseViewModel> editors)
    {
        foreach (var editor in editors.Where(Editors.Contains).ToList())
        {
            if (_projectService.FindContainingProject(editor.OriginatingProjectResource) is not null)
                ReplaceEditor(editor, editor.OriginatingProjectResource);
            else
                RemoveEditors([editor]);
        }
    }

    /// <summary>
    /// The open editors, in tab order, that depend on any of <paramref name="resources"/>
    /// </summary>
    public IReadOnlyList<ResourceEditorBaseViewModel> FindAffected(IReadOnlySet<IProjectResource> resources) =>
        Editors.Where(x => x.DependsOn(resources)).ToList();

    /// <summary>
    /// Closes <paramref name="editors"/> without prompting, changing the active editor only when it was among them.
    /// </summary>
    public void RemoveEditors(IReadOnlyCollection<ResourceEditorBaseViewModel> editors)
    {
        var activeRemoved = ActiveEditor is { } active && editors.Contains(active);
        foreach (var editor in editors)
        {
            Editors.Remove(editor);
            editor.OnClosing();
        }

        if (activeRemoved)
            ActiveEditor = Editors.FirstOrDefault();
    }

    /// <summary>
    /// Prompts to save or discard the editors affected by <paramref name="plan"/>, then closes editors of resources it changes but keeps.
    /// Editors of removed resources close when the removal is applied.
    /// </summary>
    /// <returns>False if the user cancelled</returns>
    public async Task<bool> ConfirmRemovalAsync(ResourceDeletionPlan plan)
    {
        var changedResources = ResourceSet(plan.Changes.Select(x => x.Resource));
        var affected = Editors.Where(x => x.EditsAny(changedResources)).ToList();

        if ((await ResolveUnsavedChangesAsync(affected, UnsavedChangesPrompt.Closing())).Contains(UserSaveAction.Cancel))
            return false;

        var removedResources = ResourceSet(plan.Changes.Where(x => x.Removed).Select(x => x.Resource));
        var danglingEditors = Editors.OfType<GraphicsEditorViewModel>()
            .Where(x => x.IsModified && x.WorkingArranger is ScatteredArranger or TilemapView && !affected.Contains(x) && x.DependsOn(removedResources))
            .ToList<ResourceEditorBaseViewModel>();

        if ((await ResolveUnsavedChangesAsync(danglingEditors, UnsavedChangesPrompt.DiscardForDeletion)).Contains(UserSaveAction.Cancel))
            return false;

        RemoveEditors(affected.Where(x => !x.EditsAny(removedResources)).ToList());
        return true;
    }

    public async Task<bool> CloseEditor(ResourceEditorBaseViewModel? editor)
    {
        if (editor is null)
            return true;

        if ((await ResolveUnsavedChangesAsync([editor], UnsavedChangesPrompt.Closing() with { SaveProject = true })).Contains(UserSaveAction.Cancel))
            return false;

        RemoveEditors([editor]);
        return true;
    }

    public async Task ActivateEditor(IProjectResource resource)
    {
        if (await OpenEditor(resource) is { } editor)
            ActiveEditor = editor;
    }

    /// <summary>
    /// Returns the open editor for <paramref name="resource"/>, or creates one and adds it to <see cref="Editors"/>
    /// </summary>
    /// <returns>Null when the resource has no editor</returns>
    private async Task<ResourceEditorBaseViewModel?> OpenEditor(IProjectResource resource)
    {
        var openedDocument = Editors.FirstOrDefault(x => ReferenceEquals(x.Resource, resource));

        if (openedDocument is not null)
            return openedDocument;

        var (newDocument, alert) = CreateEditor(resource);
        if (alert is { } shown)
        {
            await _interactions.AlertAsync(shown.Title, shown.Message);
            return null;
        }

        if (newDocument is not null)
            Editors.Add(newDocument);

        return newDocument;
    }

    // Neither editor nor alert means the resource has no editor
    private (ResourceEditorBaseViewModel? Editor, (string Title, string Message)? Alert) CreateEditor(IProjectResource resource)
    {
        if (FindMissingDataSource(resource) is { } missing)
            return (null, ("Missing Data File", DescribeMissingDataSource(resource, missing)));

        switch (resource)
        {
            case Palette pal:
                return (new PaletteEditorViewModel(pal, _colorFactory, _projectService, _interactions, _fileRequests, _clipboard), null);
            case TilemapArranger { MapError: { } mapError }:
                return (null, ("Tilemap", mapError));
            case MetatileMap { MapError: { } mapError }:
                return (null, ("Map Error", mapError));
            case MetatileMap map:
                return (new MetatileMapEditorViewModel(map, _preferencesStore.Preferences.Grid, _interactions, composite => ExportMetatileMapAsync(map, composite)), null);
            case MetatileSet:
                return (null, null);
            case TilemapArranger or Arranger:
                return (new GraphicsEditorViewModel((IArrangerView)resource, _interactions, _codecService, _colorFactory, _paletteStore, _elementStore, _projectService, _preferencesStore,
                    _loggerFactory.CreateLogger<GraphicsEditorViewModel>()), null);
            case CompressedDataSource { DecompressionError: { } error }:
                return (null, ("Compressed Data", error));
            case DataSource dataSource: // Always open a new SequentialArranger so users are able to view multiple sections of the same file at once
                var fileSource = dataSource as FileDataSource ?? (dataSource as CompressedDataSource)?.Parent as FileDataSource;
                var extension = Path.GetExtension(fileSource?.FileLocation ?? "").ToLower();
                string codecName;
                if (_settings.ExtensionCodecAssociations.ContainsKey(extension))
                    codecName = _settings.ExtensionCodecAssociations[extension];
                else if (_settings.ExtensionCodecAssociations.ContainsKey("default"))
                    codecName = _settings.ExtensionCodecAssociations["default"];
                else
                    codecName = "NES 1bpp";

                var codec = _codecService.CodecFactory.CreateCodec(codecName);
                if (codec is null)
                    return (null, ("Codec Error", $"Could not create Codec '{codecName}'"));

                var newArranger = codec.Layout == ImageLayout.Tiled
                    ? new SequentialArranger(8, 16, dataSource, _paletteStore.DefaultPalette, _codecService.CodecFactory, codec)
                    : new SequentialArranger(1, 1, dataSource, _paletteStore.DefaultPalette, _codecService.CodecFactory, codec);

                return (CreateSequentialEditor(newArranger, dataSource), null);
            case ResourceFolder:
            case ImageProject:
                return (null, null);
            default:
                throw new NotSupportedException($"Project resource of type '{resource.GetType()}' is not supported");
        }
    }

    private GraphicsEditorViewModel CreateSequentialEditor(SequentialArranger arranger, DataSource source) =>
        new(arranger, _interactions, _codecService, _colorFactory, _paletteStore, _elementStore, _projectService, _preferencesStore,
            _loggerFactory.CreateLogger<GraphicsEditorViewModel>())
        {
            OriginatingProjectResource = source
        };

    /// <summary>
    /// Opens a new sequential editor on the tilemap's tile set: its tile source at the tile offset, with its codec and slot 0's palette, 16 tiles wide
    /// </summary>
    public Task OpenTileSetAsync(TilemapArranger tilemap) =>
        OpenTileSetAsync(tilemap.TileSource, tilemap.TileOffset, tilemap.CodecName, tilemap.TileCount, tilemap.PaletteSlots[0]);

    /// <summary>
    /// Opens a new sequential editor on a tile set: <paramref name="tileCount"/> tiles of <paramref name="source"/> from <paramref name="offset"/>,
    /// with <paramref name="codecName"/> and <paramref name="slot0"/> (the default palette when null), 16 tiles wide
    /// </summary>
    public async Task OpenTileSetAsync(DataSource source, long offset, string codecName, int tileCount, Palette? slot0)
    {
        if (await AlertIfMissingDataSourceAsync(source, "Edit Tile Set"))
            return;

        // Only whole rows of tiles the source holds, or SequentialArranger.Move pulls the start back before the tile offset
        var columns = Math.Clamp(tileCount, 1, TilePalette.DefaultColumns);
        var rows = Math.Clamp(tileCount / columns, 1, 32);
        var palette = slot0 ?? _paletteStore.DefaultPalette;
        var codec = _codecService.CodecFactory.CreateCodec(codecName)!;
        var arranger = new SequentialArranger(columns, rows, source, palette, _codecService.CodecFactory, codec);
        arranger.Move(new BitAddress(offset, 0));

        var editor = CreateSequentialEditor(arranger, source);
        editor.SelectPalette(palette);
        Editors.Add(editor);
        ActiveEditor = editor;
    }

    /// <summary>
    /// Writes the map's composite, with its current layer visibility, to a PNG the user picks
    /// </summary>
    /// <param name="composite">Draws the image to write in place of the saved map, such as an editor's unsaved blocks</param>
    public async Task ExportMetatileMapAsync(MetatileMap map, Func<LayerImage>? composite = null)
    {
        if (await AlertIfUnavailableAsync(map, "Export"))
            return;

        var exportFileName = await _fileRequests.RequestExportArrangerFileName($"{map.Name}.png");

        if (exportFileName is null)
            return;

        try
        {
            (composite?.Invoke() ?? map.Composite()).ExportImage(exportFileName.LocalPath, new ImageSharpFileAdapter());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not export '{MapName}' to '{FileName}'", map.Name, exportFileName.LocalPath);
            await _interactions.AlertAsync("Export Error", ex.Message);
        }
    }

    public void Receive(EditTileSetMessage message) => _ = OpenTileSetAsync(message.Tilemap);

    /// <summary>
    /// Alerts the user when <paramref name="resource"/> reads from a missing data file, directly or through its palettes
    /// </summary>
    /// <returns>True if a data file is missing</returns>
    public async Task<bool> AlertIfMissingDataSourceAsync(IProjectResource resource, string title)
    {
        if (FindMissingDataSource(resource) is not { } missing)
            return false;

        await _interactions.AlertAsync(title, DescribeMissingDataSource(resource, missing));
        return true;
    }

    /// <summary>
    /// Alerts the user when <paramref name="resource"/> reads from a missing data file or its contents cannot be read, such as a tilemap's map
    /// </summary>
    /// <returns>True if the resource's pixels are unavailable</returns>
    public async Task<bool> AlertIfUnavailableAsync(IProjectResource resource, string title)
    {
        if (await AlertIfMissingDataSourceAsync(resource, title))
            return true;

        if ((resource as IUnavailableResource)?.UnavailableReason is not { } reason)
            return false;

        await _interactions.AlertAsync(title, reason);
        return true;
    }

    private static FileDataSource? FindMissingDataSource(IProjectResource resource) => (resource as IUnavailableResource)?.FindMissingDataSource();

    private static string DescribeMissingDataSource(IProjectResource resource, FileDataSource missing) =>
        ReferenceEquals(resource, missing)
            ? $"Data file '{missing.Name}' is missing from '{missing.FileLocation}'. Right-click it in the project tree and choose Relink... to repair it."
            : $"'{resource.Name}' is unavailable because data file '{missing.Name}' is missing. Right-click it in the project tree and choose Relink... to repair it.";

    /// <summary>
    /// Requests to save each opened, modified editor
    /// </summary>
    /// <returns>True if all user actions have been followed, false if the user cancelled</returns>
    public async Task<bool> RequestSaveAllUserChanges()
    {
        try
        {
            var editors = Editors.Where(x => x.IsModified).ToList();
            var actions = await ResolveUnsavedChangesAsync(editors, UnsavedChangesPrompt.Closing());
            if (actions.Contains(UserSaveAction.Cancel))
                return false;

            var savedProjects = editors.Zip(actions)
                .Where(x => x.Second == UserSaveAction.Save)
                .Select(x => _projectService.FindContainingProject(x.First.Resource))
                .OfType<ProjectTree>()
                .ToHashSet();

            await SaveProjectsAsync(savedProjects);
            return true;
        }
        catch (Exception ex)
        {
            await _interactions.AlertAsync("Error", ex.Message);
            Log.Error(ex, "Unhandled exception");
            return false;
        }
    }

    /// <summary>
    /// Saves every modified editor without prompting, then each project containing a saved editor once
    /// </summary>
    public async Task SaveAllAsync()
    {
        try
        {
            var modifiedEditors = Editors.Where(x => x.IsModified).ToList();

            foreach (var editor in modifiedEditors)
                await editor.SaveChangesAsync();

            var savedProjects = modifiedEditors
                .Where(x => !x.IsModified)
                .Select(x => _projectService.FindContainingProject(x.Resource))
                .OfType<ProjectTree>()
                .ToHashSet();

            await SaveProjectsAsync(savedProjects);
        }
        catch (Exception ex)
        {
            await _interactions.AlertAsync("Error", ex.Message);
            Log.Error(ex, "Unhandled exception");
        }
    }

    private async Task SaveProjectsAsync(IEnumerable<ProjectTree> projectTrees)
    {
        foreach (var projectTree in projectTrees)
        {
            var result = await _projectService.SaveProjectAsync(projectTree);

            if (result.HasFailed)
                await _interactions.AlertAsync("Project Error", $"An error occurred while saving the project tree to {projectTree.Root.DiskLocation}:\n{result.AsError.Reason}");
        }
    }

    /// <summary>
    /// Prompts for each modified editor in turn, stopping at the first Cancel
    /// </summary>
    /// <returns>The action for each editor prompted or skipped, in order, ending with Cancel when the user cancelled</returns>
    public async Task<IReadOnlyList<UserSaveAction>> ResolveUnsavedChangesAsync(IReadOnlyList<ResourceEditorBaseViewModel> editors, UnsavedChangesPrompt prompt)
    {
        var actions = new List<UserSaveAction>();

        foreach (var editor in editors)
        {
            var action = prompt.DiscardOnly ? await ConfirmDiscardAsync(editor, prompt) : await RequestSaveUserChanges(editor, prompt);
            actions.Add(action);

            if (action == UserSaveAction.Cancel)
                return actions;
        }

        if (prompt.DiscardOnly)
        {
            foreach (var (editor, _) in editors.Zip(actions).Where(x => x.Second == UserSaveAction.Discard))
                await DiscardChangesAsync(editor);
        }

        return actions;
    }

    private async Task<UserSaveAction> ConfirmDiscardAsync(ResourceEditorBaseViewModel editor, UnsavedChangesPrompt prompt)
    {
        if (!editor.IsModified)
            return UserSaveAction.Unmodified;

        var result = await _interactions.PromptAsync(PromptChoices.OkCancel, prompt.Title, prompt.Message(editor));
        return result == PromptResult.Accept ? UserSaveAction.Discard : UserSaveAction.Cancel;
    }

    private static async Task DiscardChangesAsync(ResourceEditorBaseViewModel editor)
    {
        if (editor is PaletteEditorViewModel paletteEditor)
            await paletteEditor.DiscardChangesAsync();
        else
            editor.DiscardChanges();
    }

    private async Task<UserSaveAction> RequestSaveUserChanges(ResourceEditorBaseViewModel editor, UnsavedChangesPrompt prompt)
    {
        if (editor.IsModified)
        {
            var result = await _interactions.PromptAsync(PromptChoices.YesNoCancel, prompt.Title, prompt.Message(editor));

            if (result == PromptResult.Accept)
            {
                await editor.SaveChangesAsync();
                if (editor.IsModified)
                    return UserSaveAction.Cancel;

                if (prompt.SaveProject && _projectService.FindContainingProject(editor.Resource) is { } projectTree)
                {
                    var saveTreeResult = await _projectService.SaveProjectAsync(projectTree);
                    await saveTreeResult.Match(
                         success =>
                         {
                             return Task.CompletedTask;
                         },
                         async fail =>
                         {
                             await _interactions.AlertAsync("Project Save Error", $"An error occurred while saving the project tree to {projectTree.Root.DiskLocation}: {fail.Reason}");
                         });
                }

                return UserSaveAction.Save;
            }
            else if (result == PromptResult.Reject)
            {
                if (prompt.DiscardOnNo)
                    await DiscardChangesAsync(editor);
                return UserSaveAction.Discard;
            }
            else if (result == PromptResult.Cancel)
                return UserSaveAction.Cancel;
        }

        return UserSaveAction.Unmodified;
    }

    private void OnResourceChanged(object? sender, IProjectResource resource)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnResourceChanged(sender, resource));
            return;
        }

        // Coalesces bursts such as a gradient fill into one refresh per resource
        if (_pendingResourceChanges.Count == 0)
            Dispatcher.UIThread.Post(FlushResourceChanges);

        _pendingResourceChanges.Add(resource);
    }

    private void FlushResourceChanges()
    {
        var resources = _pendingResourceChanges.ToList();
        _pendingResourceChanges.Clear();

        foreach (var resource in resources)
        {
            if (resource is Palette palette)
                RefreshPalette(palette);
            else if (resource is DataSource source)
                ReloadDataSource(source);

            foreach (var editor in Editors.OfType<MetatileMapEditorViewModel>().Where(x => x.ReadsFrom(resource)))
                editor.OnSourceChanged();
        }
    }

    private void ReloadDataSource(DataSource source)
    {
        // Modified editors are skipped so a refresh never wipes unsaved work
        var affectedEditors = Editors.OfType<GraphicsEditorViewModel>()
            .Where(x => !x.IsModified && x.ReadsFrom(source));

        foreach (var editor in affectedEditors)
            editor.ReloadFromSource();
    }

    /// <summary>
    /// Re-maps the palette's colors without re-decoding pixels, so graphics editors keep their pending pixel edits
    /// </summary>
    private void RefreshPalette(Palette palette)
    {
        foreach (var editor in Editors.OfType<GraphicsEditorViewModel>())
        {
            var models = editor.Palettes.Where(x => ReferenceEquals(x.Palette, palette)).ToList();
            foreach (var model in models)
                model.Refresh();

            if (models.Count > 0 || editor.WorkingArranger.GetReferencedPalettes().Contains(palette))
                editor.InvalidateEditor(InvalidationLevel.Display);

            editor.TilePalette?.Refresh();
        }
    }

    /// <summary>
    /// Routes a color edited elsewhere to the palette's editor, opening it in the background, so the palette has one modified state
    /// </summary>
    public async void Receive(PaletteColorAssignedMessage message)
    {
        try
        {
            if (await OpenEditor(message.Palette) is PaletteEditorViewModel editor)
                editor.AssignColor(message.Index, message.Color);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not assign color to palette '{PaletteName}'", message.Palette.Name);
            await _interactions.AlertAsync("Palette Error", ex.Message);
        }
    }
}
