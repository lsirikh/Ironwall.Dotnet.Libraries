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
/// 결선 보드 — 선 2가닥의 칸과 미배치 센서. <b>판정은 전부 여기</b>(UI 없음 · 헤드리스 테스트 대상).
/// </summary>
/// <remarks>
/// <para>순번은 칸 번호가 아니다 — 그 선에서 <b>자기 앞(자기 포함)의 찬 칸 수</b>다(WS L681).
/// 빈 자리는 <b>빈 자리로 남고</b> 경고가 알린다 — 당겨 붙이는 것은 [번호 순으로 자동 배치]의 일이다
/// (목업 L433·L717 의 "자동으로 당겨집니다" 에서 벗어난 자리 · PRD 이탈 D-4).</para>
/// <para>되돌리기는 <b>한 스택</b>이다 — 표 값과 결선을 같이 찍는다(WS L181 "되돌리기 한 번으로 전부 원위치").</para>
/// </remarks>
public sealed class WiringBoard
{
    /// <summary>한 선의 칸 상한. 순번 상한과 같다.</summary>
    public const int MAX_SLOTS = 64;

    /// <summary>처음 그릴 때의 칸 수(WS L676-677 과 같다).</summary>
    public const int DEFAULT_SLOTS = 8;

    /// <summary>자동 배치 뒤에 남겨 두는 최소 칸 수(WS L762).</summary>
    public const int MIN_SLOTS_AFTER_AUTO = 4;

    /// <summary>되돌리기 깊이 — 한 세션에서 이보다 더 거슬러 가지 않는다.</summary>
    public const int MAX_UNDO = 100;

    private readonly List<WiringSensorRow> _rows = new();
    private readonly List<int?>[] _lines = { new(), new() };
    private readonly Stack<Snapshot> _undo = new();
    private int _nextNewKey = -1;

    public WiringBoard()
    {
        for (var line = 1; line <= 2; line++)
            for (var i = 0; i < DEFAULT_SLOTS; i++) Slots(line).Add(null);
    }

    #region - Read -
    public IReadOnlyList<WiringSensorRow> Rows => _rows;

    /// <summary>그 선의 칸 — 값은 <see cref="WiringSensorRow.Key"/>(빈 칸은 <c>null</c>).</summary>
    public IReadOnlyList<int?> Line(int line) => Slots(line);

    public int SlotCount(int line) => Slots(line).Count;

    public WiringSensorRow? RowAt(int line, int index)
    {
        var slots = Slots(line);
        if (index < 0 || index >= slots.Count) return null;
        return slots[index] is { } key ? Find(key) : null;
    }

    public WiringSensorRow? Find(int key) => _rows.FirstOrDefault(r => r.Key == key);

    /// <summary>선에 붙이지 않은 센서 — 팔레트(WS L409).</summary>
    public IReadOnlyList<WiringSensorRow> Unplaced
        => _rows.Where(r => PlacementOf(r.Key) is null).OrderBy(r => r.Facts.Number).ToList();

    /// <summary>
    /// 그 칸의 순번(1부터) = <b>칸의 자리 그대로</b>. 빈 칸이면 <c>0</c>.
    /// </summary>
    /// <remarks>
    /// <para><b>번호 체계는 하나다</b> — 칸 번호 · 배지의 순번 · 저장되는 <c>order</c> · 빈 칸 경고가 가리키는 자리가
    /// 전부 같은 수다. 목업(L681)은 배지를 "찬 칸만 센 수"로 그려 가운데가 빈 상태에서
    /// <b>배지 2 옆의 경고가 "2번 자리가 비어 있다"</b>고 말하는 모순이 생겼다(적대 검토 C5).</para>
    /// <para>그리고 이 수는 장애의 <b>고장 구간 정수</b>와 같은 축이다 — 서버에 2·3·4 로 저장된 루프를
    /// 화면에서 1·2·3 으로 다시 매기면 "1차 4~5" 가 가리키는 자리가 말없이 바뀐다.
    /// 그래서 빈 자리는 <b>빈 자리로 두고</b> 경고로 알린다(당겨 붙이는 것은 [번호 순으로 자동 배치]가 한다).</para>
    /// </remarks>
    public int OrderAt(int line, int index)
    {
        var slots = Slots(line);
        if (index < 0 || index >= slots.Count || slots[index] is null) return 0;
        return index + 1;
    }

