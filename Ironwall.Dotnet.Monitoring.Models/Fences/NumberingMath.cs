using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Monitoring.Models.Fences;

/// <summary>번호 판정의 입력 한 줄 — 센서 한 대.</summary>
/// <param name="Key">센서 키(결선 창 보드 키 · 서버 센서는 id).</param>
/// <param name="Category">번호 갈래.</param>
/// <param name="Number">지금 번호(<c>number_device</c>).</param>
/// <param name="InChain">결선(링)에 붙어 있는가 — 붙은 센서만 대역으로 번호를 받는다.</param>
/// <param name="Name">사람이 읽는 이름(문장용).</param>
public sealed record NumberingSensor(int Key, FenceSensorCategory Category, int Number, bool InChain, string Name);

/// <summary>번호 판정이 알리는 것의 갈래.</summary>
public enum NumberingIssueKind
{
    /// <summary>그 갈래 센서 수가 대역 크기를 넘는다 — 저장 막음.</summary>
    BandOverflow = 0,
    /// <summary>번호가 <see cref="NumberingMath.MAX_NUMBER"/> 를 넘는다 — 저장 막음.</summary>
    OverMax = 1,
    /// <summary>같은 제어기 안에서 번호가 겹친다 — 결선에 붙은 센서끼리면 저장 막음, 미배치 센서가 끼어서만 겹치면 경고.</summary>
    Duplicate = 2,
    /// <summary>대역끼리 겹친다 — 경고만.</summary>
    BandOverlap = 3,
    /// <summary>대역이 없는 갈래 — 번호를 그대로 둔다(알림).</summary>
    NoBand = 4,
}

/// <summary>번호 판정 결과 한 줄.</summary>
/// <param name="BlocksSave">저장을 막는가(대역 초과 · 255 초과 · 중복).</param>
/// <param name="Keys">관련 센서 키.</param>
public sealed record NumberingIssue(NumberingIssueKind Kind, bool BlocksSave, string Message, IReadOnlyList<int> Keys);

/// <summary>저장 전 "바뀌는 번호 표"의 한 줄 — "북측 5구간 105 → 5".</summary>
public sealed record NumberChange(int Key, string Name, int Before, int After)
{
    public string Text => $"{Name} {Before} → {After}";
}

/// <summary>
/// 위치 = 순서 = 번호(fence-wiring-editor FR-09 · FR-10) — <b>순수 함수</b>(NFR-01).
/// 번호 = 갈래 대역 시작 + (그 갈래 안에서의 위치 순서 − 1). 대역이 없는 갈래는 번호를 건드리지 않는다.
/// </summary>
public static class NumberingMath
{
    /// <summary>번호 상한(현장 장비 설정 한도).</summary>
    public const int MAX_NUMBER = 255;

    /// <summary>센서 종류 → 번호 갈래. 스마트 제품군(스마트센서 · 스마트 복합센서 II 포함)은 한 갈래다.</summary>
    public static FenceSensorCategory CategoryOf(EnumDeviceType type) => type switch
    {
        EnumDeviceType.SmartSensor or EnumDeviceType.SmartSensor2 or EnumDeviceType.SmartCompound or EnumDeviceType.SmartMultisensor2
            => FenceSensorCategory.Smart,
        EnumDeviceType.Fence => FenceSensorCategory.Fence,
        EnumDeviceType.Multi => FenceSensorCategory.Multi,
        EnumDeviceType.Underground => FenceSensorCategory.Underground,
        _ => FenceSensorCategory.Other,
    };

    /// <summary>갈래 이름(한글 · 짧게).</summary>
    public static string CategoryText(FenceSensorCategory category) => category switch
    {
        FenceSensorCategory.Smart => "스마트",
        FenceSensorCategory.Fence => "펜스",
        FenceSensorCategory.Multi => "복합",
        FenceSensorCategory.Underground => "지진동",
        _ => "기타",
    };

    /// <summary>
    /// 결선 순서(Ch1(A) 쪽이 앞)대로 번호를 매긴다 — 대역이 있는 갈래의 센서만 결과에 들어간다(없는 갈래는 빠진다 = 그대로 둔다).
    /// 대역을 넘는 센서도 이어서 번호를 받는다(검증이 막는다 — 화면에서 어디가 넘는지 보이게).
    /// </summary>
    public static IReadOnlyDictionary<int, int> Assign(IEnumerable<(int Key, FenceSensorCategory Category)> chainOrder, NumberBandSet? bands)
    {
        var result = new Dictionary<int, int>();
        if (bands is null) return result;
        var ordinal = new Dictionary<FenceSensorCategory, int>();
        foreach (var (key, category) in chainOrder ?? Enumerable.Empty<(int, FenceSensorCategory)>())
        {
            if (bands.BandOf(category) is not { } band || result.ContainsKey(key)) continue;
            ordinal.TryGetValue(category, out var n);
            result[key] = band.Start + n;
            ordinal[category] = n + 1;
        }
        return result;
    }

