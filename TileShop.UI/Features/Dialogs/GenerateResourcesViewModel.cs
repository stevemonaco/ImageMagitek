using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagitek.Project;
using ImageMagitek.Services;
using ImageMagitek.Tables;
using TileShop.Shared.Interactions;

namespace TileShop.UI.ViewModels;

/// <summary>
/// Previews and runs generation of resources from a table definition over one data source (UI-PROJECT-TREE-138 to -145)
/// </summary>
/// <remarks>The result is the key of the folder generated into</remarks>
public partial class GenerateResourcesViewModel : RequestViewModel<string>
{
    public const string AllTypes = "All";

    private readonly IProjectService _projectService;
    private readonly ProjectTree _tree;
    private readonly IAsyncFileRequestService _fileSelect;
    private int _previewVersion;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviewCommand))]
    private string _definitionPath = "";

    [ObservableProperty] private string _targetFolder;
    [ObservableProperty] private bool _isPreviewing;
    [ObservableProperty] private bool _isGenerating;
    [ObservableProperty] private string? _generationErrorText;
    [ObservableProperty] private string _summary = "";

    [ObservableProperty] private IReadOnlyList<string> _reportLines = [];
    [ObservableProperty] private IReadOnlyList<string> _conflicts = [];
    [ObservableProperty] private IReadOnlyList<PlannedResource> _filteredOutputs = [];
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

    public GenerateResourcesViewModel(IProjectService projectService, ProjectTree tree, string dataFileKey, string defaultFolderKey,
        IAsyncFileRequestService fileSelect)
    {
        _projectService = projectService;
        _tree = tree;
        DataFileKey = dataFileKey;
        _targetFolder = defaultFolderKey;
        _fileSelect = fileSelect;
        Title = "Generate Resources from Table";
        AcceptName = "Generate";
    }

    partial void OnDefinitionPathChanged(string value) => InvalidatePreview();
    partial void OnTargetFolderChanged(string value) => InvalidatePreview();
    partial void OnIsPreviewingChanged(bool value) => OnBusyChanged();
    partial void OnIsGeneratingChanged(bool value) => OnBusyChanged();

    partial void OnSelectedTypeFilterChanged(string? value) =>
        FilteredOutputs = value is null or AllTypes ? Plan?.Resources ?? [] : Plan!.Resources.Where(x => x.Type == value).ToList();

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

        var planResult = _projectService.PlanGeneration(_tree, DataFileKey, definitionResult.AsSuccess.Result, targetFolder);
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
        Conflicts = result.Conflicts;
        Summary = string.Join("\n", plan.Report.CountLines("Would create"));
        ReportLines = plan.Report.IssueLines().ToList();
        TypeFilters = plan.Resources.Select(x => x.Type).Distinct().Prepend(AllTypes).ToList();
        SelectedTypeFilter = AllTypes;
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

        GenerationErrorText = null;
        IsGenerating = true;

        try
        {
            var result = await _projectService.GenerateResourcesAsync(_tree, plan);
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
