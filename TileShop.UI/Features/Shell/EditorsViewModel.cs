using System;
using System.Collections.Generic;
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

        _projectService.TreeChanged += OnTreeChanged;
        _projectService.ResourceChanged += OnResourceChanged;
    }

    private void OnTreeChanged(object? sender, ProjectTreeChange change)
    {
        if (change.Kind == ProjectTreeChangeKind.Renamed)
        {
            foreach (var editor in Editors.Where(x => ReferenceEquals(x.Resource, change.Node.Item)))
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
            var removedResources = change.Node.SelfAndDescendantsDepthFirst<ResourceNode, IProjectResource>()
                .Select(x => (object)x.Item)
                .ToHashSet(ReferenceEqualityComparer.Instance);

            RemoveEditors(Editors.Where(x => removedResources.Contains(x.Resource) || removedResources.Contains(x.OriginatingProjectResource)).ToList());

            if (GraphicsEditorViewModel.ClearClipboardIfReferencesAny(removedResources))
            {
                foreach (var editor in Editors.OfType<GraphicsEditorViewModel>())
                    editor.NotifyClipboardChanged();
            }
        }
    }

    private void RemoveEditors(IReadOnlyCollection<ResourceEditorBaseViewModel> editors)
    {
        var activeRemoved = ActiveEditor is { } active && editors.Contains(active);
        foreach (var editor in editors)
            Editors.Remove(editor);

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
        var changedResources = plan.Changes.Select(x => (object)x.Resource).ToHashSet(ReferenceEqualityComparer.Instance);
        var affected = Editors
            .Where(x => changedResources.Contains(x.Resource) || changedResources.Contains(x.OriginatingProjectResource))
            .ToList();

        foreach (var editor in affected)
        {
            if (await RequestSaveUserChanges(editor, false) == UserSaveAction.Cancel)
                return false;
        }

        var removedResources = plan.Changes.Where(x => x.Removed).Select(x => (object)x.Resource).ToHashSet(ReferenceEqualityComparer.Instance);
        var danglingEditors = Editors.OfType<GraphicsEditorViewModel>()
            .Where(x => x.IsModified && x.WorkingArranger is ScatteredArranger && !affected.Contains(x)
                && ReferencesAny(x.WorkingArranger, removedResources))
            .ToList();

        foreach (var editor in danglingEditors)
        {
            var discardResult = await _interactions.PromptAsync(PromptChoices.OkCancel, "Discard Changes",
                $"'{editor.DisplayName}' has unsaved changes that use resources being deleted. These changes will be discarded.");

            if (discardResult != PromptResult.Accept)
                return false;
        }

        foreach (var editor in danglingEditors)
            editor.DiscardChanges();

        RemoveEditors(affected.Where(x => !removedResources.Contains(x.Resource) && !removedResources.Contains(x.OriginatingProjectResource)).ToList());
        return true;
    }

    private static bool ReferencesAny(Arranger arranger, HashSet<object> resources) =>
        arranger.EnumerateElements().OfType<ArrangerElement>()
            .Any(el => resources.Contains(el.Source) || el.Codec is IIndexedCodec { Palette: { } palette } && resources.Contains(palette));

    public async Task<bool> CloseEditor(ResourceEditorBaseViewModel? editor)
    {
        if (editor is null)
            return true;

        if (editor.IsModified)
        {
            var userAction = await RequestSaveUserChanges(editor, true);
            if (userAction == UserSaveAction.Cancel)
                return false;

            if (userAction == UserSaveAction.Save)
            {
                // if (editor is not IndexedPixelEditorViewModel and not DirectPixelEditorViewModel)
                if (editor is not GraphicsEditorViewModel { EditMode: GraphicsEditMode.Draw}
                    && _projectService.FindContainingProject(editor.Resource) is { } projectTree)
                {
                    var saveResult = await _projectService.SaveProjectAsync(projectTree);
                    await saveResult.Match(
                        success =>
                        {
                            return Task.CompletedTask;
                        },
                        async fail =>
                        {
                            await _interactions.AlertAsync("Project Error", $"An error occurred while saving the project tree to {projectTree.Root.DiskLocation}: {fail.Reason}");
                        }
                    );
                }
            }
        }

        Editors.Remove(editor);
        ActiveEditor = Editors.FirstOrDefault();

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

        if (await AlertIfMissingDataSourceAsync(resource, "Missing Data File"))
            return null;

        ResourceEditorBaseViewModel? newDocument;

        switch (resource)
        {
            case Palette pal:
                newDocument = new PaletteEditorViewModel(pal, _colorFactory, _projectService, _interactions, _fileRequests, _clipboard);
                break;
            case ScatteredArranger scatteredArranger:
                // newDocument = new ScatteredArrangerEditorViewModel(scatteredArranger, _interactions, _colorFactory, _paletteStore, _projectService, _tracker, _settings);
                newDocument = new GraphicsEditorViewModel(scatteredArranger, _interactions, _codecService, _colorFactory, _paletteStore, _elementStore, _projectService, _preferencesStore, _loggerFactory.CreateLogger<GraphicsEditorViewModel>());
                break;
            case SequentialArranger sequentialArranger:
                //newDocument = new SequentialArrangerEditorViewModel(sequentialArranger, _interactions, _tracker, _codecService, _colorFactory, _paletteStore, _elementStore);
                newDocument = new GraphicsEditorViewModel(sequentialArranger, _interactions, _codecService, _colorFactory, _paletteStore, _elementStore, _projectService, _preferencesStore, _loggerFactory.CreateLogger<GraphicsEditorViewModel>());
                break;
            case FileDataSource fileSource: // Always open a new SequentialArranger so users are able to view multiple sections of the same file at once
                var extension = Path.GetExtension(fileSource.FileLocation).ToLower();
                string codecName;
                if (_settings.ExtensionCodecAssociations.ContainsKey(extension))
                    codecName = _settings.ExtensionCodecAssociations[extension];
                else if (_settings.ExtensionCodecAssociations.ContainsKey("default"))
                    codecName = _settings.ExtensionCodecAssociations["default"];
                else
                    codecName = "NES 1bpp";

                var codec = _codecService.CodecFactory.CreateCodec(codecName);
                if (codec is null)
                {
                    await _interactions.AlertAsync("Codec Error", $"Could not create Codec '{codecName}'");
                    return null;
                }

                var newArranger = codec.Layout == ImageLayout.Tiled
                    ? new SequentialArranger(8, 16, fileSource, _paletteStore.DefaultPalette, _codecService.CodecFactory, codec)
                    : new SequentialArranger(1, 1, fileSource, _paletteStore.DefaultPalette, _codecService.CodecFactory, codec);

                // newDocument = new SequentialArrangerEditorViewModel(newArranger, _interactions, _tracker, _codecService, _colorFactory, _paletteStore, _elementStore)
                // {
                //     OriginatingProjectResource = fileSource
                // };
                
                newDocument = new GraphicsEditorViewModel(newArranger, _interactions, _codecService, _colorFactory, _paletteStore, _elementStore, _projectService, _preferencesStore, _loggerFactory.CreateLogger<GraphicsEditorViewModel>())
                {
                    OriginatingProjectResource = fileSource
                };
                break;
            case ResourceFolder resourceFolder:
                newDocument = null;
                break;
            case ImageProject project:
                newDocument = null;
                break;
            default:
                throw new NotSupportedException($"Project resource of type '{resource.GetType()}' is not supported");
        }

        if (newDocument is not null)
            Editors.Add(newDocument);

        return newDocument;
    }

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

    private static FileDataSource? FindMissingDataSource(IProjectResource resource)
    {
        IEnumerable<DataSource?> sources = resource switch
        {
            FileDataSource fileSource => [fileSource],
            Palette palette => [palette.DataSource],
            Arranger arranger => arranger.EnumerateElements().OfType<ArrangerElement>().Select(x => (DataSource?)x.Source)
                .Concat(arranger.GetReferencedPalettes().Select(x => x.DataSource)),
            _ => []
        };

        return sources.OfType<FileDataSource>().Distinct().FirstOrDefault(x => x.IsMissing);
    }

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
            var savedProjects = new HashSet<ProjectTree>();

            foreach (var editor in Editors.Where(x => x.IsModified))
            {
                var userAction = await RequestSaveUserChanges(editor, true);
                if (userAction == UserSaveAction.Cancel)
                    return false;

                if (userAction == UserSaveAction.Save && _projectService.FindContainingProject(editor.Resource) is { } projectTree)
                    savedProjects.Add(projectTree);
            }

            foreach (var projectTree in savedProjects)
            {
                var result = await _projectService.SaveProjectAsync(projectTree);

                if (result.HasFailed)
                    await _interactions.AlertAsync("Project Error", $"An error occurred while saving the project tree to {projectTree.Root.DiskLocation}:\n{result.AsError.Reason}");
            }

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
    /// Requests to the user if they want to save the specified editor and saves if necessary
    /// </summary>
    /// <param name="editor">Editor to save</param>
    /// <param name="saveTree">The project tree is also saved upon a Save confirmation</param>
    /// <returns>Action requested by user</returns>
    public async Task<UserSaveAction> RequestSaveUserChanges(ResourceEditorBaseViewModel editor, bool saveTree)
    {
        if (editor.IsModified)
        {
            var result = await _interactions.PromptAsync(PromptChoices.YesNoCancel, "Save changes", $"'{editor.DisplayName}' has been modified and will be closed. Save changes?");

            if (result == PromptResult.Accept)
            {
                await editor.SaveChangesAsync();
                if (editor.IsModified)
                    return UserSaveAction.Cancel;

                if (saveTree && _projectService.FindContainingProject(editor.Resource) is { } projectTree)
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
                if (editor is PaletteEditorViewModel paletteEditor)
                    await paletteEditor.DiscardChangesAsync();
                else
                    editor.DiscardChanges();
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