    public WiringPlacement? PlacementOf(int key)
    {
        for (var line = 1; line <= 2; line++)
        {
            var index = Slots(line).IndexOf(key);
            if (index >= 0) return new WiringPlacement(line, OrderAt(line, index));
        }
        return null;
    }

    /// <summary>그 선에 붙은 센서를 순번 순으로.</summary>
    public IReadOnlyList<WiringSensorRow> Placed(int line)
        => Slots(line).Where(k => k is not null).Select(k => Find(k!.Value)).Where(r => r is not null).Select(r => r!).ToList();
    #endregion

    #region - Load -
    /// <summary>
    /// 서버에서 받은 센서로 보드를 채운다. 저장된 결선이 <b>겹치거나 범위 밖</b>이면 그 줄은 미배치로 두고 까닭을 남긴다.
    /// </summary>
    public void Load(IEnumerable<(int Id, int? Channel, SensorFacts Facts, WiringPlacement? Placement, string? Issue, IReadOnlyList<int>? Groups)> sensors)
    {
        _rows.Clear();
        _undo.Clear();
        for (var line = 1; line <= 2; line++)
        {
            Slots(line).Clear();
            for (var i = 0; i < DEFAULT_SLOTS; i++) Slots(line).Add(null);
        }

        var loaded = sensors?.ToList() ?? new();
        foreach (var s in loaded)
            _rows.Add(new WiringSensorRow(s.Id > 0 ? s.Id : _nextNewKey--, s.Id, s.Channel, s.Facts, s.Placement, s.Issue, s.Groups));

        // 저장된 자리에 그대로 앉힌다 — 빈 자리는 빈 칸으로 남는다(당겨 붙이지 않는다 · C5).
        // 같은 (선, 순번) 을 두 줄이 주장하면 번호가 작은 줄이 자리를 갖고, 나머지는 미배치 + 까닭.
        foreach (var line in new[] { WiringSpec.LINE_PRIMARY, WiringSpec.LINE_SECONDARY })
        {
            var claims = _rows
                .Where(r => r.BaselinePlacement?.Line == line)
                .OrderBy(r => r.BaselinePlacement!.Order)
                .ThenBy(r => r.Facts.Number)
                .ToList();

            foreach (var row in claims)
            {
                var order = row.BaselinePlacement!.Order;
                var index = order - 1;
                EnsureSlots(line, order);
                if (index >= Slots(line).Count)
                {
                    row.LoadIssue = $"{line}차 {order}번 자리가 이 선의 칸 수({MAX_SLOTS})를 넘어 미배치로 두었습니다.";
                    row.BaselinePlacement = null;
                    continue;
                }
                if (Slots(line)[index] is not null)
                {
                    row.LoadIssue = $"{line}차 {order}번 자리를 다른 센서가 이미 쓰고 있어 미배치로 두었습니다.";
                    row.BaselinePlacement = null;      // 기준도 "미배치" 다 — 저장 때 이 줄만 보낸다
                    continue;
                }
                Slots(line)[index] = row.Key;
            }
        }
    }

    /// <summary>새 줄을 더한다(센서 여러 개 만들기 · 엑셀 붙여넣기). 서버에는 저장 때 만든다.</summary>
    public WiringSensorRow AddRow(SensorFacts facts, IEnumerable<int>? groups = null)
    {
        var row = new WiringSensorRow(_nextNewKey--, 0, null, facts, null, null, groups);
        _rows.Add(row);
        return row;
    }
    #endregion

    #region - Edit -
    /// <summary>빈 칸에 놓는다. 찬 칸이거나 없는 칸이면 <c>false</c>(아무것도 바뀌지 않는다).</summary>
    public bool Place(int key, int line, int index)
    {
        var slots = Slots(line);
        if (index < 0 || index >= slots.Count) return false;
        if (slots[index] is { } occupant && occupant != key) return false;
        if (Find(key) is not { } row) return false;
        if (slots[index] == key) return false;

        Detach(key);
        slots[index] = key;
        row.LoadIssue = null;       // 사람이 자리를 정해 줬다 — 불러오기 경고는 여기서 풀린다(C4)
        return true;
    }

