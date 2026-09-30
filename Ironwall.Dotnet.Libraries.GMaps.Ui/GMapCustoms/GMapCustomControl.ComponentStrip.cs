using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;

/// <summary>
/// 부품 칸 줄(L2) 밀도 판정 — component-display-unify FR-04 · NFR-02.
/// </summary>
/// <remarks>
/// <para>크게 보이는(칸 줄을 그릴) 아이콘이 한 화면에 30개를 넘으면 고장 · 선택 · 호버한 아이콘에만 줄을 붙인다.
/// 이 판정은 <b>뷰포트가 바뀔 때 한 번</b>(영역 변경 · 디지털 줌) 그리고 어떤 심볼의 부품 요약이 새로 들어왔을 때 한 번 센다 —
/// 프레임마다 세지 않는다. 결과는 지도 자신에게 상속 속성(<see cref="ComponentStatusOverlay.IsStripCrowdedProperty"/>)으로 걸어
/// 모든 아이콘의 부품 층이 받는다(값이 바뀔 때만 다시 그린다).</para>
/// <para>호출 스레드: UI. 요청은 <see cref="DispatcherPriority.Background"/> 로 합쳐 한 번만 센다.</para>
/// </remarks>
public partial class GMapCustomControl
{
    private bool _componentDensityQueued;

    /// <summary>밀도 판정을 예약한다 — 여러 번 불려도 한 번만 센다. 어느 스레드에서 불러도 된다.</summary>
    public void RequestComponentStripDensity()
    {
        if (_componentDensityQueued) return;
        _componentDensityQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new System.Action(() =>
        {
            _componentDensityQueued = false;
            RefreshComponentStripDensity();
        }));
    }

    /// <summary>지금 뷰포트에서 칸 줄 대상 아이콘을 세고 밀집 여부를 건다. 호출 스레드: UI.</summary>
    internal void RefreshComponentStripDensity()
    {
        try
        {
            if (Markers == null) return;
            var area = ViewArea;
            var scale = DigitalZoomScale;
            int count = 0;
            foreach (var marker in Markers.OfType<GMapPidsMarker>())
            {
                if (!marker.IsVisible) continue;
                var strip = marker.ComponentSummary.Strip;
                if (strip.IsEmpty) continue;
                if (!ComponentStripRules.Qualifies(strip, ComponentBadgeLod.ScreenPixels(marker.Width, marker.Height, scale))) continue;
                if (!area.IsEmpty && !area.Contains(marker.Position)) continue;
                if (++count > ComponentStripRules.CrowdedThreshold) break;   // 넘었는지만 알면 된다
            }

            var crowded = ComponentStripRules.IsCrowded(count);
            if (ComponentStatusOverlay.GetIsStripCrowded(this) != crowded)
                ComponentStatusOverlay.SetIsStripCrowded(this, crowded);
        }
        catch (Exception ex)
        {
            _log?.Error($"부품 칸 줄 밀도 판정 실패(격리): {ex.Message}");
        }
    }
}
