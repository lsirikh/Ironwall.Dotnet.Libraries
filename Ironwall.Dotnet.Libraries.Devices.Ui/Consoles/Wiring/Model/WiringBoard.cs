using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;

/// <summary>센서 한 줄이 담는 <b>사실</b> — 표의 네 칸(WS L571). 값 비교로 "바뀐 줄"을 센다.</summary>
/// <param name="Number">장비 번호(<c>number_device</c>).</param>
/// <param name="Name">이름(<c>name_device</c>).</param>
/// <param name="TypeText">센서 종류(<c>type_device</c> · 7.0 <c>type_sensor</c>). 값을 보정하지 않는다.</param>
/// <param name="Zone">구역 = 위치 설명(<c>geolocation.location</c>).</param>
public sealed record SensorFacts(int Number, string Name, string TypeText, string Zone)
{
    public static readonly SensorFacts Empty = new(0, string.Empty, string.Empty, string.Empty);
}

/// <summary>
/// 표와 결선맵이 함께 보는 센서 한 줄. <b>저장 전까지 Draft</b>(WS L181).
/// </summary>
public sealed class WiringSensorRow
{
    internal WiringSensorRow(int key, int id, int? channel, SensorFacts facts, WiringPlacement? placement, string? loadIssue,
                             IEnumerable<int>? groups = null)
    {
        Key = key;
        Id = id;
        Channel = channel;
        Facts = facts;
        Baseline = facts;
        BaselinePlacement = placement;
        LoadIssue = loadIssue;
        Groups = new HashSet<int>(groups ?? Enumerable.Empty<int>());
        BaselineGroups = new HashSet<int>(Groups);
    }

    /// <summary>보드 안에서만 쓰는 안정 키 — 기존 센서는 <see cref="Id"/>, 새 줄은 음수.</summary>
    public int Key { get; }

    /// <summary>서버 Id. <c>0</c> 이면 아직 서버에 없다(저장 때 만든다).</summary>
    public int Id { get; }

    /// <summary>버스 주소(<c>connection.channel</c>) — <b>읽기 전용</b>. 결선 순번과 다를 수 있다(WS L478).</summary>
    public int? Channel { get; }

    public SensorFacts Facts { get; set; }

    /// <summary>마지막으로 서버와 맞춘 값.</summary>
    public SensorFacts Baseline { get; internal set; }

    /// <summary>불러올 때의 결선 자리 — 저장 직전 비교의 기준(WS L477).</summary>
    public WiringPlacement? BaselinePlacement { get; internal set; }

    /// <summary>서버에 저장된 결선 값을 읽지 못했을 때의 까닭(있으면 미배치로 둔다).</summary>
    public string? LoadIssue { get; internal set; }

    /// <summary>이 줄이 지금 속한 그룹(Draft) — 저장 때 <b>변화분만</b> 나간다(W2).</summary>
    public HashSet<int> Groups { get; }

    /// <summary>마지막으로 서버와 맞춘 그룹.</summary>
    public HashSet<int> BaselineGroups { get; private set; }

    public bool IsNew => Id <= 0;
    public bool FactsChanged => !Facts.Equals(Baseline);
    public bool GroupsChanged => !Groups.SetEquals(BaselineGroups);

    /// <summary>저장이 끝난 그룹을 새 기준으로.</summary>
    internal void MarkGroupBaseline() => BaselineGroups = new HashSet<int>(Groups);

    /// <summary>서버가 맞춰 준 <b>그룹 하나</b>만 기준선에 옮긴다 — 실패한 다른 그룹 변경은 그대로 "바뀐 것"으로 남는다.</summary>
    internal void MarkGroupBaseline(int groupId, bool member)
    {
        if (member) BaselineGroups.Add(groupId);
        else BaselineGroups.Remove(groupId);
    }

    /// <summary>다른 줄 객체의 그룹 기준선을 그대로 가져온다(저장으로 Id 를 받은 줄을 갈아 끼울 때).</summary>
    internal void CopyGroupBaselineFrom(WiringSensorRow other) => BaselineGroups = new HashSet<int>(other.BaselineGroups);

    /// <summary>이름이 비면 번호로 부른다 — 고스트·문장에서 빈 칸이 보이지 않게.</summary>
    public string Display => string.IsNullOrWhiteSpace(Facts.Name) ? $"센서 {Facts.Number}" : Facts.Name;

    public override string ToString() => Display;
}

