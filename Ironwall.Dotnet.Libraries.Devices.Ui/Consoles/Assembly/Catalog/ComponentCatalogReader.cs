using Ironwall.Dotnet.Libraries.Devices.Api.Models;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Catalog;

/// <summary>
/// 카탈로그 한 행(<see cref="VocabularyEntryDto"/> · <c>component_type</c>) → <see cref="ComponentTypeInfo"/>.
/// <b>순수 함수</b>다 — 서버도 화면도 모른다.
/// </summary>
/// <remarks>
/// <para><b>절대 던지지 않는다.</b> <c>definition</c> 은 유형마다 키가 다르고 서버가 계속 늘리는 자리라
/// (<c>DeviceSpecCatalogDto</c> 주석) 모양을 단정할 수 없다. 한 줄이 이상하다고 팔레트 전체가 비면
/// "부품이 하나도 없다"로 보인다 — 이상한 줄은 <b>빈 목록</b>으로 접고 나머지는 그린다.</para>
/// <para><b>읽는 것만</b> 옮긴다. <c>states</c> · <c>commands</c> · <c>produces</c> 는 <b>유형 공통 사실</b>이라
/// 요청에 실으면 422 다 — 이 타입은 <b>보여 주기 전용</b>이다.</para>
/// </remarks>
public static class ComponentCatalogReader
{
    /// <summary><c>definition.states</c> — 상태 축(없으면 동작 상태를 보고하지 못한다).</summary>
    public const string DEF_STATES = "states";
    /// <summary><c>definition.commands</c> — 받을 수 있는 명령.</summary>
    public const string DEF_COMMANDS = "commands";
    /// <summary><c>definition.produces</c> — 이 부품이 내는 메트릭 키.</summary>
    public const string DEF_PRODUCES = "produces";
    /// <summary><c>definition.override_params</c> — <c>device_config.component_overrides.&lt;key&gt;</c> 로 덮을 수 있는 설정 이름.</summary>
    public const string DEF_OVERRIDE_PARAMS = "override_params";

    /// <summary>
    /// 카탈로그 한 행을 읽는다. 코드는 <b>대문자로 정규화</b>하고, 라벨이 비면 코드를 쓴다.
    /// <c>definition</c> 이 없거나 이상하면 목록은 빈 채로 둔다. 가족은 <see cref="ComponentFamilyRules"/> 가 정한다.
    /// </summary>
    public static ComponentTypeInfo Read(VocabularyEntryDto entry)
    {
        // 널도 삼킨다 — 서버 배열에 null 한 칸이 섞여도 팔레트가 죽지 않게.
        if (entry is null)
            return new ComponentTypeInfo { Code = string.Empty, Label = string.Empty, Family = ComponentFamily.Other };

        var code = (entry.Code ?? string.Empty).Trim().ToUpperInvariant();
        var label = string.IsNullOrWhiteSpace(entry.Label) ? code : entry.Label.Trim();

        JObject? definition = null;
        try { definition = entry.Definition; } catch { /* DTO 가 이상해도 나머지는 읽는다 */ }

        return new ComponentTypeInfo
        {
            Code = code,
            Label = label,
            Family = ComponentFamilyRules.FamilyOf(code),
            AppliesTo = ReadCategories(entry.AppliesTo),
            States = ReadNames(definition, DEF_STATES),
            Commands = ReadNames(definition, DEF_COMMANDS),
            Produces = ReadNames(definition, DEF_PRODUCES),
            OverrideParams = ReadNames(definition, DEF_OVERRIDE_PARAMS),
            IsDeprecated = !string.IsNullOrWhiteSpace(entry.DeprecatedAt),
        };
    }

