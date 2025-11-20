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
    [JsonProperty("id", Order = 1)]
    public int Id { get; set; }

    /// <summary>
    /// 생성일시 (ISO 8601)
    /// </summary>
    [JsonProperty("created_at", Order = 99)]
    public string? CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

    /// <summary>
    /// 수정일시 (ISO 8601)
    /// </summary>
    [JsonProperty("updated_at", Order = 100)]
    public string? UpdatedAt { get; set; }
}
