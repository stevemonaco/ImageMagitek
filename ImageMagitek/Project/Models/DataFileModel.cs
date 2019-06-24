using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Project.Models
{
    internal class DataFileModel
    {
        public DataFileModel(string location)
        {
            Location = location;
        }

        public string Location { get; }
    }
}
