using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Systems;
/****************************************************************************
   Purpose      : 시스템 이벤트(서버 레벨 로그 · 알람, REST §8.7 · 브로커 SYSTEM_EVENT) — G52.
                  두 판본 모양을 다 읽는다:
                  · 7.0+  server{id, category_server}|null · server_description · is_acknowledged
                  · 6.3   server_id · acknowledged(NATS 옛 이름)
                  장비 이벤트와 다른 자원이라 category_event 가 없고, 판별자는 type_event 다.
   Created By   : Claude
   Created On   : 2026-10-21
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class SystemEventDto
{
    [JsonProperty("id")]
    public int Id { get; set; }

    /// <summary>7.0+ 서버 참조. 전역 이벤트 · 지워진 서버면 <c>null</c>.</summary>
    [JsonProperty("server", NullValueHandling = NullValueHandling.Ignore)]
    public SystemEventServerRefDto? Server { get; set; }

    /// <summary>6.3 서버 id 스칼라(7.0 에는 키가 없다).</summary>
    [JsonProperty("server_id", NullValueHandling = NullValueHandling.Ignore)]
    public int? ServerIdLegacy { get; set; }

    /// <summary>사건 당시 서버 표시 스냅샷(7.0+). 없으면 null.</summary>
    [JsonProperty("server_description", NullValueHandling = NullValueHandling.Ignore)]
    public string? ServerDescription { get; set; }

    /// <summary>유형(<c>SERVER_STATUS_CHANGED</c> · <c>RESOURCE_THRESHOLD</c> …, 16종 — 모르는 값도 보존).</summary>
    [JsonProperty("type_event")]
    public string? TypeEvent { get; set; }

    /// <summary>심각도(<c>INFO</c> · <c>WARNING</c> · <c>ERROR</c> · <c>CRITICAL</c>).</summary>
    [JsonProperty("severity")]
    public string? Severity { get; set; }

    [JsonProperty("title")]
    public string? Title { get; set; }

    [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
    public string? Message { get; set; }

    /// <summary>상세(JSONB 자유형).</summary>
    [JsonProperty("detail", NullValueHandling = NullValueHandling.Ignore)]
    public JToken? Detail { get; set; }

    [JsonProperty("source", NullValueHandling = NullValueHandling.Ignore)]
    public string? Source { get; set; }

    /// <summary>7.0+ 확인 여부.</summary>
    [JsonProperty("is_acknowledged", NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsAcknowledged { get; set; }

    /// <summary>6.3 NATS 의 옛 이름.</summary>
    [JsonProperty("acknowledged", NullValueHandling = NullValueHandling.Ignore)]
    public bool? AcknowledgedLegacy { get; set; }

    /// <summary>확인한 사람(문자열, 8.0.15).</summary>
    [JsonProperty("acknowledged_by", NullValueHandling = NullValueHandling.Ignore)]
    public string? AcknowledgedBy { get; set; }

    [JsonProperty("acknowledged_at", NullValueHandling = NullValueHandling.Ignore)]
    public string? AcknowledgedAt { get; set; }

    [JsonProperty("created_at", NullValueHandling = NullValueHandling.Ignore)]
    public string? CreatedAt { get; set; }

    [JsonProperty("updated_at", NullValueHandling = NullValueHandling.Ignore)]
    public string? UpdatedAt { get; set; }

    /// <summary>서버 id — 7.0 참조가 먼저, 없으면 6.3 스칼라. 전역 이벤트면 null.</summary>
    [JsonIgnore]
    public int? EffectiveServerId => Server?.Id ?? ServerIdLegacy;

    /// <summary>확인됐는가 — 7.0 <c>is_acknowledged</c> 가 먼저, 없으면 6.3 <c>acknowledged</c>.</summary>
    [JsonIgnore]
    public bool Acknowledged => IsAcknowledged ?? AcknowledgedLegacy ?? false;
}

/// <summary>시스템 이벤트의 서버 참조 <c>{id, category_server}</c>(7.0+).</summary>
public class SystemEventServerRefDto
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("category_server", NullValueHandling = NullValueHandling.Ignore)]
    public string? CategoryServer { get; set; }
}

/// <summary><c>GET /api/system-events/summary</c> 의 data.</summary>
public class SystemEventSummaryDto
{
    [JsonProperty("total_count")]
    public int TotalCount { get; set; }

    [JsonProperty("unacknowledged_count")]
    public int UnacknowledgedCount { get; set; }

    /// <summary>발생한 심각도만 키로 온다(0건은 키 없음).</summary>
    [JsonProperty("by_severity")]
    public Dictionary<string, int>? BySeverity { get; set; }

    [JsonProperty("by_type")]
    public Dictionary<string, int>? ByType { get; set; }

    [JsonProperty("recent_critical")]
    public List<SystemEventDto>? RecentCritical { get; set; }
}

/// <summary>목록 조회 조건(<c>GET /api/system-events</c>). 빈 칸은 보내지 않는다.</summary>
public sealed record SystemEventQuery(
    int Page = 1,
    int Limit = 20,
    int? ServerId = null,
    string? TypeEvent = null,
    string? Severity = null,
    bool? IsAcknowledged = null,
    string? StartDate = null,
    string? EndDate = null);

/// <summary>확인(acknowledge) 결과.</summary>
public enum SystemEventAckOutcome
{
    /// <summary>이번에 확인됐다.</summary>
    Acknowledged,
    /// <summary>이미 확인돼 있었다(7.0+ 409 · 6.3 400) — 실패가 아니다.</summary>
    AlreadyAcknowledged,
    /// <summary>이 서버 판은 시스템 이벤트를 주지 않는다(404).</summary>
    NotSupported,
    /// <summary>그 밖의 실패(권한 · 네트워크 …).</summary>
    Failed,
}

/// <summary>
/// 시스템 이벤트 공급 계약 — REST 구현은 Events.Api(<c>SystemEventApiService</c>), 소비는 서버 콘솔(Devices.Ui).
/// 두 프로젝트가 서로를 참조하지 않아 공용 DTO 자리에 둔다.
/// </summary>
public interface ISystemEventSource
{
    /// <summary>목록. 404 면 <c>StatusCode == 404</c> 그대로(호출자가 "이 서버 판은 제공하지 않음" 을 보인다).</summary>
    Task<ApiListResponse<SystemEventDto>> GetSystemEventsAsync(SystemEventQuery query, CancellationToken token = default);

    /// <summary>단건(SYNC_SYSTEM_EVENT 다시 읽기).</summary>
    Task<ApiResponse<SystemEventDto>> GetSystemEventByIdAsync(int id, CancellationToken token = default);

    /// <summary>확인. 이미 확인된 것(409/400)은 <see cref="SystemEventAckOutcome.AlreadyAcknowledged"/>.</summary>
    Task<SystemEventAckOutcome> AcknowledgeSystemEventAsync(int id, string acknowledgedBy, CancellationToken token = default);
}
