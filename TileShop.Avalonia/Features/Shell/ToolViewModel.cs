﻿using CommunityToolkit.Mvvm.ComponentModel;

namespace TileShop.AvaloniaUI.ViewModels;

public abstract partial class ToolViewModel : ObservableObject
{
    public abstract void SaveChanges();
    public abstract void DiscardChanges();

    [ObservableProperty] private string _displayName;
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _isModified;
    [ObservableProperty] private bool _isVisible = true;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _contentId;
}