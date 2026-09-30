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
/// 결선 검증(wiring-fence-view FR-14 · 옛 WS L695-705) — 전부 순수 함수다. 드래그는 자동화로 단언할 수 없어(UIA 에 드래그 패턴이 없다)
/// 회귀망은 이 함수들과 키보드 폴백이 전부다.
/// </summary>
/// <remarks>
/// N04 의 "2차 선이 비면 루프가 닫히지 않는다(치명)"는 <b>없앴다</b> — 링의 돌아오는 길은 센서가 없는 리턴케이블이라
/// 센서가 한 줄에만 있는 것이 정상이다(PRD §1-A).
/// </remarks>
public static class WiringValidation
{
    public const string CODE_UNPLACED = "unplaced";
    public const string CODE_GAP = "gap";
    public const string CODE_DUPLICATE = "duplicate";
    public const string CODE_LOAD = "load";
    public const string CODE_CHANNEL = "channel";
    public const string CODE_LIMIT = "limit";
    public const string CODE_MIX = "mix";
    public const string CODE_NUMBERING = "numbering";

    /// <summary>제어기 포트 이름(fence-wiring-editor §1-0) — 화면은 "Ch1 · Ch2" 를 주로, A · B 를 괄호 별칭으로 쓴다.</summary>
    public const string PORT_1 = "Ch1(A)";
    public const string PORT_2 = "Ch2(B)";

    /// <summary>고장 구간 예시를 보이기 시작하는 대수(WS L710).</summary>
    public const int FAULT_HINT_MIN = 5;

    public static IReadOnlyList<WiringIssue> Evaluate(WiringBoard board)
    {
        ArgumentNullException.ThrowIfNull(board);
        var issues = new List<WiringIssue>();

        // ⑤ 서버에 저장된 값을 읽지 못했거나 겹친 줄 — 가장 먼저 알린다(자리를 잃은 채로 저장하면 조용히 사라진다).
        foreach (var row in board.Rows.Where(r => !string.IsNullOrEmpty(r.LoadIssue)))
            issues.Add(new WiringIssue(WiringIssueLevel.Critical, CODE_LOAD, $"{row.Display}: {row.LoadIssue}"));

        // ③ 같은 센서가 체인에 두 번(구조상 생기지 않지만, 생겼다면 저장이 순번을 뒤집는다)
        foreach (var key in board.Chain.Keys.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key))
            issues.Add(new WiringIssue(WiringIssueLevel.Critical, CODE_DUPLICATE,
                Particles($"{board.Find(key)?.Display ?? $"센서 {key}"}이(가) 체인에 두 번 있습니다 — 한 곳에서 빼 주세요.")));

        // ④ 기준 길이 · 대수(한도 표 · v0.4 §1-C) — 경고만, 저장은 막지 않는다. 섞어 쓰기는 정상(옛 섞임 경고 O-8 폐기) —
        //    섞였을 때의 기준은 아직 모른다(O-11)는 알림만.
        foreach (var limit in board.Limits.Warnings(board.ChainTypes, board.ChainLengthMetres))
            issues.Add(new WiringIssue(WiringIssueLevel.Warning, CODE_LIMIT, limit));
        if (board.IsMixedFamily)
            issues.Add(new WiringIssue(WiringIssueLevel.Info, CODE_MIX, WiringLimitTable.MIXED_INFO));

        // ② 불러온 배치의 빈 순번 — 당겨 붙였으니 저장하면 서버 순번이 바뀐다
        foreach (var notice in board.LoadNotices.Where(n => n.Code == WiringChain.CODE_GAP))
            issues.Add(new WiringIssue(WiringIssueLevel.Warning, CODE_GAP,
                $"{notice.Message} 적용해 저장하면 뒤 센서들의 순번이 당겨진 값으로 바뀝니다 — 장애의 고장 구간 번호와 맞는지 확인해 주세요."));

