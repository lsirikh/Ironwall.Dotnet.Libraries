using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// 서버 카테고리 DTO (§8.2)
/// </summary>
public class CategoryDto : BaseDto
{
    [JsonProperty("name", Order = 2)]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("type_server", Order = 3)]
    public string TypeServer { get; set; } = string.Empty;

    [JsonProperty("description", Order = 4)]
    public string? Description { get; set; }

    [JsonProperty("sort_order", Order = 5)]
    public int SortOrder { get; set; }

    /// <summary>
    /// 7.0+ 판별자 키 <c>category_server</c> — 서버가 <c>type_server</c> 를 이 이름으로 바꿨다
    /// (서버 <c>app/schemas/server.py:69</c> <c>SERVER_CATEGORY_REMOVED_FIELDS</c>, 응답 <c>:841</c>).
    /// </summary>
    /// <remarks>
    /// <para><b>읽기 전용 창구</b>다 — 받은 값은 <see cref="TypeServer"/> 로 옮겨 호출부가 판본을 모르게 한다.
    /// 이 키가 없으면 8.0 응답의 유형이 빈 문자열로 남는다.</para>
    /// <para><b>직렬화하지 않는다</b> — 6.3 본문은 바이트 그대로 두고, 7.0+ 쓰기 본문은
    /// <c>ServerApiService</c> 가 판본을 보고 따로 조립한다(<c>type_server</c> 는 7.0+ 에서 422 <c>REMOVED_FIELD</c>).</para>
    /// </remarks>
    [JsonProperty("category_server", Order = 6)]
    public string? CategoryServer
    {
        get => string.IsNullOrEmpty(TypeServer) ? null : TypeServer;
        set { if (!string.IsNullOrEmpty(value)) TypeServer = value!; }
    }

    public bool ShouldSerializeCategoryServer() => false;
}
