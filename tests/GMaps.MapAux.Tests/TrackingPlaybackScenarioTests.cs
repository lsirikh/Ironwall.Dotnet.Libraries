using System.IO;
using System.Windows;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapAux.Tests;

/// <summary>시험용 시계 — 추적 TTL · 재생 엔진의 시간을 손으로 민다.</summary>
internal sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc);
    public DateTime Now => UtcNow.ToLocalTime();
}

/// <summary>
/// 카탈로그 §1 MAP-082~084 · MAP-181/182 · §3.5 AUX-012~017 — 추적 실시간 층 · 추적 설정 · 재생 패널.
/// 지도 컨트롤은 부모 없이 만들고(창 없음), NATS 대신 <see cref="TrackingOverlayManager.UpsertBatchAsync"/> 를 바로 부른다.
/// </summary>
public class TrackingPlaybackScenarioTests
{
    private static TrackingTargetDto Target(string id, double lat = 37.5, double lng = 127.0, string? observed = null)
        => new() { TrackId = id, Label = "person", ThreatLevel = "NORMAL", ObservedAt = observed, Location = new TrackingTargetLocationDto { Latitude = lat, Longitude = lng } };

    private static GMapCustomControl Map(double zoom = 17)
        => new() { Width = 640, Height = 480, MinZoom = 1, MaxZoom = 21, Zoom = zoom, Position = new PointLatLng(37.5, 127.0) };

    private static void Sweep(TrackingOverlayManager mgr) => Scenario.Call(mgr, "OnSweepTick", null, EventArgs.Empty);

    private static int VisibleTrackMarkers(GMapCustomControl map)
        => map.Markers.OfType<GMapTrackingMarker>().Count(m => m.Shape is UIElement s && s.Visibility == Visibility.Visible);

    // ───────────────────────── 추적 실시간 층 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-082")]
    public void should_add_tracking_marker_with_trail_and_remove_both_after_ttl_when_tracking_signal_arrives() => OneSta.Sta(() =>
    {
        var clock = new FakeClock();
        var setup = new TrackingSetupModel { DefaultTtlSec = 5 };
        var map = Map();
        var mgr = new TrackingOverlayManager(clock, setup);
        mgr.Attach(map);

        mgr.UpsertBatchAsync(7, new[] { Target("t1") }, messageTtlSec: null).GetAwaiter().GetResult();
        Assert.Single(map.Markers.OfType<GMapTrackingMarker>());
        Assert.Single(map.Markers.OfType<GMapTrailMarker>());   // 꼬리

        // 두 번째 관측 — 같은 키(camera 7, t1)는 새 마커를 만들지 않고 위치만 옮긴다
        clock.UtcNow = clock.UtcNow.AddSeconds(1);
        mgr.UpsertBatchAsync(7, new[] { Target("t1", 37.5001, 127.0001, clock.UtcNow.ToString("o")) }, null).GetAwaiter().GetResult();
        Assert.Single(map.Markers.OfType<GMapTrackingMarker>());
        Assert.Equal(37.5001, map.Markers.OfType<GMapTrackingMarker>().Single().Position.Lat, 6);

        // TTL(5 s) 전에는 남아 있고, 지나면 마커 · 꼬리 모두 사라진다
        clock.UtcNow = clock.UtcNow.AddSeconds(4);
        Sweep(mgr);
        Assert.Single(map.Markers.OfType<GMapTrackingMarker>());
        clock.UtcNow = clock.UtcNow.AddSeconds(1.5);
        Sweep(mgr);
        Assert.Empty(map.Markers.OfType<GMapTrackingMarker>());
        Assert.Empty(map.Markers.OfType<GMapTrailMarker>());
    });

    [Fact]
    [Trait("Scenario", "MAP-083")]
    public void should_not_draw_tracking_marker_when_tracking_display_is_turned_off() => OneSta.Sta(() =>
    {
        var setup = new TrackingSetupModel();
        var vm = new TrackingSetupViewModel(setup) { IsTrackingEnabled = false };   // 패널 「추적 표시」 끔 = 싱글톤 즉시 반영
        var map = Map();
        var mgr = new TrackingOverlayManager(new FakeClock(), setup);
        mgr.Attach(map);

        mgr.UpsertBatchAsync(7, new[] { Target("t1") }, null).GetAwaiter().GetResult();
        Assert.Empty(map.Markers.OfType<GMapTrackingMarker>());

        vm.IsTrackingEnabled = true;   // 다시 켜면 다음 신호부터 그린다
        mgr.UpsertBatchAsync(7, new[] { Target("t1") }, null).GetAwaiter().GetResult();
        Assert.Single(map.Markers.OfType<GMapTrackingMarker>());
    });

