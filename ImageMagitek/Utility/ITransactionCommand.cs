using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageMagitek.Utility
{
    public interface ITransactionCommand
    {
        bool Prepare();
        bool Execute();
        bool Rollback();
        bool Complete();
    }
}
