using System;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 지도 카드 틸트(2.5D) 순수 판정 — map-tilt-25d PRD FR-02 상태기계의 단일 진실원.
                  RotationMath.Decide(kill-switch·앵커 게이트)·ZoomLadder(0.5 래더·GateEpsilon)·FenceLod(순수 LOD)와
                  같은 구조: WPF 무의존, 배선(GMapCustomControl.ReevaluateTilt)은 이 함수 결과만 적용한다.
                  헤드리스 테스트 공유(tests/GMaps.Ui.Tests/TiltMathTests — SIM-T 매트릭스 재현).
   Note         : 판정 순서 = Off → Tier0 → AnchorLock → 줌 게이트(진입 Z ≥ MinZoom−ε · 이탈 Z ≤ MinZoom−0.5·h−ε ·
                  밴드는 wasActive 유지, SIM-C011). 출력 φ = clamp(userAngle, 0, MaxAngleDeg)(G1). OFF 는 즉시 0(G10 —
                  회전의 "각도 유지"와 달리 렌더 상태). ε 은 ZoomLadder.GateEpsilon 재사용(새 상수 없음).
   Created On   : 2026-09-08 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>앵커(사이트 고정) 모드 — G3: A모드(정북·회전 잠금)는 틸트 차단, B모드(회전 허용)는 허용.</summary>
public enum TiltAnchorMode
{
    /// <summary>앵커 비활성.</summary>
    None = 0,
    /// <summary>A모드 — 정북 고정·회전 잠금. 틸트도 잠근다(RotationMath.Decide anchorActive 와 동형).</summary>
    RotationLocked = 1,
    /// <summary>B모드 — 회전 허용. 틸트 허용.</summary>
    RotationAllowed = 2,
}

/// <summary>틸트 상태기계 상태(PRD §3). 게이트 ON/OFF 는 <see cref="TiltDecision.Active"/> 로 읽는다 — Hold 는 밴드 안에서 이전 상태를 이어받은 것.</summary>
public enum TiltState
{
    /// <summary>kill-switch OFF — 즉시 φ=0(G10).</summary>
    Off = 0,
    /// <summary>플래그 ON 이지만 Tier0(소프트웨어 렌더) 또는 앵커 A모드로 강제 0. 사유는 <see cref="TiltDecision.Reason"/>.</summary>
    Locked = 1,
    /// <summary>실효줌이 이탈 임계 이하 — 탑뷰.</summary>
    Below = 2,
    /// <summary>히스테리시스 밴드 안 — <c>wasActive</c> 를 그대로 유지(SIM-C011).</summary>
    Hold = 3,
    /// <summary>실효줌이 진입 임계 이상 — 틸트 적용.</summary>
    Active = 4,
}

/// <summary>
/// <see cref="TiltMath.Decide"/> 입력. 설정값(MinZoom·HysteresisSteps·MaxAngleDeg)은 MapTiltModel 에서, 런타임값은
/// GMapCustomControl(EffectiveZoom·앵커·Tier)과 사용자 요청각에서 온다.
/// </summary>
/// <param name="IsEnabled">MapTilt.IsEnabled kill-switch(G5, 기본 OFF).</param>
/// <param name="IsSoftwareTier">RenderCapability Tier0(FR-13) — true 면 강제 0.</param>
/// <param name="AnchorMode">앵커 모드(G3).</param>
/// <param name="EffectiveZoom">실효줌 SSOT(min(Zoom,MaxZoom)+0.5·dzl).</param>
/// <param name="WasActive">직전 판정의 <see cref="TiltDecision.Active"/> — 밴드 유지용.</param>
/// <param name="UserAngleDeg">사용자 요청 각(0~Max, 범위 밖은 클램프).</param>
/// <param name="MinZoom">게이트 진입 실효줌(기본 18.0, 0.5 그리드).</param>
/// <param name="HysteresisSteps">이탈 여유 스텝 수(1 스텝 = 0.5 줌, 기본 1). 음수는 0.</param>
/// <param name="MaxAngleDeg">최대 틸트 각(기본 35).</param>
public readonly record struct TiltInput(
    bool IsEnabled,
    bool IsSoftwareTier,
    TiltAnchorMode AnchorMode,
    double EffectiveZoom,
    bool WasActive,
    double UserAngleDeg,
    double MinZoom,
    int HysteresisSteps,
    double MaxAngleDeg);

/// <summary><see cref="TiltMath.Decide"/> 결과. <see cref="Active"/> 가 게이트 ON(φ 적용 상태)이며, φu=0 이면 Active 여도 φ=0 이다(SIM-T1539).</summary>
/// <param name="PhiDeg">적용할 틸트 각(도). 항상 [0, MaxAngleDeg].</param>
/// <param name="State">상태기계 상태.</param>
/// <param name="Reason">사유 식별자 — "Off" · "Tier0" · "AnchorLock" · "Below" · "Hold" · "Active"(로그·툴팁용).</param>
/// <param name="Active">게이트 ON 여부 — 다음 판정의 <see cref="TiltInput.WasActive"/> 로 되먹인다.</param>
public readonly record struct TiltDecision(double PhiDeg, TiltState State, string Reason, bool Active);

