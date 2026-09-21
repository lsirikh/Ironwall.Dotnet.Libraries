using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
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

/// <summary>테스트용 서버 행 만들기 — 실제 응답과 같은 모양을 쓴다.</summary>
internal static class AxisViews
{
    /// <summary>7.0+ 응답 — <c>status</c> 는 늘 오고, 보고 없음은 <c>status_observed_at = null</c> 이다.</summary>
    public static ServerAxisView Axis(string status = "NORMAL", string? observedAt = "2026-09-20T09:00:00+09:00",
        string type = "PROXY", int id = 1, string name = "srv", string? updatedAt = "2026-09-20T09:00:00+09:00")
        => new()
        {
            Id = id,
            TypeServer = type,
            Name = name,
            IsEnable = true,
            Status = status,
            HasStatusKey = true,
            StatusObservedAt = observedAt,
            HasStatusObservedAtKey = true,
            IpAddress = "10.0.0.1",
            Port = 8000,
            UpdatedAt = updatedAt,
            CreatedAt = "2026-01-01T00:00:00+09:00",
        };

    /// <summary>6.3 응답 — 전이 시각이 없고, 상태 키가 아예 안 올 수 있다.</summary>
    public static ServerAxisView Legacy(string? status, bool hasStatusKey, int id = 1, string type = "PROXY",
        string? updatedAt = "2026-09-20T09:00:00+09:00")
        => new()
        {
            Id = id,
            TypeServer = type,
            Name = "srv",
            Status = status,
            HasStatusKey = hasStatusKey,
            StatusObservedAt = null,
            HasStatusObservedAtKey = false,
            IpAddress = "10.0.0.1",
            Port = 8000,
            UpdatedAt = updatedAt,
            CreatedAt = "2026-01-01T00:00:00+09:00",
        };
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

        Assert.False(ServerTypeCatalog.RailOrder.Single(r => r.Key == ServerTypeCatalog.SystemEventsKey).ShowCount);
        Assert.True(ServerTypeCatalog.RailOrder.Single(r => r.Key == ServerTypeCatalog.SystemEventsKey).HasSeparatorAbove);
    }
}

public class ServerStatusRulesTests
{
    private static readonly FixedClock Clock = new(new DateTime(2026, 9, 20, 0, 5, 0, DateTimeKind.Utc));

    #region - 7.0+ : 전이 시각이 정본 -
    [Theory]
    [InlineData("NORMAL", ServerStatusKind.Normal)]
    [InlineData("WARNING", ServerStatusKind.Warning)]
    [InlineData("ERROR", ServerStatusKind.Error)]
    [InlineData("error", ServerStatusKind.Error)]
    public void should_map_known_vocabulary_when_a_transition_time_exists(string status, ServerStatusKind expected)
        => Assert.Equal(expected, ServerStatusRules.Classify(AxisViews.Axis(status), EnumServerContract.V8_0));

    [Fact]
    public void should_say_not_reported_when_the_axis_contract_has_no_transition_time()
    {
        // 서버 app/schemas/server.py:788 — status_observed_at 이 null 이면 "보고 없음" 이다.
        // 이 행의 status 는 UNKNOWN 이지만, NORMAL 이어도 결론은 같아야 한다.
        Assert.Equal(ServerStatusKind.NotReported,
            ServerStatusRules.Classify(AxisViews.Axis("UNKNOWN", observedAt: null), EnumServerContract.V8_0));
        Assert.Equal(ServerStatusKind.NotReported,
            ServerStatusRules.Classify(AxisViews.Axis("NORMAL", observedAt: null), EnumServerContract.V8_0));
    }

    [Fact]
    public void should_not_read_an_unknown_vocabulary_as_normal()
        => Assert.Equal(ServerStatusKind.NotReported,
            ServerStatusRules.Classify(AxisViews.Axis("DEGRADED_MAYBE"), EnumServerContract.V8_0));
    #endregion

    #region - 6.3 : 키의 존재가 정본 -
    [Fact]
    public void should_say_not_reported_when_the_legacy_payload_has_no_status_key()
    {
        // ServerDto.Status 기본값이 "NORMAL" 이라 종전에는 이것이 정상으로 보였다.
        var view = AxisViews.Legacy(status: null, hasStatusKey: false);
        Assert.Equal(ServerStatusKind.NotReported, ServerStatusRules.Classify(view, EnumServerContract.V6_3));
    }

