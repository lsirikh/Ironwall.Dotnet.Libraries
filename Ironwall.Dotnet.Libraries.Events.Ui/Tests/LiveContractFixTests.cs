using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Detail;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Lists;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Overview;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 실서버 왕복(tools/live-api-roundtrip events-vm E1~E9)으로 확인한 결함의 헤드리스 회귀 망.
                  왕복이 증명한 서버 계약을 그대로 옮긴다 — 서버 없이 도는 쪽이 회귀를 먼저 잡는다.
   Created On   : 2026-09-24
****************************************************************************/

#region - E1 조치보고 규칙 -
public class ActionReportRulesTests
{
    private static PermissionService Perm(string role, string modulesJson)
    {
        var p = new PermissionService();
        p.Apply(new AuthUserDto { Role = role, LoginId = "t", Permissions = JObject.Parse("{\"modules\":" + modulesJson + "}") });
        return p;
    }

    [Fact]
    public void should_refuse_report_when_user_has_events_control_but_not_edit()
    {
        // 서버 운영자 프리셋 = events RC. 서버 POST /events/actions 는 events:edit 를 요구한다(E1a 실측 403).
        var op = Perm("USER", "{\"events\":{\"view\":true,\"edit\":false,\"delete\":false,\"control\":true}}");

        Assert.True(op.CanControl("events"));
        Assert.False(ActionReportRules.CanReport(op));
    }

    [Fact]
    public void should_allow_report_when_user_has_events_edit()
    {
        var maintainer = Perm("USER", "{\"events\":{\"view\":true,\"edit\":true,\"delete\":false,\"control\":false}}");
        Assert.True(ActionReportRules.CanReport(maintainer));
    }

    [Fact]
    public void should_allow_report_when_permission_service_is_not_registered()
        => Assert.True(ActionReportRules.CanReport(null));   // 기존 폴백 유지 — 최종 방어는 서버 403

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void should_reject_content_when_empty_or_blank(string? content)
        => Assert.NotNull(ActionReportRules.ValidateContent(content));

    [Fact]
    public void should_reject_content_when_longer_than_server_limit()
        => Assert.Contains("500", ActionReportRules.ValidateContent(new string('가', 501)));

    [Fact]
    public void should_accept_content_when_within_limit()
    {
        Assert.Null(ActionReportRules.ValidateContent("오경보"));
        Assert.Null(ActionReportRules.ValidateContent(new string('가', 500)));
    }
}
#endregion

#region - E2 억제 생성 부대 -
public class SuppressionUnitStampTests
{
    private sealed class FakeScope : IUnitScopeService
    {
        public bool IsUnitEra { get; set; } = true;
        public string? UnitCode => "unit001";
        public int? CurrentUnitId { get; set; } = 7;
        public bool IsResolved => CurrentUnitId is not null;
        public bool Throw { get; set; }
        public Task<int?> ResolveAsync(CancellationToken token = default)
            => Throw ? throw new InvalidOperationException("boom") : Task.FromResult(CurrentUnitId);
        public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task should_return_own_unit_when_contract_is_unit_era()
        => Assert.Equal(7, await SuppressionUnitStamp.ResolveForCreateAsync(new FakeScope(), null, "t"));

    [Fact]
    public async Task should_omit_unit_when_contract_is_before_8_0()
        => Assert.Null(await SuppressionUnitStamp.ResolveForCreateAsync(new FakeScope { IsUnitEra = false }, null, "t"));

    [Fact]
    public async Task should_omit_unit_when_scope_is_missing_or_throws()
    {
        Assert.Null(await SuppressionUnitStamp.ResolveForCreateAsync(null, null, "t"));
        Assert.Null(await SuppressionUnitStamp.ResolveForCreateAsync(new FakeScope { Throw = true }, null, "t"));
    }

    [Fact]
    public async Task should_send_own_unit_when_console_creates_a_schedule()
    {
        PlatformProvider.Current = new DefaultPlatformProvider();
        var api = new FakeSuppressionApi();
        var console = new SuppressionConsoleViewModel(new EventAggregator(), null, api, null, null,
            new FakeClock(new DateTime(2026, 9, 20, 10, 0, 0)), unitScope: () => new FakeScope());

        console.AddNew();
        console.Drawer.Name = "정비";
        console.Drawer.IsAllMode = true;
        await console.Drawer.SaveAsync();

        Assert.Equal(1, api.CreateCalls);
        Assert.Equal(7, api.LastCreate!.UnitId);
    }

