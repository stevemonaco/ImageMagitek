using System.IO;

namespace ImageMagitek.Project;

/// <summary>
/// Class to locate (build) full paths to XML resource files on disk associated with a resource node
/// </summary>
public static class ResourceFileLocator
{
    /// <summary>
    /// Locate the path using a node that is attached to the tree.
    /// </summary>
    /// <returns>The full disk path of where the resource is or should be located</returns>
    public static string Locate(ProjectTree tree, ResourceNode node)
    {
        var root = (ProjectNode)tree.Root;
        var baseDirectory = root.BaseDirectory;
        var pathKey = tree.CreatePathKey(node, Path.DirectorySeparatorChar.ToString()).TrimStart(Path.DirectorySeparatorChar);

        if (node is ProjectNode) // Workaround for root node
            return Path.Combine(baseDirectory, $"{node.Name}.xml");
        else if (node is ResourceFolderNode) // Folders on disk do not have an extension
            return Path.Combine(baseDirectory, pathKey);
        else
            return Path.Combine(baseDirectory, $"{pathKey}.xml");
    }

    /// <summary>
    /// Locate the path using a parent node that is attached to the tree.
    /// Useful for scenarios where the child is not yet attached.
    /// </summary>
    /// <param name="tree"></param>
    /// <param name="parentNode"></param>
    /// <param name="childNode"></param>
    /// <returns>The full disk path of where the resource is or should be located</returns>
    public static string LocateByParent(ProjectTree tree, ResourceNode parentNode, ResourceNode childNode)
    {
        var root = (ProjectNode)tree.Root;
        var baseDirectory = root.BaseDirectory;
        var pathKey = tree.CreatePathKey(parentNode, Path.DirectorySeparatorChar.ToString()).TrimStart(Path.DirectorySeparatorChar);

        if (childNode is not ResourceFolderNode)
            return Path.Combine(baseDirectory, pathKey, $"{childNode.Name}.xml");
        else
            return Path.Combine(baseDirectory, pathKey, childNode.Name);
    }
}