    /// <summary>
    /// 번호 검증(FR-10) — 대역 초과 · 255 초과 · 같은 제어기 안 중복은 <b>저장 막음</b>, 대역끼리 겹침은 경고, 대역 없는 갈래는 알림.
    /// <paramref name="sensors"/> 의 번호는 <see cref="Assign"/> 을 적용한 <b>뒤</b>의 값이다(미배치 센서는 제 번호 그대로).
    /// </summary>
    public static IReadOnlyList<NumberingIssue> Validate(IReadOnlyList<NumberingSensor> sensors, NumberBandSet? bands)
    {
        var issues = new List<NumberingIssue>();
        var list = sensors ?? Array.Empty<NumberingSensor>();
        if (bands is null) return issues;

        var bandList = bands.Bands ?? Array.Empty<NumberBand>();
        for (var i = 0; i < bandList.Count; i++)
            for (var j = i + 1; j < bandList.Count; j++)
                if (bandList[i].Overlaps(bandList[j]))
                    issues.Add(new NumberingIssue(NumberingIssueKind.BandOverlap, false,
                        $"번호 대역이 겹칩니다 — {bandList[i].Text} · {bandList[j].Text}", Array.Empty<int>()));

        foreach (var group in list.Where(s => s.InChain).GroupBy(s => s.Category))
        {
            if (bands.BandOf(group.Key) is not { } band)
            {
                issues.Add(new NumberingIssue(NumberingIssueKind.NoBand, false,
                    $"{CategoryText(group.Key)} 센서 {group.Count()}대는 번호 대역이 없어 번호를 그대로 둡니다.", group.Select(s => s.Key).ToList()));
                continue;
            }
            var count = group.Count();
            if (count > band.Size)
                issues.Add(new NumberingIssue(NumberingIssueKind.BandOverflow, true,
                    $"{band.Text} 대역은 {band.Size}대까지인데 {CategoryText(group.Key)} 센서가 {count}대입니다 — 대역을 넓히거나 센서를 빼세요.",
                    group.Where(s => s.Number > band.End).Select(s => s.Key).ToList()));
        }

        // 대역으로 번호를 받은 센서만 본다 — 대역이 없어 그대로 둔 옛 번호(예: 지진동 501)까지 막으면 손대지 않은 현장이 저장을 못 한다.
        var chosen = bands;
        bool Numbered(NumberingSensor s) => s.InChain && chosen.BandOf(s.Category) is not null;

        var over = list.Where(s => Numbered(s) && (s.Number > MAX_NUMBER || s.Number < 1)).ToList();
        if (over.Count > 0)
            issues.Add(new NumberingIssue(NumberingIssueKind.OverMax, true,
                $"번호는 1~{MAX_NUMBER} 이어야 합니다 — {Names(over)}", over.Select(s => s.Key).ToList()));

        // 결선에 붙은 센서끼리 겹치면 저장 막음. 미배치 센서(체인에서 뺀 센서)가 끼어서만 겹치면 경고 — 뺀 센서의 옛 번호가 남아 있는 것은
        // 흔하고(번호는 체인 순서로만 다시 매긴다), 그 센서는 결선에 다시 붙일 때 번호를 새로 받는다.
        foreach (var duplicate in list.GroupBy(s => s.Number).Where(g => g.Count() > 1 && g.Any(Numbered)).OrderBy(g => g.Key))
        {
            var blocking = duplicate.Count(s => s.InChain) > 1;
            issues.Add(new NumberingIssue(NumberingIssueKind.Duplicate, blocking,
                blocking
                    ? $"번호 {duplicate.Key} 중복 — {Names(duplicate.ToList())} · 같은 제어기 안에서 번호가 겹치면 저장하지 않습니다."
                    : $"번호 {duplicate.Key} 가 결선에 없는 센서와 겹칩니다 — {Names(duplicate.ToList())} · 그 센서를 다시 붙이거나 번호를 바꾸세요.",
                duplicate.Select(s => s.Key).ToList()));
        }

        return issues;

        static string Names(IReadOnlyList<NumberingSensor> rows)
            => string.Join(", ", rows.Take(6).Select(r => $"{r.Name}({r.Number})")) + (rows.Count > 6 ? $" 외 {rows.Count - 6}" : string.Empty);
    }

    /// <summary>저장 전 번호 표 — 기준 번호와 지금 번호가 다른 센서만(키 순서는 <paramref name="order"/> 를 따른다).</summary>
    public static IReadOnlyList<NumberChange> Changes(IEnumerable<(int Key, string Name, int Before, int After)> order)
        => (order ?? Enumerable.Empty<(int, string, int, int)>())
            .Where(t => t.Before != t.After)
            .Select(t => new NumberChange(t.Key, t.Name, t.Before, t.After))
            .ToList();
}
