using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
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
    public bool CanReadProxySettings => Contract <= EnumServerContract.V6_3;

    public List<ServerListEntry> Servers { get; } = new();
    public List<ServerUnitOption> Units { get; } = new();
    public List<ServerCategoryOption> Categories { get; } = new();
    public ServerMetricDto? LatestMetric { get; set; }

    public List<(int DeviceId, int ServerId)> Assigns { get; } = new();
    public List<(int Id, ServerEditDraft Draft)> Saves { get; } = new();
    public List<(int CategoryId, ServerEditDraft Draft)> Creates { get; } = new();
    public int LoadCount { get; private set; }

    /// <summary>이 장비 Id 에서 배정이 실패한다.</summary>
    public int FailAssignForDeviceId { get; set; }

    public Task<ServerLoadResult> LoadAsync(int? unitId, bool includeDescendants, CancellationToken token = default)
    {
        LoadCount++;
        return Task.FromResult(new ServerLoadResult(Servers.ToList(), Units.ToList(), Categories.ToList(), false, null));
    }

    public Task<ServerDto?> GetAsync(int id, CancellationToken token = default)
        => Task.FromResult(Servers.FirstOrDefault(s => s.Dto.Id == id)?.Dto);

    public Task<ServerWriteResult> SaveAsync(int id, ServerEditDraft draft, CancellationToken token = default)
    {
        Saves.Add((id, draft));
        return Task.FromResult(new ServerWriteResult(true, "설정을 저장했습니다"));
    }

    public Task<(ServerWriteResult Result, int NewId)> CreateAsync(int categoryId, ServerEditDraft draft, CancellationToken token = default)
    {
        Creates.Add((categoryId, draft));
        return Task.FromResult((new ServerWriteResult(true, ServerStatusRules.JustRegisteredNotice), 99));
    }

    public Task<ServerMetricDto?> LatestMetricAsync(int id, CancellationToken token = default) => Task.FromResult(LatestMetric);

    public Task<IReadOnlyList<ServerMetricDto>> MetricHistoryAsync(int id, int limit = 50, CancellationToken token = default)
        => Task.FromResult<IReadOnlyList<ServerMetricDto>>(Array.Empty<ServerMetricDto>());

    public Task<(ProxySettingDto? Setting, string? Note)> OperationModeAsync(int id, CancellationToken token = default)
        => Task.FromResult<(ProxySettingDto?, string?)>((null, "모드 안내"));

    public Task<ServerWriteResult> AssignSpeakerAsync(int speakerId, int serverId, CancellationToken token = default)
    {
        if (speakerId == FailAssignForDeviceId) return Task.FromResult(new ServerWriteResult(false, "서버가 거절했습니다"));
        Assigns.Add((speakerId, serverId));
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
    private static readonly DateTime Now = new(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);

    private static ServerListEntry Entry(int id, string name, EnumServerType type, string status = "NORMAL", string? updatedAt = "2026-09-20T08:59:30+00:00")
        => new(new ServerDto
        {
            Id = id,
            Name = name,
            CategoryId = 1,
            Status = status,
            IpAddress = $"10.0.0.{id}",
            Port = 8000 + id,
            UpdatedAt = updatedAt,
            CreatedAt = updatedAt,
        }, type, type.ToString(), "1대대");

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

    [Fact]
    public async Task should_show_every_rail_slot_with_counts_when_servers_loaded()
    {
        var (vm, service, _, _) = Build();
        service.Servers.Add(Entry(1, "스피커서버", EnumServerType.SPEAKER_API));
        service.Servers.Add(Entry(2, "NVR", EnumServerType.NVR_API, "ERROR"));
        service.Servers.Add(Entry(3, "백업", EnumServerType.BACKUP, "NORMAL", updatedAt: null));

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
        Assert.True(vm.IsServerList);
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
        service.Servers.Add(Entry(1, "새 서버", EnumServerType.PROXY, "NORMAL", updatedAt: null));
        await ActivateAsync(vm);

        Assert.True(vm.Rows[0].IsNotReported);
        Assert.Equal("보고 없음", vm.Rows[0].StatusText);
        Assert.Equal("보고 없음", vm.Rows[0].LastChangeText);
        Assert.False(vm.Rows[0].IsFault);
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
    public async Task should_open_the_detail_read_only_until_edit_is_pressed()
    {
        var (vm, service, _, _) = Build();
        service.Servers.Add(Entry(1, "a", EnumServerType.PROXY));
        await ActivateAsync(vm);

        vm.OnRowsSelected(new List<object> { vm.Rows[0] });

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
    }

    [Fact]
    public async Task should_offer_a_create_form_without_any_status_field()
    {
        var (vm, service, _, _) = Build();
        service.Categories.Add(new ServerCategoryOption(1, "방송", EnumServerType.SPEAKER_API));
        await ActivateAsync(vm);

        Assert.True(vm.CanAdd);
        vm.Add();

        Assert.True(vm.Detail.IsCreating);
        Assert.Contains("상태는 서버가 보고합니다", vm.Detail.CreateBanner);

        // 등록 폼이 실어 보낼 수 있는 것에 상태가 없다 — 초안 타입에 그런 칸 자체가 없다(관측 필드를 쓰면 422).
        Assert.DoesNotContain(typeof(ServerEditDraft).GetProperties(), p => p.Name.Contains("Status", StringComparison.Ordinal));
    }

    [Fact]
    public async Task should_keep_add_disabled_with_a_reason_when_no_category_arrived()
    {
        var (vm, _, _, _) = Build();
        await ActivateAsync(vm);

        Assert.False(vm.CanAdd);
        Assert.Contains("분류를 받지 못해", vm.AddBlockedReason);

        vm.Add();
        Assert.False(vm.Detail.IsCreating);      // 꺼진 단추는 아무 일도 하지 않는다
    }

    [Fact]
    public async Task should_send_a_create_request_when_the_form_is_applied()
    {
        var (vm, service, _, _) = Build();
        service.Categories.Add(new ServerCategoryOption(3, "방송", EnumServerType.SPEAKER_API));
        await ActivateAsync(vm);

        vm.Add();
        vm.NameText = "새 서버";
        vm.IpText = "10.0.0.9";
        vm.PortText = "8080";
        await vm.ApplyAsync(CancellationToken.None);

        Assert.Single(service.Creates);
        Assert.Equal(3, service.Creates[0].CategoryId);
        Assert.Equal("새 서버", service.Creates[0].Draft.Name);
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
        Assert.Equal(9000, service.Saves[0].Draft.Port);
        Assert.False(vm.IsEditing);
        Assert.False(vm.Detail.IsDirty);
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

    [Fact]
    public async Task should_ask_before_assigning_when_more_than_one_write_goes_out()
    {
        var (vm, service, devices, dialogs) = Build();
        service.Servers.Add(Entry(7, "방송서버", EnumServerType.SPEAKER_API));
        devices.CollectionEntity.Add(NewSpeaker(1));
        devices.CollectionEntity.Add(NewSpeaker(2));
        await ActivateAsync(vm);

        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        await vm.AssignSelectionAsync(vm.AssignCandidates.ToList());

        Assert.Single(dialogs.Asked);
        Assert.Contains("서버 쓰기 2회", dialogs.Asked[0]);
        Assert.Equal(2, service.Assigns.Count);
        Assert.True(vm.CanUndoAssign);
    }

    [Fact]
    public async Task should_send_nothing_when_the_confirmation_is_declined()
    {
        var (vm, service, devices, dialogs) = Build();
        dialogs.Answer = false;
        service.Servers.Add(Entry(7, "방송서버", EnumServerType.SPEAKER_API));
        devices.CollectionEntity.Add(NewSpeaker(1));
        devices.CollectionEntity.Add(NewSpeaker(2));
        await ActivateAsync(vm);

        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        await vm.AssignSelectionAsync(vm.AssignCandidates.ToList());

        Assert.Empty(service.Assigns);
        Assert.Contains("서버 호출 0회", vm.StatusText);
    }

    [Fact]
    public async Task should_stop_at_the_first_failure_when_assigning_many()
    {
        var (vm, service, devices, _) = Build();
        service.Servers.Add(Entry(7, "방송서버", EnumServerType.SPEAKER_API));
        service.FailAssignForDeviceId = 2;
        devices.CollectionEntity.Add(NewSpeaker(1));
        devices.CollectionEntity.Add(NewSpeaker(2));
        devices.CollectionEntity.Add(NewSpeaker(3));
        await ActivateAsync(vm);

        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        await vm.AssignSelectionAsync(vm.AssignCandidates.ToList());

        // 1 은 나갔고 2 에서 멈췄다 — 3 은 보내지 않는다.
        Assert.Equal(new[] { 1 }, service.Assigns.Select(a => a.DeviceId));
        Assert.Contains("멈췄습니다", vm.StatusText);
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
        Assert.False(vm.CanAssignSelection);
    }

    [Fact]
    public async Task should_restore_only_the_devices_that_had_a_previous_server_when_undone()
    {
        var (vm, service, devices, dialogs) = Build();
        dialogs.Answer = true;
        service.Servers.Add(Entry(7, "방송서버", EnumServerType.SPEAKER_API));
        devices.CollectionEntity.Add(NewSpeaker(1));                 // 이전 서버 없음 → 되돌릴 수 없다
        devices.CollectionEntity.Add(NewSpeaker(2, previousServer: 9));
        await ActivateAsync(vm);

        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        await vm.AssignSelectionAsync(vm.AssignCandidates.ToList());
        service.Assigns.Clear();

        await vm.UndoAssignAsync();

        Assert.Equal(new[] { (2, 9) }, service.Assigns.Select(a => (a.DeviceId, a.ServerId)));
        Assert.Contains("되돌릴 수 없습니다", vm.StatusText);
    }

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
    }

    private static SpeakerDeviceModel NewSpeaker(int id, int? previousServer = null) => new()
    {
        Id = id,
        DeviceName = $"스피커{id}",
        CategoryDevice = EnumDeviceCategory.Speaker,
        Server = previousServer is null ? null : new ServerModel { Id = previousServer.Value, Name = $"서버{previousServer}" },
    };
}