    [Fact]
    public async Task should_not_send_unit_when_console_patches_a_schedule()
    {
        PlatformProvider.Current = new DefaultPlatformProvider();
        var api = new FakeSuppressionApi();
        api.Stored.Add(new EventSuppressionScheduleDto
        {
            Id = 3, Name = "정비", Status = "pending", UnitId = 9, TargetType = "all", TargetSide = "both", EventScope = "all",
            WindowStart = "2026-09-21T00:00:00+09:00", WindowEnd = "2026-09-21T01:00:00+09:00", RecurrenceType = "none",
        });
        var console = new SuppressionConsoleViewModel(new EventAggregator(), null, api, null, null,
            new FakeClock(new DateTime(2026, 9, 20, 10, 0, 0)), unitScope: () => new FakeScope());
        await console.LoadAsync();
        console.Selected = console.Schedules.Single();
        await console.EditSelectedAsync();
        console.Drawer.Name = "정비-수정";
        await console.Drawer.SaveAsync();

        Assert.Equal(1, api.PatchCalls);
        Assert.Null(api.LastUpdate!.UnitId);   // RFC 7396: 보내지 않으면 원래 부대가 남는다(E2c 실측 diffs=[])
    }
}
#endregion

#region - E3 개요 통계 -
public class EventOverviewServerContractTests
{
    private static EventDashboardDto Dashboard(string interval, params EventTrendItemDto[] series) => new()
    {
        Summary = new EventSummaryDto
        {
            Total = 5, SensorDetection = 2, Alert = 1, Malfunction = 1, Action = 1, Operation = 3, DaysInRange = 1,
        },
        Trend = new EventTrendDto { Interval = interval, Series = series.ToList() },
        ByDevice = new EventByDeviceDto
        {
            Enclosures = new List<DeviceEventStatsDto> { new() { DeviceId = 10, DeviceName = "함체-1", Operation = 3 } },
        },
    };

    [Fact]
    public void should_match_server_total_when_alerts_exist()
    {
        var vm = new EventOverviewViewModel();
        vm.Load(Dashboard("hour"), new DateTime(2026, 9, 24, 0, 0, 0), new DateTime(2026, 9, 24, 12, 0, 0));

        Assert.Equal(5, vm.Total);                                            // 서버 total 은 alert 를 포함한다
        Assert.Equal(1, vm.Slices.Single(s => s.Spec.Key == "alert").Count);
        Assert.Contains("운영 3건", vm.OperationCountText);                              // 총계 밖 운영을 숨기지 않는다
    }

    [Fact]
    public void should_show_enclosure_bars_when_facility_group_selected()
    {
        var vm = new EventOverviewViewModel();
        vm.Load(Dashboard("hour"), new DateTime(2026, 9, 24, 0, 0, 0), new DateTime(2026, 9, 24, 12, 0, 0));

        vm.DeviceGroup = OverviewDeviceGroup.Facility;

        var bar = Assert.Single(vm.Bars);
        Assert.Equal("함체-1", bar.Name);
        Assert.Equal(3, bar.Total);
    }

    [Fact]
    public void should_use_server_interval_when_trend_says_hour_for_a_long_range()
    {
        var vm = new EventOverviewViewModel();
        vm.Load(Dashboard("hour", new EventTrendItemDto { TimeBucket = "2026-09-22 05", SensorDetection = 1 }),
                new DateTime(2026, 9, 21, 0, 0, 0), new DateTime(2026, 9, 24, 0, 0, 0));

        Assert.Equal(TimeSpan.FromHours(1), vm.Bucket);                        // 짐작(1일)이 아니라 서버가 집계한 단위
        Assert.Equal("1시간 단위", vm.IntervalText);
    }

