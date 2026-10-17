using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>
/// LabelAdorner 라벨 렌더 가시성 게이트(<see cref="LabelAdorner.ShouldRenderLabel"/>) 회귀 테스트.
/// 3조건 모델: 제목 표시 = ShowTitle(속성창 조건3) &amp;&amp; 줌 &amp;&amp; IsLayerEnabled(=카테고리 &amp;&amp; 레이어 마스터 Visible).
/// 레이어 마스터(Visible) OFF는 IsLayerEnabled=false로 합성되어 라벨이 함께 숨는다(재시작 후 타이틀 잔존 버그 수정).
/// ShowShape(속성창 모양)는 게이트에서 제외 — ShowShape=false·ShowTitle=true면 제목만 표시(title-only) 보존.
/// </summary>
public class LabelAdornerVisibilityTests
{
    // 정상 표시 기준값 — 각 테스트가 한 축씩 뒤집어 검증한다.
    private const bool NotDisposed = false;
    private const string Title = "제어기-01";
    private const bool ShowTitleOn = true;
    private const double MarkerZoom = 10d;
    private const bool MasterOn = true;    // IsLayerEnabled = 카테고리 && Visible
    private const double MapZoom = 15d;    // MarkerZoom 이상 → 줌 게이트 통과

    [Fact(DisplayName = "레이어 마스터 OFF(IsLayerEnabled=false) → 라벨 숨김 (재시작 타이틀 잔존 버그 수정)")]
    public void should_hide_label_when_layer_master_off()
    {
        var render = LabelAdorner.ShouldRenderLabel(NotDisposed, Title, ShowTitleOn, MarkerZoom, /*isLayerEnabled*/ false, MapZoom);
        Assert.False(render);
    }

    [Fact(DisplayName = "마스터 ON + ShowTitle ON + 줌 통과 → 라벨 표시")]
    public void should_show_label_when_master_on_and_showtitle_on()
    {
        var render = LabelAdorner.ShouldRenderLabel(NotDisposed, Title, ShowTitleOn, MarkerZoom, MasterOn, MapZoom);
        Assert.True(render);
    }

    [Fact(DisplayName = "ShowTitle=false → 라벨 숨김(제목 미표시, 조건3). ShowShape 무관하게 title 축만 봄")]
    public void should_hide_label_when_showtitle_false()
    {
        var render = LabelAdorner.ShouldRenderLabel(NotDisposed, Title, /*showTitle*/ false, MarkerZoom, MasterOn, MapZoom);
        Assert.False(render);
    }

    [Fact(DisplayName = "현재 줌 < 심볼 최소 줌 → 라벨 숨김")]
    public void should_hide_label_when_zoom_below_marker_min()
    {
        var render = LabelAdorner.ShouldRenderLabel(NotDisposed, Title, ShowTitleOn, MarkerZoom, MasterOn, /*mapZoom*/ 5d);
        Assert.False(render);
    }

    [Fact(DisplayName = "제목 공백 → 라벨 숨김")]
    public void should_hide_label_when_title_blank()
    {
        var render = LabelAdorner.ShouldRenderLabel(NotDisposed, "   ", ShowTitleOn, MarkerZoom, MasterOn, MapZoom);
        Assert.False(render);
    }

    [Fact(DisplayName = "마커 Dispose 상태 → 라벨 숨김")]
    public void should_hide_label_when_marker_disposed()
    {
        var render = LabelAdorner.ShouldRenderLabel(/*isDisposed*/ true, Title, ShowTitleOn, MarkerZoom, MasterOn, MapZoom);
        Assert.False(render);
    }

    [Theory(DisplayName = "마스터 × 제목 조합 — 둘 다 ON일 때만 라벨 표시")]
    [InlineData(true, true, true)]     // 마스터 ON, 제목 ON → 표시
    [InlineData(true, false, false)]   // 마스터 ON, 제목 OFF → 숨김
    [InlineData(false, true, false)]   // 마스터 OFF, 제목 ON → 숨김(마스터가 이김)
    [InlineData(false, false, false)]  // 마스터 OFF, 제목 OFF → 숨김
    public void should_render_only_when_master_and_showtitle_both_on(bool isLayerEnabled, bool showTitle, bool expected)
    {
        var render = LabelAdorner.ShouldRenderLabel(NotDisposed, Title, showTitle, MarkerZoom, isLayerEnabled, MapZoom);
        Assert.Equal(expected, render);
    }
}
