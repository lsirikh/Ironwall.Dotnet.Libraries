using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Servers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
/****************************************************************************
   Purpose      : 장비 → 서버 배정 판정 · 지표 매핑 검증 (N-12)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

public class ServerDropRulesTests
{
    private static SpeakerDeviceModel Speaker(int id, int? serverId = null) => new()
    {
        Id = id,
        DeviceName = $"스피커{id}",
        CategoryDevice = EnumDeviceCategory.Speaker,
        Server = serverId is null ? null : new ServerModel { Id = serverId.Value, Name = $"서버{serverId}" },
    };

    [Theory]
    [InlineData(EnumDeviceCategory.Speaker, true)]
    [InlineData(EnumDeviceCategory.Sensor, false)]
    [InlineData(EnumDeviceCategory.Camera, false)]
    [InlineData(EnumDeviceCategory.Controller, false)]
    [InlineData(EnumDeviceCategory.Enclosure, false)]
    [InlineData(EnumDeviceCategory.Lamp, false)]
    [InlineData(EnumDeviceCategory.Gate, false)]
    public void should_accept_only_speakers_when_target_is_a_speaker_server(EnumDeviceCategory category, bool expected)
        => Assert.Equal(expected, ServerDropRules.Accepts(EnumServerType.SPEAKER_API, category));

    [Theory]
    [InlineData(EnumServerType.NVR_API)]
    [InlineData(EnumServerType.PROXY)]
    [InlineData(EnumServerType.ENCLOSURE_API)]
    [InlineData(null)]
    public void should_refuse_every_device_when_server_type_is_not_a_speaker_server(EnumServerType? type)
    {
        Assert.False(ServerDropRules.Accepts(type, EnumDeviceCategory.Speaker));
        Assert.NotNull(ServerDropRules.RefusalReason(type, EnumDeviceCategory.Speaker));
    }

    [Fact]
    public void should_have_no_refusal_reason_when_the_drop_is_allowed()
        => Assert.Null(ServerDropRules.RefusalReason(EnumServerType.SPEAKER_API, EnumDeviceCategory.Speaker));

    [Fact]
    public void should_name_the_device_kind_when_refusing_a_sensor()
        => Assert.Contains("센서", ServerDropRules.RefusalReason(EnumServerType.SPEAKER_API, EnumDeviceCategory.Sensor));

    [Fact]
    public void should_block_when_the_server_row_is_not_saved_yet()
    {
        var plan = ServerDropRules.Plan(0, EnumServerType.SPEAKER_API, new[] { Speaker(1) });

        Assert.False(plan.CanSend);
        Assert.Equal(0, plan.WriteCount);
        Assert.Contains("등록되지 않은", plan.BlockReason);
    }

    [Fact]
    public void should_block_when_nothing_was_dragged()
        => Assert.False(ServerDropRules.Plan(7, EnumServerType.SPEAKER_API, Array.Empty<IBaseDeviceModel>()).CanSend);

    [Fact]
    public void should_block_when_every_dragged_device_is_ineligible()
    {
        var plan = ServerDropRules.Plan(7, EnumServerType.NVR_API, new[] { Speaker(1) });

        Assert.False(plan.CanSend);
        Assert.Equal(1, plan.Ineligible);
        Assert.Equal(0, plan.WriteCount);
    }

    [Fact]
    public void should_exclude_drafts_and_already_assigned_when_planning()
    {
        var devices = new IBaseDeviceModel[]
        {
            Speaker(1),                 // 보낼 것
            Speaker(2, serverId: 7),    // 이미 그 서버
            Speaker(0),                 // 아직 등록 전
            Speaker(3, serverId: 9),    // 다른 서버 → 옮긴다
        };

        var plan = ServerDropRules.Plan(7, EnumServerType.SPEAKER_API, devices);

        Assert.True(plan.CanSend);
        Assert.Equal(new[] { 1, 3 }, plan.DeviceIds);
        Assert.Equal(2, plan.WriteCount);
        Assert.Equal(1, plan.AlreadyOn);
        Assert.Equal(1, plan.DraftExcluded);
    }

    [Fact]
    public void should_block_when_everything_is_already_on_that_server()
    {
        var plan = ServerDropRules.Plan(7, EnumServerType.SPEAKER_API, new[] { Speaker(1, serverId: 7) });

        Assert.False(plan.CanSend);
        Assert.Contains("이미", plan.BlockReason);
    }

    [Fact]
    public void should_require_confirmation_only_when_more_than_one_write_goes_out()
    {
        var one = ServerDropRules.Plan(7, EnumServerType.SPEAKER_API, new[] { Speaker(1) });
        var many = ServerDropRules.Plan(7, EnumServerType.SPEAKER_API, new[] { Speaker(1), Speaker(2) });

        Assert.False(one.NeedsConfirm);
        Assert.True(many.NeedsConfirm);
        Assert.Contains("2회", many.ConfirmText("방송서버"));
    }

    [Fact]
    public void should_state_the_write_count_when_the_result_line_is_built()
    {
        var plan = ServerDropRules.Plan(7, EnumServerType.SPEAKER_API,
            new IBaseDeviceModel[] { Speaker(1), Speaker(2), Speaker(3, serverId: 7), Speaker(0) });

        var line = ServerDropRules.ResultLine("방송서버", plan, assigned: 1, failed: 1);

        Assert.Contains("서버 쓰기 1회", line);
        Assert.Contains("멈췄습니다", line);
        Assert.Contains("이미 붙어 있던 1대", line);
        Assert.Contains("등록 전 1대", line);
    }

    [Fact]
    public void should_read_the_current_server_id_only_from_speakers()
    {
        Assert.Equal(7, ServerDropRules.ServerIdOf(Speaker(1, serverId: 7)));
        Assert.Null(ServerDropRules.ServerIdOf(Speaker(1)));
        Assert.Null(ServerDropRules.ServerIdOf(new CameraDeviceModel { Id = 4 }));
        Assert.Null(ServerDropRules.ServerIdOf(null));
    }

    [Fact]
    public void should_keep_a_stable_zone_key_so_the_view_and_handler_agree()
        => Assert.Equal("server-row", ServerDropRules.ZoneKey);
}

public class ServerMetricBandTests
{
    private static ServerMetricDto Metric(JToken? thresholds = null) => new()
    {
        ServerId = 1,
        CpuUsage = 91.5,
        RamUsage = 40,
        RamUsedGb = 12.8,
        RamTotalGb = 32,
        DiskUsage = 70,
        DiskUsedGb = 700,
        DiskTotalGb = 1000,
        NetworkInMbps = 120.4,
        NetworkOutMbps = 8,
        ObservedAt = "2026-09-20T09:00:00+09:00",
        ThresholdExceeded = thresholds,
    };

    private static JArray SevenOhThresholds() => new(
        JObject.FromObject(new { field = "cpu", value = 91.5, threshold = 90.0, direction = "HIGH", severity = "critical" }));

    [Fact]
    public void should_show_not_reported_in_every_cell_when_no_metric_arrived()
    {
        var cells = ServerMetricBand.Band(null);

        Assert.Equal(new[] { "cpu", "ram", "disk", "network" }, cells.Select(c => c.Key));
        Assert.All(cells, c => Assert.Equal("보고 없음", c.ValueText));
        Assert.All(cells, c => Assert.False(c.HasValue));
        Assert.All(cells, c => Assert.Null(c.Ratio));
        Assert.All(cells, c => Assert.Null(c.BadgeText));
    }

    [Fact]
    public void should_build_percentages_and_sizes_when_a_metric_arrived()
    {
        var cells = ServerMetricBand.Band(Metric());

        var cpu = cells.Single(c => c.Key == "cpu");
        Assert.Equal("91.5%", cpu.ValueText);
        Assert.Equal(0.915, cpu.Ratio!.Value, 3);

        Assert.Equal("12.8 / 32 GB", cells.Single(c => c.Key == "ram").DetailText);
        Assert.Equal("↓120.4 / ↑8", cells.Single(c => c.Key == "network").ValueText);
        Assert.Equal("Mbps (수신 / 송신)", cells.Single(c => c.Key == "network").DetailText);

        // 네트워크는 상한이 없어 비율을 만들지 않는다.
        Assert.Null(cells.Single(c => c.Key == "network").Ratio);
    }

    [Fact]
    public void should_draw_a_badge_only_when_the_server_judged_it()
    {
        var withBadge = ServerMetricBand.Band(Metric(SevenOhThresholds()));
        var withoutBadge = ServerMetricBand.Band(Metric());

        var cpu = withBadge.Single(c => c.Key == "cpu");
        Assert.NotNull(cpu.BadgeText);
        Assert.True(cpu.IsCritical);
        Assert.Contains("90", cpu.BadgeText);

        Assert.All(withoutBadge, c => Assert.Null(c.BadgeText));
        // 우리 임계로 다시 판정하지 않는다 — 91.5% 인데도 배지가 없다.
        Assert.Equal("91.5%", withoutBadge.Single(c => c.Key == "cpu").ValueText);
    }

    [Fact]
    public void should_draw_no_badge_when_the_contract_sends_a_dictionary()
    {
        // 6.3 은 threshold_exceeded 를 dict 로 보낸다 — 배열이 아니면 항목이 하나도 없다.
        var cells = ServerMetricBand.Band(Metric(JObject.FromObject(new { cpu = true })));

        Assert.All(cells, c => Assert.Null(c.BadgeText));
    }

    [Fact]
    public void should_clamp_the_bar_ratio_when_the_server_sends_over_one_hundred()
    {
        var cells = ServerMetricBand.Band(new ServerMetricDto { CpuUsage = 250 });
        Assert.Equal(1d, cells.Single(c => c.Key == "cpu").Ratio!.Value, 3);
    }

    [Fact]
    public void should_never_build_a_badge_when_history_rows_are_made()
    {
        var clock = new FixedClock(new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc));
        var rows = ServerMetricBand.History(new[] { Metric(SevenOhThresholds()) }, clock);

        // 이력 행 타입에는 배지 자리가 아예 없다(SB L1359).
        Assert.Single(rows);
        Assert.DoesNotContain("임계", rows[0].Cpu);
        Assert.DoesNotContain(typeof(ServerMetricHistoryRow).GetProperties(), p => p.Name.Contains("Badge", StringComparison.Ordinal));
    }

    [Fact]
    public void should_put_the_newest_metric_first_when_history_built()
    {
        var clock = new FixedClock(new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc));
        var rows = ServerMetricBand.History(new[]
        {
            new ServerMetricDto { CpuUsage = 1, ObservedAt = "2026-09-19T09:00:00+09:00" },
            new ServerMetricDto { CpuUsage = 2, ObservedAt = "2026-09-20T09:00:00+09:00" },
            new ServerMetricDto { CpuUsage = 3, CollectedAt = "2026-09-18T09:00:00+09:00" },
        }, clock);

        Assert.Equal(new[] { "2%", "1%", "3%" }, rows.Select(r => r.Cpu));
    }

    [Fact]
    public void should_say_not_reported_when_a_history_row_has_no_time()
    {
        var clock = new FixedClock(new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc));
        var rows = ServerMetricBand.History(new[] { new ServerMetricDto() }, clock);

        Assert.Equal("보고 없음", rows[0].TimeText);
        Assert.Equal("보고 없음", rows[0].Cpu);
    }

    [Fact]
    public void should_return_nothing_when_history_input_is_null()
        => Assert.Empty(ServerMetricBand.History(null, new FixedClock(DateTime.UtcNow)));
}