    /// <summary>
    /// <c>definition[name]</c> 의 이름 목록을 읽는다. 모양을 가리지 않는다 —
    /// 문자열 배열 · <c>{code}</c>/<c>{name}</c> 객체 배열 · <b>이름을 키로 삼는 사전</b>(<c>override_params</c> 의 실제 모양) ·
    /// 문자열 하나 · 없음 · 엉뚱한 타입 전부 받는다.
    /// 무엇도 못 읽으면 <b>빈 목록</b>(절대 <c>null</c> 도 예외도 아니다).
    /// </summary>
    public static IReadOnlyList<string> ReadNames(JObject? definition, string name)
    {
        if (definition is null || string.IsNullOrWhiteSpace(name)) return Array.Empty<string>();

        try
        {
            var key = name.Trim();
            var token = definition[key] ?? FindCaseInsensitive(definition, key);
            if (token is null) return Array.Empty<string>();

            var sink = new List<string>();
            Collect(token, sink, depth: 0);
            return sink.Count == 0 ? Array.Empty<string>() : sink;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    #region - Processes -
    private static JToken? FindCaseInsensitive(JObject definition, string key)
    {
        foreach (var property in definition.Properties())
        {
            if (string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase)) return property.Value;
        }
        return null;
    }

    /// <summary>
    /// 배열 · 객체 · 문자열을 한 목록으로 편다. <b>한 겹까지만</b> 편다 —
    /// <c>[[["A"]]]</c> 같은 중첩은 이름 목록이 아니라 <b>망가진 응답</b>이라 받아들이지 않는다.
    /// 숫자 · 불리언도 이름이 될 수 없으므로 버린다(<c>"true"</c> 가 상태 이름으로 새지 않게).
    /// </summary>
    private static void Collect(JToken? token, List<string> sink, int depth)
    {
        if (token is null || depth > 1 || sink.Count > 256) return;

        switch (token.Type)
        {
            case JTokenType.Null:
            case JTokenType.Undefined:
                return;

            case JTokenType.Array:
                foreach (var child in token.Children()) Collect(child, sink, depth + 1);
                return;

            case JTokenType.Object:
                {
                    var holder = (JObject)token;
                    var text = PickString(holder, "code")
                            ?? PickString(holder, "name")
                            ?? PickString(holder, "key")
                            ?? PickString(holder, "value");
                    if (text != null)
                    {
                        sink.Add(text);
                        return;
                    }

                    // 이름을 키로 삼는 사전 — 서버 8.x 의 override_params 가 이 모양이다
                    // (component_definition.py: dict[str, ParameterDefinition], 실측 /devices/spec
                    // "override_params": {"enabled": {"value_type": "bool"}}). 한 겹째(정의 칸 그 자체)에서만
                    // 키를 이름으로 읽는다 — 배열 속 객체는 위의 {code}/{name} 규칙만 따른다.
                    if (depth == 0)
                    {
                        foreach (var property in holder.Properties())
                        {
                            if (sink.Count > 256) break;
                            if (!string.IsNullOrWhiteSpace(property.Name)) sink.Add(property.Name.Trim());
                        }
                    }
                    return;
                }

            case JTokenType.String:
                {
                    var text = token.Value<string>();
                    if (!string.IsNullOrWhiteSpace(text)) sink.Add(text!.Trim());
                    return;
                }

            default:
                return;     // 숫자 · 불리언 · 날짜 … 이름이 아니다
        }
    }

    private static string? PickString(JObject holder, string property)
    {
        var token = holder[property] ?? FindCaseInsensitive(holder, property);
        if (token is null || token.Type != JTokenType.String) return null;
        var text = token.Value<string>();
        return string.IsNullOrWhiteSpace(text) ? null : text!.Trim();
    }

    /// <summary>
    /// <c>applies_to</c> → 카테고리 집합. 모르는 이름은 버리고, <b>없거나 비면 <c>null</c></b>(= 전 카테고리).
    /// </summary>
    private static IReadOnlyCollection<EnumDeviceCategory>? ReadCategories(IEnumerable<string>? names)
    {
        if (names is null) return null;

        HashSet<EnumDeviceCategory> set;
        try
        {
            set = names
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => DeviceTypeResolver.ParseCategory(n.Trim()))
                .Where(c => c != EnumDeviceCategory.None)
                .ToHashSet();
        }
        catch
        {
            return null;
        }

        return set.Count == 0 ? null : set;
    }
    #endregion
}
