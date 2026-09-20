using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
/****************************************************************************
   Purpose      : 억제 스케줄 편집 서랍의 Draft(초안) — 화면 없는 순수 상태.
                  서버는 [저장] 한 번에만 불린다. 이 객체는 그 한 번에 실릴 값 전부다.
   Created By   : GHLee
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 억제 스케줄 초안. <b>서버를 모른다</b> — 검증(<see cref="SuppressionFormRules"/>)과
/// 요청 만들기(<see cref="SuppressionRequestBuilder"/>)가 이 객체만 읽는다.
/// </summary>
/// <remarks>
/// <para>시각은 전부 <see cref="DateTimeOffset"/> 이다 — 서버로 나가는 값은 <b>offset 이 붙은 ISO 8601</b> 이어야 하고
/// (memory: GIS→서버 datetime aware), 불러온 스케줄을 고치지 않고 다시 보낼 때 <b>원래 offset 이 보존</b>돼야 한다.</para>
/// <para><see cref="Baseline"/> 은 서버에서 받아 온 원본이다. PATCH 는 RFC 7396 이라
/// <b>빈 DTO 로 만들면 null 이 값을 지우고 배열이 통째로 갈린다</b> — 그래서 초안은 언제나 원본에서 채워 시작한다.</para>
/// </remarks>
public sealed class SuppressionDraft
{
    /// <summary>새 스케줄 초안.</summary>
    public SuppressionDraft()
    {
    }

    /// <summary>서버 id. <c>null</c> 이면 아직 만들어지지 않은 새 스케줄이다.</summary>
    public int? Id { get; set; }

    /// <summary>새 스케줄인가 — 저장이 POST 인지 PATCH 인지를 가른다.</summary>
    public bool IsNew => Id is null or <= 0;

    /// <summary>서버에서 받아 온 원본(수정일 때만 있다). PATCH 본문을 여기서 다시 채운다.</summary>
    public EventSuppressionScheduleDto? Baseline { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>대상 유형 — <c>device</c> / <c>group</c> / <c>all</c>.</summary>
    public string TargetType { get; set; } = SuppressionTargetDrop.ModeDevice;

    /// <summary>감지/감시 필터(group·all 에서만 뜻이 있다) — <c>both</c> / <c>detection</c> / <c>surveillance</c>.</summary>
    public string TargetSide { get; set; } = "both";

    /// <summary>억제 범위 — <c>all</c> / <c>detection</c> / <c>malfunction</c> / <c>connection</c>.</summary>
    public string EventScope { get; set; } = "all";

    /// <summary>억제(유효기간) 시작.</summary>
    public DateTimeOffset WindowStart { get; set; }

    /// <summary>억제(유효기간) 끝. <c>null</c> = 무제한 — <b>주간 반복에서만</b> 허용된다.</summary>
    public DateTimeOffset? WindowEnd { get; set; }

    /// <summary>주간 반복인가.</summary>
    public bool IsWeekly { get; set; }

    /// <summary>요일 비트마스크(월1 … 일64). 서버 원점은 <b>월=0</b>.</summary>
    public int DaysOfWeekMask { get; set; } = SuppressionRules.DaysWeekdayPreset;

    /// <summary>일일 시작 시각(벽시계 — offset 없음).</summary>
    public TimeSpan DailyStart { get; set; } = TimeSpan.FromHours(8);

    /// <summary>일일 종료 시각. 시작보다 이르면 자정 넘김.</summary>
    public TimeSpan DailyEnd { get; set; } = TimeSpan.FromHours(21);

    /// <summary>대상 칩(장비 또는 그룹). <c>all</c> 이면 비어 있다.</summary>
    public List<SuppressionTargetChip> Targets { get; } = new();

    /// <summary>무제한 창인가(끝이 없다).</summary>
    public bool IsUnlimited => WindowEnd is null;

    /// <summary>
    /// 반복 규칙을 고칠 수 있는가 — <b>새 스케줄일 때만</b>.
    /// <para>서버 <c>EventSuppressionScheduleUpdate</c> 스키마에는 반복 4필드가 <b>없다</b>(<c>extra=forbid</c>).
    /// 수정에 반복 필드를 실으면 그 PATCH 는 전부 422 다 — 그래서 수정 화면에서는 잠근다.</para>
    /// </summary>
    public bool CanEditRecurrence => IsNew;

    /// <summary>반복 모드(서버 <c>recurrence_type</c> 대응).</summary>
    public SuppressionRecurrenceMode RecurrenceMode => IsWeekly ? SuppressionRecurrenceMode.Weekly : SuppressionRecurrenceMode.None;

    /// <summary>대상 장비 id(장비 모드에서만).</summary>
    public List<int> DeviceIds => TargetType == SuppressionTargetDrop.ModeDevice
        ? Targets.Where(t => t.Kind == SuppressionTargetKind.Device).Select(t => t.Id).ToList()
        : new List<int>();

    /// <summary>대상 그룹 id(그룹 모드에서만).</summary>
    public List<int> GroupIds => TargetType == SuppressionTargetDrop.ModeGroup
        ? Targets.Where(t => t.Kind == SuppressionTargetKind.Group).Select(t => t.Id).ToList()
        : new List<int>();

    /// <summary>
    /// 새 스케줄 초안 — 기본값은 '지금부터 1시간 단발'.
    /// </summary>
    /// <param name="now">시계(<see cref="Base.Services.IClock"/>)에서 온 현재 시각. <c>DateTime.Now</c> 를 직접 부르지 않는다.</param>
    public static SuppressionDraft NewSchedule(DateTimeOffset now) => new()
    {
        WindowStart = now,
        WindowEnd = now.AddHours(1),
    };

    /// <summary>
    /// 서버에서 받아 온 스케줄로 초안을 채운다 — <b>PATCH 는 여기서 시작한다</b>.
    /// </summary>
    /// <param name="dto">목록 · 단건 조회로 받은 원본.</param>
    /// <param name="resolveDevice">장비 id → 칩 이름(찾지 못하면 <c>#id</c>).</param>
    /// <param name="resolveGroup">그룹 id → 칩 이름.</param>
    public static SuppressionDraft FromDto(EventSuppressionScheduleDto dto,
                                           Func<int, string>? resolveDevice = null,
                                           Func<int, string>? resolveGroup = null)
    {
        if (dto is null) throw new ArgumentNullException(nameof(dto));

        var draft = new SuppressionDraft
        {
            Id = dto.Id,
            Baseline = dto,
            Name = dto.Name ?? string.Empty,
            Description = dto.Description,
            TargetType = string.IsNullOrWhiteSpace(dto.TargetType) ? SuppressionTargetDrop.ModeDevice : dto.TargetType,
            TargetSide = string.IsNullOrWhiteSpace(dto.TargetSide) ? "both" : dto.TargetSide,
            EventScope = string.IsNullOrWhiteSpace(dto.EventScope) ? "all" : dto.EventScope,
            WindowStart = ParseOffset(dto.WindowStart) ?? default,
            WindowEnd = ParseOffset(dto.WindowEnd),
            IsWeekly = string.Equals(dto.RecurrenceType, "weekly", StringComparison.OrdinalIgnoreCase),
        };

        if (draft.IsWeekly)
        {
            draft.DaysOfWeekMask = dto.DaysOfWeek ?? SuppressionRules.DaysWeekdayPreset;
            if (TryTime(dto.DailyStart, out var s)) draft.DailyStart = s;
            if (TryTime(dto.DailyEnd, out var e)) draft.DailyEnd = e;
        }

        foreach (var id in dto.TargetDeviceIds ?? new List<int>())
            draft.Targets.Add(new SuppressionTargetChip(SuppressionTargetKind.Device, id, resolveDevice?.Invoke(id) ?? $"#{id}"));
        foreach (var id in dto.TargetGroupIds ?? new List<int>())
            draft.Targets.Add(new SuppressionTargetChip(SuppressionTargetKind.Group, id, resolveGroup?.Invoke(id) ?? $"#{id}"));

        return draft;
    }

    /// <summary>
    /// 서버 ISO 8601 을 offset 을 보존한 채 읽는다. 값이 없으면 <c>null</c>(= 무제한).
    /// </summary>
    public static DateTimeOffset? ParseOffset(string? iso)
        => string.IsNullOrWhiteSpace(iso)
            ? null
            : DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : null;

    private static bool TryTime(string? text, out TimeSpan value)
        => TimeSpan.TryParseExact(text, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out value)
           || TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out value);
}
