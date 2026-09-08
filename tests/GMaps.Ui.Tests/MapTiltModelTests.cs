using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// map-tilt-25d PRD FR-07 — MapTiltModel 기본값(SIM-P003)·Normalize 클램프+경고(SIM-P002)·MinZoom 0.5 스냅.
/// 맵 줌 범위는 현장 기본 MinZoom=5·MaxZoom=19(실효 상한 20.5) 로 고정한다.
/// </summary>
public class MapTiltModelTests
{
    private const int MIN = 5;
    private const int MAX = 19;

    // ── 기본값 (SIM-P003: 구 appsettings 에 MapTilt 키 없음 → kill-switch OFF·20°·18.0·1·35) ──

    [Fact]
    public void should_use_prd_defaults_when_constructed()
    {
        // SIM-P003
        var m = new MapTiltModel();

        Assert.False(m.IsEnabled);
        Assert.Equal(20.0, m.AngleDeg);
        Assert.Equal(18.0, m.MinZoom);
        Assert.Equal(1, m.HysteresisSteps);
        Assert.Equal(35.0, m.MaxAngleDeg);
    }

    [Fact]
    public void should_return_no_warnings_when_defaults_normalized()
    {
        // SIM-P003 — 기본값은 정규화 후에도 변하지 않고 경고도 없다
        var m = new MapTiltModel();

        var warnings = m.Normalize(MIN, MAX);

        Assert.Empty(warnings);
        Assert.Equal(20.0, m.AngleDeg);
        Assert.Equal(18.0, m.MinZoom);
        Assert.Equal(1, m.HysteresisSteps);
        Assert.Equal(35.0, m.MaxAngleDeg);
    }

    // ── 각도 클램프 (SIM-P002: AngleDeg=50 파일 → 35 + 경고) ──

    [Fact]
    public void should_clamp_angle_to_max_angle_and_warn_when_angle_exceeds_max()
    {
        // SIM-P002
        var m = new MapTiltModel { AngleDeg = 50 };

        var warnings = m.Normalize(MIN, MAX);

        Assert.Equal(35.0, m.AngleDeg);
        var w = Assert.Single(warnings);
        Assert.Contains("[MapTilt]", w);
        Assert.Contains("AngleDeg", w);
        Assert.Contains("50", w);
        Assert.Contains("35", w);
    }

    [Fact]
    public void should_clamp_angle_to_zero_when_negative()
    {
        // SIM-P002 (하한)
        var m = new MapTiltModel { AngleDeg = -5 };

        var warnings = m.Normalize(MIN, MAX);

        Assert.Equal(0.0, m.AngleDeg);
        Assert.Single(warnings);
    }

    [Fact]
    public void should_cap_max_angle_to_35_then_clamp_angle_when_both_out_of_range()
    {
        // SIM-P002 + §0 G1: MaxAngleDeg 설정값도 하드 상한 35 를 넘지 못한다 — 순서 MaxAngleDeg → AngleDeg
        var m = new MapTiltModel { MaxAngleDeg = 60, AngleDeg = 50 };

        var warnings = m.Normalize(MIN, MAX);

        Assert.Equal(35.0, m.MaxAngleDeg);
        Assert.Equal(35.0, m.AngleDeg);
        Assert.Equal(2, warnings.Count);
        Assert.Contains("MaxAngleDeg", warnings[0]);
        Assert.Contains("AngleDeg", warnings[1]);
    }

    [Fact]
    public void should_clamp_angle_to_lowered_max_when_max_angle_below_default()
    {
        // 현장이 MaxAngleDeg=15 로 낮추면 기본 20° 도 15° 로 눌린다(경고 1건)
        var m = new MapTiltModel { MaxAngleDeg = 15 };

        var warnings = m.Normalize(MIN, MAX);

        Assert.Equal(15.0, m.MaxAngleDeg);
        Assert.Equal(15.0, m.AngleDeg);
        Assert.Single(warnings);
    }

    // ── MinZoom 0.5 스냅 (ZoomLadder.Snap 동형, AwayFromZero) ──

