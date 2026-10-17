using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 렌더 Tier 판정 순수 함수(WPF 무의존) — map-tilt-25d PRD FR-13 Tier 0 강등의
                  단일 진실원. `RenderCapability.Tier` 원시값(상위 워드 = tier 0/1/2)을 받아
                  소프트웨어 렌더링(Tier 0 = RDP/가상화/드라이버 부재) 여부를 돌려준다.
                  TiltMath.Decide 의 `tier0` 입력이 이 판정을 그대로 쓴다(SIM-C008, 분석 D4).
   Note         : WPF 래퍼(RenderCapability 1회 읽기 + TierChanged 재발행)는 RenderTierProbe.Wpf.cs.
                  이 파일은 tests/GMaps.Ui.Tests 에 소스링크되므로 System.Windows 참조를 두지 않는다.
                  기존 주석 처리된 Tier 진단(GMapCustomControl.cs:461-468, 672-679)과 동일한 `>> 16` 규칙.
                  ★ PRD V-04(RDP 세션 Tier 값·틸트 프레임 비용)는 실기 항목 — RDP 실측 전까지
                    틸트 기능은 기본 OFF 이며, 이 프로브는 강등 사유 로그(`[Tilt] tier=`)만 남긴다.
   Created By   : Claude Code
   Created On   : 2026-09-08
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public static partial class RenderTierProbe
{
    /// <summary>소프트웨어 렌더링 tier 레벨(하드웨어 가속 없음). 부분 가속=1, 전체 가속=2.</summary>
    public const int SoftwareTierLevel = 0;

    /// <summary><c>RenderCapability.Tier</c> 원시값 → tier 레벨(0/1/2). 하위 워드는 예약 비트라 버린다.</summary>
    public static int TierLevel(int renderCapabilityTier) => renderCapabilityTier >> 16;

    /// <summary>순수 판정(FR-13) — 상위 워드가 0 이면 소프트웨어 렌더링.
    /// 0x00000 → true · 0x10000 → false · 0x20000 → false. 하위 워드만 차 있는 값(0x0000FFFF)도 true.</summary>
    public static bool IsSoftwareTier(int renderCapabilityTier) => TierLevel(renderCapabilityTier) == SoftwareTierLevel;

    /// <summary>강등 사유 로그 문자열(V-04 실기 로그 grep 키 <c>[Tilt] tier=</c>).
    /// 예: <c>[Tilt] tier=0x20000 software=False</c>. 형식을 바꾸면 플랜 VER-04 grep 이 깨진다.</summary>
    public static string FormatLog(int renderCapabilityTier)
        => $"[Tilt] tier=0x{renderCapabilityTier:X} software={IsSoftwareTier(renderCapabilityTier)}";
}