/// <summary>무엇이 바뀌었나 — 저장 전 미리보기(WS L450, L772-776).</summary>
public sealed record WiringBoardDiff(
    IReadOnlyList<WiringSensorRow> Created,
    IReadOnlyList<WiringSensorRow> FactChanged,
    IReadOnlyList<WiringSensorRow> WiringChanged,
    IReadOnlyList<WiringSensorRow> GroupChanged)
{
    /// <summary>장비 호출로 나갈 줄(한 줄이 표·결선 둘 다 바뀌어도 <b>호출은 한 번</b>). 그룹은 따로 나간다.</summary>
    public IReadOnlyList<WiringSensorRow> ToSend { get; } =
        Created.Concat(FactChanged).Concat(WiringChanged).Distinct().OrderBy(r => r.Facts.Number).ToList();

    /// <summary>저장할 것이 하나도 없는가 — 그룹만 바뀐 경우도 "있다"로 센다.</summary>
    public bool IsEmpty => ToSend.Count == 0 && GroupChanged.Count == 0;
}

/// <summary>
/// 결선 보드 — 제어기 한 대의 센서 표와 <b>결선 체인</b>(<see cref="WiringChain"/>). <b>판정은 전부 여기</b>(UI 없음 · 헤드리스 테스트 대상).
/// </summary>
/// <remarks>
/// <para><b>체인 모델(wiring-fence-view F-2)</b> — N04 의 "1차 · 2차 선에 칸을 두고 반씩 꽂는다"는 링 제품 구조와 맞지 않았다(PRD §1-A).
/// 결선 모양은 제어기 종류가 정하고(<see cref="WiringTopology"/>), 순서는 체인 하나(양쪽 가지면 가지 둘)다.
/// 빈 칸이 없다 — 끼워 넣으면 뒤가 밀리고, 빼면 뒤가 당겨진다.</para>
/// <para><b>선 번호</b> — 목록 보기의 선은 링 · 한 줄이면 <b>하나</b>(선 1 = 체인 전체, Sensor A · 제어기 쪽부터), 양쪽 가지면 <b>둘</b>
/// (선 1 = 왼쪽 가지 · 선 2 = 오른쪽 가지, 각각 제어기 쪽부터). 목록 자리 i 의 순번은 늘 i+1 이다.</para>
/// <para><b>제안(FR-03)</b> — 저장된 자리가 없는 센서는 불러올 때 번호순으로 체인 끝에 <b>제안</b>으로 붙는다.
/// 제안은 [이대로 적용](<see cref="AcceptSuggestions"/>) 전까지 저장 대기가 아니다(<see cref="PlacementOf"/> 가 <c>null</c>).</para>
/// <para>되돌리기는 <b>한 스택</b>이다 — 표 값과 체인을 같이 찍는다(WS L181). 체인은 불변이라 장면에 그대로 담긴다.</para>
/// </remarks>
public sealed class WiringBoard
{
    /// <summary>되돌리기 깊이 — 한 세션에서 이보다 더 거슬러 가지 않는다.</summary>
    public const int MAX_UNDO = 100;

    private readonly List<WiringSensorRow> _rows = new();
    private readonly Stack<Snapshot> _undo = new();
    private readonly List<WiringChainLoadIssue> _loadNotices = new();
    private WiringChain _chain;
    private string? _controllerType;
    private int _nextNewKey = -1;

    public WiringBoard()
    {
        Topology = WiringTopology.For(null, Array.Empty<EnumDeviceType>());
        _chain = WiringChain.Empty(Topology.Shape);
    }

    #region - Read -
    public IReadOnlyList<WiringSensorRow> Rows => _rows;

    /// <summary>불러올 때 정한 결선 모양 — 편집 중에는 바뀌지 않는다(바뀌면 번호의 뜻이 바뀐다).</summary>
    public WiringTopology Topology { get; private set; }

    public WiringShape Shape => Topology.Shape;

    /// <summary>지금 체인(불변 값).</summary>
    public WiringChain Chain => _chain;

    /// <summary>목록 보기의 선 수 — 양쪽 가지만 2.</summary>
    public int LineCount => Shape == WiringShape.TwoBranch ? 2 : 1;

    /// <summary>불러올 때 N04 의 2차 선을 한 줄로 이어 붙였는가(FR-02) — 저장 전까지 알린다.</summary>
    public bool ConvertedFromLegacy { get; private set; }

    /// <summary>불러오기에서 당겨 붙인 빈 순번 같은 경고(FR-14 ②).</summary>
    public IReadOnlyList<WiringChainLoadIssue> LoadNotices => _loadNotices;