    [Fact]
    public void should_trust_a_present_status_value_when_contract_is_legacy()
    {
        Assert.Equal(ServerStatusKind.Error,
            ServerStatusRules.Classify(AxisViews.Legacy("ERROR", hasStatusKey: true), EnumServerContract.V6_3));
        Assert.Equal(ServerStatusKind.Normal,
            ServerStatusRules.Classify(AxisViews.Legacy("NORMAL", hasStatusKey: true), EnumServerContract.V6_3));
    }

    [Fact]
    public void should_never_claim_a_transition_when_contract_is_legacy()
    {
        // updated_at 은 이름만 바꿔도 올라간다 — 그것을 "마지막 변화" 로 내보내지 않는다.
        var view = AxisViews.Legacy("NORMAL", hasStatusKey: true, updatedAt: "2026-09-20T09:04:30+09:00");

        Assert.Equal("—", ServerStatusRules.LastChangeText(view, EnumServerContract.V6_3, Clock));
        Assert.Contains("전이 시각이 없습니다", ServerStatusRules.NoTransitionClockNote);
    }

    [Fact]
    public void should_still_show_the_edit_time_under_its_own_name_when_contract_is_legacy()
    {
        var view = AxisViews.Legacy("NORMAL", hasStatusKey: true, updatedAt: "2026-09-20T09:04:30+09:00");
        Assert.Equal("방금", ServerStatusRules.LastEditText(view, Clock));
    }
    #endregion

    #region - 시각 문구 -
    [Fact]
    public void should_say_not_reported_when_the_axis_transition_time_is_missing()
        => Assert.Equal("보고 없음",
            ServerStatusRules.LastChangeText(AxisViews.Axis(observedAt: null), EnumServerContract.V8_0, Clock));

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

        Assert.Equal(expected, ServerStatusRules.Elapsed(at, clock));
    }

    [Fact]
    public void should_fall_back_to_an_absolute_time_when_older_than_a_week()
    {
        var at = new DateTimeOffset(2026, 9, 1, 3, 0, 0, TimeSpan.Zero);
        var clock = new FixedClock(at.UtcDateTime.AddDays(8));

        Assert.Contains("2026-09-01", ServerStatusRules.Elapsed(at, clock));
    }

    [Fact]
    public void should_not_invent_liveness_when_the_time_is_in_the_future()
    {
        var at = new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);
        var clock = new FixedClock(at.UtcDateTime.AddMinutes(-10));

        Assert.Contains("2026-09-20", ServerStatusRules.Elapsed(at, clock));
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
    public void should_count_only_error_as_fault_when_badge_built()
    {
        Assert.True(ServerStatusRules.IsFault(ServerStatusKind.Error));
        Assert.False(ServerStatusRules.IsFault(ServerStatusKind.Warning));
        Assert.False(ServerStatusRules.IsFault(ServerStatusKind.NotReported));
    }

    [Fact]
    public void should_give_every_state_its_own_glyph_so_shape_carries_the_meaning()
    {
        var glyphs = new[]
        {
            ServerStatusRules.StatusGlyph(ServerStatusKind.Error),
            ServerStatusRules.StatusGlyph(ServerStatusKind.Warning),
            ServerStatusRules.StatusGlyph(ServerStatusKind.Normal),
            ServerStatusRules.StatusGlyph(ServerStatusKind.NotReported),
        };

        // 넷이 서로 달라야 한다 — 라이트 테마에서 색은 구분되지 않는다.
        Assert.Equal(4, glyphs.Distinct().Count());
    }

    [Fact]
    public void should_throw_when_clock_is_null()
        => Assert.Throws<ArgumentNullException>(() => ServerStatusRules.LastChangeText(AxisViews.Axis(), EnumServerContract.V8_0, null!));
    #endregion
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
        Assert.Equal("마지막 변화", ServerColumnCatalog.LastChangeHeader);
        Assert.Equal("마지막 변화", ServerColumnCatalog.All.Single(c => c.Key == "last_change").Header);
    }

    [Fact]
    public void should_keep_the_edit_time_under_a_different_name_and_off_by_default()
    {
        var lastEdit = ServerColumnCatalog.All.Single(c => c.Key == "last_edit");
        Assert.Equal("마지막 수정", lastEdit.Header);
        Assert.False(lastEdit.IsDefault);
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
