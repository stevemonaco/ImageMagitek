using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using CommunityToolkit.Diagnostics;
using ImageMagitek.Colors;
using ImageMagitek.Utility;
using Monaco.PathTree;

namespace ImageMagitek.Project.Serialization;

public sealed class XmlProjectWriter : IProjectWriter
{
    private readonly List<IProjectResource> _globalResources;
    private readonly Palette _globalDefaultPalette;
    private readonly ProjectTree _tree;
    private readonly IColorFactory _colorFactory;
    private readonly string _resourceBaseDirectory;
    private readonly ResourceKindRegistry _kinds;
    private string _journalDirectory;

    public XmlProjectWriter(ProjectTree tree, IColorFactory colorFactory, IEnumerable<IProjectResource> globalResources, ResourceKindRegistry kinds)
    {
        Guard.IsNotNull(tree);
        Guard.IsNotNull(tree.Root.DiskLocation);

        _tree = tree;
        _kinds = kinds;
        _colorFactory = colorFactory;
        _globalResources = globalResources.ToList();
        _globalDefaultPalette = globalResources.OfType<Palette>().First();
        _journalDirectory = Path.GetDirectoryName(Path.GetFullPath(tree.Root.DiskLocation))!;
        _resourceBaseDirectory = tree.Root is ProjectNode projectNode ? Path.GetFullPath(string.IsNullOrEmpty(projectNode.BaseDirectory) ? "." : projectNode.BaseDirectory) : _journalDirectory;
    }

    /// <summary>
    /// Writes all modified project resources contained in a ProjectTree to disk and updates the persistence models
    /// while accounting for stale references
    /// </summary>
    /// <param name="projectFileName">Filename to write to</param>
    /// <param name="additionalFiles">Files outside the tree committed in the same transaction</param>
    public async Task<MagitekResult> WriteProjectAsync(string projectFileName, IReadOnlyList<(string Path, string Contents)>? additionalFiles = null,
        Dictionary<IProjectResource, string>? resourceMap = null)
    {
        if (string.IsNullOrWhiteSpace(projectFileName))
            throw new ArgumentException($"{nameof(WriteProjectAsync)} property '{nameof(projectFileName)}' was null or empty");

        _journalDirectory = Path.GetDirectoryName(Path.GetFullPath(projectFileName))!;

        var serializeResult = await TrySerializeProjectTreeAsync(_tree.EnumerateDepthFirst().Where(x => x is not ResourceFolderNode), additionalFiles, resourceMap).ConfigureAwait(false);
        return serializeResult.Match<MagitekResult>(
            success => MagitekResult.SuccessResult,
            failed => new MagitekResult.Failed(failed.Reasons.First()));
    }

    /// <inheritdoc/>
    public string SerializeResource(ResourceNode resourceNode)
    {
        var mapResult = MapCurrentModel(resourceNode);
        return mapResult.HasSucceeded ? SerializeModel(mapResult.AsSuccess.Result) : throw new InvalidOperationException(mapResult.AsError.Reason);
    }

    /// <summary>
    /// Writes a single resource, updating its model, but does not update stale resources in the tree
    /// </summary>
    public async Task<MagitekResult> WriteResourceAsync(ResourceNode resourceNode, bool alwaysOverwrite)
    {
        if (resourceNode.DiskLocation is null)
            return new MagitekResult.Failed($"Resource '{resourceNode.Name}' cannot be saved because '{nameof(resourceNode.DiskLocation)}' is null");

        var mapResult = MapCurrentModel(resourceNode);
        if (mapResult.HasFailed)
            return new MagitekResult.Failed(mapResult.AsError.Reason);

        var currentModel = mapResult.AsSuccess.Result;

        if (currentModel != resourceNode.Model || alwaysOverwrite)
        {
            var actions = new[] { (resourceNode.DiskLocation, SerializeModel(currentModel), resourceNode, currentModel) };

            var transactionResult = await RunTransactionsAsync(actions).ConfigureAwait(false);
            return transactionResult.Match<MagitekResult>(
                success => MagitekResult.SuccessResult,
                failed => new MagitekResult.Failed(failed.Reasons.First()));
        }

        return MagitekResult.SuccessResult;
    }

    /// <inheritdoc/>
    public async Task<MagitekResult> WriteModelsAsync(IReadOnlyList<(ResourceNode Node, ResourceModel Model)> writes)
    {
        foreach (var (node, _) in writes)
        {
            if (node.DiskLocation is null)
                return new MagitekResult.Failed($"Resource '{node.Name}' cannot be saved because '{nameof(node.DiskLocation)}' is null");
        }

        var actions = writes.Select(x => (x.Node.DiskLocation!, SerializeModel(x.Model), x.Node, x.Model));
        var transactionResult = await RunTransactionsAsync(actions).ConfigureAwait(false);
        return transactionResult.Match<MagitekResult>(
            success => MagitekResult.SuccessResult,
            failed => new MagitekResult.Failed(failed.Reasons.First()));
    }

