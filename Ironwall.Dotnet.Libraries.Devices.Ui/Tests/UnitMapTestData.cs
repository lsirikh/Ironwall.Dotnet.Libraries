using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/****************************************************************************
   Purpose      : 부대 관계도 시험 데이터 빌더 — 결정적 편제 · 장비 생성기 (unit-relationship-map SETUP-02)
   Created By   : Claude
   Created On   : 2026-09-28
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 관계도 시험이 함께 쓰는 <b>결정적</b> 편제 · 장비. 같은 호출은 언제나 같은 id · 코드 · 이름을 낸다.
/// </summary>
/// <remarks>
/// <para><b>편제 → 트리는 기존 <see cref="UnitTreeBuilder"/> 를 그대로 쓴다</b> — 부모 배선 · 고아 · 순환 방어 규칙의
/// 사본을 여기 두지 않는다(플랜 SETUP-02). 그래서 모든 픽스처는 서버 응답 모양(<see cref="UnitGraphDto"/>)으로 만든다.</para>
/// <para>200 부대 편제는 스토리보드 스크립트(<c>unit-relationship-map-storyboard.html</c> 의 픽스처)와 같은 모양 · 같은 이름이다 —
/// 사단 1 · 연대 3 · 대대 9 · 중대 36(1~7중대 소초 5 · 나머지 4) · 소초 151. id 는 깊이 우선 생성 순서(사단 = 1).</para>
/// <para>장비 상태(<c>ACTIVATED</c> · <c>ERROR</c> · <c>DEACTIVATED</c>)는 <see cref="UnitMapDeviceSet.StatusById"/> 로 따로 준다 —
/// <see cref="UnitDeviceItem"/> 의 <c>Status</c> 는 레인 B(IMPL-03)가 더한다. 그 뒤에는 이 표를 생성자로 옮기면 된다.</para>
/// </remarks>
internal static class UnitMapTestData
{
    public const string ECHELON_DIVISION = "Division";
    public const string ECHELON_REGIMENT = "Regiment";
    public const string ECHELON_BATTALION = "Battalion";
    public const string ECHELON_COMPANY = "Company";
    public const string ECHELON_OUTPOST = "Outpost";

    #region - 기본 조립 -
    /// <summary>서버 목록(basic) 프로필 노드 하나.</summary>
    public static UnitListDto Node(int id, string code, string name, string echelon, int? parentId = null, bool isEnable = true)
        => new() { Id = id, Code = code, Name = name, EchelonRaw = echelon, ParentId = parentId, IsEnable = isEnable };

    /// <summary>
    /// 서버 응답 모양의 그래프. <paramref name="hierarchy"/> 를 주지 않으면 노드의 <c>parent_id</c> 중
    /// <b>그래프 안에 있는 것만</b> 계층 간선으로 싣는다 — 서버 <c>/graph</c> 와 같다(편제 밖 상위는 간선이 없다).
    /// </summary>
    public static UnitGraphDto Graph(IEnumerable<UnitListDto> nodes, IEnumerable<(int Parent, int Child)>? hierarchy = null, IEnumerable<(int Low, int High)>? adjacency = null)
    {
        var list = nodes.ToList();
        var ids = new HashSet<int>(list.Select(n => n.Id));
        var edges = hierarchy?.ToList()
                    ?? list.Where(n => n.ParentId is int p && ids.Contains(p))
                           .Select(n => (Parent: n.ParentId!.Value, Child: n.Id))
                           .ToList();

        return new UnitGraphDto
        {
            Nodes = list.OrderBy(n => n.Id).ToList(),
            Edges = new UnitGraphEdgesDto
            {
                Hierarchy = edges.OrderBy(e => e.Child).Select(e => new List<int> { e.Parent, e.Child }).ToList(),
                Adjacency = (adjacency ?? Enumerable.Empty<(int, int)>())
                            .Select(e => e.Item1 < e.Item2 ? (e.Item1, e.Item2) : (e.Item2, e.Item1))
                            .Distinct()
                            .OrderBy(e => e.Item1).ThenBy(e => e.Item2)
                            .Select(e => new List<int> { e.Item1, e.Item2 })
                            .ToList(),
            },
        };
    }

    /// <summary>그래프 → 콘솔이 쓰는 트리(기존 빌더).</summary>
    public static UnitTreeModel Tree(UnitGraphDto graph) => UnitTreeBuilder.Build(graph);
    #endregion

    #region - ① 200 부대 편제 (PRD §3.3 예 · NFR-01 규모) -
    /// <summary>
    /// 사단 1 · 연대 3 · 대대 9 · 중대 36 · 소초 151 = 200, 인접 60(중대 35 · 대대 8 · 연대 2 · 소초 15).
    /// </summary>
    public static UnitMapFixture Standard200()
    {
        var nodes = new List<UnitListDto>();
        var nextId = 1;
        int Add(string code, string name, string echelon, int? parent)
        {
            var id = nextId++;
            nodes.Add(Node(id, code, name, echelon, parent));
            return id;
        }

        var division = Add("d01", "제○○사단", ECHELON_DIVISION, null);
        var regiments = new List<int>();
        var battalions = new List<int>();
        var companies = new List<int>();
        var firstTwoOutposts = new List<(int, int)>();

        var b = 0;
        var c = 0;
        for (var r = 1; r <= 3; r++)
        {
            var regiment = Add($"r{r:00}", $"{r}연대", ECHELON_REGIMENT, division);
            regiments.Add(regiment);
            for (var i = 0; i < 3; i++)
            {
                b++;
                var battalion = Add($"b{b:00}", $"{b}대대", ECHELON_BATTALION, regiment);
                battalions.Add(battalion);
                for (var j = 0; j < 4; j++)
                {
                    c++;
                    var company = Add($"c{b:00}{c:00}", $"{c}중대", ECHELON_COMPANY, battalion);
                    companies.Add(company);
                    var outposts = c <= 7 ? 5 : 4;
                    var first = 0;
                    for (var q = 1; q <= outposts; q++)
                    {
                        var outpost = Add($"p{c:00}{q}", $"{c}{q}소초", ECHELON_OUTPOST, company);
                        if (q == 1) first = outpost;
                        if (q == 2 && c <= 15) firstTwoOutposts.Add((first, outpost));
                    }
                }
            }
        }

        var adjacency = new List<(int, int)>();
        for (var k = 0; k + 1 < companies.Count; k++) adjacency.Add((companies[k], companies[k + 1]));     // 35
        for (var k = 0; k + 1 < battalions.Count; k++) adjacency.Add((battalions[k], battalions[k + 1])); // 8
        for (var k = 0; k + 1 < regiments.Count; k++) adjacency.Add((regiments[k], regiments[k + 1]));    // 2
        adjacency.AddRange(firstTwoOutposts);                                                              // 15

        var graph = Graph(nodes, adjacency: adjacency);
        return new UnitMapFixture(graph, Tree(graph));
    }

    /// <summary>
    /// 200 부대 + <b>최상위 중대 둘</b>(<c>unit001</c> 기본중대 id 201 · <c>c99</c> 예비중대 id 202) — 최상위 부대의 상위 바꾸기 ·
    /// 되돌리기(옛 상위 = 없음 → 최상위로 돌려보내기)를 겪게 한다(REVIEW-01 HIGH-1).
    /// </summary>
    public static UnitMapFixture Standard200WithRoots()
    {
        var baseline = Standard200();
        var nodes = baseline.Graph.Nodes.ToList();
        nodes.Add(Node(201, "unit001", "기본중대", ECHELON_COMPANY));
        nodes.Add(Node(202, "c99", "예비중대", ECHELON_COMPANY));
        var graph = Graph(nodes, adjacency: baseline.Graph.Edges.AdjacencyPairs.ToList());
        return new UnitMapFixture(graph, Tree(graph));
    }
    #endregion

    #region - ② 소형 픽스처 -
    /// <summary>중대 1 밑에 소초 3 — 자식이 모두 끝 부대(세로 한 줄).</summary>
    public static UnitMapFixture LeafChildren()
    {
        var graph = Graph(new[]
        {
            Node(1, "c01", "1중대", ECHELON_COMPANY),
            Node(2, "p013", "13소초", ECHELON_OUTPOST, 1),
            Node(3, "p011", "11소초", ECHELON_OUTPOST, 1),
            Node(4, "p012", "12소초", ECHELON_OUTPOST, 1),
        });
        return new UnitMapFixture(graph, Tree(graph));
    }

    /// <summary>
    /// 섞임 — 대대 1 밑에 중대 3: <c>c01</c>(소초 2) · <c>c02</c>(끝 부대) · <c>c03</c>(소초 1).
    /// 대대는 세로 한 줄이 아니다(자식 중 끝 부대가 아닌 것이 있다) → 칸 = 1 + 1 + 1 = 3.
    /// </summary>
    public static UnitMapFixture Mixed()
    {
        var graph = Graph(new[]
        {
            Node(10, "b01", "1대대", ECHELON_BATTALION),
            Node(11, "c01", "1중대", ECHELON_COMPANY, 10),
            Node(12, "c02", "2중대", ECHELON_COMPANY, 10),
            Node(13, "c03", "3중대", ECHELON_COMPANY, 10),
            Node(14, "p011", "11소초", ECHELON_OUTPOST, 11),
            Node(15, "p012", "12소초", ECHELON_OUTPOST, 11),
            Node(16, "p031", "31소초", ECHELON_OUTPOST, 13),
        });
        return new UnitMapFixture(graph, Tree(graph));
    }

    /// <summary>편제 밖 상위 — <c>c09</c> 의 <c>parent_id</c> 가 응답에 없는 999 를 가리킨다(부분 그래프의 인접 상대).</summary>
    public static UnitMapFixture OrphanParent()
    {
        var graph = Graph(
            new[]
            {
                Node(1, "b01", "1대대", ECHELON_BATTALION),
                Node(2, "c01", "1중대", ECHELON_COMPANY, 1),
                Node(3, "c09", "9중대", ECHELON_COMPANY, 999),
            },
            adjacency: new[] { (2, 3) });
        return new UnitMapFixture(graph, Tree(graph));
    }

    /// <summary>뿌리 여럿 — 코드가 id 순서와 반대(<c>d02</c> id 1 · <c>d01</c> id 2) · 뿌리 사이의 코드 순이 드러난다.</summary>
    public static UnitMapFixture MultiRoot()
    {
        var graph = Graph(new[]
        {
            Node(1, "d02", "제2사단", ECHELON_DIVISION),
            Node(2, "d01", "제1사단", ECHELON_DIVISION),
            Node(3, "r21", "21연대", ECHELON_REGIMENT, 1),
            Node(4, "r11", "11연대", ECHELON_REGIMENT, 2),
            Node(5, "r12", "12연대", ECHELON_REGIMENT, 2),
        });
        return new UnitMapFixture(graph, Tree(graph));
    }

    /// <summary>
    /// 뿌리 여럿 + 기본 부대 + 고아(SIM-L050 모양) — 사단 <c>d01</c>(연대 · 대대 · 중대 · 소초 사슬) · 기본 중대 <c>unit001</c>(최상위) ·
    /// 상위가 응답에 없는 중대 <c>c09</c>. 뿌리 순서는 편제 트리(제대 순위 → 코드)와 같아야 한다.
    /// </summary>
    public static UnitMapFixture MultiRootWithOrphan()
    {
        var graph = Graph(new[]
        {
            Node(1, "d01", "제1사단", ECHELON_DIVISION),
            Node(2, "r01", "1연대", ECHELON_REGIMENT, 1),
            Node(3, "b01", "1대대", ECHELON_BATTALION, 2),
            Node(4, "c01", "1중대", ECHELON_COMPANY, 3),
            Node(5, "p011", "11소초", ECHELON_OUTPOST, 4),
            Node(6, "unit001", "기본중대", ECHELON_COMPANY),
            Node(7, "c09", "9중대", ECHELON_COMPANY, 999),
            Node(8, "p091", "91소초", ECHELON_OUTPOST, 7),
        });
        return new UnitMapFixture(graph, Tree(graph));
    }

    /// <summary>모르는 제대 — 서버가 나중에 늘린 <c>Brigade</c>(해석 불가 → <c>Echelon == null</c>).</summary>
    public static UnitMapFixture UnknownEchelon()
    {
        var graph = Graph(new[]
        {
            Node(1, "d01", "제1사단", ECHELON_DIVISION),
            Node(2, "x01", "1여단", "Brigade", 1),
            Node(3, "b01", "1대대", ECHELON_BATTALION, 1),
            Node(4, "c01", "1중대", ECHELON_COMPANY, 3),
        });
        return new UnitMapFixture(graph, Tree(graph));
    }
    #endregion

    #region - ③ 장비 2,000 -
    public const string STATUS_ACTIVATED = "ACTIVATED";
    public const string STATUS_ERROR = "ERROR";
    public const string STATUS_DEACTIVATED = "DEACTIVATED";

    private static readonly EnumDeviceCategory[] Categories =
    {
        EnumDeviceCategory.Controller, EnumDeviceCategory.Sensor, EnumDeviceCategory.Camera,
        EnumDeviceCategory.Speaker, EnumDeviceCategory.Enclosure, EnumDeviceCategory.Lamp, EnumDeviceCategory.Gate,
    };

    /// <summary>
    /// 장비 <paramref name="count"/> 대를 편제에 결정적으로 흩는다 — 50 대마다 1 대는 미배치(<c>UnitId == null</c>),
    /// 상태는 17 번째마다 <c>ERROR</c> · 23 번째마다 <c>DEACTIVATED</c> · 나머지 <c>ACTIVATED</c>.
    /// </summary>
    public static UnitMapDeviceSet Devices(UnitTreeModel tree, int count = 2000)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var units = tree.Ordered.Select(n => n.Id).ToList();
        var items = new List<UnitDeviceItem>(count);
        var status = new Dictionary<int, string>(count);

        for (var i = 1; i <= count; i++)
        {
            int? unitId = units.Count == 0 || i % 50 == 0 ? null : units[(i * 7) % units.Count];
            var category = Categories[i % Categories.Length];
            items.Add(new UnitDeviceItem(1000 + i, i, $"장비{i:0000}", category, unitId));
            status[1000 + i] = i % 17 == 0 ? STATUS_ERROR
                             : i % 23 == 0 ? STATUS_DEACTIVATED
                             : STATUS_ACTIVATED;
        }
        return new UnitMapDeviceSet(items, status);
    }
    #endregion
}

/// <summary>한 편제 픽스처 — 서버 응답과 그것으로 조립한 트리.</summary>
internal sealed record UnitMapFixture(UnitGraphDto Graph, UnitTreeModel Tree)
{
    /// <summary>이름으로 부대 id(없으면 예외 — 픽스처 오타를 바로 드러낸다).</summary>
    public int IdOf(string name)
        => Tree.Ordered.FirstOrDefault(n => n.Name == name)?.Id
           ?? throw new ArgumentException($"픽스처에 '{name}' 부대가 없습니다.", nameof(name));

    public UnitTreeNode NodeOf(string name) => Tree.Find(IdOf(name))!;

    /// <summary>인접 쌍 수(<c>[low, high]</c> 한 번씩).</summary>
    public int AdjacencyPairCount => Graph.Edges.Adjacency.Count;
}

/// <summary>장비 목록과 그 상태 문자열(목록 DTO 의 <c>status</c>).</summary>
internal sealed record UnitMapDeviceSet(IReadOnlyList<UnitDeviceItem> Items, IReadOnlyDictionary<int, string> StatusById);
