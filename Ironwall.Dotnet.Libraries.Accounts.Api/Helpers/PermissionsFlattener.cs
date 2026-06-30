using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;

/// <summary>
/// 서버 permissions(3원 drift, §2.3.2)를 <c>"{module}:{verb}"</c> 토큰 리스트로 평탄화한다.
/// flat-string(<c>{"events":"rw"}</c> → events:r, events:w) 과 nested(<c>{"events":{"view":true}}</c> → events:view) 양쪽 수용,
/// 미인식 형태는 무시(권한없음 안전기본). 결과는 <see cref="IPermissionService"/> 류의 contains 판정에 사용. — GOP-00 PRD FR-1/FR-8
/// </summary>
public static class PermissionsFlattener
{
    public static IReadOnlyList<string> Flatten(JObject? permissions)
    {
        if (permissions is null) return Array.Empty<string>();

        // ★ 실서버 형식은 {"modules": {"events": {...}}, "device_groups": [...]} envelope (§2.3.2).
        //   envelope 이면 modules 안을 평탄화한다(이전엔 "modules"를 모듈로 오인해 토큰 0개 → 비ADMIN 전부 차단 버그).
        //   envelope 없는 평탄형식({"events":"rw"} / {"events":{"view":true}})도 그대로 수용(하위호환).
        var source = permissions["modules"] is JObject modulesObj ? modulesObj : permissions;

        var result = new List<string>();
        foreach (var prop in source.Properties())
        {
            var module = prop.Name;
            var value = prop.Value;

            if (value.Type == JTokenType.String)
            {
                var verbs = value.Value<string>() ?? string.Empty;   // "rw" → 'r','w'
                foreach (var verb in verbs)
                    result.Add($"{module}:{verb}");
            }
            else if (value is JObject nested)
            {
                foreach (var v in nested.Properties())
                    if (v.Value.Type == JTokenType.Boolean && v.Value.Value<bool>())
                        result.Add($"{module}:{v.Name}");
            }
        }
        return result;
    }
}
