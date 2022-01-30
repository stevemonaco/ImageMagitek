using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dock.Model.Avalonia;
using Dock.Model.Controls;

namespace TileShop.AvaloniaUI.ViewExtenders.Docking;

public class DockFactory : Factory
{
    private IDocumentDock _documentDock;
    private readonly object _context;

    public DockFactory(object context)
    {
        _context = context;
    }

    //public override IRootDock CreateLayout()
    //{
    //    var documentDock = new IDocumentDock
    //    {
    //        Id = "DocumentsPane",
    //        Title = "Editors",
    //        Proportion = double.NaN,
    //        ActiveDockable = document1,

    //    }
    //}
}
