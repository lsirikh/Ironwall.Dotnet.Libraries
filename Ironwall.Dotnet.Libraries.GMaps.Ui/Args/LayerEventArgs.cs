using Ironwall.Dotnet.Monitoring.Models.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
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

/// <summary>
/// 개별 Overlay 심볼(LayerType=Symbol의 카테고리 단위가 아닌 단일 ISymbolModel) 가시성 변경.
/// LayerChangedEventArgs(IMapLayerModel)는 개별 심볼을 운반할 수 없으므로 전용 args 분리(FR-01 이음새).
/// </summary>
public class SymbolVisibilityChangedEventArgs : EventArgs
{
    public ISymbolModel Symbol { get; }
    public bool IsVisible { get; }

    public SymbolVisibilityChangedEventArgs(ISymbolModel symbol, bool isVisible)
    {
        Symbol = symbol;
        IsVisible = isVisible;
    }
}

/// <summary>
/// 개별 심볼 '중앙으로 이동' 요청 — 맵을 심볼 좌표로 팬/줌(FR-04).
/// </summary>
public class SymbolNavigateRequestedEventArgs : EventArgs
{
    public ISymbolModel Symbol { get; }

    public SymbolNavigateRequestedEventArgs(ISymbolModel symbol)
    {
        Symbol = symbol;
    }
}
