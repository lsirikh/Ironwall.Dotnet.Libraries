using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 장비 재할당(1장비 = 1마커) 경계(t-20261019-193448-b2cc 영역 D) — 확인 대기 중 대상 삭제 · 통문/스피커/경광등 문구 ·
/// 확인 전 다른 충돌 장비 또 고름 · 세 마커가 같은 장비 · 권한 없음 · 대기 중 Undo. 속성창은 VM 실제 경로, DB 는 가짜.
/// </summary>
public class DeviceReassignEdgeTests
{
    private sealed class Rig
    {
        public EditRig Edit = null!;
        public SymbolLifecycleCoordinator Life = null!;
        public DeviceProvider Devices = null!;
        public Mock<ILogService> Log = null!;
        public List<(int marker, int linked)> Saved = new();
        public MapViewModel Vm => Edit.Vm;
        public IEnumerable<OpenConfirmPopupMessageModel> Popups => Edit.Published.OfType<OpenConfirmPopupMessageModel>();
        public IEnumerable<string> Logs(string level) => Log.Invocations.Where(i => i.Method.Name == level).Select(i => i.Arguments[0] as string ?? "");
    }

    private static Rig Setup(bool canEdit, params IBaseDeviceModel[] devices)
    {
        var r = new Rig { Edit = EditRig.Create(canEdit), Devices = new DeviceProvider(), Log = new Mock<ILogService>() };
        foreach (var d in devices) r.Devices.CollectionEntity.Add(d);
        Scenario.SetField(r.Vm, "_propertyPanelFactory", new PropertyPanelFactory(Mock.Of<ILogService>(), r.Devices, new DeviceGroupProvider(Mock.Of<ILogService>())));
        Scenario.SetField(r.Vm, "_log", r.Log.Object);
        var queue = new Mock<IEventQueueManager>();
        var sem = new SymbolEventManager(new Mock<IEventAggregator>().Object, Mock.Of<ILogService>(), new EventSetupModel(new Mock<IEventSetupModel>().Object), queue.Object);
        r.Life = new SymbolLifecycleCoordinator(sem, queue.Object, Mock.Of<ILogService>());
        Scenario.SetField(r.Vm, "_symbolLifecycle", r.Life);
        r.Edit.Db.Setup(d => d.UpdatePidsSymbolAsync(It.IsAny<IPidsSymbolModel>(), It.IsAny<CancellationToken>()))
            .Returns<IPidsSymbolModel, CancellationToken>((x, _) => { r.Saved.Add((x.Id, x.LinkedDeviceId)); return Task.FromResult<IPidsSymbolModel?>(null); });
        return r;
    }

    private static Rig Setup(params IBaseDeviceModel[] devices) => Setup(true, devices);

    private static GMapPidsMarker AddPids(Rig r, int id, string title, EnumDeviceType type, IBaseDeviceModel? device = null, int linkedId = 0)
    {
        var model = new PidsSymbolModel { Id = id, Title = title, DeviceType = type, Latitude = EditRig.HomeLat, Longitude = EditRig.HomeLng, LinkedDeviceId = linkedId };
        var marker = new GMapPidsMarker(Mock.Of<ILogService>(), model);
        if (device != null) marker.LinkedDevice = device;
        r.Edit.Map.Markers.Add(marker);
        return marker;
    }

    private static GMapPropertyPidsControl Open(Rig r, GMapPidsMarker marker)
    {
        r.Edit.SelectSingle(marker);
        Scenario.Call(r.Vm, "ShowPropertyPanel");
        return Assert.IsType<GMapPropertyPidsControl>(r.Vm.PropertyPanel);
    }

    private static Task Confirm(Rig r) => r.Vm.HandleAsync(new CallReassignLinkedDeviceProcessMessageModel(), CancellationToken.None);

    private static CameraDeviceModel Cam(int id, string name) => new() { Id = id, DeviceName = name, DeviceType = EnumDeviceType.IpCamera };

