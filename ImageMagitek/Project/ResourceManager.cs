using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using MoreLinq;
using ImageMagitek;
using ImageMagitek.ExtensionMethods;
using ImageMagitek.Codec;

namespace ImageMagitek.Project
{
    /// <summary>
    /// Singleton class that manages file and editor resources
    /// A lease model is used for managing resources that are currently being edited
    /// A leased Resource is a resource that is intended to be edited and saved
    /// GetResource will return the edited copy of the Resource if is leased or the original copy if it is not
    /// This is so that Resources will be able to be previewed with unsaved changes in referenced Resources
    /// </summary>
    public class ResourceManager : IResourceManager
    {
        private Dictionary<string, ProjectResourceBase> ResourceTree = new Dictionary<string, ProjectResourceBase>();
        private ResourceFolder root;

        /// <summary>
        /// Events to notify UI components when resources have been added or renamed
        /// </summary>
        public EventHandler<ResourceEventArgs> ResourceAdded;
        public EventHandler<ResourceEventArgs> ResourceRenamed;

        /// <summary>
        /// List of graphics format codecs
        /// </summary>
        //private IDictionary<string, GraphicsFormat> Formats;

        private ICodecFactory CodecFactory;

        /// <summary>
        /// FileTypeLoader which is used to match file extensions with the default graphics codec upon opening for sequential arranger
        /// </summary>
        private FileTypeLoader Loader = new FileTypeLoader();

        /*#region Lazy Singleton implementation
        private static readonly Lazy<ResourceManager> lazySingleton = new Lazy<ResourceManager>(() => new ResourceManager());

        public static ResourceManager Instance { get { return lazySingleton.Value; } }

        private ResourceManager()
        {
        }
        #endregion*/

        public ResourceManager(ICodecFactory codecFactory)
        {
            CodecFactory = codecFactory;
        }

        #region ProjectResourceBase Management
        /// <summary>
        /// Add a resource to ResourceManager by key
        /// </summary>
        /// <param name="resource"></param>
        /// <returns></returns>
        public bool AddResource(string resourceKey, ProjectResourceBase resource)
        {
            if (resource is null)
                throw new ArgumentNullException($"{nameof(AddResource)} parameter '{nameof(resource)}' was null");
            if (resource.Name is null)
                throw new ArgumentException($"{nameof(AddResource)} parameter '{nameof(resource)}' contained a null '{nameof(resource.Name)}' property");
            if (resourceKey is null)
                throw new ArgumentNullException($"{nameof(AddResource)} parameter '{nameof(resourceKey)}' was null");

            if (ResourceTree.ContainsResource(resourceKey))
                return false;

            ResourceTree.AddResource(resourceKey, resource);
            ResourceAdded?.Invoke(this, new ResourceEventArgs(resourceKey));
            return true;
        }

        /// <summary>
        /// Remove a resource from ResourceManager by key
        /// </summary>
        /// <param name="resourceKey">Name of the resource to be removed</param>
        /// <returns>True if removed or no key exists, false if the resource is leased</returns>
        public bool RemoveResource(string resourceKey)
        {
            if (resourceKey is null)
                throw new ArgumentException($"{nameof(RemoveResource)} parameter '{nameof(resourceKey)}' was null");

            if (ResourceTree.ContainsResource(resourceKey))
                ResourceTree.Remove(resourceKey);

            return true;
        }

        /// <summary>
        /// Gets a resource by key
        /// </summary>
        /// <param name="resourceKey"></param>
        /// <returns>A leased resource if available, otherwise the original resource</returns>
        /*public ProjectResourceBase GetResource(string resourceKey)
        {
            if (resourceKey is null)
                throw new ArgumentException("Null name argument passed into GetResource");
            if (LeasedResourceMap.ContainsKey(resourceKey))
                return LeasedResourceMap[resourceKey];

            ProjectResourceBase res;

            if (ResourceTree.TryGetResource(resourceKey, out res))
                return res;

            throw new KeyNotFoundException($"Key '{resourceKey}' not found in ResourceManager");

            //if (ResourceMap.ContainsKey(ResourceKey))
            //    return ResourceMap[ResourceKey];

            //throw new KeyNotFoundException($"Key '{ResourceKey}' not found in ResourceManager");
        }*/

        /// <summary>
        /// Gets a resource by key
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="resourceKey">The resource key.</param>
        /// <returns>A leased resource if available, otherwise the original resource</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        public T GetResource<T>(string resourceKey) where T : ProjectResourceBase
        {
            if (resourceKey is null)
                throw new ArgumentException($"{nameof(GetResource)} parameter '{nameof(resourceKey)}' was null");

            if (ResourceTree.TryGetResource(resourceKey, out var res))
            {
                if (res is T tRes)
                    return tRes;
            }

            throw new KeyNotFoundException($"{nameof(GetResource)} key '{resourceKey}' of requested type not found");
        }

