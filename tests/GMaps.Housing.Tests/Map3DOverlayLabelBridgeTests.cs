using System.Windows;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// 3D 이름표가 2D 와 어긋나던 다리 결함(2026-10-22 사용자 보고 — "구역이나 영역 잠금 처리한게 Label 이 3D 에서 안 보이는 것 같다" 분석):
/// 선 · 구역에 label 이 없어 3D 가 ShowTitle 을 모르고 늘 보였다 · 이름표 자리(점 offset · 선 anchor + 지리 자리) · 모양 끔(showShape) ·
/// 투명 배경("transparent") · 글꼴 · 기울임 · 최대 폭 · 도형 opacity · 2D 에서 꼭짓점을 고치면 3D 에 pts 가 다시 간다.
/// 다리의 JSON 만 본다(BuildItem · 스냅샷 · patch) — 창 없음. 이 파일은 새 API 를 부르지 않아 고치기 전 코드로도 컴파일된다.
/// </summary>
public class Map3DOverlayLabelBridgeTests
{
    private static ILogService Log => Mock.Of<ILogService>();

    private static LineSymbolModel LineModel(bool showTitle) => new()
    {
        Id = 51, Title = "외곽 구역선", ShowTitle = showTitle, Latitude = 37.05, Longitude = 127.05, LabelOffsetX = 0.5, LabelOffsetY = 0.2,
        TitleSize = 14, TitleColor = EnumColorType.White, TitleBackground = EnumColorType.Transparent, TitleBold = true,
        TitleFontFamily = "맑은 고딕", TitleItalic = true, TitleMaxWidth = 0, StrokeColor = EnumColorType.Blue, StrokeThickness = 2,
        LinePoints = new List<GeoPoint> { new(37.0, 127.0), new(37.1, 127.0), new(37.1, 127.1) },
    };

    [Fact]
    public void should_send_line_label_with_show_false_when_title_hidden_and_2d_place() => HousingTests.Sta(() =>
    {
        var hidden = Map3DOverlayBridge.BuildItem(new GMapLineMarker(Log, LineModel(showTitle: false)))!;
        var label = (JObject)hidden["label"]!;                                         // 고치기 전: 선에는 label 이 없어 3D 가 이름을 늘 보였다
        Assert.False(label.Value<bool>("show"));
        Assert.Equal("외곽 구역선", label.Value<string>("text"));
        Assert.Equal("transparent", label.Value<string>("bg"));
        Assert.Equal(("맑은 고딕", true, 200d), (label.Value<string>("font"), label.Value<bool>("italic"), label.Value<double>("maxWidth")));   // 0 = 2D 기본 폭 200
        Assert.Equal((0.5, 0.2), (label["anchor"]!.Value<double>("u"), label["anchor"]!.Value<double>("v")));
        // 2D 자리 = 기준점 + (U · 반폭, 아래로 (1 + V) · 반높이) — 반폭 · 반높이 0.05°
        Assert.Equal(127.075, label.Value<double>("lon"), 9);
        Assert.Equal(36.99, label.Value<double>("lat"), 9);
        Assert.Null(label["offset"]);

        Assert.True(Map3DOverlayBridge.BuildItem(new GMapLineMarker(Log, LineModel(showTitle: true)))!["label"]!.Value<bool>("show"));
    });

    [Fact]
    public void should_send_zone_label_and_follow_show_title() => HousingTests.Sta(() =>
    {
        var model = new PidsGroupSymbolModel
        {
            Id = 61, Title = "A 구역", ShowTitle = false, Latitude = 37.0, Longitude = 127.001, IsLocked = true,   // 기준점 = 선 가운데 · 잠금은 이름표와 무관
            LinePoints = new List<GeoPoint> { new(37.0, 127.0), new(37.0, 127.002) },
        };
        var zone = Map3DOverlayBridge.BuildItem(new GMapPidsGroupMarker(Log, model))!;
        Assert.Equal("pidsGroup", zone.Value<string>("kind"));
        Assert.False(zone["label"]!.Value<bool>("show"));
        Assert.Equal("A 구역", zone["label"]!.Value<string>("text"));
        Assert.Equal(127.001, zone["label"]!.Value<double>("lon"), 9);   // 가운데(U 0) · 반높이 0 → 선 위
    });

    [Fact]
    public void should_send_show_shape_opacity_and_point_label_offset_like_2d() => HousingTests.Sta(() =>
    {
        // 247 · 248 처럼 모양 표시를 끈 도형(이름표만) · 투명도 0.4 · 이름표를 옮겨 둠
        var titleOnly = new GMapGeometricMarker(Log, new GeometricSymbolModel
        {
            Id = 247, Title = "정문 초소", ShowShape = false, ShowTitle = true, Opacity = 0.4, LabelOffsetX = 12, LabelOffsetY = -6,
            TitleBackground = EnumColorType.Black, TitleMaxWidth = 1000, Visible = true,
        });
        var g = Map3DOverlayBridge.BuildItem(titleOnly)!;
        Assert.False(g.Value<bool>("showShape"));
        Assert.Equal(0.4, g.Value<double>("opacity"), 6);
        Assert.Equal((12d, -6d), (g["label"]!["offset"]!.Value<double>("x"), g["label"]!["offset"]!.Value<double>("y")));
        Assert.Equal(800, g["label"]!.Value<double>("maxWidth"));                       // 2D 와 같은 상한
        Assert.StartsWith("#CD", g["label"]!.Value<string>("bg"));
        Assert.Null(g["label"]!["anchor"]);

        var plain = Map3DOverlayBridge.BuildItem(new GMapGeometricMarker(Log, new GeometricSymbolModel { Id = 248, Title = "기본", ShowShape = true, Visible = true }))!;
        Assert.True(plain.Value<bool>("showShape"));                                   // 켜짐도 싣는다(바뀔 때 patch 로 가게)
        var military = Map3DOverlayBridge.BuildItem(new GMapMilitarySymbolMarker(Log, new MilitarySymbolModel { Id = 2, Title = "부대", ShowShape = false, Visible = true }))!;
        Assert.False(military.Value<bool>("showShape"));
        Assert.Null(military["opacity"]);                                              // 투명도는 도형만
    });

    [Fact]
    public void should_send_new_points_to_3d_when_line_vertex_is_edited_in_2d() => HousingTests.Sta(() =>
    {
        var map = new GMapCustomControl();
        var line = new GMapLineMarker(Log, LineModel(showTitle: true));
        HousingTests.Layout(new FrameworkElement(), 1, 1);
        map.Markers.Add(line);
        var posts = new List<string>();
        var bridge = new Map3DOverlayBridge(posts.Add, null);
        bridge.Start(map);
        posts.Clear();

        line.UpdatePoint(2, new PointLatLng(37.2, 127.1));                               // 2D 꼭짓점 끌기와 같은 경로
        typeof(Map3DOverlayBridge).GetMethod("Flush", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(bridge, null);   // 50 ms 모음 타이머 대신

        var patch = posts.Select(JObject.Parse).Single(m => m.Value<string>("type") == "overlay.patch");
        Assert.Equal(("line", 51), (patch.Value<string>("kind"), patch.Value<int>("id")));
        Assert.Equal(37.2, patch["set"]!["pts"]![2]![0]!.Value<double>(), 9);
        bridge.Stop();
    });
}