    [Fact]
    [Trait("Scenario", "D-83")]
    public void should_cancel_without_saving_and_close_popup_when_target_or_holder_is_deleted_before_confirm() => Scenario.StaAsync(async () =>
    {
        foreach (var deleteTarget in new[] { true, false })
        {
            var cam = Cam(501, "정문 카메라");
            var r = Setup(cam);
            var a = AddPids(r, 1, "정문1", EnumDeviceType.IpCamera, cam);
            var b = AddPids(r, 2, "후문", EnumDeviceType.IpCamera);
            var panel = Open(r, b);
            panel.LinkedDevice = cam;
            await Task.Delay(30);
            Assert.Single(r.Popups);

            if (deleteTarget) b.Dispose(); else a.Dispose();   // 확인 창이 떠 있는 사이 삭제(다른 세션 · 묶음 삭제 등)
            await Confirm(r);

            Assert.Empty(r.Saved);
            Assert.False(r.Edit.Undo.CanUndo);
            Assert.Contains(r.Logs("Warning"), s => s.Contains("[장비 재할당] 확인 사이에 상태가 바뀌어 취소: '정문 카메라'"));
            Assert.IsType<ClosePopupMessageModel>(r.Edit.Published[^1]);   // 확인 창은 닫힌다
        }
    });

    [Theory]
    [Trait("Scenario", "D-90")]
    [InlineData(EnumDeviceType.Gate, "통문")]
    [InlineData(EnumDeviceType.IpSpeaker, "스피커")]
    [InlineData(EnumDeviceType.Lamp, "경광등")]
    public void should_name_gate_speaker_and_lamp_kinds_in_question_and_reassign(EnumDeviceType type, string kind) => Scenario.StaAsync(async () =>
    {
        IBaseDeviceModel device = type switch
        {
            EnumDeviceType.Gate => new GateDeviceModel { Id = 41, DeviceName = "장비41", DeviceType = type },
            EnumDeviceType.IpSpeaker => new SpeakerDeviceModel { Id = 41, DeviceName = "장비41", DeviceType = type },
            _ => new LampDeviceModel { Id = 41, DeviceName = "장비41", DeviceType = type },
        };
        Assert.Equal(type, device.DeviceType);                  // 모델 형식 ↔ 종류
        var r = Setup(device);
        var a = AddPids(r, 1, "가", type, device);
        var b = AddPids(r, 2, "나", type);
        var panel = Open(r, b);

        panel.LinkedDevice = device;
        await Task.Delay(30);
        Assert.Equal($"이 장비(장비41)는 '가' {kind} 심볼에 할당되어 있습니다. 재할당하시겠습니까?\n(예 = '가' 의 할당은 비워집니다)", Assert.Single(r.Popups).Explain);
        await Confirm(r);
        Assert.Null(a.LinkedDevice);
        Assert.Same(device, b.LinkedDevice);
        Assert.Equal(new[] { (1, 0), (2, 41) }, r.Saved);
    });

    [Fact]
    [Trait("Scenario", "D-91")]
    public void should_apply_only_last_choice_when_another_conflicting_device_is_picked_before_confirm() => Scenario.StaAsync(async () =>
    {
        var cam1 = Cam(501, "정문 카메라");
        var cam2 = Cam(502, "남문 카메라");
        var r = Setup(cam1, cam2);
        var a = AddPids(r, 1, "정문1", EnumDeviceType.IpCamera, cam1);
        var c = AddPids(r, 3, "남문1", EnumDeviceType.IpCamera, cam2);
        var b = AddPids(r, 2, "후문", EnumDeviceType.IpCamera);
        var panel = Open(r, b);

        panel.LinkedDevice = cam1;                              // 첫 확인 창(답하지 않음)
        await Task.Delay(30);
        panel.LinkedDevice = cam2;                              // 다시 다른 충돌 장비
        await Task.Delay(30);
        Assert.Equal(2, r.Popups.Count());
        Assert.Contains("'남문1'", r.Popups.Last().Explain);

        await Confirm(r);                                       // 확인 = 마지막 고름만
        Assert.Same(cam1, a.LinkedDevice);                      // 첫 고름은 버려짐
        Assert.Null(c.LinkedDevice);
        Assert.Same(cam2, b.LinkedDevice);
        Assert.Equal(new[] { (3, 0), (2, 502) }, r.Saved);

        await Confirm(r);                                       // 늦게 온 두 번째 확인 = 대기 없음 → 아무 일 없음
        Assert.Equal(2, r.Saved.Count);
    });

