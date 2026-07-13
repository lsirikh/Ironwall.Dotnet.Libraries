using Ironwall.Dotnet.Libraries.SystemResources.Models;

namespace Ironwall.Dotnet.Libraries.SystemResources.Services;

/****************************************************************************
   Purpose      : 사용률 → 레벨 매핑 (히스테리시스로 경계 깜빡임 방지)
   Created By    : GHLee
   Created On    : 2026-07-13
   Department    : SW Team
   Company       : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// 사용률 %를 <see cref="ResourceLevel"/>로 매핑하되, 진입/복귀 임계를 분리(데드밴드)하여
/// 경계값(예: 59↔61)에서 색이 깜빡이는 것을 막는다. <b>이전 레벨을 보유하는 상태 객체</b>이므로
/// 지표(CPU/GPU/RAM)마다 별도 인스턴스를 사용한다. UI 스레드 단일 호출 전제(비-스레드세이프).
/// </summary>
public sealed class HysteresisLevelMapper
{
    private readonly ResourceThresholds _t;
    private ResourceLevel _prev = ResourceLevel.Normal;

    public HysteresisLevelMapper(ResourceThresholds? thresholds = null)
        => _t = thresholds ?? ResourceThresholds.Default;

    /// <summary>현재 사용률에 대한 레벨을 히스테리시스로 결정하고 상태를 갱신한다.</summary>
    public ResourceLevel Map(double pct)
    {
        _prev = _prev switch
        {
            ResourceLevel.Normal =>
                pct >= _t.CritEnter ? ResourceLevel.Critical
                : pct >= _t.WarnEnter ? ResourceLevel.Warning
                : ResourceLevel.Normal,

            ResourceLevel.Warning =>
                pct >= _t.CritEnter ? ResourceLevel.Critical
                : pct <= _t.WarnExit ? ResourceLevel.Normal
                : ResourceLevel.Warning,

            ResourceLevel.Critical =>
                pct <= _t.WarnExit ? ResourceLevel.Normal
                : pct <= _t.CritExit ? ResourceLevel.Warning
                : ResourceLevel.Critical,

            _ => ResourceLevel.Normal,
        };
        return _prev;
    }

    /// <summary>레벨 상태를 Normal로 초기화(재시작/재프라임 시).</summary>
    public void Reset() => _prev = ResourceLevel.Normal;
}
