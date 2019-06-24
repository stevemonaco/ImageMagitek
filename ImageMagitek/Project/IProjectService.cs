using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Project
{
    public interface IProjectService
    {
        T GetResource<T>(string resourceKey);
        bool HasResource(string resourceKey);
        bool HasResource<T>(string resourceKey);
        bool MoveResource(string oldResourceKey, string newResourceKey);
        bool RemoveResource(string resourceKey);
        bool RenameResource(string resourceKey, string newName);
        T NewResource<T>(string resourceKey);
        void SaveResource<T>(string resourceKey);
        void ReloadResource(string resourceKey);
    }
}