    [Fact]
    public void should_fill_every_hour_when_server_returns_sparse_buckets()
    {
        var sparse = new List<EventTrendItemDto>
        {
            new() { TimeBucket = "2026-09-24 01", SensorDetection = 2 },
            new() { TimeBucket = "2026-09-24 04", Malfunction = 1 },
        };

        var dense = EventTrendBuckets.Densify(sparse, new DateTime(2026, 9, 24, 0, 30, 0), new DateTime(2026, 9, 24, 5, 10, 0), TimeSpan.FromHours(1));

        Assert.Equal(6, dense.Count);                                          // 00 · 01 · 02 · 03 · 04 · 05
        Assert.Equal(2, dense[1].Item.SensorDetection);
        Assert.Equal(1, dense[4].Item.Malfunction);
        Assert.True(dense[2].IsFilled);
        Assert.Equal("01시", EventTrendBuckets.Label(dense[1], TimeSpan.FromHours(1)));
        Assert.Equal("09-24", EventTrendBuckets.Label(dense[0], TimeSpan.FromHours(1)));   // 자정 칸은 날짜
    }

    [Fact]
    public void should_keep_server_buckets_as_is_when_a_bucket_is_outside_the_range()
    {
        var sparse = new List<EventTrendItemDto> { new() { TimeBucket = "2026-09-30 01", SensorDetection = 9 } };

        var result = EventTrendBuckets.Densify(sparse, new DateTime(2026, 9, 24, 0, 0, 0), new DateTime(2026, 9, 24, 5, 0, 0), TimeSpan.FromHours(1));

        Assert.Single(result);                                                 // 버리지 않는다
        Assert.Equal(9, result[0].Item.SensorDetection);
    }

    [Theory]
    [InlineData("2026-09-24 01", 1)]
    [InlineData("2026-09-24", 0)]
    public void should_parse_server_time_bucket_when_format_has_no_minutes(string bucket, int hour)
    {
        Assert.True(EventTrendBuckets.TryParseBucket(bucket, out var at));
        Assert.Equal(hour, at.Hour);
    }

    [Fact]
    public void should_request_day_interval_when_range_is_longer_than_a_day()
    {
        Assert.Equal("day", EventTrendRangeMath.IntervalFor(new DateTime(2026, 9, 21), new DateTime(2026, 9, 24)));
        Assert.Equal("hour", EventTrendRangeMath.IntervalFor(new DateTime(2026, 9, 23, 12, 0, 0), new DateTime(2026, 9, 24, 12, 0, 0)));
    }
}
#endregion

#region - E4 맵핑 워크벤치 -
public class MappingLiveContractTests
{
    [Fact]
    public void should_keep_not_found_rows_as_failed_when_bulk_create_reports_missing_devices()
    {
        var outcome = new MappingCommitOutcome();
        var rows = new[] { MappingBoardRow.NewFor(MappingActionKind.Speaker, 101), MappingBoardRow.NewFor(MappingActionKind.Speaker, 102) };

        outcome.AcceptCreate(rows, new MappingBulkCreateResultDto
        {
            CreatedIds = new List<int> { 701 },
            NotFoundConfigIds = new List<int> { 102 },   // 서버: 등록 응답의 이 목록은 '장비 id' 다
        });

        Assert.Equal(1, outcome.Failed);
        Assert.Same(rows[1], Assert.Single(outcome.FailedRows));
        Assert.Same(rows[0], Assert.Single(outcome.SettledRows).Row);
    }

    [Fact]
    public void should_count_unmatched_not_found_ids_when_no_row_carries_them()
    {
        var outcome = new MappingCommitOutcome();
        outcome.AcceptCreate(new[] { MappingBoardRow.NewFor(MappingActionKind.Camera, 5) },
            new MappingBulkCreateResultDto { CreatedIds = new List<int> { 1 }, NotFoundConfigIds = new List<int> { 999 } });

        Assert.Equal(1, outcome.Failed);                // 수는 숨기지 않는다
        Assert.Empty(outcome.FailedRows);
    }

    [Fact]
    public void should_preserve_updated_at_text_when_envelope_is_parsed()
    {
        const string raw = "{\"success\":true,\"data\":{\"id\":3,\"name_event\":\"m\",\"updated_at\":\"2026-09-24T01:55:48.053887+09:00\"}}";

        var dto = MappingWorkbenchGateway.ReadOne<EventMappingReadDto>(MappingWorkbenchGateway.Parse(raw));

        Assert.Equal("2026-09-24T01:55:48.053887+09:00", dto!.UpdatedAt);   // 예전: "09/24/2026 01:55:48"
    }
}
#endregion

#region - E5 · E8 detail 보존 · 삭제 장비 스냅샷 -
[Collection("IoC-Dependent")]
public class EventDetailCarryTests : IoCStubbedTestBase   // 행 뷰모델은 생성자에서 IoC 를 본다
{
    [Fact]
    public void should_keep_vendor_keys_when_detection_result_is_replaced()
    {
        var dto = JsonConvert.DeserializeObject<DetectionEventDto>(
            "{\"id\":1,\"type_event\":\"Intrusion\",\"result\":\"PIR_SENSOR\",\"created_at\":\"2026-09-24T01:00:00+09:00\",\"detail\":{\"signal\":5,\"vendor_x\":1}}")!;
        var model = dto.ToDetectionEventModel(null);
        model.Result = EnumDetectionType.VIBRATION_SENSOR;

        var body = JObject.FromObject(model.ToDetectionEventReplaceDto());

        Assert.Equal(5, (int)body["detail"]!["signal"]!);
        Assert.Equal(1, (int)body["detail"]!["vendor_x"]!);
    }

