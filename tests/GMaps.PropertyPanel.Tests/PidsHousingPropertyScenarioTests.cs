using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Xunit;

namespace GMaps.PropertyPanel.Tests;

/****************************************************************************
   Purpose      : 카탈로그 MAP-122(속성 · PIDS 하우징) · MAP-123(속성 · FOV/탐지) 헤드리스 짝(W3, 2026-10-11).
                  지도 위 3D 하우징 심볼 → VM 의 실제 속성창 경로(ShowPropertyPanel) → 앱 스타일(PidsPropertyStyle)의 진짜 슬라이더 ·
                  콤보 · 체크 상자를 움직인다 → 마커 모델 → 3D 컨트롤(HousingVisual 장면 기하 · 재질) · FOV 쐐기(Path 기하) ·
                  가짜 지도 DB(Update 호출 인자)까지 단언한다. 창은 띄우지 않는다(부모 없는 Measure/Arrange · RenderTargetBitmap).
                  배포 플래그 Symbol3D.IsEnabled 는 시험 bin 에 appsettings 가 없어 false 라, 3D 컨트롤은 앱이 플래그 ON 일 때 만드는 것
                  (GMapPidsMarker.CreateMarkerControl → GMapMarker3DHousingControl)과 같은 형식을 직접 붙인다.
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class PidsHousingPropertyScenarioTests
{
    private const string PidsUpdate = "UpdatePidsSymbolAsync";

    /// <summary>지도에 3D 하우징 PIDS 심볼을 올린다(앱의 Symbol3D ON 경로와 같은 컨트롤 형식 · Housing3DMarkerStyle).</summary>
    private static (GMapPidsMarker Marker, GMapMarker3DHousingControl Control, HousingVisual Housing) Place(Property3DRig rig, int id, EnumDeviceType type, EnumColorType fill = EnumColorType.Blue, string? variant = null)
    {
        var model = new PidsSymbolModel
        {
            Id = id, Title = $"{type}-{id}", DeviceType = type, Latitude = Property3DRig.HomeLat, Longitude = Property3DRig.HomeLng,
            Width = 40, Height = 40, FillColor = fill, StrokeColor = EnumColorType.Black, StrokeThickness = 1, ShowShape = true, ShowTitle = true,
            Visible = true, OperationState = EnumOperationState.ACTIVATED, ModelVariant = variant, DetectionRange = 30, DetectionAngle = 90, DetectionBearing = 0,
            FOVColor = EnumColorType.Purple, FOVOpacity = 0.8, ShowFOV = false,
        };
        var marker = new GMapPidsMarker(Mock.Of<ILogService>(), model) { Width = 40, Height = 40, IsVisible = true };
        var control = new GMapMarker3DHousingControl(marker);
        control.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/Housing3DMarkerStyle.xaml", UriKind.Relative) });
        control.Style = (Style)control.Resources[typeof(GMapMarker3DHousingControl)];
        marker.Shape = control;
        rig.Map.Markers.Add(marker);
        rig.LayoutMap();
        Assert.True(control.Template != null && System.Windows.Media.VisualTreeHelper.GetParent(control) != null,
            $"진단: template={control.Template != null} parent={System.Windows.Media.VisualTreeHelper.GetParent(control)?.GetType().Name} mapChildren={System.Windows.Media.VisualTreeHelper.GetChildrenCount(rig.Map)} mapTemplate={rig.Map.Template != null} items={rig.Map.Items.Count} actual={control.ActualWidth}");
        var housing = Property3DRig.Part<HousingVisual>(control, "PART_Housing3D");
        return (marker, control, housing);
    }

    private static Color ColorOf(EnumColorType c) => ColorHelper.ToBrush(c).Color;

    // ───────────────────────── MAP-122 색상 강도 · 설치 높이 · 모델 변형 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-122")]
    public void should_lift_3d_housing_and_save_altitude_to_db_when_install_height_slider_moves() => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        var (marker, control, housing) = Place(rig, 1220, EnumDeviceType.IpCamera);
        var panel = rig.OpenPanel<GMapPropertyPidsControl>(marker, "PidsPropertyStyle.xaml");

        // 3D 컨트롤이면 3D 전용 행(설치 높이 · 색상 강도 · 모델 변형)이 보인다(D-9 게이트 — RotatesIn2D=false)
        Assert.False(control.RotatesIn2D);
        foreach (var label in new[] { "설치 높이", "색상 강도", "모델 변형" })
            Assert.True(Property3DRig.IsShown(Property3DRig.RowOf(panel, label)), $"3D 하우징인데 '{label}' 행이 숨어 있다");

        var flat = Property3DRig.SceneBounds(housing);
        Assert.Equal(0, flat.Y, 3);                                   // 땅에 붙어 있다
        int before = rig.CountOf(PidsUpdate);

        // Act — 진짜 슬라이더를 60 으로
        var slider = Property3DRig.ByLabel<Slider>(panel, "설치 높이");
        slider.Value = 60;
        Property3DRig.Pump();

        // 모델 · 3D 장면 — 하우징이 60 × 0.008 = 0.48 만큼 떠오른다(경계 아래쪽 Y)
        Assert.Equal(60, marker.Altitude);
        Assert.Equal(60, housing.Elevation);
        var lifted = Property3DRig.SceneBounds(housing);
        Assert.Equal(0.48, lifted.Y - flat.Y, 3);
        Assert.Equal(flat.SizeY, lifted.SizeY, 3);                    // 모양은 그대로, 위치만 올라간다

        // DB — 그 순간 모델의 Altitude 가 60 으로 저장 요청
        Assert.Equal(before + 1, rig.CountOf(PidsUpdate));
        Assert.Equal(60.0, (double)rig.Last(PidsUpdate)["Altitude"]!, 3);
        Assert.Equal(1220, (int)rig.Last(PidsUpdate)["Id"]!);

        // 되돌려 0 → 다시 땅, DB 0
        slider.Value = 0;
        Property3DRig.Pump();
        Assert.Equal(0, Property3DRig.SceneBounds(housing).Y, 3);
        Assert.Equal(0.0, (double)rig.Last(PidsUpdate)["Altitude"]!, 3);
    });

    /// <summary>
    /// 색상 강도 → 3D 본체 재채색 · DB 쓰기 없음. <paramref name="slider"/> 를 움직이는 경로만 다르고 단언은 같다.
    /// </summary>
    private static void AssertTintFollowsSlider(Property3DRig rig, Slider slider, GMapMarker3DHousingControl control, HousingVisual housing, EnumColorType fillType, string because)
    {
        var fill = ColorOf(fillType);
        var neutral = Color.FromRgb(170, 178, 188);                   // HousingPalette 본체 바탕색(강도 0)
        int before = rig.CountOf(PidsUpdate);

        slider.Value = 1.0;
        Property3DRig.Pump();
        Assert.True(Math.Abs(HousingAppearance.GetTintStrength(control) - 1.0) < 1e-3, $"{because} — 슬라이더 1.0 인데 심볼 색상 강도 {HousingAppearance.GetTintStrength(control):F2}");
        Assert.Equal(1.0, HousingAppearance.GetTintStrength(housing), 3);   // 상속으로 3D 장면까지
        Assert.Contains(fill, Property3DRig.DiffuseColors(housing));        // 본체 = 채우기 색 100%
        Assert.DoesNotContain(neutral, Property3DRig.DiffuseColors(housing));

        slider.Value = 0.0;
        Property3DRig.Pump();
        Assert.Contains(neutral, Property3DRig.DiffuseColors(housing));     // 착색 없음 = 바탕 회색
        Assert.DoesNotContain(fill, Property3DRig.DiffuseColors(housing));

        // 색상 강도는 "지금 실행 중에만"(MapsHelp Map.Property.Housing · HousingAppearance) — DB 쓰기 없음
        Assert.Equal(before, rig.CountOf(PidsUpdate));
    }

    [Fact]
    [Trait("Scenario", "MAP-122")]
    [Trait("Defect", "P3D-D1")]
    public void should_retint_3d_housing_body_without_db_write_when_color_intensity_slider_moves() => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        var (marker, control, housing) = Place(rig, 1221, EnumDeviceType.IpCamera, EnumColorType.Red);
        var panel = rig.OpenPanel<GMapPropertyPidsControl>(marker, "PidsPropertyStyle.xaml");

        // 앱 스타일(PidsPropertyStyle.xaml:60)의 진짜 '색상 강도' 슬라이더
        var slider = Property3DRig.ByLabel<Slider>(panel, "색상 강도");
        var be = System.Windows.Data.BindingOperations.GetBindingExpression(slider, System.Windows.Controls.Primitives.RangeBase.ValueProperty)!;
        string evidence = $"바인딩 상태={be.Status} · 경로='{be.ParentBinding.Path.Path}' · 경로 인자 {be.ParentBinding.Path.PathParameters.Count}개(접두사 'house:' 미해석) · DataContext={slider.DataContext?.GetType().Name} · SelectedMarker.Shape 는 3D 컨트롤={ReferenceEquals(marker.Shape, control)}";
        AssertTintFollowsSlider(rig, slider, control, housing, EnumColorType.Red,
            "결함 P3D-D1: 속성창 '색상 강도' 슬라이더를 움직여도 3D 하우징 색이 바뀌지 않는다 — " + evidence);
    });

    [Fact]
    [Trait("Scenario", "MAP-122")]
    public void should_retint_3d_housing_body_without_db_write_when_tint_strength_reaches_symbol_shape() => AppHost.Run(() =>
    {
        // 대조군: 같은 경로(속성창 → SelectedMarker.Shape → HousingAppearance.TintStrength)를 해석된 DP 로 묶은 슬라이더.
        // 이것이 통과하고 위 시험만 실패하면 원인은 XAML 경로 문자열(접두사 해석)이지 3D 재채색 쪽이 아니다.
        var rig = Property3DRig.Create();
        var (marker, control, housing) = Place(rig, 1223, EnumDeviceType.IpCamera, EnumColorType.Red);
        var panel = rig.OpenPanel<GMapPropertyPidsControl>(marker, "PidsPropertyStyle.xaml");
        var slider = new Slider { Minimum = 0, Maximum = 1 };
        System.Windows.Data.BindingOperations.SetBinding(slider, System.Windows.Controls.Primitives.RangeBase.ValueProperty,
            new System.Windows.Data.Binding { Source = panel, Path = new PropertyPath("SelectedMarker.Shape.(0)", HousingAppearance.TintStrengthProperty), Mode = System.Windows.Data.BindingMode.TwoWay });
        Assert.Equal(0.35, slider.Value, 3);                                  // 기본 강도 0.35 를 읽어 온다(경로 해석 성공)
        AssertTintFollowsSlider(rig, slider, control, housing, EnumColorType.Red, "해석된 DP 경로");
        // 주의: 여기서 XamlReader 로 assembly 를 적은 xmlns 를 한 번이라도 해석하면 프로세스 전역 XAML 스키마가 그 이름공간을 기억해
        // 이후 P3D-D1 시험이 순서에 따라 통과해 버린다(실측) — 이 시험 어셈블리에서는 XamlReader 를 쓰지 않는다.
    });

    [Fact]
    [Trait("Scenario", "MAP-122")]
    public void should_swap_3d_model_and_save_variant_to_db_when_model_variant_combo_changes() => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        var (marker, control, housing) = Place(rig, 1222, EnumDeviceType.IpCamera, variant: "Fixed");
        var panel = rig.OpenPanel<GMapPropertyPidsControl>(marker, "PidsPropertyStyle.xaml");
        Assert.Equal("camera", housing.ModelKey);
        int fixedVertices = Property3DRig.VertexCount(housing);
        Assert.Equal(0, Property3DRig.JointedParts(housing));               // 고정형: 독립 헤드 없음
        int before = rig.CountOf(PidsUpdate);

        var combo = Property3DRig.ByLabel<ComboBox>(panel, "모델 변형");
        combo.SelectedValue = "Ptz";
        Property3DRig.Pump();

        Assert.Equal("Ptz", marker.ModelVariant);
        Assert.Equal("Ptz", control.ModelVariant);
        Assert.Equal("camera.ptz", housing.ModelKey);
        Assert.NotEqual(fixedVertices, Property3DRig.VertexCount(housing));   // 메시가 바뀌었다
        Assert.True(Property3DRig.JointedParts(housing) > 0, "PTZ 인데 독립 헤드 부품이 없다");
        Assert.Equal(before + 1, rig.CountOf(PidsUpdate));
        Assert.Equal("Ptz", (string?)rig.Last(PidsUpdate)["ModelVariant"]);

        combo.SelectedValue = "Dome";
        Property3DRig.Pump();
        Assert.Equal("camera.dome", housing.ModelKey);
        Assert.Equal("Dome", (string?)rig.Last(PidsUpdate)["ModelVariant"]);
        Assert.Equal(0, Property3DRig.JointedParts(housing));               // 돔형도 헤드는 본체와 함께 돈다(D-11)
    });

    // ───────────────────────── MAP-123 FOV 표시/색/투명도 · 범위/각도/방향 ─────────────────────────

    private static (Path Sector, LineSegment Line, ArcSegment Arc) Wedge(GMapMarker3DHousingControl control)
        => (Property3DRig.Part<Path>(control, "PART_FOVSector"), Property3DRig.Part<LineSegment>(control, "PART_FOVLine1"), Property3DRig.Part<ArcSegment>(control, "PART_FOVArc"));

    /// <summary>쐐기 두 끝(꼭지점 기준 벡터)의 각 · 반지름 · 이등분선 방위(지도 방위: 북 0 · 시계방향).</summary>
    private static (double SpanDeg, double Radius, double BisectorBearing) Measure(LineSegment line, ArcSegment arc)
    {
        Vector a = (Vector)line.Point, b = (Vector)arc.Point;
        double span = Vector.AngleBetween(a, b);
        if (span < 0) span += 360;
        var mid = a + b;
        double bearing = (Math.Atan2(mid.Y, mid.X) * 180 / Math.PI + 90 + 360) % 360;
        return (span, a.Length, bearing);
    }

    [Fact]
    [Trait("Scenario", "MAP-123")]
    public void should_show_recolor_and_fade_fov_wedge_and_save_each_to_db_when_fov_rows_change() => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        var (marker, control, _) = Place(rig, 1230, EnumDeviceType.Controller);   // 비카메라도 FOV 가능(D-2)
        var panel = rig.OpenPanel<GMapPropertyPidsControl>(marker, "PidsPropertyStyle.xaml");
        var (sector, _, _) = Wedge(control);
        Assert.Equal(Visibility.Collapsed, sector.Visibility);

        // FOV 표시 체크
        Property3DRig.ByLabel<CheckBox>(panel, "FOV 표시").IsChecked = true;
        Property3DRig.Pump();
        Assert.True(marker.ShowFOV);
        Assert.Equal(Visibility.Visible, sector.Visibility);
        Assert.True((bool)rig.Last(PidsUpdate)["ShowFOV"]!);

        // FOV 색상 콤보
        Property3DRig.ByLabel<ComboBox>(panel, "FOV 색상").SelectedItem = EnumColorType.Red;
        Property3DRig.Pump();
        Assert.Equal(EnumColorType.Red, marker.FOVColor);
        Assert.Equal(ColorOf(EnumColorType.Red), Assert.IsAssignableFrom<SolidColorBrush>(sector.Fill).Color);
        Assert.Equal(nameof(EnumColorType.Red), (string?)rig.Last(PidsUpdate)["FOVColor"]);

        // FOV 투명도 슬라이더
        Property3DRig.ByLabel<Slider>(panel, "FOV 투명도").Value = 0.3;
        Property3DRig.Pump();
        Assert.Equal(0.3, marker.FOVOpacity, 3);
        Assert.Equal(0.3, sector.Opacity, 3);
        Assert.Equal(0.3, (double)rig.Last(PidsUpdate)["FOVOpacity"]!, 3);

        // 픽셀 — 쐐기 안쪽 한 점이 빨강 · 알파 ≈ 0.3
        var px = WedgePixel(control);
        Assert.InRange(px.A, (byte)(0.3 * 255 - 20), (byte)(0.3 * 255 + 20));
        Assert.True(px.R > px.G + 40 && px.R > px.B + 40, $"쐐기 픽셀이 빨강이 아니다: {px}");

        // 끄면 다시 숨고 DB false
        Property3DRig.ByLabel<CheckBox>(panel, "FOV 표시").IsChecked = false;
        Property3DRig.Pump();
        Assert.Equal(Visibility.Collapsed, sector.Visibility);
        Assert.False((bool)rig.Last(PidsUpdate)["ShowFOV"]!);
    });

    [Fact]
    [Trait("Scenario", "MAP-123")]
    public void should_redraw_wedge_geometry_when_detection_range_angle_and_bearing_sliders_move() => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        var (marker, control, housing) = Place(rig, 1231, EnumDeviceType.IpCamera);
        var panel = rig.OpenPanel<GMapPropertyPidsControl>(marker, "PidsPropertyStyle.xaml");
        Property3DRig.ByLabel<CheckBox>(panel, "FOV 표시").IsChecked = true;
        Property3DRig.Pump();
        var (_, line, arc) = Wedge(control);
        var w0 = Measure(line, arc);
        Assert.Equal(90, w0.SpanDeg, 1);                                       // 초기 탐지각 90°
        Assert.Equal(0, w0.BisectorBearing % 360, 1);                          // 북쪽
        Assert.True(w0.Radius > 5, $"쐐기 반지름이 너무 작다: {w0.Radius:F1}px");

        // 탐지범위 30 → 60 m: 반지름 두 배(같은 줌)
        Property3DRig.ByLabel<Slider>(panel, "탐지범위").Value = 60;
        Property3DRig.Pump();
        Assert.Equal(60, marker.DetectionRange);
        var w1 = Measure(line, arc);
        Assert.InRange(w1.Radius / w0.Radius, 1.95, 2.05);                     // 미터→픽셀 환산의 정수 투영 오차 허용
        Assert.Equal(w1.Radius, arc.Size.Width, 3);

        // 탐지각도 90 → 120°
        Property3DRig.ByLabel<Slider>(panel, "탐지각도").Value = 120;
        Property3DRig.Pump();
        Assert.Equal(120, Measure(line, arc).SpanDeg, 1);

        // 탐지방향 0 → 90°: 이등분선이 동쪽으로, PTZ 가 아니므로 반지름 유지
        Property3DRig.ByLabel<Slider>(panel, "탐지방향").Value = 90;
        Property3DRig.Pump();
        var w3 = Measure(line, arc);
        Assert.Equal(90, w3.BisectorBearing, 1);
        Assert.Equal(w1.Radius, w3.Radius, 3);

        // (범위 · 각도 · 방향의 DB 저장은 아래 결함 시험 P3D-D2 가 따로 본다)

        // 기준방향(설치 방향) 45° → 3D 본체 yaw · DB BaseBearing
        double yaw0 = housing.Yaw;
        Property3DRig.ByLabel<Slider>(panel, "기준방향").Value = 45;
        Property3DRig.Pump();
        Assert.Equal(45, marker.BaseBearing);
        Assert.Equal(45, HousingMath.Normalize(housing.Yaw - yaw0), 3);
        Assert.Equal(45.0, (double)rig.Last(PidsUpdate)["BaseBearing"]!, 3);
    });

    [Theory]
    [Trait("Scenario", "MAP-123")]
    [Trait("Defect", "P3D-D2")]
    [InlineData("탐지범위", "DetectionRange", 60.0)]
    [InlineData("탐지각도", "DetectionAngle", 120.0)]
    [InlineData("탐지방향", "DetectionBearing", 90.0)]
    public void should_save_detection_value_to_db_when_detection_slider_moves(string label, string column, double value) => AppHost.Run(() =>
    {
        // 지도 DB 는 탐지 범위 · 각도 · 방향을 영속한다(GMapDbSymbolService COLUMN_SPECS "D-3: 탐지 방향/거리/각도 영속" · UPDATE PidsSymbols).
        // 그런데 속성창 슬라이더는 마커만 바꾸고 저장 요청(OnMarkerPropertyChanged → DbUpdateProcess)을 내지 않는다.
        var rig = Property3DRig.Create();
        var (marker, _, _) = Place(rig, 1232, EnumDeviceType.IpCamera);
        var panel = rig.OpenPanel<GMapPropertyPidsControl>(marker, "PidsPropertyStyle.xaml");
        int before = rig.CountOf(PidsUpdate);

        Property3DRig.ByLabel<Slider>(panel, label).Value = value;
        Property3DRig.Pump();

        Assert.Equal(value, (double)typeof(GMapPidsMarker).GetProperty(column)!.GetValue(marker)!, 3);   // 마커(지도 쐐기)는 바뀐다
        Assert.True(rig.CountOf(PidsUpdate) > before,
            $"결함 P3D-D2: 속성창 '{label}' 를 {value} 로 바꿨는데 지도 DB 저장 요청(UpdatePidsSymbolAsync)이 0건 — 다른 속성을 저장하기 전에 앱을 다시 켜면 {column} 이 이전 값으로 돌아간다");
        Assert.Equal(value, (double)rig.Last(PidsUpdate)[column]!, 3);
    });

    /// <summary>
    /// 심볼 컨트롤을 그려 쐐기 이등분선 위(반지름 60 %) 한 점의 픽셀(Pbgra — 투명 바탕이라 A 가 곧 불투명도).
    /// 창이 없으면 지도가 Loaded 되지 않아 마커 화면 자리(LocalPosition)가 정해지지 않으므로, 컨트롤 좌표계 그대로 VisualBrush 로 1:1 렌더한다
    /// (쐐기는 ClipToBounds=False 라 컨트롤 밖으로 뻗는다 — 넉넉한 영역을 잡는다).
    /// </summary>
    private static Color WedgePixel(GMapMarker3DHousingControl control)
    {
        var (_, line, arc) = Wedge(control);
        var transform = Property3DRig.Part<TranslateTransform>(control, "PART_FOVTransform");
        var mid = (Vector)line.Point + (Vector)arc.Point;
        mid.Normalize();
        var r = ((Vector)line.Point).Length * 0.6;
        var canvas = Property3DRig.Part<Canvas>(control, "PART_FOVCanvas");
        var p = canvas.TransformToAncestor(control).Transform(new Point(transform.X + mid.X * r, transform.Y + mid.Y * r));
        const double half = 200;
        var region = new Rect(-half, -half, 2 * half, 2 * half);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
            dc.DrawRectangle(new VisualBrush(control) { Viewbox = region, ViewboxUnits = BrushMappingMode.Absolute, Stretch = Stretch.Fill }, null, new Rect(0, 0, region.Width, region.Height));
        var bmp = new RenderTargetBitmap((int)region.Width, (int)region.Height, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        var px = new byte[4];
        bmp.CopyPixels(new Int32Rect((int)Math.Round(p.X + half), (int)Math.Round(p.Y + half), 1, 1), px, 4, 0);
        return Color.FromArgb(px[3], px[2], px[1], px[0]);
    }
}
