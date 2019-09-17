using System;
using System.Collections.Generic;
using System.Text;

namespace TileShop.WPF.ViewModels
{
    public class DataFileEditorViewModel : ResourceEditorBaseViewModel
    {
        public override bool SaveChanges()
        {
            return true;
        }

        public override bool DiscardChanges()
        {
            return true;
        }
    }
}
