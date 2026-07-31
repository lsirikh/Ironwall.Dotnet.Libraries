using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;
/****************************************************************************
   Purpose      : 이벤트 억제(정비 창) 스케줄 DTO — 복수 대상(장비 복수 / 그룹 복수 / 전체).
                  서버 /api/event-suppression-schedules 계약(event-suppression-multi-target).
   Created By   : GHLee
   Created On   : 2026-08-01
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 이벤트 억제 스케줄 <b>응답</b> DTO(GET/목록/active/단건 + POST/PATCH/DELETE 응답).
/// <para>대상 모드 배타(device/group/all), 대상은 모드 내 복수(target_device_ids[]/target_group_ids[]).</para>
/// <para>status(파생)·is_active·revoked_at 등은 서버 계산/읽기전용 — 요청에는 <see cref="EventSuppressionScheduleRequestDto"/> 사용.</para>
/// </summary>
public class EventSuppressionScheduleDto : BaseDto   // id / created_at / updated_at 상속
{
    [JsonProperty("name", Order = 2)]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("description", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    /// <summary>대상 모드(배타): device / group / all</summary>
    [JsonProperty("target_type", Order = 4)]
    public string TargetType { get; set; } = "device";

    /// <summary>대상 장비 ID 목록(target_type=device 시 복수). 그 외 빈 배열.</summary>
    [JsonProperty("target_device_ids", Order = 5)]
    public List<int> TargetDeviceIds { get; set; } = new();

    /// <summary>대상 그룹 ID 목록(target_type=group 시 복수). 그 외 빈 배열.</summary>
    [JsonProperty("target_group_ids", Order = 6)]
    public List<int> TargetGroupIds { get; set; } = new();

    /// <summary>감지/감시 필터(group·all 적용): detection / surveillance / both(기본)</summary>
    [JsonProperty("target_side", Order = 7)]
    public string TargetSide { get; set; } = "both";

    /// <summary>억제 이벤트 유형: connection / detection / malfunction / all</summary>
    [JsonProperty("event_scope", Order = 8)]
    public string EventScope { get; set; } = "all";

    /// <summary>억제 시작(ISO8601 +09:00)</summary>
    [JsonProperty("window_start", Order = 9)]
    public string WindowStart { get; set; } = string.Empty;

    /// <summary>억제 종료(ISO8601 +09:00, 필수)</summary>
    [JsonProperty("window_end", Order = 10)]
    public string WindowEnd { get; set; } = string.Empty;

    [JsonProperty("recurrence_rule", Order = 11, NullValueHandling = NullValueHandling.Ignore)]
    public string? RecurrenceRule { get; set; }

    /// <summary>sweep 비정규화 플래그(표시용). 억제 권위는 <see cref="Status"/>.</summary>
    [JsonProperty("is_active", Order = 12, NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsActive { get; set; }

    /// <summary>파생 상태(읽기전용): pending / active / expired / cancelled</summary>
    [JsonProperty("status", Order = 13, NullValueHandling = NullValueHandling.Ignore)]
    public string? Status { get; set; }

    /// <summary>soft-cancel 시각(ISO8601). null=미취소.</summary>
    [JsonProperty("revoked_at", Order = 14, NullValueHandling = NullValueHandling.Ignore)]
    public string? RevokedAt { get; set; }

    [JsonProperty("created_by", Order = 15, NullValueHandling = NullValueHandling.Ignore)]
    public int? CreatedBy { get; set; }
}

/// <summary>
/// 이벤트 억제 스케줄 <b>요청</b> DTO(POST 생성 / PATCH 수정).
/// <para>⚠ 서버 Create/Update 스키마는 <c>extra="forbid"</c> — <b>편집 필드만</b> 전송해야 한다
/// (id/status/is_active/revoked_at/created_at 등을 보내면 요청이 거부됨). 그래서 BaseDto를 상속하지 않는다.</para>
/// <para>시간은 KST aware ISO8601(+09:00)로 채운다(KoreaTimeHelper.ToServerIso8601).</para>
/// </summary>
public class EventSuppressionScheduleRequestDto
{
    [JsonProperty("name", Order = 1)]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("description", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    [JsonProperty("target_type", Order = 3)]
    public string TargetType { get; set; } = "device";

    [JsonProperty("target_device_ids", Order = 4)]
    public List<int> TargetDeviceIds { get; set; } = new();

    [JsonProperty("target_group_ids", Order = 5)]
    public List<int> TargetGroupIds { get; set; } = new();

    [JsonProperty("target_side", Order = 6)]
    public string TargetSide { get; set; } = "both";

    [JsonProperty("event_scope", Order = 7)]
    public string EventScope { get; set; } = "all";

    [JsonProperty("window_start", Order = 8)]
    public string WindowStart { get; set; } = string.Empty;

    [JsonProperty("window_end", Order = 9)]
    public string WindowEnd { get; set; } = string.Empty;

    [JsonProperty("recurrence_rule", Order = 10, NullValueHandling = NullValueHandling.Ignore)]
    public string? RecurrenceRule { get; set; }
}
