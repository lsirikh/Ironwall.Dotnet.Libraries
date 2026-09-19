using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Presets;
/****************************************************************************
   Purpose      : 프리셋이 담아도 되는 것만 남긴다 + 담으면 422 나는 것을 찾아 준다.
   Created By   : GHLee
   Created On   : 9/19/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 프리셋의 <b>문지기</b> — 저장 직전에 한 번 거친다(AS L331-342).
/// </summary>
/// <remarks>
/// <para>프리셋은 <b>구조</b>다. 한 대의 장비에만 해당하는 사실(<c>serial</c> · <c>installed_at</c> ·
/// <c>replaced_at</c>)이 템플릿에 섞이면, 그 프리셋으로 만든 <b>모든</b> 장비가 같은 일련번호를 갖게 된다.</para>
/// <para>주인 없는 재정의(<c>component_overrides.&lt;key&gt;</c> 인데 그 key 의 부품이 없다)도 여기서 버린다 —
/// 부품을 빼면서 재정의를 안 지우면 서버에 주인 없는 설정이 남는다(PRD 5-B).</para>
/// </remarks>
public static class DevicePresetSanitizer
{
    /// <summary>
    /// 유형 공통 사실 여덟 — 요청에 실으면 <b>422</b>. 카탈로그가 정본이고 장비가 되풀이해 갖는 값이 아니다(AS L213-215).
    /// </summary>
    public static IReadOnlyList<string> ForbiddenFactNames { get; } = new[]
    {
        "states", "commands", "command_params", "readable", "controllable", "produces", "critical", "enabled",
    };

    /// <summary>
    /// <c>enabled</c> 만은 <c>device_config.component_overrides.&lt;key&gt;.enabled</c> 에서 <b>정본</b>이다 —
    /// 같은 낱말이 부품 칸에선 금지, 재정의 칸에선 정상(AS L342). 둘을 섞으면 적용 즉시 422 다.
    /// </summary>
    public const string OverrideAllowedFact = "enabled";

    /// <summary>
    /// 프리셋이 담아도 되는 것만 남긴 <b>복사본</b>을 준다. 원본은 건드리지 않는다.
    /// </summary>
    /// <remarks>
    /// 재정의의 주인 찾기는 <b>대소문자를 가린다</b>(Ordinal) — 서버가 key 로 형상 ↔ 상태 ↔ 설정을 잇는데
    /// 그 이음이 정확 일치이기 때문이다. <c>Door</c> 와 <c>door</c> 는 서버에서도 남남이다.
    /// </remarks>
    public static DevicePreset Sanitize(DevicePreset preset)
    {
        if (preset is null) throw new ArgumentNullException(nameof(preset));

        var components = (preset.Components ?? Array.Empty<ComponentDefinitionModel>())
            .Where(c => c is not null)
            .Select(CleanComponent)
            .ToList();

        var keys = new HashSet<string>(components.Select(c => c.Key).Where(k => !string.IsNullOrEmpty(k)), StringComparer.Ordinal);

        JObject? overrides = null;
        if (preset.ComponentOverrides is not null)
        {
            overrides = new JObject();
            foreach (var property in preset.ComponentOverrides.Properties())
            {
                if (!keys.Contains(property.Name)) continue;      // 주인 없는 재정의는 버린다
                overrides.Add(property.Name, property.Value?.DeepClone());
            }
        }

        return preset with
        {
            Name = (preset.Name ?? string.Empty).Trim(),
            Components = components,
            Thresholds = preset.Thresholds is null ? null : (JObject)preset.Thresholds.DeepClone(),
            Modes = preset.Modes is null ? null : (JObject)preset.Modes.DeepClone(),
            ComponentOverrides = overrides,
        };
    }

    /// <summary>
    /// 담으면 안 되는 여덟이 어디에 들었는지 <b>경로</b>로 돌려준다. 빈 목록 = 깨끗하다.
    /// </summary>
    /// <remarks>
    /// <para>보는 곳은 둘이다 — ① 부품의 <c>spec</c> <b>맨 위 칸</b> ② 각 재정의 객체의 <b>맨 위 칸</b>.
    /// 더 깊은 칸(<c>spec.profiles.main.enabled</c>)까지 훑지 않는다: 그건 그 유형이 스스로 가진 구조이고,
    /// 서버가 422 로 보는 것은 <b>맨 위</b>에 올라온 유형 공통 사실이다.</para>
    /// <para>재정의 안의 <c>enabled</c> 는 <b>걸지 않는다</b> — 거기가 그 값의 정본 자리다.</para>
    /// </remarks>
    public static IReadOnlyList<string> FindForbiddenFacts(DevicePreset preset)
    {
        if (preset is null) throw new ArgumentNullException(nameof(preset));

        var found = new List<string>();

        var components = preset.Components ?? Array.Empty<ComponentDefinitionModel>();
        for (var i = 0; i < components.Count; i++)
        {
            var spec = components[i]?.Spec;
            if (spec is null) continue;
            foreach (var property in spec.Properties())
            {
                if (IsForbidden(property.Name))
                    found.Add($"components[{i}].spec.{property.Name}");
            }
        }

        if (preset.ComponentOverrides is not null)
        {
            foreach (var entry in preset.ComponentOverrides.Properties())
            {
                if (entry.Value is not JObject body) continue;
                foreach (var property in body.Properties())
                {
                    if (string.Equals(property.Name, OverrideAllowedFact, StringComparison.OrdinalIgnoreCase)) continue;
                    if (IsForbidden(property.Name))
                        found.Add($"component_overrides.{entry.Name}.{property.Name}");
                }
            }
        }

        return found;
    }

    private static bool IsForbidden(string name)
        => ForbiddenFactNames.Any(f => string.Equals(f, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 부품 한 개를 템플릿용으로 씻는다 — 한 대의 사실 셋을 비우고 유형 코드를 대문자로 맞춘다.
    /// </summary>
    private static ComponentDefinitionModel CleanComponent(ComponentDefinitionModel source) => new()
    {
        Key = (source.Key ?? string.Empty).Trim(),
        Type = (source.Type ?? string.Empty).Trim().ToUpperInvariant(),
        Label = source.Label,
        InService = source.InService,
        Channel = source.Channel,
        Position = source.Position,
        Manufacturer = source.Manufacturer,
        Model = source.Model,
        Firmware = source.Firmware,
        HardwareRev = source.HardwareRev,
        Serial = null,          // 그 한 대의 것
        InstalledAt = null,     // 그 한 대의 것
        ReplacedAt = null,      // 그 한 대의 것
        Spec = source.Spec is null ? null : (JObject)source.Spec.DeepClone(),
    };
}
