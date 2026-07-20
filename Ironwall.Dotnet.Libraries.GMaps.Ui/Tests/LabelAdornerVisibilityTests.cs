using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>
/// LabelAdorner 라벨 렌더 가시성 게이트(<see cref="LabelAdorner.ShouldRenderLabel"/>) 회귀 테스트.
/// 버그: 개별 심볼 Visibility 언체크(ShowShape=false) 후 재시작 시 모양은 숨되 타이틀만 남던 현상.
/// 원인: LabelAdorner가 ShowShape를 안 보고 IsLayerEnabled(재시작 기본 true)만 봤음.
/// </summary>
public class LabelAdornerVisibilityTests
{
    // 정상 표시 기준값 — 각 테스트가 한 축씩 뒤집어 검증한다.
    private const bool NotDisposed = false;
    private const string Title = "제어기-01";
    private const bool ShowTitleOn = true;
    private const bool ShowShapeOn = true;
    private const bool NotPidsGroup = false;
    private const double MarkerZoom = 10d;
    private const bool LayerEnabled = true;
    private const double MapZoom = 15d;   // MarkerZoom 이상 → 줌 게이트 통과

    [Fact(DisplayName = "ShowShape=false(개별 OFF) → 라벨 숨김 (재시작 잔존 버그 수정)")]
    public void should_hide_label_when_showshape_false_after_restart()
    {
        // 재시작 상태 재현: IsLayerEnabled 기본 true, ShowTitle true, 줌 통과인데 ShowShape=false
        var render = LabelAdorner.ShouldRenderLabel(
            NotDisposed, Title, ShowTitleOn, /*showShape*/ false, NotPidsGroup, MarkerZoom, LayerEnabled, MapZoom);

        Assert.False(render);
    }

    [Fact(DisplayName = "ShowShape=true(개별 ON) → 라벨 표시")]
    public void should_show_label_when_showshape_true_and_visible()
    {
        var render = LabelAdorner.ShouldRenderLabel(
            NotDisposed, Title, ShowTitleOn, ShowShapeOn, NotPidsGroup, MarkerZoom, LayerEnabled, MapZoom);

        Assert.True(render);
    }

    [Fact(DisplayName = "PidsGroup은 ShowShape=false여도 라벨 표시(전멸 방지·정책 불변)")]
    public void should_keep_pidsgroup_label_when_showshape_false()
    {
        var render = LabelAdorner.ShouldRenderLabel(
            NotDisposed, Title, ShowTitleOn, /*showShape*/ false, /*isPidsGroup*/ true, MarkerZoom, LayerEnabled, MapZoom);

        Assert.True(render);
    }

    [Fact(DisplayName = "ShowTitle=false → 라벨 숨김(기존 규칙 유지)")]
    public void should_hide_label_when_showtitle_false()
    {
        var render = LabelAdorner.ShouldRenderLabel(
            NotDisposed, Title, /*showTitle*/ false, ShowShapeOn, NotPidsGroup, MarkerZoom, LayerEnabled, MapZoom);

        Assert.False(render);
    }

    [Fact(DisplayName = "IsLayerEnabled=false(카테고리 OFF) → 라벨 숨김")]
    public void should_hide_label_when_layer_disabled()
    {
        var render = LabelAdorner.ShouldRenderLabel(
            NotDisposed, Title, ShowTitleOn, ShowShapeOn, NotPidsGroup, MarkerZoom, /*isLayerEnabled*/ false, MapZoom);

        Assert.False(render);
    }

    [Fact(DisplayName = "현재 줌 < 심볼 최소 줌 → 라벨 숨김")]
    public void should_hide_label_when_zoom_below_marker_min()
    {
        var render = LabelAdorner.ShouldRenderLabel(
            NotDisposed, Title, ShowTitleOn, ShowShapeOn, NotPidsGroup, MarkerZoom, LayerEnabled, /*mapZoom*/ 5d);

        Assert.False(render);
    }

    [Fact(DisplayName = "제목 공백 → 라벨 숨김")]
    public void should_hide_label_when_title_blank()
    {
        var render = LabelAdorner.ShouldRenderLabel(
            NotDisposed, "   ", ShowTitleOn, ShowShapeOn, NotPidsGroup, MarkerZoom, LayerEnabled, MapZoom);

        Assert.False(render);
    }

    [Fact(DisplayName = "마커 Dispose 상태 → 라벨 숨김")]
    public void should_hide_label_when_marker_disposed()
    {
        var render = LabelAdorner.ShouldRenderLabel(
            /*isDisposed*/ true, Title, ShowTitleOn, ShowShapeOn, NotPidsGroup, MarkerZoom, LayerEnabled, MapZoom);

        Assert.False(render);
    }

    [Theory(DisplayName = "카테고리×개별 조합 — 둘 다 ON일 때만 표시(PidsGroup 아님)")]
    [InlineData(true, true, true)]     // 카테고리 ON, 개별 ON → 표시
    [InlineData(true, false, false)]   // 카테고리 ON, 개별 OFF → 숨김
    [InlineData(false, true, false)]   // 카테고리 OFF, 개별 ON → 숨김
    [InlineData(false, false, false)]  // 카테고리 OFF, 개별 OFF → 숨김
    public void should_render_only_when_category_and_individual_both_on(bool layerEnabled, bool showShape, bool expected)
    {
        var render = LabelAdorner.ShouldRenderLabel(
            NotDisposed, Title, ShowTitleOn, showShape, NotPidsGroup, MarkerZoom, layerEnabled, MapZoom);

        Assert.Equal(expected, render);
    }
}