    /// <summary>번호순 제안으로 붙어 있는 센서 수(FR-03).</summary>
    public int SuggestedCount => _chain.Suggested.Count;

    public bool HasSuggestion => SuggestedCount > 0;

    public bool IsSuggested(int key) => _chain.Suggested.Contains(key);

    /// <summary>
    /// 지금 표의 센서 종류로 본 섞임 경고(O-8) — 종류를 표에서 고치면 바로 따라온다. 모양은 바꾸지 않는다.
    /// </summary>
    public string? MixWarning
        => WiringTopology.For(_controllerType, _rows.Select(r => WiringTopology.ParseSensorType(r.Facts.TypeText)).ToList()).MixWarning;

    /// <summary>그 선의 센서 키 — 목록 순서(링 · 한 줄: 체인 순서 · 가지: 제어기 쪽부터). 없는 선은 빈 목록.</summary>
    public IReadOnlyList<int> Line(int line)
        => line >= 1 && line <= LineCount ? _chain.Branch(line) : Array.Empty<int>();

    /// <summary>그 선에 붙은 센서 수.</summary>
    public int CountOn(int line) => Line(line).Count;

    public WiringSensorRow? RowAt(int line, int index)
    {
        var keys = Line(line);
        return index >= 0 && index < keys.Count ? Find(keys[index]) : null;
    }

    public WiringSensorRow? Find(int key) => _rows.FirstOrDefault(r => r.Key == key);

    /// <summary>체인에 없는 센서 — 팔레트(WS L409). 번호순.</summary>
    public IReadOnlyList<WiringSensorRow> Unplaced
        => _rows.Where(r => !_chain.Contains(r.Key)).OrderBy(r => r.Facts.Number).ThenBy(r => r.Key).ToList();

    /// <summary>목록 자리의 순번(1부터) = 자리 + 1. 범위 밖이면 0.</summary>
    public int OrderAt(int line, int index) => index >= 0 && index < CountOn(line) ? index + 1 : 0;

    /// <summary>체인 위 번호(자리 · 선 · 순번 · 링이면 B 번호). 제안 센서도 번호가 있다(보이기용).</summary>
    public WiringChainNumber? NumberOf(int key) => _chain.NumberOf(key);

    /// <summary>
    /// <b>저장될</b> 결선 자리 — 체인에 있으면 <c>{line, order}</c>, 팔레트면 <c>null</c>.
    /// <b>제안 센서는 <c>null</c></b>(적용 전에는 저장 대기가 아니다 · O-3).
    /// </summary>
    public WiringPlacement? PlacementOf(int key)
        => IsSuggested(key) ? null : DisplayPlacementOf(key);

    /// <summary>화면에 보일 자리 — 제안 센서도 자리를 보인다.</summary>
    public WiringPlacement? DisplayPlacementOf(int key)
        => _chain.NumberOf(key) is { } n ? new WiringPlacement(n.Line, n.Order) : null;

    /// <summary>그 선에 붙은 센서를 목록 순서로.</summary>
    public IReadOnlyList<WiringSensorRow> Placed(int line)
        => Line(line).Select(Find).Where(r => r is not null).Select(r => r!).ToList();
    #endregion

