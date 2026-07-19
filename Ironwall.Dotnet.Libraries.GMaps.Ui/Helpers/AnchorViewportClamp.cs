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
/// 지도 중심을 사이트 사각형 안에 "뷰포트 전체가 들어가도록" 클램프한다(중심-가두기가 아니라 뷰포트-가두기).
/// <para>축별 규칙: 뷰포트가 사이트보다 크거나 같으면 중심을 사이트 중심에 고정(이동 0 = 딱 맞는 줌에서 패닝 잠금),
/// 작으면 (사이트 ⊖ 뷰포트 절반)으로 클램프해 뷰포트가 경계를 절대 안 넘게 한다.</para>
/// <para>디지털 줌(17+)은 실제 보이는 영역이 코어 뷰포트보다 작으므로 뷰포트를 ÷scale로 축소 반영한다(FR-5).</para>
/// </summary>
internal static class AnchorViewportClamp
{
    /// <summary>
    /// 클램프된 중심(lat, lng)을 반환. 사이트 사각형이 퇴화(east≤west 또는 north≤south)면 입력 중심을 그대로 돌려준다.
    /// </summary>
    /// <param name="centerLat">현재 중심 위도</param>
    /// <param name="centerLng">현재 중심 경도</param>
    /// <param name="north">사이트 북(위) 위도</param>
    /// <param name="south">사이트 남(아래) 위도</param>
    /// <param name="east">사이트 동(우) 경도</param>
    /// <param name="west">사이트 서(좌) 경도</param>
    /// <param name="viewWidthLng">뷰포트 경도 폭(코어)</param>
    /// <param name="viewHeightLat">뷰포트 위도 높이(코어)</param>
    /// <param name="digitalZoomScale">디지털 줌 배율(1.0=없음, 1.25=17+ 등). ≤0이면 1.0 취급</param>
    internal static (double lat, double lng) ClampCenter(
        double centerLat, double centerLng,
        double north, double south, double east, double west,
        double viewWidthLng, double viewHeightLat, double digitalZoomScale)
    {
        if (east <= west || north <= south) return (centerLat, centerLng);   // 퇴화 구역 — 클램프 없음

        double scale = digitalZoomScale > 0 ? digitalZoomScale : 1.0;
        double halfLng = (viewWidthLng / scale) / 2.0;   // 디지털줌 보정 후 뷰포트 반-폭
        double halfLat = (viewHeightLat / scale) / 2.0;

        double lng = ClampAxis(centerLng, west, east, halfLng);
        double lat = ClampAxis(centerLat, south, north, halfLat);
        return (lat, lng);
    }

    /// <summary>한 축 클램프 — 뷰포트(2·half)가 사이트 폭(max-min) 이상이면 중심 고정(잠금),
    /// 아니면 [min+half, max-half]로 클램프.</summary>
    private static double ClampAxis(double center, double min, double max, double half)
    {
        double siteSpan = max - min;
        if (2.0 * half >= siteSpan) return (min + max) / 2.0;   // 뷰포트 ≥ 사이트 → 중심 고정
        double lo = min + half, hi = max - half;
        return center < lo ? lo : (center > hi ? hi : center);
    }
}
