using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
/****************************************************************************
   Purpose      : 억제 편집 서랍의 폼 검증 — 순수 함수(화면 없음).
                  서버가 422 로 막을 입력을 [저장] 전에 걸러 낸다.
   Created By   : GHLee
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>검증에서 걸린 칸 하나.</summary>
/// <param name="Field">칸 키 — 자동화 식별자 꼬리와 같다(<c>name</c>/<c>targets</c>/<c>window</c>/<c>recurrence</c>).</param>
/// <param name="Message">사람이 읽는 한 줄.</param>
public readonly record struct SuppressionFormError(string Field, string Message);

/// <summary>폼 검증 결과.</summary>
/// <param name="Errors">고쳐야 저장되는 것들(빈 목록이면 저장 가능).</param>
/// <param name="Warnings">막지는 않지만 알려야 하는 것들(중복 창 등).</param>
public sealed record SuppressionFormVerdict(IReadOnlyList<SuppressionFormError> Errors,
                                            IReadOnlyList<string> Warnings)
{
    /// <summary>저장 버튼을 켤 것인가.</summary>
    public bool CanSave => Errors.Count == 0;

    /// <summary>폼 아래 붉은 한 줄 — 첫 오류만 보인다(여러 줄은 서랍을 밀어낸다).</summary>
    public string FirstErrorText => Errors.Count == 0 ? string.Empty : Errors[0].Message;

    /// <summary>그 칸이 걸렸는가 — 칸 옆 표지에 쓴다.</summary>
    public bool Has(string field) => Errors.Any(e => string.Equals(e.Field, field, StringComparison.Ordinal));
}

/// <summary>
/// 억제 폼의 검증 규칙(순수). 시간은 전부 <b>인자로 받는다</b> — <c>DateTime.Now</c> 를 부르지 않는다.
/// </summary>
/// <remarks>
/// 기존 단발 폼 규칙(<see cref="SuppressionRules"/>)을 그대로 재사용하고,
/// 서랍이 새로 지는 것(대상 상한 · 자기 자신 제외 겹침)만 여기에 더한다.
/// </remarks>
public static class SuppressionFormRules
{
    /// <summary>칸 키 — 화면 · 테스트가 같은 문자열을 쓴다.</summary>
    public const string FieldName = "name";
    public const string FieldTargets = "targets";
    public const string FieldWindow = "window";
    public const string FieldRecurrence = "recurrence";

    /// <summary>
    /// 초안 하나를 검증한다.
    /// </summary>
    /// <param name="draft">검증할 초안.</param>
    /// <param name="now">현재 시각(시계에서 온 값). 지금은 경고 계산에만 쓴다.</param>
    /// <param name="others">서버에 이미 있는 스케줄들 — 겹침 경고용. <paramref name="draft"/> 자신은 id 로 빠진다.</param>
    public static SuppressionFormVerdict Validate(SuppressionDraft draft,
                                                  DateTimeOffset now,
                                                  IEnumerable<EventSuppressionScheduleDto>? others = null)
    {
        if (draft is null) throw new ArgumentNullException(nameof(draft));

        var errors = new List<SuppressionFormError>();
        var warnings = new List<string>();

        // ── 작업명 ──────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(draft.Name))
            errors.Add(new SuppressionFormError(FieldName, "작업명을 입력하세요."));

        // ── 대상 ────────────────────────────────────────────────────────
        if (SuppressionTargetDrop.AcceptsTargets(draft.TargetType))
        {
            var wanted = SuppressionTargetDrop.KindFor(draft.TargetType)!.Value;
            var mine = draft.Targets.Where(t => t.Kind == wanted).ToList();

            if (mine.Count == 0)
                errors.Add(new SuppressionFormError(FieldTargets,
                    wanted == SuppressionTargetKind.Device
                        ? "대상 장비를 1개 이상 담으세요. 목록에서 끌어 놓거나 [추가 ▶]를 누르세요."
                        : "대상 그룹을 1개 이상 담으세요. 목록에서 끌어 놓거나 [추가 ▶]를 누르세요."));

            if (mine.Count > SuppressionTargetDrop.MaxTargets)
                errors.Add(new SuppressionFormError(FieldTargets,
                    $"대상은 한 스케줄에 {SuppressionTargetDrop.MaxTargets}개까지 담을 수 있습니다(지금 {mine.Count}개)."));

            if (mine.Select(t => t.Key).Distinct(StringComparer.Ordinal).Count() != mine.Count)
                errors.Add(new SuppressionFormError(FieldTargets, "같은 대상이 두 번 담겼습니다."));
        }

        // ── 시간창 ──────────────────────────────────────────────────────
        // 무제한은 주간 반복 전용이다 — 단발 + 종료 없음은 서버가 422 로 막는다.
        if (draft.IsUnlimited && !draft.IsWeekly)
            errors.Add(new SuppressionFormError(FieldWindow, "단발 억제에는 종료 시각이 있어야 합니다. 기간 제한 없음은 주간 반복에서만 쓸 수 있습니다."));
        else if (!draft.IsUnlimited && draft.WindowEnd is { } end && end <= draft.WindowStart)
            errors.Add(new SuppressionFormError(FieldWindow, "종료가 시작보다 뒤여야 합니다."));

        if (!SuppressionRules.IsWindowLengthValidFor(draft.WindowStart.DateTime,
                                                     draft.WindowEnd?.DateTime ?? draft.WindowStart.DateTime,
                                                     draft.RecurrenceMode,
                                                     draft.IsUnlimited))
        {
            var max = SuppressionRules.MaxWindowDaysFor(draft.RecurrenceMode);
            errors.Add(new SuppressionFormError(FieldWindow,
                draft.IsWeekly
                    ? $"유효기간은 최대 {max}일입니다. 더 길게 하려면 [기간 제한 없음]을 켜세요."
                    : $"억제 기간은 최대 {max}일입니다."));
        }

        // ── 주간 반복 ───────────────────────────────────────────────────
        if (draft.IsWeekly)
        {
            var basic = SuppressionRules.ValidateWeeklyForm(draft.DaysOfWeekMask, draft.DailyStart, draft.DailyEnd);
            if (basic is not null)
                errors.Add(new SuppressionFormError(FieldRecurrence, basic));
            else
            {
                var unreachable = SuppressionRules.DescribeUnreachable(
                    draft.DaysOfWeekMask, draft.DailyStart, draft.DailyEnd,
                    draft.WindowStart.DateTime,
                    draft.IsUnlimited ? null : draft.WindowEnd!.Value.DateTime);
                if (unreachable is not null)
                    errors.Add(new SuppressionFormError(FieldRecurrence, unreachable));
            }
        }

        // ── 경고(막지 않는다) ───────────────────────────────────────────
        var overlapping = CountOverlapping(draft, others);
        if (overlapping > 0)
            warnings.Add($"같은 대상에 진행 중인 억제 스케줄이 이미 {overlapping}건 있습니다. "
                       + "중복으로 만들면 하나를 취소해도 억제가 계속됩니다.");

        if (draft.IsWeekly
            && SuppressionRules.ClassifyDailyTime(draft.DailyStart, draft.DailyEnd) == SuppressionRules.DailyTimeVerdict.Overnight)
            warnings.Add($"자정을 넘깁니다. 다음날 {draft.DailyEnd:hh\\:mm}에 끝납니다.");

        if (!draft.IsWeekly && !draft.IsUnlimited && draft.WindowEnd is { } past && past <= now)
            warnings.Add("종료 시각이 이미 지났습니다. 저장하면 바로 '종료'로 표시됩니다.");

        return new SuppressionFormVerdict(errors, warnings);
    }

    /// <summary>
    /// 같은 대상을 이미 덮고 있는 <b>다른</b> 억제 창의 수.
    /// <para>⚠ <b>자기 자신은 세지 않는다</b> — 수정 화면에서 자기 id 를 세면 고칠 때마다
    /// "이미 중복입니다" 가 떠 경고가 쓸모없어진다.</para>
    /// </summary>
    public static int CountOverlapping(SuppressionDraft draft, IEnumerable<EventSuppressionScheduleDto>? others)
    {
        if (draft is null || others is null) return 0;

        var pool = others
            .Where(o => o is not null)
            .Where(o => draft.Id is not { } mine || o.Id != mine)     // 자기 자신 제외
            .ToList();
        if (pool.Count == 0) return 0;

        return SuppressionRules.CountOverlappingActive(
            pool, draft.TargetType,
            draft.Targets.Where(t => t.Kind == SuppressionTargetKind.Device).Select(t => t.Id),
            draft.Targets.Where(t => t.Kind == SuppressionTargetKind.Group).Select(t => t.Id));
    }

    /// <summary>
    /// 서랍 아래 요약 한 줄 — <b>전송될 내용을 그대로</b> 옮긴다(정본 SB L2877 <c>#sf-recap</c>).
    /// </summary>
    public static string Recap(SuppressionDraft draft)
    {
        if (draft is null) return string.Empty;

        if (!draft.IsWeekly)
            return $"→ 단발 · {draft.WindowStart:MM-dd HH:mm} ~ "
                 + (draft.WindowEnd is { } e ? $"{e:MM-dd HH:mm}" : "종료 없음(단발에는 쓸 수 없습니다)");

        var rule = SuppressionRules.Summarize(draft.DaysOfWeekMask, draft.DailyStart, draft.DailyEnd);
        if (string.IsNullOrEmpty(rule)) return "→ 요일을 하나 이상 고르세요";

        var span = draft.IsUnlimited
            ? $"무제한 · {draft.WindowStart:yyyy-MM-dd} 시작"
            : $"{draft.WindowStart:yyyy-MM-dd} ~ {draft.WindowEnd!.Value:MM-dd}";
        return $"→ {rule} · {span}";
    }
}