    /// <summary>
    /// 결함 AUX-D1: 「최소 줌」(MinVisibleZoom)을 지금 줌보다 크게 해도 추적 마커가 그대로 보인다.
    /// 설정값은 모델 · 패널 · 저장에만 있고 TrackingOverlayManager 어디에서도 읽지 않는다(UpsertBatchAsync :78 은 IsTrackingEnabled 만 본다).
    /// </summary>
    [Fact]
    [Trait("Scenario", "MAP-083")]
    [Trait("Defect", "AUX-D1")]
    public void should_hide_tracking_markers_when_min_visible_zoom_is_above_current_zoom() => OneSta.Sta(() =>
    {
        // 대조: 지금 줌(17) ≥ 최소 줌(0) 이면 보인다
        var shown = Map(17);
        var mgrShown = new TrackingOverlayManager(new FakeClock(), new TrackingSetupModel { MinVisibleZoom = 0 });
        mgrShown.Attach(shown);
        mgrShown.UpsertBatchAsync(7, new[] { Target("t1") }, null).GetAwaiter().GetResult();
        Assert.Equal(1, VisibleTrackMarkers(shown));

        // 지금 줌(12) < 최소 줌(16) 이면 숨어야 한다
        var map = Map(12);
        var mgr = new TrackingOverlayManager(new FakeClock(), new TrackingSetupModel { MinVisibleZoom = 16 });
        mgr.Attach(map);
        mgr.UpsertBatchAsync(7, new[] { Target("t1") }, null).GetAwaiter().GetResult();
        Assert.True(VisibleTrackMarkers(map) == 0,
            $"최소 줌 16 > 지금 줌 12 인데 추적 마커 {VisibleTrackMarkers(map)}개가 보인다(MinVisibleZoom 미적용)");
    });

    // ───────────────────────── 추적 설정 패널 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-084,AUX-014")]
    public void should_restore_defaults_then_persist_and_reload_when_reset_and_save_pressed() => OneSta.StaAsync(async () =>
    {
        var file = Ironwall.Dotnet.Libraries.Base.Services.AppSettingsLocation.PrepareAppSettingsWrite();   // 시험 bin 의 사본(앱 설정 아님)
        string? backup = File.Exists(file) ? File.ReadAllText(file) : null;
        try
        {
            if (backup == null) File.WriteAllText(file, "{ \"AppSettings\": {} }");
            var model = new TrackingSetupModel { TrailMaxPoints = 77, DefaultTtlSec = 42, MinVisibleZoom = 9.5, IsTrackingEnabled = false };
            var vm = new TrackingSetupViewModel(model);

            vm.ResetCommand.Execute(null);
            var d = new TrackingSetupModel();
            Assert.Equal("기본값 복원됨 — [저장] 필요", vm.Status);
            Assert.Equal(d.TrailMaxPoints, vm.TrailMaxPoints);
            Assert.Equal(d.DefaultTtlSec, vm.DefaultTtlSec);
            Assert.Equal(d.MinVisibleZoom, vm.MinVisibleZoom);
            Assert.Equal(d.IsTrackingEnabled, vm.IsTrackingEnabled);

            vm.TrailMaxPoints = 55;   // 바꾸고 저장 → 다시 열기(로드)에서 유지
            vm.SaveCommand.Execute(null);
            for (int i = 0; i < 100 && vm.Status != "저장됨"; i++) await Task.Delay(20);
            Assert.Equal("저장됨", vm.Status);
            var reloaded = await MapSettingsHelper.LoadTrackingSettingsAsync();
            Assert.Equal(55, reloaded.TrailMaxPoints);
            Assert.Equal(d.DefaultTtlSec, reloaded.DefaultTtlSec);
        }
        finally
        {
            if (backup != null) File.WriteAllText(file, backup); else File.Delete(file);
        }
    });

