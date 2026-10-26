using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagitek;
using ImageMagitek.Project;
using ImageMagitek.Services;
using ImageMagitek.Tables;
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

    private readonly IResourceGenerationService _generationService;
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

    /// <param name="editors">Prompts for and reopens the editors a regeneration affects; null skips both</param>
    public GenerateResourcesViewModel(IResourceGenerationService generationService, ProjectTree tree, string dataFileKey, string defaultFolderKey,
        IAsyncFileRequestService fileSelect, IInteractionService interactions, EditorsViewModel? editors)
    {
        _generationService = generationService;
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

        var planResult = _generationService.PlanGeneration(_tree, DataFileKey, definitionResult.AsSuccess.Result, targetFolder, OverwriteEdited);
        if (planResult.HasFailed)
            return new PreviewResult(null, [], planResult.AsError.Reason);

        var plan = planResult.AsSuccess.Result;
        return new PreviewResult(plan, _generationService.FindGenerationConflicts(_tree, plan), null);
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
            var actions = await _editors.ResolveUnsavedChangesAsync(_editors.FindAffected(regeneration.Affected),
                UnsavedChangesPrompt.Closing("will be closed by regeneration"));
            var saved = actions.Contains(UserSaveAction.Save);

            // A saved generated resource now counts as edited, which the previewed plan would overwrite
            if (saved)
                await Preview();

            if (saved || actions.Contains(UserSaveAction.Cancel))
                return false;
        }

        GenerationErrorText = null;
        IsGenerating = true;

        // Editors that Replaced swaps in during the run can still read resources the commit rebuilds afterwards, so they reopen too
        var affected = plan.Regeneration is { } planned && _editors is not null ? _editors.FindAffected(planned.Affected) : [];
        var openBefore = _editors?.Editors.ToHashSet() ?? [];

        try
        {
            var result = await _generationService.GenerateResourcesAsync(_tree, plan, SaveProject, plan.Regeneration is not null);
            if (_editors is not null && plan.Regeneration is not null)
                _editors.ReopenEditors([.. affected, .. _editors.Editors.Where(x => !openBefore.Contains(x))]);

            if (result.HasFailed)
            {
                GenerationErrorText = result.AsError.Reason;
                return false;
            }

            return true;
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private sealed record PreviewResult(GenerationPlan? Plan, IReadOnlyList<string> Conflicts, string? Error);
}
