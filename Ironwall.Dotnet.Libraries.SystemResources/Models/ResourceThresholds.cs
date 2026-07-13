namespace Ironwall.Dotnet.Libraries.SystemResources.Models;

/****************************************************************************
   Purpose      : 리소스 레벨 임계값 + 히스테리시스 데드밴드
   Created By    : GHLee
   Created On    : 2026-07-13
   Department    : SW Team
   Company       : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// 리소스 레벨 전환 임계값. 진입/복귀 임계를 분리(데드밴드)하여 경계값에서의 색 깜빡임을 방지한다.
/// 기본값(공통): 경고 진입 62 / 복귀 57, 위험 진입 87 / 복귀 82 (PRD OQ-02).
/// </summary>
public sealed record ResourceThresholds
{
    /// <summary>Normal → Warning 진입 임계(이상).</summary>
    public double WarnEnter { get; init; } = 62;

    /// <summary>Warning → Normal 복귀 임계(이하).</summary>
    public double WarnExit { get; init; } = 57;

    /// <summary>Warning → Critical 진입 임계(이상).</summary>
    public double CritEnter { get; init; } = 87;

    /// <summary>Critical → Warning 복귀 임계(이하).</summary>
    public double CritExit { get; init; } = 82;

    /// <summary>기본 공통 임계값.</summary>
    public static ResourceThresholds Default { get; } = new();
}
