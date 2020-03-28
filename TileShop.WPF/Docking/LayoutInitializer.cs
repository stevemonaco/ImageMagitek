using System.Linq;
using TileShop.WPF.ViewModels;
using AvalonDock.Layout;

namespace TileShop.WPF.Docking
{
	public class LayoutInitializer : ILayoutUpdateStrategy
	{
		public bool BeforeInsertAnchorable(LayoutRoot layout, LayoutAnchorable anchorableToShow, ILayoutContainer destinationContainer)
		{
			// AD wants to add the anchorable into destinationContainer
			// just for test provide a new anchorablepane 
			// if the pane is floating let the manager go ahead
			LayoutAnchorablePane destPane = destinationContainer as LayoutAnchorablePane;
			if (destinationContainer != null &&
				destinationContainer.FindParent<LayoutFloatingWindow>() != null)
				return false;

			var projectPane = layout.Descendents().OfType<LayoutAnchorablePane>().FirstOrDefault(d => d.Name == "ProjectPane");
			if (projectPane != null && anchorableToShow.Content is ProjectTreeViewModel)
			{
				projectPane.Children.Add(anchorableToShow);
				return true;
			}
			else if (anchorableToShow.Content is ProjectTreeViewModel)
			{
				var pane = new LayoutAnchorablePane(anchorableToShow)
				{
					DockWidth = new System.Windows.GridLength(300)
				};

				layout.RootPanel.Children.Insert(0, pane);
				return true;
			}

			var pixelPane = layout.Descendents().OfType<LayoutAnchorablePane>().FirstOrDefault(x => x.Name == "PixelPane");
			if (pixelPane != null && anchorableToShow.Content is PixelEditorViewModel)
			{
				pixelPane.Children.Add(anchorableToShow);
				return true;
			}
			else if (anchorableToShow.Content is PixelEditorViewModel)
			{
				var pane = new LayoutAnchorablePane(anchorableToShow)
				{
					DockWidth = new System.Windows.GridLength(400)
				};

				layout.RootPanel.Children.Add(pane);
				return true;
			}

			return false;
		}


		public void AfterInsertAnchorable(LayoutRoot layout, LayoutAnchorable anchorableShown)
		{
		}


		public bool BeforeInsertDocument(LayoutRoot layout, LayoutDocument anchorableToShow, ILayoutContainer destinationContainer)
		{
			return false;
		}

		public void AfterInsertDocument(LayoutRoot layout, LayoutDocument anchorableShown)
		{

		}
	}
}
