using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;

/// <summary>체인을 불러올 때 센서 한 대의 입력 — 저장된 자리(<c>spec.wiring</c>)와 기본 순서에 쓰는 번호 · id.</summary>
/// <param name="Key">보드 안정 키(<see cref="WiringSensorRow.Key"/>).</param>
/// <param name="Id">서버 id(새 줄은 0).</param>
/// <param name="Number">장비 번호(<c>number_device</c>) — 서버에서 <b>유일하지 않다</b>.</param>
/// <param name="Placement">저장된 결선 자리. 없으면 <c>null</c>.</param>
public sealed record WiringChainSensor(int Key, int Id, int Number, WiringPlacement? Placement);

/// <summary>체인 위 센서 한 대의 번호.</summary>
/// <param name="Position">왼쪽에서 센 자리(1부터) — 화면 순서.</param>
/// <param name="Line">저장 선 번호 — 링 · 한 줄은 1, 양쪽 가지는 1 = 왼쪽 · 2 = 오른쪽(잠정 O-6).</param>
/// <param name="Order">저장 순번 — 링은 Sensor A 에서 센 수(= 자리), 한 줄 · 가지는 제어기 쪽에서 센 수.</param>
/// <param name="OppositeOrder">링에서만 — Sensor B 에서 센 수(N+1−자리). 그 밖은 <c>null</c>.</param>
public sealed record WiringChainNumber(int Position, int Line, int Order, int? OppositeOrder)
{
    /// <summary>"A3 · B32" — 링 칩의 작은 글씨. 링이 아니면 "3".</summary>
    public string Text => OppositeOrder is { } b ? $"A{Order} · B{b}" : $"{Order}";
}

/// <summary>불러오기에서 알게 된 것 — 겹침(치명) · 빈 자리(경고).</summary>
/// <param name="Key">그 센서(빈 자리처럼 특정 센서가 없으면 <c>null</c>).</param>
public sealed record WiringChainLoadIssue(int? Key, WiringIssueLevel Level, string Code, string Message);

/// <summary>불러오기 결과 — 체인 · 옛 배치 변환 여부 · 알릴 것.</summary>
/// <param name="ConvertedFromLegacy">N04 의 2차 선(<c>line:2</c>)을 한 줄로 이어 붙였는가(FR-02) — 알림을 띄우고 <b>자동 저장하지 않는다</b>.</param>
public sealed record WiringChainLoad(WiringChain Chain, bool ConvertedFromLegacy, IReadOnlyList<WiringChainLoadIssue> Issues)
{
    /// <summary>FR-02 알림 문구.</summary>
    public const string LEGACY_NOTICE = "옛 배치를 한 줄로 바꿨습니다 — 확인 후 저장";

    /// <summary>저장된 배치가 없어 기본 순서로 <b>제안</b>한 센서가 있는가(FR-03).</summary>
    public bool HasSuggestion => Chain.Suggested.Count > 0;
}

/// <summary>
/// 결선 체인 — 제어기 한 대에 붙은 센서의 <b>순서</b>(PRD FR-01 · FR-16). <b>불변</b>이라 편집은 새 체인을 돌려준다
/// (되돌리기는 옛 체인을 쥐고 있으면 된다).
/// </summary>
/// <remarks>
/// <para><b>한 목록 + 제어기 틈</b> — 모든 모양을 화면 왼쪽→오른쪽 순서의 목록 <see cref="Keys"/> 하나와
/// 제어기(함체)가 들어앉은 틈 <see cref="ControllerGap"/>(0…N)으로 나타낸다.</para>
/// <list type="bullet">
/// <item><b>링</b> — 체인 위치 = 자리(Sensor A 쪽 끝이 1). 함체 틈은 <b>표시용</b>(O-5)이라 번호에 영향이 없고,
/// 사람이 옮기기 전까지는 늘 가운데(N/2)다.</item>
/// <item><b>양쪽 가지</b>(잠정 O-6) — 틈 왼쪽이 1차(왼쪽 가지) · 오른쪽이 2차(오른쪽 가지), 각각 <b>제어기 쪽에서 바깥으로</b> 1, 2, 3….
/// 여기서는 틈이 번호를 정한다.</item>
/// <item><b>한 줄</b> — 틈은 늘 0(제어기가 왼쪽 끝), 번호 = 자리.</item>
/// </list>
/// <para><b>틈 번호 규칙</b> — 끼워 넣기 · 옮기기의 <c>gap</c> 은 <b>옮기기 전</b> 목록 기준 0…N 이다(<see cref="DragMath.Move{T}"/> 와 같다).
/// 제어기 틈과 같은 번호에 놓으면 <b>제어기 왼쪽</b>에 들어간다.</para>
/// </remarks>
public sealed class WiringChain
{
    /// <summary>편집 중 제어기 틈을 목록 안에 꽂아 두는 표지 — 센서 키는 절대 이 값이 아니다(새 줄 키는 -1부터 내려간다).</summary>
    private const int GAP_SENTINEL = int.MinValue;

