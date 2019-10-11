using TileShop.WPF.DialogModels;
using TileShop.WPF.Views.Dialogs;
using TileShop.WPF.Behaviors;

namespace TileShop.WPF.Services
{
    public interface IDialogService
    {
        bool ShowAddPaletteDialog(AddPaletteDialogModel model);
    }

    public class DialogService : IDialogService
    {
        public bool ShowAddPaletteDialog(AddPaletteDialogModel model)
        {
            var view = new AddPaletteView();
            view.DataContext = model;
            return Result(view.ShowDialog());
        }
        
        private bool Result(bool? showDialogResult)
        {
            if (showDialogResult.HasValue)
                return showDialogResult.Value;
            return false;
        }
    }
}