    /// <summary>선에서 뺀다 — 그 자리는 <b>빈 칸으로 남는다</b>(자리는 곧 순번이다).</summary>
    public bool Unplace(int key)
    {
        var removed = Detach(key);
        if (Find(key) is { } row) row.LoadIssue = null;      // 빼는 것도 사람의 판단이다(C4)
        return removed;
    }

    /// <summary>칸을 하나 늘린다(WS L411, L688).</summary>
    public bool AddSlot(int line)
    {
        var slots = Slots(line);
        if (slots.Count >= MAX_SLOTS) return false;
        slots.Add(null);
        return true;
    }

    /// <summary>
    /// 번호 순으로 자동 배치 — 앞 절반은 1차, 뒤 절반은 2차(WS L757-763).
    /// </summary>
    public bool AutoLayoutByNumber()
    {
        var all = _rows.OrderBy(r => r.Facts.Number).ThenBy(r => r.Key).ToList();
        var half = (int)Math.Ceiling(all.Count / 2.0);

        // 말없이 버리지 않는다 — 한 선에 담을 수 없으면 아무것도 바꾸지 않고 거절한다(C11).
        if (half > MAX_SLOTS || all.Count - half > MAX_SLOTS) return false;

        for (var line = 1; line <= 2; line++) Slots(line).Clear();

        for (var i = 0; i < all.Count; i++)
            Slots(i < half ? WiringSpec.LINE_PRIMARY : WiringSpec.LINE_SECONDARY).Add(all[i].Key);

        for (var line = 1; line <= 2; line++)
        {
            var slots = Slots(line);
            while (slots.Count < MIN_SLOTS_AFTER_AUTO) slots.Add(null);
        }

        foreach (var row in _rows) row.LoadIssue = null;     // 자리를 전부 다시 정했다(C4)
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
    /// </remarks>
    public void MarkBaseline(IEnumerable<int>? keys = null)
    {
        _undo.Clear();
        var set = keys is null ? null : new HashSet<int>(keys);
        foreach (var row in _rows)
        {
            if (set is not null && !set.Contains(row.Key)) continue;
            row.Baseline = row.Facts;
            row.BaselinePlacement = PlacementOf(row.Key);
            row.MarkGroupBaseline();
            row.LoadIssue = null;
        }
    }

    /// <summary>저장으로 서버 Id 를 받은 새 줄을 기존 줄로 바꿔 단다(키는 유지한다 — 화면이 쥐고 있다).</summary>
    public WiringSensorRow? Promote(int key, int newId)
    {
        var old = Find(key);
        if (old is null || newId <= 0) return null;

        var promoted = new WiringSensorRow(key, newId, old.Channel, old.Facts, PlacementOf(key), null, old.Groups);
        _rows[_rows.IndexOf(old)] = promoted;
        return promoted;
    }
    #endregion

    #region - Internals -
    private List<int?> Slots(int line) => _lines[line == WiringSpec.LINE_SECONDARY ? 1 : 0];

    private void EnsureSlots(int line, int count)
    {
        var slots = Slots(line);
        while (slots.Count < Math.Min(count, MAX_SLOTS)) slots.Add(null);
    }

    private bool Detach(int key)
    {
        var removed = false;
        for (var line = 1; line <= 2; line++)
        {
            var slots = Slots(line);
            for (var i = 0; i < slots.Count; i++)
                if (slots[i] == key) { slots[i] = null; removed = true; }
        }
        return removed;
    }

    private sealed record Snapshot(
        IReadOnlyList<WiringSensorRow> Rows,
        IReadOnlyList<SensorFacts> Facts,
        IReadOnlyList<int?> Line1,
        IReadOnlyList<int?> Line2);

    private Snapshot Capture()
        => new(_rows.ToList(), _rows.Select(r => r.Facts).ToList(), Slots(1).ToList(), Slots(2).ToList());

    private void Restore(Snapshot snapshot)
    {
        _rows.Clear();
        for (var i = 0; i < snapshot.Rows.Count; i++)
        {
            var row = snapshot.Rows[i];
            row.Facts = snapshot.Facts[i];      // 같은 객체를 되살린다 — 화면이 쥔 항목이 끊기지 않는다
            _rows.Add(row);
        }

        Slots(1).Clear();
        Slots(1).AddRange(snapshot.Line1);
        Slots(2).Clear();
        Slots(2).AddRange(snapshot.Line2);
    }
    #endregion
}