    public const string CODE_DUPLICATE = "chain-duplicate";
    public const string CODE_GAP = "chain-gap";

    private readonly HashSet<int> _suggested;

    private WiringChain(WiringShape shape, IReadOnlyList<int> keys, IReadOnlyList<int> unplaced, HashSet<int> suggested,
                        int controllerGap, bool gapExplicit)
    {
        Shape = shape;
        Keys = keys;
        Unplaced = unplaced;
        _suggested = suggested;
        IsControllerGapExplicit = shape != WiringShape.Line && gapExplicit;
        ControllerGap = NormalizeGap(shape, keys.Count, controllerGap, IsControllerGapExplicit);
    }

    #region - Read -
    public WiringShape Shape { get; }

    /// <summary>체인의 센서 키 — 화면 왼쪽→오른쪽.</summary>
    public IReadOnlyList<int> Keys { get; }

    /// <summary>체인에 없는 센서(팔레트) — 넣은 순서대로.</summary>
    public IReadOnlyList<int> Unplaced { get; }

    /// <summary>저장된 배치가 없어 기본 순서로 <b>제안</b>된 센서(FR-03). [이대로 적용] 전에는 저장 대기가 아니다.</summary>
    public IReadOnlySet<int> Suggested => _suggested;

    /// <summary>제어기(함체)가 들어앉은 틈(0…N). 틈 g 는 <c>Keys[g-1]</c> 과 <c>Keys[g]</c> 사이.</summary>
    public int ControllerGap { get; }

    /// <summary>사람이 함체를 옮겼는가(링) — 아니면 늘 가운데로 다시 잡는다.</summary>
    public bool IsControllerGapExplicit { get; }

    public int Count => Keys.Count;

    public bool Contains(int key) => IndexOf(key) >= 0;

    /// <summary>화면 순서의 인덱스(0부터). 없으면 -1.</summary>
    public int IndexOf(int key)
    {
        for (var i = 0; i < Keys.Count; i++) if (Keys[i] == key) return i;
        return -1;
    }

    /// <summary>자리(1부터). 없으면 0.</summary>
    public int PositionOf(int key) => IndexOf(key) + 1;

    /// <summary>
    /// 그 센서의 번호(FR-01 · FR-16). 링: 1차 = 자리, 2차 = N+1−자리. 한 줄: 제어기 쪽에서 센 자리.
    /// 양쪽 가지: 틈 왼쪽은 선 1 · 오른쪽은 선 2, 각각 제어기 쪽에서 바깥으로. 체인에 없으면 <c>null</c>.
    /// </summary>
    public WiringChainNumber? NumberOf(int key)
    {
        var index = IndexOf(key);
        if (index < 0) return null;
        var position = index + 1;

        return Shape switch
        {
            WiringShape.Ring => new WiringChainNumber(position, WiringSpec.LINE_PRIMARY, position, Count + 1 - position),
            WiringShape.TwoBranch => index < ControllerGap
                ? new WiringChainNumber(position, WiringSpec.LINE_PRIMARY, ControllerGap - index, null)
                : new WiringChainNumber(position, WiringSpec.LINE_SECONDARY, index - ControllerGap + 1, null),
            _ => new WiringChainNumber(position, WiringSpec.LINE_PRIMARY, position, null),
        };
    }

