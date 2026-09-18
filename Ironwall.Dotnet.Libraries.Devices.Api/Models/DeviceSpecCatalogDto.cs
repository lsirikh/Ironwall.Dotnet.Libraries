using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Models;
/****************************************************************************
   Purpose      : GET /api/devices/spec · /api/devices/{종류}/spec 카탈로그 DTO (A-devices D-32)
   Created By   : GHLee
   Created On   : 9/18/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 서버 7.0+ <c>DeviceSpecCatalog</c> — <c>GET /api/devices/spec</c> 의 <c>data</c>.
/// <b>장비 수와 무관한 단일 정보</b>다(장비마다 부르지 않는다).
/// </summary>
/// <remarks>
/// <para><b>왜 필요한가</b> — 종류축·부품 유형·상태·고장 사유·메트릭 키가 <b>DB 카탈로그</b>로 옮겨져
/// 값 추가에 서버 배포가 필요 없다. 즉 우리 쪽 하드코딩 상수는 <b>부분집합</b>일 수밖에 없고
/// (<c>ComponentTypeNames</c> 는 "우리가 실제로 쓰는 것"만 담는다) 어휘 정본은 이 응답이다.
/// <c>by-component</c> 의 <c>component_type</c> 이 카탈로그에 없으면 <b>422 + 허용 목록</b>이다.</para>
/// <para>중첩이 깊은 <c>definition</c> 만 <see cref="JObject"/> 로 남긴다 — 유형별로 키가 다르고
/// (상태 축이 있는 유형만 <c>states</c> 를 갖는다) 서버가 계속 늘리는 자리라 고정 타입으로 굳히면
/// 새 키가 조용히 사라진다.</para>
/// </remarks>
public class DeviceSpecCatalogDto
{
    /// <summary>
    /// 확장 어휘 — <c>어휘 이름 → 행 목록</c>(부품 유형·명령·상태·고장 사유·메트릭 키·단위·모드).
    /// </summary>
    [JsonProperty("vocabularies", Order = 1, NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, List<VocabularyEntryDto>>? Vocabularies { get; set; }

    /// <summary>
    /// 엄격 어휘(코드 Enum) — <c>component_health</c>·<c>metric_value_type</c>·<c>metric_direction</c>·
    /// <c>metric_aggregation</c>·<c>connection_type</c>.
    /// </summary>
    [JsonProperty("strict_vocabularies", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, List<EnumEntryDto>>? StrictVocabularies { get; set; }

    /// <summary>카테고리 7종의 종류축(D1).</summary>
    [JsonProperty("categories", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public List<CategorySpecDto>? Categories { get; set; }

    /// <summary>어휘 하나를 이름으로 찾는다(대소문자 무시). 없으면 <c>null</c>.</summary>
    public List<VocabularyEntryDto>? FindVocabulary(string? name)
        => FindIn(Vocabularies, name);

    /// <summary>카테고리 하나를 찾는다(대소문자 무시 — <b>단수</b> <c>enclosure</c>). 없으면 <c>null</c>.</summary>
    public CategorySpecDto? FindCategory(string? categoryDevice)
    {
        if (Categories == null || string.IsNullOrWhiteSpace(categoryDevice)) return null;
        foreach (var item in Categories)
        {
            if (string.Equals(item.CategoryDevice, categoryDevice.Trim(), StringComparison.OrdinalIgnoreCase))
                return item;
        }
        return null;
    }

    internal static List<T>? FindIn<T>(Dictionary<string, List<T>>? map, string? name)
    {
        if (map == null || string.IsNullOrWhiteSpace(name)) return null;
        var key = name.Trim();
        if (map.TryGetValue(key, out var hit)) return hit;
        foreach (var pair in map)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)) return pair.Value;
        }
        return null;
    }

    /// <summary>확장 어휘 이름 — 부품 유형(<c>by-component</c> 의 <c>component_type</c> 정본).</summary>
    public const string VOCAB_COMPONENT_TYPE = "component_type";

    /// <summary>확장 어휘 이름 — 부품 고장 사유(<c>ComponentStatus.fault_reason</c> 어휘).</summary>
    public const string VOCAB_COMPONENT_FAULT = "component_fault";

    /// <summary>확장 어휘 이름 — 메트릭 키(<c>device_config.thresholds</c> 키 어휘).</summary>
    public const string VOCAB_METRIC_KEY = "metric_key";
}

/// <summary>
/// 서버 7.0+ <c>DeviceTypeSpec</c>/<c>DeviceTypeSpecWithExtraAxes</c> —
/// <c>GET /api/devices/{종류}/spec</c> 의 <c>data</c>(한 카테고리만).
/// </summary>
/// <remarks>
/// <see cref="ExtraAxes"/> 는 <b>스피커에만</b> 있다(<c>speaker_role</c>) — 다른 카테고리는 빈 배열이다.
/// 두 응답 형태를 한 타입으로 받아 <c>anyOf</c> 분기를 없앤다(없는 키는 <c>null</c>).
/// </remarks>
public class DeviceTypeSpecDto
{
    /// <summary>장비 카테고리(<b>단수</b>, 7종).</summary>
    [JsonProperty("category_device", Order = 1, NullValueHandling = NullValueHandling.Ignore)]
    public string? CategoryDevice { get; set; }

    /// <summary>이 카테고리의 종류축.</summary>
    [JsonProperty("type_axis", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    public TypeAxisSpecDto? TypeAxis { get; set; }

    /// <summary>이 카테고리가 쓸 수 있는 확장 어휘만(공통 어휘는 항상 포함).</summary>
    [JsonProperty("vocabularies", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, List<VocabularyEntryDto>>? Vocabularies { get; set; }

    /// <summary>엄격 어휘(전 카테고리 공통).</summary>
    [JsonProperty("strict_vocabularies", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, List<EnumEntryDto>>? StrictVocabularies { get; set; }

    /// <summary>종류축 외의 축 — <b>스피커 역할축</b>(<c>speaker_role</c>)뿐이다.</summary>
    [JsonProperty("extra_axes", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    public List<ExtraAxisSpecDto>? ExtraAxes { get; set; }

    /// <summary>어휘 하나를 이름으로 찾는다(대소문자 무시).</summary>
    public List<VocabularyEntryDto>? FindVocabulary(string? name)
        => DeviceSpecCatalogDto.FindIn(Vocabularies, name);
}

/// <summary>서버 <c>CategorySpec</c>/<c>CategorySpecWithExtraAxes</c> — 카탈로그의 카테고리 한 항목.</summary>
public class CategorySpecDto
{
    /// <summary>장비 카테고리(<b>단수</b>).</summary>
    [JsonProperty("category_device", Order = 1, NullValueHandling = NullValueHandling.Ignore)]
    public string? CategoryDevice { get; set; }

    /// <summary>이 카테고리의 종류축.</summary>
    [JsonProperty("type_axis", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    public TypeAxisSpecDto? TypeAxis { get; set; }

    /// <summary>종류축 외의 축(스피커 역할축). 없으면 <c>null</c>·빈 배열.</summary>
    [JsonProperty("extra_axes", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public List<ExtraAxisSpecDto>? ExtraAxes { get; set; }
}

/// <summary>
/// 서버 <c>TypeAxisSpec</c>/<c>ShapeTypeAxisSpec</c> — 그 카테고리의 <b>종류축 한 개</b>.
/// </summary>
/// <remarks>
/// <see cref="Field"/> 가 곧 <b>목록 필터 이름</b>이다(<c>type_camera</c>·<c>type_sensor</c> …) —
/// 7.0 에서 <c>type_device</c> 가 카테고리마다 다른 이름으로 갈린 그 이름을 서버가 여기서 알려준다.
/// <see cref="UnknownCode"/>·<see cref="Default"/> 는 <b>형상축(Shape)</b> 에만 있다(없으면 <c>null</c>).
/// </remarks>
public class TypeAxisSpecDto
{
    /// <summary>장비 표현의 축 필드 이름 = <b>목록 쿼리 키</b>(예 <c>type_camera</c>).</summary>
    [JsonProperty("field", Order = 1, NullValueHandling = NullValueHandling.Ignore)]
    public string? Field { get; set; }

    /// <summary>선택 가능한 값 전부.</summary>
    [JsonProperty("values", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    public List<EnumEntryDto>? Values { get; set; }

    /// <summary>v7.0 D1 — 7축 전부 <c>false</c>(DB NOT NULL).</summary>
    [JsonProperty("nullable", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public bool? Nullable { get; set; }

    /// <summary><c>POST</c> 에서 생략할 수 없는가.</summary>
    [JsonProperty("required_on_create", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
    public bool? RequiredOnCreate { get; set; }

    /// <summary>사람이 읽는 규칙 설명.</summary>
    [JsonProperty("note", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    public string? Note { get; set; }

    /// <summary>"현장 미확인" 코드 — <b>형상축에만</b> 있다(예 <c>Unknown</c>).</summary>
    [JsonProperty("unknown_code", Order = 6, NullValueHandling = NullValueHandling.Ignore)]
    public string? UnknownCode { get; set; }

    /// <summary><c>POST</c> 에서 생략했을 때 저장되는 값 — <b>형상축에만</b> 있다.</summary>
    [JsonProperty("default", Order = 7, NullValueHandling = NullValueHandling.Ignore)]
    public string? Default { get; set; }
}

/// <summary>서버 <c>ExtraAxisSpec</c> — 종류축 외의 축(현재 스피커 <c>speaker_role</c> 뿐).</summary>
public class ExtraAxisSpecDto
{
    /// <summary>축 필드 이름 = 목록 쿼리 키(예 <c>speaker_role</c>).</summary>
    [JsonProperty("field", Order = 1, NullValueHandling = NullValueHandling.Ignore)]
    public string? Field { get; set; }

    /// <summary>화면 표시명.</summary>
    [JsonProperty("label", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    public string? Label { get; set; }

    /// <summary>선택 가능한 값 전부.</summary>
    [JsonProperty("values", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public List<EnumEntryDto>? Values { get; set; }

    [JsonProperty("nullable", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
    public bool? Nullable { get; set; }

    /// <summary>생략 시 저장되는 값(예 <c>NORMAL</c>).</summary>
    [JsonProperty("default", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    public string? Default { get; set; }
}

/// <summary>
/// 서버 <c>VocabularyEntry</c> — 확장(DB) 어휘 한 행.
/// </summary>
/// <remarks>
/// <see cref="DeprecatedAt"/> 이 있으면 <b>새 선택 목록에서만 숨긴다</b> — 동작과 <c>/spec</c> 노출은 유지된다
/// (기존 데이터가 그 값을 들고 있으므로 표시 이름은 계속 필요하다).
/// </remarks>
public class VocabularyEntryDto
{
    /// <summary>어휘 값(예 <c>DOOR_SENSOR</c>).</summary>
    [JsonProperty("code", Order = 1)]
    public string Code { get; set; } = string.Empty;

    /// <summary>화면 표시명.</summary>
    [JsonProperty("label", Order = 2)]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// 폐기 권고 시각. <c>null</c> 이 아니면 <b>새 선택 목록에서 숨긴다</b>(동작은 유지).
    /// <c>component_type</c> 밖의 어휘는 항상 <c>null</c>.
    /// </summary>
    [JsonProperty("deprecated_at", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public string? DeprecatedAt { get; set; }

    /// <summary>이 어휘를 쓰는 자원 카테고리 목록. <c>null</c> 은 <b>전 카테고리 공통</b>이다.</summary>
    [JsonProperty("applies_to", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? AppliesTo { get; set; }

    /// <summary>
    /// 어휘별 부가 정의. <c>component_type</c> 이면 <b>그 유형의 정의 전부</b>다 —
    /// <c>states</c>(상태 축이 있는 유형만)·명령·메트릭 등. 키가 유형마다 달라 <see cref="JObject"/> 로 남긴다.
    /// </summary>
    [JsonProperty("definition", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    public JObject? Definition { get; set; }

    /// <summary>새 선택 목록에 넣어도 되는가(= 폐기 권고가 없는가).</summary>
    [JsonIgnore]
    public bool IsSelectable => string.IsNullOrWhiteSpace(DeprecatedAt);

    /// <summary>
    /// 이 유형이 가진 상태 어휘(<c>definition.states</c>). 상태 축이 없는 유형이면 <c>null</c> —
    /// 그런 유형에 <c>state</c> 를 보고하면 422 다.
    /// </summary>
    [JsonIgnore]
    public List<string>? States
    {
        get
        {
            if (Definition == null) return null;
            if (Definition["states"] is not JArray array) return null;
            var list = new List<string>(array.Count);
            foreach (var token in array)
            {
                var value = token?.Type == JTokenType.String ? token.Value<string>() : token?.ToString();
                if (!string.IsNullOrWhiteSpace(value)) list.Add(value!);
            }
            return list.Count == 0 ? null : list;
        }
    }
}

/// <summary>서버 <c>EnumEntry</c> — 코드 Enum 어휘 한 행(<c>{code, label}</c> 뿐이다).</summary>
public class EnumEntryDto
{
    /// <summary>저장·전송되는 값.</summary>
    [JsonProperty("code", Order = 1)]
    public string Code { get; set; } = string.Empty;

    /// <summary>화면 표시명.</summary>
    [JsonProperty("label", Order = 2)]
    public string Label { get; set; } = string.Empty;
}
