using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
/****************************************************************************
   Purpose      : 초안 → 서버 요청 본문(순수 함수). 저장은 호출 한 번이고,
                  그 한 번에 실리는 값이 전부 여기서 만들어진다.
   Created By   : GHLee
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 억제 스케줄 저장 요청 만들기(순수).
/// </summary>
/// <remarks>
/// <para><b>서버 호출은 [저장] 때 한 번</b>이다 — 서버 계약이 대상 <b>배열</b>(<c>target_device_ids[]</c> ·
/// <c>target_group_ids[]</c>)이라 대상이 100개여도 POST/PATCH 한 번으로 끝난다
/// (정본 all-windows-drag-wireframe.html L420 "스케줄 저장에 포함 1회").</para>
/// <para>⚠ PATCH 는 RFC 7396 이다 — <b>보낸 키만 바뀌고, 배열은 통째로 갈린다</b>.
/// 그래서 <see cref="BuildUpdate"/> 는 <b>빈 DTO 가 아니라 받아 온 원본</b>에서 시작한다.
/// 초안이 손대지 않은 칸은 원본 값이 그대로 실려 나간다.</para>
/// </remarks>
public static class SuppressionRequestBuilder
{
    /// <summary>
    /// 새 스케줄(POST) 본문.
    /// </summary>
    /// <param name="draft">초안.</param>
    /// <param name="unitId">
    /// 소속 부대 id. <b>서버 계약 8.0 이상일 때만</b> 값을 넣는다 —
    /// 6.3.2 · 7.0.1 쓰기 스키마는 <c>extra=forbid</c> 라 이 키가 실리는 순간 422 다.
    /// </param>
    public static EventSuppressionScheduleCreateDto BuildCreate(SuppressionDraft draft, int? unitId = null)
    {
        if (draft is null) throw new ArgumentNullException(nameof(draft));

        return new EventSuppressionScheduleCreateDto
        {
            Name = draft.Name?.Trim() ?? string.Empty,
            Description = Blank(draft.Description),
            UnitId = unitId,
            TargetType = draft.TargetType,
            TargetDeviceIds = draft.DeviceIds,
            TargetGroupIds = draft.GroupIds,
            TargetSide = draft.TargetSide,
            EventScope = draft.EventScope,
            WindowStart = KoreaTimeHelper.ToServerIso8601(draft.WindowStart),
            // ⚠ 무제한은 키를 생략하면 422 — 명시적 null 이어야 한다(서버 model_fields_set 검사).
            WindowEnd = draft.WindowEnd is { } end ? KoreaTimeHelper.ToServerIso8601(end) : null,
            // ⚠ 생성도 같다 — 서버 스키마가 not-null 이면 거절한다(schemas/event_suppression.py:209-213).
            //   null 이면 NullValueHandling.Ignore 로 키가 나가지 않는다. 신규 반복은 recurrence_type 을 쓴다.
            RecurrenceRule = null,
            RecurrenceType = draft.IsWeekly ? "weekly" : "none",
            DaysOfWeek = draft.IsWeekly ? draft.DaysOfWeekMask : null,
            // ⚠ offset/Z 를 붙이면 즉시 422 — 일일 시각은 offset 없는 벽시계다.
            DailyStart = draft.IsWeekly ? SuppressionRules.FormatDailyTime(draft.DailyStart) : null,
            DailyEnd = draft.IsWeekly ? SuppressionRules.FormatDailyTime(draft.DailyEnd) : null,
        };
    }

    /// <summary>
    /// 수정(PATCH) 본문 — <b>받아 온 원본에서 다시 채운다</b>.
    /// </summary>
    /// <param name="draft">초안. <see cref="SuppressionDraft.Baseline"/> 이 있어야 한다.</param>
    /// <param name="sendUnitId">
    /// 서버 계약이 8.0 이상이라 <c>unit_id</c> 키를 실어도 되는가. 거짓이면 키를 보내지 않는다(6.3·7.0 무회귀).
    /// </param>
    /// <exception cref="InvalidOperationException">원본 없이 PATCH 본문을 만들려 할 때 — 빈 DTO 는 값을 지운다.</exception>
    public static EventSuppressionScheduleUpdateDto BuildUpdate(SuppressionDraft draft, bool sendUnitId = false)
    {
        if (draft is null) throw new ArgumentNullException(nameof(draft));
        if (draft.Baseline is null)
            throw new InvalidOperationException(
                "받아 온 원본 없이 PATCH 본문을 만들 수 없습니다 — RFC 7396 이라 빈 DTO 는 값을 지우고 배열을 갈아 버립니다.");

        var baseline = draft.Baseline;

        return new EventSuppressionScheduleUpdateDto
        {
            Name = draft.Name?.Trim() ?? string.Empty,
            // 서랍에 설명 칸이 없다 — 초안은 원본에서 채워져 있으므로 그대로 되돌려 보낸다(지우지 않는다).
            Description = Blank(draft.Description),
            // 6.3·7.0 에 실으면 422. 원본에 값이 있었다는 것 자체가 8.0 응답이라는 뜻이지만, 판정은 호출자가 한다.
            UnitId = sendUnitId ? baseline.UnitId : null,
            TargetType = draft.TargetType,
            TargetDeviceIds = draft.DeviceIds,
            TargetGroupIds = draft.GroupIds,
            TargetSide = draft.TargetSide,
            EventScope = draft.EventScope,
            WindowStart = KoreaTimeHelper.ToServerIso8601(draft.WindowStart),
            WindowEnd = draft.WindowEnd is { } end ? KoreaTimeHelper.ToServerIso8601(end) : null,
            // ⚠ RecurrenceRule 은 **일부러 넣지 않는다**(기본값 null → NullValueHandling.Ignore → 키 자체가 안 나간다).
            //   서버는 이 키가 본문에 **있기만 해도** 422 로 거절한다 — 값이 무엇이든 상관없다:
            //     routers/event_suppression_schedules.py:438  `if "recurrence_rule" in fields: raise 422`
            //     schemas/event_suppression.py:308-312        "**미사용 레거시 — 값을 보내면 422**"
            //   원본에서 되돌려 보내면(구버전 행에 값이 남아 있는 경우) 이름만 고치는 PATCH 까지 죽는다.
        };
    }

    /// <summary>
    /// 저장 성공 뒤 보여 줄 대상 확인 문구 — 서버가 확정한 id 를 <b>이름</b>으로 되풀이하고, 지금 상태를 괄호로 붙인다.
    /// </summary>
    /// <remarks>
    /// 예전엔 "…에 억제 스케줄을 적용했습니다" 였다 — 시작 전(예정) 스케줄에도 '적용' 이라고 해 지금 억제가 걸린 것처럼
    /// 읽혔다(실창 검토 #27). 저장은 저장이다: "저장했습니다" + 상태(<see cref="SavedStateNote"/>).
    /// </remarks>
    public static string TargetEcho(EventSuppressionScheduleDto saved,
                                    Func<int, string>? resolveDevice = null,
                                    Func<int, string>? resolveGroup = null,
                                    int nameLimit = 3)
    {
        if (saved is null) return string.Empty;

        switch (saved.TargetType)
        {
            case SuppressionTargetDrop.ModeDevice:
            {
                var ids = saved.TargetDeviceIds ?? new List<int>();
                return Sentence("장비", ids.Count, ids.Take(nameLimit).Select(id => resolveDevice?.Invoke(id) ?? $"#{id}"));
            }
            case SuppressionTargetDrop.ModeGroup:
            {
                var ids = saved.TargetGroupIds ?? new List<int>();
                return Sentence("그룹", ids.Count, ids.Take(nameLimit).Select(id => resolveGroup?.Invoke(id) ?? $"#{id}"));
            }
            default:
                return $"전체 대상 · {SideLabel(saved.TargetSide)}에 억제 스케줄을 저장했습니다{SavedStateNote(saved)}.";
        }

        string Sentence(string kind, int total, IEnumerable<string> sample)
        {
            var names = sample.ToList();
            if (total == 0) return $"대상 {kind}이(가) 없습니다.";
            var rest = total - names.Count;
            var subject = rest > 0 ? $"{string.Join(", ", names)} 외 {rest}개 {kind}" : $"{string.Join(", ", names)}({kind} {total}개)";
            return $"{subject}에 억제 스케줄을 저장했습니다{SavedStateNote(saved)}.";
        }
    }

    /// <summary>
    /// 저장한 스케줄의 지금 상태 — 확인 문구 끝 괄호. 서버가 준 상태(<c>status</c> · <c>is_suppressing_now</c>)만 읽는다
    /// (화면이 시각으로 다시 계산하지 않는다 — 억제 여부의 권위는 서버다). 모르면 붙이지 않는다.
    /// </summary>
    public static string SavedStateNote(EventSuppressionScheduleDto saved)
    {
        if (saved is null || string.IsNullOrWhiteSpace(saved.Status)) return string.Empty;
        return SuppressionStatusView.Resolve(saved.Status, saved.IsSuppressingNow == true) switch
        {
            SuppressionStatusShape.Scheduled => "(시작 전)",
            SuppressionStatusShape.Suppressing => "(지금 억제 중)",
            SuppressionStatusShape.InWindow => "(진행중 · 지금은 억제 시간 밖)",
            SuppressionStatusShape.Ended => "(이미 끝난 기간)",
            _ => string.Empty,
        };
    }

    /// <summary>감지/감시 코드 → 표시 문구.</summary>
    public static string SideLabel(string? side) => side switch
    {
        "detection" => "감지",
        "surveillance" => "감시",
        _ => "감지+감시",
    };

    /// <summary>
    /// 억제 범위 코드 — 서버 <c>EnumSuppressionEventScope</c> 5값(<c>app/utils/enums.py:295-302</c>).
    /// </summary>
    public static readonly string[] KnownScopes = { "all", "detection", "malfunction", "connection", "operation" };

    /// <summary>화면이 아는 범위인가. 모르는 값은 <b>고치지 않고 보존</b>하고 저장을 막는다.</summary>
    public static bool IsKnownScope(string? scope)
        => scope is not null && System.Array.IndexOf(KnownScopes, scope) >= 0;

    /// <summary>억제 범위 코드 → 표시 문구.</summary>
    public static string ScopeLabel(string? scope) => scope switch
    {
        "detection" => "탐지",
        "malfunction" => "장애",
        "connection" => "연결",
        "operation" => "운영",
        "all" => "전체",
        null => "전체",
        // 모르는 값은 지어내지 않되, 서버 원문을 운영자 화면에 찍지도 않는다 — 원문은 툴팁(ScopeToolTip)이 보인다.
        _ => UnknownLabel,
    };

    /// <summary>표시 사전에 없는 값의 표기.</summary>
    public const string UnknownLabel = "알 수 없음";

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
