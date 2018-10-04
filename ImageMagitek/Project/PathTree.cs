using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageMagitek
{
    class PathTree<T>
    {
        PathTreeNode<T> Root = new PathTreeNode<T>();

        /// <summary>
        /// Adds the item to the specified path if the parent exists
        /// </summary>
        /// <param name="path">The path associated with the item</param>
        /// <param name="item">The item</param>
        public void Add(string path, T item)
        {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentException();

            string parentPath = Path.GetDirectoryName(path);
            string itemName = Path.GetFileName(path);

            if (String.IsNullOrWhiteSpace(parentPath)) // Add to root
            {
                Root.Children.Add(itemName, new PathTreeNode<T>(item));
            }
            else // Add to Parent Resource
            {
                PathTreeNode<T> parent;
                if (!Root.TryGetChild(parentPath, out parent))
                    throw new KeyNotFoundException($"{nameof(TryGetItem)} could not locate parent path {parentPath}");

                if (parent.Children.ContainsKey(itemName))
                    throw new ArgumentException($"{path} already exists");
                parent.Children.Add(itemName, new PathTreeNode<T>(item));
            }
        }

        public bool TryGetItem(string itemPath, out T item)
        {
            if (String.IsNullOrWhiteSpace(itemPath))
                throw new ArgumentException();

            var paths = itemPath.Split('\\');
            var nodeVisitor = Root.Children;
            var node = new PathTreeNode<T>();

            foreach(var path in paths)
            {
                if(nodeVisitor.TryGetValue(path, out node))
                {
                    nodeVisitor = node.Children;
                }
                else
                {
                    item = default(T);
                    return false;
                }
            }
            if (Root.TryGetChild(itemPath, out node))
            {
                item = node.Item;
                return true;
            }

            item = default(T);
            return false;
        }

        private class PathTreeNode<U>
        {
            public Dictionary<string, PathTreeNode<U>> Children { get; private set; }
            public PathTreeNode<U> Parent { get; set; }
            public U Item { get; set; }

            public PathTreeNode()
            {
            }

            public PathTreeNode(U item) => Item = item;

            public bool TryGetChild(string name, out PathTreeNode<U> value)
            {
                if (Children.TryGetValue(name, out value))
                    return true;

                return false;
            }
        }
    }


}
