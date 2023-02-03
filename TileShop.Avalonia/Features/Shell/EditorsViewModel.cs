using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Services;
using TileShop.Shared.Messages;
using Jot;
using Serilog;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using TileShop.Shared.Interactions;

namespace TileShop.AvaloniaUI.ViewModels;

public enum UserSaveAction { Save, Discard, Cancel, Unmodified }

public partial class EditorsViewModel : ObservableRecipient
{
    private readonly IInteractionService _interactions;
    private readonly Tracker _tracker;
    private readonly ICodecService _codecService;
    private readonly IPaletteService _paletteService;
    private readonly IProjectService _projectService;
    private readonly IElementLayoutService _layoutService;
    private readonly AppSettings _settings;

    public ObservableCollection<ResourceEditorBaseViewModel> Editors { get; } = new();

    [ObservableProperty] private ResourceEditorBaseViewModel? _activeEditor;
    [ObservableProperty] private ShellViewModel? _shell;

    public EditorsViewModel(AppSettings settings, IInteractionService interactionService, Tracker tracker,
        ICodecService codecService, IPaletteService paletteService, IProjectService projectService, IElementLayoutService layoutService)
    {
        _settings = settings;
        _interactions = interactionService;
        _tracker = tracker;
        _codecService = codecService;
        _paletteService = paletteService;
        _projectService = projectService;
        _layoutService = layoutService;

        Messenger.Register<EditArrangerPixelsMessage>(this, (r, m) => Receive(m));
        Messenger.Register<ArrangerChangedMessage>(this, (r, m) => Receive(m));
        Messenger.Register<PaletteChangedMessage>(this, (r, m) => Receive(m));
    }

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
                if (editor is not IndexedPixelEditorViewModel && editor is not DirectPixelEditorViewModel)
                {
                    var projectTree = _projectService.GetContainingProject(editor.Resource);
                    await _projectService.SaveProject(projectTree).Match(
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

    public void ActivateEditor(IProjectResource resource)
    {
        var openedDocument = Editors.FirstOrDefault(x => ReferenceEquals(x.Resource, resource));

        if (openedDocument is null)
        {
            ResourceEditorBaseViewModel? newDocument;

            switch (resource)
            {
                case Palette pal when pal.ColorModel != ColorModel.Nes:
                    newDocument = new PaletteEditorViewModel(pal, _paletteService, _projectService);
                    break;
                case Palette pal when pal.ColorModel == ColorModel.Nes:
                    newDocument = new PaletteEditorViewModel(pal, _paletteService, _projectService);
                    break;
                case ScatteredArranger scatteredArranger:
                    newDocument = new ScatteredArrangerEditorViewModel(scatteredArranger, _interactions, _paletteService, _projectService, _tracker, _settings);
                    break;
                case SequentialArranger sequentialArranger:
                    newDocument = new SequentialArrangerEditorViewModel(sequentialArranger, _interactions, _tracker, _codecService, _paletteService, _layoutService);
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

                    var newArranger = new SequentialArranger(8, 16, fileSource, _paletteService.DefaultPalette, _codecService.CodecFactory, codecName);
                    newDocument = new SequentialArrangerEditorViewModel(newArranger, _interactions, _tracker, _codecService, _paletteService, _layoutService);
                    break;
                case ResourceFolder resourceFolder:
                    newDocument = null;
                    break;
                case ImageProject project:
                    newDocument = null;
                    break;
                default:
                    newDocument = null;
                    break;
                    //throw new InvalidOperationException();
            }

            if (newDocument is not null)
            {
                newDocument.OriginatingProjectResource = resource;
                Editors.Add(newDocument);
                ActiveEditor = newDocument;
            }
        }
        else
            ActiveEditor = openedDocument;
    }

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

                if (userAction == UserSaveAction.Save)
                    savedProjects.Add(_projectService.GetContainingProject(editor.Resource));
            }

            foreach (var projectTree in savedProjects)
            {
                var result = _projectService.SaveProject(projectTree);

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
                if (saveTree)
                {
                    var projectTree = _projectService.GetContainingProject(editor.Resource);
                    await _projectService.SaveProject(projectTree).Match(
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
                editor.DiscardChanges();
                return UserSaveAction.Discard;
            }
            else if (result == PromptResult.Cancel)
                return UserSaveAction.Cancel;
        }

        return UserSaveAction.Unmodified;
    }

    public void Receive(EditArrangerPixelsMessage message)
    {
        if (Shell is null)
            throw new NullReferenceException(nameof(Shell));

        if (message.Arranger.ColorType == PixelColorType.Indexed)
        {
            var editor = new IndexedPixelEditorViewModel(message.Arranger, message.ProjectArranger, message.X, message.Y,
                message.Width, message.Height, _interactions, _paletteService, _tracker);

            editor.DisplayName = message.Arranger.Name;

            Shell.Editors.Editors.Add(editor);
            ActiveEditor = editor;
        }
        else if (message.Arranger.ColorType == PixelColorType.Direct)
        {
            var editor = new DirectPixelEditorViewModel(message.Arranger, message.ProjectArranger, message.X, message.Y,
                message.Width, message.Height, _interactions, _paletteService, _tracker);

            editor.DisplayName = message.Arranger.Name;

            Shell.Editors.Editors.Add(editor);
            ActiveEditor = editor;
        }
    }

    public void Receive(ArrangerChangedMessage message)
    {
        if (message.Change == ArrangerChange.Pixels || message.Change == ArrangerChange.Elements)
        {
            var effectedEditors = Editors.OfType<ArrangerEditorViewModel>()
                .Where(x => ReferenceEquals(x.Resource, message.Arranger));

            foreach (var editor in effectedEditors)
            {
                if (editor is SequentialArrangerEditorViewModel || editor is ScatteredArrangerEditorViewModel)
                {
                    editor.Render();
                }
            }
        }
    }

    public void Receive(PaletteChangedMessage message)
    {
        var effectedEditors = Editors.OfType<ScatteredArrangerEditorViewModel>()
            .Where(x => x.WorkingArranger.GetReferencedPalettes().Contains(message.Palette));

        foreach (var editor in effectedEditors)
            editor.Render();
    }
}
