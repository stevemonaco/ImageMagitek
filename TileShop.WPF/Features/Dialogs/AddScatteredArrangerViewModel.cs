using ImageMagitek;
using Stylet;
using System.Collections.Generic;

namespace TileShop.WPF.ViewModels
{
    public class AddScatteredArrangerViewModel : Screen
    {
        private string _arrangerName;
        public string ArrangerName
        {
            get => _arrangerName;
            set => SetAndNotify(ref _arrangerName, value);
        }

        private PixelColorType _colorType;
        public PixelColorType ColorType
        {
            get => _colorType;
            set => SetAndNotify(ref _colorType, value);
        }

        private ElementLayout _layout;
        public ElementLayout Layout
        {
            get => _layout;
            set => SetAndNotify(ref _layout, value);
        }

        private int _arrangerElementWidth;
        public int ArrangerElementWidth
        {
            get => _arrangerElementWidth;
            set => SetAndNotify(ref _arrangerElementWidth, value);
        }

        private int _arrangerElementHeight;
        public int ArrangerElementHeight
        {
            get => _arrangerElementHeight;
            set => SetAndNotify(ref _arrangerElementHeight, value);
        }

        private int _elementPixelWidth;
        public int ElementPixelWidth
        {
            get => _elementPixelWidth;
            set => SetAndNotify(ref _elementPixelWidth, value);
        }

        private int _elementPixelHeight;
        public int ElementPixelHeight
        {
            get => _elementPixelHeight;
            set => SetAndNotify(ref _elementPixelHeight, value);
        }

        private BindableCollection<string> _existingResourceNames = new BindableCollection<string>();
        public BindableCollection<string> ExistingResourceNames
        {
            get => _existingResourceNames;
            set => SetAndNotify(ref _existingResourceNames, value);
        }

        private BindableCollection<string> _validationErrors = new BindableCollection<string>();
        public BindableCollection<string> ValidationErrors
        {
            get => _validationErrors;
            set => SetAndNotify(ref _validationErrors, value);
        }

        private bool _canAdd;
        public bool CanAdd
        {
            get => _canAdd;
            set => SetAndNotify(ref _canAdd, value);
        }

        public AddScatteredArrangerViewModel() { }

        public AddScatteredArrangerViewModel(IEnumerable<string> existingResourceNames)
        {
            ExistingResourceNames.AddRange(existingResourceNames);
        }

        public void Add()
        {
            if (Layout == ElementLayout.Single)
            {
                ArrangerElementHeight = 1;
                ArrangerElementWidth = 1;
            }

            RequestClose(true);
        }

        public void Cancel() => RequestClose(false);

        public void ValidateModel()
        {
            ValidationErrors.Clear();

            if (string.IsNullOrWhiteSpace(ArrangerName))
                ValidationErrors.Add($"Name is invalid");

            if (ExistingResourceNames.Contains(ArrangerName))
                ValidationErrors.Add($"Name already exists");

            CanAdd = ValidationErrors.Count == 0;
        }
    }
}