    #region - Load -
    /// <summary>
    /// 서버에서 받은 센서로 보드를 채운다(FR-01 ~ FR-03 · FR-16).
    /// </summary>
    /// <param name="controllerType">제어기 <c>type_controller</c> 원값 — 결선 모양을 정한다. 모르면 센서 종류로 추정한다.</param>
    /// <remarks>
    /// 옛 두 선 배치는 한 줄로 바꾸고(<see cref="ConvertedFromLegacy"/> · 바뀐 센서는 "바뀐 줄"로 센다 — 자동 저장하지 않는다),
    /// 같은 자리를 둘이 주장하면 뒤의 센서는 팔레트로 + 까닭(치명), 저장된 결선을 읽지 못한 센서도 팔레트로 둔다.
    /// </remarks>
    public void Load(IEnumerable<(int Id, int? Channel, SensorFacts Facts, WiringPlacement? Placement, string? Issue, IReadOnlyList<int>? Groups)> sensors,
                     string? controllerType = null)
    {
        _rows.Clear();
        _undo.Clear();
        _loadNotices.Clear();
        _controllerType = controllerType;

        var loaded = sensors?.ToList() ?? new();
        foreach (var s in loaded)
            _rows.Add(new WiringSensorRow(s.Id > 0 ? s.Id : _nextNewKey--, s.Id, s.Channel, s.Facts, s.Placement, s.Issue, s.Groups));

        Topology = WiringTopology.For(controllerType, _rows.Select(r => WiringTopology.ParseSensorType(r.Facts.TypeText)).ToList());

        var result = WiringChain.Load(Shape, _rows.Select(r => new WiringChainSensor(r.Key, r.Id, r.Facts.Number, r.BaselinePlacement)));
        var chain = result.Chain;

        foreach (var issue in result.Issues)
        {
            if (issue.Key is { } key && Find(key) is { } row && issue.Level == WiringIssueLevel.Critical)
            {
                row.LoadIssue = issue.Message;
                row.BaselinePlacement = null;      // 기준도 "미배치" 다 — 사람이 자리를 정해 주면 그 줄만 나간다
            }
            else _loadNotices.Add(issue);
        }

        // 저장된 결선을 읽지 못한 센서는 제안하지 않는다 — 사람이 다시 배치해야 한다(C4).
        var unreadable = _rows.Where(r => !string.IsNullOrEmpty(r.LoadIssue) && chain.Suggested.Contains(r.Key)).Select(r => r.Key).ToList();
        if (unreadable.Count > 0) chain = chain.RemoveMany(unreadable);

        _chain = chain;
        ConvertedFromLegacy = result.ConvertedFromLegacy;
    }

    /// <summary>새 줄을 더한다(센서 여러 개 만들기 · 엑셀 붙여넣기). 팔레트에 선다 — 서버에는 저장 때 만든다.</summary>
    public WiringSensorRow AddRow(SensorFacts facts, IEnumerable<int>? groups = null)
    {
        var row = new WiringSensorRow(_nextNewKey--, 0, null, facts, null, null, groups);
        _rows.Add(row);
        return row;
    }
    #endregion

    #region - Edit -
    /// <summary>
    /// 선 <paramref name="line"/> 의 <paramref name="index"/> 자리(옮기기 <b>전</b> 목록 기준 0…개수)에 끼워 넣는다 — 뒤는 밀린다.
    /// 없는 선 · 모르는 센서 · 제자리면 <c>false</c>(아무것도 바뀌지 않는다).
    /// </summary>
    public bool Place(int key, int line, int index) => PlaceMany(new[] { key }, line, index) > 0;

    /// <summary>여러 대를 한 덩어리로 끼워 넣는다(여럿 끌기). 놓은 수를 돌려준다 — 바뀐 것이 없으면 0.</summary>
    public int PlaceMany(IEnumerable<int> keys, int line, int index)
    {
        if (line < 1 || line > LineCount) return 0;
        var known = (keys ?? Enumerable.Empty<int>()).Where(k => Find(k) is not null).Distinct().ToList();
        if (known.Count == 0) return 0;

        var next = _chain.PlaceInBranch(known, line, index).AcceptSuggestions(known);
        return Commit(next, known) ? known.Count : 0;
    }

    /// <summary>체인 끝에 붙인다(팔레트 Enter · FR-11) — 양쪽 가지면 오른쪽 가지 바깥 끝. 붙인 수.</summary>
    public int Append(IEnumerable<int> keys)
    {
        var known = (keys ?? Enumerable.Empty<int>()).Where(k => Find(k) is not null && !_chain.Contains(k)).Distinct().ToList();
        if (known.Count == 0) return 0;

        var next = _chain;
        foreach (var key in known) next = next.Append(key);
        return Commit(next, known) ? known.Count : 0;
    }

    /// <summary>체인에서 뺀다 — 뒤는 한 칸씩 당겨진다.</summary>
    public bool Unplace(int key) => UnplaceMany(new[] { key }) > 0;

    /// <summary>여럿을 한꺼번에 뺀다. 뺀 수.</summary>
    public int UnplaceMany(IEnumerable<int> keys)
    {
        var inChain = (keys ?? Enumerable.Empty<int>()).Where(_chain.Contains).Distinct().ToList();
        if (inChain.Count == 0) return 0;
        return Commit(_chain.RemoveMany(inChain), inChain) ? inChain.Count : 0;
    }

    /// <summary>
    /// 한 칸 옮긴다(Alt+← · Alt+→) — 같은 선 안에서만. 끝이면 <c>false</c>.
    /// </summary>
    public bool MoveBy(int key, int direction)
    {
        if (direction == 0 || LocationOf(key) is not { } at) return false;
        var count = CountOn(at.Line);
        if (direction < 0)
            return at.Index > 0 && Place(key, at.Line, at.Index - 1);
        return at.Index < count - 1 && Place(key, at.Line, at.Index + 2);     // "옮기기 전" 기준이라 +2 가 한 칸 뒤다
    }

