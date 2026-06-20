using System.Collections.Concurrent;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/****************************************************************************
   Purpose      : IActionReportGuard 구현 — ConcurrentDictionary 기반 in-flight 집합.
                  싱글톤으로 등록되어 자동/자동복구/배치/수동 조치보고 경로가 공유한다.
   Created By   : GHLee
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public sealed class ActionReportGuard : IActionReportGuard
{
    private readonly ConcurrentDictionary<int, byte> _inFlight = new();

    public bool TryEnter(int eventId)
    {
        if (eventId <= 0) return true;          // 가드 대상 아님(잘못된 Id) — 진행 허용
        return _inFlight.TryAdd(eventId, 0);    // 이미 진행 중이면 false
    }

    public void Exit(int eventId)
    {
        if (eventId <= 0) return;
        _inFlight.TryRemove(eventId, out _);
    }
}
