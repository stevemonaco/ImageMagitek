using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Project.Models
{
    internal class DataFileModel
    {
        public string Name { get; set; }
        public string Location { get; set; }

        public DataFile ToDataFile() => new DataFile(Name, Location);
    }
}
