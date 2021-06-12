using Stylet;
using System.Threading.Tasks;
using TileShop.Shared.EventModels;
using TileShop.WPF.Models;
using ImageMagitek.Colors;
using System.Linq;
using ImageMagitek.Services;

namespace TileShop.WPF.ViewModels
{
    public class PaletteEditorViewModel : ResourceEditorBaseViewModel
    {
        private readonly Palette _palette;
        private readonly IPaletteService _paletteService;
        private readonly IProjectService _projectService;
        private readonly IEventAggregator _events;

        private BindableCollection<ValidatedColor32Model> _colors = new BindableCollection<ValidatedColor32Model>();
        public BindableCollection<ValidatedColor32Model> Colors
        {
            get => _colors;
            set => SetAndNotify(ref _colors, value);
        }

        private ValidatedColor32Model _selectedColor;
        public ValidatedColor32Model SelectedColor
        {
            get => _selectedColor;
            set => SetAndNotify(ref _selectedColor, value);
        }

        private string _paletteSource;
        public string PaletteSource
        {
            get => _paletteSource;
            set => SetAndNotify(ref _paletteSource, value);
        }

        private long _fileOffset;
        public long FileOffset
        {
            get => _fileOffset;
            set => SetAndNotify(ref _fileOffset, value);
        }

        private bool _zeroIndexTransparent;
        public bool ZeroIndexTransparent
        {
            get => _zeroIndexTransparent;
            set
            {
                if (SetAndNotify(ref _zeroIndexTransparent, value))
                    IsModified = true;
            }
        }

        public PaletteEditorViewModel(Palette palette, IPaletteService paletteService, IProjectService projectService, IEventAggregator events)
        {
            Resource = palette;
            _palette = palette;
            _paletteService = paletteService;
            _projectService = projectService;
            _events = events;
            events.Subscribe(this);

            DisplayName = Resource?.Name ?? "Unnamed Palette";

            for(int i = 0; i < _palette.Entries; i++)
                Colors.Add(new ValidatedColor32Model((IColor32)_palette.GetForeignColor(i), i, _paletteService.ColorFactory));

            SelectedColor = Colors.First();
            FileOffset = _palette.FileAddress.FileOffset;
            PaletteSource = _palette.DataFile.Name;
        }

        public void SaveColor()
        {
            SelectedColor.SaveColor();
            _palette.SetForeignColor(SelectedColor.Index, SelectedColor.WorkingColor);
            IsModified = true;
        }

        public override void SaveChanges()
        {
            _palette.ZeroIndexTransparent = ZeroIndexTransparent;
            _palette.SavePalette();

            var projectTree = _projectService.GetContainingProject(_palette);
            var paletteNode = projectTree.GetResourceNode(_palette);
            _projectService.SaveResource(projectTree, paletteNode, false);
            IsModified = false;

            var changeEvent = new PaletteChangedEvent(_palette);
            _events.PublishOnUIThread(changeEvent);
        }

        public override void DiscardChanges()
        {
            _palette.Reload();
            ZeroIndexTransparent = _palette.ZeroIndexTransparent;
            IsModified = false;
        }

        public void MouseOver(ValidatedColor32Model model)
        {
            string notifyMessage = $"Palette Index: {model.Index}";
            var notifyEvent = new NotifyStatusEvent(notifyMessage, NotifyStatusDuration.Indefinite);
            _events.PublishOnUIThread(notifyEvent);
        }

        public override void Undo()
        {
            throw new System.NotImplementedException();
        }

        public override void Redo()
        {
            throw new System.NotImplementedException();
        }

        public override void ApplyHistoryAction(HistoryAction action)
        {
            throw new System.NotImplementedException();
        }
    }
}
