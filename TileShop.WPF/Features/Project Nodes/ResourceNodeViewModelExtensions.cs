using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TileShop.WPF.ViewModels
{
    public static class ResourceNodeViewModelExtensions
    {
        public static IEnumerable<ResourceNodeViewModel> SelfAndDescendants(this ResourceNodeViewModel node)
        {
            var nodeStack = new Stack<ResourceNodeViewModel>();

            nodeStack.Push(node);

            while (nodeStack.Count > 0)
            {
                var popNode = nodeStack.Pop();
                yield return popNode;
                foreach (var child in popNode.Children)
                    nodeStack.Push(child);
            }
        }

        public static IEnumerable<ResourceNodeViewModel> Ancestors(this ResourceNodeViewModel node)
        {
            ResourceNodeViewModel nodeVisitor = node.ParentModel;

            while (nodeVisitor != null)
            {
                yield return nodeVisitor;
                nodeVisitor = nodeVisitor.ParentModel;
            }
        }

        /// <summary>
        /// Traverses the tree and sorts the result so that the deepest nodes are at the front of the result and root at the back
        /// </summary>
        /// <param name="rootNode"></param>
        /// <returns></returns>
        public static IEnumerable<ResourceNodeViewModel> BottomUpTraversal(this ResourceNodeViewModel rootNode)
        {
            if (rootNode is null)
                return Enumerable.Empty<ResourceNodeViewModel>();

            return rootNode.SelfAndDescendants()
                .OrderByDescending(x => x.Ancestors().Count());
        }
    }
}
