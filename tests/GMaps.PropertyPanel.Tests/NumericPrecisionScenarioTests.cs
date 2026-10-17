using System.Globalization;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Numerics;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using Moq;
using Xunit;

namespace GMaps.PropertyPanel.Tests;

/// <summary>
/// 사용자 보고(2026-10-13) "심볼 속성 슬라이더를 움직이면 정수가 아니라 11.9999999… 같은 무한 소수가 나온다 ·
/// 음수를 갖지 않는 값도 확인해 달라". 명세 원본 = docs/prds/map-property-numeric-precision-prd.md 의 표(코드판 MapNumericFields).
/// <para>슬라이더가 내보내는 비정수(11.9999999 · 0.1+0.2 · -3 · 최대+1)를 속성창 칸에 넣었을 때 표시 글 · 칸 값 · 마커 모델(저장 본문의 원본)
/// · 변경 이벤트(저장 · undo 의 인자)가 규칙대로인지 본다. 창은 띄우지 않는다(부모 없는 컨트롤 · AppHost 디스패처).</para>
/// </summary>
public class NumericPrecisionScenarioTests
{
    private static ILogService Log() => Mock.Of<ILogService>();

    private static readonly double[] Wild = { 11.9999999, 0.1 + 0.2, -3, 37.4285714, 1e9, -1e9, double.PositiveInfinity, double.NegativeInfinity, 12.5, -0.5 };

    /// <summary>값이 범위 안 · 단위의 정확한 배수이고, 문자열로 바꿔도 정해진 자리수를 넘지 않는다.</summary>
    private static void AssertClean(NumericFieldSpec spec, double value)
    {
        Assert.InRange(value, spec.Min, spec.Max);
        decimal ratio = (decimal)value / (decimal)spec.Step;
        Assert.True(ratio == Math.Round(ratio), $"{spec}: {value:R} 는 단위 배수가 아니다");
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        int digits = text.Contains('.') ? text.Length - text.IndexOf('.') - 1 : 0;
        Assert.True(digits <= spec.Digits, $"{spec}: {text} — 소수 {digits}자리(허용 {spec.Digits})");
        if (!spec.AllowsNegative) Assert.True(value >= 0, $"{spec}: 음수 {text}");
    }

    // ── NUM-001~004 — 공용 도우미 ──

    [Fact]
    [Trait("Scenario", "NUM-001")]
    public void should_return_clean_value_for_every_field_when_slider_emits_wild_number()
    {
        foreach (var spec in MapNumericFields.All)
            foreach (var raw in Wild)
                AssertClean(spec, spec.Normalize(raw));
    }

    [Fact]
    [Trait("Scenario", "NUM-002")]
    public void should_keep_nan_when_group_pending_marker_passes_through()
    {
        foreach (var spec in MapNumericFields.All)
            Assert.True(double.IsNaN(spec.Normalize(double.NaN)), spec.ToString());
    }

    [Theory]
    [Trait("Scenario", "NUM-003")]
    [InlineData(11.9999999, 12)]
    [InlineData(11.5, 12)]          // 중간값 = 0 에서 먼 쪽
    [InlineData(11.4999999, 11)]
    [InlineData(-3, 0)]             // 음수 → 최소
    [InlineData(101, 100)]          // 최대 + 1 → 최대
    public void should_round_and_clamp_at_boundaries_when_integer_field_receives_value(double raw, double expected)
        => Assert.Equal(expected, MapNumericFields.MarkerAltitude.Normalize(raw));

