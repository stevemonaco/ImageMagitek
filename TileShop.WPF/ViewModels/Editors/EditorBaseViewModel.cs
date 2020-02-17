using Stylet;
using System.Windows.Media;

namespace TileShop.WPF.ViewModels
{
	public abstract class EditorBaseViewModel : Screen
	{
		public abstract string Name { get; }

		protected bool _isModified;
		public virtual bool IsModified
		{
			get => _isModified;
			set => SetAndNotify(ref _isModified, value);
		}

		private ImageSource _iconSource;
		public ImageSource IconSource
		{ 
			get => _iconSource;
			set => SetAndNotify(ref _iconSource, value);
		}

		private string _contentId;
		public string ContentId
		{
			get => _contentId;
			set => SetAndNotify(ref _contentId, value);
		}

		private string _isSelected;
		public string IsSelected
		{
			get => _isSelected;
			set => SetAndNotify(ref _isSelected, value);
		}

		private string _isEditorActive;
		public string IsEditorActive
		{
			get => _isEditorActive;
			set => SetAndNotify(ref _isEditorActive, value);
		}
	}
}
