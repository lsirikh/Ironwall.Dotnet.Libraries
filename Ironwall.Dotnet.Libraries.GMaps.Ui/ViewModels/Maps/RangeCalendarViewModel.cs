using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 날짜 범위 캘린더 VM(2클릭 시작/끝 · 월 그리드) — Playback 콘솔 팝업용
   Created By   : GHLee
   Created On   : 2026-06-29
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 추적 재생 콘솔의 기간 선택 캘린더 VM. 순수 로직(<see cref="DateRangeCalendar"/>)을 감싸
/// 월 그리드(<see cref="Days"/>) · 이전/다음 달 · 날짜 클릭 커맨드를 제공한다.
/// <para>2클릭(첫 클릭=시작, 둘째 클릭=종료, 역순 자동 swap)으로 범위가 확정되면
/// <see cref="RangeCompleted"/>를 발화 → 소유 VM(PlaybackViewModel)이 From/To에 반영한다.</para>
/// </summary>
public sealed class RangeCalendarViewModel : PropertyChangedBase
{
    private DateTime _month;          // 표시 중인 달(1일)
    private DateTime _start;          // 현재 선택 시작일
    private DateTime _end;            // 현재 선택 종료일
    private DateTime? _pending;       // 첫 클릭 대기 상태(둘째 클릭 전)

    public RangeCalendarViewModel()
    {
        var today = DateTime.Today;
        _month = DateRangeCalendar.MonthOf(today);
        _start = today;
        _end = today;
        PrevMonthCommand = new RelayCommand(_ => ShiftMonth(-1));
        NextMonthCommand = new RelayCommand(_ => ShiftMonth(1));
        PickDayCommand = new RelayCommand(OnPickDay);
        Rebuild();
    }

    /// <summary>둘째 클릭으로 범위 확정 시 (시작일, 종료일) 통지(둘 다 .Date 기준).</summary>
    public event Action<DateTime, DateTime>? RangeCompleted;

    /// <summary>표시 월의 42칸 그리드.</summary>
    public ObservableCollection<DateRangeCalendar.DayCell> Days { get; } = new();

    /// <summary>"2026년 6월" 형식 헤더 라벨.</summary>
    public string MonthLabel => $"{_month:yyyy년 M월}";

    private string _hint = "시작일을 선택하세요";
    /// <summary>안내 문구(시작/종료 선택 단계 표시).</summary>
    public string Hint { get => _hint; set { _hint = value; NotifyOfPropertyChange(); } }

    public ICommand PrevMonthCommand { get; }
    public ICommand NextMonthCommand { get; }
    public ICommand PickDayCommand { get; }     // 파라미터: DateRangeCalendar.DayCell

    /// <summary>
    /// 팝업을 열 때 현재 선택 범위로 달력을 동기화하고 대기 상태를 초기화한다.
    /// </summary>
    public void Sync(DateTime start, DateTime end)
    {
        _start = start.Date;
        _end = end.Date;
        if (_end < _start) (_start, _end) = (_end, _start);
        _pending = null;
        _month = DateRangeCalendar.MonthOf(_start);
        Hint = "시작일을 선택하세요";
        NotifyOfPropertyChange(nameof(MonthLabel));
        Rebuild();
    }

    private void ShiftMonth(int months)
    {
        _month = _month.AddMonths(months);
        NotifyOfPropertyChange(nameof(MonthLabel));
        Rebuild();
    }

    private void OnPickDay(object? param)
    {
        if (param is not DateRangeCalendar.DayCell cell) return;
        var (s, e, pending, completed) = DateRangeCalendar.Click(_pending, cell.Date);
        _start = s;
        _end = e;
        _pending = pending;
        Hint = completed ? "시작일을 선택하세요" : $"시작 {s:M/d} · 종료일을 선택하세요";
        Rebuild();
        if (completed) RangeCompleted?.Invoke(s, e);
    }

    private void Rebuild()
    {
        Days.Clear();
        foreach (var c in DateRangeCalendar.BuildGrid(_month, _start, _end, DateTime.Today))
            Days.Add(c);
    }
}
