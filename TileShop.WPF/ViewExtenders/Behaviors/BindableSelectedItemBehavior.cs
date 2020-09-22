using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;
using System.Windows.Media;

namespace TileShop.WPF.Behaviors
{
    /// <summary>
    ///     Behavior that makes the <see cref="System.Windows.Controls.TreeView.SelectedItem" /> bindable.
    /// </summary>
    /// <remarks>
    ///     Taken and cleaned up from https://stackoverflow.com/questions/11065995/binding-selecteditem-in-a-hierarchicaldatatemplate-applied-wpf-treeview/18700099#18700099
    /// </remarks>
    public class BindableSelectedItemBehavior : Behavior<TreeView>
    {
        /// <summary>
        ///     Identifies the <see cref="SelectedItem" /> dependency property.
        /// </summary>
        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(
                nameof(SelectedItem),
                typeof(object),
                typeof(BindableSelectedItemBehavior),
                new UIPropertyMetadata(null, OnSelectedItemChanged));

        /// <summary>
        ///     Gets or sets the selected item of the <see cref="TreeView" /> that this behavior is attached
        ///     to.
        /// </summary>
        public object SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        /// <summary>
        ///     Called after the behavior is attached to an AssociatedObject.
        /// </summary>
        /// <remarks>
        ///     Override this to hook up functionality to the AssociatedObject.
        /// </remarks>
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.SelectedItemChanged += OnTreeViewSelectedItemChanged;
        }

        /// <summary>
        ///     Called when the behavior is being detached from its AssociatedObject, but before it has
        ///     actually occurred.
        /// </summary>
        /// <remarks>
        ///     Override this to unhook functionality from the AssociatedObject.
        /// </remarks>
        protected override void OnDetaching()
        {
            base.OnDetaching();
            if (AssociatedObject != null)
                AssociatedObject.SelectedItemChanged -= OnTreeViewSelectedItemChanged;
        }

        private static Action<int> GetBringIndexIntoView(Panel itemsHostPanel)
        {
            var virtualizingPanel = itemsHostPanel as VirtualizingStackPanel;
            if (virtualizingPanel == null)
                return null;

            var method = virtualizingPanel.GetType().GetMethod(
                "BringIndexIntoView",
                BindingFlags.Instance | BindingFlags.NonPublic,
                Type.DefaultBinder,
                new[] { typeof(int) },
                null);

            if (method == null)
                return null;

            return i => method.Invoke(virtualizingPanel, new object[] { i });
        }

        /// <summary>
        /// Recursively search for an item in this subtree.
        /// </summary>
        /// <param name="container">
        /// The parent ItemsControl. This can be a TreeView or a TreeViewItem.
        /// </param>
        /// <param name="item">
        /// The item to search for.
        /// </param>
        /// <returns>
        /// The TreeViewItem that contains the specified item.
        /// </returns>
        private static TreeViewItem GetTreeViewItem(ItemsControl container, object item)
        {
            if (container != null)
            {
                if (container.DataContext == item)
                    return container as TreeViewItem;

                // Expand the current container
                if (container is TreeViewItem && !((TreeViewItem)container).IsExpanded)
                    container.SetCurrentValue(TreeViewItem.IsExpandedProperty, true);

                // Try to generate the ItemsPresenter and the ItemsPanel.
                // by calling ApplyTemplate.  Note that in the 
                // virtualizing case even if the item is marked 
                // expanded we still need to do this step in order to 
                // regenerate the visuals because they may have been virtualized away.
                container.ApplyTemplate();
                var itemsPresenter =
                    (ItemsPresenter)container.Template.FindName("ItemsHost", container);
                
                if (itemsPresenter != null)
                {
                    itemsPresenter.ApplyTemplate();
                }
                else
                {
                    // The Tree template has not named the ItemsPresenter, 
                    // so walk the descendents and find the child.
                    itemsPresenter = container.GetVisualDescendant<ItemsPresenter>();
                    if (itemsPresenter == null)
                    {
                        container.UpdateLayout();
                        itemsPresenter = container.GetVisualDescendant<ItemsPresenter>();
                    }
                }

                var itemsHostPanel = (Panel)VisualTreeHelper.GetChild(itemsPresenter, 0);

                // Ensure that the generator for this panel has been created.
                var children = itemsHostPanel.Children;

                var bringIndexIntoView = GetBringIndexIntoView(itemsHostPanel);
                for (int i = 0, count = container.Items.Count; i < count; i++)
                {
                    TreeViewItem subContainer;
                    if (bringIndexIntoView != null)
                    {
                        // Bring the item into view so 
                        // that the container will be generated.
                        bringIndexIntoView(i);
                        subContainer =
                            (TreeViewItem)container.ItemContainerGenerator.
                                                    ContainerFromIndex(i);
                    }
                    else
                    {
                        subContainer =
                            (TreeViewItem)container.ItemContainerGenerator.
                                                    ContainerFromIndex(i);

                        // Bring the item into view to maintain the 
                        // same behavior as with a virtualizing panel.
                        subContainer.BringIntoView();
                    }

                    if (subContainer == null)
                    {
                        continue;
                    }

                    var initialExpanded = subContainer.IsExpanded;

                    // Search the next level for the object.
                    var resultContainer = GetTreeViewItem(subContainer, item);
                    if (resultContainer != null)
                        return resultContainer;

                    // The object is not under this TreeViewItem
                    // so collapse it.
                    subContainer.SetCurrentValue(TreeViewItem.IsExpandedProperty, initialExpanded);
                }
            }

            return null;
        }

        private static void OnSelectedItemChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var item = e.NewValue as TreeViewItem;
            if (item != null)
            {
                item.SetCurrentValue(TreeViewItem.IsSelectedProperty, true);
                return;
            }

            var behavior = (BindableSelectedItemBehavior)sender;
            var treeView = behavior.AssociatedObject;
            if (treeView == null)
            {
                // at designtime the AssociatedObject sometimes seems to be null
                return;
            }

            item = GetTreeViewItem(treeView, e.NewValue);
            if (item != null)
                item.SetCurrentValue(TreeViewItem.IsSelectedProperty, true);
        }

        private void OnTreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            SetCurrentValue(SelectedItemProperty, e.NewValue);
        }
    }
}