    /// <summary>
    /// 한 가지의 센서를 <b>제어기 쪽에서 바깥으로</b>. 양쪽 가지가 아니면 선 1 = 체인 전체(선 2 는 빈 목록).
    /// </summary>
    public IReadOnlyList<int> Branch(int line)
    {
        if (Shape != WiringShape.TwoBranch)
            return line == WiringSpec.LINE_PRIMARY ? Keys : Array.Empty<int>();

        return line == WiringSpec.LINE_PRIMARY
            ? Keys.Take(ControllerGap).Reverse().ToList()
            : Keys.Skip(ControllerGap).ToList();
    }
    #endregion

    #region - Create / Load -
    /// <summary>빈 체인.</summary>
    public static WiringChain Empty(WiringShape shape) => Create(shape, Array.Empty<int>());

    /// <summary>
    /// 순서를 그대로 받아 만든다. <paramref name="controllerGap"/> 을 주면 사람이 정한 틈으로 본다(링 · 가지). 겹친 키는 앞의 것만 남긴다.
    /// </summary>
    public static WiringChain Create(WiringShape shape, IEnumerable<int> keys, IEnumerable<int>? unplaced = null, int? controllerGap = null)
    {
        var list = (keys ?? Enumerable.Empty<int>()).Where(k => k != GAP_SENTINEL).Distinct().ToList();
        var inChain = new HashSet<int>(list);
        var palette = (unplaced ?? Enumerable.Empty<int>()).Where(k => k != GAP_SENTINEL && !inChain.Contains(k)).Distinct().ToList();
        return new WiringChain(shape, list, palette, new HashSet<int>(), controllerGap ?? 0, controllerGap is not null);
    }

    /// <summary>기본 순서(FR-03) — 장비번호 오름차순, 같으면 id 오름차순(새 줄은 키 순).</summary>
    public static IReadOnlyList<WiringChainSensor> DefaultOrder(IEnumerable<WiringChainSensor> sensors)
        => (sensors ?? Enumerable.Empty<WiringChainSensor>())
            .OrderBy(s => s.Number)
            .ThenBy(s => s.Id <= 0 ? 1 : 0)          // 서버에 있는 센서가 먼저
            .ThenBy(s => s.Id)
            .ThenBy(s => Math.Abs(s.Key))
            .ToList();

