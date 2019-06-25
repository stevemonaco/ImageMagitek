using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml.Linq;
using ImageMagitek.Project;

namespace ImageMagitek
{
    /// <summary>
    /// DataFile manages access to user-modifiable files
    /// </summary>
    public class DataFile: ProjectResourceBase
    {
        public string Location { get; private set; }

        public FileStream Stream { get => _stream.Value; }
        Lazy<FileStream> _stream;

        public DataFile(string name): this(name, "")
        {
        }

        public DataFile(string name, string location)
        {
            Name = name;
            Location = location;

            _stream = new Lazy<FileStream>(() =>
            {
                if (string.IsNullOrWhiteSpace(Location))
                    throw new ArgumentException($"{nameof(DataFile)} parameter {nameof(Location)} was null or empty");

                return File.Open(Location, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            });
        }

        /// <summary>
        /// Renames a DataFile to a new name
        /// </summary>
        /// <param name="name"></param>
        public override void Rename(string name)
        {
            Name = name;
        }

        public override ProjectResourceBase Clone()
        {
            DataFile df = new DataFile(Name, Location);
            return df;
        }

        public void Close()
        {
            if (Stream != null)
                Stream.Close();
        }
    }
}
