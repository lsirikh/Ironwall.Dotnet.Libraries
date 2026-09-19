using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Model;

/// <summary>차이 한 줄. 더한 것 · 뺀 것은 <paramref name="ChangedFields"/> 가 비어 있다.</summary>
public sealed record AssemblyDiffEntry(string Key, string TypeCode, string? Label, IReadOnlyList<string> ChangedFields);

/// <summary>
/// 적용 전 변경 미리보기(FR-16) — 더한 것 · 뺀 것 · 고친 것.
/// </summary>
/// <remarks>
/// <b>짝은 오직 <c>key</c> 로만 잇는다.</b> 같은 <c>type</c> 이 양쪽에 여럿이면 type 으로 이을 수 없고,
/// 서버도 부품을 key 로 식별한다 — 뺀 <c>door</c> 와 더한 <c>door_2</c> 는 같은 유형이어도 <b>제거 1 + 추가 1</b> 이다.
/// 순서는 비교에 들어가지 않는다 — 서버 <c>components[]</c> 에 순서 계약이 없다(AS L295).
/// </remarks>
public sealed record AssemblyDiff(
    IReadOnlyList<AssemblyDiffEntry> Added,
    IReadOnlyList<AssemblyDiffEntry> Removed,
    IReadOnlyList<AssemblyDiffEntry> Changed)
{
    public bool IsEmpty => Added.Count == 0 && Removed.Count == 0 && Changed.Count == 0;

    public static AssemblyDiff Empty { get; } = new(
        Array.Empty<AssemblyDiffEntry>(), Array.Empty<AssemblyDiffEntry>(), Array.Empty<AssemblyDiffEntry>());

    public static AssemblyDiff Compute(
        IEnumerable<ComponentDefinitionModel> baseline,
        JObject? baselineOverrides,
        IEnumerable<ComponentDefinitionModel> current,
        JObject? currentOverrides)
    {
        var before = Index(baseline, out var beforeOrder);
        var after = Index(current, out var afterOrder);

        var added = new List<AssemblyDiffEntry>();
        var removed = new List<AssemblyDiffEntry>();
        var changed = new List<AssemblyDiffEntry>();

        foreach (var key in afterOrder)
        {
            var now = after[key];
            if (!before.TryGetValue(key, out var was))
            {
                added.Add(Entry(key, now, Array.Empty<string>()));
                continue;
            }

            var fields = ChangedFields(was, Pick(baselineOverrides, key), now, Pick(currentOverrides, key));
            if (fields.Count > 0) changed.Add(Entry(key, now, fields));
        }

        foreach (var key in beforeOrder)
        {
            if (!after.ContainsKey(key)) removed.Add(Entry(key, before[key], Array.Empty<string>()));
        }

        return new AssemblyDiff(added, removed, changed);
    }

    /// <summary>key 로 색인한다 — 중복 key 는 첫 줄이 이긴다(중복 자체는 보드가 따로 막는다).</summary>
    private static Dictionary<string, ComponentDefinitionModel> Index(IEnumerable<ComponentDefinitionModel>? items, out List<string> order)
    {
        var map = new Dictionary<string, ComponentDefinitionModel>(StringComparer.Ordinal);
        order = new List<string>();
        foreach (var item in items ?? Enumerable.Empty<ComponentDefinitionModel>())
        {
            if (item is null) continue;
            var key = item.Key ?? string.Empty;
            if (map.TryAdd(key, item)) order.Add(key);
        }
        return map;
    }

    private static AssemblyDiffEntry Entry(string key, ComponentDefinitionModel model, IReadOnlyList<string> fields)
        => new(key, (model.Type ?? string.Empty).ToUpperInvariant(), model.Label, fields);

    private static JToken? Pick(JObject? source, string key)
        => source is not null && source.TryGetValue(key, out var token) ? token : null;

    /// <summary>
    /// 고쳐진 칸의 이름(API 이름). <c>installed_at</c> · <c>replaced_at</c> 은 비교하지 않는다 —
    /// 들고 나르기만 하는 값이라 사람이 고친 변경으로 세지 않는다.
    /// </summary>
    private static IReadOnlyList<string> ChangedFields(
        ComponentDefinitionModel was, JToken? wasOverrides,
        ComponentDefinitionModel now, JToken? nowOverrides)
    {
        var fields = new List<string>();

        if (!SameText(was.Label, now.Label)) fields.Add("label");
        if (was.Channel != now.Channel) fields.Add("channel");
        if (!SameText(was.Position, now.Position)) fields.Add("position");
        if ((was.InService ?? true) != (now.InService ?? true)) fields.Add("in_service");
        if (!SameText(was.Manufacturer, now.Manufacturer)) fields.Add("manufacturer");
        if (!SameText(was.Model, now.Model)) fields.Add("model");
        if (!SameText(was.Serial, now.Serial)) fields.Add("serial");
        if (!SameText(was.Firmware, now.Firmware)) fields.Add("firmware");
        if (!SameText(was.HardwareRev, now.HardwareRev)) fields.Add("hardware_rev");
        if (!JToken.DeepEquals(was.Spec, now.Spec)) fields.Add("spec");
        if (!SameOverrides(wasOverrides, nowOverrides)) fields.Add("overrides");
        if (!string.Equals((was.Type ?? string.Empty).ToUpperInvariant(), (now.Type ?? string.Empty).ToUpperInvariant(), StringComparison.Ordinal))
            fields.Add("type");

        return fields;
    }

    /// <summary>빈 칸은 null 이든 "" 이든 같은 빈 칸이다 — 화면이 빈 칸을 "" 로 돌려주는 것이 변경으로 세면 안 된다.</summary>
    private static bool SameText(string? a, string? b)
        => string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b) || string.Equals(a, b, StringComparison.Ordinal);

    /// <summary>재정의는 없음 · JSON null · 빈 객체를 모두 "없음" 으로 본다.</summary>
    private static bool SameOverrides(JToken? a, JToken? b)
    {
        var emptyA = IsNothing(a);
        var emptyB = IsNothing(b);
        if (emptyA || emptyB) return emptyA && emptyB;
        return JToken.DeepEquals(a, b);
    }

    private static bool IsNothing(JToken? token)
        => token is null || token.Type == JTokenType.Null || (token is JObject o && !o.HasValues);
}
