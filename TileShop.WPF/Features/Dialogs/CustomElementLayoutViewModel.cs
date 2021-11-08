using Stylet;

namespace TileShop.WPF.ViewModels;

public enum ElementLayoutFlowDirection { RowLeftToRight, ColumnTopToBottom }

public class CustomElementLayoutViewModel : Screen
{
    private ElementLayoutFlowDirection _flowDirection;
    public ElementLayoutFlowDirection FlowDirection
    {
        get => _flowDirection;
        set => SetAndNotify(ref _flowDirection, value);
    }

    private int _width;
    public int Width
    {
        get => _width;
        set
        {
            if (SetAndNotify(ref _width, value))
                ValidateModel();
        }
    }

    private int _height;
    public int Height
    {
        get => _height;
        set
        {
            if (SetAndNotify(ref _height, value))
                ValidateModel();
        }
    }

    private bool _canConfirm;
    public bool CanConfirm
    {
        get => _canConfirm;
        set => SetAndNotify(ref _canConfirm, value);
    }

    private BindableCollection<string> _validationErrors = new BindableCollection<string>();
    public BindableCollection<string> ValidationErrors
    {
        get => _validationErrors;
        set => SetAndNotify(ref _validationErrors, value);
    }

    protected override void OnInitialActivate()
    {
        ValidateModel();
    }

    public void Confirm() => RequestClose(true);

    public void Cancel() => RequestClose(false);

    public void ValidateModel()
    {
        ValidationErrors.Clear();

        if (Width <= 0)
            ValidationErrors.Add($"{nameof(Width)} must be 1 or larger");

        if (Height <= 0)
            ValidationErrors.Add($"{nameof(Height)} must be 1 or larger");

        CanConfirm = ValidationErrors.Count == 0;
    }
}