    [Fact]
    [Trait("Scenario", "NUM-004")]
    public void should_drop_binary_tail_when_wpf_snap_formula_produces_it()
    {
        // 수정 전 빨강의 원인: WPF 스냅은 Minimum + round((v-Minimum)/Tick)*Tick 을 double 로 계산한다 → 꼬리
        double wpfSnap = 0 + Math.Round(3.3 / 1.1) * 1.1;                     // 눈금 1.1 · 최소 0 인 스냅과 같은 식
        Assert.NotEqual(3.3, wpfSnap);                                           // 3.3000000000000003
        Assert.Equal(3.3, MapNumericFields.FenceHeightM.Normalize(wpfSnap));     // 도우미를 지나면 정확히 3.3
        Assert.Equal(0.3, MapNumericFields.TrackMaxSpeedMs.Normalize(0.1 + 0.2));
        Assert.Equal("0.3", MapNumericFields.LineOpacity.Normalize(0.1 + 0.2).ToString(CultureInfo.InvariantCulture));
        // 옛 강제 함수(소수 2자리 반올림)는 정수 칸에 37.43 을 남겼다
        Assert.Equal(37.43, Math.Round(37.4285714, 2));
        Assert.Equal(37, MapNumericFields.MarkerAltitude.Normalize(37.4285714));
    }

    // ── NUM-010~015 — 속성창 칸 → 마커 모델 → 이벤트 ──

    private static GMapPidsMarker Camera(int id = 1) => new(Log(), new PidsSymbolModel
    {
        Id = id, Title = $"cam{id}", DeviceType = EnumDeviceType.IpCamera, Latitude = 37.5, Longitude = 127.0 + id * 0.001,
        Width = 40, Height = 40, TitleSize = 12, DetectionRange = 30, DetectionAngle = 90, FOVOpacity = 0.8, Visible = true,
    });

    [Fact]
    [Trait("Scenario", "NUM-010")]
    public void should_store_integers_when_pids_detection_sliders_emit_fractions() => AppHost.Run(() =>
    {
        using var marker = Camera();
        var panel = new GMapPropertyPidsControl { SelectedMarker = marker };
        var events = new List<(string Name, object? Old, object? New)>();
        panel.MarkerPropertyChanged += (_, e) => events.Add((e.PropertyName, e.OldValue, e.NewValue));

        panel.DetectionRange = 123.4567;
        panel.DetectionAngle = 0.1 + 0.2;          // 최소 5 아래
        panel.DetectionBearing = -180.6;           // 음수 허용 칸 — 최소 -180 까지
        panel.BaseBearing = 359.6;
        panel.FOVOpacity = 0.1 + 0.2 + 0.0000001;

        Assert.Equal(123, panel.DetectionRange); Assert.Equal(123, marker.DetectionRange);
        Assert.Equal(5, panel.DetectionAngle); Assert.Equal(5, marker.DetectionAngle);
        Assert.Equal(-180, panel.DetectionBearing); Assert.Equal(-180, marker.DetectionBearing);
        Assert.Equal(360, panel.BaseBearing);
        Assert.Equal(0.3, panel.FOVOpacity); Assert.Equal(0.3, marker.FOVOpacity);
        Assert.Contains(events, e => e.Name == "DetectionRange" && Equals(e.New, 123d));
        Assert.Contains(events, e => e.Name == "FOVOpacity" && Equals(e.New, 0.3));
    });

    [Fact]
    [Trait("Scenario", "NUM-011")]
    public void should_store_integer_altitude_bearing_and_title_size_when_base_fields_get_fractions() => AppHost.Run(() =>
    {
        using var marker = Camera();
        var panel = new GMapPropertyPidsControl { SelectedMarker = marker };

        panel.MarkerAltitude = 37.4285714;
        panel.MarkerBearing = 11.9999999;
        panel.TitleSize = 11.9999999;
        panel.MarkerZoom = 7.3;
        panel.TitleMaxWidthPx = 123.456;

        Assert.Equal(37f, marker.Altitude);
        Assert.Equal(12, marker.Bearing);
        Assert.Equal(12, marker.TitleSize);
        Assert.Equal(7.5, marker.Zoom);
        Assert.Equal(123, marker.TitleMaxWidth);

        panel.MarkerAltitude = -3;   // 음수 금지
        panel.TitleSize = -3;
        Assert.Equal(0f, marker.Altitude);
        Assert.Equal(6, marker.TitleSize);
    });

