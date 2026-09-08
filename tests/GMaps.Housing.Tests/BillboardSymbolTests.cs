using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// map-tilt-25d PRD 결정③ 아이콘 빌보드 — FR-10(IsBillboard·루트 각 0) / FR-11(FOV 월드각 D−θ 정확-1회) / FR-12(히트 파리티·회전 핸들 숨김).
/// SIM-C002. 3D 선례 <c>HousingTests.should_preserve_stored_size_and_keep_hit_box_upright</c> 동형(STA + 실제 템플릿).
/// </summary>
public class BillboardSymbolTests
{
    private const double MarkerBearing = 30;
    private const double Detection = 120;

    private static ResourceDictionary Theme(string file)
        => new() { Source = new Uri($"/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/{file}", UriKind.Relative) };

    private static RotateTransform Root(Control control)
        => (RotateTransform)((TransformGroup)control.RenderTransform).Children[0];

    /// <summary>protected 파생값 관측용 프로브 — 템플릿은 GMapMarkerPidsControl 스타일을 그대로 쓴다.</summary>
    private sealed class FovProbePidsControl : GMapMarkerPidsControl
    {
        public FovProbePidsControl(GMapPidsMarker marker) : base(marker) { }
        public double FovBearing => GetFovBearing();
        public double DisplayAngle => CurrentDisplayAngle;
    }

    private static (Control control, IEditableMarker marker) Create(string kind, double latitude = 37.5, double longitude = 127)
    {
        var log = Mock.Of<ILogService>();
        Control control; IEditableMarker marker;
        switch (kind)
        {
            case "Pids2D":
            {
                var m = new GMapPidsMarker(log, new PidsSymbolModel { Title = "cam", DeviceType = EnumDeviceType.IpCamera, Bearing = MarkerBearing, DetectionBearing = Detection, DetectionAngle = 60, DetectionRange = 50, Width = 80, Height = 40, Latitude = latitude, Longitude = longitude });
                var c = new FovProbePidsControl(m); c.Resources.MergedDictionaries.Add(Theme("PidsMarkerStyle.xaml"));
                c.Style = (Style)c.Resources[typeof(GMapMarkerPidsControl)]; control = c; marker = m; break;
            }
            case "Infra2D":
            {
                var m = new GMapInfraMarker(log, new InfraSymbolModel { Title = "bld", BuildingType = EnumBuildingType.Factory, Bearing = MarkerBearing, Width = 80, Height = 40, Latitude = latitude, Longitude = longitude });
                var c = new GMapMarkerInfraControl(m); c.Resources.MergedDictionaries.Add(Theme("InfraMarkerStyle.xaml"));
                c.Style = (Style)c.Resources[typeof(GMapMarkerInfraControl)]; control = c; marker = m; break;
            }
            case "Military":
            {
                var m = new GMapMilitarySymbolMarker(log, new MilitarySymbolModel { Title = "mil", Bearing = MarkerBearing, Width = 80, Height = 40, Latitude = latitude, Longitude = longitude });
                var c = new GMapMilitarySymbolMarkerControl(m); c.Resources.MergedDictionaries.Add(Theme("MilitaryMarkerStyle.xaml"));
                c.Style = (Style)c.Resources[typeof(GMapMilitarySymbolMarkerControl)]; control = c; marker = m; break;
            }
            case "Custom":
            {
                var m = new GMapCustomMarker(log, new SymbolModel { Title = "cus", Bearing = MarkerBearing, Width = 80, Height = 40, Latitude = latitude, Longitude = longitude });
                var c = new GMapMarkerCustomControl(m); c.Resources.MergedDictionaries.Add(Theme("CustomMarkerStyle.xaml"));
                c.Style = (Style)c.Resources[typeof(GMapMarkerCustomControl)]; control = c; marker = m; break;
            }
            case "Geometric":
            {
                var m = new GMapGeometricMarker(log, new GeometricSymbolModel { Title = "geo", Bearing = MarkerBearing, Width = 80, Height = 40, Latitude = latitude, Longitude = longitude });
                var c = new GMapGeometricMarkerControl(m); c.Resources.MergedDictionaries.Add(Theme("GeometricMarkerStyle.xaml"));
                c.Style = (Style)c.Resources[typeof(GMapGeometricMarkerControl)]; control = c; marker = m; break;
            }
            default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
        marker.Width = 80; marker.Height = 40; marker.IsVisible = true;
        ((GMap.NET.WindowsPresentation.GMapMarker)marker).Shape = control;
        return (control, marker);
    }

    // ── FR-10: 빌보드 타입은 맵 회전 시 루트 각 0, 비빌보드는 b−θ 유지 ──

    [Theory]
    [InlineData("Pids2D")]
    [InlineData("Infra2D")]
    [InlineData("Military")]
    [InlineData("Custom")]
    public void should_keep_root_angle_zero_when_map_rotates_for_billboard_type(string kind) => HousingTests.Sta(() =>
    {
        var (control, marker) = Create(kind);
        HousingTests.Layout(control, 80, 40);
        var aware = (IMapRotationAwareShape)control;
        Assert.True(aware.IsBillboard);
        Assert.True(aware.RotatesIn2D);          // REFAC-I1: 속성창 3D 행 트리거(RotatesIn2D=False)로 누출되지 않는다
        Assert.True(aware.AppliesMapRotation);   // 배포 경로(UpdateOverlaysAfterRotation)는 그대로 탄다

        aware.OnMapBearingChanged(45);
        Assert.Equal(0, Root(control).Angle);
        Assert.Equal(MarkerBearing, marker.Bearing);    // R-35: 모델 write-back 없음

        aware.OnMapBearingChanged(-135);
        Assert.Equal(0, Root(control).Angle);
        aware.OnMapBearingChanged(0);
        Assert.Equal(0, Root(control).Angle);

        // R-40: TransformGroup[Rotate, Scale] 구조 무변경(값만 0)
        var group = Assert.IsType<TransformGroup>(control.RenderTransform);
        Assert.Equal(2, group.Children.Count);
        Assert.IsType<ScaleTransform>(group.Children[1]);
    });

    [Fact]
    public void should_keep_display_angle_bearing_minus_theta_when_map_rotates_for_geometric() => HousingTests.Sta(() =>
    {
        var (control, _) = Create("Geometric");
        HousingTests.Layout(control, 80, 40);
        var aware = (IMapRotationAwareShape)control;
        Assert.False(aware.IsBillboard);
        aware.OnMapBearingChanged(45);
        Assert.Equal(RotationMath.NormalizeDeg(MarkerBearing - 45), Root(control).Angle, 6);
        aware.OnMapBearingChanged(0);
        Assert.Equal(MarkerBearing, Root(control).Angle, 6);
    });

    [Fact]
    public void should_not_inherit_billboard_when_control_is_3d_housing() => HousingTests.Sta(() =>
    {
        // 3D 는 Pids2D/Infra2D 파생이라 IsBillboard=true 를 상속하면 회전 핸들·속성 회전 행이 사라진다 — 명시 false 로 차단.
        using var pids = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel { DeviceType = EnumDeviceType.IpCamera, Width = 83, Height = 41 });
        using var infra = new GMapInfraMarker(Mock.Of<ILogService>(), new InfraSymbolModel { BuildingType = EnumBuildingType.Factory, Width = 83, Height = 41 });
        Assert.False(new GMapMarker3DHousingControl(pids).IsBillboard);
        Assert.False(new GMapMarkerInfra3DControl(infra).IsBillboard);
        Assert.False(new GMapMarker3DHousingControl(pids).RotatesIn2D);
    });

