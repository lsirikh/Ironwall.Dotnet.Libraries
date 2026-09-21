using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Servers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
/****************************************************************************
   Purpose      : 서버 모니터 콘솔 뷰모델 검증 (N-12)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>서버를 부르지 않는 가짜 통로 — 무엇이 몇 번 나갔는지 센다.</summary>
internal sealed class FakeServerConsoleService : IServerConsoleService
{
    public FakeServerConsoleService(EnumServerContract contract = EnumServerContract.V8_0) => Contract = contract;

    public EnumServerContract Contract { get; }
    public bool IsUnitEra => Contract >= EnumServerContract.V8_0;
    public bool IsAxisEra => Contract >= EnumServerContract.V7_0;

    public List<ServerAxisView> Servers { get; } = new();
    public List<ServerUnitOption> Units { get; } = new();
    public List<ServerCategoryOption> Categories { get; } = new();
    public ServerMetricDto? LatestMetric { get; set; }

    public List<(int DeviceId, int? ServerId)> Assigns { get; } = new();
    public List<(int Id, ServerWriteIntent Intent)> Saves { get; } = new();
    public List<(ServerCategoryOption Category, ServerWriteIntent Intent)> Creates { get; } = new();
    public int LoadCount { get; private set; }

    /// <summary>이 장비 Id 에서 배정이 실패한다.</summary>
    public int FailAssignForDeviceId { get; set; }

    /// <summary>단건 조회를 이 id 에서 붙잡아 둔다(늦은 응답 재현).</summary>
    public int SlowGetId { get; set; }
    public TaskCompletionSource<bool> SlowGate { get; } = new();

    public Task<ServerLoadResult> LoadAsync(int? unitId, bool includeDescendants, CancellationToken token = default)
    {
        LoadCount++;
        return Task.FromResult(new ServerLoadResult(Servers.ToList(), Units.ToList(), Categories.ToList(), false, null));
    }

    public async Task<ServerAxisView?> GetAsync(int id, CancellationToken token = default)
    {
        if (SlowGetId == id) await SlowGate.Task.ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        return Servers.FirstOrDefault(s => s.Id == id);
    }

    public Task<ServerWriteResult> SaveAsync(int id, ServerWriteIntent intent, CancellationToken token = default)
    {
        Saves.Add((id, intent));
        return Task.FromResult(new ServerWriteResult(true, "설정을 저장했습니다"));
    }

    public Task<(ServerWriteResult Result, int NewId)> CreateAsync(
        ServerCategoryOption category, ServerWriteIntent intent, CancellationToken token = default)
    {
        Creates.Add((category, intent));
        return Task.FromResult((new ServerWriteResult(true, ServerStatusRules.JustRegisteredNotice), 99));
    }

    public Task<ServerMetricDto?> LatestMetricAsync(int id, CancellationToken token = default) => Task.FromResult(LatestMetric);

    public Task<IReadOnlyList<ServerMetricDto>> MetricHistoryAsync(int id, int limit = 50, CancellationToken token = default)
        => Task.FromResult<IReadOnlyList<ServerMetricDto>>(Array.Empty<ServerMetricDto>());

    public Task<(ProxySettingDto? Setting, string? Note)> LegacyOperationModeAsync(int id, CancellationToken token = default)
        => Task.FromResult<(ProxySettingDto?, string?)>((null, null));

    public Task<ServerWriteResult> AssignDeviceAsync(
        EnumDeviceCategory category, int deviceId, int? serverId, CancellationToken token = default)
    {
        if (deviceId == FailAssignForDeviceId) return Task.FromResult(new ServerWriteResult(false, "서버가 거절했습니다"));
        Assigns.Add((deviceId, serverId));
        return Task.FromResult(new ServerWriteResult(true, string.Empty));
    }
}

internal sealed class FakeServerDialogs : IServerConsoleDialogs
{
    public bool Answer { get; set; } = true;
    public List<string> Asked { get; } = new();
    public int HistoryOpened { get; private set; }

    public Task<bool> ConfirmAsync(string title, string message) { Asked.Add(message); return Task.FromResult(Answer); }
    public Task ShowMetricHistoryAsync(int serverId, string serverName) { HistoryOpened++; return Task.CompletedTask; }
}

public class ServerMonitorViewModelTests
{
    private static readonly DateTime Now = new(2026, 9, 20, 0, 5, 0, DateTimeKind.Utc);

    private static ServerAxisView Entry(int id, string name, EnumServerType type,
        string status = "NORMAL", string? observedAt = "2026-09-20T00:03:30+00:00")
        => new()
        {
            Id = id,
            TypeServer = type.ToString(),
            Name = name,
            IsEnable = true,
            UnitId = 4,
            Status = status,
            HasStatusKey = true,
            StatusObservedAt = observedAt,
            HasStatusObservedAtKey = true,
            IpAddress = $"10.0.0.{id}",
            Port = 8000 + id,
            Hostname = $"host-{id}",
            UserName = "admin",
            HasConnectionSection = true,
            HasConfigSection = true,
            CreatedAt = "2026-01-01T00:00:00+00:00",
            UpdatedAt = observedAt,
        };

    private static (ServerMonitorViewModel Vm, FakeServerConsoleService Service, DeviceProvider Devices, FakeServerDialogs Dialogs) Build(
        EnumServerContract contract = EnumServerContract.V8_0)
    {
        var service = new FakeServerConsoleService(contract);
        var devices = new DeviceProvider();
        var dialogs = new FakeServerDialogs();
        var vm = new ServerMonitorViewModel(new EventAggregator(), new MockLogService(), service, devices,
            new FixedClock(Now), new Lazy<IServerConsoleDialogs>(() => dialogs));
        return (vm, service, devices, dialogs);
    }

    private static async Task ActivateAsync(ServerMonitorViewModel vm) => await ((IActivate)vm).ActivateAsync();

    private static SpeakerDeviceModel NewSpeaker(int id, int? previousServer = null) => new()
    {
        Id = id,
        DeviceName = $"스피커{id}",
        CategoryDevice = EnumDeviceCategory.Speaker,
        Server = previousServer is null ? null : new ServerModel { Id = previousServer.Value, Name = $"서버{previousServer}" },
    };

    private static CameraDeviceModel NewCamera(int id) => new()
    {
        Id = id,
        DeviceName = $"카메라{id}",
        CategoryDevice = EnumDeviceCategory.Camera,
    };

    #region - 목록 · 레일 -
    [Fact]
    public async Task should_show_every_rail_slot_with_counts_when_servers_loaded()
    {
        var (vm, service, _, _) = Build();
        service.Servers.Add(Entry(1, "스피커서버", EnumServerType.SPEAKER_API));
        service.Servers.Add(Entry(2, "NVR", EnumServerType.NVR_API, "ERROR"));
        service.Servers.Add(Entry(3, "백업", EnumServerType.BACKUP, "NORMAL", observedAt: null));

        await ActivateAsync(vm);

        Assert.Equal(7, vm.RailEntries.Count);
        Assert.Equal(3, vm.RailEntries.Single(e => e.Key == ServerTypeCatalog.AllKey).Count);
        Assert.Equal(1, vm.RailEntries.Single(e => e.Key == ServerTypeCatalog.NvrKey).BadCount);
        Assert.Equal(1, vm.RailEntries.Single(e => e.Key == ServerTypeCatalog.EtcKey).Count);
        Assert.Contains("보고 없음 1대", vm.RailFooterText);
        Assert.Equal(3, vm.Rows.Count);
    }

    [Fact]
    public async Task should_filter_rows_when_a_type_rail_is_selected()
    {
        var (vm, service, _, _) = Build();
        service.Servers.Add(Entry(1, "스피커서버", EnumServerType.SPEAKER_API));
        service.Servers.Add(Entry(2, "NVR", EnumServerType.NVR_API));
        await ActivateAsync(vm);

        Assert.True(vm.SelectRail(ServerTypeCatalog.NvrKey));

        Assert.Single(vm.Rows);
        Assert.Equal("NVR", vm.Rows[0].Name);
    }

    [Fact]
    public async Task should_stop_showing_the_list_when_the_system_events_rail_is_selected()
    {
        var (vm, service, _, _) = Build();
        service.Servers.Add(Entry(1, "스피커서버", EnumServerType.SPEAKER_API));
        await ActivateAsync(vm);

        vm.SelectRail(ServerTypeCatalog.SystemEventsKey);

        Assert.True(vm.IsSystemEvents);
        Assert.False(vm.IsServerList);
        Assert.Contains("받은 것이 없어", vm.SystemEventsNote);
    }

    [Fact]
    public async Task should_search_by_name_and_address_when_text_is_typed()
    {
        var (vm, service, _, _) = Build();
        service.Servers.Add(Entry(1, "스피커서버", EnumServerType.SPEAKER_API));
        service.Servers.Add(Entry(2, "NVR", EnumServerType.NVR_API));
        await ActivateAsync(vm);

        vm.SearchText = "10.0.0.2";
        Assert.Single(vm.Rows);
        Assert.Equal("NVR", vm.Rows[0].Name);

        vm.SearchText = string.Empty;
        Assert.Equal(2, vm.Rows.Count);
    }

    [Fact]
    public async Task should_label_a_never_reported_server_as_not_reported()
    {
        var (vm, service, _, _) = Build();
        service.Servers.Add(Entry(1, "새 서버", EnumServerType.PROXY, "UNKNOWN", observedAt: null));
        await ActivateAsync(vm);

        Assert.True(vm.Rows[0].IsNotReported);
        Assert.Equal("보고 없음", vm.Rows[0].StatusText);
        Assert.Equal("보고 없음", vm.Rows[0].LastChangeText);
        Assert.Equal("○", vm.Rows[0].StatusGlyph);
        Assert.False(vm.Rows[0].IsFault);
    }

    [Fact]
    public async Task should_not_claim_a_status_transition_when_the_contract_is_legacy()
    {
        var (vm, service, _, _) = Build(EnumServerContract.V6_3);
        service.Servers.Add(Entry(1, "운영 서버", EnumServerType.PROXY));
        await ActivateAsync(vm);

        Assert.Equal("—", vm.Rows[0].LastChangeText);
        Assert.Contains("전이 시각이 없습니다", vm.LastChangeNote);
    }

    [Fact]
    public async Task should_hide_the_unit_column_when_the_contract_has_no_unit_axis()
    {
        var (vm, _, _, _) = Build(EnumServerContract.V6_3);
        await ActivateAsync(vm);

        Assert.False(vm.IsUnitEra);
        Assert.DoesNotContain(vm.Columns, c => c.Key == "unit");
        Assert.Contains("부대 편제가 없습니다", vm.UnitSectionText);
    }
    #endregion

    #region - 상세 -
    [Fact]
    public async Task should_draw_no_detail_form_when_nothing_is_selected()
    {
        var (vm, service, _, _) = Build();
        service.Servers.Add(Entry(1, "a", EnumServerType.PROXY));
        await ActivateAsync(vm);

        Assert.False(vm.HasDetail);
        Assert.Equal(string.Empty, vm.PortText);      // 빈 폼이 "0" 을 보이지 않는다
        Assert.Equal(string.Empty, vm.NameText);
    }

    [Fact]
    public async Task should_show_the_metric_band_only_when_one_row_is_selected()
    {
        var (vm, service, _, _) = Build();
        service.Servers.Add(Entry(1, "스피커서버", EnumServerType.SPEAKER_API));
        service.Servers.Add(Entry(2, "NVR", EnumServerType.NVR_API));
        service.LatestMetric = new ServerMetricDto { CpuUsage = 10 };
        await ActivateAsync(vm);

        Assert.False(vm.IsMetricBandVisible);

        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        Assert.True(vm.IsMetricBandVisible);
        Assert.Equal(4, vm.MetricCells.Count);

        vm.OnRowsSelected(new List<object> { vm.Rows[0], vm.Rows[1] });
        Assert.False(vm.IsMetricBandVisible);
    }

    [Fact]
    public async Task should_drop_a_late_detail_response_when_another_row_was_picked()
    {
        var (vm, service, _, _) = Build();
        service.Servers.Add(Entry(1, "느린 서버", EnumServerType.PROXY));
        service.Servers.Add(Entry(2, "빠른 서버", EnumServerType.NVR_API));
        await ActivateAsync(vm);

        service.SlowGetId = 1;
        vm.OnRowsSelected(new List<object> { vm.Rows[0] });    // A — 응답이 붙잡혀 있다
        vm.OnRowsSelected(new List<object> { vm.Rows[1] });    // B — 먼저 끝난다
        service.SlowGate.TrySetResult(true);
        await Task.Delay(30);

        // A 의 응답이 B 의 상세에 들어앉으면 안 된다.
        Assert.Equal("빠른 서버", vm.NameText);
        Assert.Equal("10.0.0.2", vm.IpText);
    }

    [Fact]
    public async Task should_not_refill_the_detail_after_the_console_was_closed()
    {
        var (vm, service, _, _) = Build();
        service.Servers.Add(Entry(1, "느린 서버", EnumServerType.PROXY));
        await ActivateAsync(vm);

        service.SlowGetId = 1;
        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        await ((IDeactivate)vm).DeactivateAsync(true);
        service.SlowGate.TrySetResult(true);
        await Task.Delay(30);

        Assert.False(vm.HasDetail);
        Assert.Equal(string.Empty, vm.NameText);
    }

    [Fact]
    public async Task should_open_the_detail_read_only_until_edit_is_pressed()
    {
        var (vm, service, _, _) = Build();
        service.Servers.Add(Entry(1, "a", EnumServerType.PROXY));
        await ActivateAsync(vm);

        vm.OnRowsSelected(new List<object> { vm.Rows[0] });

        Assert.True(vm.HasDetail);
        Assert.False(vm.IsEditing);
        Assert.True(vm.CanBeginEdit);
        vm.BeginEdit();
        Assert.True(vm.IsEditing);
        Assert.False(vm.CanBeginEdit);
    }

    [Fact]
    public async Task should_never_expose_a_typed_password_when_the_field_is_read_back()
    {
        var (vm, service, _, _) = Build();
        service.Servers.Add(Entry(1, "a", EnumServerType.PROXY));
        await ActivateAsync(vm);
        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        vm.BeginEdit();

        vm.PasswordText = "hunter2";

        Assert.Equal(string.Empty, vm.PasswordText);
        Assert.Equal("저장할 때 바뀝니다", vm.PasswordNote);
        Assert.True(vm.Detail.IsDirty);
        Assert.True(vm.IsPasswordTouched);
    }

    [Fact]
    public async Task should_mark_a_touched_threshold_the_same_way_as_a_touched_field()
    {
        var (vm, service, _, _) = Build();
        service.Servers.Add(Entry(1, "a", EnumServerType.PROXY));
        await ActivateAsync(vm);
        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        vm.BeginEdit();

        vm.CpuWarningText = "65";

        Assert.True(vm.IsCpuTouched);
        Assert.True(vm.Detail.IsDirty);
    }

    [Fact]
    public async Task should_offer_the_modes_section_only_for_a_proxy_server_on_the_axis_contract()
    {
        var (vm, service, _, _) = Build();
        service.Servers.Add(Entry(1, "프록시", EnumServerType.PROXY));
        service.Servers.Add(Entry(2, "NVR", EnumServerType.NVR_API));
        await ActivateAsync(vm);

        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        Assert.True(vm.HasModesSection);

        vm.OnRowsSelected(new List<object> { vm.Rows[1] });
        Assert.False(vm.HasModesSection);
    }

    [Fact]
    public async Task should_block_the_row_change_when_there_are_unapplied_edits()
    {
        var (vm, service, _, _) = Build();
        service.Servers.Add(Entry(1, "a", EnumServerType.PROXY));
        service.Servers.Add(Entry(2, "b", EnumServerType.PROXY));
        await ActivateAsync(vm);

        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        vm.BeginEdit();
        vm.NameText = "고친 이름";

        Assert.True(vm.Detail.IsDirty);
        Assert.False(vm.OnRowsSelected(new List<object> { vm.Rows[1] }));
        Assert.False(vm.SelectRail(ServerTypeCatalog.NvrKey));
    }

    [Fact]
    public async Task should_refuse_a_unit_filter_change_while_edits_are_unapplied()
    {
        var (vm, service, _, _) = Build();
        service.Servers.Add(Entry(1, "a", EnumServerType.PROXY));
        service.Units.Add(new ServerUnitOption(4, "1대대", "unit001"));
        await ActivateAsync(vm);

        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        vm.BeginEdit();
        vm.NameText = "고친 이름";

        var before = service.LoadCount;
        vm.SelectedUnit = vm.UnitOptions[0];
        vm.IncludeDescendants = true;

        // 콤보만 바뀌고 목록은 그대로인 상태를 만들지 않는다 — 되돌리고 조회도 나가지 않는다.
        Assert.Null(vm.SelectedUnit);
        Assert.False(vm.IncludeDescendants);
        Assert.Equal(before, service.LoadCount);
    }
    #endregion

    #region - 등록 · 저장 -
    [Fact]
    public async Task should_offer_a_create_form_without_any_status_field()
    {
        var (vm, service, _, _) = Build();
        service.Categories.Add(new ServerCategoryOption(1, "방송", EnumServerType.SPEAKER_API, "SPEAKER_API"));
        await ActivateAsync(vm);

        Assert.True(vm.CanAdd);
        vm.Add();

        Assert.True(vm.Detail.IsCreating);
        Assert.True(vm.HasDetail);
        Assert.Contains("상태는 서버가 보고합니다", vm.Detail.CreateBanner);
        Assert.DoesNotContain(typeof(ServerWriteIntent).GetProperties(), p => p.Name.Contains("Status", StringComparison.Ordinal));
    }

    [Fact]
    public async Task should_keep_add_disabled_with_a_reason_when_no_category_arrived()
    {
        var (vm, _, _, _) = Build();
        await ActivateAsync(vm);

        Assert.False(vm.CanAdd);
        Assert.Contains("분류를 받지 못해", vm.AddBlockedReason);

        vm.Add();
        Assert.False(vm.Detail.IsCreating);
    }

    [Fact]
    public async Task should_send_a_create_request_with_the_chosen_category_when_applied()
    {
        var (vm, service, _, _) = Build();
        service.Categories.Add(new ServerCategoryOption(3, "방송", EnumServerType.SPEAKER_API, "SPEAKER_API"));
        await ActivateAsync(vm);

        vm.Add();
        vm.NameText = "새 서버";
        vm.IpText = "10.0.0.9";
        vm.PortText = "8080";
        await vm.ApplyAsync(CancellationToken.None);

        Assert.Single(service.Creates);
        Assert.Equal(3, service.Creates[0].Category.Id);
        Assert.Equal("새 서버", service.Creates[0].Intent.Name);
        Assert.Equal(ServerStatusRules.JustRegisteredNotice, vm.StatusText);
    }

    [Fact]
    public async Task should_send_one_save_request_when_settings_are_applied()
    {
        var (vm, service, _, _) = Build();
        service.Servers.Add(Entry(1, "a", EnumServerType.PROXY));
        await ActivateAsync(vm);

        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        vm.BeginEdit();
        vm.PortText = "9000";
        await vm.ApplyAsync(CancellationToken.None);

        Assert.Single(service.Saves);
        Assert.Equal(1, service.Saves[0].Id);
        Assert.Equal(9000, service.Saves[0].Intent.Port);
        Assert.False(vm.IsEditing);
        Assert.False(vm.Detail.IsDirty);
    }

    [Fact]
    public async Task should_ask_to_clear_a_field_with_an_explicit_flag_when_it_is_emptied()
    {
        var (vm, service, _, _) = Build();
        service.Servers.Add(Entry(1, "a", EnumServerType.PROXY));
        await ActivateAsync(vm);

        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        vm.BeginEdit();
        vm.HostnameText = string.Empty;
        await vm.ApplyAsync(CancellationToken.None);

        Assert.Single(service.Saves);
        Assert.True(service.Saves[0].Intent.ClearHostname);
    }

    [Fact]
    public async Task should_drop_the_edits_without_calling_the_server_when_reverted()
    {
        var (vm, service, _, _) = Build();
        service.Servers.Add(Entry(1, "a", EnumServerType.PROXY));
        await ActivateAsync(vm);

        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        vm.BeginEdit();
        vm.NameText = "다른 이름";
        vm.Revert();

        Assert.Empty(service.Saves);
        Assert.False(vm.Detail.IsDirty);
        Assert.Equal("a", vm.NameText);
    }
    #endregion

    #region - 배정 -
    [Fact]
    public async Task should_send_straight_out_when_a_single_device_is_assigned()
    {
        var (vm, service, devices, dialogs) = Build();
        service.Servers.Add(Entry(7, "방송서버", EnumServerType.SPEAKER_API));
        devices.CollectionEntity.Add(NewSpeaker(1));
        await ActivateAsync(vm);

        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        await vm.AssignSelectionAsync(vm.AssignCandidates.ToList());

        Assert.Single(service.Assigns);
        Assert.Empty(dialogs.Asked);          // 한 건은 묻지 않는다
        Assert.Empty(vm.Tray.Entries);
        Assert.True(vm.CanUndoAssign);
    }

    [Fact]
    public async Task should_queue_without_calling_the_server_when_several_devices_are_assigned()
    {
        var (vm, service, devices, _) = Build();
        service.Servers.Add(Entry(7, "방송서버", EnumServerType.SPEAKER_API));
        devices.CollectionEntity.Add(NewSpeaker(1));
        devices.CollectionEntity.Add(NewSpeaker(2));
        await ActivateAsync(vm);

        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        await vm.AssignSelectionAsync(vm.AssignCandidates.ToList());

        Assert.Empty(service.Assigns);        // 아직 0회
        Assert.Equal(2, vm.Tray.Count);
        Assert.Contains("지금은 0회", vm.StatusText);
    }

    [Fact]
    public async Task should_send_every_queued_write_when_the_tray_is_applied()
    {
        var (vm, service, devices, dialogs) = Build();
        service.Servers.Add(Entry(7, "방송서버", EnumServerType.SPEAKER_API));
        devices.CollectionEntity.Add(NewSpeaker(1));
        devices.CollectionEntity.Add(NewSpeaker(2));
        await ActivateAsync(vm);

        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        await vm.AssignSelectionAsync(vm.AssignCandidates.ToList());
        await vm.ApplyTrayAsync();

        Assert.Contains("2회", dialogs.Asked.Single());
        Assert.Equal(2, service.Assigns.Count);
        Assert.True(vm.CanUndoAssign);
    }

    [Fact]
    public async Task should_throw_the_queue_away_without_calling_the_server_when_reverted()
    {
        var (vm, service, devices, _) = Build();
        service.Servers.Add(Entry(7, "방송서버", EnumServerType.SPEAKER_API));
        devices.CollectionEntity.Add(NewSpeaker(1));
        devices.CollectionEntity.Add(NewSpeaker(2));
        await ActivateAsync(vm);

        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        await vm.AssignSelectionAsync(vm.AssignCandidates.ToList());
        vm.RevertTray();

        Assert.Empty(service.Assigns);
        Assert.Empty(vm.Tray.Entries);
        Assert.Contains("서버 호출 0회", vm.StatusText);
    }

    [Fact]
    public async Task should_keep_the_failed_entry_in_the_tray_when_one_write_is_refused()
    {
        var (vm, service, devices, _) = Build();
        service.Servers.Add(Entry(7, "방송서버", EnumServerType.SPEAKER_API));
        service.FailAssignForDeviceId = 2;
        devices.CollectionEntity.Add(NewSpeaker(1));
        devices.CollectionEntity.Add(NewSpeaker(2));
        await ActivateAsync(vm);

        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        await vm.AssignSelectionAsync(vm.AssignCandidates.ToList());
        await vm.ApplyTrayAsync();

        Assert.Equal(new[] { 1 }, service.Assigns.Select(a => a.DeviceId));
        Assert.Single(vm.Tray.Entries);      // 실패한 것만 남는다
    }

    [Fact]
    public async Task should_offer_a_camera_to_an_nvr_server_on_the_axis_contract()
    {
        var (vm, service, devices, _) = Build();
        service.Servers.Add(Entry(7, "NVR", EnumServerType.NVR_API));
        devices.CollectionEntity.Add(NewCamera(5));
        await ActivateAsync(vm);

        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        await vm.AssignSelectionAsync(vm.AssignCandidates.ToList());

        Assert.Equal(new[] { 5 }, service.Assigns.Select(a => a.DeviceId));
    }

    [Fact]
    public async Task should_only_offer_speakers_when_the_contract_is_legacy()
    {
        var (vm, service, devices, _) = Build(EnumServerContract.V6_3);
        service.Servers.Add(Entry(7, "NVR", EnumServerType.NVR_API));
        devices.CollectionEntity.Add(NewCamera(5));
        devices.CollectionEntity.Add(NewSpeaker(1));
        await ActivateAsync(vm);

        Assert.Equal(new[] { 1 }, vm.AssignCandidates.Select(c => c.Id));
        Assert.Contains("6.3", vm.AssignHint);
    }

    [Fact]
    public async Task should_not_send_anything_when_the_server_type_refuses_the_device()
    {
        var (vm, service, devices, _) = Build();
        service.Servers.Add(Entry(7, "NVR", EnumServerType.NVR_API));
        devices.CollectionEntity.Add(NewSpeaker(1));
        await ActivateAsync(vm);

        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        await vm.AssignSelectionAsync(vm.AssignCandidates.ToList());

        Assert.Empty(service.Assigns);
        Assert.Contains("NVR", vm.StatusText);
    }

    [Fact]
    public async Task should_detach_a_device_that_had_no_previous_server_when_undone_on_the_axis_contract()
    {
        var (vm, service, devices, _) = Build();
        service.Servers.Add(Entry(7, "방송서버", EnumServerType.SPEAKER_API));
        devices.CollectionEntity.Add(NewSpeaker(1));           // 이전 서버 없음
        await ActivateAsync(vm);

        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        await vm.AssignSelectionAsync(vm.AssignCandidates.ToList());
        service.Assigns.Clear();

        await vm.UndoAssignAsync();

        // server_id: null 이 해제다(app/schemas/device.py:740).
        Assert.Equal(new (int, int?)[] { (1, null) }, service.Assigns.Select(a => (a.DeviceId, a.ServerId)));
    }

    [Fact]
    public async Task should_say_it_cannot_undo_when_the_contract_has_no_detach_entry()
    {
        var (vm, service, devices, _) = Build(EnumServerContract.V6_3);
        service.Servers.Add(Entry(7, "방송서버", EnumServerType.SPEAKER_API));
        devices.CollectionEntity.Add(NewSpeaker(1));
        await ActivateAsync(vm);

        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        await vm.AssignSelectionAsync(vm.AssignCandidates.ToList());
        service.Assigns.Clear();

        await vm.UndoAssignAsync();

        Assert.Empty(service.Assigns);
        Assert.Contains("해제 입구가 없어", vm.StatusText);
    }

    [Fact]
    public async Task should_restore_the_previous_server_when_undone()
    {
        var (vm, service, devices, _) = Build();
        service.Servers.Add(Entry(7, "방송서버", EnumServerType.SPEAKER_API));
        devices.CollectionEntity.Add(NewSpeaker(2, previousServer: 9));
        await ActivateAsync(vm);

        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        await vm.AssignSelectionAsync(vm.AssignCandidates.ToList());
        service.Assigns.Clear();

        await vm.UndoAssignAsync();

        Assert.Equal(new (int, int?)[] { (2, 9) }, service.Assigns.Select(a => (a.DeviceId, a.ServerId)));
    }
    #endregion

    #region - 그 밖 -
    [Fact]
    public async Task should_open_the_metric_history_when_the_button_is_pressed()
    {
        var (vm, service, _, dialogs) = Build();
        service.Servers.Add(Entry(1, "a", EnumServerType.PROXY));
        await ActivateAsync(vm);
        vm.OnRowsSelected(new List<object> { vm.Rows[0] });

        Assert.True(vm.CanShowHistory);
        await vm.ShowHistoryAsync();
        Assert.Equal(1, dialogs.HistoryOpened);
    }

    [Fact]
    public async Task should_never_offer_server_deletion_from_this_console()
    {
        var (vm, _, _, _) = Build();
        await ActivateAsync(vm);

        Assert.False(vm.CanDelete);
        Assert.Contains("제공하지 않습니다", vm.DeleteBlockedReason);
    }

    [Fact]
    public async Task should_say_no_liveness_signal_is_connected()
    {
        var (vm, _, _, _) = Build();
        await ActivateAsync(vm);

        Assert.Contains("생존 신호", vm.LivenessNote);
        Assert.Contains("REST 로는", vm.LivenessNote);
    }

    [Fact]
    public async Task should_let_go_of_selection_and_edits_when_the_console_is_closed()
    {
        var (vm, service, _, _) = Build();
        service.Servers.Add(Entry(1, "a", EnumServerType.PROXY));
        await ActivateAsync(vm);
        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        vm.BeginEdit();
        vm.NameText = "고침";

        await ((IDeactivate)vm).DeactivateAsync(true);

        Assert.Empty(vm.Rows);
        Assert.False(vm.Detail.IsDirty);
        Assert.False(vm.IsEditing);
        Assert.False(vm.CanUndoAssign);
        Assert.False(vm.HasDetail);
    }
    #endregion
}