    /// <summary>
    /// 저장된 자리(<c>spec.wiring</c>)에서 체인을 세운다(FR-02 · FR-03).
    /// </summary>
    /// <remarks>
    /// <para><b>옛 배치 변환 규칙(FR-02)</b> — 링 · 한 줄에 <c>line:2</c> 가 있으면 <b>1차를 순번 오름차순</b>으로 놓고
    /// <b>2차를 순번 내림차순</b>으로 뒤에 잇는다. N04 의 2차 순번 1 은 <b>Sensor B 포트 바로 옆</b>이라, 뒤집어 이으면
    /// 2차 1번이 체인의 맨 끝(= Sensor B 쪽 끝, 2차 번호 1)에 온다. 예: 1차 [a1, b2, c3] + 2차 [e1, d2] → [a, b, c, d, e].</para>
    /// <para><b>양쪽 가지</b>는 변환하지 않는다 — 선 1 = 왼쪽 가지, 선 2 = 오른쪽 가지가 그대로 뜻이다(잠정 O-6).</para>
    /// <para><b>겹침</b> — 같은 (선, 순번)을 둘이 주장하면 기본 순서가 앞선 센서가 자리를 갖고 나머지는 <b>팔레트</b>로 + 치명 알림(FR-14 ⑤).
    /// <b>빈 순번</b>은 당겨 붙이고 경고로 알린다(FR-14 ②).</para>
    /// <para><b>저장된 자리가 없는 센서</b>는 <paramref name="suggestUnplaced"/> 이면 기본 순서로 체인 <b>끝에</b> 붙이고 제안 표지를 단다.
    /// 아니면 팔레트로. <b>양쪽 가지는 늘 팔레트로</b> — 두 가지 중 어디에 붙일지 번호로는 알 수 없다.</para>
    /// </remarks>
    public static WiringChainLoad Load(WiringShape shape, IEnumerable<WiringChainSensor> sensors, bool suggestUnplaced = true)
    {
        var ordered = DefaultOrder(sensors).GroupBy(s => s.Key).Select(g => g.First()).ToList();
        var rank = ordered.Select((s, i) => (s.Key, i)).ToDictionary(t => t.Key, t => t.i);
        var issues = new List<WiringChainLoadIssue>();
        var palette = new List<int>();

        var lines = new Dictionary<int, List<WiringChainSensor>>
        {
            [WiringSpec.LINE_PRIMARY] = new(),
            [WiringSpec.LINE_SECONDARY] = new(),
        };

        foreach (var line in lines.Keys.ToList())
        {
            var claims = ordered.Where(s => s.Placement?.Line == line)
                                .OrderBy(s => s.Placement!.Order).ThenBy(s => rank[s.Key]);
            var expected = 1;
            int? lastOrder = null;
            foreach (var s in claims)
            {
                var order = s.Placement!.Order;
                if (order == lastOrder)
                {
                    palette.Add(s.Key);
                    issues.Add(new WiringChainLoadIssue(s.Key, WiringIssueLevel.Critical, CODE_DUPLICATE,
                        $"{line}차 {order}번 자리를 다른 센서가 이미 쓰고 있어 팔레트로 뺐습니다."));
                    continue;
                }
                if (order > expected)
                    issues.Add(new WiringChainLoadIssue(null, WiringIssueLevel.Warning, CODE_GAP,
                        expected == order - 1
                            ? $"{line}차 {expected}번 자리가 비어 있어 당겨 붙였습니다."
                            : $"{line}차 {expected}~{order - 1}번 자리가 비어 있어 당겨 붙였습니다."));
                lines[line].Add(s);
                lastOrder = order;
                expected = order + 1;
            }
        }

        var first = lines[WiringSpec.LINE_PRIMARY].Select(s => s.Key).ToList();
        var second = lines[WiringSpec.LINE_SECONDARY].Select(s => s.Key).ToList();

        List<int> keys;
        int? gap = null;
        var legacy = false;
        if (shape == WiringShape.TwoBranch)
        {
            first.Reverse();                     // 왼쪽 가지는 제어기 쪽이 1 — 화면에서는 오른쪽 끝(제어기 옆)
            keys = first.Concat(second).ToList();
            gap = first.Count;
        }
        else
        {
            legacy = second.Count > 0;
            second.Reverse();                    // 2차 1번(Sensor B 옆)이 체인 맨 끝으로
            keys = first.Concat(second).ToList();
        }

        // 양쪽 가지는 제안하지 않는다 — 전체를 한 줄로 번호순 정렬하면 종류가 한쪽에 몰린다(코디네이터 확인 2026-09-29).
        var suggest = suggestUnplaced && shape != WiringShape.TwoBranch;
        var suggested = new HashSet<int>();
        var claimed = new HashSet<int>(keys.Concat(palette));
        foreach (var s in ordered.Where(s => !claimed.Contains(s.Key)))
        {
            if (suggest) { keys.Add(s.Key); suggested.Add(s.Key); }
            else palette.Add(s.Key);
        }

        var chain = new WiringChain(shape, keys, palette, suggested, gap ?? 0, gap is not null);
        return new WiringChainLoad(chain, legacy, issues);
    }
    #endregion

    #region - Edit (새 체인을 돌려준다) -
    /// <summary>
    /// 틈 <paramref name="gap"/>(0…N, 벗어나면 끝으로 눌러 붙인다)에 끼워 넣는다 — 뒤는 한 칸씩 밀린다(FR-08).
    /// 이미 체인에 있으면 <see cref="Move"/> 와 같다. 팔레트에 있었으면 팔레트에서 빠진다.
    /// </summary>
    public WiringChain Insert(int key, int gap)
    {
        if (key == GAP_SENTINEL) return this;
        if (Contains(key)) return Move(key, gap);

        var list = WithSentinel();
        list.Insert(SentinelIndex(gap), key);
        return FromSentinel(list, Unplaced.Where(k => k != key), _suggested);
    }

    /// <summary>체인 맨 끝(양쪽 가지면 오른쪽 가지 바깥 끝)에 붙인다 — 팔레트 Enter(FR-11).</summary>
    public WiringChain Append(int key)
    {
        if (key == GAP_SENTINEL) return this;
        var list = WithSentinel();
        var from = list.IndexOf(key);
        if (from >= 0) DragMath.Move(list, from, list.Count);
        else list.Add(key);
        return FromSentinel(list, Unplaced.Where(k => k != key), _suggested);
    }

