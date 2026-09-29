using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// 부품 요약 순수 함수 표 시험 — 장비 부품 설정을 지도 아이콘에 표현(1차 조각, WP-2 Part B).
/// </summary>
public class ComponentHealthSummaryTests
{
    internal static DeviceAxesModel Axes(bool received = true, params (string Key, string Type, string? Label, bool? InService, string? State, string? Health, string? Reason, bool Observed)[] parts)
    {
        var spec = new HardwareSpecModel();
        var status = received ? new DeviceStatusModel() : null;
        foreach (var p in parts)
        {
            if (!string.IsNullOrEmpty(p.Type))
                spec.Components.Add(new ComponentDefinitionModel { Key = p.Key, Type = p.Type, Label = p.Label, InService = p.InService });
            if (p.Observed && status != null)
                status.Components[p.Key] = new ComponentStatusModel { State = p.State, Health = p.Health, FaultReason = p.Reason, ObservedAt = "2026-09-30T00:00:00.000000+09:00" };
        }
        return new DeviceAxesModel { HardwareSpec = spec, DeviceStatus = status, Meta = new ResponseMeta("full", received ? new[] { "hardware_spec", "device_status" } : new[] { "hardware_spec" }) };
    }

    [Fact]
    public void should_draw_and_write_nothing_when_device_has_no_axes()
    {
        var s = ComponentHealthSummary.Build(null);
        Assert.Same(ComponentHealthSummary.None, s);
        Assert.False(s.ShowsBadge);
        Assert.Null(s.ToToolTipSection());
        Assert.Null(s.ShortText());
    }

    [Fact]
    public void should_say_not_received_without_badge_when_status_section_is_missing()
    {
        var s = ComponentHealthSummary.Build(Axes(false, ("heater_1", "HEATER", null, null, null, null, null, false)));
        Assert.False(s.IsReceived);
        Assert.Equal(ComponentHealthLevel.None, s.Health);
        Assert.False(s.ShowsBadge);
        Assert.Equal("부품 상태: 미수신", s.ToToolTipSection());
    }

    [Theory]
    // 건강 문자열들 → 최악 · 고장 수 · 저하 수 · 배지
    [InlineData("OK,OK", ComponentHealthLevel.Ok, 0, 0, false)]
    [InlineData("OK,FAULT", ComponentHealthLevel.Fault, 1, 0, true)]
    [InlineData("FAULT,FAULT,DEGRADED", ComponentHealthLevel.Fault, 2, 1, true)]
    [InlineData("OK,DEGRADED", ComponentHealthLevel.Degraded, 0, 1, true)]
    [InlineData("OK,UNKNOWN", ComponentHealthLevel.Unknown, 0, 0, false)]
    [InlineData("ok,fault", ComponentHealthLevel.Fault, 1, 0, true)]          // 대소문자 무시
    [InlineData("OK,BROKEN", ComponentHealthLevel.Unknown, 0, 0, false)]     // 모르는 어휘 = 확인 안 됨(거짓 고장 없음)
    public void should_pick_worst_health_when_components_report(string healths, ComponentHealthLevel expected, int faults, int degraded, bool badge)
    {
        var parts = healths.Split(',').Select((h, i) => ($"c{i}", "FAN", (string?)null, (bool?)null, (string?)"ON", (string?)h, (string?)null, true)).ToArray();
        var s = ComponentHealthSummary.Build(Axes(true, parts));
        Assert.Equal(expected, s.Health);
        Assert.Equal(faults, s.FaultCount);
        Assert.Equal(degraded, s.DegradedCount);
        Assert.Equal(badge, s.ShowsBadge);
    }

