using TileShop.WPF.ViewModels;

namespace TileShop.WPF.EventModels
{
    public class RequestRemoveTreeNodeEvent
    {
        public ResourceNodeViewModel TreeNode { get; }

        public RequestRemoveTreeNodeEvent(ResourceNodeViewModel treeNode)
        {
            TreeNode = treeNode;
        }
    }
}
