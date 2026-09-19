using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Presets;
/****************************************************************************
   Purpose      : 프리셋 파일의 봉투(schema · saved_at · presets[]) 와 DevicePreset 사이의 옮김.
   Created By   : GHLee
   Created On   : 9/19/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 디스크에 놓이는 봉투 — <c>{ "schema": 1, "saved_at": "...", "presets": [ ... ] }</c>.
/// </summary>
/// <remarks>
/// <para>이름은 전부 <b>snake_case</b> 로 박아 둔다 — 속성 이름을 바꿔도 파일이 안 깨지게, 그리고
/// 서버 본문(<c>hardware_spec.components[]</c>)과 같은 낱말을 쓰게.</para>
/// <para><b>봉투는 일부러 좁다.</b> <c>serial</c> · <c>installed_at</c> · <c>replaced_at</c> 을 담을 자리가
/// 아예 없다 — 그 셋은 그 <b>한 대</b>의 사실이지 템플릿의 사실이 아니다(AS L331-339).
/// 담을 자리가 없으면 실수로도 못 담는다.</para>
/// <para>모르는 JSON 속성은 조용히 버린다(더 새 판이 더한 칸을 만나도 읽기는 된다).
/// 반대로 <see cref="Schema"/> 가 <see cref="DevicePresetStore.CurrentSchema"/> 보다 크면
/// <b>쓰지 않는다</b> — 그 판단은 <see cref="DevicePresetStore"/> 가 한다.</para>
/// </remarks>
public sealed class DevicePresetFile
{
    /// <summary>
    /// 읽기 · 쓰기가 같이 쓰는 설정.
    /// </summary>
    /// <remarks>
    /// <para><see cref="TypeNameHandling.None"/> 은 <b>보안 요구</b>다 — <c>$type</c> 을 믿고 아무 형식이나
    /// 만들어 주면 남이 놓아둔 파일 하나로 임의 형식이 깨어난다.</para>
    /// <para><see cref="DateParseHandling.None"/> 이 없으면 <c>spec</c>/<c>thresholds</c> 안의
    /// "2026-01-01" 같은 <b>문자열</b>이 읽는 도중 <c>DateTime</c> 으로 바뀌어, 되쓸 때 글자가 달라진다.</para>
    /// </remarks>
    internal static JsonSerializerSettings Settings { get; } = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.Indented,
        TypeNameHandling = TypeNameHandling.None,
        MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
        DateParseHandling = DateParseHandling.None,
        MissingMemberHandling = MissingMemberHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace,
        Culture = CultureInfo.InvariantCulture,
    };

    [JsonProperty("schema")]
    public int Schema { get; set; } = DevicePresetStore.CurrentSchema;

    /// <summary>ISO 8601 + 시간대(<c>"o"</c>). 문자열로 들고 있는다 — 읽다가 형식이 바뀌지 않게.</summary>
    [JsonProperty("saved_at")]
    public string? SavedAt { get; set; }

    [JsonProperty("presets")]
    public List<DevicePresetEntry>? Presets { get; set; }

    /// <summary>
    /// <b>우리가 읽지 못한 줄의 원문</b> — 모르는 카테고리라 <see cref="ToPresets(out int)"/> 가 건너뛴 것들.
    /// </summary>
    /// <remarks>
    /// <para>건너뛰는 것 자체는 옳다(더 새 판이 더한 카테고리일 수 있다). 문제는 <b>그다음</b>이다 —
    /// 읽을 때 버리고 저장할 때 안 쓰면, 사용자가 프리셋 하나만 고쳐도 <b>모르는 줄이 파일에서 사라진다</b>.
    /// 그 줄은 그 판본에서 완전히 멀쩡한 프리셋이고, 우리가 못 읽는다는 사실이 지울 이유가 되지 않는다.</para>
    /// <para>그래서 <see cref="JObject"/> <b>원문 그대로</b> 들고 있다가 되쓸 때 아는 줄 뒤에 그대로 붙인다.
    /// 우리 모델로 한 번 굽히면(읽고 다시 쓰면) 모르는 칸이 그 자리에서 사라지므로 원문이어야 한다.</para>
    /// </remarks>
    [JsonIgnore]
    public List<JObject> UnknownEntries { get; } = new();

    /// <summary>프리셋들을 봉투에 담는다.</summary>
    /// <param name="unknownEntries">
    /// 되쓸 때 그대로 붙일 원문 줄(<see cref="UnknownEntries"/>). 없으면 <c>null</c>.
    /// </param>
    public static DevicePresetFile Wrap(
        IEnumerable<DevicePreset> presets,
        DateTimeOffset savedAt,
        IEnumerable<JObject>? unknownEntries = null)
    {
        var file = new DevicePresetFile
        {
            Schema = DevicePresetStore.CurrentSchema,
            SavedAt = savedAt.ToString("o", CultureInfo.InvariantCulture),
            Presets = (presets ?? Enumerable.Empty<DevicePreset>()).Select(DevicePresetEntry.From).ToList(),
        };

        foreach (var entry in unknownEntries ?? Enumerable.Empty<JObject>())
        {
            if (entry is null) continue;
            file.UnknownEntries.Add((JObject)entry.DeepClone());
        }

        return file;
    }

    /// <summary>
    /// 봉투를 글로. <see cref="UnknownEntries"/> 가 있으면 <c>presets</c> 배열 <b>뒤에</b> 원문 그대로 붙인다.
    /// </summary>
    /// <remarks>
    /// 직렬화한 뒤 토큰으로 한 번 더 손대는 까닭은, <see cref="Presets"/> 가 형식 있는 목록이라
    /// <see cref="JObject"/> 를 섞어 담을 자리가 없기 때문이다. 다시 읽을 때도
    /// <see cref="DateParseHandling.None"/> 을 써서 <c>spec</c> 안의 날짜꼴 문자열이 바뀌지 않게 한다.
    /// </remarks>
    public string ToJson()
    {
        var json = JsonConvert.SerializeObject(this, Settings);
        if (UnknownEntries.Count == 0) return json;

        JObject root;
        using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
        {
            root = JObject.Load(reader);
        }

        if (root["presets"] is not JArray array)
        {
            array = new JArray();
            root["presets"] = array;
        }

        foreach (var entry in UnknownEntries)
            array.Add(entry.DeepClone());

        return root.ToString(Formatting.Indented);
    }

    /// <summary>
    /// 글을 봉투로 읽는다. 봉투가 아니면 <see cref="JsonException"/> — 부르는 쪽이 "깨졌다"로 판정한다.
    /// </summary>
    public static DevicePresetFile Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new JsonException("빈 파일이라 봉투를 읽을 수 없다.");

        JToken root;
        using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
        {
            root = JToken.ReadFrom(reader);
        }

        if (root is not JObject obj)
            throw new JsonException("봉투가 객체가 아니다.");
        if (obj["schema"] is not JValue schema || schema.Type != JTokenType.Integer)
            throw new JsonException("봉투에 schema 가 없다.");
        if (obj["presets"] is not null and not JArray)
            throw new JsonException("presets 가 배열이 아니다.");

        var file = obj.ToObject<DevicePresetFile>(JsonSerializer.Create(Settings))
                   ?? throw new JsonException("봉투를 읽지 못했다.");
        if (file.Schema <= 0)
            throw new JsonException("schema 값이 1 보다 작다.");

        // 건너뛴 줄을 되쓰려면 원문이 필요하다 — 형식 있는 목록과 자리가 1:1 로 맞는다.
        file._raw = obj["presets"] as JArray;
        return file;
    }

    /// <summary>
    /// 봉투의 줄들을 프리셋으로 옮긴다. <b>모르는 카테고리 한 줄이 파일 전체를 버리게 두지 않는다</b> —
    /// 그 줄만 빼고 <paramref name="skipped"/> 로 센다(다음 판이 더한 카테고리일 수 있다).
    /// </summary>
    public IReadOnlyList<DevicePreset> ToPresets(out int skipped)
        => ToPresets(out skipped, out _);

    /// <summary>
    /// 위와 같되, 건너뛴 줄의 <b>원문</b>도 함께 돌려준다 — 되쓸 때 그대로 붙이기 위한 것이다.
    /// </summary>
    /// <param name="skippedEntries">
    /// 건너뛴 줄 중 <see cref="JObject"/> 인 것들. 원문을 확보하지 못한 줄(배열에 객체가 아닌 값이 섞였거나
    /// <see cref="Parse"/> 를 거치지 않고 만든 봉투)은 <paramref name="skipped"/> 에만 세고 여기엔 없다.
    /// </param>
    public IReadOnlyList<DevicePreset> ToPresets(out int skipped, out IReadOnlyList<JObject> skippedEntries)
    {
        skipped = 0;
        var kept = new List<DevicePreset>();
        var unknown = new List<JObject>();
        var entries = Presets ?? new List<DevicePresetEntry>();

        for (var i = 0; i < entries.Count; i++)
        {
            var preset = entries[i]?.ToPreset();
            if (preset is not null) { kept.Add(preset); continue; }

            skipped++;
            if (_raw is not null && i < _raw.Count && _raw[i] is JObject raw)
                unknown.Add((JObject)raw.DeepClone());
        }

        skippedEntries = unknown;
        return kept;
    }

    /// <summary><see cref="Parse"/> 가 읽은 <c>presets</c> 배열 원문 — 자리로 <see cref="Presets"/> 와 맞춘다.</summary>
    private JArray? _raw;
}

/// <summary>봉투 안의 프리셋 한 줄.</summary>
public sealed class DevicePresetEntry
{
    [JsonProperty("id")] public string? Id { get; set; }
    [JsonProperty("name")] public string? Name { get; set; }

    /// <summary>enum 의 <b>이름</b>(예: <c>"Enclosure"</c>). 숫자로 적지 않는다 — 값이 밀리면 뜻이 바뀐다.</summary>
    [JsonProperty("category")] public string? Category { get; set; }

    [JsonProperty("type_axis")] public string? TypeAxis { get; set; }
    [JsonProperty("extra_axis")] public string? ExtraAxis { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("is_seed")] public bool IsSeed { get; set; }
    [JsonProperty("updated_at")] public string? UpdatedAt { get; set; }
    [JsonProperty("hardware")] public PresetHardwareEntry? Hardware { get; set; }
    [JsonProperty("components")] public List<PresetComponentEntry>? Components { get; set; }
    [JsonProperty("thresholds")] public JObject? Thresholds { get; set; }
    [JsonProperty("modes")] public JObject? Modes { get; set; }
    [JsonProperty("component_overrides")] public JObject? ComponentOverrides { get; set; }

    public static DevicePresetEntry From(DevicePreset preset)
    {
        if (preset is null) throw new ArgumentNullException(nameof(preset));

        var hardware = PresetHardwareEntry.From(preset);
        return new DevicePresetEntry
        {
            Id = preset.Id,
            Name = preset.Name,
            Category = preset.Category.ToString(),
            TypeAxis = preset.TypeAxisCode,
            ExtraAxis = preset.ExtraAxisCode,
            Description = preset.Description,
            IsSeed = preset.IsSeed,
            UpdatedAt = preset.UpdatedAt == default
                ? null
                : preset.UpdatedAt.ToString("o", CultureInfo.InvariantCulture),
            Hardware = hardware,
            Components = (preset.Components ?? Array.Empty<ComponentDefinitionModel>())
                .Where(c => c is not null)
                .Select(PresetComponentEntry.From)
                .ToList(),
            Thresholds = preset.Thresholds is null ? null : (JObject)preset.Thresholds.DeepClone(),
            Modes = preset.Modes is null ? null : (JObject)preset.Modes.DeepClone(),
            ComponentOverrides = preset.ComponentOverrides is null ? null : (JObject)preset.ComponentOverrides.DeepClone(),
        };
    }

    /// <summary>모르는 카테고리면 null — 부르는 쪽이 그 줄만 건너뛴다.</summary>
    public DevicePreset? ToPreset()
    {
        var category = ParseCategory(Category);
        if (category is null) return null;

        var updated = DateTimeOffset.TryParse(UpdatedAt, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var parsed) ? parsed : default;

        return new DevicePreset
        {
            Id = string.IsNullOrWhiteSpace(Id) ? DevicePresetStore.NewId() : Id!,
            Name = Name ?? string.Empty,
            Category = category.Value,
            TypeAxisCode = TypeAxis,
            ExtraAxisCode = ExtraAxis,
            Description = Description,
            Manufacturer = Hardware?.Manufacturer,
            Model = Hardware?.Model,
            Firmware = Hardware?.Firmware,
            HardwareRev = Hardware?.HardwareRev,
            MaxDetectionRange = Hardware?.MaxDetectionRange,
            OnvifVersion = Hardware?.OnvifVersion,
            Components = (Components ?? new List<PresetComponentEntry>())
                .Where(c => c is not null)
                .Select(c => c.ToModel())
                .ToList(),
            Thresholds = Thresholds is null ? null : (JObject)Thresholds.DeepClone(),
            Modes = Modes is null ? null : (JObject)Modes.DeepClone(),
            ComponentOverrides = ComponentOverrides is null ? null : (JObject)ComponentOverrides.DeepClone(),
            UpdatedAt = updated,
            IsSeed = IsSeed,
        };
    }

    /// <remarks>
    /// <c>Enum.TryParse</c> 는 <b>"3" 같은 숫자 글자도 통과시킨다</b>(정의에 없는 값까지). 파일이 이름으로
    /// 적기로 한 이상 숫자는 모르는 카테고리로 본다.
    /// </remarks>
    private static EnumDeviceCategory? ParseCategory(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var text = name!.Trim();
        if (text.Length > 0 && (char.IsDigit(text[0]) || text[0] == '-' || text[0] == '+')) return null;
        return Enum.TryParse<EnumDeviceCategory>(text, ignoreCase: true, out var value)
               && Enum.IsDefined(typeof(EnumDeviceCategory), value)
            ? value
            : null;
    }
}

/// <summary>제원 기본값(<c>hardware_spec</c> 중 템플릿이 가질 수 있는 것만).</summary>
public sealed class PresetHardwareEntry
{
    [JsonProperty("manufacturer")] public string? Manufacturer { get; set; }
    [JsonProperty("model")] public string? Model { get; set; }
    [JsonProperty("firmware")] public string? Firmware { get; set; }
    [JsonProperty("hardware_rev")] public string? HardwareRev { get; set; }
    [JsonProperty("max_detection_range")] public double? MaxDetectionRange { get; set; }
    [JsonProperty("onvif_version")] public string? OnvifVersion { get; set; }

    /// <summary>여섯 칸이 모두 비면 null — 빈 <c>"hardware": {}</c> 를 파일에 남기지 않는다.</summary>
    public static PresetHardwareEntry? From(DevicePreset preset)
    {
        if (preset.Manufacturer is null && preset.Model is null && preset.Firmware is null
            && preset.HardwareRev is null && preset.MaxDetectionRange is null && preset.OnvifVersion is null)
            return null;

        return new PresetHardwareEntry
        {
            Manufacturer = preset.Manufacturer,
            Model = preset.Model,
            Firmware = preset.Firmware,
            HardwareRev = preset.HardwareRev,
            MaxDetectionRange = preset.MaxDetectionRange,
            OnvifVersion = preset.OnvifVersion,
        };
    }
}

/// <summary>부품 한 개 — 장비별 사실만(일련번호 · 설치일 · 교체일 칸이 없다).</summary>
public sealed class PresetComponentEntry
{
    [JsonProperty("key")] public string? Key { get; set; }
    [JsonProperty("type")] public string? Type { get; set; }
    [JsonProperty("label")] public string? Label { get; set; }
    [JsonProperty("channel")] public int? Channel { get; set; }
    [JsonProperty("position")] public string? Position { get; set; }
    [JsonProperty("in_service")] public bool? InService { get; set; }
    [JsonProperty("manufacturer")] public string? Manufacturer { get; set; }
    [JsonProperty("model")] public string? Model { get; set; }
    [JsonProperty("firmware")] public string? Firmware { get; set; }
    [JsonProperty("hardware_rev")] public string? HardwareRev { get; set; }
    [JsonProperty("spec")] public JObject? Spec { get; set; }

    public static PresetComponentEntry From(ComponentDefinitionModel model) => new()
    {
        Key = model.Key,
        Type = model.Type,
        Label = model.Label,
        Channel = model.Channel,
        Position = model.Position,
        InService = model.InService,
        Manufacturer = model.Manufacturer,
        Model = model.Model,
        Firmware = model.Firmware,
        HardwareRev = model.HardwareRev,
        Spec = model.Spec is null ? null : (JObject)model.Spec.DeepClone(),
    };

    public ComponentDefinitionModel ToModel() => new()
    {
        Key = Key ?? string.Empty,
        Type = Type ?? string.Empty,
        Label = Label,
        Channel = Channel,
        Position = Position,
        InService = InService,
        Manufacturer = Manufacturer,
        Model = Model,
        Firmware = Firmware,
        HardwareRev = HardwareRev,
        Spec = Spec is null ? null : (JObject)Spec.DeepClone(),
    };
}