        // ⑥ 번호 대역(fence-wiring-editor FR-10) — 대역을 고른 제어기만. 대역 초과 · 255 초과 · 같은 제어기 안 중복 = 저장 막음, 대역 겹침 = 경고.
        foreach (var numbering in board.NumberingIssues())
            issues.Add(new WiringIssue(
                numbering.BlocksSave ? WiringIssueLevel.Critical
                    : numbering.Kind == Monitoring.Models.Fences.NumberingIssueKind.BandOverlap ? WiringIssueLevel.Warning
                    : WiringIssueLevel.Info,
                CODE_NUMBERING, numbering.Message));

        // ① 아직 체인에 없는 센서(알림)
        var unplaced = board.Unplaced.Count;
        if (unplaced > 0)
            issues.Add(new WiringIssue(WiringIssueLevel.Info, CODE_UNPLACED,
                $"아직 결선에 안 붙인 센서 {unplaced}대 — 끌어다 놓거나 [번호 순으로 자동 배치] 를 누르세요. 이 센서의 결선은 저장하지 않습니다."));

        // 버스 주소와 순번이 다른 줄 — 건드리지 않고 알리기만 한다(WS L478, L524)
        var mismatched = board.Rows
            .Where(r => r.Channel is { } ch && board.NumberOf(r.Key) is { } n && ch != n.Order)
            .ToList();
        if (mismatched.Count > 0)
            issues.Add(new WiringIssue(WiringIssueLevel.Info, CODE_CHANNEL,
                $"버스 주소와 순번이 다른 센서 {mismatched.Count}대 — 주소는 그대로 두고 순번만 저장합니다(예: {mismatched[0].Display} 주소 {mismatched[0].Channel} · 순번 {board.NumberOf(mismatched[0].Key)!.Order})."));

