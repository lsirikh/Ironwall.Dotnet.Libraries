using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Args;

/// <summary>
/// 오버레이 맵 등록 요청 이벤트 인자
/// </summary>
public class MapRegistrationEventArgs : EventArgs
{
    public string LayerName { get; set; } = string.Empty;
    public int MinZoom { get; set; } = 10;
    public int MaxZoom { get; set; } = 16;
}
