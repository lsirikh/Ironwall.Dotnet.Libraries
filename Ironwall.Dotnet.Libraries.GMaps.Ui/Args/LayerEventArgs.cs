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
