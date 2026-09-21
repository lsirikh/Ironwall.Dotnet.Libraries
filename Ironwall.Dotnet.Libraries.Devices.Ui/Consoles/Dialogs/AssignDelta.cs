using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Dialogs;

/// <summary>
/// 장비 배정 창이 [저장] 때 보낼 것 — <b>들어온 것과 빠진 것</b>.
/// </summary>
/// <param name="GroupId">대상 그룹.</param>
/// <param name="Added">그룹에 넣을 장비 Id(오름차순 · 중복 없음).</param>
/// <param name="Removed">그룹에서 뺄 장비 Id(오름차순 · 중복 없음).</param>
/// <param name="DraftExcluded">아직 저장되지 않아(Id≤0) 뺀 수 — 말없이 버리지 않고 센다.</param>
/// <param name="BlockReason">보낼 수 없는 까닭. 보낼 수 있으면 null.</param>
public sealed record AssignPlan(int GroupId, IReadOnlyList<int> Added, IReadOnlyList<int> Removed, int DraftExcluded, string? BlockReason)
{
    public bool HasChanges => Added.Count > 0 || Removed.Count > 0;
    public bool CanSend => BlockReason is null && HasChanges;

    /// <summary>
    /// 서버를 몇 번 부르는가 — 넣기는 <b>한 번</b>, 빼기는 <b>⌈n/100⌉번</b>이다(장비마다가 아니다).
    /// </summary>
    /// <remarks>
    /// 빼기만 나뉘는 까닭: 서버의 <c>DeviceUnassignRequest.device_ids</c> 가
    /// <c>max_length=100</c> 이다(<c>app/schemas/device_group.py:108</c>). 넣기 쪽에는 상한이 없다(<c>:78</c>).
    /// 101대를 한 번에 빼려 하면 422 로 <b>한 대도</b> 빠지지 않는다.
    /// </remarks>
    public int CallCount => (Added.Count > 0 ? 1 : 0) + AssignDelta.RemoveCallCount(Removed.Count);
}

/// <summary>한 방향(넣기 · 빼기)의 결과 판정.</summary>
/// <param name="Requested">보낸 수.</param>
/// <param name="Applied">서버가 실제로 처리한 수.</param>
/// <param name="Skipped">서버가 건너뛴 수.</param>
/// <param name="Failed">호출 자체가 실패했는가.</param>
public sealed record AssignLegOutcome(int Requested, int Applied, int Skipped, bool Failed)
{
    public bool IsPartial => !Failed && Applied < Requested;
}

/// <summary>
/// 배정 창의 판정 — <b>순수 함수</b>. 창 · 서버 없이 시험한다
/// (규칙 drag-first-ux "판정 로직은 UI 에서 분리한 순수 함수로 두고 헤드리스 단위테스트를 붙인다").
/// </summary>
/// <remarks>
/// <para>왜 전체 덮어쓰기가 아니라 차분인가: 서버 통로가 <c>AssignDevicesToGroupAsync</c> · <c>RemoveDevicesFromGroupAsync</c>
/// 두 <b>배치</b> 호출이라, 바뀐 것만 보내면 호출이 <b>최대 두 번</b>으로 끝난다. 전체 집합을 다시 밀면
/// 손대지 않은 장비까지 건드려 다른 창이 그 사이에 넣은 것을 지운다.</para>
/// <para>보내기 전에 서버를 다시 읽어 <see cref="Drift"/> 로 견준다 — 어긋났으면 <b>아무것도 보내지 않고</b> 다시 읽기를 권한다.</para>
/// </remarks>
public static class AssignDelta
{
    /// <summary>
    /// 한 번에 뺄 수 있는 최대 수 — 서버 계약(<c>app/schemas/device_group.py:108</c> <c>max_length=100</c>).
    /// 넣기 쪽에는 상한이 없다.
    /// </summary>
    public const int RemoveChunkSize = 100;

    /// <summary>빼기를 몇 번에 나눠 보내는가.</summary>
    public static int RemoveCallCount(int removedCount)
        => removedCount <= 0 ? 0 : ((removedCount - 1) / RemoveChunkSize) + 1;

    /// <summary>빼기 목록을 서버 상한에 맞춰 자른다(순서 유지).</summary>
    public static IReadOnlyList<IReadOnlyList<int>> ChunkRemovals(IReadOnlyList<int> removed)
    {
        if (removed is null || removed.Count == 0) return Array.Empty<IReadOnlyList<int>>();

        var chunks = new List<IReadOnlyList<int>>();
        for (var offset = 0; offset < removed.Count; offset += RemoveChunkSize)
            chunks.Add(removed.Skip(offset).Take(RemoveChunkSize).ToList());
        return chunks;
    }

    /// <summary>왼쪽 목록(후보) 드롭존.</summary>
    public const string AvailableZone = "assign-available";

    /// <summary>오른쪽 목록(배정됨) 드롭존.</summary>
    public const string AssignedZone = "assign-assigned";