        /// <summary>
        /// Gets the type of a resource by key.
        /// </summary>
        /// <param name="resourceKey">The resource key.</param>
        /// <returns>The resource Type</returns>
        /// <exception cref="ArgumentException">GetResourceType</exception>
        /// <exception cref="KeyNotFoundException"></exception>
        public Type GetResourceType(string resourceKey)
        {
            if (resourceKey is null)
                throw new ArgumentException($"{nameof(GetResourceType)} parameter '{nameof(resourceKey)}' was null");

            if (ResourceTree.TryGetResource(resourceKey, out var res))
                return res.GetType();

            throw new KeyNotFoundException($"{nameof(GetResourceType)} key '{resourceKey}' of requested type not found");
        }

        /// <summary>
        /// Determines if the ResourceManager has a resource associated with the specified key
        /// </summary>
        /// <param name="resourceKey"></param>
        /// <returns>True if the Resource exists</returns>
        public bool HasResource(string resourceKey)
        {
            if (resourceKey is null)
                throw new ArgumentException($"{nameof(HasResource)} parameter '{nameof(resourceKey)}' was null");

            return ResourceTree.ContainsResource(resourceKey);
        }

        /// <summary>
        /// Determines if the ResourceManager has a resource of a specific type associated with the specified key
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="resourceKey">The resource key.</param>
        /// <returns>True if the Resource exists and is of the specified type</returns>
        /// <exception cref="ArgumentException">HasResource</exception>
        public bool HasResource<T>(string resourceKey) where T : ProjectResourceBase
        {
            if (resourceKey is null)
                throw new ArgumentException($"{nameof(HasResource)} parameter '{nameof(resourceKey)}' was null");

            if(ResourceTree.TryGetResource(resourceKey, out var res))
            {
                if (res is T)
                    return true;
            }

            return false;
        }

        public bool MoveResource(string oldResourceKey, string newResourceKey)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Renames the resource
        /// </summary>
        /// <param name="resourceKey">The resource key.</param>
        /// <param name="newName">The new name.</param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public bool RenameResource(string resourceKey, string newName)
        {
            var res = GetResource<ProjectResourceBase>(resourceKey);
            res.Rename(newName);
            return true;
        }

        public IEnumerable<ProjectResourceBase> EnumerateResources() => ResourceTree.SelfAndDescendants();

        /// <summary>
        /// Clears all project resources
        /// Does not remove graphic formats, cursors, or file loaders
        /// </summary>
        /// <returns></returns>
        public void ClearResources()
        {
            ResourceTree.SelfAndDescendants().OfType<DataFile>().ForEach((x) =>
            {
                x.Close();
            });

            ResourceTree.Clear();
        }

        public IEnumerable<string> ResourceKeys { get => ResourceTree.SelfAndDescendants().Select(x => x.ResourceKey); }
        #endregion

        #region XML Management
        public bool LoadProject(string fileName, string baseDirectory)
        {
            var serializer = new XmlGameDescriptorDeserializer();
            var tree = serializer.DeserializeProject(fileName, baseDirectory);

            // Add root-level nodes to the ResourceTree
            foreach (var item in tree)
                ResourceTree.AddResource(item.Key, item.Value);

            foreach(var arranger in tree.SelfAndDescendants().OfType<ScatteredArranger>())
            {
                foreach(var el in arranger.EnumerateElements())
                {
                    el.Codec = CodecFactory.GetCodec(el.FormatName);
                    if (el.Codec is GenericGraphicsCodec ggc)
                        el.InitializeGraphicsFormat(ggc.Format);
                }
            }

            root = new ResourceFolder();
            root.Rename("");
            root.Parent = null;

            foreach(var resourcePair in ResourceTree)
            {
                root.ChildResources.AddResource(resourcePair.Key, resourcePair.Value);
                resourcePair.Value.Parent = root;
            }

            // Resolve resources
            foreach(var item in ResourceTree.SelfAndDescendants())
            {
                DataFile dataFile;
                Palette palette;

                switch(item)
                {
                    case Palette pal:
                        if (ResourceTree.TryGetResource<DataFile>(pal.DataFileKey, out  dataFile))
                            pal.DataFile = dataFile;
                        else
                            throw new KeyNotFoundException($"{nameof(ResourceTreeExtensions.TryGetResource)} failed to find the DataFile associated with the key {pal.DataFileKey} referenced by the Palette {item.ResourceKey}");
                        break;
                    case Arranger arr:
                        foreach(var el in arr.EnumerateElements())
                        {
                            if (ResourceTree.TryGetResource<DataFile>(el.DataFileKey, out dataFile))
                                el.DataFile = dataFile;
                            else
                                throw new KeyNotFoundException($"{nameof(ResourceTreeExtensions.TryGetResource)} failed to find the DataFile associated with the key {el.DataFileKey} referenced by the Arranger {item.ResourceKey}");

                            if (ResourceTree.TryGetResource<Palette>(el.PaletteKey, out palette))
                                el.Palette = palette;
                            else
                                throw new KeyNotFoundException($"{nameof(ResourceTreeExtensions.TryGetResource)} failed to find the Palette associated with the key {el.PaletteKey} referenced by the Arranger {item.ResourceKey}");
                        }
                        break;
                    default:
                        break;
                }
            }

            // Invoke ResourceAdded event for every deserialized node
            foreach (var item in tree.SelfAndDescendants())
                ResourceAdded?.Invoke(this, new ResourceEventArgs(item.ResourceKey));

            return true;
        }

