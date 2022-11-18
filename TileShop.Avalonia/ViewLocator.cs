using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Core;

namespace TileShop.AvaloniaUI;
public class ViewLocator : IDataTemplate
{
    public IControl Build(object? data)
    {
        if (data is null)
            return new TextBlock { Text = "Null object" };

        var name = data.GetType().FullName!.Replace("ViewModel", "View");
        var type = Type.GetType(name);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }
        else
        {
            return new TextBlock { Text = "Not Found: " + name };
        }
    }

    public bool Match(object? data)
    {
        return data is ObservableObject || data is IDockable;
    }
}