    /// <summary>
    /// 다른 가지로 옮긴다(Alt+↑ · Alt+↓) — <b>양쪽 가지에서만</b>. 같은 자리(없으면 그 가지 끝)로 간다.
    /// </summary>
    public bool MoveToOtherLine(int key)
    {
        if (Shape != WiringShape.TwoBranch || LocationOf(key) is not { } at) return false;
        var other = at.Line == WiringSpec.LINE_PRIMARY ? WiringSpec.LINE_SECONDARY : WiringSpec.LINE_PRIMARY;
        return Place(key, other, Math.Min(at.Index, CountOn(other)));
    }

    /// <summary>센서의 목록 위치(선 · 자리). 체인에 없으면 <c>null</c>.</summary>
    public (int Line, int Index)? LocationOf(int key)
    {
        for (var line = 1; line <= LineCount; line++)
        {
            var keys = Line(line);
            for (var i = 0; i < keys.Count; i++) if (keys[i] == key) return (line, i);
        }
        return null;
    }

    /// <summary>
    /// 번호 순으로 배치(FR-03) — 링 · 한 줄은 <b>전체</b>(팔레트 포함)를 장비번호 → id 순으로 한 줄에,
    /// 양쪽 가지는 <b>가지 안에서만</b> 줄 세운다(팔레트는 그대로). 사람이 누른 것이라 제안 표지도 걷는다.
    /// 센서가 없으면 <c>false</c>.
    /// </summary>
    public bool AutoLayoutByNumber()
    {
        if (_rows.Count == 0) return false;
        var sensors = _rows.Select(r => new WiringChainSensor(r.Key, r.Id, r.Facts.Number, null));

        // 팔레트는 보드가 쥔다(불러온 뒤 더한 줄은 체인이 모른다) — 링 · 한 줄은 먼저 체인 끝에 붙이고 전체를 줄 세운다.
        var next = _chain;
        if (Shape != WiringShape.TwoBranch)
            foreach (var row in Unplaced) next = next.Append(row.Key);

        _chain = next.SortByDefaultOrder(sensors, includeUnplaced: false).AcceptSuggestions();
        foreach (var row in _rows.Where(r => _chain.Contains(r.Key))) row.LoadIssue = null;   // 자리를 다시 정했다(C4)
        return true;
    }

    /// <summary>[이대로 적용](FR-03) — 제안을 저장 대기로 만든다. 제안이 없으면 <c>false</c>.</summary>
    public bool AcceptSuggestions()
    {
        if (!HasSuggestion) return false;
        _chain = _chain.AcceptSuggestions();
        return true;
    }

    /// <summary>바꾸기 <b>전에</b> 부른다 — 이 한 장이 되돌리기의 단위다.</summary>
    public void PushUndo()
    {
        if (_undo.Count >= MAX_UNDO)
        {
            var keep = _undo.Reverse().Skip(1).ToList();     // 가장 오래된 한 장을 버린다
            _undo.Clear();
            foreach (var s in keep) _undo.Push(s);
        }
        _undo.Push(Capture());
    }

    public bool CanUndo => _undo.Count > 0;