    [Fact]
    [Trait("Scenario", "NUM-012")]
    public void should_cut_loaded_values_when_marker_holds_tail_or_negative() => AppHost.Run(() =>
    {
        // DB 에서 꼬리 · 음수가 실려 와도 칸에는 규칙대로만 보인다
        using var marker = Camera();
        marker.DetectionRange = 87.6543210;
        marker.DetectionAngle = -10;
        var panel = new GMapPropertyPidsControl { SelectedMarker = marker };

        Assert.Equal(88, panel.DetectionRange);
        Assert.Equal(5, panel.DetectionAngle);
    });

    [Fact]
    [Trait("Scenario", "NUM-013")]
    public void should_raise_clean_values_for_all_members_when_group_editing_numeric_field() => AppHost.Run(() =>
    {
        using var a = Camera(1);
        using var b = Camera(2);
        b.DetectionRange = 50;
        var panel = new GMapPropertyPidsControl { SelectedMarker = a };
        panel.EnterGroupMode(new IEditableMarker[] { a, b });
        Assert.True(double.IsNaN(panel.DetectionRange));   // 다름 → 빈칸(NaN) 그대로 — 표지는 정규화하지 않는다
        var events = new List<(string Name, object? New)>();
        panel.MarkerPropertyChanged += (_, e) => events.Add((e.PropertyName, e.NewValue));

        panel.DetectionRange = 0.1 + 0.2 + 99.9999999;    // 최소 10 → 100
        panel.MarkerAltitude = 11.9999999;

        var range = Assert.Single(events, e => e.Name == "DetectionRange");
        Assert.Equal(100d, range.New);
        Assert.Contains(events, e => e.Name == "Altitude" && Equals(e.New, 12d));
        // VM 은 이 이벤트 값을 전원에 같은 경로로 적용한다 — 두 마커 모두 정수
        foreach (var m in new IEditableMarker[] { a, b }) UndoableCommandBase.ApplyProperty(m, "DetectionRange", range.New);
        Assert.Equal(100, a.DetectionRange); Assert.Equal(100, b.DetectionRange);
    });

    [Fact]
    [Trait("Scenario", "NUM-014")]
    public void should_restore_clean_value_when_numeric_edit_is_undone_and_redone() => AppHost.Run(() =>
    {
        using var marker = Camera();
        var panel = new GMapPropertyPidsControl { SelectedMarker = marker };
        (object? Old, object? New) change = default;
        bool recording = true;
        panel.MarkerPropertyChanged += (_, e) => { if (recording && e.PropertyName == "DetectionRange") change = (e.OldValue, e.NewValue); };

        panel.DetectionRange = 245.5;   // 중간값 → 246
        recording = false;              // undo/redo 재적용이 칸을 따라 움직일 때의 이벤트는 기록하지 않는다

        Assert.Equal(30d, change.Old); Assert.Equal(246d, change.New);
        UndoableCommandBase.ApplyProperty(marker, "DetectionRange", change.Old);   // undo
        Assert.Equal(30, marker.DetectionRange); Assert.Equal(30, panel.DetectionRange);
        UndoableCommandBase.ApplyProperty(marker, "DetectionRange", change.New);   // redo
        Assert.Equal(246, marker.DetectionRange); Assert.Equal(246, panel.DetectionRange);
    });

    [Fact]
    [Trait("Scenario", "NUM-015")]
    public void should_keep_fence_height_on_exact_tenth_when_snap_formula_leaves_tail() => AppHost.Run(() =>
    {
        var model = new PidsGroupSymbolModel { Title = "구역", Latitude = 37.5, Longitude = 127.0, FenceMode = EnumFenceMode.Posts };
        model.LinePoints = new List<GeoPoint> { new(37.5, 127.0, 0), new(37.5, 127.001, 0) };
        using var marker = new GMapPidsGroupMarker(Log(), model);
        var panel = new GMapPropertyPidsGroupControl { Is3DFeatureEnabled = true, SelectedMarker = marker };

        panel.FenceHeightM = 3 * 1.1;            // 3.3000000000000003
        panel.PostSpacingM = -3;
        panel.LineOpacity = 0.1 + 0.2;

        Assert.Equal(3.3, panel.FenceHeightM);
        Assert.Equal(1, panel.PostSpacingM);
        Assert.Equal(0.3, panel.LineOpacity);
        panel.FlushDeferredCommits();   // 150 ms 지연 커밋을 마커가 살아 있을 때 확정 — 남기면 다음 시험 중에 터져 시험 호스트가 죽었다(2026-10-14)
    });