        return issues;
    }

    /// <summary>조사 병기("과(와)" · "이(가)" · "은(는)")를 앞 낱말의 받침에 맞게 고른다(커널 · 숫자로 끝나는 이름도).</summary>
    internal static string Particles(string text) => Ironwall.Dotnet.Libraries.Utils.Consoles.KoreanParticles.Resolve(text);

    /// <summary>치명이 하나라도 있으면 저장을 막는다.</summary>
    public static bool BlocksSave(IEnumerable<WiringIssue> issues)
        => issues?.Any(i => i.Level == WiringIssueLevel.Critical) == true;

    /// <summary>
    /// 글로 확인 — "Ch1(A) ─▶ 1. 이름 → … ◀─ Ch2(B)"(모든 제어기가 링 · fence-wiring-editor §1-0 포트 이름).
    /// </summary>
    public static string LoopText(WiringBoard board)
    {
        ArgumentNullException.ThrowIfNull(board);
        // 모든 제어기가 링(§1-0) — 포트는 Ch1(A) · Ch2(B). 옛 가지 · 한 줄 글은 뺐다.
        return $"{PORT_1} ─▶ {Describe(board.Placed(WiringSpec.LINE_PRIMARY))} ◀─ {PORT_2}{Environment.NewLine}"
               + "    (양 끝은 리턴케이블로 함체에 돌아옵니다)";

        static string Describe(IReadOnlyList<WiringSensorRow> placed)
            => placed.Count == 0 ? "(비어 있음)" : string.Join(" → ", placed.Select((r, i) => $"{i + 1}. {r.Display}"));
    }

    /// <summary>고장 구간 예시(WS L709-712) — 장애 화면의 "1차 4~5" 가 어느 센서 사이인지. 링이면 2차(Sensor B 쪽)도 함께.</summary>
    public static string FaultHint(WiringBoard board)
    {
        ArgumentNullException.ThrowIfNull(board);
        if (SequenceFor(board, WiringSpec.LINE_PRIMARY).Count < FAULT_HINT_MIN)
            return $"센서를 {FAULT_HINT_MIN}대 이상 붙이면 예시를 보여 줍니다.";

        var first = $"장애 {DescribeFaultSection(board, WiringSpec.LINE_PRIMARY, 4, 5)}";
        return board.Shape == WiringShape.Ring
            ? $"{first}{Environment.NewLine}장애 {DescribeFaultSection(board, WiringSpec.LINE_SECONDARY, 4, 5)}"
            : first;
    }

    /// <summary>
    /// 장애의 고장 구간(선 + 두 정수)을 센서 이름으로 옮긴다.
    /// </summary>
    /// <remarks>
    /// <para><b>링</b> — 1차 n = Sensor A 쪽에서 센 체인 위치 n · <b>2차 n = 체인 위치 N+1−n</b>(Sensor B 쪽에서 센 수). 같은 체인을 양 끝에서 센다(PRD FR-01 · O-1).</para>
    /// <para><b>양쪽 가지</b> — 선 1 = 왼쪽 · 선 2 = 오른쪽 가지, 제어기 쪽에서 센 수(잠정 O-6). <b>한 줄</b> — 선 1 만 있다.</para>
    /// <para>구간의 정수가 <b>순번</b>인지 <b>버스 주소</b>인지는 서버팀 확인 항목이다(PRD W-D4) — 여기서는 순번으로 읽는다.</para>
    /// </remarks>
    public static string DescribeFaultSection(WiringBoard board, int line, int startOrder, int endOrder)
    {
        ArgumentNullException.ThrowIfNull(board);
        var sequence = SequenceFor(board, line);
        if (sequence.Count == 0) return $"{line}차 선에 붙은 센서가 없어 자리를 알 수 없습니다.";

        var start = Name(startOrder);
        var end = Name(endOrder);
        // 조사는 앞 낱말(숫자로 끝나는 이름 포함)의 받침으로 고른다 — "펜스 3과" · "센서와"(커널 KoreanParticles).
        if (start is null || end is null)
            return Particles($"{line}차 {startOrder}~{endOrder}은(는) 지금 결선 범위(1~{sequence.Max(p => p.Order)}) 밖입니다.");
        return startOrder == endOrder ? $"{line}차 {startOrder}번 = {start}" : Particles($"{line}차 {startOrder}~{endOrder} → {start}과(와) {end} 사이");

        string? Name(int order) => sequence.FirstOrDefault(p => p.Order == order).Row?.Display;
    }

    /// <summary>
    /// 그 선의 (번호, 센서) — 번호가 1, 2, 3… 으로 매겨지는 차례. 적용하지 않은 불러오기 제안이 있으면 <b>받아들인(서버와 같은) 자리</b>로 센다(L2) —
    /// 장애의 고장 구간 번호는 서버에 저장된 번호를 가리키지, 화면의 제안 번호를 가리키지 않는다(빈 번호는 빈 채로).
    /// </summary>
    private static IReadOnlyList<(int Order, WiringSensorRow Row)> SequenceFor(WiringBoard board, int line)
    {
        if (board.HasPendingProposals)
        {
            var saved = board.Rows.Where(r => r.BaselinePlacement is not null).ToList();
            var hasSecond = saved.Any(r => r.BaselinePlacement!.Line == WiringSpec.LINE_SECONDARY);
            if (board.Shape == WiringShape.Ring && line == WiringSpec.LINE_SECONDARY && !hasSecond)
            {
                // 링의 2차 n = 체인 위치 N+1−n — 저장된 한 줄을 반대쪽에서 센다.
                var first = saved.Where(r => r.BaselinePlacement!.Line == WiringSpec.LINE_PRIMARY).ToList();
                var n = first.Count == 0 ? 0 : first.Max(r => r.BaselinePlacement!.Order);
                return first.Select(r => (n + 1 - r.BaselinePlacement!.Order, r)).OrderBy(p => p.Item1).ToList();
            }
            return saved.Where(r => r.BaselinePlacement!.Line == line)
                        .Select(r => (r.BaselinePlacement!.Order, r)).OrderBy(p => p.Item1).ToList();
        }

        IReadOnlyList<WiringSensorRow> rows;
        if (board.Shape == WiringShape.Ring)
        {
            var chain = board.Placed(WiringSpec.LINE_PRIMARY);
            rows = line switch
            {
                WiringSpec.LINE_PRIMARY => chain,
                WiringSpec.LINE_SECONDARY => chain.Reverse().ToList(),     // Sensor B 쪽 끝이 2차 1번
                _ => Array.Empty<WiringSensorRow>(),
            };
        }
        else rows = board.Placed(line);
        return rows.Select((r, i) => (i + 1, r)).ToList();
    }

}
