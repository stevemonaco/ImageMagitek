using System.Linq;
﻿using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek.Project;

namespace TileShop.Shared.Models;

public partial class ResourceChangeViewModel : ObservableObject
{
    public IProjectResource Resource { get; }
    public ResourceNode ResourceNode { get; }

    [ObservableProperty] private string _resourceName;
    [ObservableProperty] private string _resourcePath;
    [ObservableProperty] private bool _removed;
    [ObservableProperty] private bool _lostPalette;
    [ObservableProperty] private bool _lostElement;
    [ObservableProperty] private bool _lostTilePalette;
    [ObservableProperty] private bool _lostSlotPalette;
    [ObservableProperty] private bool _isChanged;

    public string ChangeDescription
    {
        get
        {
            string?[] parts =
            [
                LostElement ? "loses elements" : null,
                LostPalette ? "uses default palette" : null,
                LostTilePalette ? "uses the generated tile palette" : null,
                LostSlotPalette ? "removed palette slots use slot 0" : null
            ];
            return string.Join(", ", parts.OfType<string>());
        }
    }

    public ResourceChangeViewModel(ResourceNode resourceNode, string resourcePathKey, bool removed, bool lostPalette, bool lostElement)
    {
        ResourceNode = resourceNode;
        Resource = ResourceNode.Item;

        _resourceName = Resource.Name;
        _resourcePath = resourcePathKey;
        _removed = removed;
        _lostPalette = lostPalette;
        _lostElement = lostElement;
        _isChanged = lostPalette || lostElement;
    }

    public ResourceChangeViewModel(ResourceChange change)
    {
        ResourceNode = change.ResourceNode;
        Resource = change.Resource;

        _resourceName = Resource.Name;
        _resourcePath = change.ResourcePath;
        _removed = change.Removed;
        _lostPalette = change.LostPalette;
        _lostElement = change.LostElement;
        _lostTilePalette = change.LostTilePalette;
        _lostSlotPalette = change.LostSlotPalette;
        IsChanged = change.IsChanged;
    }
}
