using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Model;

/// <summary>
/// 부품 <c>key</c> 의 규칙 — 형식 · 관례 이름 유도 · 충돌 회피(FR-05).
/// </summary>
/// <remarks>
/// key 는 <b>계약이 아니라 그 장비의 사실</b>이다(AS L217-226). 그래서 제안만 하고 현장이 고칠 수 있게 열어 둔다 —
/// 문 위치 부품의 관례 key 가 함체는 <c>door</c>, 통문은 <c>actuator</c> 로 갈린다.
/// </remarks>
public static class AssemblyKeyRules
{
    /// <summary>서버가 받는 형식. 어기면 422.</summary>
    public const string KeyPattern = "^[a-z][a-z0-9_]*$";

    private static readonly Regex _pattern = new(KeyPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// 목업 <c>keyFor()</c> 가 쓰는 관례 key 표(스토리보드 <c>CAT</c> 의 7번째 칸, 32종).
    /// 줄임말은 규칙으로 유도할 수 없어서(<c>TEMPERATURE_SENSOR → temp</c> · <c>CONTACT_INPUT → ci</c>) 목업 그대로 옮겼다.
    /// </summary>
    private static readonly Dictionary<string, string> _conventional = new(StringComparer.Ordinal)
    {
        ["DOOR_SENSOR"] = "door",
        ["DOOR_LOCK"] = "lock",
        ["DOOR_ACTUATOR"] = "actuator",
        ["LIMIT_SWITCH"] = "limit",
        ["TEMPERATURE_SENSOR"] = "temp",
        ["HUMIDITY_SENSOR"] = "humid",
        ["VOLTAGE_SENSOR"] = "volt",
        ["CURRENT_SENSOR"] = "curr",
        ["VIBRATION_METER"] = "vib_meter",
        ["UPS"] = "ups",
        ["HEATER"] = "heater",
        ["FAN"] = "fan",
        ["HEADLIGHT"] = "headlight",
        ["PIR_SENSOR"] = "pir",
        ["ULTRASONIC_SENSOR"] = "ultra",
        ["RADAR_UNIT"] = "radar",
        ["VIBRATION_SENSOR"] = "vib",
        ["OPTICAL_FIBER_SENSOR"] = "fiber",
        ["THERMAL_CAMERA"] = "thermal_cam",
        ["EO_CAMERA"] = "eo_cam",
        ["THERMAL_SENSOR"] = "thermal",
        ["CONTACT_INPUT"] = "ci",
        ["LAMP_LIGHT"] = "light",
        ["BUZZER"] = "buzzer",
        ["AMPLIFIER"] = "amp",
        ["MIC"] = "mic",
        ["PTZ_UNIT"] = "ptz",
        ["IR_LED"] = "ir",
        ["WIPER"] = "wiper",
        ["OPTICAL_LENS"] = "lens",
        ["TRACKER"] = "tracker",
        ["NETWORK_INTERFACE"] = "nic",
    };

    /// <summary>표에 없는 유형(카탈로그가 넓어졌을 때)에서 떼는 꼬리. 표에 있는 것은 표가 이긴다.</summary>
    private static readonly string[] _suffixes = { "_SENSOR", "_INPUT", "_OUTPUT", "_UNIT", "_MODULE" };

    private const string Fallback = "part";

    /// <summary>형식 검사. null = 통과.</summary>
    public static string? ValidateFormat(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "key 가 비어 있다";
        return _pattern.IsMatch(key) ? null : "key 는 소문자로 시작하고 소문자 · 숫자 · 밑줄만 쓴다";
    }

    /// <summary>
    /// 유형 코드에서 관례 key 를 유도한다. 목업 표에 있으면 그 값을 그대로 쓰고(정본),
    /// 없으면 꼬리(<c>_SENSOR</c> · <c>_INPUT</c> · <c>_OUTPUT</c> · <c>_UNIT</c> · <c>_MODULE</c>)를 떼고 소문자로 내린다.
    /// </summary>
    public static string BaseKeyFor(string typeCode)
    {
        var code = (typeCode ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length == 0) return Fallback;
        if (_conventional.TryGetValue(code, out var known)) return known;

        foreach (var suffix in _suffixes)
        {
            if (code.Length > suffix.Length && code.EndsWith(suffix, StringComparison.Ordinal))
            {
                code = code[..^suffix.Length];
                break;
            }
        }

        return Sanitize(code);
    }

    /// <summary><see cref="BaseKeyFor"/>, 이미 쓰고 있으면 <c>_2</c> · <c>_3</c> … 첫 빈자리(목업 <c>keyFor()</c>).</summary>
    public static string Suggest(string typeCode, IEnumerable<string> existingKeys)
    {
        var taken = new HashSet<string>(existingKeys?.Where(x => x is not null) ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
        var b = BaseKeyFor(typeCode);
        if (!taken.Contains(b)) return b;

        for (var i = 2; ; i++)
        {
            var candidate = $"{b}_{i}";
            if (!taken.Contains(candidate)) return candidate;
        }
    }

    /// <summary>형식에 맞게 깎는다 — 못 쓰는 글자는 밑줄, 앞은 소문자로 시작하게.</summary>
    private static string Sanitize(string code)
    {
        var chars = code.ToLowerInvariant()
                        .Select(ch => (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') ? ch : '_')
                        .ToArray();
        var text = new string(chars).TrimStart('_', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        return text.Length > 0 && _pattern.IsMatch(text) ? text : Fallback;
    }
}
