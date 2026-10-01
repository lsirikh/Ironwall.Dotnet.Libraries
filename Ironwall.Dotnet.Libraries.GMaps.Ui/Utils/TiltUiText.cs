using System;
using System.Globalization;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 지도 카드 틸트(2.5D) 뷰모델 표시 로직 순수 함수 — map-tilt-25d PRD FR-08(툴바 토글 툴팁 사유) ·
                  FR-09(축척바 옆 "기울임 φ°" 배지) · G3(앵커 A모드 토글 잠금). MapViewModel.Tilt.cs 가 소비하고
                  tests/GMaps.Ui.Tests/TiltUiTextTests 가 실코드 링크로 검증한다(WPF 무의존).
   Note         : 사유 키(TiltMath.Reason*)를 한국어 문구로 매핑하는 단일 진실원. 숫자 포맷은 InvariantCulture 고정
                  (de-DE "16,5" 금지 — ZoomLadder.Label NFR-01 동형). 배지 임계 0.1° 는 GMapCustomControl.TILT_SNAP_GATE_EPSILON
                  과 같은 값(순수 파일이라 상수 복제 — 스냅 게이트와 배지가 같은 φ 에서 켜지고 꺼진다).
   Created On   : 2026-09-08 · Sensorway Co., Ltd.
****************************************************************************/
public static class TiltUiText
{
    /// <summary>배지 표시 임계(도) — φ_applied &gt; 0.1 만 표시(= GMapCustomControl.TILT_SNAP_GATE_EPSILON).</summary>
    public const double BadgeVisibleEpsilon = 0.1;

    /// <summary>토글 단축키 표기(보기 메뉴 InputGestureText 와 동일).</summary>
    public const string ShortcutText = "Ctrl+Shift+T";

    private const string Title = "지도 기울이기 (" + ShortcutText + ")";

    /// <summary>배지 표시 여부 — 적용 φ 가 0.1° 초과일 때만(NaN/∞ 는 숨김).</summary>
    public static bool IsBadgeVisible(double phiAppliedDeg)
        => double.IsFinite(phiAppliedDeg) && phiAppliedDeg > BadgeVisibleEpsilon;

    /// <summary>축척바 옆 배지 문구 "기울임 20°"(FR-09 · G6 세로 축척 이탈 표시). 숨김이면 빈 문자열.</summary>
    public static string BadgeText(double phiAppliedDeg)
        => IsBadgeVisible(phiAppliedDeg)
            ? string.Format(CultureInfo.InvariantCulture, "기울임 {0:0.#}°", phiAppliedDeg)
            : string.Empty;

    /// <summary>줌 표기 — 정수는 "18", 하프는 "16.5"(InvariantCulture).</summary>
    public static string FormatZoom(double zoom)
        => double.IsFinite(zoom) ? zoom.ToString("0.#", CultureInfo.InvariantCulture) : "-";

    /// <summary>토글 버튼 활성 조건(FR-08 ① "IsEnabled=앵커 A모드 아님") — 앵커 활성 ∧ 회전 비허용(A모드)이면 잠금.</summary>
    public static bool IsToggleEnabled(bool anchorActive, bool anchorAllowsRotation)
        => !(anchorActive && !anchorAllowsRotation);

    /// <summary>각도 스텝 결과 = clamp(current + delta, 0, max) — GMapCustomControl.StepTiltAngle 과 같은 규칙(VM 커맨드 CanExecute 용).
    /// 결과가 현재값과 같으면(상·하한 도달) 스텝 불가.</summary>
    public static double StepAngle(double currentDeg, double deltaDeg, double maxAngleDeg)
    {
        if (!double.IsFinite(deltaDeg)) return TiltMath.ClampAngle(currentDeg, maxAngleDeg);
        return TiltMath.ClampAngle(currentDeg + deltaDeg, maxAngleDeg);
    }

    /// <summary>
    /// 툴바 토글 툴팁(ToolTipService.ShowOnDisabled — 비활성 사유 포함). 이름 + 단축키 + 지금 상태 한 구절만 —
    /// 각도 단축키(Ctrl(+Shift)+↑/↓) · "켜면 줌 N 이상에서 적용" 같은 동작 원리는 회전 · 기울이기 "?"(MapsHelp
    /// "Map.Toolbar.RotateTilt", help-callout H-4)로 옮겼다. 우선순위:
    /// 앵커 A모드 잠금 → OFF → Tier0 → 게이트 미달(Below/Hold-inactive: "줌 18 이상에서 적용 · 현재 16.5 → 탑뷰")
    /// → 적용 중(Active/Hold-active: "기울임 20° 적용 중").
    /// </summary>
    /// <param name="isEnabled">kill-switch 상태.</param>
    /// <param name="reason">직전 판정 사유(TiltMath.Reason*). null/미지 키는 OFF 문구로 폴백.</param>
    /// <param name="active">직전 판정의 게이트 ON 여부(TiltDecision.Active).</param>
    /// <param name="phiAppliedDeg">적용 φ(도).</param>
    /// <param name="effectiveZoom">현재 실효줌.</param>
    /// <param name="minZoom">게이트 진입 줌(설정).</param>
    /// <param name="anchorLocked">앵커 A모드(정북 고정) 잠금 여부 — 플래그와 무관하게 최우선.</param>
    public static string ToggleToolTip(bool isEnabled, string? reason, bool active, double phiAppliedDeg,
        double effectiveZoom, double minZoom, bool anchorLocked)
    {
        string min = FormatZoom(minZoom);
        if (anchorLocked || string.Equals(reason, TiltMath.ReasonAnchorLock, StringComparison.Ordinal))
            return Title + " — 앵커(정북 고정) 중 사용 불가 · 변경하려면 앵커를 먼저 해제하세요";

        if (!isEnabled || string.Equals(reason, TiltMath.ReasonOff, StringComparison.Ordinal))
            return Title + " — OFF · 탑뷰";

        if (string.Equals(reason, TiltMath.ReasonTier0, StringComparison.Ordinal))
            return Title + " — 소프트웨어 렌더링(Tier 0) 감지 · 기울이기 강제 해제(탑뷰)";

        if (active && IsBadgeVisible(phiAppliedDeg))
        {
            string hold = string.Equals(reason, TiltMath.ReasonHold, StringComparison.Ordinal)
                ? $" · 현재 {FormatZoom(effectiveZoom)}(유지 밴드)"
                : string.Empty;
            return Title + string.Format(CultureInfo.InvariantCulture,
                " — 기울임 {0:0.#}° 적용 중 · 줌 {1} 이상{2}", phiAppliedDeg, min, hold);
        }

        if (active)
            return Title + " — ON · 각도 0°(탑뷰)";

        // Below · Hold(inactive)
        return Title + $" — 줌 {min} 이상에서 적용 · 현재 {FormatZoom(effectiveZoom)} → 탑뷰";
    }
}
