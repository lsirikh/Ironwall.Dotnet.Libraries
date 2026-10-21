using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// 함체 안에 든 장비 한 대 — <c>GET /api/devices/enclosures/{id}?include=contents</c> 의 <c>contents[]</c> 원소(v8.0.6, 명세 §5.5.6.1).
/// <para>모양: <c>{id, category_device, name_device}</c>. 실재하는 장비만 실린다.</para>
/// </summary>
public class EnclosureContentDto
{
    /// <summary>장비 id(<c>devices.id</c> — 전 카테고리 공통 유일).</summary>
    [JsonProperty("id", Order = 1)]
    public int Id { get; set; }

    /// <summary>장비 카테고리(단수 — <c>controller</c> · <c>camera</c> …).</summary>
    [JsonProperty("category_device", Order = 2)]
    public string? CategoryDevice { get; set; }

    /// <summary>장비 이름.</summary>
    [JsonProperty("name_device", Order = 3)]
    public string? NameDevice { get; set; }
}
