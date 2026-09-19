using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using System;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Catalog;

/// <summary>
/// 부품 유형 코드 → <see cref="ComponentFamily"/>(= 블록 <b>형태</b>) 규칙표(FR-04).
/// </summary>
/// <remarks>
/// <para><b>왜 표인가</b> — 부품 유형은 <b>DB 카탈로그</b>라 서버 배포 없이 늘어난다
/// (<c>GET /api/devices/spec</c>). 32종을 코드에 그대로 박으면 33번째가 들어오는 날 모양이 사라진다.
/// 그래서 <b>낱개 코드가 아니라 규칙</b>을 적는다 — 아는 코드는 정확히 집고, 모르는 코드는
/// 이름의 낱말(<c>*_SENSOR</c> · <c>*ACTUATOR*</c> …)로 가장 그럴듯한 가족에 떨어진다.</para>
/// <para><b>순서 계약</b> — 위에서 아래로 훑어 <b>처음 맞는 줄이 이긴다</b>. 정확 일치 줄이 전부 앞에 오고
/// 낱말 규칙이 뒤에 온다(정확 일치가 가장 구체적이므로 규칙에 가려질 일이 없다).
/// 비교는 <b>대문자로 정규화한 코드</b>에 대해 한다.</para>
/// <para><b>가족을 가르는 기준</b>(색이 아니라 형태다 — 드래그 규칙 §시각 피드백):</para>
/// <list type="bullet">
///   <item><b>감지</b>(둥근 칩 + 왼쪽 위 홈) — 바깥을 <b>읽는</b> 것. 접점 입력 · 리미트 스위치 · 마이크도 여기다.</item>
///   <item><b>구동</b>(네모 + 오른쪽 아래 삼각) — 바깥으로 <b>내보내는</b> 것. 잠금 · 문 구동 · 경광등 · 부저 · 앰프.</item>
///   <item><b>전원 · 환경</b>(육각) — 함체의 <b>살림</b>. 전원(UPS · 전압 · 전류)과 온습도 · 히터 · 팬은
///         한 덩어리로 읽히는 편이 낫다(히터/팬은 명령을 받지만 <b>환경 세트</b>로 묶는다 — 의도된 선택).</item>
///   <item><b>광학</b>(둥근 칩 + 원) — 광축 위의 것. 영상부 · 렌즈 · 조명(IR · 전조등) · 와이퍼 · PTZ · 트래커.</item>
///   <item><b>네트워크</b>(마름모) — 링크. 보드에서 <b>늘 맨 끝</b>이다.</item>
///   <item><b>기타</b>(둥근 칩) — 규칙에 걸리지 않은 것.</item>
/// </list>
/// </remarks>
public static class ComponentFamilyRules
{
    #region - Rule table -
    // ── 정확 일치 (카탈로그 8.0.1 의 32종 중 낱말 규칙으로는 못 가르는 것) ──────────────────
    private static readonly (string Pattern, ComponentFamily Family)[] _rules =
    {
        // 전원 · 환경 — '…_SENSOR' 지만 함체 살림이다. 반드시 아래 *_SENSOR 규칙보다 앞.
        ("UPS",                 ComponentFamily.PowerEnvironment),
        ("HEATER",              ComponentFamily.PowerEnvironment),
        ("FAN",                 ComponentFamily.PowerEnvironment),
        ("TEMPERATURE_SENSOR",  ComponentFamily.PowerEnvironment),
        ("HUMIDITY_SENSOR",     ComponentFamily.PowerEnvironment),
        ("VOLTAGE_SENSOR",      ComponentFamily.PowerEnvironment),
        ("CURRENT_SENSOR",      ComponentFamily.PowerEnvironment),

        // 광학 — 광축 위의 것.
        ("HEADLIGHT",           ComponentFamily.Optics),   // 조명(LAMP_LIGHT 와 낱말이 겹쳐 정확 일치로 가른다)
        ("IR_LED",              ComponentFamily.Optics),
        ("WIPER",               ComponentFamily.Optics),
        ("PTZ_UNIT",            ComponentFamily.Optics),
        ("TRACKER",             ComponentFamily.Optics),

        // 구동 — 바깥으로 내보내는 것.
        ("DOOR_LOCK",           ComponentFamily.Actuation),
        ("LAMP_LIGHT",          ComponentFamily.Actuation),
        ("BUZZER",              ComponentFamily.Actuation),
        ("AMPLIFIER",           ComponentFamily.Actuation),

        // 감지 — 바깥을 읽는 것.
        ("MIC",                 ComponentFamily.Sensing),  // 앰프(구동)와 짝이지만 마이크는 '받는' 쪽이다
        ("CONTACT_INPUT",       ComponentFamily.Sensing),
        ("RADAR_UNIT",          ComponentFamily.Sensing),

        // ── 낱말 규칙 (카탈로그가 늘어나도 모양이 사라지지 않게) ───────────────────────────
        ("*NETWORK*",           ComponentFamily.Network),  // 링크는 가장 먼저 — 'NETWORK_SWITCH' 가 스위치로 새지 않게

        ("*CAMERA*",            ComponentFamily.Optics),
        ("*LENS*",              ComponentFamily.Optics),

        ("*ACTUATOR*",          ComponentFamily.Actuation),
        ("*RELAY*",             ComponentFamily.Actuation),
        ("*MOTOR*",             ComponentFamily.Actuation),
        ("*OUTPUT*",            ComponentFamily.Actuation),

        ("*BATTERY*",           ComponentFamily.PowerEnvironment),
        ("*POWER*",             ComponentFamily.PowerEnvironment),

        ("*SWITCH*",            ComponentFamily.Sensing),
        ("*DETECTOR*",          ComponentFamily.Sensing),
        ("*_SENSOR",            ComponentFamily.Sensing),
        ("*_METER",             ComponentFamily.Sensing),
    };
    #endregion

