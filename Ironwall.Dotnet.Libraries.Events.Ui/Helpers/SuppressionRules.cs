using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
/****************************************************************************
   Purpose      : 이벤트 억제(정비 창) 폼 검증·중복 판정 순수 규칙.
                  서버 명세 v2.0 §5-B(겹친 창)/§5-D(기간 상한) 회피 로직과
                  API 6.3.3 주간 반복 규칙을 VM에서 분리해 단위 테스트 가능하게 한다.
   Created By   : GHLee
   Created On   : 2026-08-03
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>반복 유형 — 서버 <c>recurrence_type</c> 과 1:1.</summary>
public enum SuppressionRecurrenceMode
{
    /// <summary>단발(기존 동작). 서버 기본값.</summary>
    None,
    /// <summary>주간 반복(API 6.3.3 신설).</summary>
    Weekly,
}

/// <summary>억제 창 생성 폼의 순수 검증 규칙(부작용 없음 — 단위 테스트 대상).</summary>
public static class SuppressionRules
{
    #region - 상수(단일 출처) -

    /// <summary>
    /// 단발 창 길이 상한(일). 오타로 장기 억제가 생기는 것을 막는 <b>클라 방어선</b>이다.
    /// <para>⚠ API 6.3.4 부터는 <b>서버도 단발에 366일 상한</b>을 건다(그전에는 무제한이었다).
    /// 클라 30일이 더 좁으므로 이 값으로는 422 가 나지 않는다.</para>
    /// </summary>
    public const int DefaultMaxWindowDays = 30;

    /// <summary>
    /// 주간 반복 유효기간 상한(일). 서버 <c>SUPPRESSION_MAX_VALIDITY_DAYS</c> 와 동일.
    /// <para>서버 판정은 <c>(end-start).days &gt; 366</c> 이므로 <b>정확히 366일은 통과</b>한다.</para>
    /// </summary>
    public const int WeeklyMaxWindowDays = 366;

    /// <summary>요일 비트 — 월=1 화=2 수=4 목=8 금=16 토=32 일=64 (서버 <c>1 &lt;&lt; date.weekday()</c>).</summary>
    public const int DaysWeekdayPreset = 31;    // 월~금
    /// <summary>주말 프리셋(토+일).</summary>
    public const int DaysWeekendPreset = 96;
    /// <summary>매일 프리셋(월~일).</summary>
    public const int DaysEveryDayPreset = 127;

    private static readonly string[] DayNames = { "월", "화", "수", "목", "금", "토", "일" };

    #endregion

    #region - 요일 비트마스크 -

    /// <summary>
    /// .NET <see cref="DayOfWeek"/>(일=0) → 서버 원점(월=0)으로 변환한다.
    /// <para>⚠ <c>(int)DayOfWeek</c> 를 그대로 시프트하면 요일이 통째로 어긋난다.</para>
    /// </summary>
    public static int ToMon0(DayOfWeek day) => ((int)day + 6) % 7;

    /// <summary>Mon0 인덱스(0=월 … 6=일)를 비트로.</summary>
    public static int BitOf(int mon0Index) => 1 << mon0Index;

    /// <summary>해당 요일이 마스크에 포함되는가.</summary>
    public static bool HasDay(int mask, DayOfWeek day) => (mask & BitOf(ToMon0(day))) != 0;

    /// <summary>Mon0 인덱스 나열 → 비트마스크 합산.</summary>
    public static int ToMask(IEnumerable<int>? mon0Indexes)
    {
        if (mon0Indexes is null) return 0;
        var mask = 0;
        foreach (var i in mon0Indexes)
            if (i >= 0 && i <= 6) mask |= BitOf(i);
        return mask;
    }

    /// <summary>비트마스크 → 선택된 Mon0 인덱스 오름차순.</summary>
    public static IReadOnlyList<int> ToIndexes(int mask)
    {
        var list = new List<int>(7);
        for (var i = 0; i < 7; i++)
            if ((mask & BitOf(i)) != 0) list.Add(i);
        return list;
    }

    /// <summary>요일이 하나라도 선택됐는가. <b>서버는 0을 거부하지 않으므로 UI가 유일 방어선이다.</b></summary>
    public static bool HasAnyDay(int mask) => (mask & DaysEveryDayPreset) != 0;

    #endregion

    #region - 반복 요약 문자열 -

    /// <summary>일일 시각을 서버 규약(<c>"HH:mm:ss"</c>, <b>offset 금지</b>)으로 포맷한다.</summary>
    public static string FormatDailyTime(TimeSpan time)
        => time.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

    /// <summary><see cref="DateTime"/> 의 시각 부분만 서버 규약으로 포맷한다(날짜는 버린다).</summary>
    public static string? FormatDailyTime(DateTime? value)
        => value is null ? null : FormatDailyTime(value.Value.TimeOfDay);

    /// <summary>요일 마스크를 사람이 읽는 문자열로. 연속은 <c>~</c>, 불연속은 <c>·</c>.</summary>
    public static string SummarizeDays(int mask)
    {
        if (!HasAnyDay(mask)) return string.Empty;
        if ((mask & DaysEveryDayPreset) == DaysEveryDayPreset) return "매일";
        if ((mask & DaysEveryDayPreset) == DaysWeekendPreset) return "주말";
        if ((mask & DaysEveryDayPreset) == DaysWeekdayPreset) return "월~금";

        var idx = ToIndexes(mask);
        var parts = new List<string>();
        var runStart = 0;
        for (var i = 0; i < idx.Count; i++)
        {
            var isLast = i == idx.Count - 1;
            var breaks = isLast || idx[i + 1] != idx[i] + 1;
            if (!breaks) continue;

            var len = i - runStart + 1;
            parts.Add(len >= 3
                ? $"{DayNames[idx[runStart]]}~{DayNames[idx[i]]}"
                : string.Join("·", Enumerable.Range(runStart, len).Select(k => DayNames[idx[k]])));
            runStart = i + 1;
        }
        return string.Join("·", parts);
    }

    /// <summary>
    /// 반복 규칙 요약(예: <c>"월~금 08:00~21:00"</c>, 자정 넘김은 <c>"월~금 22:00~익일 06:00"</c>).
    /// <para>⚠ 이것은 <b>규칙</b>을 말한다. 실제 회차는 유효기간 경계에서 잘릴 수 있으므로
    /// 서버 <c>occurrence_end</c> 를 이 문자열로 재계산하지 말 것.</para>
    /// </summary>
    public static string Summarize(int mask, TimeSpan dailyStart, TimeSpan dailyEnd)
    {
        var days = SummarizeDays(mask);
        if (string.IsNullOrEmpty(days)) return string.Empty;

        var s = dailyStart.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        var e = dailyEnd.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        var overnight = dailyEnd < dailyStart;
        return overnight ? $"{days} {s}~익일 {e}" : $"{days} {s}~{e}";
    }

    #endregion

    #region - 폼 검증 -

    /// <summary>모드별 유효기간 상한(일).</summary>
    public static int MaxWindowDaysFor(SuppressionRecurrenceMode mode)
        => mode == SuppressionRecurrenceMode.Weekly ? WeeklyMaxWindowDays : DefaultMaxWindowDays;

    /// <summary>창 길이가 상한 이내인가. (종료 &gt; 시작 여부는 별도 검사)</summary>
    public static bool IsWindowLengthValid(DateTime start, DateTime end, int maxDays = DefaultMaxWindowDays)
        => (end - start).TotalDays <= maxDays;

    /// <summary>
    /// 모드를 인지하는 유효기간 검사.
    /// <para>무제한(<paramref name="isUnlimited"/>)이면 <b>검사 자체를 건너뛴다</b>(서버도 검사하지 않음).</para>
    /// </summary>
    public static bool IsWindowLengthValidFor(
        DateTime start, DateTime end, SuppressionRecurrenceMode mode, bool isUnlimited = false)
        => isUnlimited || IsWindowLengthValid(start, end, MaxWindowDaysFor(mode));

    /// <summary>일일 시각 입력 판정 결과.</summary>
    public enum DailyTimeVerdict
    {
        /// <summary>정상.</summary>
        Ok,
        /// <summary>
        /// <c>00:00:00~00:00:00</c> — <b>종일의 정식 표현</b>. 허용한다.
        /// <para>서버가 자정 표기만 남긴 이유: 전면 금지하면 진짜 24시간을 표현할 방법이 없어진다
        /// (<c>00:00:00~23:59:59</c> 는 매일 1초 구멍).</para>
        /// </summary>
        AllDayMidnight,
        /// <summary>
        /// 자정이 아닌 같은 시각(예: <c>09:00~09:00</c>) — 서버가 <b>422</b> 로 막는다(API 6.3.4).
        /// <para>"종료를 안 고친 실수"일 가능성이 압도적인데 결과는 종일 억제다.
        /// 월~금이면 닷새 연속 알람이 죽는다 — 안전 방향이 나쁘다.</para>
        /// </summary>
        AllDayAmbiguous,
        /// <summary>종료 &lt; 시작 → 자정 넘김. <b>정상 입력</b>이며 안내만 한다.</summary>
        Overnight,
    }

    /// <summary>
    /// 일일 시각 쌍을 판정한다.
    /// <para>⚠ 서버(API 6.3.4)는 <c>daily_start == daily_end &amp;&amp; daily_start != 00:00</c> 을 <b>422</b> 로 막는다.
    /// 자정끼리는 통과시킨다 — 그것이 종일을 표현하는 유일한 정식 방법이다.</para>
    /// </summary>
    public static DailyTimeVerdict ClassifyDailyTime(TimeSpan dailyStart, TimeSpan dailyEnd)
        => dailyStart != dailyEnd
            ? (dailyEnd < dailyStart ? DailyTimeVerdict.Overnight : DailyTimeVerdict.Ok)
            : (dailyStart == TimeSpan.Zero
                ? DailyTimeVerdict.AllDayMidnight
                : DailyTimeVerdict.AllDayAmbiguous);

    /// <summary>
    /// 반복 폼 전체 검증. 통과면 <c>null</c>, 아니면 <b>사용자에게 보일 오류 문구</b>를 돌려준다.
    /// <para>서버가 막지 않는 2건(요일 0개 · 24시간 종일)이 여기서 걸린다.</para>
    /// </summary>
    public static string? ValidateWeeklyForm(int mask, TimeSpan? dailyStart, TimeSpan? dailyEnd)
    {
        if (!HasAnyDay(mask)) return "요일을 1개 이상 선택하세요";
        if (dailyStart is null || dailyEnd is null) return "일일 시각을 입력하세요";
        if (ClassifyDailyTime(dailyStart.Value, dailyEnd.Value) == DailyTimeVerdict.AllDayAmbiguous)
            return "시작과 종료가 같으면 24시간 종일이 됩니다 — 종일이 맞다면 00:00:00 ~ 00:00:00 으로 입력하세요";
        return null;
    }

    /// <summary>
    /// 이 반복 규칙이 유효기간 안에서 <b>한 번이라도 발동하는가</b>.
    /// <para>서버(API 6.3.4)가 발동 불가 창을 <b>422</b> 로 막는다. 저장 전에 알려주면
    /// 사용자가 422 를 보기 전에 고칠 수 있다.</para>
    /// <para>검사 범위는 좁다 — 무제한 창은 언젠가 반드시 걸리므로 <b>항상 도달 가능</b>이고,
    /// 유효기간 안에 해당 요일이 한 번이라도 있으면 통과한다.</para>
    /// </summary>
    /// <param name="windowEnd">무제한이면 <c>null</c>.</param>
    public static bool IsOccurrenceReachable(
        int mask, TimeSpan dailyStart, TimeSpan dailyEnd,
        DateTime windowStart, DateTime? windowEnd)
    {
        if (!HasAnyDay(mask)) return false;
        if (windowEnd is null) return true;          // 무제한 — 언젠가 반드시 걸린다
        var end = windowEnd.Value;
        if (end <= windowStart) return false;

        // 유효기간 상한이 366일이라 일 단위 순회가 안전하다(최악 367회).
        for (var d = windowStart.Date; d <= end.Date; d = d.AddDays(1))
        {
            if (!HasDay(mask, d.DayOfWeek)) continue;

            var s = d + dailyStart;
            // 자정 넘김(종료 < 시작)과 자정 종일(둘 다 00:00)은 다음날로 넘어간다.
            var e = dailyEnd > dailyStart ? d + dailyEnd : d.AddDays(1) + dailyEnd;

            // 유효기간으로 클램프 — 잘라내고 길이가 남아야 실제 회차다.
            if (s < windowStart) s = windowStart;
            if (e > end) e = end;
            if (s < e) return true;
        }
        return false;
    }

    /// <summary>도달 불가 창에 대한 안내 문구(정상이면 <c>null</c>).</summary>
    public static string? DescribeUnreachable(
        int mask, TimeSpan dailyStart, TimeSpan dailyEnd,
        DateTime windowStart, DateTime? windowEnd)
        => IsOccurrenceReachable(mask, dailyStart, dailyEnd, windowStart, windowEnd)
            ? null
            : $"선택한 기간({windowStart:MM-dd} ~ {windowEnd:MM-dd})에 " +
              $"{SummarizeDays(mask)} 회차가 없습니다 — 이 창은 발동하지 않습니다";

    #endregion

    #region - 중복 창 판정 -

    /// <summary>
    /// (§5-B) 선택한 대상이 <b>지금 억제 중인</b> 창에 이미 덮여 있는 건수.
    /// device/group 은 대상 배열 교집합, all 은 활성 all 창 수로 판정한다.
    /// </summary>
    /// <param name="now">반복 창의 '오늘 해당 여부' 판정 기준(미지정 시 <see cref="DateTime.Now"/>).</param>
    public static int CountOverlappingActive(
        IEnumerable<EventSuppressionScheduleDto>? active,
        string targetType,
        IEnumerable<int>? selectedDeviceIds,
        IEnumerable<int>? selectedGroupIds,
        DateTime? now = null)
    {
        if (active is null) return 0;
        var list = active as IList<EventSuppressionScheduleDto> ?? active.ToList();
        if (list.Count == 0) return 0;

        var stamp = now ?? DateTime.Now;
        bool Relevant(EventSuppressionScheduleDto a) => IsRelevantToday(a, stamp);

        switch (targetType)
        {
            case "device":
                var devIds = (selectedDeviceIds ?? Enumerable.Empty<int>()).ToHashSet();
                if (devIds.Count == 0) return 0;
                return list.Count(a => a.TargetType == "device"
                                    && Relevant(a)
                                    && (a.TargetDeviceIds ?? new()).Any(devIds.Contains));
            case "group":
                var grpIds = (selectedGroupIds ?? Enumerable.Empty<int>()).ToHashSet();
                if (grpIds.Count == 0) return 0;
                return list.Count(a => a.TargetType == "group"
                                    && Relevant(a)
                                    && (a.TargetGroupIds ?? new()).Any(grpIds.Contains));
            default:   // all
                return list.Count(a => a.TargetType == "all" && Relevant(a));
        }
    }

    /// <summary>
    /// 이 창이 <b>오늘</b> 경고 대상인가.
    /// <para>단발이면 항상 대상. 주간 반복이면 <b>오늘(또는 자정 넘김의 어제 시작분) 요일에 해당할 때만</b>.
    /// 서버가 <c>is_suppressing_now</c> 를 주면 그것을 우선한다.</para>
    /// </summary>
    public static bool IsRelevantToday(EventSuppressionScheduleDto dto, DateTime now)
    {
        // 서버 파생값이 있으면 그것이 권위다.
        if (dto.IsSuppressingNow is { } flag) return flag;

        if (!string.Equals(dto.RecurrenceType, "weekly", StringComparison.OrdinalIgnoreCase))
            return true;    // 단발 — 기존 동작

        var mask = dto.DaysOfWeek ?? 0;
        if (!HasAnyDay(mask)) return false;     // 요일 0개 = 영원히 발동 안 함

        if (HasDay(mask, now.DayOfWeek)) return true;

        // 자정 넘김: 어제 시작분이 오늘 새벽까지 이어질 수 있다.
        if (TryParseDaily(dto.DailyStart, out var ds) && TryParseDaily(dto.DailyEnd, out var de) && de < ds)
            return HasDay(mask, now.AddDays(-1).DayOfWeek) && now.TimeOfDay < de;

        return false;
    }

    private static bool TryParseDaily(string? text, out TimeSpan value)
        => TimeSpan.TryParseExact(text, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out value)
        || TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out value);

    #endregion
}
