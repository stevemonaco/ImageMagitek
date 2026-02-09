using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Services;
using ImageMagitek.Services.Stores;
using Jot;
using TileShop.Shared.Input;
using TileShop.Shared.Interactions;
using TileShop.Shared.Messages;
using TileShop.Shared.Models;
using TileShop.UI.Imaging;
using TileShop.UI.Models;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Features.Graphics;

public enum GraphicsEditMode { Arrange, Draw }
public enum PixelTool { Select, Pencil, ColorPicker, FloodFill }
public enum ArrangerTool { Select, ApplyPalette, PickPalette, InspectElement, RotateLeft, RotateRight, MirrorHorizontal, MirrorVertical }
public enum ColorPriority { Primary, Secondary }

public sealed partial class GraphicsEditorViewModel : ResourceEditorBaseViewModel, IStateDriver
{
    private readonly Tracker _tracker;
    private readonly IInteractionService _interactions;
    private readonly ICodecService _codecService;
    private readonly IColorFactory _colorFactory;
    private readonly PaletteStore _paletteStore;
    private readonly ElementStore _elementStore;
    private readonly IProjectService _projectService;

    public Arranger WorkingArranger { get; private set; }
    private Arranger _projectArranger;
    private ArrangerImageAdapter _imageAdapter = null!;

    // public IProjectResource Resource { get; private set; }
    // public IProjectResource? OriginatingProjectResource { get; private set; }

    [ObservableProperty] private BitmapAdapter _bitmapAdapter = null!;
    [ObservableProperty] private string _activityMessage = "";
    [ObservableProperty] private string _pendingOperationMessage = "";

    public bool IsSingleLayout => WorkingArranger?.Layout == ElementLayout.Single;
    public bool IsTiledLayout => WorkingArranger?.Layout == ElementLayout.Tiled;
    public bool IsIndexedColor => WorkingArranger?.ColorType == PixelColorType.Indexed;
    public bool IsDirectColor => WorkingArranger?.ColorType == PixelColorType.Direct;

    [ObservableProperty] private GraphicsEditMode _editMode = GraphicsEditMode.Arrange;

    partial void OnEditModeChanged(GraphicsEditMode value)
    {
        CancelOverlay();
        OnPropertyChanged(nameof(IsArrangerMode));
        OnPropertyChanged(nameof(IsDrawMode));
    }

    [ObservableProperty] private bool _canArrange;
    [ObservableProperty] private bool _canEdit;

    public bool IsArrangerMode => EditMode == GraphicsEditMode.Arrange;
    public bool IsDrawMode => EditMode == GraphicsEditMode.Draw;

    [ObservableProperty] private SnapMode _snapMode = SnapMode.Element;
    partial void OnSnapModeChanged(SnapMode value) => Selection.SelectionRect.SnapMode = value;

    [ObservableProperty] private ArrangerSelection _selection;
    [ObservableProperty] private bool _isSelecting;
    [ObservableProperty] private ArrangerPaste? _paste;

    public bool CanChangeSnapMode { get; private set; }
    public bool CanAcceptPixelPastes { get; init; }
    public bool CanAcceptElementPastes { get; init; }

    public bool CanEditSelection
    {
        get
        {
            if (Selection.HasSelection)
            {
                var rect = Selection.SelectionRect;
                if (rect.SnappedWidth == 0 || rect.SnappedHeight == 0)
                    return false;

                return !WorkingArranger.EnumerateElementsWithinPixelRange(rect.SnappedLeft, rect.SnappedTop, rect.SnappedWidth, rect.SnappedHeight)
                    .Any(x => x is null || x?.Source is null);
            }

            return false;
        }
    }

    [ObservableProperty] private GridSettingsViewModel _gridSettings;

    public Action? OnImageModified { get; set; }

    public int ViewDx { get; private set; }
    public int ViewDy { get; private set; }
    private int _viewWidth;
    private int _viewHeight;

    [ObservableProperty] private ObservableCollection<PaletteModel> _palettes = new();
    [ObservableProperty] private PaletteModel? _selectedPalette;
    [ObservableProperty] private PaletteModel? _activePalette;

    [ObservableProperty] private ColorRgba32 _activeColor = new(255, 255, 255, 255);
    [ObservableProperty] private ColorRgba32 _primaryColor = new(255, 255, 255, 255);
    [ObservableProperty] private ColorRgba32 _secondaryColor = new(0, 0, 0, 255);

    [ObservableProperty] private byte _activeColorIndex;
    [ObservableProperty] private byte _primaryColorIndex;
    [ObservableProperty] private byte _secondaryColorIndex = 1;

    private PixelTool? _priorPixelTool;
    private HistoryAction? _activePencilHistory;
    private ApplyPaletteHistoryAction? _applyPaletteHistory;

