using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 틸트 게이트 재평가 코얼레싱 큐(순수, WPF 무의존) — map-tilt-25d PRD v1.1 FR-03 "판정 원자성".
                  휠·스텝·SetEffectiveZoom 은 벤더 정수 줌(→ OnMapZoomChanged 동기 발화) 뒤에 디지털 줌을 바꾸므로
                  중간 정수 상태(예 18.0 → 17.0 → 17.5)에서 판정하면 wasActive 가 파괴돼 "17.5 유지"(히스테리시스)가
                  무효가 된다. 이 큐는 요청(Request)만 세고, 배선(GMapCustomControl.ReevaluateTilt)이 Dispatcher Render
                  우선순위로 프레임당 1회 Commit 을 호출해 **최종 실효줌 하나로만** TiltMath.Decide 를 돌린다.
                  WasActive 는 직전 커밋의 TiltDecision.Active 로만 갱신된다(중간 상태 무시).
   Note         : tests/GMaps.Ui.Tests/TiltGateCoalescerTests — "18.0 휠다운 → 중간 17.0 → 최종 17.5 = Hold(Active 유지)",
                  "17.0 → 17.5 = Below 유지(진입 방향 구분)". 배선의 Dispatcher 코얼레싱은 QueueViewportSnapshot 과 동형.
   Created On   : 2026-09-08 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 상태: <see cref="PendingCount"/>(프레임 안에 모인 요청 수) · <see cref="WasActive"/>(직전 커밋 게이트) · <see cref="Last"/>(직전 판정).
/// <list type="bullet">
/// <item><see cref="Request"/>: 재평가 요청 — 첫 요청이면 true(호출자가 Dispatcher 예약), 이후는 false(코얼레싱).</item>
/// <item><see cref="Commit"/>: 최종 입력으로 1회 판정 — 입력의 WasActive 는 무시하고 <see cref="WasActive"/> 로 대체한 뒤 되먹인다.</item>
/// </list>
/// </summary>
public sealed class TiltGateCoalescer
{
    /// <summary>프레임 안에 모인 요청 수(0 = 예약 없음). NFR-01 로그 <c>coalesced=</c> 값.</summary>
    public int PendingCount { get; private set; }

    /// <summary>마지막 요청 사유(로그용). 커밋 후에도 유지된다.</summary>
    public string? LastCause { get; private set; }

    /// <summary>직전 커밋의 <see cref="TiltDecision.Active"/> — 다음 판정의 <see cref="TiltInput.WasActive"/>.</summary>
    public bool WasActive { get; private set; }

    /// <summary>직전 커밋 결과(없으면 null).</summary>
    public TiltDecision? Last { get; private set; }

    /// <summary>총 커밋 횟수(조작당 1 이어야 한다 — NFR-01 계측).</summary>
    public int CommitCount { get; private set; }

    /// <summary>예약(대기) 중인가.</summary>
    public bool IsPending => PendingCount > 0;

    /// <summary>재평가 요청. 반환 true = 이 프레임의 첫 요청(호출자가 커밋을 1회 예약해야 함), false = 이미 예약됨(코얼레싱).</summary>
    public bool Request(string cause)
    {
        LastCause = cause;
        PendingCount++;
        return PendingCount == 1;
    }

    /// <summary>
    /// 최종 입력으로 1회 판정. <paramref name="input"/>.WasActive 는 무시하고 이 큐의 <see cref="WasActive"/>(직전 커밋 기준)를 쓴다 —
    /// 중간 정수 줌 상태가 판정에 끼어들 수 없다. 결과의 Active 로 <see cref="WasActive"/> 를 되먹이고 대기를 비운다.
    /// 예약 없이(직접) 호출해도 동작한다(부팅 복원·Flush).
    /// </summary>
    public TiltDecision Commit(TiltInput input)
    {
        var decision = TiltMath.Decide(input with { WasActive = WasActive });
        WasActive = decision.Active;
        Last = decision;
        PendingCount = 0;
        CommitCount++;
        return decision;
    }

    /// <summary>대기 취소(언로드 등) — WasActive/Last 는 유지.</summary>
    public void Cancel() => PendingCount = 0;
}
