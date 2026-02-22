using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    public bool IsSequentialArranger => WorkingArranger is SequentialArranger;

    // Sequential arranger file offset navigation
    private long _fileOffset;
    public long FileOffset
    {
        get => _fileOffset;
        set
        {
            if (SetProperty(ref _fileOffset, value))
                MoveToOffset(_fileOffset);
        }
    }

    [ObservableProperty] private long _maxFileDecodingOffset;
    [ObservableProperty] private int _arrangerPageSize;

    // Sequential arranger sizing properties
    private int _tiledArrangerWidth = 8;
    public int TiledArrangerWidth
    {
        get => _tiledArrangerWidth;
        set
        {
            if (SetProperty(ref _tiledArrangerWidth, value))
                ResizeSequentialArranger(TiledArrangerWidth, TiledArrangerHeight);
        }
    }

    private int _tiledArrangerHeight = 16;
    public int TiledArrangerHeight
    {
        get => _tiledArrangerHeight;
        set
        {
            if (SetProperty(ref _tiledArrangerHeight, value))
                ResizeSequentialArranger(TiledArrangerWidth, TiledArrangerHeight);
        }
    }

    private int _linearArrangerWidth = 256;
    public int LinearArrangerWidth
    {
        get => _linearArrangerWidth;
        set
        {
            if (WorkingArranger is SequentialArranger seqArr)
            {
                var preferredWidth = seqArr.ActiveCodec.GetPreferredWidth(value);
                SetProperty(ref _linearArrangerWidth, preferredWidth);
                ResizeSequentialArranger(LinearArrangerWidth, LinearArrangerHeight);
            }
        }
    }

    private int _linearArrangerHeight = 256;
    public int LinearArrangerHeight
    {
        get => _linearArrangerHeight;
        set
        {
            if (WorkingArranger is SequentialArranger seqArr)
            {
                var preferredHeight = seqArr.ActiveCodec.GetPreferredHeight(value);
                SetProperty(ref _linearArrangerHeight, preferredHeight);
                ResizeSequentialArranger(LinearArrangerWidth, LinearArrangerHeight);
            }
        }
    }

    [ObservableProperty] private int _arrangerWidthIncrement = 1;
    [ObservableProperty] private int _arrangerHeightIncrement = 1;
    [ObservableProperty] private int _elementWidthIncrement = 1;
    [ObservableProperty] private int _elementHeightIncrement = 1;

    [ObservableProperty] private GraphicsEditMode _editMode = GraphicsEditMode.Arrange;

    partial void OnEditModeChanged(GraphicsEditMode oldValue, GraphicsEditMode newValue)
    {
        // Deactivate the outgoing tool
        var outgoingTool = oldValue == GraphicsEditMode.Arrange
            ? _arrangerTools.GetValueOrDefault(ActiveArrangerTool)
            : _pixelTools.GetValueOrDefault(ActivePixelTool);

        if (outgoingTool is not null)
        {
            var historyAction = outgoingTool.Deactivate(this);
            if (historyAction is not null)
                AddHistoryAction(historyAction);
        }

        _modifierOverrideTool = null;

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

    [ObservableProperty] private ObservableCollection<PaletteModel> _palettes = new();
    [ObservableProperty] private PaletteModel? _selectedPalette;
    [ObservableProperty] private PaletteModel? _activePalette;

    [ObservableProperty] private ColorRgba32 _activeColor = new(255, 255, 255, 255);
    [ObservableProperty] private ColorRgba32 _primaryColor = new(255, 255, 255, 255);
    [ObservableProperty] private ColorRgba32 _secondaryColor = new(0, 0, 0, 255);

    [ObservableProperty] private byte _activeColorIndex;
    [ObservableProperty] private byte _primaryColorIndex;
    [ObservableProperty] private byte _secondaryColorIndex = 1;

    private HistoryAction? _activePencilHistory;

    partial void OnActiveArrangerToolChanged(ArrangerTool oldValue, ArrangerTool newValue)
    {
        if (_arrangerTools.TryGetValue(oldValue, out var outgoing))
        {
            var historyAction = outgoing.Deactivate(this);
            if (historyAction is not null)
                AddHistoryAction(historyAction);
        }

        if (newValue != ArrangerTool.Select && newValue != ArrangerTool.ApplyPalette)
            CancelOverlay();
    }

    public GraphicsEditorViewModel(Arranger arranger, IInteractionService interactionService,
        ICodecService codecService, IColorFactory colorFactory, PaletteStore paletteStore, 
        ElementStore elementStore, IProjectService projectService, Tracker tracker)
        : base(arranger)
    {
        WorkingArranger = arranger.Mode == ArrangerMode.Scattered ? arranger.CloneArranger() : arranger;
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
        // ViewDx = 0;
        // ViewDy = 0;
        // _viewWidth = WorkingArranger.ArrangerPixelSize.Width;
        // _viewHeight = WorkingArranger.ArrangerPixelSize.Height;

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

        if (WorkingArranger is SequentialArranger seqArr)
        {
            ArrangerPageSize = (int)seqArr.ArrangerBitSize / 8;
            MaxFileDecodingOffset = seqArr.FileSize - ArrangerPageSize;

            if (seqArr.Layout == ElementLayout.Tiled)
            {
                _tiledArrangerWidth = seqArr.ArrangerElementSize.Width;
                _tiledArrangerHeight = seqArr.ArrangerElementSize.Height;
            }
            else if (seqArr.Layout == ElementLayout.Single)
            {
                _linearArrangerWidth = seqArr.ArrangerPixelSize.Width;
                _linearArrangerHeight = seqArr.ArrangerPixelSize.Height;
            }

            ArrangerWidthIncrement = 1;
            ArrangerHeightIncrement = 1;
            ElementWidthIncrement = seqArr.ActiveCodec.WidthResizeIncrement;
            ElementHeightIncrement = seqArr.ActiveCodec.HeightResizeIncrement;
        }
    }

    private void CreateImages()
    {
        CancelOverlay();

        _imageAdapter = new ArrangerImageAdapter(WorkingArranger);
        BitmapAdapter = _imageAdapter.CreateBitmapAdapter();
        GridSettings = GridSettingsViewModel.CreateDefault(WorkingArranger);
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

    public bool ContainsPoint(int x, int y)
    {
        return x > 0 && y > 0 && x < WorkingArranger.ArrangerPixelSize.Width && y < WorkingArranger.ArrangerPixelSize.Height;
    }

    [RelayCommand]
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
        _imageAdapter.Reinitialize(WorkingArranger);
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
