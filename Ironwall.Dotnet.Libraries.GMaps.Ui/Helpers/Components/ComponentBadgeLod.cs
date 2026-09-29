namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;

/// <summary>
/// 부품 배지 · 문 표시의 상세도(LOD) — <b>마커가 화면에 그려진 픽셀 크기</b>로 정한다(순수 함수).
/// </summary>
/// <remarks>
/// <para>이 앱의 마커는 지도 줌과 무관하게 사용자가 정한 픽셀 크기로 그려지고, 디지털 줌(×1.0~2.0)만 오버레이를 키운다.
/// 그래서 줌 숫자가 아니라 "화면에서 몇 px 인가"로 가른다 — 작게 그려진 아이콘에 12px 배지를 얹으면 아이콘을 덮는다
/// (선례: <c>Helpers/Fence/FenceLod.cs</c> 도 픽셀 기준). 심볼 자체의 최소 줌 게이트는 그 바깥에서 이미 걸려 있다.</para>
/// </remarks>
public static class ComponentBadgeLod
{
    /// <summary>이 크기(px, 짧은 변 × 디지털 배율) 미만이면 부품 층을 그리지 않는다.</summary>
    public const double MinMarkerPixels = 24.0;

    /// <summary>화면 픽셀 = 마커 짧은 변 × 화면 배율(디지털 줌). 배율이 0 이하 · 비정상이면 1 로 본다.</summary>
    public static double ScreenPixels(double width, double height, double screenScale)
    {
        var side = Math.Min(width, height);
        if (double.IsNaN(side) || side <= 0) return 0;
        var scale = double.IsNaN(screenScale) || double.IsInfinity(screenScale) || screenScale <= 0 ? 1.0 : screenScale;
        return side * scale;
    }

    /// <summary>건강 배지를 그리는가 — 고장 · 저하이고 마커가 충분히 클 때만.</summary>
    public static bool ShowsBadge(ComponentHealthLevel health, double screenPixels)
        => health is ComponentHealthLevel.Fault or ComponentHealthLevel.Degraded && screenPixels >= MinMarkerPixels;

    /// <summary>문 표시를 그리는가.</summary>
    public static bool ShowsDoor(DoorIndicatorKind kind, double screenPixels)
        => kind != DoorIndicatorKind.None && screenPixels >= MinMarkerPixels;
}
