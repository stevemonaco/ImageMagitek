using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TileShop.WPF.ViewModels
{
    public static class TreeNodeViewModelExtensions
    {
        public static IEnumerable<TreeNodeViewModel> SelfAndDescendants(this TreeNodeViewModel node)
        {
            var nodeStack = new Stack<TreeNodeViewModel>();

            nodeStack.Push(node);

            while (nodeStack.Count > 0)
            {
                var popNode = nodeStack.Pop();
                yield return popNode;
                foreach (var child in popNode.Children)
                    nodeStack.Push(child);
            }
        }

        public static IEnumerable<TreeNodeViewModel> Ancestors(this TreeNodeViewModel node)
        {
            TreeNodeViewModel nodeVisitor = node.ParentModel;

            while (nodeVisitor != null)
            {
                yield return nodeVisitor;
                nodeVisitor = nodeVisitor.ParentModel;
            }
        }

        /// <summary>
        /// Enumerates all child nodes in post order
        /// </summary>
        /// <param name=""></param>
        /// <returns></returns>
        /// <remarks>Based upon https://www.geeksforgeeks.org/tree-traversals-inorder-preorder-and-postorder/ </remarks>
        public static IEnumerable<TreeNodeViewModel> PostOrderTraversal(this TreeNodeViewModel node)
        {
            if (node is null)
                yield break;

            var visited = new HashSet<TreeNodeViewModel>();

            var stack = new Stack<TreeNodeViewModel>();
            stack.Push(node);
            TreeNodeViewModel previous = null;

        }

        /// <summary>
        /// Traverses the tree and sorts the result so that the deepest nodes are at the front of the result and root at the back
        /// </summary>
        /// <param name="rootNode"></param>
        /// <returns></returns>
        public static IEnumerable<TreeNodeViewModel> BottomUpTraversal(this TreeNodeViewModel rootNode)
        {
            if (rootNode is null)
                return Enumerable.Empty<TreeNodeViewModel>();

            return rootNode.SelfAndDescendants()
                .OrderByDescending(x => x.Ancestors().Count());
        }
    }
}
