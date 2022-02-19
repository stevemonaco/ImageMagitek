using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Data;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using TileShop.AvaloniaUI.ViewExtenders.Dock;
using TileShop.AvaloniaUI.ViewModels;

namespace TileShop.AvaloniaUI.ViewExtenders.Docking;

public class DockFactory : Factory
{
    private IDocumentDock _documentDock;
    private IRootDock _rootDock;

    private readonly object _context;
    private readonly ProjectTreeViewModel _projectTreeVm;
    private readonly EditorsViewModel _editorsVm;

    public DockFactory(ProjectTreeViewModel projectTreeVm, EditorsViewModel editorsViewModel)
    {
        _projectTreeVm = projectTreeVm;
        _editorsVm = editorsViewModel;
    }

    public override IRootDock CreateLayout()
    {
        var _treeDockVm = new DockableToolViewModel(_projectTreeVm)
        {
            Id = "Project Tree",
            Title = "Project Tree"
        };

        var documents = _editorsVm.Editors.Select(x => new DockableEditorViewModel(x) as IDockable).ToArray();

        var documentDock = new DocumentDock
        {
            Id = "DocumentsPane",
            Title = "DocumentsPane",
            Proportion = double.NaN,
            VisibleDockables = CreateList(_editorsVm.Editors.Select(x => new DockableEditorViewModel(x) as IDockable).ToArray())
        };

        documentDock.CanCreateDocument = false;
        _editorsVm.Editors.CollectionChanged += Editors_CollectionChanged;

        var mainLayout = new ProportionalDock
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
                    ActiveDockable = _treeDockVm,
                    VisibleDockables = CreateList<IDockable>
                    (
                        _treeDockVm
                    ),
                    Alignment = Alignment.Left,
                    GripMode = GripMode.Visible
                },
                new ProportionalDockSplitter()
                {
                    Id = "MainSplitter",
                    Title = "MainSplitter"
                },
                documentDock
            )
        };

        var _rootDock = CreateRootDock();

        _rootDock.Id = "Root";
        _rootDock.Title = "Root";
        _rootDock.ActiveDockable = mainLayout;
        _rootDock.DefaultDockable = mainLayout;
        _rootDock.VisibleDockables = CreateList<IDockable>(mainLayout);

        _documentDock = documentDock;

        return _rootDock;
    }

    private void Editors_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
        {
            var newItem = e.NewItems[0] as ResourceEditorBaseViewModel;
            var newDoc = new DockableEditorViewModel(newItem)
            {
                Id = newItem.DisplayName,
                Title = newItem.DisplayName
            };
            //_documentDock.VisibleDockables.Add(new EditorDocumentViewModel(newItem));
            this.AddDockable(_documentDock, newDoc);
            this.SetActiveDockable(newDoc);
            this.SetFocusedDockable(_documentDock, newDoc);
        }
        else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
        {
            var removeItem = _documentDock.VisibleDockables.OfType<DockableEditorViewModel>().First(x => x.Editor == e.OldItems[0]!);
            _documentDock.VisibleDockables.Remove(removeItem);
        }
    }

    public override void InitLayout(IDockable layout)
    {
        this.ContextLocator = new Dictionary<string, Func<object>>
        {
            [nameof(IRootDock)] = () => _context,
            [nameof(IProportionalDock)] = () => _context,
            [nameof(IDocumentDock)] = () => _context,
            [nameof(IToolDock)] = () => _context,
            [nameof(IProportionalDockSplitter)] = () => _context,
            [nameof(IDockWindow)] = () => _context,
            [nameof(IDocument)] = () => _context,
            [nameof(ITool)] = () => _context,
            //["TreePane"] = () => _projectTreeVm,
            ["MainSplitter"] = () => _context,
            ["MainLayout"] = () => _context,
        };

        this.HostWindowLocator = new Dictionary<string, Func<IHostWindow>>
        {
            [nameof(IDockWindow)] = () =>
            {
                var hostWindow = new HostWindow()
                {
                    [!HostWindow.TitleProperty] = new Binding("ActiveDockable.Title")
                };
                return hostWindow;
            }
        };

        this.DockableLocator = new Dictionary<string, Func<IDockable?>>
        {
            ["Root"] = () => _rootDock,
            ["DocumentsPane"] = () => _documentDock,
        };

        base.InitLayout(layout);

        this.SetActiveDockable(_documentDock);
        this.SetFocusedDockable(_documentDock, _documentDock.VisibleDockables?.FirstOrDefault());
    }
}