    [Fact]
    [Trait("Scenario", "D-92")]
    public void should_clear_every_other_holder_and_undo_all_together_when_three_markers_share_device() => Scenario.StaAsync(async () =>
    {
        // 결함 ⑥: 같은 장비를 세 마커가 가진 자료에서 재할당이 첫 마커만 비웠다(OtherDeviceHolders(...).FirstOrDefault()).
        var cam = Cam(501, "정문 카메라");
        var r = Setup(cam);
        var a = AddPids(r, 1, "정문1", EnumDeviceType.IpCamera, cam);
        var c = AddPids(r, 3, "정문2", EnumDeviceType.IpCamera, linkedId: 501);   // 이미 중복된 자료(Id 만 — 부팅 재바인딩은 첫 마커에만 객체)
        var b = AddPids(r, 2, "후문", EnumDeviceType.IpCamera);
        var panel = Open(r, b);

        panel.LinkedDevice = cam;
        await Task.Delay(30);
        Assert.Contains("'정문1' 카메라 심볼", Assert.Single(r.Popups).Explain);
        await Confirm(r);

        Assert.Null(a.LinkedDevice);
        Assert.Equal(0, a.LinkedDeviceId);
        Assert.Equal(0, c.LinkedDeviceId);                      // 세 번째 마커도 비움
        Assert.Same(cam, b.LinkedDevice);
        Assert.Contains((1, 0), r.Saved);
        Assert.Contains((3, 0), r.Saved);
        Assert.Equal((2, 501), r.Saved[^1]);                    // 비움 저장 뒤 연결 저장
        Assert.Null(panel.LinkedDeviceSharedWarning);
        Assert.Equal("장비 재할당: 정문 카메라", r.Vm.NextUndoDescription);

        r.Vm.UndoCommand!.Execute(null);                        // Undo 1번 = 세 마커 함께
        await Task.Delay(80);
        Assert.Equal(501, a.LinkedDeviceId);
        Assert.Equal(501, c.LinkedDeviceId);
        Assert.Equal(0, b.LinkedDeviceId);
        Assert.False(r.Edit.Undo.CanUndo);
    });

    [Fact]
    [Trait("Scenario", "D-93")]
    public void should_block_without_reassign_popup_when_account_cannot_edit_map() => Scenario.StaAsync(async () =>
    {
        var cam = Cam(501, "정문 카메라");
        var r = Setup(canEdit: false, cam);
        var a = AddPids(r, 1, "정문1", EnumDeviceType.IpCamera, cam);
        var b = AddPids(r, 2, "후문", EnumDeviceType.IpCamera);
        var panel = Open(r, b);

        panel.LinkedDevice = cam;
        await Task.Delay(30);
        Assert.Empty(r.Popups);                                 // 재할당 확인 창 없이
        Assert.Contains(r.Edit.Published.OfType<OpenInfoPopupMessageModel>(), p => p.Title == "권한 없음");   // 권한 안내만
        Assert.Empty(r.Saved);
        Assert.Same(cam, a.LinkedDevice);                       // A 는 그대로
        Assert.False(r.Edit.Undo.CanUndo);
        Assert.Contains(r.Logs("Warning"), s => s.Contains("[RBAC] 맵 편집 권한 없음 — 속성 저장 차단: LinkedDevice"));
    });

    [Fact]
    [Trait("Scenario", "D-94")]
    public void should_cancel_pending_reassign_when_undo_changes_target_before_confirm() => Scenario.StaAsync(async () =>
    {
        var cam = Cam(501, "정문 카메라");
        var other = Cam(502, "남문 카메라");
        var free = Cam(503, "서문 카메라");
        var r = Setup(cam, other, free);
        var a = AddPids(r, 1, "정문1", EnumDeviceType.IpCamera, cam);
        var b = AddPids(r, 2, "후문", EnumDeviceType.IpCamera, other);
        var panel = Open(r, b);
        panel.LinkedDevice = free;                              // 평범한 변경(기록됨)
        await Task.Delay(30);
        panel.LinkedDevice = cam;                               // 충돌 → 대기(B 는 free 로 되돌려 둠)
        await Task.Delay(30);
        Assert.Same(free, b.LinkedDevice);
        r.Saved.Clear();

        r.Vm.UndoCommand!.Execute(null);                        // 확인 전 Ctrl+Z — 앞의 평범한 변경을 되돌림(B = other)
        await Task.Delay(80);
        Assert.Same(other, b.LinkedDevice);
        await Confirm(r);                                       // 그 뒤 온 '예' = 상태가 바뀌어 취소

        Assert.Same(cam, a.LinkedDevice);
        Assert.Same(other, b.LinkedDevice);
        Assert.DoesNotContain(r.Saved, s => s.marker == 1);     // A 를 비우지 않는다
        Assert.Contains(r.Logs("Warning"), s => s.Contains("[장비 재할당] 확인 사이에 상태가 바뀌어 취소"));
    });
}
