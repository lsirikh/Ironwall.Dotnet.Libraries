using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Detail;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Lists;

/// <summary>목록 한 행에서 <b>거르는 데 쓰는 사실만</b> 뽑아 둔 것 — 순수 판정이 뷰모델 타입을 모르게 한다.</summary>
/// <param name="DeviceName">장비명.</param>
/// <param name="Zone">구역(장비 그룹 이름).</param>
/// <param name="Id">서버 이벤트 Id(글자).</param>
/// <param name="IsActionReported">조치가 있는가.</param>
/// <param name="TypeEventKey">탐지의 유형 키 — <c>Intrusion</c> · <c>Alert</c>.</param>
/// <param name="StateKey">연결의 상태 키 — <c>on</c>(연결) · <c>off</c>(끊김).</param>
/// <param name="Extra">사유 · 내용 · 사용자처럼 검색에만 쓰는 나머지 글자.</param>
public readonly record struct EventRowFacts(
    string? DeviceName,
    string? Zone,
    string? Id,
    bool IsActionReported,
    string? TypeEventKey = null,
    string? StateKey = null,
    string? Extra = null);

/// <summary>툴바 필터 칩 하나.</summary>
public sealed record EventFilterChip(string Key, string Label);

/// <summary>
/// 목록 필터 — <b>검색어</b>와 <b>필터 칩</b>의 순수 판정(N-07 적대 검토 R2 · R4).
/// </summary>
/// <remarks>
/// <para>칩 구성은 정본 <c>window-layout-system-storyboard.html</c> L2310-2313 그대로다.</para>
/// <para>거르기는 <b>이미 불러온 행</b> 위에서만 한다 — 서버에 해당 질의 파라미터가 있는지 확인되지 않았고,
/// 목록은 무한 스크롤로 한 쪽씩 온다. 그래서 상태 띠에 "불러온 N건 안에서" 를 못박아 둔다.</para>
/// </remarks>
public static class EventListFilter
{
    public const string ChipAll = "all";
    public const string ChipOpen = "open";          // 미조치
    public const string ChipDone = "done";          // 조치 있음
    public const string ChipIntrusion = "Intrusion";
    public const string ChipAlert = "Alert";
    public const string ChipConnected = "on";       // 연결
    public const string ChipDisconnected = "off";   // 끊김

    private static readonly EventFilterChip All = new(ChipAll, "전체");

    /// <summary>그 레일에 다는 칩들. 조치 내역에는 칩이 없다(정본 FILTERS 에 항목이 없다).</summary>
    public static IReadOnlyList<EventFilterChip> ChipsFor(EventDetailKind kind) => kind switch
    {
        EventDetailKind.Detection => new[]
        {
            All,
            new EventFilterChip(ChipIntrusion, "침입"),
            new EventFilterChip(ChipAlert, "사전 경보"),
            new EventFilterChip(ChipOpen, "미조치"),
            new EventFilterChip(ChipDone, "조치 있음"),
        },
        EventDetailKind.Malfunction => new[]
        {
            All,
            new EventFilterChip(ChipOpen, "미조치"),
            new EventFilterChip(ChipDone, "조치 있음"),
        },
        EventDetailKind.Connection => new[]
        {
            All,
            new EventFilterChip(ChipDisconnected, "끊김"),
            new EventFilterChip(ChipConnected, "연결"),
        },
        _ => Array.Empty<EventFilterChip>(),
    };

    /// <summary>칩 하나만 통과시킨다.</summary>
    public static bool MatchesChip(in EventRowFacts row, string? chipKey)
    {
        if (string.IsNullOrEmpty(chipKey) || chipKey == ChipAll) return true;

        return chipKey switch
        {
            ChipOpen => !row.IsActionReported,
            ChipDone => row.IsActionReported,
            ChipIntrusion or ChipAlert => string.Equals(row.TypeEventKey ?? ChipIntrusion, chipKey, StringComparison.Ordinal),
            ChipConnected or ChipDisconnected => string.Equals(row.StateKey, chipKey, StringComparison.Ordinal),
            _ => true,
        };
    }

    /// <summary>검색어 — 장비 · 구역 · 번호(그리고 사유 · 내용)에 대소문자 없이 들어 있으면 통과.</summary>
    public static bool MatchesSearch(in EventRowFacts row, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;

        var needle = search.Trim();
        return Has(row.DeviceName) || Has(row.Zone) || Has(row.Id) || Has(row.Extra);

        bool Has(string? haystack)
            => haystack is not null && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    public static bool Matches(in EventRowFacts row, string? search, string? chipKey)
        => MatchesChip(row, chipKey) && MatchesSearch(row, search);

    /// <summary>
    /// 상태 띠 한 줄. <b>불러온 범위 안에서만</b> 걸렀다는 사실을 감추지 않는다 —
    /// 무한 스크롤이라 조건에 맞는 행이 아직 서버에 더 있을 수 있다.
    /// </summary>
    public static string StatusLine(int shown, int loaded, int selected, bool isFiltered, bool hasMorePages)
    {
        if (!isFiltered)
            return $"불러온 {loaded}건 · 선택 {selected}건";

        var tail = hasMorePages ? " (더 있을 수 있습니다 — 아래로 내려 더 불러오세요)" : string.Empty;
        return $"불러온 {loaded}건 안에서 {shown}건 · 선택 {selected}건{tail}";
    }
}
