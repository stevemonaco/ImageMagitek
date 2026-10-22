using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Metatiles;
using ImageMagitek.Project;
using ImageMagitek.Services;
using ImageMagitek.Tables;
using Monaco.PathTree;
using TileShop.Shared.Interactions;

namespace TileShop.UI.ViewModels;

/// <summary>
/// One row of the outputs list: a planned resource, or a removed, kept or skipped one for a regeneration
/// </summary>
/// <param name="Change">For a regeneration: added, changed with its kinds, unchanged, removed, kept with its reason, or skipped</param>
public sealed record GenerationOutputItem(string Type, string Name, string Key, string ParentKey, string Summary, string Change);

/// <summary>
/// Previews and runs generation or regeneration of resources from a table definition over one data source (UI-PROJECT-TREE-138 to -151)
/// </summary>
/// <remarks>The result is the key of the folder generated into</remarks>
public partial class GenerateResourcesViewModel : RequestViewModel<string>
{
    public const string AllTypes = "All";

    private readonly IProjectService _projectService;
    private readonly ProjectTree _tree;
    private readonly IAsyncFileRequestService _fileSelect;
    private readonly IInteractionService _interactions;
    private readonly EditorsViewModel? _editors;
    private int _previewVersion;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviewCommand))]
    private string _definitionPath = "";

    [ObservableProperty] private string _targetFolder;
    [ObservableProperty] private bool _saveProject;
    [ObservableProperty] private bool _overwriteEdited;
    [ObservableProperty] private bool _isRegeneration;
    [ObservableProperty] private bool _isPreviewing;
    [ObservableProperty] private bool _isGenerating;
    [ObservableProperty] private string? _generationErrorText;
    [ObservableProperty] private string _summary = "";

    [ObservableProperty] private IReadOnlyList<string> _reportLines = [];
    [ObservableProperty] private IReadOnlyList<string> _conflicts = [];
    [ObservableProperty] private IReadOnlyList<GenerationOutputItem> _filteredOutputs = [];
    [ObservableProperty] private IReadOnlyList<string> _typeFilters = [];
    [ObservableProperty] private string? _selectedTypeFilter;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreview))]
    [NotifyCanExecuteChangedFor(nameof(TryAcceptCommand))]
    private GenerationPlan? _plan;

    public string DataFileKey { get; }
    public bool IsBusy => IsPreviewing || IsGenerating;
    public bool HasPreview => Plan is not null;
    public override DialogSize Size => DialogSize.Large;

    /// <param name="editors">Reopens the editors a regeneration replaces; null leaves them closed</param>
    public GenerateResourcesViewModel(IProjectService projectService, ProjectTree tree, string dataFileKey, string defaultFolderKey,
        IAsyncFileRequestService fileSelect, IInteractionService interactions, EditorsViewModel? editors)
    {
        _projectService = projectService;
        _tree = tree;
        DataFileKey = dataFileKey;
        _targetFolder = defaultFolderKey;
        _fileSelect = fileSelect;
        _interactions = interactions;
        _editors = editors;
        Title = "Generate Resources from Table";
        AcceptName = "Generate";
    }

    partial void OnDefinitionPathChanged(string value) => InvalidateInputs();
    partial void OnTargetFolderChanged(string value) => InvalidateInputs();
    partial void OnOverwriteEditedChanged(bool value) => InvalidatePreview();
    partial void OnIsPreviewingChanged(bool value) => OnBusyChanged();
    partial void OnIsGeneratingChanged(bool value) => OnBusyChanged();

    partial void OnSelectedTypeFilterChanged(string? value)
    {
        var outputs = Plan is { } plan ? ListOutputs(plan) : [];
        FilteredOutputs = value is null or AllTypes ? outputs : outputs.Where(x => x.Type == value).ToList();
    }

    private static List<GenerationOutputItem> ListOutputs(GenerationPlan plan)
    {
        if (plan.Regeneration is not { } regeneration)
            return plan.Resources.Select(x => new GenerationOutputItem(x.Type, x.Name, x.Key, x.ParentKey, x.Summary, "")).ToList();

        return regeneration.Resources
            .Select(x => new GenerationOutputItem(x.Planned.Type, x.Name, x.TargetKey, x.ParentKey, x.Planned.Summary, x.Kind switch
            {
                RegenerationKind.Added => "added",
                RegenerationKind.Changed => $"changed: {string.Join(", ", x.ChangeKinds)}",
                _ => "unchanged"
            }))
            .Concat(regeneration.Removed.Select(x => Existing(x.Entry.Type, x.CurrentKey!, "removed")))
            .Concat(regeneration.Kept.Select(x => Existing(x.Entry.Type, x.Key, x.Deleted is { } deleted ? $"kept: references deleted {deleted}" : $"kept {x.Reason}")))
            .Concat(regeneration.Skipped.Select(x => new GenerationOutputItem(x.Planned.Type, x.Planned.Name, x.Planned.Key, x.Planned.ParentKey, x.Planned.Summary,
                $"skipped: references deleted {x.Deleted}")))
            .ToList();

        static GenerationOutputItem Existing(string type, string key, string change) =>
            new(type, RegenerationPlan.NameOf(key), key, RegenerationPlan.ParentKeyOf(key), "", change);
    }

    [RelayCommand]
    private async Task Browse()
    {
        if (await _fileSelect.RequestTableDefinitionFileName() is { } fileName)
            DefinitionPath = fileName.LocalPath;
    }

    private bool CanPreview() => !string.IsNullOrWhiteSpace(DefinitionPath) && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanPreview))]
    private async Task Preview()
    {
        InvalidatePreview();
        var version = _previewVersion;
        var definitionPath = DefinitionPath.Trim().Trim('"');
        var targetFolder = TargetFolder.Trim();
        IsPreviewing = true;

        try
        {
            var result = await Task.Run(() => BuildPreview(definitionPath, targetFolder));
            if (version == _previewVersion)
                ApplyPreview(result);
        }
        catch (Exception ex)
        {
            if (version == _previewVersion)
                GenerationErrorText = ex.Message;
        }
        finally
        {
            IsPreviewing = false;
        }
    }

    private PreviewResult BuildPreview(string definitionPath, string targetFolder)
    {
        var definitionResult = new XmlTableDefinitionReader(Path.Combine(AppContext.BaseDirectory, "_schemas")).LoadFromFile(definitionPath);
        if (definitionResult.HasFailed)
            return new PreviewResult(null, [], string.Join("\n", XmlTableDefinitionReader.DescribeErrors(definitionPath, definitionResult.AsError.Reasons)));

        var planResult = _projectService.PlanGeneration(_tree, DataFileKey, definitionResult.AsSuccess.Result, targetFolder, OverwriteEdited);
        if (planResult.HasFailed)
            return new PreviewResult(null, [], planResult.AsError.Reason);

        var plan = planResult.AsSuccess.Result;
        return new PreviewResult(plan, _projectService.FindGenerationConflicts(_tree, plan), null);
    }

    private void ApplyPreview(PreviewResult result)
    {
        GenerationErrorText = result.Error;
        if (result.Plan is not { } plan)
            return;

        Plan = plan;
        IsRegeneration = plan.Regeneration is not null;
        SetAcceptName(IsRegeneration ? "Regenerate" : "Generate");
        Conflicts = result.Conflicts;
        Summary = string.Join("\n", plan.Regeneration?.SummaryLines(true) ?? plan.Report.CountLines("Would create"));
        ReportLines = plan.Report.IssueLines().ToList();
        TypeFilters = ListOutputs(plan).Select(x => x.Type).Distinct().Prepend(AllTypes).ToList();
        SelectedTypeFilter = AllTypes;
    }

    private void SetAcceptName(string name)
    {
        AcceptName = name;
        if (Options.FirstOrDefault(x => x.IsDefault) is { } option)
            option.OptionText = name;
    }

    // A new definition or folder may not be a regeneration, while the overwrite box must stay visible after it clears the preview
    private void InvalidateInputs()
    {
        IsRegeneration = false;
        InvalidatePreview();
    }

    private void InvalidatePreview()
    {
        _previewVersion++;
        Plan = null;
        GenerationErrorText = null;
        Summary = "";
        ReportLines = [];
        Conflicts = [];
        TypeFilters = [];
        SelectedTypeFilter = null;
    }

    private void OnBusyChanged()
    {
        OnPropertyChanged(nameof(IsBusy));
        PreviewCommand.NotifyCanExecuteChanged();
        TryAcceptCommand.NotifyCanExecuteChanged();
        TryCancelCommand.NotifyCanExecuteChanged();
    }

    protected override bool CanAccept() => Plan is not null && Conflicts.Count == 0 && !IsBusy;

    protected override bool CanTryCancel() => !IsGenerating;

    public override Task<bool> TryCancel() => IsGenerating ? Task.FromResult(false) : base.TryCancel();

    public override string? ProduceResult() => Plan?.FolderKeys.FirstOrDefault();

    protected override async Task<bool> OnAccepted()
    {
        if (Plan is not { } plan)
            return false;

        if (plan.Regeneration is { Removed.Count: > 0 } removing)
        {
            var keys = string.Join("\n", removing.Removed.Select(x => x.CurrentKey));
            if (await _interactions.PromptAsync(PromptChoices.YesNo, "Remove resources", $"Remove {removing.Removed.Count} resources?\n{keys}") != PromptResult.Accept)
                return false;
        }

        if (_editors is not null && plan.Regeneration is { } regeneration)
        {
            var saved = false;
            var cancelled = false;
            foreach (var editor in EditorsUsing(PredictReplaced(regeneration)))
            {
                var action = await _editors.RequestSaveUserChanges(editor, false, "will be closed by regeneration");
                if (action == UserSaveAction.Cancel)
                {
                    cancelled = true;
                    break;
                }

                saved |= action == UserSaveAction.Save;
            }

            // A saved generated resource now counts as edited, which the previewed plan would overwrite
            if (saved)
                await Preview();

            if (saved || cancelled)
                return false;
        }

        GenerationErrorText = null;
        IsGenerating = true;

        try
        {
            var openEditors = FindOpenEditors(plan.Regeneration);
            var activeEditor = _editors?.ActiveEditor;
            var itemsBefore = _tree.EnumerateDepthFirst().Select(x => (object)x.Item).ToHashSet(ReferenceEqualityComparer.Instance);
            var result = await _projectService.GenerateResourcesAsync(_tree, plan, SaveProject, plan.Regeneration is not null);
            if (result.HasFailed)
            {
                GenerationErrorText = result.AsError.Reason;
                return false;
            }

            CloseEditorsOfReplacedResources(itemsBefore);
            await ReopenEditorsAsync(openEditors, activeEditor);
            return true;
        }
        finally
        {
            IsGenerating = false;
        }
    }

    // The key each editor's resource will have after regeneration, so editors closed by a replaced node can be reopened
    private List<(ResourceEditorBaseViewModel Editor, string Key)> FindOpenEditors(RegenerationPlan? regeneration)
    {
        if (_editors is null || regeneration is null)
            return [];

        var keys = _tree.EnumerateDepthFirst().ToDictionary(x => (object)x.Item, x => _tree.CreatePathKey(x), ReferenceEqualityComparer.Instance);
        var moves = regeneration.Written
            .Where(x => x.Entry?.CurrentKey is not null)
            .ToDictionary(x => x.Entry!.CurrentKey!, x => x.TargetKey, StringComparer.Ordinal);

        return _editors.Editors
            .Select(x => (Editor: x, Item: keys.ContainsKey(x.Resource) ? x.Resource : x.OriginatingProjectResource))
            .Where(x => keys.ContainsKey(x.Item))
            .Select(x => (x.Editor, moves.GetValueOrDefault(keys[x.Item], keys[x.Item])))
            .ToList();
    }

    // Arrangers retargeted in place stay in the tree, but their editors' working copies still read the replaced resources
    private void CloseEditorsOfReplacedResources(HashSet<object> itemsBefore)
    {
        if (_editors is null)
            return;

        var gone = new HashSet<object>(itemsBefore, ReferenceEqualityComparer.Instance);
        gone.ExceptWith(_tree.EnumerateDepthFirst().Select(x => (object)x.Item));
        _editors.RemoveEditors(EditorsUsing(gone));
    }

    // Retargeting sets the palette of codecs the working copies share, so only the editor's palette list still holds a replaced palette
    private List<ResourceEditorBaseViewModel> EditorsUsing(HashSet<object> gone) =>
        _editors!.Editors
            .Where(x => gone.Contains(x.Resource) || x.OriginatingProjectResource is { } origin && gone.Contains(origin)
                || x is GraphicsEditorViewModel graphics
                    && (EditorsViewModel.ReferencesAny(graphics.WorkingArranger, gone) || graphics.Palettes.Any(palette => gone.Contains(palette.Palette))))
            .ToList();

    // The resources regeneration replaces or removes, and the dependents it rebuilds as LIB-PROJECT-SERVICE-079 retargets them
    private HashSet<object> PredictReplaced(RegenerationPlan regeneration)
    {
        var gone = regeneration.Resources.Where(x => x.Kind == RegenerationKind.Changed).Select(x => x.Entry!.CurrentKey)
            .Concat(regeneration.Removed.Select(x => x.CurrentKey))
            .Select(x => x is not null && _tree.TryGetNode(x, out var node) ? node.Item : null)
            .OfType<object>()
            .ToHashSet(ReferenceEqualityComparer.Instance);

        var rebuildable = _tree.EnumerateDepthFirst()
            .Select(x => x.Item)
            .Where(x => ProjectService.RebuildOrder(x) is not null)
            .ToList();

        for (var added = true; added;)
        {
            added = false;
            foreach (var item in rebuildable.Where(x => !gone.Contains(x)))
            {
                if (item.LinkedResources.Any(gone.Contains))
                    added |= gone.Add(item);
            }
        }

        return gone;
    }

    private async Task ReopenEditorsAsync(List<(ResourceEditorBaseViewModel Editor, string Key)> openEditors, ResourceEditorBaseViewModel? active)
    {
        if (_editors is null || openEditors.Count == 0)
            return;

        ResourceEditorBaseViewModel? activeReplacement = null;

        foreach (var (editor, key) in openEditors)
        {
            if (_editors.Editors.Contains(editor) || !_tree.TryGetNode(key, out var node))
                continue;

            await _editors.ActivateEditor(node.Item);

            // Kept by editor, since activating a data source again opens a second sequential editor
            if (ReferenceEquals(editor, active) && _editors.ActiveEditor is { } reopened
                && (ReferenceEquals(reopened.Resource, node.Item) || ReferenceEquals(reopened.OriginatingProjectResource, node.Item)))
                activeReplacement = reopened;
        }

        if (activeReplacement is not null)
            _editors.ActiveEditor = activeReplacement;
        else if (active is not null && _editors.Editors.Contains(active))
            _editors.ActiveEditor = active;
    }

    private sealed record PreviewResult(GenerationPlan? Plan, IReadOnlyList<string> Conflicts, string? Error);
}
