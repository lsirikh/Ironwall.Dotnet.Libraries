using Ironwall.Dotnet.Monitoring.Models.Components;
using Xunit;

namespace Ironwall.Dotnet.Monitoring.Models.Tests;

/// <summary>
/// 공용 부품 사전(component-display-unify FR-01) — 서버 카탈로그 어휘 전수 표 · 어휘 밖 원문 · 이름 규칙 · 요약 줄.
/// </summary>
/// <remarks>어휘 표의 정본은 서버 <c>api-test-server app/utils/init_component_catalog.py</c>(유형 32 · 상태 10 · 사유 6).</remarks>
public class ComponentDisplayTests
{
    /// <summary>서버 카탈로그의 유형 32종 — 코드와 한글이 서버와 같아야 한다(카탈로그를 못 읽을 때의 폴백).</summary>
    public static readonly TheoryData<string, string> ServerTypes = new()
    {
        { "DOOR_SENSOR", "도어 센서" }, { "DOOR_LOCK", "도어 잠금" }, { "DOOR_ACTUATOR", "문 구동부" }, { "LIMIT_SWITCH", "리미트 스위치" },
        { "TEMPERATURE_SENSOR", "온도 센서" }, { "HUMIDITY_SENSOR", "습도 센서" }, { "VOLTAGE_SENSOR", "전압 센서" },
        { "CURRENT_SENSOR", "전류 센서" }, { "VIBRATION_METER", "진동 계측기" }, { "UPS", "무정전 전원장치" },
        { "HEATER", "히터" }, { "FAN", "팬" }, { "HEADLIGHT", "전조등" },
        { "PIR_SENSOR", "PIR 센서" }, { "ULTRASONIC_SENSOR", "초음파 센서" }, { "RADAR_UNIT", "레이더" },
        { "VIBRATION_SENSOR", "진동 센서" }, { "OPTICAL_FIBER_SENSOR", "광케이블 감지부" },
        { "THERMAL_CAMERA", "열영상 카메라" }, { "EO_CAMERA", "EO 카메라" }, { "THERMAL_SENSOR", "열화상 센서" },
        { "CONTACT_INPUT", "접점 입력" }, { "LAMP_LIGHT", "경광등" }, { "BUZZER", "부저" },
        { "AMPLIFIER", "앰프" }, { "MIC", "마이크" },
        { "PTZ_UNIT", "PTZ 구동부" }, { "IR_LED", "적외선 LED" }, { "WIPER", "와이퍼" }, { "OPTICAL_LENS", "광학 렌즈" }, { "TRACKER", "트래커" },
        { "NETWORK_INTERFACE", "네트워크 인터페이스" },
    };

    [Theory]
    [MemberData(nameof(ServerTypes))]
    public void should_name_every_server_catalog_type_in_korean_when_catalog_is_unavailable(string code, string korean)
        => Assert.Equal(korean, ComponentDisplay.TypeName(code));

    [Fact]
    public void should_cover_exactly_the_32_server_types_when_dictionary_is_enumerated()
        => Assert.Equal(32, ComponentDisplay.KnownTypes.Count());

    [Theory]
    [InlineData("IDLE", "대기")]
    [InlineData("RUNNING", "구동 중")]      // 서버 "동작 중" 이 아니라 콘솔 말투(스토리보드 §A)
    [InlineData("ON", "켜짐")]
    [InlineData("OFF", "꺼짐")]
    [InlineData("OPEN", "열림")]
    [InlineData("CLOSED", "닫힘")]
    [InlineData("LOCKED", "잠김")]
    [InlineData("UNLOCKED", "풀림")]
    [InlineData("ACTIVE", "추적 중")]
    [InlineData("LOST", "놓침")]
    [InlineData("running", "구동 중")]      // 대소문자 무관
    public void should_name_every_server_state_in_korean_when_state_is_known(string code, string korean)
        => Assert.Equal(korean, ComponentDisplay.StateName(code));

    [Fact]
    public void should_cover_exactly_the_10_server_states_when_dictionary_is_enumerated()
        => Assert.Equal(10, ComponentDisplay.KnownStates.Count());

