using System;
using System.Collections.Generic;
using System.IO;

namespace ImageMagitek.Project
{
    interface IResourceManager
    {
        //void AddGraphicsFormat(GraphicsFormat format);
        //GraphicsFormat GetGraphicsFormat(string formatName);
        //bool LoadFormat(string fileName);
        //void RemoveGraphicsFormat(string formatName);
        //IEnumerable<GraphicsFormat> EnumerateFormats();

        bool AddResource(string resourceKey, ProjectResourceBase resource);
        T GetResource<T>(string resourceKey) where T : ProjectResourceBase;
        Type GetResourceType(string resourceKey);
        bool HasResource(string resourceKey);
        bool HasResource<T>(string resourceKey) where T : ProjectResourceBase;
        bool MoveResource(string oldResourceKey, string newResourceKey);
        bool RemoveResource(string resourceKey);
        bool RenameResource(string resourceKey, string newName);
        IEnumerable<ProjectResourceBase> EnumerateResources();

        bool LoadProject(string fileName, string baseDirectory);
        bool SaveProject(Stream stream);
    }
}
