namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/****************************************************************************
   Purpose      : 사이트 고정 뷰포트-가두기 (순수 수학 — WPF/GMap 무의존, 단위 테스트 가능)
                  PRD: GMap_Anchor_Viewport_Lock (FR-1/2/5)
   Created By   : GHLee
   Created On   : 7/19/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 사이트 사각형을 "현재 뷰포트 절반만큼 안쪽으로 축소(inset)"해, 그 inset 사각형을 중심(Position) 가두기 구역으로
/// 쓰면 결과적으로 "뷰포트 전체가 사이트 안"이 되는 뷰포트-가두기가 성립한다.
/// <para>GMap.NET 벤더는 드래그 시 중심이 BoundsOfMap 밖이면 드래그를 스킵한다 → BoundsOfMap을 이 inset으로 주면
/// 벤더의 검증된 메커니즘이 그대로 뷰포트-가두기가 된다(RenderOffset 시각 이동을 애초에 막음).</para>
/// <para>축별로 뷰포트가 사이트보다 크거나 같으면 inset이 붕괴 → 중심 근방(±ε)으로 만들어 "딱 맞는 줌에서 잠금"을 구현.
/// 디지털 줌(17+)은 실제 보이는 영역이 작으므로 뷰포트를 ÷scale로 축소 반영(FR-5).</para>
/// </summary>
internal static class AnchorViewportClamp
{
    private const double COLLAPSE_EPS = 1e-7;   // 붕괴 시 중심 근방 반폭(≈1cm) — 잠금(비퇴화)
    // 남는 이동여유(inset)가 이 값 이하면 붕괴 처리. fit-zoom 부근에서 (a)폭 0 퇴화 사각형 방지
    //   (b)Mercator 위도변동(ViewArea.HeightLat이 위도별로 ~1e-7 흔들림)에 안 흔들리는 히스테리시스.
    //   ≈0.1m 상당 — 이 미만 이동여유는 실질 "잠김"으로 간주.
    private const double MIN_INSET = 1e-6;

    /// <summary>사이트(N/S/E/W)를 현재 뷰포트 절반만큼 안쪽으로 축소한 inset 경계를 반환.
    /// 축이 붕괴(뷰포트≥사이트)하면 그 축을 중심±ε으로(잠금). 디지털줌 보정(÷scale) 반영.</summary>
    internal static (double north, double south, double east, double west) InsetBounds(
        double north, double south, double east, double west,
        double viewWidthLng, double viewHeightLat, double digitalZoomScale)
    {
        double scale = digitalZoomScale > 0 ? digitalZoomScale : 1.0;
        double halfLng = (viewWidthLng / scale) / 2.0;
        double halfLat = (viewHeightLat / scale) / 2.0;
        var (w, e) = InsetAxis(west, east, halfLng);
        var (s, n) = InsetAxis(south, north, halfLat);
        return (n, s, e, w);
    }

    /// <summary>
    /// 지도 중심을 inset 경계로 클램프해 반환(줌 변경 후 중심을 새 구역 안으로 넣는 nudge용).
    /// 사이트 사각형이 퇴화면 입력 중심 그대로.
    /// </summary>
    internal static (double lat, double lng) ClampCenter(
        double centerLat, double centerLng,
        double north, double south, double east, double west,
        double viewWidthLng, double viewHeightLat, double digitalZoomScale)
    {
        if (east <= west || north <= south) return (centerLat, centerLng);   // 퇴화 구역 — 클램프 없음
        var (n, s, e, w) = InsetBounds(north, south, east, west, viewWidthLng, viewHeightLat, digitalZoomScale);
        double lng = centerLng < w ? w : (centerLng > e ? e : centerLng);
        double lat = centerLat < s ? s : (centerLat > n ? n : centerLat);
        return (lat, lng);
    }

    /// <summary>한 축 inset — 남는 이동여유(span-2·half)가 MIN_INSET 이하면 중심±ε으로 안정 붕괴(잠금·비퇴화),
    /// 아니면 [min+half, max-half]. 붕괴 조건에 MIN_INSET 여유를 둬 fit-zoom 경계에서 퇴화·요동을 막는다.</summary>
    private static (double lo, double hi) InsetAxis(double min, double max, double half)
    {
        double span = max - min;
        // span - 2·half = 남는 이동여유. 이게 MIN_INSET 이하(음수 포함)면 붕괴 → 중심 근방 안정 잠금.
        //   경계에서 '거의 0'인 non-degenerate inset(HeightLat≈0)을 만들지 않아 벤더 BoundsOfMap 오작동 차단.
        if (span - 2.0 * half <= MIN_INSET)
        {
            double c = (min + max) / 2.0;
            return (c - COLLAPSE_EPS, c + COLLAPSE_EPS);   // 뷰포트 ≥ 사이트(≈) → 중심 근방으로 붕괴 = 잠금
        }
        return (min + half, max - half);
    }
}
