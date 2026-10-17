using System.ComponentModel;

namespace Ironwall.Dotnet.Libraries.Enums;

/// <summary>
/// 펜스 한 칸(망)의 모양 — 결선 펜스 편집기(fence-wiring-editor FR-02)와 지도 PIDS 3D 펜스가 함께 쓰는 한 벌(FR-16).
/// 로컬 저장값(JSON)에는 이름 글자로 싣는다 — 정수 값을 바꾸지 않는다.
/// </summary>
public enum EnumFenceStyle
{
    /// <summary>철조망 펜스 — 기둥 + 마름모 철망.</summary>
    [Description("철조망")]
    ChainLink = 0,

    /// <summary>철조망 + 윤형철조망 — 기둥 위에 원형 코일.</summary>
    [Description("철조망+윤형")]
    ChainLinkRazor = 1,

    /// <summary>벽돌담 — 기둥 없음, 위에 갓돌.</summary>
    [Description("벽돌담")]
    Brick = 2,

    /// <summary>시멘트담 — 기둥 없음, 판 이음매.</summary>
    [Description("시멘트담")]
    Concrete = 3,

    /// <summary>디자인펜스 — 초록 세로살.</summary>
    [Description("디자인펜스")]
    DesignFence = 4,
}
