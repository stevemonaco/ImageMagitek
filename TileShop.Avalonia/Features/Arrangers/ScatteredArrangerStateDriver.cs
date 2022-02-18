using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TileShop.AvaloniaUI.ViewModels;

namespace TileShop.Shared.Input;

public class ScatteredArrangerStateDriver : ArrangerStateDriver<ScatteredArrangerEditorViewModel>
{
    public ScatteredArrangerStateDriver(ScatteredArrangerEditorViewModel viewModel) : base(viewModel)
    {
    }
}