    partial void OnActiveArrangerToolChanged(ArrangerTool value)
    {
        if (value != ArrangerTool.Select && value != ArrangerTool.ApplyPalette)
            CancelOverlay();
    }

    public GraphicsEditorViewModel(Arranger arranger, IInteractionService interactionService,
        ICodecService codecService, IColorFactory colorFactory, PaletteStore paletteStore, 
        ElementStore elementStore, IProjectService projectService, Tracker tracker)
        : base(arranger)
    {
        WorkingArranger = arranger.CloneArranger();
        _projectArranger = arranger;
        Resource = arranger;
        OriginatingProjectResource = arranger;
        DisplayName = WorkingArranger.Name ?? "Unnamed Graphics";

        _interactions = interactionService;
        _codecService = codecService;
        _colorFactory = colorFactory;
        _paletteStore = paletteStore;
        _elementStore = elementStore;
        _projectService = projectService;
        _tracker = tracker;

        CanAcceptElementPastes = true;
        CanAcceptPixelPastes = true;

        Initialize();

        _selection = new ArrangerSelection(WorkingArranger, SnapMode);
        //Messenger.Register<ResourceRenamedMessage>(this, HandleResourceRenamed);
    }

    private void Initialize()
    {
        ViewDx = 0;
        ViewDy = 0;
        _viewWidth = WorkingArranger.ArrangerPixelSize.Width;
        _viewHeight = WorkingArranger.ArrangerPixelSize.Height;

        CreateImages();
        InitializePalettes();

        if (WorkingArranger.Layout == ElementLayout.Single)
        {
            SnapMode = SnapMode.Pixel;
        }
        else if (WorkingArranger.Layout == ElementLayout.Tiled)
        {
            SnapMode = SnapMode.Element;
            CanChangeSnapMode = true;
        }
    }

    private void CreateImages()
    {
        CancelOverlay();

        _imageAdapter = new ArrangerImageAdapter(WorkingArranger, ViewDx, ViewDy, _viewWidth, _viewHeight);
        BitmapAdapter = _imageAdapter.CreateBitmapAdapter();
        GridSettings = GridSettingsViewModel.CreateDefault(WorkingArranger, ViewDx, ViewDy, _viewWidth, _viewHeight);
    }

    private void InitializePalettes()
    {
        if (IsIndexedColor)
        {
            var arrangerPalettes = WorkingArranger.GetReferencedPalettes();
            arrangerPalettes.ExceptWith(_paletteStore.GlobalPalettes);

            var palModels = arrangerPalettes.OrderBy(x => x.Name)
                .Concat(_paletteStore.GlobalPalettes.OrderBy(x => x.Name))
                .Select(x => new PaletteModel(x));

            Palettes = new(palModels);
            if (Palettes.Count > 0)
            {
                SelectedPalette = Palettes.First();
                ActivePalette = Palettes.First();
            }
        }
    }

    public void Render()
    {
        _imageAdapter.Render();
        BitmapAdapter.Invalidate();
        OnImageModified?.Invoke();
    }

    private void ReloadImage() => _imageAdapter.Render();

    public override Task SaveChangesAsync() => SaveChangesInternalAsync();

    private async Task SaveChangesInternalAsync()
    {
        try
        {
            _imageAdapter.SaveImage();

            var projectTree = _projectService.GetContainingProject(Resource);
            projectTree.TryFindResourceNode(Resource, out var resourceNode);

            if (resourceNode is not null)
            {
                await _projectService.SaveResource(projectTree, resourceNode, true).Match(
                    success =>
                    {
                        ClearHistory();
                        IsModified = false;
                        return Task.CompletedTask;
                    },
                    async fail => await _interactions.AlertAsync("Project Error", $"An error occurred while saving: {fail.Reason}")
                );
            }

            var changeMessage = new ArrangerChangedMessage(_projectArranger, ArrangerChange.Pixels);
            Messenger.Send(changeMessage);
        }
        catch (Exception ex)
        {
            await _interactions.AlertAsync("Save Error", $"Could not save the graphics contents\n{ex.Message}\n{ex.StackTrace}");
        }
    }

    public override void DiscardChanges()
    {
        WorkingArranger = _projectArranger.CloneArranger();
        _imageAdapter.Reinitialize(WorkingArranger, ViewDx, ViewDy, _viewWidth, _viewHeight);
        BitmapAdapter = _imageAdapter.CreateBitmapAdapter();
        GridSettings.AdjustGridlines(WorkingArranger);
        ClearHistory();
        IsModified = false;
    }

    private void HandleResourceRenamed(object recipient, ResourceRenamedMessage message)
    {
        if (ReferenceEquals(Resource, message.Resource))
            DisplayName = message.NewName;
    }

    public void NotifyColorTypeChanged()
    {
        OnPropertyChanged(nameof(IsIndexedColor));
        OnPropertyChanged(nameof(IsDirectColor));
        InitializePalettes();
    }
}
