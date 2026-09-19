using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;

/// <summary>얼마나 나쁜가 — 치명만 저장을 막는다.</summary>
public enum WiringIssueLevel
{
    /// <summary>알아 두면 좋은 것(버스 주소와 순번이 다르다 등).</summary>
    Info = 0,
    /// <summary>저장은 되지만 확인하고 가야 하는 것.</summary>
    Warning = 1,
    /// <summary>저장을 막는다.</summary>
    Critical = 2,
}

/// <summary>검증 한 줄 — <b>무엇이 · 어디가 · 어떻게</b>(WS L186-188).</summary>
public sealed record WiringIssue(WiringIssueLevel Level, string Code, string Message);

/// <summary>
/// 루프 검증(WS L695-705) — 전부 순수 함수다. 드래그는 자동화로 단언할 수 없어(UIA 에 드래그 패턴이 없다)
/// 회귀망은 이 함수들과 키보드 폴백이 전부다.
/// </summary>
public static class WiringValidation
{
    public const string CODE_UNPLACED = "unplaced";
    public const string CODE_GAP = "gap";
    public const string CODE_LOOP_OPEN = "loop-open";
    public const string CODE_DUPLICATE = "duplicate";
    public const string CODE_LOAD = "load";
    public const string CODE_CHANNEL = "channel";

    /// <summary>고장 구간 예시를 보이기 시작하는 대수(WS L710).</summary>
    public const int FAULT_HINT_MIN = 5;

    public static IReadOnlyList<WiringIssue> Evaluate(WiringBoard board)
    {
        ArgumentNullException.ThrowIfNull(board);
        var issues = new List<WiringIssue>();

        // ⑤ 서버에 저장된 값을 읽지 못한 줄 — 가장 먼저 알린다(자리를 잃은 채로 저장하면 조용히 사라진다).
        foreach (var row in board.Rows.Where(r => !string.IsNullOrEmpty(r.LoadIssue)))
            issues.Add(new WiringIssue(WiringIssueLevel.Critical, CODE_LOAD, $"{row.Display}: {row.LoadIssue}"));

        // ④ 같은 센서가 두 칸에(구조상 생기지 않지만, 생겼다면 저장이 순번을 뒤집는다)
        foreach (var line in new[] { WiringSpec.LINE_PRIMARY, WiringSpec.LINE_SECONDARY })
        {
            var duplicated = board.Line(line).Where(k => k is not null)
                                  .GroupBy(k => k!.Value).Where(g => g.Count() > 1).Select(g => g.Key);
            foreach (var key in duplicated)
                issues.Add(new WiringIssue(WiringIssueLevel.Critical, CODE_DUPLICATE,
                    $"{board.Find(key)?.Display ?? $"센서 {key}"} 이(가) {line}차 선의 두 칸에 있습니다 — 한 칸에서 빼 주세요."));
        }

        // ③ 1차에 센서가 있는데 2차가 비었다 — 루프가 닫히지 않는다
        var first = board.Placed(WiringSpec.LINE_PRIMARY).Count;
        var second = board.Placed(WiringSpec.LINE_SECONDARY).Count;
        if (first > 0 && second == 0)
            issues.Add(new WiringIssue(WiringIssueLevel.Critical, CODE_LOOP_OPEN,
                "2차 선이 비어 있습니다 — 루프가 닫히지 않습니다. 제어기로 돌아오는 센서를 2차 선에 놓아 주세요."));

        // ② 선 가운데의 빈 칸
        foreach (var line in new[] { WiringSpec.LINE_PRIMARY, WiringSpec.LINE_SECONDARY })
        {
            var gap = FirstGap(board, line);
            if (gap is { } slot)
                issues.Add(new WiringIssue(WiringIssueLevel.Warning, CODE_GAP,
                    $"{line}차 선 {slot}번 자리가 비어 있어요. 센서를 끌어다 놓거나 [번호 순으로 자동 배치] 로 순번을 다시 매겨 주세요."));
        }

        // ① 아직 선에 안 붙인 센서
        var unplaced = board.Unplaced.Count;
        if (unplaced > 0)
            issues.Add(new WiringIssue(WiringIssueLevel.Warning, CODE_UNPLACED,
                $"아직 선에 안 붙인 센서 {unplaced}대 — 끌어다 놓거나 [번호 순으로 자동 배치] 를 누르세요. 이 센서의 결선은 저장하지 않습니다."));

        // 버스 주소와 순번이 다른 줄 — 건드리지 않고 알리기만 한다(WS L478, L524)
        var mismatched = board.Rows
            .Where(r => r.Channel is { } ch && board.PlacementOf(r.Key) is { } p && ch != p.Order)
            .ToList();
        if (mismatched.Count > 0)
            issues.Add(new WiringIssue(WiringIssueLevel.Info, CODE_CHANNEL,
                $"버스 주소와 순번이 다른 센서 {mismatched.Count}대 — 주소는 그대로 두고 순번만 저장합니다(예: {mismatched[0].Display} 주소 {mismatched[0].Channel} · 순번 {board.PlacementOf(mismatched[0].Key)!.Order})."));

        return issues;
    }

