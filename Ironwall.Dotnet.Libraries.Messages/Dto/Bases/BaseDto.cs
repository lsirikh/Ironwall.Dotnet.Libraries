using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
public class BaseDto
{
    /// <summary>
    /// 데이터베이스 ID (자동 생성)
    /// </summary>
    [JsonProperty("id", Order = 1, DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int Id { get; set; }

    /// <summary>
    /// 생성일시 (ISO 8601 with Korea offset)
    /// 예: "2025-11-28T18:30:00.000+09:00"
    /// </summary>
    [JsonProperty("created_at", Order = 99, NullValueHandling = NullValueHandling.Ignore)]
    public string? CreatedAt { get; set; } = KoreaTimeHelper.GetKoreaTimeIso8601();

    /// <summary>
    /// 수정일시 (ISO 8601)
    /// </summary>
    [JsonProperty("updated_at", Order = 100, NullValueHandling = NullValueHandling.Ignore)]
    public string? UpdatedAt { get; set; }
}
