﻿using System;
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
using ImageMagitek.Services.Stores;
using ImageMagitek.Codec;

namespace TileShop.UI.ViewModels;

public enum UserSaveAction { Save, Discard, Cancel, Unmodified }

public partial class EditorsViewModel : ObservableRecipient
{
    private readonly IInteractionService _interactions;
    private readonly Tracker _tracker;
    private readonly ICodecService _codecService;
    private readonly IColorFactory _colorFactory;
    private readonly PaletteStore _paletteStore;
    private readonly IProjectService _projectService;
    private readonly ElementStore _elementStore;
    private readonly AppSettings _settings;

    public ObservableCollection<ResourceEditorBaseViewModel> Editors { get; } = new();

    [ObservableProperty] private ResourceEditorBaseViewModel? _activeEditor;
    [ObservableProperty] private ShellViewModel? _shell;

    public EditorsViewModel(AppSettings settings, IInteractionService interactionService, Tracker tracker, ICodecService codecService,
        IColorFactory colorFactory, PaletteStore paletteStore, IProjectService projectService, ElementStore elementStore)
    {
        _settings = settings;
        _interactions = interactionService;
        _tracker = tracker;
        _codecService = codecService;
        _colorFactory = colorFactory;
        _paletteStore = paletteStore;
        _projectService = projectService;
        _elementStore = elementStore;

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
                if (editor is not IndexedPixelEditorViewModel and not DirectPixelEditorViewModel)
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

    public async Task ActivateEditor(IProjectResource resource)
    {
        var openedDocument = Editors.FirstOrDefault(x => ReferenceEquals(x.Resource, resource));

        if (openedDocument is not null)
        {
            ActiveEditor = openedDocument;
            return;
        }

        ResourceEditorBaseViewModel? newDocument;

        switch (resource)
        {
            case Palette pal when pal.ColorModel != ColorModel.Nes:
                newDocument = new PaletteEditorViewModel(pal, _colorFactory, _projectService);
                break;
            case Palette pal when pal.ColorModel == ColorModel.Nes:
                newDocument = new PaletteEditorViewModel(pal, _colorFactory, _projectService);
                break;
            case ScatteredArranger scatteredArranger:
                newDocument = new ScatteredArrangerEditorViewModel(scatteredArranger, _interactions, _colorFactory, _paletteStore, _projectService, _tracker, _settings);
                break;
            case SequentialArranger sequentialArranger:
                newDocument = new SequentialArrangerEditorViewModel(sequentialArranger, _interactions, _tracker, _codecService, _colorFactory, _paletteStore, _elementStore);
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
                    return;
                }

                var newArranger = codec.Layout == ImageLayout.Tiled
                    ? new SequentialArranger(8, 16, fileSource, _paletteStore.DefaultPalette, _codecService.CodecFactory, codec)
                    : new SequentialArranger(1, 1, fileSource, _paletteStore.DefaultPalette, _codecService.CodecFactory, codec);

                newDocument = new SequentialArrangerEditorViewModel(newArranger, _interactions, _tracker, _codecService, _colorFactory, _paletteStore, _elementStore)
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
        {
            Editors.Add(newDocument);
            ActiveEditor = newDocument;
        }
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
                message.Width, message.Height, _interactions, _colorFactory, _paletteStore, _tracker);

            editor.DisplayName = message.Arranger.Name;

            Shell.Editors.Editors.Add(editor);
            ActiveEditor = editor;
        }
        else if (message.Arranger.ColorType == PixelColorType.Direct)
        {
            var editor = new DirectPixelEditorViewModel(message.Arranger, message.ProjectArranger, message.X, message.Y,
                message.Width, message.Height, _interactions, _colorFactory, _paletteStore, _tracker);

            editor.DisplayName = message.Arranger.Name;

            Shell.Editors.Editors.Add(editor);
            ActiveEditor = editor;
        }
    }

    public void Receive(ArrangerChangedMessage message)
    {
        if (message.Change is ArrangerChange.Pixels or ArrangerChange.Elements)
        {
            var effectedEditors = Editors.OfType<ArrangerEditorViewModel>()
                .Where(x => ReferenceEquals(x.Resource, message.Arranger));

            foreach (var editor in effectedEditors)
            {
                if (editor is SequentialArrangerEditorViewModel or ScatteredArrangerEditorViewModel)
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
