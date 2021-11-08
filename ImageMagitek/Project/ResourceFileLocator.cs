using System.IO;

namespace ImageMagitek.Project;

public static class ResourceFileLocator
{
    public static string Locate(ProjectTree tree, ResourceNode node)
    {
        var baseDirectory = (tree.Root as ProjectNode).BaseDirectory;
        var pathKey = tree.CreatePathKey(node, Path.DirectorySeparatorChar.ToString()).TrimStart(Path.DirectorySeparatorChar);

        if (node.Item is ResourceFolder)
            return Path.Combine(baseDirectory, pathKey);
        else
            return Path.Combine(baseDirectory, $"{pathKey}.xml");
    }

    public static string LocateByParent(ProjectTree tree, ResourceNode parentNode, ResourceNode childNode)
    {
        var baseDirectory = (tree.Root as ProjectNode).BaseDirectory;
        var pathKey = tree.CreatePathKey(parentNode, Path.DirectorySeparatorChar.ToString()).TrimStart(Path.DirectorySeparatorChar);

        if (childNode is not ResourceFolderNode)
            return Path.Combine(baseDirectory, pathKey, $"{childNode.Name}.xml");
        else
            return Path.Combine(baseDirectory, pathKey, childNode.Name);
    }
}
