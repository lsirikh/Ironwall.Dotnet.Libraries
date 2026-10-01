namespace Ironwall.Dotnet.Monitoring.Models.Fences;

/// <summary>
/// 센서를 펜스 어디에 다는가(fence-wiring-editor FR-07). 펜스 3종(철조망 · 철조망+윤형 · 디자인)은 기둥 위 · 기둥 중간 · 망 가운데,
/// 담 2종(벽돌 · 시멘트)은 담 위 · 담 앞면. 망 아래 · 윤형 코일은 높이 단계(위 · 아래로 올리고 내리기)에서 더해졌다. 로컬 저장값에는 이름 글자로 싣는다.
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
    /// <summary>망 아래 — 망 번호(펜스 3종 · 망 높이의 <see cref="FenceLayoutMath.PANEL_BOTTOM_RATIO"/>).</summary>
    PanelBottom = 5,
    /// <summary>
    /// 윤형 코일 — 망 번호(철조망+윤형 망 가운데 · 코일 위에 단다). 늘 <b>위 줄</b>이다(<see cref="FenceLane.Upper"/>) — 아래 줄이거나 윤형이 아닌 망이면
    /// <see cref="FenceLayoutMath.Normalize"/> 가 망 가운데로 맞춘다.
    /// </summary>
    RazorCoil = 6,
    /// <summary>망 위(상단) — 망 번호(펜스 3종 · 망 높이의 <see cref="FenceLayoutMath.PANEL_TOP_RATIO"/>) · 9점 격자의 윗줄.</summary>
    PanelTop = 7,
    /// <summary>담 아래(하단) — 망 번호(담 2종 · 9점 격자의 아랫줄).</summary>
    WallBottom = 8,
}

/// <summary>
/// 망(담) 한 칸 안의 가로 자리(9점 격자의 열 · 2026-10-01 사용자: "1개의 판망을 중심으로 9개의 포인트") — 기둥에서 안쪽으로 들인 왼쪽 · 가운데 · 오른쪽.
/// 기둥 자리 · 옛 문서는 가운데다(값이 없으면 가운데로 읽는다 — 옛 자리가 그대로 보인다).
/// </summary>
public enum FenceColumn
{
    Center = 0,
    Left = 1,
    Right = 2,
}