    /// <summary>치명이 하나라도 있으면 저장을 막는다.</summary>
    public static bool BlocksSave(IEnumerable<WiringIssue> issues)
        => issues?.Any(i => i.Level == WiringIssueLevel.Critical) == true;

    /// <summary>그 선에서 <b>마지막 찬 칸보다 앞</b>에 있는 첫 빈 칸의 칸 번호(1부터). 없으면 <c>null</c>.</summary>
    public static int? FirstGap(WiringBoard board, int line)
    {
        var slots = board.Line(line);
        var last = -1;
        for (var i = 0; i < slots.Count; i++) if (slots[i] is not null) last = i;
        if (last < 0) return null;

        for (var i = 0; i < last; i++) if (slots[i] is null) return i + 1;
        return null;
    }

    /// <summary>글로 확인(WS L706-708) — "제어기 ─1차▶ 1. 이름 → … ⟲ 제어기 ◀2차─ …".</summary>
    public static string LoopText(WiringBoard board)
    {
        ArgumentNullException.ThrowIfNull(board);
        var first = Describe(board, WiringSpec.LINE_PRIMARY);
        var second = Describe(board, WiringSpec.LINE_SECONDARY);
        return $"제어기 ─1차▶ {first}{Environment.NewLine}    ⟲{Environment.NewLine}제어기 ◀2차─ {second}";

        static string Describe(WiringBoard b, int line)
        {
            var placed = b.Placed(line);
            if (placed.Count == 0) return "(비어 있음)";
            return string.Join(" → ", placed.Select((r, i) => $"{i + 1}. {r.Display}"));
        }
    }

    /// <summary>고장 구간 예시(WS L709-712) — 장애 화면의 "1차 4~5" 가 어느 센서 사이인지.</summary>
    public static string FaultHint(WiringBoard board)
    {
        ArgumentNullException.ThrowIfNull(board);
        var placed = board.Placed(WiringSpec.LINE_PRIMARY);
        if (placed.Count < FAULT_HINT_MIN) return $"1차 선에 {FAULT_HINT_MIN}대 이상 붙이면 예시를 보여 줍니다.";
        return $"장애 \"1차 4~5\" → {placed[3].Display} 와 {placed[4].Display} 사이";
    }

    /// <summary>
    /// 장애의 고장 구간(선 + 두 정수)을 센서 이름으로 옮긴다 — 1차로 나가 2차로 들어오는 루프 위의 지점이다.
    /// </summary>
    /// <remarks>구간의 정수가 <b>순번</b>인지 <b>버스 주소</b>인지는 서버팀 확인 항목이다(PRD W-D4) — 여기서는 순번으로 읽는다.</remarks>
    public static string DescribeFaultSection(WiringBoard board, int line, int startOrder, int endOrder)
    {
        ArgumentNullException.ThrowIfNull(board);
        var placed = board.Placed(line);
        if (placed.Count == 0) return $"{line}차 선에 붙은 센서가 없어 자리를 알 수 없습니다.";

        var start = Name(startOrder);
        var end = Name(endOrder);
        if (start is null || end is null) return $"{line}차 {startOrder}~{endOrder} 는 지금 결선 범위(1~{placed.Count}) 밖입니다.";
        return startOrder == endOrder ? $"{line}차 {startOrder}번 = {start}" : $"{line}차 {startOrder}~{endOrder} → {start} 와 {end} 사이";

        string? Name(int order) => order >= 1 && order <= placed.Count ? placed[order - 1].Display : null;
    }
}
