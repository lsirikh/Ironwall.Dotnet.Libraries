namespace Ironwall.Dotnet.Monitoring.Models.Fences;

/// <summary>
/// 줄 위 번호가 커지는 방향(fence-wiring-editor §1-0b) — <b>가정 · 사용자 확인 대기</b>라 함수 인자 하나로 뒤집을 수 있게 둔다.
/// </summary>
public enum FenceNumberingDirection
{
    /// <summary>
    /// 줄마다 제어기 쪽에서 멀어질수록 커진다(그림 ①: 아래 1→6 · 위 101→106 이 모두 왼쪽 제어기에서 멀어지는 쪽) — 위 줄은 사슬 방향과 반대(기본).
    /// </summary>
    AwayFromController = 0,
    /// <summary>사슬 순서 그대로(Ch1 → Ch2).</summary>
    AlongChain = 1,
}
