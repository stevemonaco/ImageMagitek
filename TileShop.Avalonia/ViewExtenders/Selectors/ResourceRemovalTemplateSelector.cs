using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using TileShop.Shared.Models;

namespace TileShop.AvaloniaUI.Selectors;
internal class ResourceRemovalTemplateSelector : IDataTemplate
{
    [Content]
    public Dictionary<string, IDataTemplate> AvailableTemplates { get; } = new();

    public Control Build(object? param)
    {
        if (param is not ResourceChangeViewModel changeVm)
            throw new ArgumentException(nameof(param));

        var key = GetKey(changeVm);

        if (key is null)
            throw new InvalidOperationException(nameof(key));

        var control = AvailableTemplates[key].Build(param);

        return control ?? new TextBlock { Text = "Not found" };
    }
    public bool Match(object? data)
    {
        if (data is not ResourceChangeViewModel changeVm)
            return false;

        if (GetKey(changeVm) is not string key)
            return false;

        if (!AvailableTemplates.ContainsKey(key))
            return false;

        return true;
    }

    private string? GetKey(ResourceChangeViewModel changeVm)
    {
        return changeVm.Resource switch
        {
            Arranger => "arranger",
            Palette => "palette",
            DataSource => "file",
            ResourceFolder => "folder",
            _ => null
        };
    }
}