    /// <summary>한 센서를 틈 <paramref name="gap"/>(옮기기 <b>전</b> 목록 기준)으로 옮긴다. 체인에 없으면 그대로.</summary>
    public WiringChain Move(int key, int gap) => MoveMany(new[] { key }, gap);

    /// <summary>
    /// 여러 센서를 함께 옮긴다 — 옮긴 것끼리는 <b>체인에서의 원래 순서</b>를 지킨다(고른 순서가 아니라). 체인에 없는 키는 무시한다.
    /// </summary>
    public WiringChain MoveMany(IEnumerable<int> keys, int gap)
    {
        var list = WithSentinel();
        var from = (keys ?? Enumerable.Empty<int>()).Where(k => k != GAP_SENTINEL)
                                                    .Select(k => list.IndexOf(k)).Where(i => i >= 0).Distinct().ToList();
        if (from.Count == 0) return this;

        DragMath.MoveMany(list, from, SentinelIndex(gap));
        return FromSentinel(list, Unplaced, _suggested);
    }

    /// <summary>체인에서 빼 팔레트 끝으로(FR-08 빼는 곳 · FR-11 Delete). 뒤는 한 칸씩 당겨진다.</summary>
    public WiringChain Remove(int key) => RemoveMany(new[] { key });

    /// <summary>여럿을 한꺼번에 뺀다 — 팔레트에는 체인 순서대로 쌓인다.</summary>
    public WiringChain RemoveMany(IEnumerable<int> keys)
    {
        var set = new HashSet<int>(keys ?? Enumerable.Empty<int>());
        var removed = Keys.Where(set.Contains).ToList();
        if (removed.Count == 0) return this;

        var list = WithSentinel();
        list.RemoveAll(k => k != GAP_SENTINEL && set.Contains(k));
        var suggested = new HashSet<int>(_suggested);
        suggested.ExceptWith(removed);
        return FromSentinel(list, Unplaced.Concat(removed), suggested);
    }

    /// <summary>
    /// 함체(제어기)를 틈 <paramref name="gap"/> 으로 옮긴다(FR-09). 링에서는 <b>번호가 바뀌지 않는다</b>(표시용).
    /// 한 줄은 제어기가 늘 왼쪽 끝이라 그대로.
    /// </summary>
    public WiringChain WithControllerGap(int gap)
        => Shape == WiringShape.Line ? this
         : new WiringChain(Shape, Keys, Unplaced, _suggested, Math.Clamp(gap, 0, Count), gapExplicit: true);

    /// <summary>
    /// 번호순 배치(FR-03) — 장비번호 오름차순, 같으면 id 오름차순. 링 · 한 줄은 체인 전체를 다시 줄 세우고
    /// <paramref name="includeUnplaced"/> 이면 팔레트 센서도 끝까지 이어 넣는다.
    /// <b>양쪽 가지는 가지 안에서만</b> 제어기 쪽부터 줄 세우고 팔레트는 그대로 둔다(전체 정렬은 종류를 한쪽에 몰아넣는다).
    /// 번호를 모르는 키(<paramref name="sensors"/> 에 없음)는 같은 가지 맨 바깥에 원래 순서대로 남는다.
    /// </summary>
    public WiringChain SortByDefaultOrder(IEnumerable<WiringChainSensor> sensors, bool includeUnplaced = true)
    {
        var rank = DefaultOrder(sensors).Select((s, i) => (s.Key, i))
                                        .GroupBy(t => t.Key).ToDictionary(g => g.Key, g => g.First().i);
        List<int> Sort(IEnumerable<int> keys) => keys.Select((k, i) => (k, i))
            .OrderBy(t => rank.TryGetValue(t.k, out var r) ? r : int.MaxValue).ThenBy(t => t.i)
            .Select(t => t.k).ToList();

        if (Shape == WiringShape.TwoBranch)
        {
            var left = Sort(Branch(WiringSpec.LINE_PRIMARY));          // 제어기 쪽부터
            left.Reverse();                                            // 화면에서는 제어기가 오른쪽 끝
            var keys = left.Concat(Sort(Branch(WiringSpec.LINE_SECONDARY))).ToList();
            var keepSuggested = new HashSet<int>(_suggested);
            return new WiringChain(Shape, keys, Unplaced, keepSuggested, ControllerGap, IsControllerGapExplicit);
        }

        var all = Sort(includeUnplaced ? Keys.Concat(Unplaced) : Keys);
        var palette = includeUnplaced ? Array.Empty<int>() : Unplaced;
        return new WiringChain(Shape, all, palette, new HashSet<int>(_suggested.Where(all.Contains)), ControllerGap, IsControllerGapExplicit);
    }

