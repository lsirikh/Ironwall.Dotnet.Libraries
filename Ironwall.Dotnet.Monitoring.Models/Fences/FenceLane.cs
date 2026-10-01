namespace Ironwall.Dotnet.Monitoring.Models.Fences;

/// <summary>
/// 센서가 달린 줄(레인 · fence-wiring-editor FR-18 · §1-0b) — 펜스 개념도는 아래 줄 · 위 줄 두 줄이다. 설치 자리(<see cref="FenceMountSpot"/>)와 별개다.
/// 사슬은 아래 줄(제어기 쪽 → 먼 끝) → 위 줄(먼 끝 → 제어기 쪽)이고, 위 줄이 비면 리턴선만 지난다. 로컬 저장값에는 이름 글자로 싣는다.
/// </summary>
public enum FenceLane
{
    /// <summary>아래 줄 — Ch1(실선)이 먼저 지나는 줄(판망 · 스마트센서 · 4차 현장).</summary>
    Lower = 0,
    /// <summary>위 줄 — 먼 끝에서 꺾여 Ch2(점선)로 돌아오는 줄(윤형 · 펜스센서 · 4차 현장).</summary>
    Upper = 1,
}
