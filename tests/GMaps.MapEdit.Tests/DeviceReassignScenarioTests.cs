using System.Collections.Concurrent;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Data;
using Caliburn.Micro;
using GMap.NET;
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
/// 장비 1개 = 마커 1개(2026-10-19 사용자 요청) — 다른 마커에 연결된 장비를 고르면 확인 팝업 → 예: 옛 마커 비움 + 새 마커 연결(저장 두 건 · 실패 시 원복 · Undo 1단위),
/// 아니오: 원래 값. 속성창은 VM 의 실제 경로(PropertyPanelFactory → ShowPropertyPanel → 이벤트 배선)로 만든다. DB 는 가짜(운영 DB 쓰기 없음).
/// </summary>
public class DeviceReassignScenarioTests
{
    private sealed class Rig
    {
        public EditRig Edit = null!;
        public SymbolLifecycleCoordinator Life = null!;
        public DeviceProvider Devices = null!;
        public Mock<ILogService> Log = null!;
        /// <summary>DB 저장 기록(마커 Id, 그때 LinkedDeviceId) — 순서대로.</summary>
        public List<(int marker, int linked)> Saved = new();
        /// <summary>이 마커 Id 저장은 실패(예외)시킨다.</summary>
        public int FailId = -1;
        public MapViewModel Vm => Edit.Vm;
        public IEnumerable<OpenConfirmPopupMessageModel> Popups => Edit.Published.OfType<OpenConfirmPopupMessageModel>();
    }

    private static Rig Setup(params IBaseDeviceModel[] devices)
    {
        var r = new Rig { Edit = EditRig.Create(), Devices = new DeviceProvider(), Log = new Mock<ILogService>() };
        foreach (var d in devices) r.Devices.CollectionEntity.Add(d);
        Scenario.SetField(r.Vm, "_propertyPanelFactory", new PropertyPanelFactory(Mock.Of<ILogService>(), r.Devices, new DeviceGroupProvider(Mock.Of<ILogService>())));
        Scenario.SetField(r.Vm, "_log", r.Log.Object);
        var queue = new Mock<IEventQueueManager>();
        var sem = new SymbolEventManager(new Mock<IEventAggregator>().Object, Mock.Of<ILogService>(), new EventSetupModel(new Mock<IEventSetupModel>().Object), queue.Object);
        r.Life = new SymbolLifecycleCoordinator(sem, queue.Object, Mock.Of<ILogService>());
        Scenario.SetField(r.Vm, "_symbolLifecycle", r.Life);
        r.Edit.Db.Setup(d => d.UpdatePidsSymbolAsync(It.IsAny<IPidsSymbolModel>(), It.IsAny<CancellationToken>()))
            .Returns<IPidsSymbolModel, CancellationToken>((x, _) =>
            {
                r.Saved.Add((x.Id, x.LinkedDeviceId));
                if (x.Id == r.FailId) throw new InvalidOperationException("가짜 DB 실패");
                return Task.FromResult<IPidsSymbolModel?>(null);
            });
        return r;
    }

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

    private static IPidsEventCapable? Lookup(Rig r, int id, EnumDeviceType type)
        => Scenario.GetField<ConcurrentDictionary<(int Id, EnumDeviceType Type), IPidsEventCapable>>(r.Life, "_devices").TryGetValue((id, type), out var s) ? s : null;

    private static Task Confirm(Rig r) => r.Vm.HandleAsync(new CallReassignLinkedDeviceProcessMessageModel(), CancellationToken.None);

    private static CameraDeviceModel Cam(int id, string name) => new() { Id = id, DeviceName = name, DeviceType = EnumDeviceType.IpCamera };

    // ───────────────────────── 확인 안내 · 아니오 ─────────────────────────