    [Theory]
    [InlineData(17.3, 17.5)]
    [InlineData(18.24, 18.0)]
    [InlineData(18.25, 18.5)]   // 중간값은 AwayFromZero — 은행가 반올림 금지
    [InlineData(18.75, 19.0)]
    [InlineData(18.0, 18.0)]
    [InlineData(20.5, 20.5)]    // 실효 상한 Max+1.5 는 유효
    public void should_snap_min_zoom_to_half_grid_without_warning_when_in_range(double file, double expected)
    {
        var m = new MapTiltModel { MinZoom = file };

        var warnings = m.Normalize(MIN, MAX);

        Assert.Equal(expected, m.MinZoom);
        Assert.Empty(warnings);
    }

    [Fact]
    public void should_clamp_min_zoom_to_max_plus_soft_band_when_exceeds()
    {
        // MaxZoom 19 → 실효 상한 20.5(0.5 × DigitalZoomSteps 3)
        var m = new MapTiltModel { MinZoom = 25 };

        var warnings = m.Normalize(MIN, MAX);

        Assert.Equal(20.5, m.MinZoom);
        var w = Assert.Single(warnings);
        Assert.Contains("MinZoom", w);
        Assert.Contains("20.5", w);
    }

    [Fact]
    public void should_clamp_min_zoom_to_map_min_zoom_when_below()
    {
        var m = new MapTiltModel { MinZoom = 3 };

        var warnings = m.Normalize(MIN, MAX);

        Assert.Equal(5.0, m.MinZoom);
        Assert.Single(warnings);
    }

    // ── 비정상 값(NaN/∞) ──

    [Fact]
    public void should_restore_defaults_and_warn_when_values_not_finite()
    {
        var m = new MapTiltModel
        {
            AngleDeg = double.NaN,
            MinZoom = double.NaN,
            MaxAngleDeg = double.PositiveInfinity,
        };

        var warnings = m.Normalize(MIN, MAX);

        Assert.Equal(35.0, m.MaxAngleDeg);
        Assert.Equal(20.0, m.AngleDeg);
        Assert.Equal(18.0, m.MinZoom);
        Assert.Equal(3, warnings.Count);
    }

    // ── 히스테리시스 ──

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(9, 4)]
    public void should_clamp_hysteresis_steps_when_out_of_range(int file, int expected)
    {
        var m = new MapTiltModel { HysteresisSteps = file };

        var warnings = m.Normalize(MIN, MAX);

        Assert.Equal(expected, m.HysteresisSteps);
        Assert.Single(warnings);
    }

    // ── 로그 전달·메시지 형식 ──

    [Fact]
    public void should_forward_each_warning_to_log_service_when_provided()
    {
        // SIM-P002 — 경고 로그가 실제 ILogService.Warning 으로 나간다
        var log = new RecordingLog();
        var m = new MapTiltModel { AngleDeg = 50, HysteresisSteps = 9 };

        var warnings = m.Normalize(MIN, MAX, log);

        Assert.Equal(2, warnings.Count);
        Assert.Equal(warnings, log.Warnings);
        Assert.Empty(log.Errors);
    }

    [Fact]
    public void should_format_warning_invariant_when_culture_uses_comma()
    {
        var m = new MapTiltModel { MinZoom = 25 };

        var prev = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            var w = Assert.Single(m.Normalize(MIN, MAX));
            Assert.Contains("20.5", w);
            Assert.DoesNotContain("20,5", w);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = prev;
        }
    }

    [Fact]
    public void should_describe_state_when_to_string()
    {
        var m = new MapTiltModel { IsEnabled = true, AngleDeg = 25 };

        Assert.Equal("MapTilt[enabled=True, angle=25.0, minZoom=18.0, hyst=1, maxAngle=35.0]", m.ToString());
    }

    private sealed class RecordingLog : ILogService
    {
        public List<string> Warnings { get; } = new();
        public List<string> Errors { get; } = new();

#pragma warning disable CS0067
        public event EventHandler<LogEventArgs>? LogEvent;
#pragma warning restore CS0067

        public void Error(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
            => Errors.Add(msg);

        public void Info(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) { }

        public void Warning(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
            => Warnings.Add(msg);
    }
}
