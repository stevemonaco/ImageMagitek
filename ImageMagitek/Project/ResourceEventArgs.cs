using System;

namespace ImageMagitek.Project
{
    public class ResourceEventArgs : EventArgs
    {
        public string ResourceKey { get; private set; }

        public ResourceEventArgs(string key)
        {
            ResourceKey = key;
        }
    }
}