    [Fact]
    public void should_keep_detail_when_it_only_has_vendor_keys()
    {
        var dto = JsonConvert.DeserializeObject<DetectionEventDto>(
            "{\"id\":2,\"type_event\":\"Intrusion\",\"result\":\"PIR_SENSOR\",\"detail\":{\"vendor_only\":\"abc\"}}")!;
        var body = JObject.FromObject(dto.ToDetectionEventModel(null).ToDetectionEventReplaceDto());

        Assert.Equal("abc", (string)body["detail"]!["vendor_only"]!);
    }

    [Fact]
    public void should_leave_malfunction_detail_null_when_it_was_null()
    {
        var dto = JsonConvert.DeserializeObject<MalfunctionEventDto>("{\"id\":3,\"type_event\":\"Fault\",\"reason\":\"FAULT_CONTROLLER\",\"detail\":null}")!;
        var model = dto.ToMalfunctionEventModel(null);
        model.Reason = EnumFaultType.FAULT_FENCE;

        Assert.Null(model.ToMalfunctionEventReplaceDto().Detail);
    }

    [Fact]
    public void should_not_add_absent_sections_when_malfunction_detail_had_only_first_line()
    {
        var dto = JsonConvert.DeserializeObject<MalfunctionEventDto>(
            "{\"id\":4,\"type_event\":\"Fault\",\"reason\":\"FAULT_CONTROLLER\",\"detail\":{\"first_start\":10,\"first_end\":20,\"vendor_y\":\"k\"}}")!;
        var body = JObject.FromObject(dto.ToMalfunctionEventModel(null).ToMalfunctionEventReplaceDto());
        var detail = (JObject)body["detail"]!;

        Assert.Equal(10, (int)detail["first_start"]!);
        Assert.Null(detail["second_start"]);
        Assert.Equal("k", (string)detail["vendor_y"]!);
    }

    [Fact]
    public void should_send_all_sections_when_model_was_not_read_from_server()
    {
        // 곁들인 모양이 없는 모델(직접 만든 것 · NATS) — 종전 동작 그대로.
        var model = new MalfunctionEventModel { Id = 5, Reason = EnumFaultType.FAULT_FENCE, FirstStart = 1 };
        var detail = model.ToMalfunctionEventReplaceDto().Detail!;

        Assert.Equal(0, detail.SecondStart);
    }

    [Fact]
    public void should_show_server_snapshot_when_device_was_deleted()
    {
        var dto = JsonConvert.DeserializeObject<DetectionEventDto>(
            "{\"id\":6,\"type_event\":\"Intrusion\",\"result\":\"PIR_SENSOR\",\"device\":null,\"device_description\":\"[sensor:PIR] 북측 1 (number: 3, id: 9)\"}")!;
        var row = new DetectionEventViewModel(dto.ToDetectionEventModel(null));

        // 목록 · 제목은 짧게(종류 코드 · 번호 없이), 원문은 툴팁으로 남는다(완성도 감사 E-4 #3).
        Assert.Equal("삭제된 장비 (북측 1)", row.DeviceLabel);
        Assert.Equal(EventDeviceSnapshot.DeletedPrefix + "[sensor:PIR] 북측 1 (number: 3, id: 9)", row.DeviceSnapshotText);
    }