    // ── FR-11: 빌보드 Pids FOV 월드각 = D−θ (루트 0 + 로컬각이 θ 를 정확히 1회 가산) ──

    [Theory]
    [InlineData(0)]
    [InlineData(45)]
    [InlineData(-90)]
    [InlineData(270)]
    [InlineData(180)]
    public void should_keep_fov_world_angle_detection_minus_theta_when_billboard_root_is_zero(double theta) => HousingTests.Sta(() =>
    {
        var (control, _) = Create("Pids2D");
        var probe = (FovProbePidsControl)control;
        HousingTests.Layout(control, 80, 40);
        probe.OnMapBearingChanged(theta);

        Assert.Equal(0, Root(control).Angle);
        Assert.True(RotationMath.AreClose(MarkerBearing - theta, probe.DisplayAngle), $"CurrentDisplayAngle={probe.DisplayAngle}");
        // 월드각 = 루트(0) + FOV 로컬각 = D−θ. 종전 2D 식(D−b)만이면 θ 만큼 어긋난다(이중/누락 −θ 회귀 금지).
        Assert.True(RotationMath.AreClose(Detection - theta, Root(control).Angle + probe.FovBearing), $"FovBearing={probe.FovBearing}");
    });

    // ── FR-12: GetMarkerAtScreen 파리티 — 빌보드는 축정렬 AABB(역회전 없음), Geometric 은 b−θ 역회전 ──