    [Fact]
    [Trait("Scenario", "DEV-REASSIGN")]
    public void should_ask_with_holder_title_and_not_save_when_device_is_already_linked_to_other_marker() => Scenario.StaAsync(async () =>
    {
        // 수정 전엔 B 에 그대로 연결 · 저장되고 안내가 없었다(정문1 · 후문 둘 다 같은 카메라).
        var cam = Cam(501, "???-66");
        var r = Setup(cam, Cam(502, "남문 카메라"));
        var a = AddPids(r, 1, "정문1", EnumDeviceType.IpCamera, cam);
        var b = AddPids(r, 2, "후문", EnumDeviceType.IpCamera);
        var panel = Open(r, b);

        panel.LinkedDevice = cam;
        await Task.Delay(30);

        var popup = Assert.Single(r.Popups);
        Assert.Equal("장비 재할당", popup.Title);
        Assert.Equal("이 장비(???-66)는 '정문1' 카메라 심볼에 할당되어 있습니다. 재할당하시겠습니까?\n(예 = '정문1' 의 할당은 비워집니다)", popup.Explain);
        Assert.IsType<CallReassignLinkedDeviceProcessMessageModel>(popup.MessageModel);

        Assert.Null(b.LinkedDevice);           // 확인 전엔 B 원래 값(연결 안 됨)
        Assert.Equal(0, b.LinkedDeviceId);
        Assert.Null(panel.LinkedDevice);       // 판(콤보)도 원래 값
        Assert.Same(cam, a.LinkedDevice);      // A 그대로
        Assert.Equal(501, a.LinkedDeviceId);
        Assert.Empty(r.Saved);                 // 저장 없음
        Assert.False(r.Edit.Undo.CanUndo);     // Undo 기록 없음
    });

