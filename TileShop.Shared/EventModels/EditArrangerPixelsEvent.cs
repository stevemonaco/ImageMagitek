using System;
using System.Collections.Generic;
using System.Text;
using TileShop.Shared.Models;

namespace TileShop.Shared.EventModels
{
    public class EditArrangerPixelsEvent
    {
        public ArrangerTransferModel ArrangerTransferModel { get; set; }

        public EditArrangerPixelsEvent(ArrangerTransferModel model)
        {
            ArrangerTransferModel = model;
        }
    }
}
