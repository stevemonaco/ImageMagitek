using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TileShop.CLI.Commands
{
    public enum ExitCode { Success = 0, Unset = -1, Exception = -2, InvalidCommandArguments = -3, ProjectOpenError = -4 }
}