    [Theory]
    [InlineData("SENSOR_TIMEOUT", "센서 응답 없음")]
    [InlineData("OVER_CURRENT", "과전류")]
    [InlineData("OVER_TEMP", "과열")]
    [InlineData("COMM_ERROR", "통신 오류")]
    [InlineData("POWER_LOSS", "전원 끊김")]
    [InlineData("ETC", "기타")]
    public void should_name_every_server_fault_reason_in_korean_when_reason_is_known(string code, string korean)
        => Assert.Equal(korean, ComponentDisplay.FaultName(code));

    [Fact]
    public void should_cover_exactly_the_6_server_fault_reasons_when_dictionary_is_enumerated()
        => Assert.Equal(6, ComponentDisplay.KnownFaultReasons.Count());

    [Theory]
    [InlineData("OK", ComponentHealthLevel.Ok, "정상")]
    [InlineData("DEGRADED", ComponentHealthLevel.Degraded, "저하")]
    [InlineData("FAULT", ComponentHealthLevel.Fault, "고장")]
    [InlineData("UNKNOWN", ComponentHealthLevel.Unknown, "미상")]
    [InlineData("fault", ComponentHealthLevel.Fault, "고장")]
    [InlineData(null, ComponentHealthLevel.Unknown, "미상")]
    [InlineData("", ComponentHealthLevel.Unknown, "미상")]
    public void should_parse_and_name_health_when_value_is_in_vocabulary_or_empty(string? raw, ComponentHealthLevel level, string korean)
    {
        Assert.Equal(level, ComponentDisplay.ParseHealth(raw));
        Assert.Equal(korean, ComponentDisplay.HealthText(raw));
    }