/// <summary>틸트 판정 순수 수학(FR-02). 상태·배선 없음 — 같은 입력이면 같은 출력.</summary>
public static class TiltMath
{
    /// <summary>줌 1 스텝(0.5 래더) — <see cref="TiltInput.HysteresisSteps"/> 를 줌 폭으로 환산한다.</summary>
    private const double ZoomStep = 0.5;

    /// <summary>사유 식별자(로그 `[Tilt] reason=`·툴팁 매핑용 상수).</summary>
    public const string ReasonOff = "Off";
    public const string ReasonTier0 = "Tier0";
    public const string ReasonAnchorLock = "AnchorLock";
    public const string ReasonBelow = "Below";
    public const string ReasonHold = "Hold";
    public const string ReasonActive = "Active";

    /// <summary>
    /// PRD §3 상태기계:
    /// <code>
    /// Off     : !F                      → φ=0 (즉시, G10)
    /// Locked  : F ∧ (T0 ∨ A==A모드)      → φ=0 reason=Tier0|AnchorLock
    /// Below   : F ∧ Z ≤ MinZoom−0.5h−ε  → φ=0, active=false
    /// Active  : F ∧ Z ≥ MinZoom−ε       → φ=clamp(φu,0,Max), active=true
    /// Hold    : F ∧ 밴드 안              → wasActive ? φ=clamp : 0   (히스테리시스, SIM-C011)
    /// </code>
    /// NaN/∞ 실효줌·MinZoom 은 안전측(Below)으로 떨어뜨린다(RotationMath.NormalizeDeg 의 NaN→0 방어와 동형).
    /// </summary>
    public static TiltDecision Decide(TiltInput input)
    {
        if (!input.IsEnabled)
            return new TiltDecision(0d, TiltState.Off, ReasonOff, false);

        if (input.IsSoftwareTier)
            return new TiltDecision(0d, TiltState.Locked, ReasonTier0, false);

        if (input.AnchorMode == TiltAnchorMode.RotationLocked)
            return new TiltDecision(0d, TiltState.Locked, ReasonAnchorLock, false);

        double z = input.EffectiveZoom;
        double minZoom = input.MinZoom;
        if (!double.IsFinite(z) || !double.IsFinite(minZoom))
            return new TiltDecision(0d, TiltState.Below, ReasonBelow, false);

        double enter = EnterThreshold(minZoom);
        double exit = ExitThreshold(minZoom, input.HysteresisSteps);
        double phi = ClampAngle(input.UserAngleDeg, input.MaxAngleDeg);

        if (z >= enter)
            return new TiltDecision(phi, TiltState.Active, ReasonActive, true);

        if (z <= exit)
            return new TiltDecision(0d, TiltState.Below, ReasonBelow, false);

        // 밴드(exit, enter): 직전 게이트 상태 유지
        return input.WasActive
            ? new TiltDecision(phi, TiltState.Hold, ReasonHold, true)
            : new TiltDecision(0d, TiltState.Hold, ReasonHold, false);
    }

    /// <summary>게이트 진입 임계 = MinZoom − ε (ε = <see cref="ZoomLadder.GateEpsilon"/>).</summary>
    public static double EnterThreshold(double minZoom)
        => minZoom - ZoomLadder.GateEpsilon;

    /// <summary>게이트 이탈 임계 = MinZoom − 0.5·h − ε. h&lt;0 은 0(밴드 없음 = 진입/이탈 동일).</summary>
    public static double ExitThreshold(double minZoom, int hysteresisSteps)
        => minZoom - ZoomStep * Math.Max(0, hysteresisSteps) - ZoomLadder.GateEpsilon;

    /// <summary>각도 클램프 [0, maxAngleDeg](G1). NaN/∞ 각도 → 0, maxAngleDeg 가 NaN/음수면 0 으로 닫는다(안전측 탑뷰).</summary>
    public static double ClampAngle(double angleDeg, double maxAngleDeg)
    {
        if (!double.IsFinite(angleDeg)) return 0d;
        double max = double.IsFinite(maxAngleDeg) ? Math.Max(0d, maxAngleDeg) : 0d;
        return Math.Clamp(angleDeg, 0d, max);
    }

    // SnapMinZoom(0.5 그리드 + [Min, Max+1.5] 캡)은 제거됨 — 설정 정규화의 SSOT 는 MapTiltModel.Normalize(GMaps 모델, SnapHalf + 클램프 + 경고).
    // 두 곳에 같은 규칙을 두면 ZoomLadder.Snap/SnapHalf 가 갈라질 때 조용히 어긋난다(2026-09-08 IMPL-F 정리).
}
