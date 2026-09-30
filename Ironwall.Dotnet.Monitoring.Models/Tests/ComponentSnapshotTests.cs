using Ironwall.Dotnet.Monitoring.Models.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Monitoring.Models.Tests;

/// <summary>
/// 부품 표 빌더(<see cref="ComponentSnapshot"/>) — 선언 · 관측 · 설정 합치기, 수 · 요약 줄, 정렬 두 방식, 6.3 무회귀.
/// </summary>
public class ComponentSnapshotTests
{
    internal static DeviceAxesModel Axes(bool received, JObject? overrides, params (string Key, string Type, string? Label, bool? InService, string? State, string? Health, string? Reason, string? ObservedAt)[] parts)
    {
        var spec = new HardwareSpecModel();
        var status = received ? new DeviceStatusModel() : null;
        foreach (var p in parts)
        {
            if (!string.IsNullOrEmpty(p.Type))
                spec.Components.Add(new ComponentDefinitionModel { Key = p.Key, Type = p.Type, Label = p.Label, InService = p.InService });
            if (status != null && (p.Health != null || p.State != null))
                status.Components[p.Key] = new ComponentStatusModel { State = p.State, Health = p.Health, FaultReason = p.Reason, ObservedAt = p.ObservedAt };
        }
        return new DeviceAxesModel
        {
            HardwareSpec = spec,
            DeviceStatus = status,
            DeviceConfig = overrides == null ? null : new DeviceConfigModel { ComponentOverrides = overrides },
            Meta = new ResponseMeta("full", received ? new[] { "hardware_spec", "device_status" } : new[] { "hardware_spec" }),
        };
    }

    [Fact]
    public void should_draw_nothing_and_say_no_info_when_device_has_no_axes()
    {
        var s = ComponentSnapshot.Build(null);
        Assert.Same(ComponentSnapshot.None, s);
        Assert.False(s.IsAvailable);
        Assert.Empty(s.Rows);
        Assert.Equal("부품 정보 없음", s.SummaryText);   // FR-08
    }

    [Fact]
    public void should_say_no_info_without_counts_when_status_section_is_not_received()
    {
        var s = ComponentSnapshot.Build(Axes(false, null, ("heater_1", "HEATER", null, null, null, null, null, null)));
        Assert.True(s.HasAxes);
        Assert.False(s.IsReceived);
        Assert.Equal(ComponentHealthLevel.None, s.WorstHealth);
        Assert.Equal(0, s.UnknownCount);
        Assert.Equal("부품 정보 없음", s.SummaryText);
        Assert.Single(s.Rows);   // 선언은 보인다
    }

    [Fact]
    public void should_count_and_summarize_when_components_report()
    {
        var s = ComponentSnapshot.Build(Axes(true, null,
            ("radar", "RADAR_UNIT", null, null, "IDLE", "FAULT", "COMM_ERROR", "2026-10-01T09:41:07.000000+09:00"),
            ("eo", "EO_CAMERA", null, null, null, "OK", null, "2026-10-01T09:12:55.000000+09:00"),
            ("vib", "VIBRATION_SENSOR", "진동 감지부", null, "IDLE", "OK", null, "2026-10-01T08:59:02.000000+09:00"),
            ("pir", "PIR_SENSOR", "PIR 감지부", null, null, null, null, null),
            ("ir", "IR_LED", null, null, "ON", "DEGRADED", "OVER_CURRENT", "2026-10-01T09:22:40.000000+09:00")));

        Assert.Equal(1, s.FaultCount);
        Assert.Equal(1, s.DegradedCount);
        Assert.Equal(2, s.OkCount);
        Assert.Equal(1, s.UnknownCount);   // PIR: 선언은 있는데 관측이 없다
        Assert.Equal(ComponentHealthLevel.Fault, s.WorstHealth);
        Assert.Equal("고장 1 · 저하 1 · 정상 2 · 미상 1", s.SummaryText);

        var radar = s.Rows[0];
        Assert.Equal("레이더", radar.Name);
        Assert.Equal("대기", radar.StateText);
        Assert.Equal("고장", radar.HealthText);
        Assert.Equal("통신 오류", radar.FaultText);
        Assert.Equal(ComponentHealthKind.Crit, radar.HealthKind);
        Assert.Equal("—", s.Rows[1].StateText);            // 상태 축이 없는 유형
        Assert.Equal("미상", s.Rows[3].HealthText);
        Assert.Equal("radar", s.LastObserved!.Key);        // 가장 최근 변화
    }