    /// <summary>규칙표 원본(설명 + 가족). 문서 · 테스트가 표 자체를 단언할 수 있게 연다.</summary>
    public static IReadOnlyList<(string Pattern, ComponentFamily Family)> Rules => _rules;

    /// <summary>
    /// 유형 코드의 가족. 모르는 코드 · <c>null</c> · 빈 값은 <see cref="ComponentFamily.Other"/>.
    /// 대소문자를 가리지 않는다(대문자로 정규화한 뒤 비교).
    /// </summary>
    public static ComponentFamily FamilyOf(string? typeCode)
    {
        var code = typeCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code)) return ComponentFamily.Other;

        foreach (var (pattern, family) in _rules)
        {
            if (Matches(code, pattern)) return family;
        }
        return ComponentFamily.Other;
    }

    /// <summary>
    /// 팔레트 · 보드에서의 가족 정렬 순서. <b>네트워크가 늘 맨 끝</b>이다(목업 규약 — 링크는 마지막 줄).
    /// </summary>
    public static int PaletteOrder(ComponentFamily family) => family switch
    {
        ComponentFamily.Sensing => 0,
        ComponentFamily.Actuation => 1,
        ComponentFamily.PowerEnvironment => 2,
        ComponentFamily.Optics => 3,
        ComponentFamily.Other => 4,
        ComponentFamily.Network => 5,   // 늘 맨 끝
        _ => 4,
    };

    /// <summary><c>*X*</c>(포함) · <c>*X</c>(끝) · <c>X*</c>(시작) · <c>X</c>(정확). 이미 대문자로 정규화된 코드에만 쓴다.</summary>
    private static bool Matches(string code, string pattern)
    {
        if (string.IsNullOrEmpty(pattern) || pattern == "*") return false;

        var head = pattern[0] == '*';
        var tail = pattern[^1] == '*';

        if (head && tail) return code.Contains(pattern[1..^1], StringComparison.Ordinal);
        if (head) return code.EndsWith(pattern[1..], StringComparison.Ordinal);
        if (tail) return code.StartsWith(pattern[..^1], StringComparison.Ordinal);
        return string.Equals(code, pattern, StringComparison.Ordinal);
    }
}
