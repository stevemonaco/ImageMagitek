using TileShop.WPF.ViewModels;

namespace TileShop.WPF.EventModels
{
    public class RequestRemoveTreeNodeEvent
    {
        public TreeNodeViewModel TreeNode { get; }

        public RequestRemoveTreeNodeEvent(TreeNodeViewModel treeNode)
        {
            TreeNode = treeNode;
        }
    }
}
