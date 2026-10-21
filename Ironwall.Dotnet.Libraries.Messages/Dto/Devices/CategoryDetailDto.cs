using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// 서버 카테고리 상세 DTO (§8.2.2) — 소속 서버 목록 포함
/// </summary>
public class CategoryDetailDto : CategoryDto
{
    /// <summary>
    /// 소속 서버 — 받은 그대로(호환용). ⚠ 7.0+ 서버는 <b>참조 <c>{id, category_server}</c></b> 만 싣는다(명세 §8.2.2) —
    /// 그때 이 목록의 이름 · 상태 · 접속 칸은 서버 값이 아니라 <see cref="ServerDto"/> 기본값이다. 새 코드는 <see cref="ServerReferences"/> 를 쓴다.
    /// </summary>
    [JsonProperty("servers", Order = 10)]
    public List<ServerDto>? Servers { get; set; }

    /// <summary>
    /// 소속 서버 참조(<c>id</c> · <c>category_server</c>) — 판본과 무관하게 서버가 준 값만 담는다(8.0.15 대조 G30).
    /// 이름 · 상태가 필요하면 서버 목록 캐시에서 <c>id</c> 로 찾는다. 직렬화되지 않는다.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<ServerReferenceDto> ServerReferences
        => Servers?.Select(s => new ServerReferenceDto { Id = s.Id, CategoryServer = s.CategoryServer }).ToList()
           ?? (IReadOnlyList<ServerReferenceDto>)System.Array.Empty<ServerReferenceDto>();
}

/// <summary>
/// 서버 참조 — <c>{id, category_server}</c>(명세 §8.2.2 <c>server_reference</c>). 접속 · 자격증명 · 이름 · 상태는 없다.
/// </summary>
public class ServerReferenceDto
{
    [JsonProperty("id", Order = 1)]
    public int Id { get; set; }

    [JsonProperty("category_server", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    public string? CategoryServer { get; set; }
}
