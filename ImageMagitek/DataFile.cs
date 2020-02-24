using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagitek.Project;

namespace ImageMagitek
{
    /// <summary>
    /// DataFile manages access to user-modifiable files
    /// </summary>
    public class DataFile: IProjectResource
    {
        public string Location { get; private set; }

        public Stream Stream { get => _stream.Value; }
        public string Name { get; set; }

        public bool CanContainChildResources => true;

        public bool ShouldBeSerialized { get; set; } = true;

        Lazy<Stream> _stream;

        public DataFile(string name): this(name, "") { }

        public DataFile(string name, string location)
        {
            Name = name;
            Location = location;

            _stream = new Lazy<Stream>(() =>
            {
                if (string.IsNullOrWhiteSpace(Location))
                    throw new ArgumentException($"{nameof(DataFile)} parameter {nameof(Location)} was null or empty");

                return File.Open(Location, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            });
        }

        public void Close()
        {
            if (Stream != null)
                Stream.Close();
        }

        public IEnumerable<IProjectResource> LinkedResources() => Enumerable.Empty<IProjectResource>();
    }
}
