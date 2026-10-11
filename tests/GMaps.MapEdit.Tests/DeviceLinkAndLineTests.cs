using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 카탈로그 §1 속성 · 장비 연결(MAP-124) · 속성 · 선(MAP-128).
/// 속성창은 VM 의 실제 경로(PropertyPanelFactory → ShowPropertyPanel → 이벤트 배선)로 만든다. 서버 · NATS 는 쓰지 않는다 —
/// 서버 좌표 저장은 VM 경계 인터페이스(IDeviceLocationGateway)를 가짜로 두고 넘어온 값을 본다(실제 PATCH 는 Devices.Ui 게이트웨이 몫).
/// </summary>
public class DeviceLinkAndLineTests
{
    private static void UseFactory(EditRig rig, DeviceProvider? devices = null)
        => Scenario.SetField(rig.Vm, "_propertyPanelFactory", new PropertyPanelFactory(Mock.Of<ILogService>(), devices!, new DeviceGroupProvider(Mock.Of<ILogService>())));

    // ───────────────────────── MAP-124 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-124")]
    public void should_link_device_from_combo_then_apply_current_location_to_server_and_show_toast() => Scenario.StaAsync(async () =>
    {
        // 헤디드 몫: 콤보를 펼쳐 고르는 손동작 · 단추 안 진행 막대 그림 · 실제 서버 PATCH(운영 서버 금지 — 가짜 게이트웨이).
        var rig = EditRig.Create();
        var cam = new CameraDeviceModel { Id = 501, DeviceName = "북문 카메라", DeviceType = EnumDeviceType.IpCamera };
        var other = new CameraDeviceModel { Id = 502, DeviceName = "남문 카메라", DeviceType = EnumDeviceType.IpCamera };
        var speaker = new SpeakerDeviceModel { Id = 503, DeviceName = "스피커", DeviceType = EnumDeviceType.IpSpeaker };
        var devices = new DeviceProvider();
        devices.CollectionEntity.Add(cam); devices.CollectionEntity.Add(other); devices.CollectionEntity.Add(speaker);
        UseFactory(rig, devices);

        // 등록 수명(연결 → 이벤트 조회표 등록) — 실제 코디네이터 + 가짜 큐
        var queue = new Mock<IEventQueueManager>();
        var sem = new SymbolEventManager(new Mock<IEventAggregator>().Object, Mock.Of<ILogService>(), new EventSetupModel(new Mock<IEventSetupModel>().Object), queue.Object);
        var life = new SymbolLifecycleCoordinator(sem, queue.Object, Mock.Of<ILogService>());
        Scenario.SetField(rig.Vm, "_symbolLifecycle", life);

        // 서버 저장 경계(가짜) — 받은 값 기록
        var applied = new List<(IBaseDeviceModel dev, double lat, double lng, double? heading)>();
        bool serverOk = true;
        var gateway = new Mock<IDeviceLocationGateway>();
        gateway.Setup(g => g.ApplyLocationAsync(It.IsAny<IBaseDeviceModel>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double?>(), It.IsAny<CancellationToken>()))
               .Callback<IBaseDeviceModel, double, double, double?, CancellationToken>((d, la, ln, h, _) => applied.Add((d, la, ln, h)))
               .ReturnsAsync(() => serverOk);
        Scenario.SetField(rig.Vm, "_deviceLocationGateway", gateway.Object);
        Scenario.SetField(rig.Vm, "_deviceLocationGatewayResolved", true);

        var saved = new List<int>();
        rig.Db.Setup(d => d.UpdatePidsSymbolAsync(It.IsAny<IPidsSymbolModel>(), It.IsAny<CancellationToken>()))
              .Callback<IPidsSymbolModel, CancellationToken>((x, _) => saved.Add(x.LinkedDeviceId))
              .ReturnsAsync((IPidsSymbolModel?)null);

        var pids = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel
        {
            Id = 124, Title = "북문", DeviceType = EnumDeviceType.IpCamera, Latitude = 37.3951234, Longitude = 126.9687654, BaseBearing = 45,
        });
        rig.Map.Markers.Add(pids);
        rig.SelectSingle(pids);
        Scenario.Call(rig.Vm, "ShowPropertyPanel");
        var panel = Assert.IsType<GMapPropertyPidsControl>(rig.Vm.PropertyPanel);

        // 「현재위치 적용」 단추 — 실제 처리기(OnAnyButtonClick)는 이름이 PART_ApplyDeviceLocationButton 인 단추의 Click 버블을 받는다.
        // 창 없이 쓰려고 그 단추 하나짜리 템플릿을 붙인다(진짜 스타일은 MDIX 앱 자원이 필요).
        panel.Template = new ControlTemplate(typeof(GMapPropertyPidsControl)) { VisualTree = new FrameworkElementFactory(typeof(Button), "PART_ApplyDeviceLocationButton") };
        panel.ApplyTemplate();
        var applyButton = (Button)panel.Template.FindName("PART_ApplyDeviceLocationButton", panel);
        applyButton.Name = "PART_ApplyDeviceLocationButton";   // 템플릿 이름은 이름 범위 등록만 한다 — 처리기는 Button.Name 으로 식별
        applyButton.Content = "현재위치 적용";
        var xaml = RepoFiles.Read("Ironwall.Dotnet.Libraries.GMaps.Ui/Themes/PidsPropertyStyle.xaml");
        Assert.Contains("x:Name=\"PART_ApplyDeviceLocationButton\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding FilteredDeviceList}\"", xaml);
        Assert.Contains("SelectedItem=\"{Binding LinkedDevice, Mode=TwoWay}\"", xaml);

        int requested = 0;
        panel.DeviceLocationApplyRequested += (_, _) => requested++;

        // 연결 전 「현재위치 적용」 → 알림만, 서버 호출 없음
        applyButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        await Task.Delay(30);
        Assert.True(requested == 1, $"단추 Click 이 속성창 요청 이벤트로 가야 한다(이름={applyButton.Name}, 부모={VisualTreeHelper.GetParent(applyButton)?.GetType().Name}, 내용={applyButton.Content})");
        Assert.Empty(applied);
        Assert.Equal("연결된 디바이스가 없어 저장할 수 없습니다.", rig.Vm.AimStatusMessage);
        Assert.Equal("현재위치 적용", applyButton.Content);   // 진행 막대 → 원래 글로 복원

        // 「연결디바이스」: 같은 종류(카메라)만 후보 → 고르면 마커 · DB · 이벤트 조회표
        Assert.Equal(new[] { 501, 502 }, panel.FilteredDeviceList.Select(d => d.Id).OrderBy(x => x));
        panel.LinkedDevice = cam;
        await Task.Delay(30);
        Assert.Same(cam, pids.LinkedDevice);
        Assert.Equal(501, pids.LinkedDeviceId);
        Assert.Contains(501, saved);                              // DB LinkedDeviceId
        Assert.True(life.IsRegistered(501, EnumDeviceType.IpCamera), "연결한 장비가 이벤트 조회표에 바로 등록돼야 한다");

        // 「현재위치 적용」 → 서버 좌표 = 심볼 좌표 + 방위, 토스트(상태 띠)
        applyButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        await Task.Delay(50);
        var call = Assert.Single(applied);
        Assert.Same(cam, call.dev);
        Assert.Equal(pids.Latitude, call.lat, 9);
        Assert.Equal(pids.Longitude, call.lng, 9);
        Assert.Equal(45, call.heading);
        Assert.True(rig.Vm.IsAimStatusVisible);
        Assert.Equal($"디바이스 '북문 카메라' 위치 저장됨 ({pids.Latitude:F6}, {pids.Longitude:F6}).", rig.Vm.AimStatusMessage);
        Assert.Equal("현재위치 적용", applyButton.Content);

        // 서버 거절 → 실패 토스트
        serverOk = false;
        applyButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        await Task.Delay(50);
        Assert.Equal(2, applied.Count);
        Assert.Equal("디바이스 위치 저장에 실패했습니다.", rig.Vm.AimStatusMessage);
        Assert.Equal("현재위치 적용", applyButton.Content);
    });

    // ───────────────────────── MAP-128 ─────────────────────────

    private static double Haversine(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371000d;
        double dLat = (lat2 - lat1) * Math.PI / 180, dLng = (lng2 - lng1) * Math.PI / 180;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }

    private static IEnumerable<DependencyObject> Tree(DependencyObject root)
    {
        yield return root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var d in Tree(VisualTreeHelper.GetChild(root, i))) yield return d;
    }

    [Fact]
    [Trait("Scenario", "MAP-128")]
    public void should_reshape_line_and_persist_pattern_opacity_closed_path_and_show_point_count_and_distance() => Scenario.StaAsync(async () =>
    {
        // 헤디드 몫: 선 그림(픽셀) · 패턴 콤보/투명도 슬라이더 손동작.
        // 화살표 · 닫힌 경로 체크박스는 LinePropertyStyle.xaml 에서 주석 처리돼 화면에 없다(닫힌 경로는 팔레트 Line/Area 로 생성 때 정해짐) —
        // 그래서 닫힌 경로는 속성창 DP 경로(살아 있음)로, 화살표는 마커 → 선 컨트롤 표시 경로로만 본다. 아래 별도 시험이 화면 부재를 고정한다.
        var rig = EditRig.Create();
        UseFactory(rig);
        var saved = new List<(EnumLinePattern pattern, double opacity, bool closed)>();
        rig.Db.Setup(d => d.UpdateLineSymbolAsync(It.IsAny<ILineSymbolModel>(), It.IsAny<CancellationToken>()))
              .Callback<ILineSymbolModel, CancellationToken>((x, _) => saved.Add((x.LinePattern, x.LineOpacity, x.IsClosedPath)))
              .ReturnsAsync((ILineSymbolModel?)null);

        double lat = EditRig.HomeLat, lng = EditRig.HomeLng;
        var model = new LineSymbolModel
        {
            Id = 128, Title = "경계선", Latitude = lat, Longitude = lng, StrokeThickness = 2,
            LinePoints = new List<GeoPoint> { new(lat, lng), new(lat, lng + 0.001), new(lat + 0.001, lng + 0.001) },
        };
        var line = new GMapLineMarker(Mock.Of<ILogService>(), model);
        rig.Map.Template = new ControlTemplate(typeof(GMapCustomControl)) { VisualTree = new FrameworkElementFactory(typeof(ItemsPresenter)) };
        rig.Map.Markers.Add(line);
        Scenario.Layout(rig.Map, 1200, 800);
        var shape = Assert.IsType<GMapMarkerLineControl>(line.Shape);
        Assert.NotNull(shape.MainPolyline);   // LineMarkerStyle 템플릿(generic.xaml)
        shape.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));   // 창이 없어 Loaded 가 안 온다 — 선 컨트롤은 Loaded 에서 지도를 찾는다
        Assert.Equal(3, shape.MainPolyline!.Points.Count);

        rig.SelectSingle(line);
        Scenario.Call(rig.Vm, "ShowPropertyPanel");
        var panel = Assert.IsType<GMapPropertyLineControl>(rig.Vm.PropertyPanel);

        // 읽음 값: 포인트 수 · 총 거리 + 화면 글(StringFormat)
        double expectDist = Haversine(lat, lng, lat, lng + 0.001) + Haversine(lat, lng + 0.001, lat + 0.001, lng + 0.001);
        Assert.Equal(3, panel.PointCount);
        Assert.InRange(panel.TotalDistance, expectDist * 0.995, expectDist * 1.005);
        var lineXaml = RepoFiles.Read("Ironwall.Dotnet.Libraries.GMaps.Ui/Themes/LinePropertyStyle.xaml");
        Assert.Contains("Text=\"{Binding PointCount, StringFormat='{}{0}개'}\"", lineXaml);
        Assert.Contains("Text=\"{Binding TotalDistance, StringFormat='{}{0:F1}m'}\"", lineXaml);
        Assert.Equal("3개", string.Format("{0}개", panel.PointCount));
        Assert.Equal($"{expectDist:F1}m", string.Format("{0:F1}m", panel.TotalDistance));

        // 점 추가 → 읽음 값이 따라간다
        line.AddPoint(new GMap.NET.PointLatLng(lat + 0.001, lng));
        Assert.Equal(4, panel.PointCount);
        Assert.InRange(panel.TotalDistance, expectDist * 1.3, expectDist * 1.7);

        // 패턴 → 선 모양(대시) · DB
        panel.LinePattern = EnumLinePattern.Dashed;
        await Task.Delay(30);
        Assert.Equal(EnumLinePattern.Dashed, line.LinePattern);
        Assert.Equal(new double[] { 10, 5 }, shape.MainPolyline!.StrokeDashArray.ToArray());
        Assert.Equal(EnumLinePattern.Dashed, saved[^1].pattern);
        panel.LinePattern = EnumLinePattern.Dotted;
        await Task.Delay(30);
        Assert.Equal(new double[] { 2, 3 }, shape.MainPolyline!.StrokeDashArray.ToArray());
        Assert.Equal(EnumLinePattern.Dotted, saved[^1].pattern);

        // 투명도 → 선 불투명도 · DB
        panel.LineOpacity = 0.4;
        await Task.Delay(30);
        Assert.Equal(0.4, line.LineOpacity, 6);
        Assert.Equal(0.4, shape.MainPolyline!.Opacity, 6);
        Assert.Equal(0.4, saved[^1].opacity, 6);

        // 닫힌 경로 → 첫 점으로 돌아오는 선 · DB
        int open = shape.MainPolyline!.Points.Count;
        panel.IsClosedPath = true;
        await Task.Delay(30);
        Assert.True(line.IsClosedPath);
        Assert.Equal(open + 1, shape.MainPolyline!.Points.Count);
        Assert.Equal(shape.MainPolyline.Points[0], shape.MainPolyline.Points[^1]);
        Assert.True(saved[^1].closed);

        // 화살표(마커 값) → 선 컨트롤 화살촉 표시
        line.ShowArrowHead = true;
        Scenario.Layout(rig.Map, 1200, 800);
        Assert.True(shape.ShowArrowHead);
        var arrow = Tree(shape).OfType<FrameworkElement>().FirstOrDefault(e => e.Name == "PART_ArrowHead");
        Assert.NotNull(arrow);
        Assert.Equal(Visibility.Visible, arrow!.Visibility);

        Assert.True(rig.Vm.CanUndo);
    });

    /// <summary>
    /// 카탈로그 MAP-128 은 속성창에서 「닫힌 경로」 · 「화살표」를 바꾼다고 적었지만, 두 체크박스는 Line/Area 심볼을 처음 넣은 커밋(b5a1dc2a)부터
    /// 주석 처리돼 있고 닫힌 경로는 팔레트 종류(Area=닫힘, Line=열림 — MapViewModel 의 area/line 분기)로 생성 때 정해진다 → 의도된 설계로 본다.
    /// 실제 동작(화면에 두 칸이 없다)을 고정한다 — 다시 살리면 이 시험과 카탈로그를 함께 고친다. (Scenario 표지 없음: 카탈로그 기대를 단언하지 않는다)
    /// </summary>
    [Fact]
    public void should_not_offer_closed_path_or_arrow_checkboxes_in_line_property_panel_by_design()
    {
        var xaml = RepoFiles.Read("Ironwall.Dotnet.Libraries.GMaps.Ui/Themes/LinePropertyStyle.xaml");
        var active = System.Text.RegularExpressions.Regex.Replace(xaml, "<!--.*?-->", string.Empty, System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.DoesNotContain("IsClosedPath", active);
        Assert.DoesNotContain("ShowArrowHead", active);
        Assert.Contains("PointCount", active);      // 대조: 살아 있는 칸은 남아 있다
        Assert.Contains("LinePattern", active);
        Assert.Contains("LineOpacity", active);
        Assert.Null(typeof(GMapPropertyLineControl).GetField("ShowArrowHeadProperty"));   // 속성창에는 화살표 DP 자체가 없다
    }
}
