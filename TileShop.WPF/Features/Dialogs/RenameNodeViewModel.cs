using Stylet;

namespace TileShop.WPF.ViewModels;

public class RenameNodeViewModel : Screen
{
    private ResourceNodeViewModel _nodeModel;

    private string _name;
    public string Name
    {
        get => _name;
        set => SetAndNotify(ref _name, value);
    }

    public RenameNodeViewModel(ResourceNodeViewModel nodeModel)
    {
        _nodeModel = nodeModel;
        Name = nodeModel.Name;
    }

    public void Rename()
    {
        RequestClose(true);
    }

    public void Cancel() => RequestClose(false);
}