    [Fact]
    public void should_list_fault_first_with_korean_reason_and_state_when_heater_overheats()
    {
        var s = ComponentHealthSummary.Build(Axes(true,
            ("fan_1", "FAN", null, null, "ON", "OK", null, true),
            ("heater_1", "HEATER", null, null, "ON", "FAULT", "OVER_TEMP", true)));
        Assert.Equal("히터: 고장 · 과열 · 켜짐", s.Rows[0].ToLine());
        Assert.Equal("팬: 정상 · 켜짐", s.Rows[1].ToLine());
        Assert.Equal(1, s.BadgeCount);
        Assert.StartsWith("부품 상태 — 고장 1 · 저하 0 / 2", s.ToToolTipSection());
    }

    [Fact]
    public void should_exclude_out_of_service_component_from_health_when_in_service_is_false()
    {
        var s = ComponentHealthSummary.Build(Axes(true,
            ("fan_1", "FAN", null, null, "ON", "OK", null, true),
            ("heater_1", "HEATER", "예비 히터", false, "OFF", "FAULT", "POWER_LOSS", true)));
        Assert.Equal(ComponentHealthLevel.Ok, s.Health);
        Assert.False(s.ShowsBadge);
        Assert.Equal(1, s.OutOfServiceCount);
        Assert.Equal("예비 히터: 사용 안 함", s.Rows.Last().ToLine());
    }

    [Fact]
    public void should_treat_declared_but_unobserved_component_as_unknown_when_no_report_yet()
    {
        var s = ComponentHealthSummary.Build(Axes(true,
            ("fan_1", "FAN", null, null, "ON", "OK", null, true),
            ("ups", "UPS", null, null, null, null, null, false)));
        Assert.Equal(ComponentHealthLevel.Unknown, s.Health);
        Assert.Contains(s.Rows, r => r.ToLine() == "무정전 전원장치: 미수신");
    }

    [Fact]
    public void should_count_observed_key_without_declaration_when_shape_is_not_entered()
    {
        var axes = Axes(true);
        axes.DeviceStatus!.Components["nic0"] = new ComponentStatusModel { Health = "FAULT", FaultReason = "COMM_ERROR" };
        var s = ComponentHealthSummary.Build(axes);
        Assert.Equal(ComponentHealthLevel.Fault, s.Health);
        Assert.Equal("nic0: 고장 · 통신 오류", s.Rows[0].ToLine());
        Assert.Equal(0, s.DeclaredCount);
    }

    [Fact]
    public void should_cap_tooltip_rows_when_many_components()
    {
        var parts = Enumerable.Range(0, 8).Select(i => ($"f{i}", "FAN", (string?)null, (bool?)null, (string?)"ON", (string?)"OK", (string?)null, true)).ToArray();
        var lines = ComponentHealthSummary.Build(Axes(true, parts)).ToToolTipSection()!.Split(Environment.NewLine);
        Assert.Equal(1 + ComponentHealthSummary.MaxToolTipRows + 1, lines.Length);
        Assert.Equal("· 외 2개", lines.Last());
    }

    [Theory]
    [InlineData("DOOR_ACTUATOR", "gate_motor", "RUNNING", DoorMotionState.Running)]
    [InlineData("DOOR_ACTUATOR", "gate_motor", "OPEN", DoorMotionState.Open)]
    [InlineData("DOOR_SENSOR", "front", "CLOSED", DoorMotionState.Closed)]
    [InlineData("", "door", "OPEN", DoorMotionState.Open)]          // 선언 없이 관례 key
    [InlineData("", "actuator", "RUNNING", DoorMotionState.Running)]
    [InlineData("HEATER", "heater", "ON", DoorMotionState.Unknown)]
    public void should_read_door_motion_when_door_component_reports(string type, string key, string state, DoorMotionState expected)
    {
        var axes = Axes(true, (key, type, null, null, state, "OK", null, true));
        if (string.IsNullOrEmpty(type)) axes.DeviceStatus!.Components[key] = new ComponentStatusModel { State = state, Health = "OK" };
        Assert.Equal(expected, ComponentHealthSummary.Build(axes).DoorMotion);
    }