    [Theory]
    [InlineData("[sensor:PIR] 북측 1 (number: 3, id: 9)", "북측 1")]
    [InlineData("정문 카메라", "정문 카메라")]
    [InlineData("[camera:PTZ] 남문 돔", "남문 돔")]
    [InlineData("(id: 9)", "(id: 9)")]
    public void should_strip_kind_tag_and_trailing_numbers_when_shortening_snapshot(string snapshot, string expected)
        => Assert.Equal(expected, EventDeviceSnapshot.ShortName(snapshot));
}
#endregion

#region - E6 · E7 칩 · 트레이 -
public class EventListChipContractTests
{
    [Theory]
    [InlineData(EnumEventType.Intrusion, EventListFilter.ChipIntrusion)]
    [InlineData(EnumEventType.Alert, EventListFilter.ChipAlert)]
    [InlineData(EnumEventType.ContactOn, EventListFilter.ChipContact)]
    [InlineData(EnumEventType.ContactOff, EventListFilter.ChipContact)]
    [InlineData(EnumEventType.WindyMode, EventListFilter.ChipContact)]
    public void should_route_detection_type_to_its_own_chip_when_server_sends_it(EnumEventType type, string chip)
        => Assert.Equal(chip, EventRowFactsFactory.TypeEventKeyOf(type));

    [Fact]
    public void should_offer_no_state_chips_when_rail_is_connection()
        => Assert.Empty(EventListFilter.ChipsFor(EventDetailKind.Connection));   // 서버 연결 이벤트에는 상태 칸이 없다

    [Fact]
    public void should_block_tray_apply_when_etc_memo_exceeds_server_limit()
    {
        var tray = new ActionTrayViewModel((_, _, _) => Task.FromResult(DraftOutcome.Applied));
        tray.Enqueue(ActionTrayDrop.Plan(new[] { new ActionTrayCandidate(1, ActionTrayDrop.KindDetection, "a", false) }, true));
        tray.Phrase = ActionTrayViewModel.EtcPhrase;

        tray.Memo = new string('가', 501);

        Assert.False(tray.CanApply);
        Assert.Contains("500", tray.ApplyBlockedReason);
    }
}
#endregion

#region - E9 무한 스크롤 중복 -
[Collection("IoC-Dependent")]
public class InfiniteScrollDedupTests : IoCStubbedTestBase
{
    private static DetectionEventDto Dto(int id) => new()
    {
        Id = id, TypeEvent = "Intrusion", Result = "PIR_SENSOR", ActionReported = "False", CreatedAt = "2026-09-24T01:00:00+09:00",
    };

    [Fact]
    public async Task should_not_append_duplicate_ids_when_next_page_overlaps()
    {
        // 실서버 E9b: page1=[511,510] · page2=[510,509] — 쪽 사이에 새 이벤트가 생기면 겹친다.
        var api = new Mock<IEventApiService>();
        api.Setup(a => a.GetDetectionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ApiListResponse<DetectionEventDto>
           {
               Success = true,
               Data = new List<DetectionEventDto> { Dto(510), Dto(509) },
               Pagination = new PaginationDto { Page = 2, Limit = 2, Total = 4, TotalPages = 2 },
           });
        var errors = new List<string>();
        var logMock = new Mock<ILogService>();
        logMock.Setup(l => l.Error(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>())).Callback<string, string, string, int>((m, _, _, _) => errors.Add(m));
        var log = logMock.Object;
        var panel = new DetectionEventPanelViewModel(new EventAggregator(), log, new EventProviderService(log, api.Object), new DeviceProvider(), new EventProvider());
        panel.SetDate(new DateTime(2026, 9, 24, 0, 0, 0), new DateTime(2026, 9, 24, 3, 0, 0));
        panel.ViewModelProvider.Add(new DetectionEventViewModel(new DetectionEventModel { Id = 511 }));
        panel.ViewModelProvider.Add(new DetectionEventViewModel(new DetectionEventModel { Id = 510 }));
        SetPaging(panel, currentPage: 1, totalPages: 2);

        await panel.LoadNextPageAsync();

        Assert.Empty(errors);
        Assert.Equal(new[] { 511, 510, 509 }, panel.ViewModelProvider.Select(v => v.Model.Id).ToArray());
    }

    // 페이지 상태는 첫 조회가 채운다 — 이 시험은 두 번째 쪽만 보므로 그 두 칸만 맞춘다.
    private static void SetPaging(object panel, int currentPage, int totalPages)
    {
        var t = panel.GetType();
        t.GetField("_currentPage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(panel, currentPage);
        t.GetField("_totalPages", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(panel, totalPages);
    }
}
#endregion
