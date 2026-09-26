using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Utils.Converters;
using System;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
/****************************************************************************
   Purpose      : 장비 콘솔 전역의 enum·코드값 → 한글 표시 라벨 단일 정본(device-console enum-korean-consistency).
                  그리드 · 상세 폼 콤보 · 부품 필터 칩 · 셋업/결선맵이 전부 이 표 하나를 함께 읽는다 —
                  같은 개념(운영 상태 · 장비 종류 · 부품 건강)이 화면마다 다른 말이나 raw 영문 이름으로
                  갈라지지 않게 한다.
   ⚠ 표시 계층 전용   : enum 정의/DTO/NATS/서버 전송 값은 절대 바꾸지 않는다. 매핑에 없는 값은
                  원문(코드 · ToString())을 그대로 보존한다 — 모르는 값을 기본값으로 밀어 넣으면
                  억제 범위가 조용히 넓어지는 것과 같은 종류의 사고다(가공하지 말고 드러낸다).
   Created By   : GHLee
   Created On   : 9/23/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 장비 콘솔(Devices.Ui) 전역에서 쓰는 enum·코드값 한글 표시 헬퍼.
/// </summary>
/// <remarks>
/// <para><b>왜 <see cref="UiKoreanMap"/>(Utils) 를 그대로 재사용하지 않는 값이 있는가</b> —
/// <see cref="EnumDeviceStatus"/> 는 GMaps 심볼 팝업(<c>UiKoreanMap</c>)과 장비 콘솔 그리드가
/// 이미 서로 다른 말을 쓰고 있었다(정상/장애/비활성 vs 운영/오류/중지 — 목업
/// <c>docs/design/window-layout-system-storyboard.html</c> <c>DEV_ST</c> 표가 정본).
/// 콘솔 화면끼리(그리드 · 상세 폼 · 카테고리)는 하나로 모으되, 맵 팝업의 기존 말은 이 정리 범위 밖이라
/// 건드리지 않는다 — 대신 콘솔 쪽 정본은 여기 하나로만 둔다(<see cref="StatusKorean"/>).</para>
/// <para><see cref="EnumDeviceType"/>·<see cref="EnumCameraType"/>·<see cref="EnumCameraMode"/> 는
/// 다른 화면(맵 팝업)과 갈릴 이유가 없는 값이라 <see cref="UiKoreanMap"/> 을 그대로 부른다 —
/// 세 번째 사본을 만들지 않는다.</para>
/// </remarks>
public static class DeviceEnumDisplay
{
    // ── 장비 운영 상태 — 콘솔 그리드 "상태" 컬럼(구 BaseDeviceViewModel.StatusDisplay)의 정본 ──
    private static readonly IReadOnlyDictionary<EnumDeviceStatus, string> _status = new Dictionary<EnumDeviceStatus, string>
    {
        [EnumDeviceStatus.ACTIVATED]   = "운영",
        [EnumDeviceStatus.ERROR]       = "오류",
        [EnumDeviceStatus.DEACTIVATED] = "중지",
    };

    /// <summary>못 알아보는 값은 원문 이름을 그대로 보인다(하네스가 조용히 빈칸을 내지 않게).</summary>
    public static string StatusKorean(EnumDeviceStatus status)
        => _status.TryGetValue(status, out var s) ? s : status.ToString();

    // ── 장비 카테고리 — 상세 폼 읽기 전용 "카테고리" 칸의 정본(ByComponentRowViewModel.CategoryLabels 와 같은 말) ──
    private static readonly IReadOnlyDictionary<EnumDeviceCategory, string> _category = new Dictionary<EnumDeviceCategory, string>
    {
        [EnumDeviceCategory.None]       = "없음",
        [EnumDeviceCategory.Controller] = "제어기",
        [EnumDeviceCategory.Sensor]     = "센서",
        [EnumDeviceCategory.Camera]     = "카메라",
        [EnumDeviceCategory.Speaker]    = "스피커",
        [EnumDeviceCategory.Enclosure]  = "함체",
        [EnumDeviceCategory.Lamp]       = "경광등",
        [EnumDeviceCategory.Etc]        = "기타",
        [EnumDeviceCategory.Gate]       = "통문",
    };

    public static string CategoryKorean(EnumDeviceCategory category)
        => _category.TryGetValue(category, out var s) ? s : category.ToString();

    // ── 부품 건강 — "부품으로 찾기" 결과 행 배지 · 상세의 부품 상태 줄의 정본(목업 HEALTH 표: 정상 · 저하 · 고장 · 미상) ──
    private static readonly IReadOnlyDictionary<string, string> _componentHealth = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["OK"]       = "정상",
        ["DEGRADED"] = "저하",
        ["FAULT"]    = "고장",
        ["UNKNOWN"]  = "미상",
    };

    /// <summary>서버 건강 코드(대소문자 무관) → 한글. 빈 값은 "미상", 어휘 밖 값은 <see cref="UnknownValue"/>.</summary>
    public static string ComponentHealthKorean(string? health)
    {
        var trimmed = health?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return "미상";
        return _componentHealth.TryGetValue(trimmed, out var s) ? s : UnknownValue;
    }

    /// <summary>표시 사전에 없는 값의 화면 글 — 원문은 툴팁 · 로그로만 보인다(운영자 화면에 영문 코드를 내지 않는다).</summary>
    public const string UnknownValue = "알 수 없음";

    // ── 문 · 통문 위치(부품 state) — DOOR_SENSOR(OPEN/CLOSED) · DOOR_ACTUATOR(OPEN/CLOSED/RUNNING) ──
    private static readonly IReadOnlyDictionary<string, string> _doorState = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["OPEN"]    = "열림",
        ["CLOSED"]  = "닫힘",
        ["RUNNING"] = "구동 중",
        ["ON"]      = "켜짐",
        ["OFF"]     = "꺼짐",
    };

    /// <summary>문 · 부품 동작 상태 코드 → 한글. 빈 값은 "미상", 모르는 값은 <see cref="UnknownValue"/>.</summary>
    public static string DoorStateKorean(string? state)
    {
        var trimmed = state?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return "미상";
        return _doorState.TryGetValue(trimmed, out var s) ? s : UnknownValue;
    }

    // ── 종류축(type_<category>) — 목업 AX 표. 서버 카탈로그 라벨이 코드와 같을 때(한국어 라벨이 없을 때) 쓴다 ──
    private static readonly IReadOnlyDictionary<string, string> _typeAxis = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // 제어기
        ["Controller"] = "일반", ["SmartController"] = "스마트", ["IoController"] = "IO",
        // 센서
        ["Multi"] = "복합", ["Fence"] = "펜스", ["Underground"] = "지중", ["Contact"] = "접점", ["PIR"] = "PIR",
        ["Laser"] = "레이저", ["Radar"] = "레이더", ["OpticalCable"] = "광케이블", ["SmartSensor"] = "스마트",
        ["SmartSensor2"] = "스마트2", ["SmartCompound"] = "스마트복합", ["SmartMultisensor2"] = "스마트멀티2",
        // 카메라
        ["FIXED"] = "고정", ["PTZ"] = "PTZ", ["SPEED_DOME"] = "스피드돔",
        // 스피커 · 함체 · 경광등 · 통문(형상 축의 Unknown = "등록됐으나 현장 미확인")
        ["Horn"] = "혼", ["Pillar"] = "컬럼", ["Outdoor"] = "옥외", ["Indoor"] = "옥내",
        ["Beacon"] = "회전", ["Strobe"] = "점멸", ["LedBar"] = "LED바",
        ["Sliding"] = "슬라이딩", ["Swing"] = "스윙", ["Barrier"] = "바리어",
        ["Unknown"] = "미지정",
    };

    /// <summary>
    /// 종류축 값의 화면 글. 서버 카탈로그가 한국어 라벨을 주면(<paramref name="serverLabel"/> ≠ 코드) 그것을, 아니면 이 표를,
    /// 둘 다 없으면 서버 라벨(=코드) 그대로 돌려준다 — 카탈로그에 있는 값을 "알 수 없음"으로 뭉개지 않는다.
    /// </summary>
    public static string TypeAxisKorean(string code, string? serverLabel = null)
    {
        var trimmed = code?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(serverLabel) && !string.Equals(serverLabel.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
            return serverLabel.Trim();
        return _typeAxis.TryGetValue(trimmed, out var s) ? s : (serverLabel?.Trim() is { Length: > 0 } label ? label : trimmed);
    }

    // ── 스피커 역할(speaker_role) — 목업 AX.speaker.x 표 ──
    private static readonly IReadOnlyDictionary<string, string> _speakerRole = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["NORMAL"] = "일반", ["ADMIN"] = "관리", ["MONITOR"] = "감시", ["DEV"] = "개발",
    };

    /// <summary>스피커 역할 코드 → 한국어. 서버 라벨이 코드와 다르면(한국어 라벨) 그것을 쓴다.</summary>
    public static string SpeakerRoleKorean(string code, string? serverLabel = null)
    {
        var trimmed = code?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(serverLabel) && !string.Equals(serverLabel.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
            return serverLabel.Trim();
        return _speakerRole.TryGetValue(trimmed, out var s) ? s : (string.IsNullOrEmpty(trimmed) ? string.Empty : UnknownValue);
    }

    // ── 접속 방식(connection.type) — 목업 CONN_T 표(EnumConnectionType 7값, 엄격) ──
    public static readonly IReadOnlyList<(string Code, string Display)> ConnectionTypes = new[]
    {
        ("IP_DIRECT", "IP 직결"), ("IP_CONVERTER", "IP 변환기"), ("CONTROLLER_CONTACT", "제어기 접점"),
        ("RS485", "RS485"), ("ENCLOSURE_CONTACT", "함체 접점"), ("SERVER_MANAGED", "서버 관리"), ("NONE", "없음"),
    };

    // ── 카메라 제어 프로토콜(connection.protocol, 카메라 필수) — 목업 CAM_P 표 ──
    public static readonly IReadOnlyList<(string Code, string Display)> CameraProtocols = new[]
    {
        ("NONE", "없음"), ("ONVIF", "ONVIF"), ("EMSTONE_API", "엠스톤"), ("INNODEP_API", "이노뎁"), ("ETC", "기타"),
    };

    // ── 카메라 동작 모드(device_config.modes) — 서버 CAMERA_MODE_ENUMS 어휘. 첫 항목(빈 코드)은 "지정 안 함" = 키 삭제 ──
    public const string NotSetDisplay = "지정 안 함";

    public static readonly IReadOnlyList<(string Code, string Display)> WeatherModes = new[]
    {
        ("", NotSetDisplay), ("NORMAL", "평시"), ("FOG", "안개"), ("SEA_FOG", "해무"), ("YELLOW_DUST", "황사"), ("RAIN", "강우"), ("SNOW", "강설"),
    };

    public static readonly IReadOnlyList<(string Code, string Display)> CameraVideoModes = new[]
    {
        ("", NotSetDisplay), ("NORMAL", "보통"), ("STABILIZATION", "흔들림 보정"), ("BLC", "역광 보정"), ("NIGHT_ENHANCE", "야간 영상 개선"),
    };

    public static readonly IReadOnlyList<(string Code, string Display)> DayNightModes = new[]
    {
        ("", NotSetDisplay), ("AUTO", "자동"), ("DAY", "주간"), ("NIGHT", "야간"),
    };

    public static readonly IReadOnlyList<(string Code, string Display)> AutoManualModes = new[]
    {
        ("", NotSetDisplay), ("AUTO", "자동"), ("MANUAL", "수동"),
    };

    public static readonly IReadOnlyList<(string Code, string Display)> Palettes = new[]
    {
        ("", NotSetDisplay), ("WHITE_HOT", "백색 열상"), ("BLACK_HOT", "흑색 열상"), ("RAINBOW", "무지개"), ("IRONBOW", "철 색상"),
    };

    /// <summary>고정 표시 사전에서 코드의 화면 글을 찾는다. 없으면 <see cref="UnknownValue"/>, 빈 코드는 빈 글.</summary>
    public static string FixedKorean(IReadOnlyList<(string Code, string Display)> table, string? code)
    {
        var trimmed = code?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return string.Empty;
        foreach (var (c, d) in table)
            if (string.Equals(c, trimmed, StringComparison.OrdinalIgnoreCase)) return d;
        return UnknownValue;
    }

    /// <summary>enum 값 → 한글. 타입별 정본 표로 나눠 읽는다(이 파일 remarks 참조).</summary>
    public static string KoreanOf(Enum value) => value switch
    {
        EnumDeviceStatus v   => StatusKorean(v),
        EnumDeviceCategory v => CategoryKorean(v),
        EnumDeviceType v     => UiKoreanMap.To(v),
        EnumCameraType v     => UiKoreanMap.To(v),
        EnumCameraMode v     => UiKoreanMap.To(v),
        _                    => value.ToString(),
    };

    /// <summary>
    /// "한국어 (코드)" 병기 — <c>CatalogOption.Display</c>(device-console-v8 목업 규칙: 라벨+코드 병기,
    /// 같으면 코드 숨김)와 정확히 같은 규칙. 매핑을 못 찾아 한글=코드가 같아지면 코드를 또 보이지 않는다.
    /// </summary>
    public static string Bilingual(string korean, string code)
        => string.IsNullOrEmpty(code) || string.Equals(korean, code, StringComparison.Ordinal)
            ? korean
            : $"{korean} ({code})";

    /// <summary>
    /// CLR enum 콤보(<c>DevicePropertyFormViewModel.ResolveOptions</c> 의 <c>ClrEnum</c> 선택지)의 화면 글 — <b>한국어만</b>.
    /// 저장 값(코드)은 선택지의 Text 가 따로 쥔다. 한국어가 없는 값은 원문 이름 그대로(지어내지 않는다).
    /// </summary>
    public static string EnumBilingual(Enum value) => KoreanOf(value);

    /// <summary>
    /// 셋업·결선맵 "종류" 열·콤보의 센서 종류 코드 → "한국어 (코드)". 레거시(6.3) 코드는
    /// <see cref="EnumDeviceType"/> 이름과 같아 그 표를 빌리고, v7.0+ 카탈로그 코드처럼 enum 이 모르는
    /// 값은 원문 그대로 보인다(지어내지 않는다 — 서버가 모르는 종류를 보내면 422 이고, 그 422 가 진단 정보다).
    /// </summary>
    public static string SensorTypeBilingual(string? code)
    {
        var trimmed = code?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return code ?? string.Empty;

        return Enum.TryParse<EnumDeviceType>(trimmed, ignoreCase: true, out var type) && Enum.IsDefined(type)
            ? Bilingual(UiKoreanMap.To(type), trimmed)
            : trimmed;
    }

    /// <summary>
    /// <see cref="SensorTypeBilingual"/> 의 역방향 — "한국어 (코드)" 에서 코드만 되돌린다. 편집형
    /// 콤보(<c>WiringView.xaml</c> "종류" 열·콤보)는 사람이 드롭다운에서 병기 표시를 고르든, 칸에 코드를
    /// 직접 새로 타이핑하든 <b>둘 다</b> 같은 칸에 떨어진다 — 병기 형식이 아니면(직접 타이핑) 입력을
    /// 그대로 코드로 받아들인다(지어내지 않는다).
    /// </summary>
    public static string ExtractSensorTypeCode(string? display)
    {
        var trimmed = display?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return trimmed ?? string.Empty;

        var openIndex = trimmed.LastIndexOf(" (", StringComparison.Ordinal);
        if (openIndex > 0 && trimmed.EndsWith(")", StringComparison.Ordinal))
        {
            var code = trimmed.Substring(openIndex + 2, trimmed.Length - openIndex - 3);
            if (!string.IsNullOrWhiteSpace(code)) return code;
        }

        return trimmed;
    }
}
