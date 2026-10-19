using System.Reflection;
using System.Windows.Controls;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Monitoring.Models.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using Moq;
using Newtonsoft.Json.Linq;
using ScenarioShared;
using Xunit;
using Rig = GMaps.MapEdit.Tests.Map3DEntryScenarioTests.Rig;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 3D 위 고르기(pick, 영역 C 전수 시나리오 2026-10-19) — 3D 가 보낸 pick 메시지(OnMap3DMessage → OnMap3DPick)가 실제 지도 통지 · 배선된 VM 처리기로 가는지:
/// 빈 곳 클릭(C-10/C-47) · 더블클릭(카메라 · 구역, C-18/C-22) · 3D 아닐 때 무시 · 추적 · 잠긴 마커(C-20/C-21) · 카메라 더블클릭 2D 복귀(C-24) ·
/// 카메라 상자 연 채 나가기(C-28) · 조립 카드(C-46/C-48). 화면 좌표(screen)는 넣지 않는다(3D 호스트가 지도와 같은 트리에 없어 TranslatePoint 가 던진다).
/// WPF 안 3D(composition) 갈래는 WebView2CompositionControl 이 있어야 해 헤디드 몫(순수 규칙은 Map3DGuideTests).
/// </summary>
public class Map3DPickScenarioTests
{
    private const string Area = "area-a";

    // ─────────────── 장치 ───────────────