        public bool SaveProject(Stream stream)
        {
            //GameDescriptorSerializer.SerializeProject(ResourceTree, stream);
            return true;
        }

        #endregion

        #region GraphicsFormat Management
        /*public void AddGraphicsFormat(GraphicsFormat format)
        {
            Formats.Add(format.Name, format);
        }

        public bool LoadFormat(string filename)
        {
            GraphicsFormat fmt = new GraphicsFormat();
            if (!fmt.LoadFromXml(filename))
                return false;

            AddGraphicsFormat(fmt);
            return true;
        }

        public GraphicsFormat GetGraphicsFormat(string formatName)
        {
            if (Formats.ContainsKey(formatName))
                return Formats[formatName];
            else
                throw new KeyNotFoundException();
        }

        public void RemoveGraphicsFormat(string formatName)
        {
            if(Formats.ContainsKey(formatName))
                Formats.Remove(formatName);
        }

        public IEnumerable<GraphicsFormat> EnumerateFormats() => Formats.Values;

        public IEnumerable<string> GetGraphicsFormatsNameList()
        {
            var keyList = Formats.Keys.ToList();
            keyList.Sort();

            return keyList;
        }*/
        #endregion

        #region DataFile Management

        /// <summary>
        /// Renames a file that is currently loaded into the FileManager, renames it on disk, and remaps all references to it in the project
        /// </summary>
        /// <param name="fileName">FileManager file to be renamed</param>
        /// <param name="newFileName">Name that the file will be renamed to</param>
        /// <returns>Success state</returns>
        public bool RenameFile(string fileName, string newFileName)
        {
            /*if (String.IsNullOrWhiteSpace(FileName) || String.IsNullOrWhiteSpace(NewFileName))
                throw new ArgumentException("");

            // Must contain FileName and must not contain NewFileName
            if (HasDataFile(NewFileName) || !HasDataFile(FileName) || File.Exists(NewFileName))
                return false;

            // File must not already exist
            if (File.Exists(NewFileName))
                return false;

            DataFile df = GetResource(FileName) as DataFile;
            string name = df.Stream.Name;
            df.Stream.Close();

            DataFileList.Remove(FileName);

            File.Move(name, NewFileName);
            df.Open(NewFileName);
            DataFileList.Add(NewFileName, df);

            // Rename references
            foreach (Arranger arr in ArrangerList.Values)
            {
                foreach (ArrangerElement el in arr.ElementGrid)
                {
                    if (el.DataFileKey == FileName)
                        el.DataFileKey = NewFileName;
                }
            }

            foreach (Arranger arr in ArrangerList.Values)
            {
                foreach (ArrangerElement el in arr.ElementGrid)
                {
                    if (el.DataFileKey == FileName)
                        el.DataFileKey = NewFileName;
                }
            }

            foreach (Palette pal in PaletteList.Values)
            {
                if (pal.DataFileKey == FileName)
                    pal.SetFileKey(NewFileName);
            }*/

            return true;
        }

        #endregion

        #region Palette Management
        /// <summary>
        /// Loads and associates a palette within FileManager
        /// </summary>
        /// <param name="filename">Filename to palette to be loaded</param>
        /// <param name="paletteName">Name associated to the palette within FileManager </param>
        /// <returns></returns>
        public bool LoadPalette(string filename, string paletteName)
        {
            if (filename is null || paletteName is null)
                throw new ArgumentNullException();

            Palette pal = new Palette(paletteName);
            if (!pal.LoadPalette(filename))
                return false;

            pal.ShouldBeSerialized = false;

            AddResource(pal.Name, pal);

            return true;
        }

        /// <summary>
        /// Renames an palette that is currently loaded into the FileManager and remaps all references to it in the project
        /// </summary>
        /// <param name="paletteName">Palette to be renamed</param>
        /// <param name="newPaletteName">Name that the palette will be renamed to</param>
        /// <returns></returns>
        public bool RenamePalette(string paletteName, string newPaletteName)
        {
            /*if (!HasPalette(PaletteName) || HasPalette(NewPaletteName))
                return false;

            Palette pal = GetResource(PaletteName) as Palette;
            pal.SetFileKey(NewPaletteName);
            PaletteList.Remove(NewPaletteName);
            PaletteList.Add(NewPaletteName, pal);

            // Rename references
            foreach (Arranger arr in ArrangerList.Values)
            {
                foreach (ArrangerElement el in arr.ElementGrid)
                {
                    if (el.PaletteKey == PaletteName)
                        el.PaletteKey = NewPaletteName;
                }
            }*/

            return true;
        }
        #endregion

        #region Traversal

        public IEnumerable<ProjectResourceBase> TraverseDepthFirst()
        {
            return ResourceTree.SelfAndDescendants();
        }

        #endregion
    }
}