    [Fact]
    public void should_append_raw_code_in_parentheses_when_values_are_out_of_vocabulary()
    {
        Assert.Equal("미상 (BROKEN)", ComponentDisplay.HealthText("BROKEN"));
        Assert.Equal(ComponentHealthLevel.Unknown, ComponentDisplay.ParseHealth("BROKEN"));   // 거짓 고장을 만들지 않는다
        Assert.Equal("알 수 없음 (BLINKING)", ComponentDisplay.StateName("BLINKING"));
        Assert.Equal("알 수 없음 (MELTED)", ComponentDisplay.FaultName("MELTED"));
        Assert.Equal("NEW_PART", ComponentDisplay.TypeName("NEW_PART"));   // 유형 코드는 그대로 보인다(숨기지 않음)
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void should_return_null_when_state_or_reason_is_empty(string? raw)
    {
        Assert.Null(ComponentDisplay.StateName(raw));   // 상태 축이 없는 유형 — "꺼짐"이 아니다
        Assert.Null(ComponentDisplay.FaultName(raw));
    }

    [Theory]
    [InlineData("지붕 히터", "HEATER", "heater_1", "지붕 히터")]      // ① label
    [InlineData(null, "HEATER", "heater_1", "히터")]                   // ③ 내장 사전(카탈로그 없음)
    [InlineData(null, "NEW_PART", "part_x", "part_x")]                 // ④ key — 모르는 유형은 key 로
    [InlineData(null, null, "nic0", "nic0")]                           // 관측만 온 key
    [InlineData(null, "NEW_PART", null, "NEW_PART")]                  // key 도 없으면 코드
    [InlineData("  ", null, null, "부품")]
    public void should_follow_label_catalog_builtin_key_order_when_naming_component(string? label, string? type, string? key, string expected)
        => Assert.Equal(expected, ComponentDisplay.ComponentName(label, type, key));

    [Fact]
    public void should_prefer_catalog_korean_over_builtin_when_catalog_has_label()
    {
        var catalog = new FakeLabels(("HEATER", "함체 히터"), ("RADAR_UNIT", "RADAR_UNIT"));
        Assert.Equal("함체 히터", ComponentDisplay.ComponentName(null, "HEATER", "h1", catalog));
        Assert.Equal("레이더", ComponentDisplay.TypeName("RADAR_UNIT", catalog));   // 카탈로그 라벨 = 코드면 건너뛴다
        Assert.Equal("지붕 히터", ComponentDisplay.ComponentName("지붕 히터", "HEATER", "h1", catalog));   // label 이 이긴다
    }

    [Fact]
    public void should_fall_back_to_builtin_dictionary_when_catalog_throws()
        => Assert.Equal("팬", ComponentDisplay.TypeName("FAN", new ThrowingLabels()));

    [Theory]
    [InlineData(1, 1, 3, 0, 0, "고장 1 · 저하 1 · 정상 3")]
    [InlineData(0, 0, 5, 0, 0, "부품 이상 없음")]
    [InlineData(0, 0, 5, 0, 1, "부품 이상 없음 · 사용 안 함 1")]
    [InlineData(2, 0, 0, 0, 0, "고장 2")]
    [InlineData(0, 0, 2, 1, 0, "정상 2 · 미상 1")]                  // 미상이 있으면 "이상 없음"이라 하지 않는다
    [InlineData(0, 0, 0, 0, 2, "사용 안 함 2")]
    [InlineData(0, 0, 0, 0, 0, "부품 없음")]
    public void should_build_summary_line_when_counts_given(int fault, int degraded, int ok, int unknown, int off, string expected)
        => Assert.Equal(expected, ComponentDisplay.SummaryText(fault, degraded, ok, unknown, off));

    [Theory]
    [InlineData("OFF", true, "설정 켬 / 관측 꺼짐", true)]
    [InlineData("ON", false, "설정 끔 / 관측 켜짐", true)]
    [InlineData("ON", true, "켜짐", false)]
    [InlineData("OFF", false, "꺼짐", false)]
    [InlineData(null, true, "설정 켬 / 관측 없음", true)]
    [InlineData("ON", null, "켜짐", false)]
    [InlineData(null, null, "—", false)]
    [InlineData("RUNNING", true, "구동 중", false)]                 // ON/OFF 어휘가 아닌 상태는 비교하지 않는다
    public void should_show_intent_and_observation_side_by_side_when_they_differ(string? observed, bool? intent, string expected, bool mismatch)
    {
        Assert.Equal(expected, ComponentDisplay.StateWithIntent(observed, intent));
        Assert.Equal(mismatch, ComponentDisplay.IsIntentMismatch(observed, intent));
    }

    [Theory]
    [InlineData("ON", true)]
    [InlineData("RUNNING", true)]
    [InlineData("ACTIVE", true)]
    [InlineData("OFF", false)]
    [InlineData("IDLE", false)]
    [InlineData("OPEN", false)]
    [InlineData(null, false)]
    public void should_classify_active_state_when_state_given(string? state, bool active)
        => Assert.Equal(active, ComponentDisplay.IsActiveState(state));

    [Fact]
    public void should_show_time_only_when_observation_is_today_and_date_otherwise()
    {
        var at = new DateTimeOffset(2026, 10, 1, 9, 41, 7, TimeSpan.Zero);
        var iso = at.ToString("yyyy-MM-ddTHH:mm:ss.ffffffzzz", System.Globalization.CultureInfo.InvariantCulture);
        var local = at.ToLocalTime();
        Assert.Equal(local.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture), ComponentDisplay.ObservedTimeText(iso, local.Date));
        Assert.Equal(local.ToString("MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture), ComponentDisplay.ObservedTimeText(iso, local.Date.AddDays(1)));
        Assert.Equal("—", ComponentDisplay.ObservedTimeText(null, local.Date));
        Assert.Equal("어제쯤", ComponentDisplay.ObservedTimeText("어제쯤", local.Date));   // 못 읽으면 원문
    }

    [Theory]
    [InlineData(ComponentHealthLevel.Ok, true, ComponentHealthKind.Ok)]
    [InlineData(ComponentHealthLevel.Degraded, true, ComponentHealthKind.Warn)]
    [InlineData(ComponentHealthLevel.Fault, true, ComponentHealthKind.Crit)]
    [InlineData(ComponentHealthLevel.Unknown, true, ComponentHealthKind.Unknown)]
    [InlineData(ComponentHealthLevel.Fault, false, ComponentHealthKind.OutOfService)]
    public void should_pick_dot_kind_when_health_and_service_given(ComponentHealthLevel health, bool inService, ComponentHealthKind expected)
        => Assert.Equal(expected, ComponentDisplay.KindOf(health, inService));

    internal sealed class FakeLabels : IComponentTypeLabels
    {
        private readonly Dictionary<string, string> _labels;
        public FakeLabels(params (string Code, string Label)[] labels)
            => _labels = labels.ToDictionary(l => l.Code, l => l.Label, StringComparer.OrdinalIgnoreCase);
        public string? TypeLabel(string? type) => type != null && _labels.TryGetValue(type, out var label) ? label : null;
    }

    private sealed class ThrowingLabels : IComponentTypeLabels
    {
        public string? TypeLabel(string? type) => throw new InvalidOperationException("catalog down");
    }
}
