using Ironwall.Dotnet.Libraries.Enums;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Managers;
/****************************************************************************
   Purpose      : 제어기 무통신(Blackout) 상태 전파 순수 로직 (GMap_Controller_Blackout).
                  UI/스레드/큐 무관 정적 함수만 — 헤드리스 시뮬레이션·테스트 대상.
   개념         : 제어기 장애(FAULT_CONTROLLER)면 그 제어기에 연결된 센서가 모두 먹통 →
                  해당 센서들이 속한 그룹의 PidsGroupSymbol을 검은색(Blackout)으로.
                  우선순위: Blackout > FaultedDetecting > Faulted > Detecting > Normal.
                  제어기 장애 해소 시 Blackout 제거 → 남은 센서 상태(Faulted/Normal)로 자연 복귀.
   Created On   : 2026-07-31 · Sensorway Co., Ltd.
 ****************************************************************************/
public static class ControllerBlackoutModel
{
    /// <summary>그룹 내 활성 이벤트 집계 3플래그 → 복합 상태(단일 우선순위 지점).
    /// hasControllerBlackout이 최상위 — 제어기 무통신이면 센서 Fault/Detection을 덮어 검은색.</summary>
    public static EnumCompositeEventStatus Resolve(bool hasControllerBlackout, bool hasFault, bool hasDetection)
    {
        if (hasControllerBlackout) return EnumCompositeEventStatus.Blackout;
        if (hasFault && hasDetection) return EnumCompositeEventStatus.FaultedDetecting;
        if (hasFault) return EnumCompositeEventStatus.Faulted;
        if (hasDetection) return EnumCompositeEventStatus.Detecting;
        return EnumCompositeEventStatus.Normal;
    }

    /// <summary>제어기 장애 → 검은색이 될 그룹 집합 = 그 제어기에 연결된 센서들의 소속 그룹 합집합.
    /// sensors = (연결 제어기 Id, 센서 소속 그룹들) 시퀀스. controllerId와 매칭되는 센서의 그룹만 수집.
    /// 제어기 자신의 그룹(ownGroups)도 포함(제어기 심볼이 그룹에 속할 수 있음).</summary>
    public static IReadOnlyCollection<int> ExpandBlackoutGroups(
        int controllerId,
        IEnumerable<(int? ControllerId, IEnumerable<int>? Groups)> sensors,
        IEnumerable<int>? ownGroups = null)
    {
        var set = new HashSet<int>();
        if (ownGroups != null)
            foreach (var g in ownGroups) if (g > 0) set.Add(g);
        if (sensors != null)
            foreach (var (ctrlId, groups) in sensors)
                if (ctrlId == controllerId && groups != null)
                    foreach (var g in groups) if (g > 0) set.Add(g);
        return set;
    }
}
