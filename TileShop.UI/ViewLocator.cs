using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Dock.Model.Core;

namespace TileShop.UI;
public class ViewLocator : IDataTemplate
{
    private Dictionary<Type, Func<Control>> _locatorMap = new();
    
    public Control Build(object? data)
    {
        if (data is null)
            return new TextBlock { Text = "Null object" };

        if (_locatorMap.TryGetValue(data.GetType(), out var factory))
        {
            return factory();
        }
        
        return new TextBlock { Text = $"{data.GetType()} not registered" };
    }

    public bool Match(object? data)
    {
        return data is ObservableObject or IDockable;
    }
    
    public void RegisterViewFactory<TViewModel>(Func<Control> factory) where TViewModel : class =>
        _locatorMap.Add(typeof(TViewModel), factory);

    public void RegisterViewFactory<TViewModel, TView>()
        where TViewModel : INotifyPropertyChanged
        where TView : Control =>
        _locatorMap.Add(typeof(TViewModel), Ioc.Default.GetRequiredService<TView>);
}