    /// <summary>[이대로 적용](FR-03) — 제안 표지를 걷어 저장 대기로 만든다.</summary>
    public WiringChain AcceptSuggestions()
        => _suggested.Count == 0 ? this
         : new WiringChain(Shape, Keys, Unplaced, new HashSet<int>(), ControllerGap, IsControllerGapExplicit);
    #endregion

    #region - Save -
    /// <summary>
    /// 저장할 자리 — 체인 센서는 <c>{line, order}</c>(<see cref="NumberOf"/>), 팔레트 센서는 <c>null</c>.
    /// <paramref name="includeSuggested"/> 가 거짓이면 제안 센서도 <c>null</c>(아직 사람이 적용하지 않았다).
    /// </summary>
    /// <remarks>순번은 서버 일괄 API 전까지 <see cref="WiringSpec.MAX_ORDER"/> 를 넘을 수 있다 — 그 상한 조정은 저장 전환(F-2)의 몫이다.</remarks>
    public IReadOnlyDictionary<int, WiringPlacement?> ToPlacements(bool includeSuggested = true)
    {
        var result = new Dictionary<int, WiringPlacement?>();
        foreach (var key in Keys)
        {
            if (!includeSuggested && _suggested.Contains(key)) { result[key] = null; continue; }
            var n = NumberOf(key)!;
            result[key] = new WiringPlacement(n.Line, n.Order);
        }
        foreach (var key in Unplaced) result[key] = null;
        return result;
    }

    /// <summary>
    /// 기준(불러올 때 · 마지막 저장)과 자리가 달라진 센서 — 저장에 나갈 것. 제안 센서는 적용 전이면 세지 않는다.
    /// 기준에만 있고 체인 · 팔레트 어디에도 없는 키는 "자리 없음"과 비교한다.
    /// </summary>
    public IReadOnlyList<int> ChangedKeys(IReadOnlyDictionary<int, WiringPlacement?> baseline, bool includeSuggested = false)
    {
        var now = ToPlacements(includeSuggested);
        var all = now.Keys.Concat(baseline?.Keys ?? Enumerable.Empty<int>()).Distinct();
        return all.Where(k =>
        {
            now.TryGetValue(k, out var current);
            WiringPlacement? before = null;
            baseline?.TryGetValue(k, out before);
            return !WiringSpec.SamePlacement(current, before);
        }).ToList();
    }
    #endregion

    #region - Internals -
    private static int NormalizeGap(WiringShape shape, int count, int gap, bool gapExplicit) => shape switch
    {
        WiringShape.Line => 0,
        WiringShape.Ring when !gapExplicit => count / 2,
        _ => Math.Clamp(gap, 0, count),
    };

    /// <summary>체인 키 목록에 제어기 틈 표지를 꽂은 편집용 사본.</summary>
    private List<int> WithSentinel()
    {
        var list = Keys.ToList();
        list.Insert(ControllerGap, GAP_SENTINEL);
        return list;
    }

    /// <summary>체인 틈 번호 → 표지가 든 목록의 인덱스. 제어기 틈과 같은 번호는 <b>표지 앞</b>(제어기 왼쪽).</summary>
    private int SentinelIndex(int gap)
    {
        var g = Math.Clamp(gap, 0, Count);
        return g <= ControllerGap ? g : g + 1;
    }

    private WiringChain FromSentinel(List<int> list, IEnumerable<int> unplaced, HashSet<int> suggested)
    {
        var gap = list.IndexOf(GAP_SENTINEL);
        list.RemoveAt(gap);
        var inChain = new HashSet<int>(list);
        var palette = unplaced.Where(k => !inChain.Contains(k)).Distinct().ToList();
        var keep = new HashSet<int>(suggested.Where(inChain.Contains));
        return new WiringChain(Shape, list, palette, keep, gap, IsControllerGapExplicit || Shape == WiringShape.TwoBranch);
    }
    #endregion
}
