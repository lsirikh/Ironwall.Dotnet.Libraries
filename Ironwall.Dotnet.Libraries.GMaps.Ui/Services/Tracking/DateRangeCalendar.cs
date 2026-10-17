using System;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
/****************************************************************************
   Purpose      : Playback 기간 캘린더 순수 로직(WPF 무의존 · 단위테스트 링크 대상)
   Created By   : GHLee
   Created On   : 2026-06-29
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 추적 재생 콘솔의 날짜 범위 캘린더 순수 로직.
/// <para>2클릭 상태머신(첫 클릭=시작일, 둘째 클릭=종료일, 역순 자동 swap)과
/// 월(月) 그리드(6주 × 7일 = 42칸, 앞뒤 달 채움) 생성을 담당한다.
/// WPF/Caliburn 무의존 → <c>GMaps.Ui.Tests</c>가 소스 링크로 실코드 검증.</para>
/// </summary>
public static class DateRangeCalendar
{
    /// <summary>그리드 칸 수(6주 × 7일).</summary>
    public const int Cells = 42;

    /// <summary>해당 날짜가 속한 달의 1일(00:00).</summary>
    public static DateTime MonthOf(DateTime any) => new(any.Year, any.Month, 1);

    /// <summary>
    /// 2클릭 범위 선택 상태머신.
    /// </summary>
    /// <param name="pending">대기 중인 시작일(null이면 이번이 첫 클릭).</param>
    /// <param name="clicked">클릭한 날짜.</param>
    /// <returns>
    /// 갱신된 (start, end, pending, completed).
    /// <list type="bullet">
    /// <item>첫 클릭: start=end=클릭일, pending=클릭일, completed=false.</item>
    /// <item>둘째 클릭: 범위 확정(역순이면 swap), pending=null, completed=true.</item>
    /// </list>
    /// </returns>
    public static (DateTime start, DateTime end, DateTime? pending, bool completed)
        Click(DateTime? pending, DateTime clicked)
    {
        var d = clicked.Date;
        if (pending is null)
            return (d, d, d, false);              // 첫 클릭 — 시작일 대기

        var s = pending.Value.Date;
        var e = d;
        if (e < s) (s, e) = (e, s);               // 역순 클릭 → swap
        return (s, e, null, true);                // 둘째 클릭 — 범위 확정
    }

    /// <summary>
    /// 표시 월의 42칸 그리드 생성(앞 달 말일 ~ 뒤 달 초일로 6주 채움).
    /// 각 칸에 현재월 여부 · 오늘 · 시작/끝/범위내 하이라이트 플래그를 부여한다.
    /// </summary>
    /// <param name="month">표시할 달(어느 날짜든 그 달로 정규화).</param>
    /// <param name="start">선택 시작일.</param>
    /// <param name="end">선택 종료일(start보다 빠르면 내부 swap).</param>
    /// <param name="today">오늘(하이라이트용 — 호출측 주입).</param>
    public static IReadOnlyList<DayCell> BuildGrid(DateTime month, DateTime start, DateTime end, DateTime today)
    {
        var first = MonthOf(month);
        var s = start.Date;
        var e = end.Date;
        if (e < s) (s, e) = (e, s);

        int leading = (int)first.DayOfWeek;       // 일=0 .. 토=6
        var gridStart = first.AddDays(-leading);  // 1일이 든 주의 일요일
        var list = new List<DayCell>(Cells);
        for (int i = 0; i < Cells; i++)
        {
            var d = gridStart.AddDays(i);
            list.Add(new DayCell(
                Date: d,
                IsCurrentMonth: d.Month == first.Month && d.Year == first.Year,
                IsToday: d == today.Date,
                IsStart: d == s,
                IsEnd: d == e,
                IsInRange: d > s && d < e));
        }
        return list;
    }

    /// <summary>월 그리드 한 칸(불변). WPF는 이 값들에 바인딩한다.</summary>
    public readonly record struct DayCell(
        DateTime Date, bool IsCurrentMonth, bool IsToday, bool IsStart, bool IsEnd, bool IsInRange)
    {
        /// <summary>표시용 일(日) 숫자.</summary>
        public int Day => Date.Day;
    }
}