    [Fact]
    public void should_keep_declared_order_or_put_faults_first_when_sort_mode_changes()
    {
        var s = ComponentSnapshot.Build(Axes(true, null,
            ("a", "HEATER", null, null, "ON", "OK", null, null),
            ("b", "FAN", null, false, "OFF", "FAULT", "OVER_TEMP", null),     // 사용 안 함 — 고장이어도 맨 뒤
            ("c", "UPS", null, null, null, null, null, null),
            ("d", "DOOR_SENSOR", null, null, "OPEN", "DEGRADED", null, null),
            ("e", "BUZZER", null, null, "OFF", "FAULT", "ETC", null)));

        Assert.Equal(new[] { "a", "b", "c", "d", "e" }, s.Sorted(ComponentSortMode.Declared).Select(r => r.Key));
        // 고장 → 저하 → 나머지(정상 · 미상은 선언 순서 그대로) → 사용 안 함
        Assert.Equal(new[] { "e", "d", "a", "c", "b" }, s.Sorted(ComponentSortMode.FaultFirst).Select(r => r.Key));
        Assert.Equal(1, s.FaultCount);            // 사용 안 함은 집계에서 뺀다
        Assert.Equal(1, s.OutOfServiceCount);
        Assert.Equal("사용 안 함", s.Rows[1].HealthText);
        Assert.Equal(ComponentHealthKind.OutOfService, s.Rows[1].HealthKind);
        Assert.Equal(string.Empty, s.Rows[1].FaultText);
    }

    [Fact]
    public void should_keep_observed_only_keys_after_declared_rows_when_shape_is_missing()
    {
        var axes = Axes(true, null, ("heater", "HEATER", null, null, "ON", "OK", null, null));
        axes.DeviceStatus!.Components["nic0"] = new ComponentStatusModel { Health = "FAULT", FaultReason = "COMM_ERROR" };

        var s = ComponentSnapshot.Build(axes);
        Assert.Equal(new[] { "heater", "nic0" }, s.Rows.Select(r => r.Key));
        Assert.False(s.Rows[1].IsDeclared);
        Assert.Equal("nic0", s.Rows[1].Name);
        Assert.Equal("nic0: 고장 · 통신 오류", s.Rows[1].ToLine());
        Assert.Equal(1, s.DeclaredCount);
    }

    [Fact]
    public void should_show_intent_against_observation_and_honor_override_out_of_service_when_overrides_exist()
    {
        var overrides = JObject.Parse("""{ "heater": { "enabled": true }, "fan": { "enabled": false }, "door_sw": { "in_service": false } }""");
        var s = ComponentSnapshot.Build(Axes(true, overrides,
            ("heater", "HEATER", null, null, "OFF", "FAULT", "OVER_TEMP", null),
            ("fan", "FAN", null, null, "OFF", "OK", null, null),
            ("door_sw", "DOOR_SENSOR", null, null, "OPEN", "FAULT", "SENSOR_TIMEOUT", null)));

        Assert.Equal("설정 켬 / 관측 꺼짐", s.Rows[0].StateText);
        Assert.True(s.Rows[0].IsIntentMismatch);
        Assert.Equal("꺼짐", s.Rows[1].StateText);
        Assert.False(s.Rows[2].InService);     // 설정의 in_service=false 도 사용 안 함
        Assert.Equal(1, s.FaultCount);
        Assert.Equal("히터: 고장 · 과열 · 설정 켬 / 관측 꺼짐", s.Rows[0].ToLine());
    }

    [Fact]
    public void should_use_catalog_korean_for_names_when_catalog_labels_are_given()
    {
        var s = ComponentSnapshot.Build(Axes(true, null, ("h", "HEATER", null, null, "ON", "OK", null, null)),
            new ComponentDisplayTests.FakeLabels(("HEATER", "함체 히터")));
        Assert.Equal("함체 히터", s.Rows[0].Name);
        Assert.Equal("함체 히터", s.Rows[0].TypeName);
    }

    [Fact]
    public void should_toggle_row_order_and_notify_when_sort_button_is_pressed()
    {
        var table = new ComponentTableModel(ComponentSnapshot.Build(Axes(true, null,
            ("a", "HEATER", null, null, "ON", "OK", null, null),
            ("b", "FAN", null, null, "OFF", "FAULT", "OVER_TEMP", "2026-10-01T09:41:07.000000+09:00"))), ComponentSortMode.Declared, today: null);
        var raised = new List<string?>();
        table.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Assert.Equal("선언 순서", table.SortText);
        Assert.Equal(new[] { "a", "b" }, table.Rows.Select(r => r.Key));

        table.IsFaultFirst = true;

        Assert.Equal("고장 먼저", table.SortText);
        Assert.Equal(new[] { "b", "a" }, table.Rows.Select(r => r.Key));
        Assert.Contains(nameof(ComponentTableModel.Rows), raised);
        Assert.Equal(ComponentHealthKind.Crit, table.SummaryKind);
        Assert.StartsWith("마지막 변화 ", table.LastChangeText);
        Assert.EndsWith("· 팬", table.LastChangeText);
    }

    [Fact]
    public void should_show_only_no_info_line_when_table_is_built_without_axes()
    {
        var table = new ComponentTableModel(ComponentSnapshot.None, ComponentSortMode.FaultFirst, today: null);
        Assert.False(table.IsAvailable);
        Assert.False(table.HasRows);
        Assert.Equal("부품 정보 없음", table.SummaryText);
        Assert.Equal(string.Empty, table.LastChangeText);
    }

    [Fact]
    public void should_say_no_components_when_status_is_received_but_nothing_declared_or_observed()
        => Assert.Equal("부품 없음", ComponentSnapshot.Build(Axes(true, null)).SummaryText);
}
