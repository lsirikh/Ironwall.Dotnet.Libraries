using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using System;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
/****************************************************************************
   Purpose      : 억제 스케줄 목록 행 표시 VM — DTO를 화면용 문자열/상태로 투영.
                  대상 요약(장비/그룹 이름), 상태 배지, 시간창 KST 표시, 취소가능 여부.
   Created By   : GHLee
   Created On   : 2026-08-01
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>억제 스케줄 DataGrid 행. 서버 DTO + Device/Group Provider로 표시명 해석.</summary>
public class EventSuppressionScheduleItemViewModel : PropertyChangedBase
{
    public EventSuppressionScheduleItemViewModel(
        EventSuppressionScheduleDto dto,
        DeviceProvider? deviceProvider,
        DeviceGroupProvider? groupProvider,
        System.Action? onSelectionChanged = null)
    {
        _onSelectionChanged = onSelectionChanged;
        Id = dto.Id;
        Name = dto.Name;
        Status = dto.Status ?? "pending";
        TargetSummary = BuildTargetSummary(dto, deviceProvider, groupProvider);
        ScopeText = MapScope(dto.EventScope);
        WindowStartText = ToDisplay(dto.WindowStart);
        WindowEndText = ToWindowEndDisplay(dto.WindowEnd);
        // ⚠ ctor 시그니처는 불변 — 인자를 추가하면 테스트 11곳이 CS7036 으로 깨져 라이브러리 빌드가 실패한다.
        //    "억제중" 표식은 인자가 아니라 DTO 파생 필드에서 만든다.
        IsRecurring = string.Equals(dto.RecurrenceType, "weekly", StringComparison.OrdinalIgnoreCase);
        RecurrenceSummary = BuildRecurrenceSummary(dto);
        // is_suppressing_now 가 null(구버전 서버)이면 status=="active" 로 폴백한다.
        //   bool 로 선언했다면 필드 부재 시 조용히 false(억제 안 함)로 굳어 안전 방향의 반대가 된다.
        IsSuppressingNow = dto.IsSuppressingNow ?? (Status == "active");
        OccurrenceText = BuildOccurrenceText(dto);
        IsUnlimited = dto.WindowEnd is null;
        // 취소(soft-cancel) 가능 = 아직 취소 안 됐고 종료되지 않음(예정/진행중).
        IsCancellable = string.IsNullOrEmpty(dto.RevokedAt)
                        && Status is not ("expired" or "cancelled");
        // 하드삭제 대상 = 취소됨/종료됨(terminal) — 목록 정리용 선택 체크박스 노출 조건.
        IsDeletable = Status is "cancelled" or "expired";
    }

    private readonly System.Action? _onSelectionChanged;

    /// <summary>스케줄 DB Id.</summary>
    public int Id { get; }
    /// <summary>작업명/사유.</summary>
    public string Name { get; }
    /// <summary>파생 상태(pending/active/expired/cancelled) — 배지 DataTrigger 키.</summary>
    public string Status { get; }
    /// <summary>상태 한글 표기.</summary>
    public string StatusText => Status switch
    {
        "active" => "진행중",
        "pending" => "예정",
        "expired" => "종료",
        "cancelled" => "취소",
        _ => Status,
    };
    /// <summary>대상 요약(예: "장비 3 · FN-0312 외 2" / "그룹 2 · A·B" / "전체(both)").</summary>
    public string TargetSummary { get; }
    /// <summary>억제 범위 한글 표기.</summary>
    public string ScopeText { get; }
    /// <summary>시작(KST 표시).</summary>
    public string WindowStartText { get; }
    /// <summary>종료(KST 표시).</summary>
    public string WindowEndText { get; }
    /// <summary>취소 버튼 활성 여부.</summary>
    public bool IsCancellable { get; }

    /// <summary>주간 반복 창인가.</summary>
    public bool IsRecurring { get; }

    /// <summary>반복 요약(예: "월~금 08:00~21:00"). 단발이면 빈 문자열.</summary>
    public string RecurrenceSummary { get; }

    /// <summary>
    /// <b>지금 억제 중인가.</b> 상태 배지(<see cref="StatusText"/>)와 <b>분리된 축</b>이다.
    /// <para>status 는 창의 생애주기를, 이 값은 지금 이 순간을 말한다.
    /// 반복 창에서는 유효기간의 62.2% 가 active 이면서 미억제다(서버 실측).</para>
    /// </summary>
    public bool IsSuppressingNow { get; }

    /// <summary>회차 정보 — 진행 중이면 "~21:00 까지", 대기면 "다음 08-11 08:00".</summary>
    public string OccurrenceText { get; }

    /// <summary>무제한 창(window_end = null)인가.</summary>
    public bool IsUnlimited { get; }

    /// <summary>하드삭제 대상 여부(취소/종료 = terminal). 삭제 체크박스 노출 조건.</summary>
    public bool IsDeletable { get; }

    /// <summary>삭제 선택(체크박스, TwoWay). IsDeletable 행에서만 유효 — 변경 시 패널에 통지(선택수/버튼 갱신).</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            var v = value && IsDeletable;   // 비대상(진행중/예정) 행은 선택 불가(방어)
            if (_isSelected == v) return;
            _isSelected = v;
            NotifyOfPropertyChange(nameof(IsSelected));
            _onSelectionChanged?.Invoke();
        }
    }
    private bool _isSelected;

    #region - Helpers -
    private static string MapScope(string scope) => scope switch
    {
        "connection" => "연결",
        "detection" => "탐지",
        "malfunction" => "장애",
        "all" => "전체",
        _ => scope,
    };

    /// <summary>
    /// 종료 표기 — <b>무제한 / 파싱실패 / 값없음 3종을 구분</b>한다.
    /// <para>예전에는 셋 다 '—' 로 같아 보여 무제한 창을 알아볼 수 없었다.</para>
    /// </summary>
    private static string ToWindowEndDisplay(string? iso)
        => iso is null ? "무제한" : ToDisplay(iso);

    private static string BuildRecurrenceSummary(EventSuppressionScheduleDto dto)
    {
        if (!string.Equals(dto.RecurrenceType, "weekly", StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        var mask = dto.DaysOfWeek ?? 0;
        if (!Helpers.SuppressionRules.HasAnyDay(mask)) return string.Empty;
        if (!TryTime(dto.DailyStart, out var s) || !TryTime(dto.DailyEnd, out var e)) return string.Empty;
        return Helpers.SuppressionRules.Summarize(mask, s, e);
    }

    /// <summary>
    /// 회차 정보. ⚠ 서버 값을 <b>그대로</b> 쓴다 — 유효기간 경계에서 회차가 잘리므로
    /// 요약 문자열로 재계산하면 마지막 날에 거짓말을 한다.
    /// </summary>
    private static string BuildOccurrenceText(EventSuppressionScheduleDto dto)
    {
        if (dto.OccurrenceEnd is { } oe && DateTime.TryParse(oe, out var end))
            return $"~{end:HH:mm} 까지";
        if (dto.NextOccurrenceStart is { } ns && DateTime.TryParse(ns, out var next))
            return $"다음 {next:MM-dd HH:mm}";
        return string.Empty;
    }

    private static bool TryTime(string? text, out TimeSpan value)
        => TimeSpan.TryParseExact(text, @"hh\:mm\:ss",
               System.Globalization.CultureInfo.InvariantCulture, out value)
        || TimeSpan.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out value);

    private static string ToDisplay(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso)) return "—";
        // 서버 ISO8601(+09:00) → 로컬(KST) 표시. 파싱 실패 시 원문.
        return DateTime.TryParse(iso, out var dt) ? dt.ToString("yyyy-MM-dd HH:mm") : iso!;
    }

    private static string BuildTargetSummary(
        EventSuppressionScheduleDto dto, DeviceProvider? dp, DeviceGroupProvider? gp)
    {
        switch (dto.TargetType)
        {
            case "device":
            {
                var ids = dto.TargetDeviceIds ?? new();
                if (ids.Count == 0) return "장비 0";
                var first = dp?.CollectionEntity.FirstOrDefault(x => x.Id == ids[0])?.DeviceName
                            ?? $"#{ids[0]}";
                return ids.Count > 1 ? $"장비 {ids.Count} · {first} 외 {ids.Count - 1}"
                                     : $"장비 1 · {first}";
            }
            case "group":
            {
                var ids = dto.TargetGroupIds ?? new();
                if (ids.Count == 0) return "그룹 0";
                var names = ids.Select(id => gp?.CollectionEntity.FirstOrDefault(x => x.Id == id)?.Name ?? $"#{id}");
                return $"그룹 {ids.Count} · {string.Join("·", names.Take(2))}{(ids.Count > 2 ? " 외 " + (ids.Count - 2) : string.Empty)}";
            }
            default:
                return $"전체({dto.TargetSide})";
        }
    }
    #endregion
}
