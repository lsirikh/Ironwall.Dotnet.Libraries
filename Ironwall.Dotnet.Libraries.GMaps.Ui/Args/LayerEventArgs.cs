using Ironwall.Dotnet.Monitoring.Models.Maps;
using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Args;

public class LayerChangedEventArgs : EventArgs
{
    public IMapLayerModel Layer { get; }
    public bool IsVisible { get; }

    public LayerChangedEventArgs(IMapLayerModel layer, bool isVisible)
    {
        Layer = layer;
        IsVisible = isVisible;
    }
}

public class LayerRenameEventArgs : EventArgs
{
    public IMapLayerModel Layer { get; }
    public string NewName { get; }

    public LayerRenameEventArgs(IMapLayerModel layer, string newName)
    {
        Layer = layer;
        NewName = newName;
    }
}

public class LayerOpacityChangedEventArgs : EventArgs
{
    public IMapLayerModel Layer { get; }
    public double Opacity { get; }

    public LayerOpacityChangedEventArgs(IMapLayerModel layer, double opacity)
    {
        Layer = layer;
        Opacity = opacity;
    }
}
