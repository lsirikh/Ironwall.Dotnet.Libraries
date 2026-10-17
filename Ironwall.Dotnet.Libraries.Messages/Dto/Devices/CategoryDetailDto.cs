using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// 서버 카테고리 상세 DTO (§8.2.2) — 하위 서버 목록 포함
/// </summary>
public class CategoryDetailDto : CategoryDto
{
    [JsonProperty("servers", Order = 10)]
    public List<ServerDto>? Servers { get; set; }
}