    [Fact]
    [Trait("Scenario", "AUX-013")]
    public void should_expose_check_two_sliders_five_number_boxes_and_source_combo_without_automation_ids_in_tracking_settings()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoPaths.GMapsUi, "Themes", "TrackingSettingsStyle.xaml"));
        var body = xaml[xaml.IndexOf("<!-- 바디", StringComparison.Ordinal)..xaml.IndexOf("<!-- footer", StringComparison.Ordinal)];
        Assert.Equal(1, Count(body, "<CheckBox "));
        Assert.Equal(2, Count(body, "<Slider "));
        Assert.Equal(5, Count(body, "Style=\"{StaticResource SetNum}\""));
        Assert.Equal(1, Count(body, "<ComboBox "));
        Assert.Contains("SelectedItem=\"{Binding DataSource, Mode=TwoWay}\"", body);
        Assert.DoesNotContain("AutomationProperties.AutomationId", body);   // 카탈로그 기록: id 없음 → 헤디드는 라벨로 찾는다

        static int Count(string s, string needle) { int n = 0, i = 0; while ((i = s.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; } return n; }
    }

    /// <summary>AUX-015 판정: TTL −5 · 0 은 패널이 1 초로 잘라 받고(2026-10-13 숫자 칸 명세), 소비처(TrackingOverlayManager.cs:80 MinTtlGuard=3)가 3 초로 올려 쓴다.</summary>
    [Theory]
    [InlineData(-5)]
    [InlineData(0)]
    [Trait("Scenario", "AUX-015")]
    public void should_apply_minimum_three_second_ttl_when_default_ttl_is_zero_or_negative(int ttl) => OneSta.Sta(() =>
    {
        var setup = new TrackingSetupModel();
        var vm = new TrackingSetupViewModel(setup) { DefaultTtlSec = ttl };
        Assert.Equal(1, setup.DefaultTtlSec);     // 즉시 적용 — 0 · 음수는 칸 명세(MapNumericFields, 최소 1 초)로 잘린다(map-property-numeric-precision)

        var clock = new FakeClock();
        var map = Map();
        var mgr = new TrackingOverlayManager(clock, setup);
        mgr.Attach(map);
        mgr.UpsertBatchAsync(7, new[] { Target("t1") }, null).GetAwaiter().GetResult();
        Assert.Single(map.Markers.OfType<GMapTrackingMarker>());   // 0 · 음수여도 그리자마자 지우지 않는다

        clock.UtcNow = clock.UtcNow.AddSeconds(2.9); Sweep(mgr);
        Assert.Single(map.Markers.OfType<GMapTrackingMarker>());
        clock.UtcNow = clock.UtcNow.AddSeconds(0.2); Sweep(mgr);
        Assert.Empty(map.Markers.OfType<GMapTrackingMarker>());
    });

    // ───────────────────────── 재생 패널 ─────────────────────────

    private sealed class Reader : ITrackPointReader
    {
        public readonly List<(DateTime from, DateTime to)> Calls = new();
        public List<ITrackPointModel> Points = new();
        public Task<List<ITrackPointModel>> FetchAsync(int? cameraId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
        { Calls.Add((fromUtc, toUtc)); return Task.FromResult(Points); }
        public string? Notice;
        public string? Unavailable;
        public int AvailabilityChecks;
        public string? LastFetchNotice => Notice;
        public Task<string?> CheckAvailabilityAsync(CancellationToken ct = default) { AvailabilityChecks++; return Task.FromResult(Unavailable); }
    }

    [Theory]
    [InlineData("추적 이력을 볼 권한이 없습니다(지도 보기 권한 필요)")]
    [InlineData("일부만 불러왔습니다")]
    [Trait("Scenario", "G12")]
    public void should_show_the_fetch_notice_instead_of_no_data_when_the_server_refused_or_failed(string notice) => OneSta.StaAsync(async () =>
    {
        var (vm, reader) = Playback();
        reader.Notice = notice;
        vm.PresetCommand.Execute("30");
        await Until(() => reader.Calls.Count > 0 && vm.Status.Length > 0);
        Assert.Equal(notice, vm.Status);   // 예전엔 "해당 기간 데이터 없음"
    });

    [Fact]
    [Trait("Scenario", "G12")]
    public void should_keep_loaded_points_and_append_partial_notice() => OneSta.StaAsync(async () =>
    {
        var (vm, reader) = Playback(Pt("a", DateTime.UtcNow.AddMinutes(-10)), Pt("a", DateTime.UtcNow.AddMinutes(-9)));
        reader.Notice = "일부만 불러왔습니다";
        vm.PresetCommand.Execute("30");
        await Until(() => vm.HasData);
        Assert.EndsWith("— 일부만 불러왔습니다", vm.Status);
        Assert.StartsWith("2점", vm.Status);
    });

    [Fact]
    [Trait("Scenario", "G13")]
    public void should_not_query_and_say_so_when_tracking_store_is_unavailable_then_check_only_once_after_it_recovers() => OneSta.StaAsync(async () =>
    {
        var (vm, reader) = Playback();
        reader.Unavailable = "추적 이력 저장소를 쓸 수 없습니다";
        vm.PresetCommand.Execute("30");
        await Until(() => vm.Status.Length > 0);
        Assert.Equal("추적 이력 저장소를 쓸 수 없습니다", vm.Status);
        Assert.Empty(reader.Calls);

        reader.Unavailable = null;
        vm.PresetCommand.Execute("30");
        await Until(() => reader.Calls.Count == 1);
        vm.PresetCommand.Execute("30");
        await Until(() => reader.Calls.Count == 2);
        Assert.Equal(2, reader.AvailabilityChecks);   // 막혔던 1회 + 회복 뒤 1회, 그다음은 다시 묻지 않는다
    });

    private static (PlaybackViewModel vm, Reader reader) Playback(params ITrackPointModel[] points)
    {
        var engine = new PlaybackEngine(new FakeClock());
        var reader = new Reader { Points = points.ToList() };
        return (new PlaybackViewModel(engine, reader, new PlaybackOverlayManager(engine), new TrackingSetupModel()), reader);
    }

    private static TrackPointModel Pt(string track, DateTime utc, double lat = 37.5, double lng = 127.0)
        => new() { TrackId = track, CameraId = 7, ObservedAt = utc, Latitude = lat, Longitude = lng, Label = "person", ThreatLevel = "NORMAL" };

    private static async Task Until(Func<bool> done) { for (int i = 0; i < 100 && !done(); i++) await Task.Delay(10); }

    [Theory]
    [InlineData("5", 5)]
    [InlineData("30", 30)]
    [InlineData("60", 60)]
    [Trait("Scenario", "AUX-016")]
    public void should_query_recent_window_when_preset_button_pressed(string preset, int minutes) => OneSta.StaAsync(async () =>
    {
        var (vm, reader) = Playback();
        vm.PresetCommand.Execute(preset);
        await Until(() => reader.Calls.Count > 0);
        var (from, to) = Assert.Single(reader.Calls);
        Assert.Equal(minutes, (to - from).TotalMinutes, 3);
        Assert.Equal("해당 기간 데이터 없음", vm.Status);
    });

    [Fact]
    [Trait("Scenario", "AUX-016")]
    public void should_query_from_midnight_when_today_preset_pressed() => OneSta.StaAsync(async () =>
    {
        var (vm, reader) = Playback();
        vm.PresetCommand.Execute("today");
        await Until(() => reader.Calls.Count > 0);
        Assert.Equal(DateTime.Today, vm.FromTime);
        Assert.Equal(DateTime.Today.ToUniversalTime(), reader.Calls.Single().from);
    });

    /// <summary>31일 초과 = 「거부」의 실제 구현은 시작 기준 31일로 잘라 불러오기(PlaybackViewModel.cs:183-188 — 선택 범위 표시는 그대로).</summary>
    [Fact]
    [Trait("Scenario", "AUX-016")]
    public void should_limit_query_to_31_days_from_start_and_tell_user_when_range_exceeds_31_days() => OneSta.StaAsync(async () =>
    {
        var (vm, reader) = Playback();
        vm.FromTime = new DateTime(2026, 8, 1); vm.ToTime = new DateTime(2026, 10, 1);
        vm.LoadCommand.Execute(null);
        await Until(() => reader.Calls.Count > 0);
        var (from, to) = reader.Calls.Single();
        Assert.Equal(31, (to - from).TotalDays, 3);
        Assert.Equal(new DateTime(2026, 8, 1), vm.FromTime);   // 사용자가 고른 시작은 바꾸지 않는다
        Assert.Equal(new DateTime(2026, 10, 1), vm.ToTime);

        // 끝이 시작보다 빠르면 조회하지 않고 거부
        vm.FromTime = new DateTime(2026, 10, 2); vm.ToTime = new DateTime(2026, 10, 1);
        vm.LoadCommand.Execute(null);
        await Task.Delay(30);
        Assert.Single(reader.Calls);
        Assert.Equal("종료 시각이 시작보다 빨라야 합니다.", vm.Status);
    });

    [Fact]
    [Trait("Scenario", "MAP-181")]
    public void should_list_sessions_play_pause_change_speed_and_close_when_playback_flow_runs() => OneSta.StaAsync(async () =>
    {
        var t0 = DateTime.UtcNow.AddMinutes(-20);
        var (vm, reader) = Playback(
            Pt("a", t0), Pt("a", t0.AddSeconds(5)),
            Pt("a", t0.AddSeconds(100)),                 // 30 s 넘는 공백 → 같은 track 의 둘째 세션
            Pt("b", t0.AddSeconds(10), 37.6, 127.1));
        vm.PresetCommand.Execute("30");
        await Until(() => vm.HasData);

        Assert.True(vm.HasData);
        Assert.Equal(3, vm.Events.Count);
        Assert.Equal(new[] { "a#1", "b", "a#2" }, vm.Events.Select(e => e.SessionId));   // 시작 시각 순
        Assert.Equal("4점 · 3건 로드", vm.Status);

        vm.PlayPauseCommand.Execute(null);
        Assert.True(vm.IsPlaying); Assert.Equal("⏸", vm.PlayPauseGlyph);
        vm.SpeedCommand.Execute("4");
        Assert.Equal(4, vm.Speed);
        vm.SpeedCommand.Execute("0.5");
        Assert.Equal(0.5, vm.Speed);
        vm.PlayPauseCommand.Execute(null);
        Assert.False(vm.IsPlaying); Assert.Equal("▶", vm.PlayPauseGlyph);

        // 한 세션 행을 끄면 같은 track 의 다른 세션 행도 함께 꺼진다
        vm.Events.First(e => e.SessionId == "a#1").IsEnabled = false;
        Assert.False(vm.Events.First(e => e.SessionId == "a#2").IsEnabled);
        Assert.True(vm.Events.First(e => e.SessionId == "b").IsEnabled);

        bool closed = false; vm.CloseRequested += () => closed = true;
        vm.PlayPauseCommand.Execute(null);
        vm.CloseCommand.Execute(null);
        Assert.True(closed); Assert.False(vm.IsPlaying);
    });

    [Fact]
    [Trait("Scenario", "MAP-182")]
    public void should_request_map_follow_to_session_coordinate_when_session_focus_pressed() => OneSta.StaAsync(async () =>
    {
        var t0 = DateTime.UtcNow.AddMinutes(-10);
        var (vm, _) = Playback(Pt("a", t0, 37.51, 127.01), Pt("a", t0.AddSeconds(3), 37.52, 127.02));
        vm.PresetCommand.Execute("30");
        await Until(() => vm.HasData);

        var follows = new List<(double lat, double lng)>();
        vm.FocusRequested += (lat, lng) => follows.Add((lat, lng));
        vm.FocusCommand.Execute(vm.Events.Single());       // ⌖
        Assert.Equal((37.52, 127.02), follows.Single());   // 세션 마지막 관측 좌표
        vm.JumpToSessionCommand.Execute(vm.Events.Single());   // 더블클릭 = 시크 + 따라가기 + 재생
        Assert.Equal(2, follows.Count);
        Assert.True(vm.IsPlaying);
        vm.Close();
    });

    [Fact]
    [Trait("Scenario", "MAP-181")]
    public void should_set_whole_days_and_warn_over_31_days_when_calendar_range_completed() => OneSta.Sta(() =>
    {
        var (vm, _) = Playback();
        vm.ToggleCalendarCommand.Execute(null);
        Assert.True(vm.IsCalendarOpen);
        Scenario.Call(vm, "OnCalendarRange", new DateTime(2026, 9, 1), new DateTime(2026, 9, 3));
        Assert.False(vm.IsCalendarOpen);
        Assert.Equal(new DateTime(2026, 9, 1, 0, 0, 0), vm.FromTime);
        Assert.Equal(new DateTime(2026, 9, 3, 23, 59, 59), vm.ToTime);
        Assert.Equal("2026-09-01 ~ 2026-09-03 선택됨", vm.Status);

        Scenario.Call(vm, "OnCalendarRange", new DateTime(2026, 7, 1), new DateTime(2026, 9, 3));
        Assert.Contains("31일까지만", vm.Status);
    });
}

/// <summary>워크트리 안 제품 소스 경로(XAML 정적 판독용).</summary>
internal static class RepoPaths
{
    internal static string Root
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Ironwall.Dotnet.Libraries.GMaps.Ui"))) dir = dir.Parent;
            return dir?.FullName ?? throw new DirectoryNotFoundException("Ironwall.Dotnet.Libraries.GMaps.Ui");
        }
    }
    internal static string GMapsUi => Path.Combine(Root, "Ironwall.Dotnet.Libraries.GMaps.Ui");
}