    // ── NUM-016~018 — 표시 글 · 다른 칸 ──

    [Theory]
    [Trait("Scenario", "NUM-016")]
    [InlineData("32.7", 33d)]
    [InlineData("-5", 0d)]
    [InlineData("99999", 10000d)]
    [InlineData("11.9999999", 12d)]
    public void should_cut_typed_text_when_size_box_receives_fraction_or_negative(string typed, double expected)
    {
        var converter = new DoubleNaNToBlankConverter { Spec = MapNumericFields.MarkerWidth };
        Assert.Equal(expected, converter.ConvertBack(typed, typeof(double), null, CultureInfo.InvariantCulture));
        Assert.Equal("33", converter.Convert(32.7, typeof(string), null, CultureInfo.InvariantCulture));   // 끌기로 생긴 소수도 정수로만 보인다
        Assert.Equal(string.Empty, converter.Convert(double.NaN, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    [Trait("Scenario", "NUM-017")]
    public void should_show_tailless_text_when_panel_values_are_formatted() => AppHost.Run(() =>
    {
        // 칸은 NaNBlank(기본 ToString) 로 그린다 — 칸 값에 꼬리가 없으면 글에도 없다
        using var marker = Camera();
        var panel = new GMapPropertyPidsControl { SelectedMarker = marker };
        var blank = new DoubleNaNToBlankConverter();
        panel.TitleSize = 11.9999999;
        panel.FOVOpacity = 0.1 + 0.2;

        Assert.Equal("12", blank.Convert(panel.TitleSize, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("0.3", blank.Convert(panel.FOVOpacity, typeof(string), null, CultureInfo.InvariantCulture));
    });

    [Fact]
    [Trait("Scenario", "NUM-018")]
    public void should_round_tint_strength_and_floor_counts_when_sliders_emit_fractions() => AppHost.Run(() =>
    {
        var visual = new HousingVisual();
        HousingAppearance.SetTintStrength(visual, 0.1 + 0.2 + 0.0004);
        Assert.Equal(0.3, HousingAppearance.GetTintStrength(visual));
        HousingAppearance.SetTintStrength(visual, -1);
        Assert.Equal(0, HousingAppearance.GetTintStrength(visual));

        var panel = new GMapPropertyInfraControl { FloorCount = -3, BasementFloorCount = 99, BuildingArea = -12.7 };
        Assert.Equal(1, panel.FloorCount);
        Assert.Equal(10, panel.BasementFloorCount);
        Assert.Equal(0, panel.BuildingArea);
        panel.BuildingArea = 123.6;
        Assert.Equal(124, panel.BuildingArea);
    });

    // ── NUM-020 — 지도 겹창(추적 설정) ──

    [Fact]
    [Trait("Scenario", "NUM-020")]
    public void should_clamp_and_round_tracking_settings_when_fields_get_wild_values()
    {
        var model = new TrackingSetupModel();
        var vm = new TrackingSetupViewModel(model);

        vm.TrailMaxPoints = 5; vm.GapThresholdSec = -3; vm.DefaultTtlSec = 0; vm.LostTtlSec = -1;
        vm.MaxSpeedMs = 0.1 + 0.2; vm.MinVisibleZoom = 11.9999999; vm.MaxPlaybackHours = -24;

        Assert.Equal(10, model.TrailMaxPoints);
        Assert.Equal(1, model.GapThresholdSec);
        Assert.Equal(1, model.DefaultTtlSec);
        Assert.Equal(1, model.LostTtlSec);
        Assert.Equal(0.3, model.MaxSpeedMs);
        Assert.Equal(12, model.MinVisibleZoom);
        Assert.Equal(1, model.MaxPlaybackHours);

        vm.MaxSpeedMs = -5;
        Assert.Equal(0, model.MaxSpeedMs);
    }
}
