namespace TileShop.WPF.Models
{
    public class ForeignColorSourceModel : ColorSourceModel
    {
        private string _foreignHexColor;
        public string ForeignHexColor
        {
            get => _foreignHexColor;
            set => SetAndNotify(ref _foreignHexColor, value);
        }

        public ForeignColorSourceModel(string foreignHexColor)
        {
            ForeignHexColor = foreignHexColor;
        }
    }
}
