using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using Moq;
using Xunit;

namespace GMaps.PropertyPanel.Tests;

/****************************************************************************
   Purpose      : 카탈로그 MAP-126(속성 · 기반시설) · MAP-127(속성 · 도형) · MAP-129(속성 · 울타리) 헤드리스 짝(W3, 2026-10-11).
                  VM 의 실제 속성창 경로(ShowPropertyPanel) → 앱 스타일의 진짜 슬라이더 · 콤보 · 체크 상자 → 마커 → 지도 심볼(3D 장면 기하 ·
                  픽셀) · 가짜 지도 DB(Update 호출 인자). 창은 띄우지 않는다.
                  울타리 3D 는 배포 플래그(Symbol3D.IsEnabled — 프로세스당 1회 appsettings 읽기, 시험 bin 에는 없어 false)와 LOD 게이트를
                  지난 본문(GMapMarkerPidsGroup3DControl.ApplyFrame)을, 컨트롤이 마커에서 받은 값(PostSpacingM · FenceHeightM · FenceMode ·
                  Render3D)으로 만든 프레임으로 구동한다 — 게이트 판정식(FenceLod.Show3D)도 같은 함수를 쓴다.
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class InfraShapeFencePropertyScenarioTests
{
    // ───────────────────────── MAP-126 기반시설: 층수 없는 종류는 층 꺼짐 ─────────────────────────

    private const string InfraUpdate = "UpdateInfraSymbolAsync";

    private static (GMapInfraMarker Marker, GMapMarkerInfra3DControl Control, HousingVisual Housing) PlaceInfra(Property3DRig rig, EnumBuildingType type, int floors)
    {
        var model = new InfraSymbolModel
        {
            Id = 1260, Title = "건물-1260", Latitude = Property3DRig.HomeLat, Longitude = Property3DRig.HomeLng, Width = 60, Height = 60,
            FillColor = EnumColorType.Blue, StrokeColor = EnumColorType.Black, StrokeThickness = 1, ShowShape = true, ShowTitle = true, Visible = true,
            OperationState = EnumOperationState.ACTIVATED, BuildingType = type, BuildingUsage = EnumBuildingUsage.Office, FloorCount = floors, BasementFloorCount = 0, BuildingArea = 100,
        };
        var marker = new GMapInfraMarker(Mock.Of<ILogService>(), model) { IsVisible = true };
        var control = new GMapMarkerInfra3DControl(marker);   // 앱의 Symbol3D ON 경로(GMapInfraMarker.CreateMarkerControl)와 같은 형식
        control.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/Housing3DMarkerStyle.xaml", UriKind.Relative) });
        control.Style = (Style)control.Resources[typeof(GMapMarkerInfra3DControl)];
        marker.Shape = control;
        rig.Map.Markers.Add(marker);
        rig.LayoutMap();
        AppHost.Layout(control, 60, 60);
        return (marker, control, Property3DRig.Part<HousingVisual>(control, "PART_Housing3D"));
    }

    private static void SelectBuilding(ComboBox combo, EnumBuildingType type)
    {
        combo.SelectedItem = type;
        Property3DRig.Pump();
    }

    [Fact]
    [Trait("Scenario", "MAP-126")]
    public void should_disable_floor_sliders_and_hide_floor_text_when_building_type_has_no_floors() => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        var (marker, control, housing) = PlaceInfra(rig, EnumBuildingType.Factory, 5);
        var panel = rig.OpenPanel<GMapPropertyInfraControl>(marker, "InfraPropertyStyle.xaml");
        var typeCombo = Property3DRig.ByLabel<ComboBox>(panel, "건물종류");
        var floors = Property3DRig.ByLabel<Slider>(panel, "지상층");
        var basement = Property3DRig.ByLabel<Slider>(panel, "지하층");
        var floorText = Property3DRig.Part<TextBlock>(control, "PART_FloorText");

        // 공장(층수 있음): 슬라이더 켜짐 · 마커에 F5 · 층수만큼 높은 3D
        Assert.True(floors.IsEnabled && basement.IsEnabled);
        Assert.Equal(Visibility.Visible, floorText.Visibility);
        Assert.Equal("F5", floorText.Text.Trim());
        double h5 = Property3DRig.SceneBounds(housing).SizeY;

        floors.Value = 8;                                              // 진짜 슬라이더
        Property3DRig.Pump();
        Assert.Equal(8, marker.FloorCount);
        Assert.Equal(8, housing.FloorCount);
        Assert.True(Property3DRig.SceneBounds(housing).SizeY > h5, "층수를 올렸는데 3D 높이가 그대로다");
        Assert.Equal(8, (int)rig.Last(InfraUpdate)["FloorCount"]!);   // DB(InfraRow) FloorCount

        // 게이트(층수 없음)로 — 슬라이더 꺼짐 · 층 글 숨김 · 3D 모델 교체 · DB BuildingType
        SelectBuilding(typeCombo, EnumBuildingType.Gate);
        Assert.Equal(EnumBuildingType.Gate, marker.BuildingType);
        Assert.False(panel.SupportsFloors);
        Assert.False(floors.IsEnabled, "게이트인데 지상층 슬라이더가 켜져 있다");
        Assert.False(basement.IsEnabled, "게이트인데 지하층 슬라이더가 켜져 있다");
        Assert.Equal(Visibility.Collapsed, floorText.Visibility);
        Assert.Equal("infra.gate", housing.ModelKey);
        Assert.Equal(nameof(EnumBuildingType.Gate), (string?)rig.Last(InfraUpdate)["BuildingType"]);

        // 다시 감시탑(층수 있음) — 켜짐 · 글 보임
        SelectBuilding(typeCombo, EnumBuildingType.Watchtower);
        Assert.True(floors.IsEnabled && basement.IsEnabled);
        Assert.Equal(Visibility.Visible, floorText.Visibility);
        Assert.Equal("infra.watchtower", housing.ModelKey);
    });

    [Theory]
    [Trait("Scenario", "MAP-126")]
    [InlineData(EnumBuildingType.Factory, true)]
    [InlineData(EnumBuildingType.Barracks, true)]
    [InlineData(EnumBuildingType.GuardPost, true)]
    [InlineData(EnumBuildingType.Watchtower, true)]
    [InlineData(EnumBuildingType.Warehouse, true)]
    [InlineData(EnumBuildingType.Antenna, true)]
    [InlineData(EnumBuildingType.PowerPole, true)]
    [InlineData(EnumBuildingType.Gate, false)]
    [InlineData(EnumBuildingType.Generator, false)]
    [InlineData(EnumBuildingType.WaterTank, false)]
    [InlineData(EnumBuildingType.Helipad, false)]
    [InlineData(EnumBuildingType.Bridge, false)]
    public void should_gate_floor_rows_and_marker_floor_text_by_building_type_when_selected_in_panel(EnumBuildingType type, bool hasFloors) => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        var start = type == EnumBuildingType.Factory ? EnumBuildingType.Barracks : EnumBuildingType.Factory;
        var (marker, control, housing) = PlaceInfra(rig, start, 4);
        var panel = rig.OpenPanel<GMapPropertyInfraControl>(marker, "InfraPropertyStyle.xaml");
        var floorText = Property3DRig.Part<TextBlock>(control, "PART_FloorText");

        SelectBuilding(Property3DRig.ByLabel<ComboBox>(panel, "건물종류"), type);

        Assert.Equal(type, marker.BuildingType);
        Assert.Equal(hasFloors, Property3DRig.ByLabel<Slider>(panel, "지상층").IsEnabled);
        Assert.Equal(hasFloors, Property3DRig.ByLabel<Slider>(panel, "지하층").IsEnabled);
        Assert.Equal(hasFloors ? Visibility.Visible : Visibility.Collapsed, floorText.Visibility);   // 4층 — 층수 있는 종류만 'F4'
        Assert.Equal("infra." + type.ToString().ToLowerInvariant(), housing.ModelKey);
        Assert.Equal(type.ToString(), (string?)rig.Last(InfraUpdate)["BuildingType"]);
    });

    [Fact]
    [Trait("Defect", "P3D-D1")]
    public void should_retint_3d_building_when_infra_color_intensity_slider_moves() => AppHost.Run(() =>
    {
        // P3D-D1 의 두 번째 자리(InfraPropertyStyle.xaml:44) — PIDS 와 같은 바인딩 경로 문자열
        var rig = Property3DRig.Create();
        var (marker, control, housing) = PlaceInfra(rig, EnumBuildingType.Factory, 3);
        var panel = rig.OpenPanel<GMapPropertyInfraControl>(marker, "InfraPropertyStyle.xaml");
        var slider = Property3DRig.ByLabel<Slider>(panel, "색상 강도");
        var be = System.Windows.Data.BindingOperations.GetBindingExpression(slider, RangeBase.ValueProperty)!;
        int writes = rig.CountOf(InfraUpdate);
        slider.Value = 1.0;
        Property3DRig.Pump();
        Assert.True(Math.Abs(HousingAppearance.GetTintStrength(control) - 1.0) < 1e-3,
            $"결함 P3D-D1: 건물 속성창 '색상 강도' 슬라이더 1.0 인데 3D 건물 색상 강도 {HousingAppearance.GetTintStrength(control):F2} — 바인딩 상태={be.Status} · 경로='{be.ParentBinding.Path.Path}'");
        Assert.Equal(1.0, HousingAppearance.GetTintStrength(housing), 3);
        Assert.Equal(writes, rig.CountOf(InfraUpdate));                 // 실행 중 표시 설정 — DB 쓰기 없음
    });

    // ───────────────────────── MAP-127 도형: 모양 · 투명도 ─────────────────────────

    private const string GeoUpdate = "UpdateGeometrySymbolAsync";

    private static GMapGeometricMarker NewShape(EnumShapeType shape, int id = 1270, double opacity = 1.0)
    {
        var marker = new GMapGeometricMarker(Mock.Of<ILogService>(), new GeometricSymbolModel
        {
            Id = id, Title = $"도형-{shape}", Latitude = Property3DRig.HomeLat, Longitude = Property3DRig.HomeLng, Width = 40, Height = 40, TitleSize = 12,
            FillColor = EnumColorType.Blue, StrokeColor = EnumColorType.Black, StrokeThickness = 1, ShapeType = shape, Opacity = opacity,
            ShowShape = true, ShowTitle = false, Visible = true, OperationState = EnumOperationState.ACTIVATED,
        });
        var control = Assert.IsType<GMapGeometricMarkerControl>(marker.Shape);
        control.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/GeometricMarkerStyle.xaml", UriKind.Relative) });
        control.Style = (Style)control.Resources[typeof(GMapGeometricMarkerControl)];
        return marker;
    }

    /// <summary>40×40 렌더(투명 바탕) — 픽셀 배열(Pbgra).</summary>
    private static byte[] Render(FrameworkElement control)
    {
        AppHost.Layout(control, 40, 40);
        var bmp = new RenderTargetBitmap(40, 40, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(control);
        var px = new byte[40 * 40 * 4];
        bmp.CopyPixels(px, 40 * 4, 0);
        return px;
    }

    /// <summary>채우기 색(파랑)에 가까운 불투명 픽셀 = 도형 실루엣.</summary>
    private static bool[] Silhouette(byte[] px)
    {
        var fill = ColorHelper.ToBrush(EnumColorType.Blue).Color;
        var mask = new bool[40 * 40];
        for (int i = 0; i < mask.Length; i++)
        {
            int a = px[i * 4 + 3];
            if (a < 128) continue;
            int b = px[i * 4] * 255 / a, g = px[i * 4 + 1] * 255 / a, r = px[i * 4 + 2] * 255 / a;
            mask[i] = Math.Abs(r - fill.R) + Math.Abs(g - fill.G) + Math.Abs(b - fill.B) < 60;
        }
        return mask;
    }

    [Fact]
    [Trait("Scenario", "MAP-127")]
    public void should_draw_distinct_silhouette_for_every_palette_shape_when_shape_symbols_are_created() => AppHost.Run(() =>
    {
        // 팔레트 도형 8종(원형 · 삼각형 · 사각형 · 다이아몬드 · 오각형 · 육각형 · 별 · 화살표) — 같은 크기 · 색에서 실루엣이 서로 다르다
        var shapes = new[] { EnumShapeType.Circle, EnumShapeType.Triangle, EnumShapeType.Square, EnumShapeType.Diamond, EnumShapeType.Pentagon, EnumShapeType.Hexagon, EnumShapeType.Star, EnumShapeType.Arrow };
        var masks = new Dictionary<EnumShapeType, bool[]>();
        foreach (var shape in shapes)
        {
            using var marker = NewShape(shape);
            var control = (GMapGeometricMarkerControl)marker.Shape;
            Assert.Equal(shape, control.ShapeType);
            var mask = Silhouette(Render(control));
            Assert.True(mask.Count(m => m) > 100, $"{shape}: 채우기 픽셀이 {mask.Count(m => m)}개뿐(도형이 안 그려짐)");
            masks[shape] = mask;
        }
        foreach (var a in shapes)
            foreach (var b in shapes.Where(s => s > a))
            {
                int diff = Enumerable.Range(0, 1600).Count(i => masks[a][i] != masks[b][i]);
                Assert.True(diff > 30, $"{a} 와 {b} 의 실루엣이 거의 같다(차이 {diff}px)");
            }
    });

    [Fact]
    [Trait("Scenario", "MAP-127")]
    public void should_fade_symbol_alpha_and_save_opacity_to_db_when_opacity_slider_moves() => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        using var marker = NewShape(EnumShapeType.Square);
        var control = (GMapGeometricMarkerControl)marker.Shape;
        rig.Map.Markers.Add(marker);
        var panel = rig.OpenPanel<GMapPropertyGeometricControl>(marker, "GeometricPropertyStyle.xaml");
        int AlphaAt(byte[] px) => px[(10 * 40 + 10) * 4 + 3];   // 도형 안쪽 · 테두리 밖(MapSymbolReflectionTests 와 같은 점)
        Assert.InRange(AlphaAt(Render(control)), 250, 255);

        var slider = Property3DRig.ByLabel<Slider>(panel, "투명도");
        slider.Value = 0.4;
        Property3DRig.Pump();

        Assert.Equal(0.4, marker.Opacity, 3);
        Assert.Equal(0.4, control.MarkerOpacity, 3);
        Assert.InRange(AlphaAt(Render(control)), (int)(0.4 * 255) - 8, (int)(0.4 * 255) + 8);   // 알파 한 번만(이중 적용 아님)
        Assert.Equal(0.4, (double)rig.Last(GeoUpdate)["Opacity"]!, 3);
        Assert.Equal(1270, (int)rig.Last(GeoUpdate)["Id"]!);
    });

    [Fact]
    [Trait("Scenario", "MAP-127")]
    public void should_hide_and_restore_shape_and_save_show_shape_to_db_when_shape_checkbox_toggles() => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        using var marker = NewShape(EnumShapeType.Star, 1271);
        var control = (GMapGeometricMarkerControl)marker.Shape;
        rig.Map.Markers.Add(marker);
        var panel = rig.OpenPanel<GMapPropertyGeometricControl>(marker, "GeometricPropertyStyle.xaml");
        int filled = Silhouette(Render(control)).Count(m => m);
        Assert.True(filled > 100);

        // DISPLAY 절 '모양' 체크 상자(BasePropertyStyle)
        var shapeBox = Property3DRig.Descendants(panel).OfType<CheckBox>().First(c => Equals(c.Content, "모양"));
        shapeBox.IsChecked = false;
        Property3DRig.Pump();
        Assert.False(marker.ShowShape);
        Assert.Equal(0, Silhouette(Render(control)).Count(m => m));                      // 그림 사라짐
        Assert.False((bool)rig.Last(GeoUpdate)["ShowShape"]!);

        shapeBox.IsChecked = true;
        Property3DRig.Pump();
        Assert.True(marker.ShowShape);
        Assert.Equal(filled, Silhouette(Render(control)).Count(m => m));                 // 같은 별 모양으로 복원
        Assert.True((bool)rig.Last(GeoUpdate)["ShowShape"]!);
    });

    // ───────────────────────── MAP-129 울타리: 3D 울타리 · 기둥 · 요약 ─────────────────────────

    private const string GroupUpdate = "UpdatePidsGroupSymbolAsync";
    private const double Mpp = 0.1;   // 1 px = 0.1 m — 3 m 간격 = 30 px(Full3D LOD)

    private static GMapPidsGroupMarker NewFence()
    {
        // 북쪽으로 ≈60 m · 동쪽으로 ≈40 m 꺾인 울타리
        double lat = Property3DRig.HomeLat, lng = Property3DRig.HomeLng;
        var model = new PidsGroupSymbolModel { Id = 1290, Title = "울타리-1290", Latitude = lat, Longitude = lng, FenceMode = EnumFenceMode.Posts, PostSpacingM = null, FenceHeightM = null, Render3D = true };
        model.LinePoints = new List<GeoPoint> { new(lat, lng, 0), new(lat + 60 / 111320.0, lng, 0), new(lat + 60 / 111320.0, lng + 40 / (111320.0 * Math.Cos(lat * Math.PI / 180)), 0) };
        return new GMapPidsGroupMarker(Mock.Of<ILogService>(), model) { IsVisible = true };
    }

    /// <summary>템플릿만 적용한 3D 울타리 컨트롤(지도 없음 — FenceRunVisualTests.TemplatedControl 과 같은 방식).</summary>
    private static (GMapMarkerPidsGroup3DControl Control, FenceRunVisual Fence, Canvas Posts) Fence3D(GMapPidsGroupMarker marker)
    {
        var control = new GMapMarkerPidsGroup3DControl(marker);
        control.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/PidsGroupMarkerStyle.xaml", UriKind.Relative) });
        control.Style = (Style)control.Resources[typeof(GMapMarkerPidsGroup3DControl)];
        AppHost.Layout(control, 800, 800);
        return (control, Property3DRig.Part<FenceRunVisual>(control, "PART_Fence3D"), Property3DRig.Part<Canvas>(control, "PART_PostCanvas"));
    }

    /// <summary>마커 정점(위경도) → 로컬 px(Mpp) — 속성창 요약이 쓰는 것과 같은 미터 변환(FenceLayout.ToLocalMeters).</summary>
    private static IReadOnlyList<Point> LocalPx(GMapPidsGroupMarker marker)
        => FenceLayout.ToLocalMeters(marker.Model.LinePoints!.Select(p => (p.Latitude, p.Longitude)).ToList())
            .Select(p => new Point(20 + p.X / Mpp, 700 - p.Z / Mpp)).ToList();

    /// <summary>게이트 뒤 본문 구동: 컨트롤이 마커에서 받은 값으로 프레임 → ApplyFrame. 표시 판정은 앱과 같은 FenceLod.Show3D(플래그 ON 가정).</summary>
    private static FenceFrame Frame(GMapMarkerPidsGroup3DControl control, GMapPidsGroupMarker marker)
    {
        var frame = GMapMarkerPidsGroup3DControl.ComputeFrame(LocalPx(marker), control.PostSpacingM ?? FenceDefaults.PostSpacingM, control.FenceHeightM ?? FenceDefaults.FenceHeightM,
            control.FenceMode, false, Mpp, 1.0);
        bool show3D = FenceLod.Show3D(true, control.Render3D, frame.Lod);
        bool showPosts = control.Render3D && frame.Lod == FenceLodLevel.PostsAndThickLine;
        control.ApplyFrame(frame, show3D, showPosts, Mpp);
        return frame;
    }

    private static double MeshTop(FenceRunVisual fence)
        => fence.StaticMeshes.SelectMany(m => m.Positions).Max(p => p.Y);

    private static int PostsInSummary(string summary)
    {
        var m = System.Text.RegularExpressions.Regex.Match(summary, @"기둥 (\d+)개");
        Assert.True(m.Success, $"요약 '{summary}' 에 기둥 수가 없다");
        return int.Parse(m.Groups[1].Value);
    }

    [Fact]
    [Trait("Scenario", "MAP-129")]
    public void should_rebuild_3d_fence_posts_and_summary_and_save_to_db_when_fence_section_edited() => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        using var marker = NewFence();
        var (control, fence, _) = Fence3D(marker);
        Assert.Equal(FenceLodLevel.Full3D, Frame(control, marker).Lod);   // 시험 전제: 3 m 간격이 30 px — 3D 단계
        var panel = rig.OpenPanel<GMapPropertyPidsGroupControl>(marker, "PidsGroupPropertyStyle.xaml");
        panel.Is3DFeatureEnabled = true;                                 // 배포 플래그 ON(앱: 생성자에서 Symbol3DFeature.IsEnabled)
        Property3DRig.LayoutPanel(panel);

        var spacing = Property3DRig.ById<Slider>(panel, "GMaps.Property.PostSpacing");
        var height = Property3DRig.ById<Slider>(panel, "GMaps.Property.FenceHeight");
        var mode = Property3DRig.ById<ComboBox>(panel, "GMaps.Property.FenceMode");
        var render3D = Property3DRig.ById<ToggleButton>(panel, "GMaps.Property.Render3D");
        var summary = Property3DRig.ById<TextBlock>(panel, "GMaps.Property.FenceSummary");
        foreach (var e in new FrameworkElement[] { spacing, height, mode, render3D, summary })
            Assert.True(Property3DRig.IsShown(e), $"{System.Windows.Automation.AutomationProperties.GetAutomationId(e)} 가 안 보인다");

        // 기본(상속) 3 m: 요약의 기둥 수 = 3D 레이아웃 기둥 수
        int posts3 = fence.Layout!.Posts.Count;
        Assert.Equal(posts3, PostsInSummary(summary.Text));
        Assert.True(fence.PartCounts.Static > 0, "3D 정적 메시가 없다");
        double top24 = MeshTop(fence);

        // 기둥 간격 6 m — 슬라이더는 150 ms 지연 커밋(D1)
        spacing.Value = 6;
        Property3DRig.PumpFor(FenceDefaults.SliderCommitDelayMs * 3);
        Assert.Equal(6.0, marker.PostSpacingM);
        Assert.Equal(6.0, control.PostSpacingM);                         // 마커 → 3D 컨트롤(OneWay)
        Assert.Equal(6.0, (double)rig.Last(GroupUpdate)["PostSpacingM"]!);
        Frame(control, marker);
        int posts6 = fence.Layout!.Posts.Count;
        Assert.True(posts6 < posts3, $"간격을 넓혔는데 기둥이 줄지 않았다({posts3} → {posts6})");
        Assert.Equal(posts6, PostsInSummary(summary.Text));             // 요약 = 3D

        // 높이 8 m(기본 4.8 보다 높게) → 3D 메시가 위로 자란다 · DB
        height.Value = 8.0;
        Property3DRig.PumpFor(FenceDefaults.SliderCommitDelayMs * 3);
        Assert.Equal(8.0, marker.FenceHeightM);
        Assert.Equal(8.0, (double)rig.Last(GroupUpdate)["FenceHeightM"]!);
        Frame(control, marker);
        Assert.True(MeshTop(fence) > top24 * 1.3, $"높이 8 m 인데 메시 꼭대기가 {top24:F1} → {MeshTop(fence):F1}");

        // 형태 → 센서 장착: 요약이 '센서 N개' 로 · 3D 노드 버킷 · DB
        mode.SelectedItem = mode.Items.Cast<object>().First(i => Equals(i, EnumFenceMode.SensorMount) || i.ToString() == nameof(EnumFenceMode.SensorMount));
        Property3DRig.Pump();
        Assert.Equal(EnumFenceMode.SensorMount, marker.FenceMode);
        Assert.StartsWith("센서 ", summary.Text);
        Assert.Equal(nameof(EnumFenceMode.SensorMount), (string?)rig.Last(GroupUpdate)["FenceMode"]);
        Frame(control, marker);
        Assert.Equal(EnumFenceMode.SensorMount, fence.Mode);
        Assert.True(fence.Layout!.Nodes.Count > 0 && fence.PartCounts.Nodes > 0, "센서 장착인데 3D 노드가 없다");

        // 3D 끔 → 표시 판정 false(Full3D 여도) · DB Render3D false
        render3D.IsChecked = false;
        Property3DRig.Pump();
        Assert.False(marker.Render3D);
        Assert.False(control.Render3D);
        Assert.False(FenceLod.Show3D(true, control.Render3D, FenceLodLevel.Full3D));
        Assert.False((bool)rig.Last(GroupUpdate)["Render3D"]!);
    });
}