    private static List<string> Capture(Rig rig, bool warnings = false)
    {
        var lines = new List<string>();
        if (warnings)
            rig.W.Log.Setup(l => l.Warning(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
               .Callback<string, string, string, int>((m, _, _, _) => lines.Add(m));
        else
            rig.W.Log.Setup(l => l.Info(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
               .Callback<string, string, string, int>((m, _, _, _) => lines.Add(m));
        return lines;
    }

    private static void Enter(Rig rig)
    {
        if (!rig.Vm.Map3DAreas.Any()) rig.Areas(rig.AreaAround(Area));
        rig.W.Map.Position = rig.Center;
        rig.HostReady(Area);
        rig.Gate();
        rig.PressEnter();
        Map3DEntryScenarioTests.SettleEntry(rig);
        Assert.True(rig.Shown);
    }

    private static void Pick(Rig rig, string action, string? kind = null, object? id = null)
    {
        var o = new JObject { ["type"] = "pick", ["action"] = action };
        if (kind != null) o["kind"] = kind;
        if (id != null) o["id"] = JToken.FromObject(id);
        var msg = Map3DMath.ParseMessage(o.ToString());
        Assert.NotNull(msg);
        Scenario.Call(rig.Vm, "OnMap3DMessage", msg);
    }

    /// <summary>부품 축이 있는 카메라(조립 카드를 열 수 있다) — 지도에 올린다.</summary>
    private static GMapPidsMarker Camera(Rig rig, int id, bool locked = false, bool axes = true)
    {
        var marker = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel
        {
            Id = id, Title = $"cam{id}", DeviceType = EnumDeviceType.IpCamera, Latitude = rig.Center.Lat, Longitude = rig.Center.Lng, Zoom = 1, IsLocked = locked,
        });
        marker.Width = 40; marker.Height = 40; marker.IsLayerEnabled = true; marker.IsVisible = true;
        marker.LinkedDevice = new CameraDeviceModel
        {
            Id = 100 + id, DeviceType = EnumDeviceType.IpCamera, Status = EnumDeviceStatus.ACTIVATED, DeviceName = $"cam{id}",
            Axes = axes ? Axes() : null,
        };
        rig.W.Map.Markers.Add(marker);
        return marker;
    }

    private static DeviceAxesModel Axes()
    {
        var spec = new HardwareSpecModel();
        spec.Components.Add(new ComponentDefinitionModel { Key = "ptz", Type = "PTZ_UNIT", InService = true });
        var status = new DeviceStatusModel();
        status.Components["ptz"] = new ComponentStatusModel { State = "IDLE", Health = "OK", ObservedAt = "2026-09-30T00:00:00.000000+09:00" };
        return new DeviceAxesModel { HardwareSpec = spec, DeviceStatus = status, Meta = new ResponseMeta("full", new[] { "hardware_spec", "device_status" }) };
    }

    private static GMapPidsGroupMarker Fence(Rig rig, int id)
    {
        var model = new PidsGroupSymbolModel { Id = id, Title = "울타리", Latitude = rig.Center.Lat, Longitude = rig.Center.Lng, FenceMode = EnumFenceMode.Posts };
        model.LinePoints = new List<GeoPoint> { new(rig.Center.Lat, rig.Center.Lng, 0), new(rig.Center.Lat, rig.Center.Lng + 0.001, 0) };
        var marker = new GMapPidsGroupMarker(Mock.Of<ILogService>(), model);
        rig.W.Map.Markers.Add(marker);
        return marker;
    }

    private static ComponentCardService Card(Rig rig) => Scenario.GetField<ComponentCardService>(rig.Vm, "_componentCard");

    /// <summary>카메라 더블클릭 — 팝업 설정 창구 · 호스트는 미등록(IoC 없음), 카메라에 RTSP 주소가 없어 상자(호스트 프로세스)는 열지 않는다.</summary>
    private static void NoCameraPopupServices(Rig rig)
    {
        Scenario.SetField(rig.Vm, "_overlaySettingsResolved", true);
        Scenario.SetField(rig.Vm, "_cameraPopupHostResolved", true);
    }

    private static void SetHostElement(Rig rig, object? el)
        => typeof(Map3DViewHost).GetField("_el", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(rig.Vm.Map3DHost, el);

    private static void InitLocate(Rig rig)
    {
        foreach (var name in new[] { "_locateHighlight", "_locateRings" })
            Scenario.SetField(rig.Vm, name, Activator.CreateInstance(typeof(MapViewModel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.FieldType));
    }

    // ─────────────── C-10 / C-47 빈 곳 클릭 ───────────────

    [Fact]
    public void should_clear_locate_highlight_and_close_card_when_empty_place_clicked_in_3d()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            InitLocate(rig);
            var cam = Camera(rig, 1);
            Card(rig).Show(cam);                                            // 열린 조립 카드
            Assert.Same(cam, Card(rig).Current);
            var highlight = Scenario.GetField<object>(rig.Vm, "_locateHighlight");
            highlight.GetType().GetProperty("ActiveRequestId")!.SetValue(highlight, (Guid?)Guid.NewGuid());   // "지도에서 보기" 강조 중
            Enter(rig);
            var infos = Capture(rig);

            Pick(rig, "click");                                              // kind · id 없음 = 빈 곳

            Assert.Null(highlight.GetType().GetProperty("ActiveRequestId")!.GetValue(highlight));
            Assert.Null(Card(rig).Current);
            Assert.True(rig.Shown);                                          // 3D 그대로
            Assert.Contains("[Map3D] pick click kind=- id=- → 빈 곳", infos);

            // 빈 곳 더블클릭 · 우클릭은 아무것도 하지 않는다
            Card(rig).Show(cam);
            Pick(rig, "dblclick");
            Pick(rig, "context");
            Assert.Same(cam, Card(rig).Current);
            Assert.True(rig.Shown);
        });

    // ─────────────── C-18 / C-22 더블클릭 · 3D 아닐 때 무시 ───────────────

    [Fact]
    public void should_raise_marker_double_click_once_for_camera_and_group_report_for_fence_and_ignore_when_not_3d()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            NoCameraPopupServices(rig);
            var launcher = new Mock<IGroupActionReportLauncher>();
            Scenario.SetField(rig.Vm, "_groupActionReportLauncher", launcher.Object);
            var cam = Camera(rig, 1);
            var fence = Fence(rig, 9);
            int doubles = 0;
            Action<IEditableMarker> count = _ => doubles++;
            rig.W.Map.OnMarkerDoubleClicked += count;
            try
            {
                Pick(rig, "dblclick", "pids", 1);                            // 2D(3D 아님) — pick 무시
                Pick(rig, "dblclick", "pidsGroup", 9);
                Assert.Equal(0, doubles);
                launcher.Verify(l => l.TryOpenForGroup(It.IsAny<IPidsGroupEditableMarker>()), Times.Never);

                Enter(rig);
                var infos = Capture(rig);
                Pick(rig, "dblclick", "pidsGroup", 9);                       // C-22 구역 = 조치보고(마커 더블클릭 통지 아님)
                launcher.Verify(l => l.TryOpenForGroup(fence), Times.Once);
                Assert.Equal(0, doubles);
                Assert.Contains("[Map3D] pick dblclick kind=pidsGroup id=9 → 마커", infos);

                Pick(rig, "dblclick", "pids", 1);                            // C-18 카메라 = 마커 더블클릭 1회
                Assert.Equal(1, doubles);
                Assert.Contains("[Map3D] pick dblclick kind=pids id=1 → 마커", infos);

                Pick(rig, "dblclick", "pids", 404);                          // 지도에 없는 id = 빈 곳(동작 없음)
                Assert.Equal(1, doubles);
            }
            finally { rig.W.Map.OnMarkerDoubleClicked -= count; }
        });

    // ─────────────── C-20 / C-21 추적 · 잠긴 마커 ───────────────

    [Fact]
    public void should_do_nothing_for_track_pick_and_not_open_card_for_locked_marker()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            var locked = Camera(rig, 2, locked: true);
            Enter(rig);
            var infos = Capture(rig);
            foreach (var action in new[] { "click", "dblclick", "context" })
                Pick(rig, action, "track", "t-77");
            Assert.Equal(3, infos.Count(l => l.Contains("kind=track id=t-77 → 동작 없음(2D 와 같음)")));
            Assert.Null(Card(rig).Current);
            Assert.True(rig.Shown);

            Pick(rig, "click", "pids", 2);                                   // C-21 잠긴 마커 — 카드 안 열림 · 3D 그대로
            Assert.Null(Card(rig).Current);
            Assert.False(locked.IsComponentCardOpen);
            Assert.True(rig.Shown);
        });

    // ─────────────── C-24 카메라 더블클릭 — 네이티브 창 · 없음 호스트는 2D 로 ───────────────

    [Theory]
    [InlineData("none")]
    [InlineData("hwnd")]
    public void should_return_to_2d_with_camera_popup_cause_when_camera_double_clicked_in_window_covering_3d(string hostMode)
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            NoCameraPopupServices(rig);
            Camera(rig, 1);
            Enter(rig);
            if (hostMode == "hwnd") SetHostElement(rig, new Border());
            try
            {
                Assert.Equal(hostMode, rig.Vm.Map3DHost.HostMode);
                var infos = Capture(rig);
                Pick(rig, "dblclick", "pids", 1);
                Assert.False(rig.Shown, "상자가 3D(네이티브 창) 뒤에 가려지지 않게 2D 로 돌아온다");
                Assert.Contains(infos, l => l.StartsWith("[Map3D] 2D 복귀(camera-popup)", StringComparison.Ordinal));
                Assert.True(Scenario.GetField<bool>(rig.Vm, "_map3DEntrySuppressed"));   // 곧바로 다시 3D 로 들어가지 않는다
            }
            finally { SetHostElement(rig, null); }
        });

    // ─────────────── C-28 카메라 상자 연 채 나가기 ───────────────

    [Fact]
    public void should_keep_camera_box_with_finite_position_when_3d_exited_with_box_open()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            Enter(rig);
            var box = new CameraStreamPopupViewModel(11, "정문 PTZ", rig.Center);
            rig.Vm.CameraPopups.Add(box);
            rig.PressExit();
            Assert.False(rig.Shown);
            Assert.Same(box, Assert.Single(rig.Vm.CameraPopups));            // 닫지 않는다
            Assert.True(double.IsFinite(box.CanvasLeft) && double.IsFinite(box.CanvasTop), $"상자 자리 {box.CanvasLeft},{box.CanvasTop}");
        });

    // ─────────────── C-46 / C-48 조립 카드 ───────────────

    [Fact]
    public void should_open_card_in_2d_for_camera_click_and_replace_it_when_another_marker_clicked_in_3d()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            var cam1 = Camera(rig, 1);
            var cam2 = Camera(rig, 2);
            Enter(rig);
            var infos = Capture(rig);

            Pick(rig, "click", "pids", 1);                                   // C-46 3D 에서 카메라 클릭 → 카드(이 호스트는 WPF 를 덮는다 → 2D 로)
            Assert.Same(cam1, Card(rig).Current);
            Assert.True(cam1.IsComponentCardOpen);
            Assert.False(rig.Shown);
            Assert.Contains(infos, l => l.StartsWith("[Map3D] 2D 복귀(component-card)", StringComparison.Ordinal));

            Enter(rig);                                                      // 카드 연 채 다시 3D
            Assert.Same(cam1, Card(rig).Current);
            Pick(rig, "click", "pids", 2);                                   // C-48 다른 마커 → 카드 교체
            Assert.Same(cam2, Card(rig).Current);
            Assert.False(cam1.IsComponentCardOpen);
            Assert.True(cam2.IsComponentCardOpen);

            var plain = Camera(rig, 3, axes: false);                         // 카드를 못 여는 심볼 → 옛 카드는 걷는다
            Enter(rig);
            Pick(rig, "click", "pids", 3);
            Assert.Null(Card(rig).Current);
            Assert.False(plain.IsComponentCardOpen);
        });
}
