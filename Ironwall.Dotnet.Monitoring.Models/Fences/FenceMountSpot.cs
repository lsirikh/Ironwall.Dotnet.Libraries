namespace Ironwall.Dotnet.Monitoring.Models.Fences;

/// <summary>
/// 센서를 펜스 어디에 다는가(fence-wiring-editor FR-07). 펜스 3종(철조망 · 철조망+윤형 · 디자인)은 기둥 위 · 기둥 중간 · 망 가운데,
/// 담 2종(벽돌 · 시멘트)은 담 위 · 담 앞면. 로컬 저장값에는 이름 글자로 싣는다.
/// </summary>
public enum FenceMountSpot
{
    /// <summary>기둥 위 — <see cref="SensorMountSpec.Panel"/> 은 <b>기둥 번호</b>(0…망 수).</summary>
    PostTop = 0,
    /// <summary>기둥 중간 — <see cref="SensorMountSpec.Panel"/> 은 기둥 번호.</summary>
    PostMiddle = 1,
    /// <summary>망 가운데 — <see cref="SensorMountSpec.Panel"/> 은 망 번호.</summary>
    PanelCenter = 2,
    /// <summary>담 위 — 망 번호.</summary>
    WallTop = 3,
    /// <summary>담 앞면 — 망 번호.</summary>
    WallFace = 4,
}