    [Fact]
    [Trait("Scenario", "DEV-REASSIGN")]
    public void should_revert_combo_selection_and_keep_everything_when_user_answers_no() => Scenario.StaAsync(async () =>
    {
        var cam = Cam(501, "???-66");
        var other = Cam(502, "남문 카메라");
        var r = Setup(cam, other);
        var a = AddPids(r, 1, "정문1", EnumDeviceType.IpCamera, cam);
        var b = AddPids(r, 2, "후문", EnumDeviceType.IpCamera, other);
        var panel = Open(r, b);

        // 실제 콤보(창 없이) — 속성 판 XAML 과 같은 바인딩
        var combo = new ComboBox();
        combo.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(GMapPropertyPidsControl.FilteredDeviceList)) { Source = panel });
        combo.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedItemProperty, new Binding(nameof(GMapPropertyPidsControl.LinkedDevice)) { Source = panel, Mode = BindingMode.TwoWay });
        Assert.Same(other, combo.SelectedItem);

        combo.SelectedItem = cam;              // 손으로 고름
        await Task.Delay(50);

        Assert.Single(r.Popups);
        Assert.Same(other, combo.SelectedItem); // '아니오'(콜백 없음) — 콤보 표시도 원래 장비
        Assert.Same(other, panel.LinkedDevice);
        Assert.Same(other, b.LinkedDevice);
        Assert.Equal(502, b.LinkedDeviceId);
        Assert.Same(cam, a.LinkedDevice);
        Assert.Empty(r.Saved);
        Assert.False(r.Edit.Undo.CanUndo);
    });

    // ───────────────────────── 예 · 저장 두 건 · Undo ─────────────────────────

    [Fact]
    [Trait("Scenario", "DEV-REASSIGN")]
    public void should_clear_old_marker_and_link_new_marker_with_two_saves_and_one_undo_when_user_answers_yes() => Scenario.StaAsync(async () =>
    {
        var cam = Cam(501, "???-66");
        var r = Setup(cam);
        var a = AddPids(r, 1, "정문1", EnumDeviceType.IpCamera, cam);
        var b = AddPids(r, 2, "후문", EnumDeviceType.IpCamera);
        r.Life.RegisterDevice(cam, a.Model);   // 부팅 등록 = A
        var panel = Open(r, b);

        panel.LinkedDevice = cam;
        await Task.Delay(30);
        await Confirm(r);

        Assert.Null(a.LinkedDevice);
        Assert.Equal(0, a.LinkedDeviceId);                          // 기존 '연결 안 됨' 표현
        Assert.Same(cam, b.LinkedDevice);
        Assert.Equal(501, b.LinkedDeviceId);
        Assert.Same(cam, panel.LinkedDevice);
        Assert.Equal(new[] { (1, 0), (2, 501) }, r.Saved);         // A 비움 → B 연결
        Assert.Same(b.Model, Lookup(r, 501, EnumDeviceType.IpCamera));   // 이벤트 조회표 = B
        Assert.IsType<ClosePopupMessageModel>(r.Edit.Published[^1]);
        Assert.Equal("장비 '???-66' 을(를) '후문' 에 할당했습니다. '정문1' 의 할당은 비웠습니다.", r.Vm.AimStatusMessage);

        // Undo 1번 = 두 마커 함께
        Assert.True(r.Vm.UndoCommand!.CanExecute(null));
        Assert.Equal("장비 재할당: ???-66", r.Vm.NextUndoDescription);
        r.Saved.Clear();
        r.Vm.UndoCommand!.Execute(null);
        await Task.Delay(80);
        Assert.Same(cam, a.LinkedDevice);
        Assert.Equal(501, a.LinkedDeviceId);
        Assert.Null(b.LinkedDevice);
        Assert.Equal(0, b.LinkedDeviceId);
        Assert.Null(panel.LinkedDevice);
        Assert.Equal(2, r.Saved.Count);                             // 되돌림도 둘 다 저장
        Assert.Contains((1, 501), r.Saved);
        Assert.Contains((2, 0), r.Saved);
        Assert.Same(a.Model, Lookup(r, 501, EnumDeviceType.IpCamera));   // 조회표도 A 로
        Assert.False(r.Edit.Undo.CanUndo);                          // 한 단위였다
        Assert.Empty(r.Popups.Skip(1));                             // 되돌림은 안내 없음

        // Redo
        r.Vm.RedoCommand!.Execute(null);
        await Task.Delay(80);
        Assert.Null(a.LinkedDevice);
        Assert.Same(cam, b.LinkedDevice);
        Assert.Same(b.Model, Lookup(r, 501, EnumDeviceType.IpCamera));
    });

    [Fact]
    [Trait("Scenario", "DEV-REASSIGN")]
    public void should_restore_both_markers_and_saves_when_new_marker_save_fails() => Scenario.StaAsync(async () =>
    {
        var cam = Cam(501, "???-66");
        var r = Setup(cam);
        var a = AddPids(r, 1, "정문1", EnumDeviceType.IpCamera, cam);
        var b = AddPids(r, 2, "후문", EnumDeviceType.IpCamera);
        var panel = Open(r, b);
        r.FailId = 2;

        panel.LinkedDevice = cam;
        await Task.Delay(30);
        await Confirm(r);

        Assert.Same(cam, a.LinkedDevice);                           // 둘 다 원래대로
        Assert.Equal(501, a.LinkedDeviceId);
        Assert.Null(b.LinkedDevice);
        Assert.Null(panel.LinkedDevice);
        Assert.Equal(new[] { (1, 0), (2, 501), (1, 501) }, r.Saved);   // A 비움 → B 실패 → A 다시 연결 저장
        Assert.False(r.Edit.Undo.CanUndo);
        Assert.Equal("장비 재할당을 저장하지 못했습니다.", r.Vm.AimStatusMessage);
        Assert.IsType<ClosePopupMessageModel>(r.Edit.Published[^1]);
    });

    [Fact]
    [Trait("Scenario", "DEV-REASSIGN")]
    public void should_change_nothing_when_old_marker_clear_save_fails() => Scenario.StaAsync(async () =>
    {
        var cam = Cam(501, "???-66");
        var r = Setup(cam);
        var a = AddPids(r, 1, "정문1", EnumDeviceType.IpCamera, cam);
        var b = AddPids(r, 2, "후문", EnumDeviceType.IpCamera);
        var panel = Open(r, b);
        r.FailId = 1;

        panel.LinkedDevice = cam;
        await Task.Delay(30);
        await Confirm(r);

        Assert.Same(cam, a.LinkedDevice);
        Assert.Null(b.LinkedDevice);
        Assert.Equal(new[] { (1, 0) }, r.Saved);                   // B 는 시도도 안 함
        Assert.False(r.Edit.Undo.CanUndo);
    });

    [Fact]
    [Trait("Scenario", "DEV-REASSIGN")]
    public void should_do_nothing_when_confirm_arrives_without_pending_or_after_state_changed() => Scenario.StaAsync(async () =>
    {
        var cam = Cam(501, "???-66");
        var r = Setup(cam);
        var a = AddPids(r, 1, "정문1", EnumDeviceType.IpCamera, cam);
        var b = AddPids(r, 2, "후문", EnumDeviceType.IpCamera);
        await Confirm(r);                                           // 대기 없음
        Assert.Empty(r.Saved);

        var panel = Open(r, b);
        panel.LinkedDevice = cam;
        await Task.Delay(30);
        a.LinkedDevice = null;                                      // 확인 사이 A 가 다른 경로로 풀림
        await Confirm(r);
        Assert.Empty(r.Saved);
        Assert.Null(b.LinkedDevice);
    });

    // ───────────────────────── 안내 없는 경우 ─────────────────────────

    [Fact]
    [Trait("Scenario", "DEV-REASSIGN")]
    public void should_not_ask_when_same_marker_repicks_its_device_or_unlinks_or_picks_free_device() => Scenario.StaAsync(async () =>
    {
        var cam = Cam(501, "???-66");
        var free = Cam(502, "남문 카메라");
        var r = Setup(cam, free);
        var a = AddPids(r, 1, "정문1", EnumDeviceType.IpCamera, cam);
        AddPids(r, 2, "후문", EnumDeviceType.IpCamera);
        var panel = Open(r, a);

        panel.LinkedDevice = cam;                                   // 같은 장비 다시
        await Task.Delay(20);
        Assert.Empty(r.Popups);
        Assert.Empty(r.Saved);

        panel.LinkedDevice = null;                                  // 연결 해제
        await Task.Delay(20);
        Assert.Empty(r.Popups);
        Assert.Null(a.LinkedDevice);
        Assert.Equal(new[] { (1, 0) }, r.Saved);

        panel.LinkedDevice = cam;                                   // 다시 연결(다른 마커가 안 가짐)
        await Task.Delay(20);
        panel.LinkedDevice = free;                                  // 빈 장비
        await Task.Delay(20);
        Assert.Empty(r.Popups);
        Assert.Same(free, a.LinkedDevice);
        Assert.Equal(new[] { (1, 0), (1, 501), (1, 502) }, r.Saved);

        // 일반 연결 변경도 Undo 된다(LinkedDevice 가 되돌릴 수 있는 속성이 됨)
        r.Vm.UndoCommand!.Execute(null);
        await Task.Delay(60);
        Assert.Same(cam, a.LinkedDevice);
    });

    [Fact]
    [Trait("Scenario", "DEV-REASSIGN")]
    public void should_not_treat_same_id_of_other_device_kind_as_conflict() => Scenario.StaAsync(async () =>
    {
        // 장비 Id 는 종류마다 따로 — 제어기 7 과 카메라 7 은 다른 장비
        var ctrl = new ControllerDeviceModel { Id = 7, DeviceName = "제어기7", DeviceType = EnumDeviceType.Controller };
        var cam = Cam(7, "카메라7");
        var r = Setup(ctrl, cam);
        AddPids(r, 1, "제어기 심볼", EnumDeviceType.Controller, ctrl);
        var b = AddPids(r, 2, "카메라 심볼", EnumDeviceType.IpCamera);
        var panel = Open(r, b);

        panel.LinkedDevice = cam;
        await Task.Delay(20);
        Assert.Empty(r.Popups);
        Assert.Same(cam, b.LinkedDevice);
        Assert.Equal(new[] { (2, 7) }, r.Saved);
    });

    // ───────────────────────── 다른 경로 ─────────────────────────

    [Fact]
    [Trait("Scenario", "DEV-REASSIGN")]
    public void should_block_device_link_change_in_group_property_panel() => Scenario.StaAsync(async () =>
    {
        var cam = Cam(501, "???-66");
        var free = Cam(502, "남문 카메라");
        var r = Setup(cam, free);
        var a = AddPids(r, 1, "정문1", EnumDeviceType.IpCamera, cam);
        var b = AddPids(r, 2, "후문", EnumDeviceType.IpCamera);
        r.Edit.SelectGroup(a, b);
        Scenario.Call(r.Vm, "ShowGroupPropertyPanelIfNeeded");
        var panel = Assert.IsType<GMapPropertyPidsControl>(r.Vm.PropertyPanel);
        Assert.True(panel.IsGroupEditing);

        panel.LinkedDevice = free;                                  // 여러 마커에 같은 장비를 한 번에 주려 함
        await Task.Delay(30);
        Assert.Same(cam, a.LinkedDevice);
        Assert.Null(b.LinkedDevice);
        Assert.Empty(r.Saved);
        Assert.Empty(r.Popups);

        // 막는 이유 안내(콤보 비활성 툴팁)
        var xaml = RepoFiles.Read("Ironwall.Dotnet.Libraries.GMaps.Ui/Themes/PidsPropertyStyle.xaml");
        Assert.Contains("그룹 편집에서는 디바이스 연결을 변경할 수 없습니다 (심볼별 고유)", xaml);
    });

    [Fact]
    [Trait("Scenario", "DEV-REASSIGN")]
    public void should_clear_device_link_on_copy_paste_clone()
    {
        var model = new PidsSymbolModel { Id = 1, Title = "정문1", DeviceType = EnumDeviceType.IpCamera, LinkedDeviceId = 501, Latitude = 37.5, Longitude = 127.0 };
        var apply = typeof(MapViewModel).GetMethod("ApplyCopyTransform", BindingFlags.NonPublic | BindingFlags.Static)!;
        apply.Invoke(null, new object[] { model, new PointLatLng(37.5, 127.0), new PointLatLng(37.6, 127.1), true });
        Assert.Equal(0, model.LinkedDeviceId);                      // 복제본은 연결 비움 → 중복 생기지 않음
        Assert.Equal("정문1_Copy", model.Title);
    }

    // ───────────────────────── 기존 중복 자료 ─────────────────────────

    [Fact]
    [Trait("Scenario", "DEV-REASSIGN")]
    public void should_keep_first_marker_warn_in_log_and_show_note_without_saving_when_loading_duplicate_links() => Scenario.StaAsync(async () =>
    {
        var cam = Cam(501, "???-66");
        var r = Setup(cam);
        var first = AddPids(r, 1, "정문1", EnumDeviceType.IpCamera, linkedId: 501);   // DB 에 이미 둘 다 501
        var second = AddPids(r, 2, "후문", EnumDeviceType.IpCamera, linkedId: 501);
        typeof(MapViewModel).GetField("<DeviceProvider>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(r.Vm, r.Devices);

        await (Task)Scenario.Call(r.Vm, "InitializeDeviceSymbolIntegration")!;

        Assert.Same(cam, first.LinkedDevice);                       // 첫 마커만 유효
        Assert.Null(second.LinkedDevice);
        Assert.Equal(501, second.LinkedDeviceId);                   // 자료는 그대로(자동 수정 없음)
        Assert.Same(first.Model, Lookup(r, 501, EnumDeviceType.IpCamera));
        Assert.Empty(r.Saved);
        var warn = r.Log.Invocations.Where(i => i.Method.Name == "Warning").Select(i => i.Arguments[0] as string).Single(s => s!.Contains("[장비 중복 연결]"));
        Assert.Contains("'정문1'", warn);
        Assert.Contains("'후문'", warn);

        var panel = Open(r, second);
        Assert.Equal("이 장비는 다른 마커('정문1')에도 연결되어 있습니다. 한 마커에만 연결하세요.", panel.LinkedDeviceSharedWarning);
        panel = Open(r, first);
        Assert.Equal("이 장비는 다른 마커('후문')에도 연결되어 있습니다. 한 마커에만 연결하세요.", panel.LinkedDeviceSharedWarning);

        // 중복인 두 번째 마커에서 같은 장비를 고르면 안내 → 예 → 첫 마커 비움, 경고 글 사라짐
        panel = Open(r, second);
        panel.LinkedDevice = cam;
        await Task.Delay(30);
        Assert.Contains("'정문1' 카메라 심볼에 할당되어 있습니다", Assert.Single(r.Popups).Explain);
        await Confirm(r);
        Assert.Equal(0, first.LinkedDeviceId);
        Assert.Same(cam, second.LinkedDevice);
        Assert.Null(panel.LinkedDeviceSharedWarning);
        Assert.Equal(new[] { (1, 0), (2, 501) }, r.Saved);

        var xaml = RepoFiles.Read("Ironwall.Dotnet.Libraries.GMaps.Ui/Themes/PidsPropertyStyle.xaml");
        Assert.Contains("Text=\"{Binding LinkedDeviceSharedWarning}\"", xaml);
    });

    // ───────────────────────── 콤보 표시 글 ─────────────────────────

    [Fact]
    [Trait("Scenario", "DEV-REASSIGN")]
    public void should_mark_devices_linked_to_other_markers_in_combo_items() => Scenario.StaAsync(async () =>
    {
        var cam = Cam(501, "???-66");
        var free = Cam(502, "남문 카메라");
        var r = Setup(cam, free);
        AddPids(r, 1, "정문1", EnumDeviceType.IpCamera, cam);
        var b = AddPids(r, 2, "후문", EnumDeviceType.IpCamera);
        var panel = Open(r, b);

        var notes = panel.DeviceAssignmentNotes!;
        Assert.Equal("· 정문1 에 할당됨", notes[cam]);
        Assert.False(notes.ContainsKey(free));
        Assert.Null(panel.LinkedDeviceSharedWarning);

        var converter = new DeviceAssignmentNoteConverter();
        Assert.Equal("· 정문1 에 할당됨", converter.Convert(new object[] { cam, notes }, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(string.Empty, converter.Convert(new object[] { free, notes }, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture));

        var xaml = RepoFiles.Read("Ironwall.Dotnet.Libraries.GMaps.Ui/Themes/PidsPropertyStyle.xaml");
        Assert.Contains("<MultiBinding Converter=\"{StaticResource DeviceAssignmentNote}\">", xaml);
        Assert.Contains("ItemsSource=\"{Binding FilteredDeviceList}\"", xaml);

        // 고를 수 있고, 고르면 안내 → 예 뒤엔 이 마커 자신이 가진 장비라 표시 없음
        panel.LinkedDevice = cam;
        await Task.Delay(30);
        Assert.Single(r.Popups);
        await Confirm(r);
        Assert.False(panel.DeviceAssignmentNotes!.ContainsKey(cam));
    });

    // ───────────────────────── 장비 종류별 ─────────────────────────

    [Theory]
    [Trait("Scenario", "DEV-REASSIGN")]
    [InlineData(EnumDeviceType.IpCamera, "카메라")]
    [InlineData(EnumDeviceType.SmartSensor, "센서")]
    [InlineData(EnumDeviceType.Controller, "제어기")]
    [InlineData(EnumDeviceType.Enclosure, "함체")]
    public void should_reassign_per_device_kind_with_kind_name_in_question(EnumDeviceType type, string kind) => Scenario.StaAsync(async () =>
    {
        IBaseDeviceModel device = type switch
        {
            EnumDeviceType.IpCamera => Cam(31, "장비31"),
            EnumDeviceType.SmartSensor => new SensorDeviceModel { Id = 31, DeviceName = "장비31", DeviceType = type },
            EnumDeviceType.Controller => new ControllerDeviceModel { Id = 31, DeviceName = "장비31", DeviceType = type },
            _ => new EnclosureDeviceModel { Id = 31, DeviceName = "장비31", DeviceType = type },
        };
        var r = Setup(device);
        var a = AddPids(r, 1, "가", type, device);
        var b = AddPids(r, 2, "나", type);
        var panel = Open(r, b);

        panel.LinkedDevice = device;
        await Task.Delay(30);
        Assert.Equal($"이 장비(장비31)는 '가' {kind} 심볼에 할당되어 있습니다. 재할당하시겠습니까?\n(예 = '가' 의 할당은 비워집니다)", Assert.Single(r.Popups).Explain);
        Assert.Null(b.LinkedDevice);

        await Confirm(r);
        Assert.Null(a.LinkedDevice);
        Assert.Same(device, b.LinkedDevice);
        Assert.Equal(new[] { (1, 0), (2, 31) }, r.Saved);
    });
}
