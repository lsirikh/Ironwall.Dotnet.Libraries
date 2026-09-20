using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;

/// <summary>그룹 칸 하나의 상태 — 전부 / 하나도 / 줄마다 다름(WS L614-617).</summary>
public enum GroupCheck
{
    /// <summary>고른 줄 가운데 아무도 이 그룹에 없다.</summary>
    None = 0,
    /// <summary>고른 줄 전부가 이 그룹에 있다.</summary>
    All = 1,
    /// <summary>섞여 있다 — 건드리지 않으면 <b>그대로 둔다</b>.</summary>
    Mixed = 2,
}

/// <summary>저장 때 보낼 그룹 호출 한 건 — <b>그룹 하나 · 방향 하나에 호출 한 번</b>(센서 수와 무관).</summary>
/// <param name="GroupId">대상 그룹.</param>
/// <param name="Add"><c>true</c> = 넣기(POST) · <c>false</c> = 빼기(DELETE).</param>
/// <param name="DeviceIds">그 호출에 실릴 장비 id(서버에 이미 있는 줄만).</param>
public sealed record GroupCall(int GroupId, bool Add, IReadOnlyList<int> DeviceIds);

/// <summary>
/// 그룹 3상태 편집(WS L326, L367, L448, L614-618) — <b>덮어쓰지 않고 변화분만</b> 더하고 뺀다.
/// </summary>
/// <remarks>
/// <para>지금 앱의 결함이 여기다(WS L326): 여러 줄을 고르고 그룹을 적용하면 <b>선택한 줄 전체를 덮어써</b>
/// 줄마다 달랐던 그룹이 사라진다. 그래서 이 창은 <b>체크를 바꾼 그룹만</b>, 그것도
/// <b>실제로 바뀌어야 하는 줄만</b> 보낸다(WS L367 "적용을 전체 덮어쓰기에서 체크 변화분만 더하고 빼기로").</para>
/// <para>서버 통로는 배치다 — <c>AssignDevicesToGroupAsync</c> · <c>RemoveDevicesFromGroupAsync</c> 가
/// 장비 id 목록을 한 번에 받는다. 그래서 센서 24대의 그룹 하나를 바꿔도 <b>호출은 한 번</b>이다.</para>
/// </remarks>
public static class SensorGroupEdit
{
    /// <summary>고른 줄에서 그 그룹의 상태.</summary>
    public static GroupCheck StateOf(IEnumerable<WiringSensorRow>? rows, int groupId)
    {
        var list = rows?.ToList() ?? new List<WiringSensorRow>();
        if (list.Count == 0) return GroupCheck.None;

        var inGroup = list.Count(r => r.Groups.Contains(groupId));
        if (inGroup == 0) return GroupCheck.None;
        return inGroup == list.Count ? GroupCheck.All : GroupCheck.Mixed;
    }

    /// <summary>체크를 한 번 누르면 어떤 상태가 되는가 — 섞인 칸은 <b>전부 넣기</b>로 간다(WS L628).</summary>
    public static bool NextValue(GroupCheck state) => state != GroupCheck.All;

    /// <summary>
    /// 손댄 그룹을 고른 줄의 Draft 에 적용한다. 손대지 않은 그룹은 줄마다 원래 값을 그대로 둔다.
    /// </summary>
    /// <param name="rows">고른 줄.</param>
    /// <param name="touched">그룹 id → 넣을 것인가(<c>true</c>) 뺄 것인가(<c>false</c>).</param>
    /// <returns>실제로 값이 바뀐 줄 수.</returns>
    public static int Apply(IEnumerable<WiringSensorRow>? rows, IReadOnlyDictionary<int, bool>? touched)
    {
        if (rows is null || touched is null || touched.Count == 0) return 0;

        var changed = 0;
        foreach (var row in rows)
        {
            var before = row.Groups.Count;
            var mutated = false;
            foreach (var (groupId, add) in touched)
            {
                if (add) mutated |= row.Groups.Add(groupId);
                else mutated |= row.Groups.Remove(groupId);
            }
            if (mutated || row.Groups.Count != before) changed++;
        }
        return changed;
    }

    /// <summary>
    /// 저장 때 보낼 호출 목록 — 그룹 하나 · 방향 하나에 한 건. 바뀐 줄이 없는 그룹은 아예 나오지 않는다.
    /// </summary>
    /// <param name="rows">보드의 모든 줄. 아직 서버에 없는 줄(<c>Id &lt;= 0</c>)은 <paramref name="idOf"/> 로 id 를 얻는다.</param>
    /// <param name="idOf">줄 → 서버 id(막 만든 줄의 id 를 넘기는 자리). <c>null</c> 이면 <see cref="WiringSensorRow.Id"/>.</param>
    public static IReadOnlyList<GroupCall> Plan(IEnumerable<WiringSensorRow>? rows, Func<WiringSensorRow, int>? idOf = null)
    {
        var adds = new Dictionary<int, List<int>>();
        var removes = new Dictionary<int, List<int>>();

        foreach (var row in rows ?? Enumerable.Empty<WiringSensorRow>())
        {
            var id = idOf?.Invoke(row) ?? row.Id;
            if (id <= 0) continue;                       // 서버에 없는 줄은 보낼 수 없다

            foreach (var groupId in row.Groups.Except(row.BaselineGroups))
                Bucket(adds, groupId).Add(id);

            foreach (var groupId in row.BaselineGroups.Except(row.Groups))
                Bucket(removes, groupId).Add(id);
        }

        var calls = new List<GroupCall>();
        foreach (var (groupId, ids) in adds.OrderBy(p => p.Key)) calls.Add(new GroupCall(groupId, true, ids));
        foreach (var (groupId, ids) in removes.OrderBy(p => p.Key)) calls.Add(new GroupCall(groupId, false, ids));
        return calls;

        static List<int> Bucket(Dictionary<int, List<int>> map, int key)
        {
            if (!map.TryGetValue(key, out var list)) map[key] = list = new List<int>();
            return list;
        }
    }

    /// <summary>적용 전에 말로 보여 주는 조각 — "북측 추가 · 정문 제거".</summary>
    public static string PreviewSentence(IReadOnlyDictionary<int, bool>? touched, Func<int, string> nameOf)
    {
        if (touched is null || touched.Count == 0) return string.Empty;
        return string.Join(" · ", touched.OrderBy(p => p.Key).Select(p => $"{nameOf(p.Key)} {(p.Value ? "추가" : "제거")}"));
    }
}
