using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>호스트 → GIS: 오버레이 스트림 또는 이벤트 창 타일의 상태(FR-26 — 실패는 그 상자 · 타일에만).</summary>
public sealed class StreamStateChanged : IIpcMessage
{
    public string StreamId { get; init; } = string.Empty;

    /// <summary>이벤트 창 타일이면 그 창의 이벤트 키, 오버레이면 null.</summary>
    public string? EventKey { get; init; }
    public StreamState State { get; init; }
    public string? Detail { get; init; }

    /// <summary>
    /// 이벤트 창 타일의 <b>첫</b> <see cref="StreamState.Playing"/> 에만: 호스트가 그 창의 열기 요청(<see cref="OpenEventWindow"/>)을 받은 뒤
    /// 첫 영상 프레임까지 걸린 시간(ms). 그 밖에는 null(직렬화 안 함). 선택 필드라 판을 올리지 않는다 — 옛 GIS 는 무시하고,
    /// 옛 호스트는 보내지 않는다(GIS 는 첫 Playing 을 받은 시각만 계측한다). 계측 카탈로그 PERF-004.
    /// </summary>
    public long? FirstFrameMs { get; init; }
}
