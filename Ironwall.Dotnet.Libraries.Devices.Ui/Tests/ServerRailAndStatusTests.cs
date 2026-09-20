using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
/****************************************************************************
   Purpose      : 서버 모니터 레일 · 상태 판정 검증 (N-12)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>테스트용 고정 시계 — <c>DateTime.Now</c> 를 판정에 쓰지 않는다는 계약을 여기서 잠근다.</summary>
internal sealed class FixedClock : IClock
{
    public FixedClock(DateTime utcNow) { UtcNow = utcNow; Now = utcNow.ToLocalTime(); }
    public DateTime Now { get; }
    public DateTime UtcNow { get; }
}

internal sealed class RailItem : IServerRailItem
{
    public RailItem(string railKey, ServerStatusKind status) { RailKey = railKey; Status = status; }
    public string RailKey { get; }
    public ServerStatusKind Status { get; }
}

public class ServerRailCounterTests
{
    private static readonly string[] ExpectedOrder =
    {
        ServerTypeCatalog.AllKey, ServerTypeCatalog.ProxyKey, ServerTypeCatalog.NvrKey,
        ServerTypeCatalog.SpeakerKey, ServerTypeCatalog.EnclosureKey, ServerTypeCatalog.EtcKey,
        ServerTypeCatalog.SystemEventsKey,
    };

    [Fact]
    public void should_keep_every_rail_slot_at_zero_when_no_servers_given()
    {
        var counts = ServerRailCounter.Count(Array.Empty<IServerRailItem>());

        Assert.Equal(ExpectedOrder, counts.Select(c => c.Key));
        Assert.All(counts, c => Assert.Equal(0, c.Total));
        Assert.All(counts, c => Assert.Equal(0, c.Fault));
        Assert.All(counts, c => Assert.Equal(0, c.NotReported));
    }

    [Fact]
    public void should_count_fault_and_not_reported_separately_when_both_present()
    {
        var counts = ServerRailCounter.Count(new IServerRailItem[]
        {
            new RailItem(ServerTypeCatalog.SpeakerKey, ServerStatusKind.Normal),
            new RailItem(ServerTypeCatalog.SpeakerKey, ServerStatusKind.Error),
            new RailItem(ServerTypeCatalog.SpeakerKey, ServerStatusKind.NotReported),
            new RailItem(ServerTypeCatalog.ProxyKey, ServerStatusKind.Warning),
        });

        var speaker = counts.Single(c => c.Key == ServerTypeCatalog.SpeakerKey);
        Assert.Equal(3, speaker.Total);
        Assert.Equal(1, speaker.Fault);
        Assert.Equal(1, speaker.NotReported);

        var all = counts.Single(c => c.Key == ServerTypeCatalog.AllKey);
        Assert.Equal(4, all.Total);
        Assert.Equal(1, all.Fault);
        Assert.Equal(1, all.NotReported);

        // 경고는 장애가 아니다 — ▲ 배지에 세지 않는다.
        Assert.Equal(0, counts.Single(c => c.Key == ServerTypeCatalog.ProxyKey).Fault);
    }

    [Fact]
    public void should_fall_back_to_etc_when_rail_key_is_unknown()
    {
        var counts = ServerRailCounter.Count(new IServerRailItem[] { new RailItem("no-such-rail", ServerStatusKind.Normal) });

        Assert.Equal(1, counts.Single(c => c.Key == ServerTypeCatalog.EtcKey).Total);
        Assert.Equal(1, counts.Single(c => c.Key == ServerTypeCatalog.AllKey).Total);
    }

    [Fact]
    public void should_never_count_servers_into_the_system_events_slot()
    {
        var counts = ServerRailCounter.Count(new IServerRailItem[]
        {
            new RailItem(ServerTypeCatalog.SystemEventsKey, ServerStatusKind.Normal),
        });

        Assert.Equal(0, counts.Single(c => c.Key == ServerTypeCatalog.SystemEventsKey).Total);
        Assert.Equal(1, counts.Single(c => c.Key == ServerTypeCatalog.EtcKey).Total);
    }

    [Fact]
    public void should_report_totals_from_the_all_slot_when_footer_text_built()
    {
        var counts = ServerRailCounter.Count(new IServerRailItem[]
        {
            new RailItem(ServerTypeCatalog.NvrKey, ServerStatusKind.Error),
            new RailItem(ServerTypeCatalog.NvrKey, ServerStatusKind.NotReported),
        });

        Assert.Equal("전체 2대 / 장애 1대 / 보고 없음 1대", ServerRailCounter.FooterText(counts));
    }

    [Fact]
    public void should_throw_when_servers_is_null() => Assert.Throws<ArgumentNullException>(() => ServerRailCounter.Count(null!));
}

public class ServerTypeCatalogTests
{
    [Theory]
    [InlineData(EnumServerType.PROXY, ServerTypeCatalog.ProxyKey)]
    [InlineData(EnumServerType.NVR_API, ServerTypeCatalog.NvrKey)]
    [InlineData(EnumServerType.SPEAKER_API, ServerTypeCatalog.SpeakerKey)]
    [InlineData(EnumServerType.ENCLOSURE_API, ServerTypeCatalog.EnclosureKey)]
    [InlineData(EnumServerType.VMS, ServerTypeCatalog.EtcKey)]
    [InlineData(EnumServerType.BACKUP, ServerTypeCatalog.EtcKey)]
    public void should_map_type_to_rail_slot_when_type_given(EnumServerType type, string expected)
        => Assert.Equal(expected, ServerTypeCatalog.RailKeyOf(type));

    [Fact]
    public void should_map_unknown_type_to_etc_when_type_is_null()
        => Assert.Equal(ServerTypeCatalog.EtcKey, ServerTypeCatalog.RailKeyOf(null));

    [Fact]
    public void should_return_null_instead_of_throwing_when_discriminator_is_unknown()
    {
        Assert.Null(ServerTypeCatalog.ParseType("SOMETHING_NEW"));
        Assert.Null(ServerTypeCatalog.ParseType(null));
        Assert.Null(ServerTypeCatalog.ParseType("   "));
        Assert.Equal(EnumServerType.SPEAKER_API, ServerTypeCatalog.ParseType(" speaker_api "));
    }

    [Fact]
    public void should_show_raw_wire_value_when_type_is_unknown()
    {
        Assert.Equal("SOMETHING_NEW", ServerTypeCatalog.TypeLabel(null, "SOMETHING_NEW"));
        Assert.Equal("—", ServerTypeCatalog.TypeLabel(null, null));
        Assert.Equal("스피커", ServerTypeCatalog.TypeLabel(EnumServerType.SPEAKER_API));
    }

    [Fact]
    public void should_match_every_type_when_rail_is_all()
    {
        Assert.True(ServerTypeCatalog.Matches(ServerTypeCatalog.AllKey, EnumServerType.BACKUP));
        Assert.True(ServerTypeCatalog.Matches(ServerTypeCatalog.EtcKey, EnumServerType.BACKUP));
        Assert.False(ServerTypeCatalog.Matches(ServerTypeCatalog.NvrKey, EnumServerType.BACKUP));
    }

    [Fact]
    public void should_declare_system_events_slot_as_not_a_server_list()
    {
        Assert.False(ServerTypeCatalog.IsServerList(ServerTypeCatalog.SystemEventsKey));
        Assert.True(ServerTypeCatalog.IsServerList(ServerTypeCatalog.AllKey));
    }

    [Fact]
    public void should_keep_the_storyboard_rail_order_when_rail_built()
    {
        Assert.Equal(
            new[] { "all", "proxy", "nvr", "speaker", "enclosure", "etc", "system-events" },
            ServerTypeCatalog.RailOrder.Select(r => r.Key));

        // "시스템 이벤트" 는 서버 목록이 아니라 개수를 내지 않는다.
        Assert.False(ServerTypeCatalog.RailOrder.Single(r => r.Key == ServerTypeCatalog.SystemEventsKey).ShowCount);
        Assert.True(ServerTypeCatalog.RailOrder.Single(r => r.Key == ServerTypeCatalog.SystemEventsKey).HasSeparatorAbove);
    }
}

public class ServerStatusRulesTests
{
    private static readonly DateTimeOffset Reported = new(2026, 9, 20, 9, 0, 0, TimeSpan.FromHours(9));

    [Theory]
    [InlineData("NORMAL", ServerStatusKind.Normal)]
    [InlineData("WARNING", ServerStatusKind.Warning)]
    [InlineData("ERROR", ServerStatusKind.Error)]
    [InlineData("error", ServerStatusKind.Error)]
    public void should_map_known_vocabulary_when_a_transition_time_exists(string status, ServerStatusKind expected)
        => Assert.Equal(expected, ServerStatusRules.Classify(status, Reported));

    [Fact]
    public void should_report_not_reported_when_status_is_unknown_or_empty()
    {
        Assert.Equal(ServerStatusKind.NotReported, ServerStatusRules.Classify("UNKNOWN", Reported));
        Assert.Equal(ServerStatusKind.NotReported, ServerStatusRules.Classify(string.Empty, Reported));
        Assert.Equal(ServerStatusKind.NotReported, ServerStatusRules.Classify(null, Reported));
    }

    [Fact]
    public void should_report_not_reported_when_no_transition_time_exists()
        => Assert.Equal(ServerStatusKind.NotReported, ServerStatusRules.Classify("NORMAL", null));

    [Fact]
    public void should_not_read_an_unknown_vocabulary_as_normal()
        => Assert.Equal(ServerStatusKind.NotReported, ServerStatusRules.Classify("DEGRADED_MAYBE", Reported));

    [Fact]
    public void should_count_only_error_as_fault_when_badge_built()
    {
        Assert.True(ServerStatusRules.IsFault(ServerStatusKind.Error));
        Assert.False(ServerStatusRules.IsFault(ServerStatusKind.Warning));
        Assert.False(ServerStatusRules.IsFault(ServerStatusKind.NotReported));
    }

    [Fact]
    public void should_say_not_reported_when_last_change_is_missing()
        => Assert.Equal("보고 없음", ServerStatusRules.LastChangeText(null, new FixedClock(DateTime.UtcNow)));

    [Theory]
    [InlineData(0, "방금")]
    [InlineData(59, "방금")]
    [InlineData(60, "1분 전")]
    [InlineData(3599, "59분 전")]
    [InlineData(3600, "1시간 전")]
    [InlineData(86399, "23시간 전")]
    [InlineData(86400, "1일 전")]
    public void should_describe_elapsed_time_at_each_boundary_when_last_change_given(int elapsedSeconds, string expected)
    {
        var at = new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);
        var clock = new FixedClock(at.UtcDateTime.AddSeconds(elapsedSeconds));

        Assert.Equal(expected, ServerStatusRules.LastChangeText(at, clock));
    }

    [Fact]
    public void should_fall_back_to_an_absolute_time_when_older_than_a_week()
    {
        var at = new DateTimeOffset(2026, 9, 1, 3, 0, 0, TimeSpan.Zero);
        var clock = new FixedClock(at.UtcDateTime.AddDays(8));

        var text = ServerStatusRules.LastChangeText(at, clock);
        Assert.Contains("2026-09-01", text);
    }

    [Fact]
    public void should_not_invent_liveness_when_the_time_is_in_the_future()
    {
        var at = new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);
        var clock = new FixedClock(at.UtcDateTime.AddMinutes(-10));

        // 시계가 뒤집혔어도 "죽었다" 를 만들어 내지 않는다 — 그대로 시각을 보인다.
        Assert.Contains("2026-09-20", ServerStatusRules.LastChangeText(at, clock));
    }

    [Fact]
    public void should_read_korea_time_when_the_wire_value_has_no_offset()
    {
        var withOffset = ServerStatusRules.ParseTime("2026-09-20T09:00:00+09:00");
        var naive = ServerStatusRules.ParseTime("2026-09-20T09:00:00");

        Assert.Equal(withOffset, naive);
        Assert.Null(ServerStatusRules.ParseTime("not a time"));
        Assert.Null(ServerStatusRules.ParseTime(null));
    }

    [Fact]
    public void should_prefer_updated_at_when_both_times_exist()
    {
        var at = ServerStatusRules.LastChangeOf("2026-09-20T09:00:00+09:00", "2026-01-01T00:00:00+09:00");
        Assert.Equal(new DateTimeOffset(2026, 9, 20, 9, 0, 0, TimeSpan.FromHours(9)), at);

        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(9)),
            ServerStatusRules.LastChangeOf(null, "2026-01-01T00:00:00+09:00"));
        Assert.Null(ServerStatusRules.LastChangeOf(null, null));
    }

    [Fact]
    public void should_throw_when_clock_is_null()
        => Assert.Throws<ArgumentNullException>(() => ServerStatusRules.LastChangeText(Reported, null!));
}

public class ServerColumnCatalogTests
{
    [Fact]
    public void should_hide_the_unit_column_when_contract_has_no_unit_axis()
    {
        var legacy = ServerColumnCatalog.For(isUnitEra: false);
        var modern = ServerColumnCatalog.For(isUnitEra: true);

        Assert.DoesNotContain(legacy, c => c.Key == "unit");
        Assert.Contains(modern, c => c.Key == "unit");
    }

    [Fact]
    public void should_keep_the_storyboard_column_order_when_unit_era()
    {
        var keys = ServerColumnCatalog.For(isUnitEra: true).Where(c => c.IsDefault).Select(c => c.Key);
        Assert.Equal(new[] { "name", "type", "address", "status", "last_change", "unit" }, keys);
    }

    [Fact]
    public void should_label_the_status_time_column_as_last_change()
    {
        // 머리글 자체가 계약이다 — "최종 확인" 으로 바꾸면 REST 로 생존을 판정한다는 거짓말이 된다.
        Assert.Equal("마지막 변화", ServerColumnCatalog.LastChangeHeader);
        Assert.Equal("마지막 변화", ServerColumnCatalog.All.Single(c => c.Key == "last_change").Header);
    }

    [Fact]
    public void should_have_exactly_one_star_column_when_built()
        => Assert.Single(ServerColumnCatalog.For(isUnitEra: true), c => c.Width == 0);

    [Fact]
    public void should_bind_every_column_to_a_real_row_property()
    {
        var properties = typeof(ServerRowViewModel).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.All(ServerColumnCatalog.All, spec => Assert.Contains(spec.BindingPath, properties));
    }
}