    /// <inheritdoc/>
    public async Task<MagitekResult> WriteResourcesAsync(IReadOnlyList<ResourceNode> nodes, IReadOnlyList<(string Path, string Contents)>? additionalFiles = null,
        Dictionary<IProjectResource, string>? resourceMap = null)
    {
        var serializeResult = await TrySerializeProjectTreeAsync(nodes, additionalFiles, resourceMap).ConfigureAwait(false);
        return serializeResult.Match<MagitekResult>(
            success => MagitekResult.SuccessResult,
            failed => new MagitekResult.Failed(failed.Reasons.First()));
    }

    private async Task<MagitekResults> TrySerializeProjectTreeAsync(IEnumerable<ResourceNode> nodes, IReadOnlyList<(string Path, string Contents)>? additionalFiles,
        Dictionary<IProjectResource, string>? resourceMap)
    {
        var actions = new List<(string targetPath, string contents, ResourceNode node, ResourceModel model)>();

        foreach (var node in nodes)
        {
            var mapResult = MapCurrentModel(node, resourceMap);
            if (mapResult.HasFailed)
                return new MagitekResults.Failed([mapResult.AsError.Reason]);

            var currentModel = mapResult.AsSuccess.Result;
            var location = ResourceFileLocator.Locate(_tree, node);

            if (currentModel != node.Model || node.DiskLocation != location)
                actions.Add((location, SerializeModel(currentModel), node, currentModel));
        }

        return await RunTransactionsAsync(actions, additionalFiles).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public MagitekResult<ResourceModel> MapCurrentModel(ResourceNode node, Dictionary<IProjectResource, string>? resourceMap = null)
    {
        resourceMap ??= _tree.CreateResourceMap(_globalResources);

        if (node is ProjectNode { Item: ImageProject project })
            return new MagitekResult<ResourceModel>.Success(project.MapToModel(ProjectFormat.Required(_tree, _kinds)));

        var kind = _kinds.FindByNode(node);
        if (kind?.MapToModel(node, resourceMap, _colorFactory) is not { } model)
            return new MagitekResult<ResourceModel>.Failed($"Cannot write resource of type '{node.GetType()}'");

        if (kind.FindUnwritableReason(model, _colorFactory) is string reason)
            return new MagitekResult<ResourceModel>.Failed(reason);

        return new MagitekResult<ResourceModel>.Success(model);
    }

    private async Task<MagitekResults> RunTransactionsAsync(IEnumerable<(string targetPath, string contents, ResourceNode node, ResourceModel model)> actions,
        IReadOnlyList<(string Path, string Contents)>? additionalFiles = null)
    {
        var actionList = actions.ToList();
        additionalFiles ??= [];

        if (actionList.Count == 0 && additionalFiles.Count == 0)
            return MagitekResults.SuccessResults;

        var transaction = new WriteAheadLogTransaction(_journalDirectory);

        foreach (var action in actionList)
        {
            transaction.AddWriteFile(action.targetPath, action.contents);
        }

        foreach (var (path, contents) in additionalFiles)
            transaction.AddWriteFile(path, contents);

        var result = await transaction.ExecuteAsync().ConfigureAwait(false);

        if (result.HasSucceeded)
        {
            foreach (var action in actionList)
            {
                action.node.Model = action.model;
                action.node.DiskLocation = action.targetPath;
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public string SerializeModel(ResourceModel model) => Stringify(ToElement(model));

    private XElement ToElement(ResourceModel model)
    {
        if (model is ImageProjectModel projectModel)
            return Serialize(projectModel);

        return _kinds.FindByModel(model)?.Write(model, _resourceBaseDirectory, _colorFactory)
            ?? throw new InvalidOperationException($"{nameof(WriteProjectAsync)}: unexpected resource model of type '{model.GetType()}'");
    }

    private static XElement Serialize(ImageProjectModel projectModel)
    {
        var element = new XElement("project");
        element.Add(new XAttribute("version", projectModel.Version.ToString(2)));

        if (!string.IsNullOrEmpty(projectModel.Root))
            element.Add(new XAttribute("root", projectModel.Root));

        return element;
    }

    private string Stringify(XElement resourceElement)
    {
        var xws = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "\t",
        };

        using var writer = new Utf8StringWriter();
        using var xw = XmlWriter.Create(writer, xws);

        resourceElement.Save(xw);
        xw.Flush();

        return writer.ToString();
    }
}