    /// <summary>한 단계 되돌린다. 되돌릴 것이 없으면 <c>false</c>.</summary>
    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        Restore(_undo.Pop());
        return true;
    }
    #endregion

    #region - Diff -
    public WiringBoardDiff Diff()
    {
        var created = new List<WiringSensorRow>();
        var factChanged = new List<WiringSensorRow>();
        var wiringChanged = new List<WiringSensorRow>();
        var groupChanged = new List<WiringSensorRow>();

        foreach (var row in _rows)
        {
            if (row.GroupsChanged) groupChanged.Add(row);
            if (row.IsNew) { created.Add(row); continue; }
            if (row.FactsChanged) factChanged.Add(row);
            if (!WiringSpec.SamePlacement(PlacementOf(row.Key), row.BaselinePlacement)) wiringChanged.Add(row);
        }

        return new WiringBoardDiff(created, factChanged, wiringChanged, groupChanged);
    }

    public bool IsDirty => !Diff().IsEmpty;

    public int UnsavedChangeCount
    {
        get
        {
            var diff = Diff();
            return diff.ToSend.Concat(diff.GroupChanged).Distinct().Count();
        }
    }

    /// <summary>
    /// 저장이 끝난 줄을 새 기준으로 삼는다 — 저장한 것이 다시 "바뀐 줄"로 세이지 않게.
    /// </summary>
    /// <remarks>
    /// <b>되돌리기 스택을 버린다</b>(C3) — 저장 전의 장면에는 <b>서버 Id 를 받기 전의 줄 객체</b>(Id=0)가 들어 있어,
    /// 저장 뒤에 되돌리면 이미 만든 센서가 다시 "새 줄"로 되살아나 같은 번호로 한 번 더 POST 된다.
    /// 결선이 모두 서버와 맞으면 옛 배치 변환 알림 · 빈 순번 경고도 걷는다.
    /// </remarks>
    /// <param name="keys">새 기준으로 삼을 줄(<c>null</c> 이면 전부).</param>
    /// <param name="includeGroups">
    /// 그룹도 함께 기준으로 삼을지. 저장 결과를 받을 때는 <c>false</c> 로 부르고 그룹은 <see cref="MarkGroupsSaved"/> 로 옮긴다 —
    /// 그룹은 장비 PATCH 와 <b>따로</b> 나가서, 행 PATCH 가 됐어도 그룹 호출은 실패했을 수 있다.
    /// </param>
    public void MarkBaseline(IEnumerable<int>? keys = null, bool includeGroups = true)
    {
        _undo.Clear();
        var set = keys is null ? null : new HashSet<int>(keys);
        foreach (var row in _rows)
        {
            if (set is not null && !set.Contains(row.Key)) continue;
            row.Baseline = row.Facts;
            row.BaselinePlacement = PlacementOf(row.Key);
            if (includeGroups) row.MarkGroupBaseline();
            row.LoadIssue = null;
        }

        if (Diff().WiringChanged.Count == 0)
        {
            ConvertedFromLegacy = false;
            _loadNotices.Clear();
        }
    }

    /// <summary>
    /// 서버가 맞춰 준 그룹 호출(<paramref name="saved"/>: 그룹 · 방향 · 장비 id)만 그룹 기준선으로 옮긴다.
    /// </summary>
    public void MarkGroupsSaved(IEnumerable<(int GroupId, bool Add, IReadOnlyList<int> DeviceIds)>? saved)
    {
        if (saved is null) return;
        var any = false;
        foreach (var (groupId, add, deviceIds) in saved)
        {
            if (deviceIds is null || deviceIds.Count == 0) continue;
            var ids = new HashSet<int>(deviceIds);
            foreach (var row in _rows.Where(r => r.Id > 0 && ids.Contains(r.Id)))
            {
                row.MarkGroupBaseline(groupId, add);
                any = true;
            }
        }
        if (any) _undo.Clear();
    }

    /// <summary>저장으로 서버 Id 를 받은 새 줄을 기존 줄로 바꿔 단다(키는 유지한다 — 화면이 쥐고 있다).</summary>
    public WiringSensorRow? Promote(int key, int newId)
    {
        var old = Find(key);
        if (old is null || newId <= 0) return null;

        var promoted = new WiringSensorRow(key, newId, old.Channel, old.Facts, PlacementOf(key), null, old.Groups);
        promoted.CopyGroupBaselineFrom(old);
        _rows[_rows.IndexOf(old)] = promoted;
        return promoted;
    }
    #endregion

    #region - Internals -
    /// <summary>새 체인이 실제로 다르면 받고, 사람이 자리를 정한 센서의 불러오기 경고를 푼다(C4).</summary>
    private bool Commit(WiringChain next, IEnumerable<int> touched)
    {
        if (next.SameAs(_chain)) return false;
        _chain = next;
        foreach (var key in touched)
            if (Find(key) is { } row) row.LoadIssue = null;
        return true;
    }

    private sealed record Snapshot(IReadOnlyList<WiringSensorRow> Rows, IReadOnlyList<SensorFacts> Facts, WiringChain Chain);

    private Snapshot Capture() => new(_rows.ToList(), _rows.Select(r => r.Facts).ToList(), _chain);

    private void Restore(Snapshot snapshot)
    {
        _rows.Clear();
        for (var i = 0; i < snapshot.Rows.Count; i++)
        {
            var row = snapshot.Rows[i];
            row.Facts = snapshot.Facts[i];      // 같은 객체를 되살린다 — 화면이 쥔 항목이 끊기지 않는다
            _rows.Add(row);
        }
        _chain = snapshot.Chain;
    }
    #endregion
}
