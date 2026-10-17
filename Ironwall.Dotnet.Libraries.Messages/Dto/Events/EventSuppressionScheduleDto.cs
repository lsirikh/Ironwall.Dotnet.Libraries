using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Events;
/****************************************************************************
   Purpose      : 이벤트 억제(정비 창) 스케줄 DTO — 복수 대상(장비 복수 / 그룹 복수 / 전체)
                  + API 6.3.3 주간 반복.
                  서버 /api/event-suppression-schedules 계약.
   Created By   : GHLee
   Created On   : 2026-08-01
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 이벤트 억제 스케줄 <b>응답</b> DTO(GET/목록/active/단건 + POST/PATCH/DELETE 응답).
/// <para>대상 모드 배타(device/group/all), 대상은 모드 내 복수(target_device_ids[]/target_group_ids[]).</para>
/// <para>status(파생)·is_active·revoked_at 등은 서버 계산/읽기전용 — 요청에는
/// <see cref="EventSuppressionScheduleCreateDto"/> / <see cref="EventSuppressionScheduleUpdateDto"/> 사용.</para>
/// </summary>
public class EventSuppressionScheduleDto : BaseDto   // id / created_at / updated_at 상속
{
    [JsonProperty("name", Order = 2)]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("description", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    /// <summary>
    /// 소속 부대 id. 서버 <b>8.0+ 응답에서 <c>required</c></b>, 6.3.2·7.0.1 응답에는 없어 <c>null</c> 이다(읽기 전용).
    /// <para>8.0 부터 <c>target_type=all</c> 의 사정거리가 "그 부대 + 예하" 로 바뀌므로 UI 문구는 이 값을 참조한다.</para>
    /// </summary>
    [JsonProperty("unit_id", Order = 25, NullValueHandling = NullValueHandling.Ignore)]
    public int? UnitId { get; set; }

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

    /// <summary>
    /// 억제 시작(ISO8601 +09:00).
    /// <para>recurrence_type=weekly 이면 <b>유효기간 시작</b>을 뜻한다.</para>
    /// </summary>
    [JsonProperty("window_start", Order = 9)]
    public string WindowStart { get; set; } = string.Empty;

    /// <summary>
    /// 억제 종료(ISO8601 +09:00). <b><c>null</c> = 무제한 창</b>(주간 반복 전용).
    /// <para>recurrence_type=weekly 이면 <b>유효기간 끝</b>을 뜻한다.</para>
    /// ⚠ non-nullable 로 두면 무제한 창을 수신할 때 '값 없음'과 구분되지 않는다.
    /// </summary>
    [JsonProperty("window_end", Order = 10)]
    public string? WindowEnd { get; set; }

    /// <summary>⚠ <b>미사용 레거시</b> — 항상 null. 서버가 Phase 2 RRULE 용으로 보존한다.
    /// 신규 반복은 <see cref="RecurrenceType"/> 를 쓴다.</summary>
    [JsonProperty("recurrence_rule", Order = 11, NullValueHandling = NullValueHandling.Ignore)]
    public string? RecurrenceRule { get; set; }

    /// <summary>sweep 비정규화 플래그(표시용). 억제 권위는 <see cref="IsSuppressingNow"/>.</summary>
    [JsonProperty("is_active", Order = 12, NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsActive { get; set; }

    /// <summary>파생 상태(읽기전용): pending / active / expired / cancelled.
    /// <para>⚠ <b>"유효기간 안"이라는 뜻일 뿐 "지금 억제 중"이 아니다.</b></para></summary>
    [JsonProperty("status", Order = 13, NullValueHandling = NullValueHandling.Ignore)]
    public string? Status { get; set; }

    /// <summary>soft-cancel 시각(ISO8601). null=미취소.</summary>
    [JsonProperty("revoked_at", Order = 14, NullValueHandling = NullValueHandling.Ignore)]
    public string? RevokedAt { get; set; }

    [JsonProperty("created_by", Order = 15, NullValueHandling = NullValueHandling.Ignore)]
    public int? CreatedBy { get; set; }

    #region - 주간 반복 (API 6.3.3) -

    /// <summary>반복 유형: <c>none</c>(기본) / <c>weekly</c>.</summary>
    [JsonProperty("recurrence_type", Order = 16, NullValueHandling = NullValueHandling.Ignore)]
    public string? RecurrenceType { get; set; }

    /// <summary>요일 비트마스크 — 월1 화2 수4 목8 금16 토32 일64 (서버 원점 <b>월=0</b>).</summary>
    [JsonProperty("days_of_week", Order = 17, NullValueHandling = NullValueHandling.Ignore)]
    public int? DaysOfWeek { get; set; }

    /// <summary>일일 시작 <c>"HH:mm:ss"</c>.
    /// <para>⚠ <b>offset 없는 벽시계</b>다. <c>DateTime</c> 으로 받으면 '오늘 08:00'으로 오염된다.</para></summary>
    [JsonProperty("daily_start", Order = 18, NullValueHandling = NullValueHandling.Ignore)]
    public string? DailyStart { get; set; }

    /// <summary>일일 종료 <c>"HH:mm:ss"</c>. <c>daily_end &lt; daily_start</c> 면 자정 넘김, 같으면 24시간 종일.</summary>
    [JsonProperty("daily_end", Order = 19, NullValueHandling = NullValueHandling.Ignore)]
    public string? DailyEnd { get; set; }

    /// <summary>서버 <c>DISPLAY_TIMEZONE</c>(읽기전용). <b>요청으로 보내면 422</b> — UI 미노출.</summary>
    [JsonProperty("schedule_tz", Order = 20, NullValueHandling = NullValueHandling.Ignore)]
    public string? ScheduleTz { get; set; }

    /// <summary>
    /// ⚠ <b>억제 여부의 표시 권위.</b> null(구버전 서버 응답)이면 <c>Status=="active"</c> 로 폴백한다.
    /// <para><c>bool</c> 로 선언하면 필드 부재 시 조용히 <c>false</c>(억제 안 함)로 굳어 <b>안전 방향의 반대</b>가 된다.</para>
    /// </summary>
    [JsonProperty("is_suppressing_now", Order = 21, NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsSuppressingNow { get; set; }

    /// <summary>현재 회차 시작(표시용).</summary>
    [JsonProperty("occurrence_start", Order = 22, NullValueHandling = NullValueHandling.Ignore)]
    public string? OccurrenceStart { get; set; }

    /// <summary>현재 회차 종료. null = 지금 회차가 아님.
    /// <para>⚠ 유효기간 경계에서 <b>잘릴 수 있다</b> — 요약 문자열로 재계산하지 말 것.</para></summary>
    [JsonProperty("occurrence_end", Order = 23, NullValueHandling = NullValueHandling.Ignore)]
    public string? OccurrenceEnd { get; set; }

    /// <summary>다음 억제 예정 시각. 진행 중이면 null.</summary>
    [JsonProperty("next_occurrence_start", Order = 24, NullValueHandling = NullValueHandling.Ignore)]
    public string? NextOccurrenceStart { get; set; }

    #endregion
}

/// <summary>
/// 억제 스케줄 <b>수정(PATCH)</b> 요청 DTO.
/// <para>⚠ 서버 <c>EventSuppressionScheduleUpdate</c> 는 <c>extra="forbid"</c> 이고
/// <b>반복 4필드가 없다</b> — 여기에 반복 필드를 추가하면 <b>모든 PATCH 가 422</b> 가 된다.
/// 그래서 <see cref="EventSuppressionScheduleCreateDto"/> 와 <b>상속 관계를 두지 않는다</b>(sealed).</para>
/// </summary>
public sealed class EventSuppressionScheduleUpdateDto
{
    [JsonProperty("name", Order = 1)]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("description", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    /// <summary>
    /// 소속 부대 id. <b>서버 8.0 이상에서만 존재하는 키</b>다.
    /// <para>⚠ <b>계약 세대가 <c>&gt;= EnumServerContract.V8_0</c> 일 때만 값을 넣는다.</b>
    /// 6.3.2·7.0.1 쓰기 스키마는 <c>extra="forbid"</c> 라 이 키가 실리는 순간 422 <c>extra_forbidden</c> 이고
    /// 억제 생성·수정이 통째로 죽는다(실측). <c>null</c> 이면 <c>NullValueHandling.Ignore</c> 로 전송되지 않는다 —
    /// <b>기본값 유지가 곧 6.3.2 무회귀</b>다.</para>
    /// </summary>
    [JsonProperty("unit_id", Order = 15, NullValueHandling = NullValueHandling.Ignore)]
    public int? UnitId { get; set; }

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

    /// <summary>⚠ <b><c>NullValueHandling.Ignore</c> 금지</b> — 무제한 창은 키를 <b>명시적 null</b> 로
    /// 보내야 하고, 키가 없으면 서버가 <b>422</b> 로 거절한다(<c>model_fields_set</c> 검사).</summary>
    [JsonProperty("window_end", Order = 9)]
    public string? WindowEnd { get; set; }

    [JsonProperty("recurrence_rule", Order = 10, NullValueHandling = NullValueHandling.Ignore)]
    public string? RecurrenceRule { get; set; }
}

/// <summary>
/// 억제 스케줄 <b>생성(POST)</b> 요청 DTO. 서버 Create 스키마에만 있는 반복 4필드를 보유한다.
/// <para>⚠ <see cref="EventSuppressionScheduleUpdateDto"/> 를 <b>상속하지 않는다</b> —
/// 상속하면 "PATCH 에 반복 필드가 못 들어간다"는 컴파일 타임 보증이 무너진다.</para>
/// </summary>
public sealed class EventSuppressionScheduleCreateDto
{
    [JsonProperty("name", Order = 1)]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("description", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    /// <summary>
    /// 소속 부대 id. <b>서버 8.0 이상에서만 존재하는 키</b>다.
    /// <para>⚠ <b>계약 세대가 <c>&gt;= EnumServerContract.V8_0</c> 일 때만 값을 넣는다.</b>
    /// 6.3.2·7.0.1 쓰기 스키마는 <c>extra="forbid"</c> 라 이 키가 실리는 순간 422 <c>extra_forbidden</c> 이고
    /// 억제 생성·수정이 통째로 죽는다(실측). <c>null</c> 이면 <c>NullValueHandling.Ignore</c> 로 전송되지 않는다 —
    /// <b>기본값 유지가 곧 6.3.2 무회귀</b>다.</para>
    /// </summary>
    [JsonProperty("unit_id", Order = 15, NullValueHandling = NullValueHandling.Ignore)]
    public int? UnitId { get; set; }

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

    /// <summary>⚠ <b><c>NullValueHandling.Ignore</c> 금지</b> — 무제한은 <b>명시적 null</b> 전송.</summary>
    [JsonProperty("window_end", Order = 9)]
    public string? WindowEnd { get; set; }

    [JsonProperty("recurrence_rule", Order = 10, NullValueHandling = NullValueHandling.Ignore)]
    public string? RecurrenceRule { get; set; }

    #region - 주간 반복 (weekly 일 때만 전송) -

    /// <summary><c>none</c>(기본) / <c>weekly</c>.</summary>
    [JsonProperty("recurrence_type", Order = 11)]
    public string RecurrenceType { get; set; } = "none";

    /// <summary>요일 비트마스크(월=1 … 일=64). weekly 필수.</summary>
    [JsonProperty("days_of_week", Order = 12)]
    public int? DaysOfWeek { get; set; }

    /// <summary>일일 시작 <c>"HH:mm:ss"</c>. ⚠ <b>offset/Z 를 붙이면 즉시 422.</b>
    /// <para><c>DateTime</c>/<c>DateTimeOffset</c> 을 여기 노출하면 공통 직렬화 설정
    /// (<c>ApiService._jsonSettings.DateTimeZoneHandling = Local</c>)이 offset 을 붙여 버린다.
    /// 그래서 타입이 <b>string</b> 이다.</para></summary>
    [JsonProperty("daily_start", Order = 13)]
    public string? DailyStart { get; set; }

    /// <summary>일일 종료 <c>"HH:mm:ss"</c>. ⚠ offset 금지.</summary>
    [JsonProperty("daily_end", Order = 14)]
    public string? DailyEnd { get; set; }

    // ── 단발일 때 반복 4필드를 본문에서 제거한다 ─────────────────────────
    //    서버 Create 스키마: recurrence_type=none 인데 반복 필드가 오면 422
    //    ("{n} is only allowed when recurrence_type=weekly").
    //    레포 선례: SpeakerDeviceDto.ShouldSerializeServer()

    private bool IsWeekly => string.Equals(RecurrenceType, "weekly", System.StringComparison.OrdinalIgnoreCase);

    /// <summary>weekly 일 때만 직렬화한다.</summary>
    public bool ShouldSerializeDaysOfWeek() => IsWeekly;
    /// <summary>weekly 일 때만 직렬화한다.</summary>
    public bool ShouldSerializeDailyStart() => IsWeekly;
    /// <summary>weekly 일 때만 직렬화한다.</summary>
    public bool ShouldSerializeDailyEnd() => IsWeekly;

    #endregion
}

/// <summary>
/// 취소/종료(terminal) 억제 스케줄 <b>일괄 하드삭제</b> 요청(POST /bulk-delete).
/// <para>soft-cancel(DELETE)과 달리 목록에서 물리 제거(복구 불가). 서버는 활성/예정은 skip.</para>
/// </summary>
public class EventSuppressionBulkDeleteRequestDto
{
    [JsonProperty("ids")]
    public List<int> Ids { get; set; } = new();
}

/// <summary>일괄 하드삭제 결과 — 삭제/스킵(활성·예정)/미존재 분리.</summary>
public class EventSuppressionBulkDeleteResultDto
{
    [JsonProperty("deleted_ids")]
    public List<int> DeletedIds { get; set; } = new();

    [JsonProperty("skipped_ids")]
    public List<int> SkippedIds { get; set; } = new();

    [JsonProperty("not_found_ids")]
    public List<int> NotFoundIds { get; set; } = new();
}
