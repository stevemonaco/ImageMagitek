using Stylet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TileShop.WPF.ViewModels
{
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
            set => SetAndNotify(ref _width, value);
        }

        private int _height;
        public int Height
        {
            get => _height;
            set => SetAndNotify(ref _height, value);
        }

        private bool _canConfirm;
        public bool CanConfirm
        {
            get => _canConfirm;
            set => SetAndNotify(ref _canConfirm, value);
        }

        public void Confirm()
        {

        }

        public void Cancel()
        {

        }
    }
}
