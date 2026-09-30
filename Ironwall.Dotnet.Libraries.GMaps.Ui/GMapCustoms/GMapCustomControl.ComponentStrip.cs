using System.Collections.Specialized;
using System.ComponentModel;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;

/// <summary>
/// 부품 칸 줄(L2) 밀도 판정 — component-display-unify FR-04 · NFR-02.
/// </summary>
/// <remarks>
/// <para>크게 보이는(칸 줄을 그릴) 아이콘이 한 화면에 30개를 넘으면 고장 · 선택 · 호버한 아이콘에만 줄을 붙인다.
/// 이 판정은 <b>뷰포트가 바뀔 때 한 번</b>(영역 변경 · 디지털 줌), 그리고 어떤 심볼의 부품 요약이 새로 들어왔을 때(SYNC_DEVICE)
/// 한 번 센다 — 프레임마다 세지 않는다.</para>
/// <para><b>값의 흐름</b>: 지도 → 각 PIDS 심볼(<see cref="GMapPidsMarker.ComponentStripCrowded"/>) → 마커 컨트롤 → 부품 층.
/// 값이 <b>바뀔 때만</b> 심볼들에 내려 준다(O(N) 은 전환 때 한 번). 새로 들어온 심볼은 들어올 때 현재 값을 받는다.
/// 시각 트리 상속에 기대지 않는다(마커 컨테이너가 아직 없어도 값이 맞다).</para>
/// <para>크기 판정은 칸 줄 그리기와 <b>같은 함수</b>(<see cref="ComponentBadgeLod.MarkerScreenPixels"/>: 심볼 크기 × 디지털 배율)를 쓴다.</para>
/// <para>호출 스레드: UI. 요청은 <see cref="DispatcherPriority.Background"/> 로 합쳐 한 번만 센다.</para>
/// </remarks>
public partial class GMapCustomControl
{
    private bool _componentDensityQueued;
    private bool _componentStripCrowded;
    private readonly HashSet<GMapPidsMarker> _componentWatched = new();

    /// <summary>지금 밀집 판정(시험 · 진단).</summary>
    public bool IsComponentStripCrowded => _componentStripCrowded;

    /// <summary>밀도 판정을 예약한다 — 여러 번 불려도 한 번만 센다.</summary>
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

    /// <summary>지금 뷰포트에서 칸 줄 대상 아이콘을 세고, 밀집 여부가 바뀌었으면 모든 PIDS 심볼에 내려 준다. 호출 스레드: UI.</summary>
    internal void RefreshComponentStripDensity()
    {
        try
        {
            if (Markers == null) return;
            var area = ViewArea;
            // 크기가 아직 없는 지도(배치 전 · 헤드리스)는 영역이 퇴화한다 — 그때는 영역으로 거르지 않는다(전부 센다).
            var hasArea = !area.IsEmpty && area.WidthLng > 0 && area.HeightLat > 0;
            var scale = DigitalZoomScale;
            int count = 0;
            foreach (var marker in Markers.OfType<GMapPidsMarker>())
            {
                if (!marker.IsVisible) continue;
                var strip = marker.ComponentSummary.Strip;
                if (strip.IsEmpty) continue;
                if (!ComponentStripRules.Qualifies(strip, ComponentBadgeLod.MarkerScreenPixels(marker.Width, marker.Height, scale))) continue;
                if (hasArea && !area.Contains(marker.Position)) continue;
                if (++count > ComponentStripRules.CrowdedThreshold) break;   // 넘었는지만 알면 된다
            }
            ApplyComponentStripCrowded(ComponentStripRules.IsCrowded(count));
        }
        catch (Exception ex)
        {
            _log?.Error($"부품 칸 줄 밀도 판정 실패(격리): {ex.Message}");
        }
    }

    private void ApplyComponentStripCrowded(bool crowded)
    {
        if (_componentStripCrowded == crowded) return;
        _componentStripCrowded = crowded;
        foreach (var marker in Markers.OfType<GMapPidsMarker>()) marker.ComponentStripCrowded = crowded;
    }

    /// <summary>
    /// 마커 컬렉션 변화 — 들어온 PIDS 심볼은 현재 밀집 값을 받고 부품 요약 통지를 구독한다(SYNC_DEVICE 로 칸 줄이 생기거나 사라지면 다시 센다).
    /// </summary>
    private void TrackComponentStripMarkers(NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var old in _componentWatched) old.PropertyChanged -= OnWatchedMarkerPropertyChanged;
            _componentWatched.Clear();
            foreach (var marker in Markers.OfType<GMapPidsMarker>()) Watch(marker);
        }
        else
        {
            foreach (var old in e.OldItems?.OfType<GMapPidsMarker>() ?? Enumerable.Empty<GMapPidsMarker>())
                if (_componentWatched.Remove(old)) old.PropertyChanged -= OnWatchedMarkerPropertyChanged;
            foreach (var added in e.NewItems?.OfType<GMapPidsMarker>() ?? Enumerable.Empty<GMapPidsMarker>())
                Watch(added);
        }
        RequestComponentStripDensity();
    }

    private void Watch(GMapPidsMarker marker)
    {
        marker.ComponentStripCrowded = _componentStripCrowded;
        if (_componentWatched.Add(marker)) marker.PropertyChanged += OnWatchedMarkerPropertyChanged;
    }

    private void OnWatchedMarkerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GMapPidsMarker.ComponentSummary) or nameof(IEditableMarker.IsVisible)
            or nameof(IEditableMarker.Width) or nameof(IEditableMarker.Height))
            RequestComponentStripDensity();
    }
}
