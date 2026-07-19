namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/****************************************************************************
   Purpose      : 앵커 뷰포트-락 하드월 — 드래그 중심 좌표를 BoundsOfMap(inset) 경계 안으로 클램프
                  (WPF/GMap 무의존, 순수 산술 → 헤드리스 단위 테스트 가능)
   Created By   : GHLee
   Created On   : 7/19/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 사이트 고정(앵커) 상태에서 맵 드래그가 <c>BoundsOfMap</c>(inset) 경계를 넘어가면, 벤더의
/// "경계 밖이면 드래그 스킵(→ 1프레임 오버슈트 후 마우스업 스냅백=튕김)" 대신 중심 좌표를 경계
/// 안쪽으로 클램프해 맵을 벽처럼 매끄럽게 정지시킨다. 이 클래스는 클램프의 <b>순수 좌표 계산</b>만
/// 담당하며, 실제 <c>RenderOffset</c> 시프트는 벤더 <c>GMapControl.ClampDragToBounds</c>가 이 규약을
/// 인라인 미러링한다(벤더는 앱 어셈블리를 참조할 수 없어 로직을 복제 — 두 곳을 함께 유지).
/// <para><c>RectLatLng.Contains</c>는 <b>반열림</b>이다: <c>Left ≤ lng &lt; Right</c>,
/// <c>Bottom &lt; lat ≤ Top</c>. 따라서 열린 모서리(Right, Bottom)는 <see cref="EDGE_EPS"/>만큼
/// 안쪽에 둬야 다음 프레임 <c>Contains</c>가 true를 유지해 경계에서 진동(shimmer)하지 않는다.</para>
/// </summary>
internal static class BoundaryLatLngClamp
{
    /// <summary>반열림 모서리(Right·Bottom) 안쪽 여유. 위경도 도(度) 단위 — 서브밀리미터라 시각 영향 없음.</summary>
    internal const double EDGE_EPS = 1e-9;

    /// <summary>
    /// <paramref name="lat"/>,<paramref name="lng"/>를 반열림 경계 <c>[left, right) × (bottom, top]</c>
    /// 안으로 클램프한다. 위반한 축만 조정하고 이미 안쪽인 축은 보존한다.
    /// </summary>
    /// <param name="left">서쪽 경도(Lng)</param>
    /// <param name="right">동쪽 경도(Lng+WidthLng)</param>
    /// <param name="top">북쪽 위도(Lat)</param>
    /// <param name="bottom">남쪽 위도(Lat-HeightLat)</param>
    /// <returns>경계 안쪽으로 클램프된 (lat, lng)</returns>
    internal static (double lat, double lng) Clamp(
        double lat, double lng, double left, double right, double top, double bottom)
    {
        // 경도: [left, right) — right는 열림이라 안쪽 epsilon
        double clampedLng = lng < left ? left
                          : lng >= right ? right - EDGE_EPS
                          : lng;

        // 위도: (bottom, top] — bottom은 열림이라 안쪽 epsilon
        double clampedLat = lat > top ? top
                          : lat <= bottom ? bottom + EDGE_EPS
                          : lat;

        return (clampedLat, clampedLng);
    }
}
