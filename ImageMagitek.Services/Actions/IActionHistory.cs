using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageMagitek.Services.Actions;

public interface IActionHistory
{
    void Undo();
    void Redo();
}
