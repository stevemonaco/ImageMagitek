using System.Collections.Generic;

namespace TileShop.UI.ViewModels;

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
        var nodeVisitor = node.ParentModel;

        while (nodeVisitor != null)
        {
            yield return nodeVisitor;
            nodeVisitor = nodeVisitor.ParentModel;
        }
    }
}
