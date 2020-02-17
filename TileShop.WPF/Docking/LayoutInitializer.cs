using System.Linq;
using TileShop.WPF.ViewModels;
using Xceed.Wpf.AvalonDock.Layout;

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

			var pixelPane = layout.Descendents().OfType<LayoutAnchorablePane>().FirstOrDefault(x => x.Name == "PixelPane");
			if (projectPane != null && anchorableToShow.Content is PixelEditorViewModel)
			{
				pixelPane.Children.Add(anchorableToShow);
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
