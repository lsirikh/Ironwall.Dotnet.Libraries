using System;
using System.Globalization;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/****************************************************************************
   Purpose      : 줌 0.5 래더 순수 산술(SSOT) — zoom-float-halfstep PRD v1.1 §2
   Created By   : GHLee
   Created On   : 2026-08-06
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 줌 0.5 래더 순수 로직 — WPF 무의존. MapZoomControl(합성/라우팅/라벨)과 가시성 게이트,
/// 단위 테스트(NFR-05)가 공용한다. 정책 근거: docs/prds/zoom-float-halfstep-prd.md §2 (G-1=B 확정),
/// 시뮬레이션 검증: docs/tests/zoom-float-halfstep-simulation-log.md (SIM-T/S/R 1,346 PASS).
/// 실효줌 = min(타일줌, MaxZoom) + 0.5×dzl. 라벨: "17" / "17.5" / 최상단 "19.5+" / "19.5++".
/// </summary>
public static class ZoomLadder
{
    /// <summary>객체 줌 게이트 부동소수 허용 오차(FR-10/G-3).</summary>
    public const double GateEpsilon = 1e-6;

    /// <summary>0.5 그리드 스냅 — 중간값은 AwayFromZero(은행가 반올림 금지 — SIM-D002 실증).</summary>
    public static double Snap(double value)
        => Math.Round(value * 2.0, MidpointRounding.AwayFromZero) / 2.0;

    /// <summary>합성: (타일줌, dzl) → 실효줌. 0.5×dzl 가산으로 하프 상태와 다음 정수가
    /// 항상 구분된다(구 합성식 min(zoom,max)+dzl 의 비단사 — SIM-D003 — 해소).</summary>
    public static double Compose(double zoom, int maxZoom, int dzl)
        => Math.Min(zoom, maxZoom) + 0.5 * Math.Max(0, dzl);

    /// <summary>라우팅: 실효줌 → (타일줌, dzl) 분해. NaN/∞ 가드 + 0.5 스냅 + [min, max+0.5×topSteps] 클램프.</summary>
    public static (int Tile, int Dzl) Route(double value, int minZoom, int maxZoom, int topSteps)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return (minZoom, 0);
        double v = Math.Clamp(Snap(value), minZoom, maxZoom + 0.5 * topSteps);
        if (v >= maxZoom)
        {
            int dzl = Math.Clamp((int)Math.Round((v - maxZoom) / 0.5, MidpointRounding.AwayFromZero), 0, topSteps);
            return (maxZoom, dzl);
        }
        int tile = (int)Math.Floor(v + 1e-9);
        return (tile, (v - tile) >= 0.25 ? 1 : 0);
    }

    /// <summary>
    /// 라벨(G-1=B 확정): Max 미만 "Z"/"Z.5", 최상단 "Max"/"Max.5"/"Max.5+"/"Max.5++".
    /// InvariantCulture 고정(NFR-01 — de-DE "17,5" 금지, SIM-L003). 정수는 소수점 없이.
    /// </summary>
    public static string Label(double zoom, int maxZoom, int dzl)
    {
        int baseZoom = (int)Math.Min(Math.Floor(zoom + 1e-9), maxZoom);
        // Max 미만 구간에서 dzl은 0/1만 유효 — stale 값(구 소프트줌 2·3) 유입 시 하프로 강등
        int d = baseZoom < maxZoom ? Math.Clamp(dzl, 0, 1) : Math.Max(0, dzl);
        string b = baseZoom.ToString(CultureInfo.InvariantCulture);
        return d switch
        {
            0 => b,
            1 => b + ".5",
            _ => b + ".5" + new string('+', d - 1),   // 소프트 밴드: "Max.5+", "Max.5++"
        };
    }

    /// <summary>소프트 밴드 여부(G-6=B, FR-18): 최상단(dzl≥2) 소프트 줌 — 주황 라벨 트리거.</summary>
    public static bool IsSoftBand(double zoom, int maxZoom, int dzl)
        => zoom >= maxZoom && dzl >= 2;

    /// <summary>
    /// 객체(심볼/이미지/라벨) 최소 표시 줌 게이트(FR-10, G-3 확정):
    /// 실효줌 ≥ 객체줌(0.5 스냅 정규화) + ε 비교. objectZoom ≤ 0 은 항상 표시(이미지 규약 —
    /// GMapCustomControl 이미지 게이트 `Zoom&lt;=0 ||` 과 동치, 마커에도 무해).
    /// </summary>
    public static bool IsVisibleAtEffectiveZoom(double effectiveZoom, double objectZoom)
        => objectZoom <= 0 || effectiveZoom + GateEpsilon >= Snap(objectZoom);
}
