using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
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
                             IEnumerable<int>? groups = null, WiringFacing? facing = null)
    {
        Key = key;
        Facing = facing ?? placement?.Facing ?? WiringFacing.Front;
        Id = id;
        Channel = channel;
        Facts = facts;
        Baseline = facts;
        BaselinePlacement = placement;
        ServerPlacement = placement;
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

    /// <summary>
    /// 보는 쪽(FR-20 · Draft) — 기둥 센서(스마트 · 복합)만 바꿀 수 있다. 저장값에 없으면 앞. 기준은 <see cref="BaselinePlacement"/> 의 <c>Facing</c>.
    /// </summary>
    public WiringFacing Facing { get; internal set; }

    /// <summary>이 줄이 보는 쪽을 가질 수 있는가(스마트 복합 · 복합).</summary>
    public bool SupportsFacing => WiringTopology.SupportsFacing(WiringTopology.ParseSensorType(Facts.TypeText));

    /// <summary>마지막으로 서버와 맞춘 값.</summary>
    public SensorFacts Baseline { get; internal set; }

    /// <summary>
    /// <b>받아들인</b> 결선 자리 — "바뀐 줄" 을 세는 기준. 불러올 때 겹쳐 팔레트로 뺀 센서는 <c>null</c> 이다(불러오기만으로 더러워지지 않게).
    /// </summary>
    public WiringPlacement? BaselinePlacement { get; internal set; }

    /// <summary>
    /// 서버에 <b>실제로</b> 있는 결선 자리(불러온 원값 · 저장한 값) — 저장 직전 재조회 비교(드리프트)의 기준(F-2b H1).
    /// <see cref="BaselinePlacement"/> 와 따로 둔다 — 겹친 센서는 받아들인 기준이 <c>null</c> 이어도 서버에는 자리가 있어,
    /// 그 둘을 한 값으로 쓰면 사람이 다시 놓은 뒤 저장이 "남이 바꿨다" 로 영원히 막혔다.
    /// </summary>
    public WiringPlacement? ServerPlacement { get; internal set; }

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

/// <summary>불러올 때 보드가 <b>제안</b>한 것의 갈래(F-2b) — 적용 전에는 저장 대상이 아니다.</summary>
public enum WiringProposalKind
{
    /// <summary>저장된 자리가 없어 번호순으로 붙인 센서(FR-03).</summary>
    Suggested = 0,
    /// <summary>옛 두 선(N04)의 2차 선 센서를 한 줄로 옮긴 것(FR-02).</summary>
    Converted = 1,
    /// <summary>불러온 순번의 빈 자리를 당겨 붙여 순번이 바뀐 센서(FR-14 ②).</summary>
    Compacted = 2,
    /// <summary>옛 양쪽 가지 저장값을 링 한 줄로 이어 붙인 것(v0.4 · 가지 폐기).</summary>
    JoinedBranches = 3,
}

/// <summary>
/// 결선 보드 — 제어기 한 대의 센서 표와 <b>결선 체인</b>(<see cref="WiringChain"/>). <b>판정은 전부 여기</b>(UI 없음 · 헤드리스 테스트 대상).
/// </summary>
/// <remarks>
/// <para><b>체인 모델(wiring-fence-view F-2)</b> — 결선 모양은 제어기 종류가 정하고(<see cref="WiringTopology"/>) 순서는 체인 하나(양쪽 가지면 가지 둘)다.
/// 빈 칸이 없다 — 끼워 넣으면 뒤가 밀리고, 빼면 뒤가 당겨진다.</para>
/// <para><b>선 번호</b> — 목록 보기의 선은 링 · 한 줄이면 <b>하나</b>(선 1 = 체인 전체, Sensor A · 제어기 쪽부터), 양쪽 가지면 <b>둘</b>
/// (선 1 = 왼쪽 가지 · 선 2 = 오른쪽 가지, 각각 제어기 쪽부터). 목록 자리 i 의 순번은 늘 i+1 이다.</para>
/// <para><b>불러오기 제안(F-2b)</b> — 번호순 제안 · 옛 두 선 변환 · 빈 자리 당겨 붙임은 모두 <b>제안</b>이다. 화면에는 보이되 바뀐 줄이 아니고
/// 저장에 실리지 않는다(<see cref="PlacementOf"/> 가 받아들인 자리를 돌려준다). [이대로 적용](<see cref="AcceptProposals"/>)하거나
/// <b>체인을 처음 고치는 순간</b>(끌기 · 한 칸 이동 · 붙이기 · 빼기 · 번호순 배치) 전부 함께 적용된다 — 적용하지 않은 제안 위에서 번호를 매기면
/// 사람이 고친 자리와 제안 자리가 서로 어긋난다(H2). 그 적용은 고친 동작과 <b>같은 되돌리기 한 걸음</b>이다.</para>
/// <para>되돌리기는 <b>한 스택</b>이다 — 표 값 · 체인 · 제안 상태 · 불러오기 경고를 같이 찍는다(WS L181 · L1).</para>
/// </remarks>
public sealed class WiringBoard
{
    /// <summary>되돌리기 깊이 — 한 세션에서 이보다 더 거슬러 가지 않는다.</summary>
    public const int MAX_UNDO = 100;

    private readonly List<WiringSensorRow> _rows = new();
    private readonly Stack<Snapshot> _undo = new();
    private readonly List<WiringChainLoadIssue> _loadNotices = new();
    private readonly Dictionary<int, WiringProposalKind> _proposals = new();
    private WiringChain _chain;
    private WiringFenceLayout _fence = WiringFenceLayout.None;
    private string? _controllerType;
    private bool _pending;
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

    /// <summary>
    /// 저장된 결선 모양(<c>"v": 2</c> 표지) — 있으면 제어기 종류 추정보다 이긴다(H3). 옛 값뿐이면 <c>null</c>.
    /// </summary>
    public WiringShape? StoredShape { get; private set; }

    /// <summary>저장된 모양과 제어기 종류로 추정한 모양이 다를 때의 알림 — 저장된 모양으로 연다.</summary>
    public string? ShapeNotice { get; private set; }

    /// <summary>지금 체인(불변 값).</summary>
    public WiringChain Chain => _chain;

    /// <summary>목록 보기의 선 수 — 양쪽 가지만 2.</summary>
    public int LineCount => Shape == WiringShape.TwoBranch ? 2 : 1;

    /// <summary>불러올 때 N04 의 2차 선을 한 줄로 이어 붙였는가(FR-02) — 저장 전까지 알린다.</summary>
    public bool ConvertedFromLegacy { get; private set; }

    /// <summary>불러오기에서 당겨 붙인 빈 순번 같은 경고(FR-14 ②).</summary>
    public IReadOnlyList<WiringChainLoadIssue> LoadNotices => _loadNotices;

    /// <summary>적용하지 않은 불러오기 제안이 있는가(번호순 제안 · 옛 배치 변환 · 빈 자리 당김).</summary>
    public bool HasPendingProposals => _pending;

    /// <summary>
    /// 불러오기 제안의 갈래(키 → 갈래) — 적용한 뒤에도 저장 전까지 남아 미리보기가 "무엇이 자동으로 바뀌었나" 를 따로 말한다(M1).
    /// </summary>
    public IReadOnlyDictionary<int, WiringProposalKind> Proposals => _proposals;

    /// <summary>한 갈래의 제안 수.</summary>
    public int ProposalCount(WiringProposalKind kind) => _proposals.Values.Count(k => k == kind);

    /// <summary>체인을 고치다가 제안을 함께 적용했는가 — 알림 "제안 · 변환 배치를 함께 적용했습니다".</summary>
    public bool ProposalsAppliedByEdit { get; private set; }

    /// <summary>번호순 제안으로 붙어 있는 센서 수(FR-03) — 적용 전에만 센다.</summary>
    public int SuggestedCount => _pending ? _chain.Suggested.Count : 0;

    /// <summary>번호순 제안이 걸려 있는가.</summary>
    public bool HasSuggestion => SuggestedCount > 0;

    /// <summary>그 센서가 적용 전 번호순 제안인가(모서리 표지).</summary>
    public bool IsSuggested(int key) => _pending && _chain.Suggested.Contains(key);

    /// <summary>그 센서의 자리가 적용 전 제안(번호순 · 변환 · 당김)인가.</summary>
    public bool IsProposed(int key) => _pending && _proposals.ContainsKey(key);

    /// <summary>
    /// 이 제어기의 제품군(표의 센서 종류 전부) — 스마트만 · 펜스 계열만 · 섞임. 섞어 쓰기는 정상(v0.4 · 옛 섞임 경고 O-8 폐기) —
    /// 한도 표의 행과 "번호순이 실제 순서와 다를 수 있다"(O-12) 안내를 고른다.
    /// </summary>
    public WiringFamily Family => WiringLimitTable.FamilyOf(_rows.Select(r => WiringTopology.ParseSensorType(r.Facts.TypeText)));

    /// <summary>제품군이 섞였는가(스마트 1번~ · 펜스 101번~ 같은 현장).</summary>
    public bool IsMixedFamily => Family == WiringFamily.Mixed;

    /// <summary>
    /// 간격 표(v0.4 §1-C) — 펜스센서 현장 간격(2~4m)을 제어기마다 바꿀 수 있다. 보드 상태로만 쥐고 서버에 싣지 않는다(O-10) ·
    /// 바꾸면 그림만 다시 놓이고 바뀐 줄 · 되돌리기와 무관하다.
    /// </summary>
    public WiringSpacingTable Spacing { get; private set; } = WiringSpacingTable.Default;

    /// <summary>펜스센서 현장 간격을 바꾼다(2~4m · 0.5m 단위로 맞춘다). 바뀌었으면 <c>true</c>.</summary>
    public bool SetFenceSpacing(double metres)
    {
        var next = Spacing.WithFence(metres);
        if (next == Spacing) return false;
        Spacing = next;
        return true;
    }

    /// <summary>한도 표(경고만).</summary>
    public WiringLimitTable Limits { get; } = WiringLimitTable.Default;

    /// <summary>체인 길이(m) — 첫 센서에서 끝 센서까지, 이웃 간격(간격 표 · 두 종류 중 작은 값)의 합.</summary>
    public double ChainLengthMetres
    {
        get
        {
            var types = _chain.Keys.Select(k => WiringTopology.ParseSensorType(Find(k)?.Facts.TypeText)).ToList();
            var sum = 0.0;
            for (var i = 1; i < types.Count; i++) sum += Spacing.GapBetween(types[i - 1], types[i]);
            return sum;
        }
    }

    /// <summary>체인의 센서 종류(체인 순서).</summary>
    public IReadOnlyList<EnumDeviceType> ChainTypes => _chain.Keys.Select(k => WiringTopology.ParseSensorType(Find(k)?.Facts.TypeText)).ToList();

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

    /// <summary>체인 위 번호(자리 · 선 · 순번 · 링이면 B 번호) — <b>보이는</b> 번호(제안 포함).</summary>
    public WiringChainNumber? NumberOf(int key) => _chain.NumberOf(key);

    /// <summary>
    /// <b>저장될</b> 결선 자리. 적용하지 않은 제안이 있으면 <b>받아들인 자리</b>(<see cref="WiringSensorRow.BaselinePlacement"/>) 그대로 —
    /// 이름만 고쳐 저장해도 제안 자리가 실려 나가지 않는다(M1). 그 밖에는 체인의 자리, 팔레트면 <c>null</c>.
    /// </summary>
    /// <remarks>보는 쪽(FR-20)은 늘 지금 값이다 — 제안이 걸려 있어도 방향만 바꾼 줄은 받아들인 자리 그대로 방향만 실린다.</remarks>
    public WiringPlacement? PlacementOf(int key)
    {
        if (Find(key) is not { } row) return null;
        var at = _pending ? row.BaselinePlacement : DisplayPlacementOf(key);
        return at is null ? null : at with { Facing = row.Facing };
    }

    /// <summary>그 센서가 보는 쪽(모르는 키는 앞).</summary>
    public WiringFacing FacingOf(int key) => Find(key)?.Facing ?? WiringFacing.Front;

    /// <summary>그 센서가 보는 쪽을 가질 수 있는가(FR-20).</summary>
    public bool SupportsFacing(int key) => Find(key)?.SupportsFacing == true;

    /// <summary>
    /// 보는 쪽이 저장 기준과 다른가(미리보기 "방향 바뀜") — 저장된 자리가 있고 지금도 자리가 있는 줄만. 새로 붙인 줄은 "자리"로 센다.
    /// </summary>
    public bool FacingChanged(int key)
        => Find(key) is { BaselinePlacement: { } before } && PlacementOf(key) is { } now && now.Facing != before.Facing;

    /// <summary>화면에 보일 자리 — 제안도 자리를 보인다.</summary>
    public WiringPlacement? DisplayPlacementOf(int key)
        => _chain.NumberOf(key) is { } n ? new WiringPlacement(n.Line, n.Order) : null;

    /// <summary>그 선에 붙은 센서를 목록 순서로.</summary>
    public IReadOnlyList<WiringSensorRow> Placed(int line)
        => Line(line).Select(Find).Where(r => r is not null).Select(r => r!).ToList();
    #endregion

    #region - Load -
    /// <summary>
    /// 서버에서 받은 센서로 보드를 채운다(FR-01 ~ FR-03 · FR-16 · F-2b).
    /// </summary>
    /// <param name="controllerType">제어기 <c>type_controller</c> 원값 — 결선 모양을 정한다. 모르면 센서 종류로 추정한다.</param>
    /// <param name="savedShapes">센서 id → 저장된 결선 모양(<c>"v": 2</c> 표지). 있으면 추정보다 이긴다.</param>
    /// <remarks>
    /// 옛 두 선 변환 · 번호순 제안 · 빈 자리 당김은 <b>제안</b>으로 남긴다(적용 전 저장 대상 아님).
    /// 같은 자리를 둘이 주장하면 뒤의 센서는 팔레트로 + 까닭(치명), 저장된 결선을 읽지 못한 센서도 팔레트로 둔다.
    /// </remarks>
    public void Load(IEnumerable<(int Id, int? Channel, SensorFacts Facts, WiringPlacement? Placement, string? Issue, IReadOnlyList<int>? Groups)> sensors,
                     string? controllerType = null, IReadOnlyDictionary<int, WiringShape>? savedShapes = null)
    {
        _rows.Clear();
        _undo.Clear();
        _loadNotices.Clear();
        _proposals.Clear();
        _controllerType = controllerType;
        ProposalsAppliedByEdit = false;
        _fence = WiringFenceLayout.None;
        FenceBaseline = WiringFenceLayout.None;

        var loaded = sensors?.ToList() ?? new();
        foreach (var s in loaded)
            _rows.Add(new WiringSensorRow(s.Id > 0 ? s.Id : _nextNewKey--, s.Id, s.Channel, s.Facts, s.Placement, s.Issue, s.Groups));

        // 모든 제어기는 링(v0.4 §1-C). 저장된 모양은 옛 값을 어떻게 읽을지만 정한다 — 가지로 저장된 값은 링으로 이어 붙이는 제안이 된다.
        Topology = WiringTopology.For(controllerType, _rows.Select(r => WiringTopology.ParseSensorType(r.Facts.TypeText)).ToList());
        StoredShape = StoredShapeOf(savedShapes);
        ShapeNotice = null;

        var chainSensors = _rows.Select(r => new WiringChainSensor(r.Key, r.Id, r.Facts.Number, r.BaselinePlacement)).ToList();
        var leftCount = 0;
        JoinedFromBranches = StoredShape == WiringShape.TwoBranch && chainSensors.Any(s => s.Placement is not null);
        if (JoinedFromBranches) chainSensors = JoinBranches(chainSensors, out leftCount).ToList();

        var result = WiringChain.Load(Shape, chainSensors);
        var chain = JoinedFromBranches ? result.Chain.WithControllerGap(leftCount) : result.Chain;

        foreach (var issue in result.Issues)
        {
            if (issue.Key is { } key && Find(key) is { } row && issue.Level == WiringIssueLevel.Critical)
            {
                row.LoadIssue = issue.Message;
                row.BaselinePlacement = null;      // 받아들인 기준은 "미배치" — 서버 원값(ServerPlacement)은 그대로 둔다(H1)
            }
            else _loadNotices.Add(issue);
        }

        // 저장된 결선을 읽지 못한 센서는 제안하지 않는다 — 사람이 다시 배치해야 한다(C4).
        var unreadable = _rows.Where(r => !string.IsNullOrEmpty(r.LoadIssue) && chain.Suggested.Contains(r.Key)).Select(r => r.Key).ToList();
        if (unreadable.Count > 0) chain = chain.RemoveMany(unreadable);

        _chain = chain;
        ConvertedFromLegacy = result.ConvertedFromLegacy;

        // 제안 갈래 — 보이는 자리가 받아들인 자리와 다른 센서(번호순 제안 · 옛 2차 변환 · 빈 자리 당김).
        foreach (var row in _rows.Where(r => _chain.Contains(r.Key)))
        {
            if (_chain.Suggested.Contains(row.Key)) { _proposals[row.Key] = WiringProposalKind.Suggested; continue; }
            if (WiringSpec.SamePlacement(DisplayPlacementOf(row.Key), row.BaselinePlacement)) continue;
            _proposals[row.Key] = JoinedFromBranches ? WiringProposalKind.JoinedBranches
                : ConvertedFromLegacy && row.BaselinePlacement?.Line == WiringSpec.LINE_SECONDARY ? WiringProposalKind.Converted
                : WiringProposalKind.Compacted;
        }
        _pending = _proposals.Count > 0;
    }

    /// <summary>
    /// 옛 양쪽 가지 저장값 → 링 한 줄(v0.4 · 가지 폐기). <b>펜스를 따라 왼쪽 → 오른쪽 물리 순서를 지킨다</b>:
    /// 왼쪽 가지는 제어기에서 바깥으로 L1, L2 … 였으므로 <b>뒤집어</b>(바깥 L n → 제어기 옆 L1) 앞에 두고,
    /// 이어서 오른쪽 가지를 그대로(제어기 옆 R1 → 바깥 R n) 붙인다. 함체(제어기) 틈은 L1 과 R1 사이 = 왼쪽 가지 수.
    /// 같은 가지 안 순번이 겹치면 id 순. 자리 없는 센서는 그대로(번호순 제안 대상).
    /// </summary>
    internal static IEnumerable<WiringChainSensor> JoinBranches(IReadOnlyList<WiringChainSensor> sensors, out int leftCount)
    {
        var left = sensors.Where(s => s.Placement?.Line == WiringSpec.LINE_PRIMARY)
                          .OrderByDescending(s => s.Placement!.Order).ThenBy(s => s.Id).ToList();
        var right = sensors.Where(s => s.Placement?.Line == WiringSpec.LINE_SECONDARY)
                           .OrderBy(s => s.Placement!.Order).ThenBy(s => s.Id).ToList();
        leftCount = left.Count;
        var joined = left.Concat(right)
                         .Select((s, i) => s with { Placement = new WiringPlacement(WiringSpec.LINE_PRIMARY, i + 1, s.Placement!.Facing) })
                         .ToList();
        return joined.Concat(sensors.Where(s => s.Placement is null));
    }

    /// <summary>옛 가지 배치를 링으로 이어 붙였고 아직 저장하지 않았다.</summary>
    public bool JoinedFromBranches { get; private set; }

    public const string JOINED_NOTICE = "옛 가지 배치를 링으로 이어 붙였습니다 — 확인 후 저장";

    /// <summary>새 줄을 더한다(센서 여러 개 만들기 · 엑셀 붙여넣기). 팔레트에 선다 — 서버에는 저장 때 만든다.</summary>
    public WiringSensorRow AddRow(SensorFacts facts, IEnumerable<int>? groups = null)
    {
        var row = new WiringSensorRow(_nextNewKey--, 0, null, facts, null, null, groups);
        _rows.Add(row);
        return row;
    }

    private static WiringShape? StoredShapeOf(IReadOnlyDictionary<int, WiringShape>? savedShapes)
        => savedShapes is null || savedShapes.Count == 0
            ? null
            : savedShapes.Values.GroupBy(s => s).OrderByDescending(g => g.Count()).ThenBy(g => (int)g.Key).First().Key;

    /// <summary>사람이 읽는 모양 이름.</summary>
    public static string ShapeName(WiringShape shape) => shape switch
    {
        WiringShape.Ring => "링",
        WiringShape.TwoBranch => "가지",
        _ => "한 줄",
    };
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
        return Edit(chain => chain.PlaceInBranch(known, line, index), known) ? known.Count : 0;
    }

    /// <summary>체인 끝에 붙인다(팔레트 Enter · FR-11) — 양쪽 가지면 오른쪽 가지 바깥 끝. 붙인 수.</summary>
    public int Append(IEnumerable<int> keys)
    {
        var known = (keys ?? Enumerable.Empty<int>()).Where(k => Find(k) is not null && !_chain.Contains(k)).Distinct().ToList();
        if (known.Count == 0) return 0;
        return Edit(chain =>
        {
            foreach (var key in known) chain = chain.Append(key);
            return chain;
        }, known) ? known.Count : 0;
    }

    /// <summary>체인에서 뺀다 — 뒤는 한 칸씩 당겨진다.</summary>
    public bool Unplace(int key) => UnplaceMany(new[] { key }) > 0;

    /// <summary>여럿을 한꺼번에 뺀다. 뺀 수.</summary>
    public int UnplaceMany(IEnumerable<int> keys)
    {
        var inChain = (keys ?? Enumerable.Empty<int>()).Where(_chain.Contains).Distinct().ToList();
        if (inChain.Count == 0) return 0;
        return Edit(chain => chain.RemoveMany(inChain), inChain) ? inChain.Count : 0;
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
    /// 함체(제어기) 틈을 옮긴다(FR-09) — 링에서는 <b>표시용</b>이라 번호 · 저장과 무관하고 제안도 적용하지 않는다.
    /// </summary>
    public bool MoveControllerGap(int gap)
    {
        var next = _chain.WithControllerGap(gap);
        if (next.SameAs(_chain)) return false;
        _chain = next;
        return true;
    }

    /// <summary>
    /// 번호 순으로 배치(FR-03) — 링 · 한 줄은 <b>전체</b>(팔레트 포함)를 장비번호 → id 순으로 한 줄에,
    /// 양쪽 가지는 <b>가지 안에서만</b> 줄 세운다(팔레트는 그대로). 걸려 있던 제안은 함께 적용된다.
    /// 센서가 없으면 <c>false</c>.
    /// </summary>
    public bool AutoLayoutByNumber()
    {
        if (_rows.Count == 0) return false;
        var sensors = _rows.Select(r => new WiringChainSensor(r.Key, r.Id, r.Facts.Number, null));
        var palette = Shape == WiringShape.TwoBranch ? new List<int>() : Unplaced.Select(r => r.Key).ToList();

        // 팔레트는 보드가 쥔다(불러온 뒤 더한 줄은 체인이 모른다) — 링 · 한 줄은 먼저 체인 끝에 붙이고 전체를 줄 세운다.
        Edit(chain =>
        {
            foreach (var key in palette) chain = chain.Append(key);
            return chain.SortByDefaultOrder(sensors, includeUnplaced: false);
        }, _rows.Where(r => _chain.Contains(r.Key) || palette.Contains(r.Key)).Select(r => r.Key).ToList(), force: true, keepSuggestions: true);
        return true;
    }

    /// <summary>
    /// [이대로 적용](FR-03) — 걸려 있는 불러오기 제안(번호순 · 변환 · 당김)을 전부 받아들여 저장 대기로 만든다. 없으면 <c>false</c>.
    /// </summary>
    public bool AcceptProposals()
    {
        if (!_pending) return false;
        _chain = _chain.AcceptSuggestions();
        _pending = false;
        return true;
    }

    /// <summary>
    /// 보는 쪽을 정한다(FR-20) — 방향이 있는 줄(스마트 복합 · 복합)만. 바뀐 줄 수를 돌려준다(0 이면 아무것도 안 바뀜).
    /// 체인 편집이 아니다 — 자리 · 제안은 그대로 두고, 되돌리기 한 걸음은 부르는 쪽이 <see cref="PushUndo"/> 로 찍는다.
    /// </summary>
    public int SetFacing(IEnumerable<int> keys, WiringFacing facing)
    {
        var changed = 0;
        foreach (var key in (keys ?? Enumerable.Empty<int>()).Distinct())
        {
            if (Find(key) is not { SupportsFacing: true } row || row.Facing == facing) continue;
            row.Facing = facing;
            changed++;
        }
        return changed;
    }

    /// <summary><see cref="AcceptProposals"/> 의 옛 이름 — 번호순 제안만이 아니라 모든 불러오기 제안을 적용한다.</summary>
    public bool AcceptSuggestions() => AcceptProposals();

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
        _renumbered = 0;            // 새 동작의 시작 — 그 동작이 다시 매긴 번호만 센다(상태 줄 "번호 n대 바뀜")
    }

    public bool CanUndo => _undo.Count > 0;

    /// <summary>한 단계 되돌린다. 되돌릴 것이 없으면 <c>false</c>.</summary>
    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        Restore(_undo.Pop());
        _renumbered = 0;
        return true;
    }
    #endregion

    #region - Fence layout (fence-wiring-editor FR-01 · FR-07 · FR-09 · FR-10) -
    /// <summary>
    /// 펜스 구성(망 · 자리 · 번호 대역) — 켜져 있으면 체인과 <b>늘 맞물린다</b>: 자리 순서(A 쪽 끝부터, 같은 망이면 기둥 위 → 망 가운데)대로
    /// 줄 세운 것이 체인이다. 체인을 고치는 모든 길(<see cref="Edit"/>)이 자리를 맞추고, 자리를 고치는 길(<see cref="ApplyFenceEdit"/>)이 체인을 맞춘다.
    /// </summary>
    public WiringFenceLayout FenceLayout => _fence;

    /// <summary>마지막으로 로컬에 저장한(불러온) 구성 — "로컬 저장할 것이 있나"의 기준.</summary>
    public WiringFenceLayout FenceBaseline { get; private set; } = WiringFenceLayout.None;

    /// <summary>
    /// 펜스 구성이 기준과 다른가(제안만 걸려 있고 손대지 않았으면 거짓). 펜스센서 현장 간격(<see cref="Spacing"/>)도 로컬 문서에 실리므로
    /// 간격만 바꿔도 바뀐 것이다.
    /// </summary>
    public bool IsFenceDirty => _fence.IsActive && (!_fence.SameContent(FenceBaseline) || !SameMetres(Spacing.FenceMetres, _fenceSpacingBaseline));

    /// <summary>마지막으로 로컬에 저장한(불러온) 펜스센서 간격(m).</summary>
    private double _fenceSpacingBaseline = WiringSpacingTable.Default.FenceMetres;

    private static bool SameMetres(double a, double b) => Math.Abs(a - b) < 1e-9;

    /// <summary>마지막 동작(<see cref="PushUndo"/> 이후)이 다시 매긴 번호 수.</summary>
    private int _renumbered;

    /// <summary>
    /// 마지막 동작이 위치 순서대로 다시 매긴 번호 수를 꺼내고 비운다 — 창이 상태 줄에 "번호 n대 바뀜" 을 붙인다(Draft 로 번호가 바뀌면 늘 알린다).
    /// </summary>
    public int TakeRenumbered()
    {
        var n = _renumbered;
        _renumbered = 0;
        return n;
    }

    /// <summary>
    /// 펜스 구성을 싣는다(불러오기 · 제안). 되돌리기 장면을 쌓지 않고 번호도 매기지 않는다 — 불러오기만으로 바뀐 줄이 생기지 않게.
    /// <paramref name="baseline"/> 이 없으면 실은 구성이 곧 기준이다.
    /// </summary>
    public void LoadFenceLayout(WiringFenceLayout layout, WiringFenceLayout? baseline = null)
    {
        _fence = layout ?? WiringFenceLayout.None;
        FenceBaseline = baseline ?? _fence;
        _fenceSpacingBaseline = Spacing.FenceMetres;
    }

    /// <summary>로컬 저장이 끝났다 — 지금 구성을 새 기준으로(제안 표지도 걷는다).</summary>
    public void MarkFenceSaved()
    {
        if (!_fence.IsActive) return;
        _fence = _fence.Accepted();
        FenceBaseline = _fence;
        _fenceSpacingBaseline = Spacing.FenceMetres;
    }

    /// <summary>
    /// 펜스 구성을 고친다(망 속성 · 센서 자리) — 고친 자리대로 체인을 다시 세우고(순서가 바뀌면 체인 편집 한 번 · 그때만 번호 대역대로 다시 매긴다).
    /// 망 색 · 높이 · 거리처럼 순서가 그대로인 편집은 번호를 건드리지 않는다(표에서 손으로 고친 번호가 되돌아가지 않게).
    /// 되돌리기 한 걸음은 부르는 쪽이 <see cref="PushUndo"/> 로 찍는다. 바뀐 것이 없으면 <c>false</c>.
    /// </summary>
    /// <remarks>자리가 있는데 체인에 없던 센서(팔레트)는 체인에 들어오고, 자리를 뺀 센서는 팔레트로 간다.</remarks>
    /// <param name="tieOrder">같은 자리 센서끼리의 차례 — 없으면 지금 체인 순서(끌어 옮긴 센서를 이미 있는 센서 앞 · 뒤에 둘 때 준다).</param>
    public bool ApplyFenceEdit(Func<WiringFenceLayout, WiringFenceLayout> edit, IReadOnlyList<int>? tieOrder = null)
    {
        ArgumentNullException.ThrowIfNull(edit);
        if (!_fence.IsActive) return false;

        var before = _fence;
        var next = edit(before);
        if (next is null || !next.IsActive) return false;
        var mounts = next.Mounts.Where(p => Find(p.Key) is not null).Select(p => (p.Key, p.Value)).ToList();
        var order = FenceLayoutMath.PositionOrder(mounts, tieOrder ?? _chain.Keys);
        var chainChanged = !order.SequenceEqual(_chain.Keys);
        var layoutChanged = !next.SameContent(before);
        if (!chainChanged && !layoutChanged) return false;

        _fence = next;
        if (chainChanged)
        {
            var placed = new HashSet<int>(order);
            Edit(chain =>
            {
                var palette = chain.Unplaced.Concat(chain.Keys).Where(k => !placed.Contains(k)).ToList();
                return WiringChain.Create(chain.Shape, order, palette, chain.IsControllerGapExplicit ? chain.ControllerGap : null);
            }, order, force: true);
        }
        return true;
    }

    /// <summary>번호 대역을 정한다(<c>null</c> = 자동 번호 끔) — 정하면 바로 위치 순서대로 다시 매긴다(Draft). 바뀐 것이 있으면 <c>true</c>.</summary>
    public bool SetNumberBands(NumberBandSet? bands)
    {
        if (!_fence.IsActive) return false;
        var numbers = _rows.Select(r => r.Facts.Number).ToList();
        var changed = !Equals(_fence.Bands, bands);
        _fence = _fence.WithBands(bands);
        if (changed) Renumber();        // 대역이 바뀌었을 때만 — 같은 대역을 다시 골라도 손으로 고친 번호를 덮지 않는다
        return changed || !numbers.SequenceEqual(_rows.Select(r => r.Facts.Number));
    }

    /// <summary>그 센서의 번호 갈래(종류 → 대역 갈래).</summary>
    public FenceSensorCategory CategoryOf(int key) => NumberingMath.CategoryOf(WiringTopology.ParseSensorType(Find(key)?.Facts.TypeText));

    /// <summary>번호 검증의 입력 — 모든 줄(체인 여부 · 지금 번호).</summary>
    public IReadOnlyList<NumberingSensor> NumberingSensors()
        => _rows.Select(r => new NumberingSensor(r.Key, CategoryOf(r.Key), r.Facts.Number, _chain.Contains(r.Key), r.Display)).ToList();

    /// <summary>번호 검증(대역이 없으면 빈 목록).</summary>
    public IReadOnlyList<NumberingIssue> NumberingIssues()
        => _fence.IsActive && _fence.Bands is { } bands ? NumberingMath.Validate(NumberingSensors(), bands) : Array.Empty<NumberingIssue>();

    /// <summary>저장 전 "바뀌는 번호 표" — 서버에 있는 센서 중 번호가 기준과 다른 것, 체인 순서 → 팔레트 순서.</summary>
    public IReadOnlyList<NumberChange> NumberChanges()
    {
        var order = _chain.Keys.Concat(Unplaced.Select(r => r.Key)).Distinct();
        return NumberingMath.Changes(order.Select(Find).Where(r => r is { IsNew: false }).Select(r => (r!.Key, r.Display, r.Baseline.Number, r.Facts.Number)));
    }

    /// <summary>
    /// 대역이 있으면 체인 순서대로 번호를 다시 매긴다 — 바뀐 줄의 <see cref="WiringSensorRow.Facts"/> 번호만 고친다(Draft · 되돌리기 장면에 든다).
    /// 부르는 곳은 셋뿐이다: 체인 순서가 바뀔 때 · 체인 구성원이 바뀔 때(<see cref="Edit"/>) · 대역이 바뀔 때(<see cref="SetNumberBands"/>).
    /// </summary>
    private int Renumber()
    {
        if (!_fence.IsActive || _fence.Bands is not { } bands) return 0;
        var numbers = NumberingMath.Assign(_chain.Keys.Select(k => (k, CategoryOf(k))), bands);
        var changed = 0;
        foreach (var (key, number) in numbers)
        {
            if (Find(key) is not { } row || row.Facts.Number == number) continue;
            row.Facts = row.Facts with { Number = number };
            changed++;
        }
        _renumbered += changed;
        return changed;
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
            if (!WiringSpec.SameWiring(PlacementOf(row.Key), row.BaselinePlacement)) wiringChanged.Add(row);
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
    /// 저장이 끝난 줄을 새 기준으로 삼는다 — 저장한 것이 다시 "바뀐 줄"로 세이지 않게. 서버 원값(<see cref="WiringSensorRow.ServerPlacement"/>)도
    /// 보낸 값으로 옮긴다(다음 저장의 드리프트 기준).
    /// </summary>
    /// <remarks>
    /// <b>되돌리기 스택을 버린다</b>(C3) — 저장 전의 장면에는 <b>서버 Id 를 받기 전의 줄 객체</b>(Id=0)가 들어 있어,
    /// 저장 뒤에 되돌리면 이미 만든 센서가 다시 "새 줄"로 되살아나 같은 번호로 한 번 더 POST 된다.
    /// 결선이 모두 서버와 맞으면(그리고 걸린 제안이 없으면) 옛 배치 변환 알림 · 빈 순번 경고 · 제안 갈래도 걷는다.
    /// </remarks>
    /// <param name="keys">새 기준으로 삼을 줄(<c>null</c> 이면 전부).</param>
    /// <param name="includeGroups">
    /// 그룹도 함께 기준으로 삼을지. 저장 결과를 받을 때는 <c>false</c> 로 부르고 그룹은 <see cref="MarkGroupsSaved"/> 로 옮긴다.
    /// </param>
    public void MarkBaseline(IEnumerable<int>? keys = null, bool includeGroups = true)
    {
        _undo.Clear();
        var set = keys is null ? null : new HashSet<int>(keys);
        foreach (var row in _rows)
        {
            if (set is not null && !set.Contains(row.Key)) continue;
            var placement = PlacementOf(row.Key);
            if (!WiringSpec.SameWiring(placement, row.BaselinePlacement)) row.ServerPlacement = placement;
            row.Baseline = row.Facts;
            row.BaselinePlacement = placement;
            if (includeGroups) row.MarkGroupBaseline();
            row.LoadIssue = null;
        }

        if (!_pending && Diff().WiringChanged.Count == 0)
        {
            ConvertedFromLegacy = false;
            JoinedFromBranches = false;
            ProposalsAppliedByEdit = false;
            _loadNotices.Clear();
            _proposals.Clear();
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

        var promoted = new WiringSensorRow(key, newId, old.Channel, old.Facts, PlacementOf(key), null, old.Groups, old.Facing);
        promoted.CopyGroupBaselineFrom(old);
        _rows[_rows.IndexOf(old)] = promoted;
        return promoted;
    }
    #endregion

    #region - Internals -
    /// <summary>
    /// 체인 편집 하나 — 걸린 제안이 있으면 <b>먼저 전부 적용한 체인</b> 위에서 고친다(H2). 바뀐 것이 없으면 아무것도 적용하지 않는다.
    /// 사람이 자리를 정한 센서의 불러오기 경고는 여기서 풀린다(C4).
    /// </summary>
    /// <param name="force">바뀐 것이 없어도 적용(번호순 배치 — 사람이 누른 것).</param>
    /// <param name="keepSuggestions">
    /// 번호순 제안도 받아들인 채 고치는가. 제품군이 섞인 제어기(O-12)에서는 번호 대역이 종류별로 갈려 번호순이 실제 순서가 아닐 수 있어
    /// <b>제안을 저절로 적용하지 않는다</b> — 체인을 고치면 제안 센서는 미배치로 돌아간다. [번호 순으로 자동 배치]처럼 사람이 번호순을 고른 동작만 참.
    /// </param>
    private bool Edit(Func<WiringChain, WiringChain> edit, IEnumerable<int> touched, bool force = false, bool keepSuggestions = false)
    {
        var withdrawn = _pending && IsMixedFamily && !keepSuggestions ? _chain.Suggested.ToList() : new List<int>();
        var basis = !_pending ? _chain
            : withdrawn.Count > 0 ? _chain.RemoveMany(withdrawn).AcceptSuggestions()
            : _chain.AcceptSuggestions();
        var next = edit(basis);
        if (!force && next.SameAs(basis)) return false;            // 제자리 — 제안도 그대로 둔다
        foreach (var key in withdrawn) _proposals.Remove(key);     // 섞인 제어기: 번호순 제안은 미배치로 돌아갔다(O-12)

        if (_pending)
        {
            _pending = false;
            ProposalsAppliedByEdit = true;
        }
        _chain = next.AcceptSuggestions();
        foreach (var key in touched)
            if (Find(key) is { } row) row.LoadIssue = null;

        // 펜스 구성이 켜져 있으면 자리를 새 체인에 맞추고(남은 센서는 자리 묶음을 나눠 갖고 · 새 센서는 이웃 사이 · 빠진 센서는 자리를 비운다)
        // 번호 대역이 있으면 번호를 다시 매긴다 — 이 편집과 <b>같은 되돌리기 한 걸음</b>이다(FR-09 · FR-12).
        if (_fence.IsActive)
        {
            var (mounts, panels) = FenceLayoutMath.Reconcile(basis.Keys, _chain.Keys, _fence.Mounts, _fence.Panels, CategoryOf);
            _fence = _fence.With(panels, mounts);
            // 번호는 체인 순서 · 구성원이 실제로 바뀌었을 때만 다시 매긴다(사람이 누른 같은 배치 — force — 로는 번호를 덮지 않는다).
            if (!basis.Keys.SequenceEqual(_chain.Keys)) Renumber();
        }
        return true;
    }

    private sealed record Snapshot(
        IReadOnlyList<WiringSensorRow> Rows,
        IReadOnlyList<SensorFacts> Facts,
        IReadOnlyList<string?> LoadIssues,
        IReadOnlyList<WiringFacing> Facings,
        WiringChain Chain,
        bool Pending,
        bool AppliedByEdit,
        IReadOnlyDictionary<int, WiringProposalKind> Proposals,
        WiringFenceLayout Fence);

    private Snapshot Capture()
        => new(_rows.ToList(), _rows.Select(r => r.Facts).ToList(), _rows.Select(r => r.LoadIssue).ToList(), _rows.Select(r => r.Facing).ToList(),
               _chain, _pending, ProposalsAppliedByEdit, new Dictionary<int, WiringProposalKind>(_proposals), _fence);

    private void Restore(Snapshot snapshot)
    {
        _rows.Clear();
        for (var i = 0; i < snapshot.Rows.Count; i++)
        {
            var row = snapshot.Rows[i];
            row.Facts = snapshot.Facts[i];      // 같은 객체를 되살린다 — 화면이 쥔 항목이 끊기지 않는다
            row.LoadIssue = snapshot.LoadIssues[i];
            row.Facing = snapshot.Facings[i];
            _rows.Add(row);
        }
        _chain = snapshot.Chain;
        _fence = snapshot.Fence;             // 불변 값 — 되돌리면 그 장면의 망 · 자리 · 대역 그대로
        _pending = snapshot.Pending;
        ProposalsAppliedByEdit = snapshot.AppliedByEdit;
        _proposals.Clear();
        foreach (var (key, kind) in snapshot.Proposals) _proposals[key] = kind;
    }
    #endregion
}
