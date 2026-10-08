using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek.Project;
using TileShop.Shared.Interactions;

namespace TileShop.UI.ViewModels;

public sealed record MoveDestinationModel(ResourceNode Node, string Path);

public sealed partial class MoveNodeViewModel : RequestViewModel<ResourceNode>
{
    public IReadOnlyList<MoveDestinationModel> Destinations { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TryAcceptCommand))]
    private MoveDestinationModel? _selectedDestination;

    public MoveNodeViewModel(string nodeName, IReadOnlyList<MoveDestinationModel> destinations)
    {
        Destinations = destinations;
        Title = $"Move '{nodeName}'";
        AcceptName = "Move";
    }

    protected override bool CanAccept() => SelectedDestination is not null;

    public override ResourceNode? ProduceResult() => SelectedDestination?.Node;
}
