using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using TileShop.UI.ViewModels;

namespace TileShop.UI.ViewExtenders.Docking;

public class DockFactory : Factory
{
    private IDocumentDock? _documentDock;
    private IRootDock? _rootDock;
    private ProportionalDock? _mainLayout;

    private readonly ProjectTreeViewModel _projectTreeVm;
    private readonly EditorsViewModel _editorsVm;

    public DockFactory(ProjectTreeViewModel projectTreeVm, EditorsViewModel editorsViewModel)
    {
        _projectTreeVm = projectTreeVm;
        _editorsVm = editorsViewModel;
    }

    public override IRootDock CreateLayout()
    {
        var treeDockVm = new DockableToolViewModel(_projectTreeVm)
        {
            Id = "Project Tree",
            Title = "Project Tree",
            CanClose = false,
            CanPin = true,
            CanFloat = false
        };

        _documentDock = new DocumentDock
        {
            Id = "Documents",
            Title = "Documents",
            Proportion = double.NaN,
            IsCollapsable = false,
            CanClose = false,
            VisibleDockables = CreateList(_editorsVm.Editors.Select(x => new DockableEditorViewModel(x, _editorsVm) as IDockable).ToArray()),
            CanCreateDocument = false
        };

        _mainLayout = new ProportionalDock
        {
            Id = "MainLayout",
            Title = "MainLayout",
            Proportion = double.NaN,
            Orientation = Orientation.Horizontal,
            ActiveDockable = null,
            VisibleDockables = CreateList<IDockable>
            (
                new ToolDock
                {
                    Id = "TreePane",
                    Title = "TreePane",
                    CanFloat = false,
                    CanPin = true,
                    CanClose = false,
                    Proportion = double.NaN,
                    ActiveDockable = treeDockVm,
                    VisibleDockables = CreateList<IDockable>
                    (
                        treeDockVm
                    ),
                    Alignment = Alignment.Left,
                    GripMode = GripMode.Visible
                },
                new ProportionalDockSplitter()
                {
                    Id = "MainSplitter",
                    Title = "MainSplitter"
                },
                _documentDock
            )
        };

        _rootDock = CreateRootDock();

        _rootDock.Id = "Root";
        _rootDock.Title = "Root";
        _rootDock.IsCollapsable = false;
        _rootDock.ActiveDockable = _mainLayout;
        _rootDock.DefaultDockable = _mainLayout;
        _rootDock.VisibleDockables = CreateList<IDockable>(_mainLayout);

        _editorsVm.Editors.CollectionChanged += Editors_CollectionChanged;

        return _rootDock;
    }

    private void Editors_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems![0] is ResourceEditorBaseViewModel newItem && _documentDock is not null)
        {
            var newDoc = new DockableEditorViewModel(newItem, _editorsVm)
            {
                Id = newItem.DisplayName,
                Title = newItem.DisplayName,
            };

            AddDockable(_documentDock, newDoc);
            SetActiveDockable(newDoc);
            SetFocusedDockable(_documentDock, newDoc);
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems![0] is ResourceEditorBaseViewModel oldItem && _documentDock is not null)
        {
            var removeItem = _documentDock.VisibleDockables?.OfType<DockableEditorViewModel>().FirstOrDefault(x => x.Editor == oldItem);
            
            if (removeItem is not null)
                _documentDock.VisibleDockables?.Remove(removeItem);
        }
    }

    public override void InitLayout(IDockable layout)
    {
        ContextLocator = new Dictionary<string, Func<object?>>
        {
            //[nameof(IRootDock)] = () => _context,
            //[nameof(IProportionalDock)] = () => _context,
            //[nameof(IDocumentDock)] = () => _context,
            //[nameof(IToolDock)] = () => _context,
            //[nameof(IProportionalDockSplitter)] = () => _context,
            //[nameof(IDockWindow)] = () => _context,
            //[nameof(IDocument)] = () => _context,
            //[nameof(ITool)] = () => _context,
            ["TreePane"] = () => _projectTreeVm,
            //["MainSplitter"] = () => _context,
            //["MainLayout"] = () => _context,
        };

        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => new HostWindow()
        };

        DockableLocator = new Dictionary<string, Func<IDockable?>>
        {
            ["Root"] = () => _rootDock,
            ["Documents"] = () => _documentDock,
        };

        base.InitLayout(layout);

        //SetActiveDockable(_documentDock);
        //SetFocusedDockable(_documentDock, _documentDock.VisibleDockables?.FirstOrDefault());
    }
}
