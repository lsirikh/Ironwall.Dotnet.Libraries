namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;

/// <summary>
/// 아이콘 좌하단 문 표시가 그릴 모양. <see cref="None"/> 이면 그리지 않는다.
/// </summary>
public enum DoorIndicatorKind
{
    None = 0,
    /// <summary>문이 있는 장비인데 위치를 아직 모른다 — 빈 문틀 + 가운데 점(흐린 색).</summary>
    Unknown = 1,
    Closed = 2,
    Open = 3,
    /// <summary>구동 중(통문 <c>RUNNING</c>) — 좌우 화살표.</summary>
    Running = 4,
}
