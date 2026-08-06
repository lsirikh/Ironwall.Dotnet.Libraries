namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
/****************************************************************************
   Purpose      : 탐지·장애 신호등 점등 규칙 SSOT (map-topbar-trafficlight FR-A).
                  - 설비 상태등 의미론: 빨(장애)·노(탐지) 동시 점등 허용
                  - green 불변식: green = ready && 탐지 0 && 장애 0 — 동시 점등 시 구조적 소등
                  - 미초기화 게이트: ready=false(첫 EQM 집계 전) 면 전체 소등(데이터 없음 ≠ 정상)
   Created On   : 2026-08-06 · Sensorway Co., Ltd.
 ****************************************************************************/
public static class TrafficLampLogic
{
    /// <summary>빨간 램프(장애) — 상시 점등(깜빡임 없음, 사용자 확정).</summary>
    public static bool FaultOn(bool ready, int faultCount) => ready && faultCount > 0;

    /// <summary>노란 램프(탐지) — 점등 조건이자 XAML 깜빡임 애니메이션 게이트(사용자 확정).</summary>
    public static bool DetectionOn(bool ready, int detectionCount) => ready && detectionCount > 0;

    /// <summary>초록 램프(정상) — ready 이면서 두 카운트 모두 0일 때만.</summary>
    public static bool GreenOn(bool ready, int detectionCount, int faultCount)
        => ready && detectionCount == 0 && faultCount == 0;
}
