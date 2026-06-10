using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Args;

public enum ZOrderDirection { Up, Down, ToTop, ToBottom }

/// <summary>
/// PropertyPanel Z-order 버튼 클릭 시 GMapPropertyBaseControl → Behavior로 전달되는 이벤트 아규먼트
/// </summary>
public class ZOrderChangeRequestedEventArgs : EventArgs
{
    public ZOrderDirection Direction { get; }
    public ZOrderChangeRequestedEventArgs(ZOrderDirection direction) => Direction = direction;
}

/// <summary>
/// PropertyPanelEventBehavior → EventAggregator를 통해 MapViewModel로 전달되는 이벤트
/// </summary>
public class ZOrderChangeRequestedEvent
{
    public ZOrderDirection Direction { get; set; }
    public IEditableMarker? Marker { get; set; }
}