    [Fact]
    public void should_hit_upright_aabb_when_map_rotated_for_billboard_but_inverse_rotate_for_geometric() => HousingTests.Sta(() =>
    {
        bool previous = RotationFeature.IsEnabled;
        // 맵은 레이아웃하지 않는다(MapBearingProjectionTests 선례) — 벤더 ItemsPresenter 템플릿이 다른 STA 스레드에 봉인돼 Measure 가 크로스스레드 예외.
        var map = new GMapCustomControl { Width = 640, Height = 480 };
        try
        {
            RotationFeature.IsEnabled = true;
            var (pidsControl, pids) = Create("Pids2D", 37.5, 127);
            var (geoControl, geo) = Create("Geometric", 37.5, 127);   // Bearing=30 → θ=90 에서 표시각 −60
            var host = new StackPanel { Orientation = Orientation.Horizontal, Width = 160, Height = 40 };
            host.Children.Add(pidsControl); host.Children.Add(geoControl);
            using var source = new HwndSource(new HwndSourceParameters("Billboard hit parity") { Width = 160, Height = 40, WindowStyle = unchecked((int)0x80000000) }) { RootVisual = host };
            HousingTests.Layout(host, 160, 40);
            Assert.Equal(80, pidsControl.ActualWidth); Assert.Equal(40, pidsControl.ActualHeight);
            Assert.Equal(80, geoControl.ActualWidth); Assert.Equal(40, geoControl.ActualHeight);
            map.Position = new PointLatLng(37.5, 127); map.Zoom = 18;

            var hit = typeof(GMapCustomControl).GetMethod("GetMarkerAtScreen", BindingFlags.NonPublic | BindingFlags.Instance)!;
            // 마커를 하나씩 올려 판정한다(같은 Position → 후보 경합 배제). 클릭점은 히트 코드와 같은 FromLatLngToLocal 로 유도.
            IEditableMarker? At(IEditableMarker marker, double dx, double dy)
            {
                map.Markers.Clear(); map.Markers.Add((GMap.NET.WindowsPresentation.GMapMarker)marker);
                var p = map.FromLatLngToLocal(marker.Position);
                return (IEditableMarker?)hit.Invoke(map, new object[] { new Point(p.X + dx, p.Y + dy) });
            }

            // θ=0: 둘 다 축정렬 — 가로 +35px(half W 40 이내, half H 20 밖) 은 둘 다 히트
            map.SetMapRotation(0);
            Assert.Same(pids, At(pids, 35, 0));
            Assert.Same(geo, At(geo, 35, 0));

            // θ=90: 빌보드(각 0)는 여전히 히트, Geometric(표시각 −60)은 역회전 후 dy 가 half H 를 넘어 미스
            map.SetMapRotation(90);
            Assert.Equal(0, Root(pidsControl).Angle);
            Assert.Same(pids, At(pids, 35, 0));
            Assert.Null(At(geo, 35, 0));
            // Geometric 은 렌더 표시각(−60) 만큼 돌아간 곳을 찍어야 맞는다 — 렌더/히트 파리티
            var rotated = OffsetRotation.Rotate(35, 0, -60);
            Assert.Same(geo, At(geo, rotated.x, rotated.y));
            Assert.Null(At(pids, rotated.x, rotated.y));   // 빌보드는 돌아간 점(dy≈−30 > half H)에서 미스 — 역회전이 걸리지 않는다는 증거
        }
        finally
        {
            map.Markers.Clear();
            RotationFeature.IsEnabled = previous;
        }
    });

    // ── FR-12: MarkerEditAdorner 회전 핸들 — 빌보드는 생성·히트 제외, Geometric 은 유지 ──

    [Theory]
    [InlineData("Pids2D", false)]
    [InlineData("Custom", false)]
    [InlineData("Geometric", true)]
    public void should_expose_rotate_handle_only_when_target_is_not_billboard(string kind, bool expectRotate) => HousingTests.Sta(() =>
    {
        var (control, marker) = Create(kind);
        HousingTests.Layout(control, 80, 40);
        var map = new GMapCustomControl();
        using var adorner = new MarkerEditAdorner(control, marker, map, null!);
        var detect = typeof(MarkerEditAdorner).GetMethod("DetectClickedHandle", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var center = new Point(40, 20);
        bool found = false;
        for (double y = center.Y - 20 - 1; y >= center.Y - 20 - 80; y -= 1)
        {
            var handle = detect.Invoke(adorner, new object[] { new Point(center.X, y), center })!;
            if (handle.ToString() == "Rotate") { found = true; break; }
        }
        Assert.Equal(expectRotate, found);
        // 이동 핸들(중심)은 타입과 무관하게 유지된다
        Assert.Equal("Move", detect.Invoke(adorner, new object[] { center, center })!.ToString());
    });
}

/// <summary>테스트 로컬 회전 헬퍼(HousingMath.InverseRotate 의 정방향) — 렌더 표시각만큼 오프셋을 돌린다.</summary>
internal static class OffsetRotation
{
    public static (double x, double y) Rotate(double dx, double dy, double angleDeg)
    {
        double r = angleDeg * Math.PI / 180;
        return (dx * Math.Cos(r) - dy * Math.Sin(r), dx * Math.Sin(r) + dy * Math.Cos(r));
    }
}
