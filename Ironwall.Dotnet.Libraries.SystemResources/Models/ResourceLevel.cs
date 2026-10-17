namespace Ironwall.Dotnet.Libraries.SystemResources.Models;

/****************************************************************************
   Purpose      : 리소스 사용률 심각도 레벨 (UI 색 상태 매핑용)
   Created By    : GHLee
   Created On    : 2026-07-13
   Department    : SW Team
   Company       : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// 리소스(CPU/GPU/RAM) 사용률의 심각도 레벨. UI 칩 색상(정상=시안 / 경고=앰버 / 위험=빨강)에 매핑된다.
/// 히스테리시스(<see cref="Services.HysteresisLevelMapper"/>)로 경계값 깜빡임을 방지한다.
/// </summary>
public enum ResourceLevel
{
    /// <summary>정상 — 기본값.</summary>
    Normal = 0,

    /// <summary>경고 — 임계 진입.</summary>
    Warning = 1,

    /// <summary>위험 — 고부하.</summary>
    Critical = 2,
}