    /// <summary>
    /// <paramref name="baseline"/>(창을 열 때의 소속)과 <paramref name="desired"/>(지금 오른쪽 목록)의 차분.
    /// </summary>
    public static AssignPlan Plan(int groupId, IEnumerable<int>? baseline, IEnumerable<int>? desired)
    {
        var desiredAll = (desired ?? Enumerable.Empty<int>()).ToList();
        var draftExcluded = desiredAll.Count(id => id <= 0);

        // 저장되지 않은 장비(Id≤0)는 어느 쪽에서도 보내지 않는다 — 서버가 모르는 Id 다.
        var before = new HashSet<int>((baseline ?? Enumerable.Empty<int>()).Where(id => id > 0));
        var after = new HashSet<int>(desiredAll.Where(id => id > 0));

        var added = after.Except(before).OrderBy(id => id).ToList();
        var removed = before.Except(after).OrderBy(id => id).ToList();

        string? reason = null;
        if (groupId <= 0) reason = "아직 저장되지 않은 그룹이다 — 그룹을 먼저 저장한다";
        else if (added.Count == 0 && removed.Count == 0) reason = "바뀐 것이 없다";

        return new AssignPlan(groupId, added, removed, draftExcluded, reason);
    }

    /// <summary>
    /// 보내기 직전 재조회와 견주기. 어긋났으면 사람이 읽을 까닭을, 같으면 <c>null</c>.
    /// </summary>
    public static string? Drift(IEnumerable<int>? baseline, IEnumerable<int>? server)
    {
        var before = new HashSet<int>((baseline ?? Enumerable.Empty<int>()).Where(id => id > 0));
        var now = new HashSet<int>((server ?? Enumerable.Empty<int>()).Where(id => id > 0));
        if (before.SetEquals(now)) return null;

        var appeared = now.Except(before).Count();
        var vanished = before.Except(now).Count();
        var parts = new List<string>();
        if (appeared > 0) parts.Add($"{appeared}대가 더 들어와 있다");
        if (vanished > 0) parts.Add($"{vanished}대가 빠져 있다");
        return $"이 창을 연 뒤 다른 곳에서 그룹이 바뀌었다 — {string.Join(" · ", parts)}. 아무것도 보내지 않았다, 다시 읽고 고쳐 주세요.";
    }

    /// <summary>
    /// 보내기 직전 재조회와 견주기 — <b>수</b>만 아는 경우.
    /// </summary>
    /// <remarks>
    /// 지금 그룹 상세 DTO 에는 소속 장비 id 목록이 없어 수밖에 읽지 못한다(<see cref="IGroupMembershipProbe"/> 주석).
    /// 수가 같은 <b>맞교환</b>은 이 검사로 잡히지 않는다 — 다만 보내는 것이 차분이라 그 경우에도 남의 변경을 덮어쓰지는 않는다.
    /// </remarks>
    public static string? DriftByCount(int baselineCount, int? serverCount)
    {
        if (serverCount is not { } now) return "그룹을 다시 읽지 못해 보내지 않았다 — 잠시 뒤 다시 시도하세요.";
        if (now == baselineCount) return null;

        var delta = now - baselineCount;
        var what = delta > 0 ? $"{delta}대가 더 들어와 있다" : $"{-delta}대가 빠져 있다";
        return $"이 창을 연 뒤 다른 곳에서 그룹이 바뀌었다 — {what}. 아무것도 보내지 않았다, 다시 읽고 고쳐 주세요.";
    }

    /// <summary>버튼 줄에 적을 한 줄 — 무엇을 보낼 참인지.</summary>
    public static string Summary(AssignPlan plan)
    {
        if (plan.BlockReason is not null && !plan.HasChanges) return plan.BlockReason;

        var parts = new List<string>();
        if (plan.Added.Count > 0) parts.Add($"＋{plan.Added.Count}");
        if (plan.Removed.Count > 0) parts.Add($"−{plan.Removed.Count}");
        var head = parts.Count == 0 ? "바뀐 것이 없다" : $"보낼 변화 {string.Join(" · ", parts)} (호출 {plan.CallCount}회)";
        if (plan.DraftExcluded > 0) head += $" · 저장 전 {plan.DraftExcluded}대는 뺀다";
        return head;
    }

    /// <summary>
    /// 보낸 뒤의 한 줄. <b>서버가 실제로 한 것</b>만 성공으로 적는다 — 보냈다는 사실은 성공이 아니다.
    /// </summary>
    public static string ResultLine(string groupName, AssignLegOutcome? add, AssignLegOutcome? remove)
    {
        var good = new List<string>();
        var bad = new List<string>();

        Describe(add, "넣었다", "넣지 못했다");
        Describe(remove, "뺐다", "빼지 못했다");

        if (good.Count == 0 && bad.Count == 0) return "보낸 것이 없다";
        var line = good.Count > 0 ? $"'{groupName}' 에서 {string.Join(" · ", good)}" : $"'{groupName}' — {string.Join(" · ", bad)}";
        if (good.Count > 0 && bad.Count > 0) line += $" · {string.Join(" · ", bad)}";
        return line;

        void Describe(AssignLegOutcome? leg, string okWord, string failWord)
        {
            if (leg is null || leg.Requested == 0) return;
            if (leg.Failed) { bad.Add($"{leg.Requested}대를 {failWord}"); return; }
            if (leg.Applied > 0) good.Add($"{leg.Applied}대를 {okWord}");
            var missed = leg.Requested - leg.Applied;
            if (missed > 0) bad.Add($"{missed}대는 서버가 건너뛰었다");
        }
    }

    /// <summary>한쪽이라도 어긋났으면 창을 닫지 않는다 — 사람이 무엇이 남았는지 보고 다시 시도해야 한다.</summary>
    public static bool ShouldStayOpen(AssignLegOutcome? add, AssignLegOutcome? remove)
        => IsBad(add) || IsBad(remove);

    private static bool IsBad(AssignLegOutcome? leg)
        => leg is not null && leg.Requested > 0 && (leg.Failed || leg.IsPartial);
}