    [Theory]
    // hasDoor, 형태 축, 부품 관측, 3D(문짝이 위치를 그림) → 표시
    [InlineData(false, EnumDoorState.Open, DoorMotionState.Running, false, DoorIndicatorKind.None)]
    [InlineData(true, EnumDoorState.Closed, DoorMotionState.Running, false, DoorIndicatorKind.Running)]   // 구동 중이 이긴다(형태 축엔 자리 없음)
    [InlineData(true, EnumDoorState.Open, DoorMotionState.Unknown, false, DoorIndicatorKind.Open)]
    [InlineData(true, EnumDoorState.Unknown, DoorMotionState.Closed, false, DoorIndicatorKind.Closed)]
    [InlineData(true, EnumDoorState.Unknown, DoorMotionState.Unknown, false, DoorIndicatorKind.Unknown)]
    [InlineData(true, EnumDoorState.Open, DoorMotionState.Open, true, DoorIndicatorKind.None)]           // 3D: 문짝이 그린다
    [InlineData(true, EnumDoorState.Open, DoorMotionState.Running, true, DoorIndicatorKind.Running)]     // 3D 도 구동 중은 표시
    public void should_resolve_door_indicator_when_channels_combine(bool hasDoor, EnumDoorState door, DoorMotionState motion, bool leaves, DoorIndicatorKind expected)
        => Assert.Equal(expected, DoorIndicatorRules.Resolve(hasDoor, door, motion, leaves));

    [Theory]
    [InlineData(ComponentHealthLevel.Fault, 32, 1.0, true)]
    [InlineData(ComponentHealthLevel.Degraded, 24, 1.0, true)]
    [InlineData(ComponentHealthLevel.Fault, 20, 1.0, false)]       // 작게 그려진 아이콘 — 숨김
    [InlineData(ComponentHealthLevel.Fault, 20, 1.25, true)]       // 디지털 줌으로 커지면 표시
    [InlineData(ComponentHealthLevel.Unknown, 64, 1.0, false)]     // 모르는 것은 그리지 않는다
    [InlineData(ComponentHealthLevel.Ok, 64, 1.0, false)]
    [InlineData(ComponentHealthLevel.Fault, 32, double.NaN, true)] // 비정상 배율 = 1
    public void should_gate_badge_by_screen_pixels_when_marker_size_varies(ComponentHealthLevel health, double side, double scale, bool expected)
        => Assert.Equal(expected, ComponentBadgeLod.ShowsBadge(health, ComponentBadgeLod.ScreenPixels(side, side + 10, scale)));

    [Fact]
    public void should_compose_automation_id_and_name_when_symbol_state_is_known()
    {
        Assert.Equal("GMaps.Symbol.Enclosure.77", SymbolStatusText.AutomationId(EnumDeviceType.Enclosure, 77, 5));
        Assert.Equal("GMaps.Symbol.Gate.Unlinked.5", SymbolStatusText.AutomationId(EnumDeviceType.Gate, 0, 5));
        var summary = ComponentHealthSummary.Build(Axes(true, ("heater_1", "HEATER", null, null, "ON", "FAULT", "OVER_TEMP", true)));
        Assert.Equal("함체 A · 이벤트 탐지 중 · 장비 오류 · 문 동작 중 · 부품 고장 1 · 저하 0 / 1",
            SymbolStatusText.AutomationName("함체 A", EnumEventStatus.Detecting, EnumOperationState.ERROR, DoorIndicatorKind.Running, summary));
        Assert.Equal("카메라 · 이벤트 정상 · 장비 활성", SymbolStatusText.AutomationName("카메라", EnumEventStatus.Normal, EnumOperationState.ACTIVATED, DoorIndicatorKind.None, ComponentHealthSummary.None));
        Assert.Equal("카메라", SymbolStatusText.ToolTip("카메라", DoorIndicatorKind.None, ComponentHealthSummary.None));   // 6.3 무회귀: 제목만
    }
}
