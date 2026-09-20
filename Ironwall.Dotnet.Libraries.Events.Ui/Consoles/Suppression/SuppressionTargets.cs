using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
/****************************************************************************
   Purpose      : 억제 스케줄 '대상 칩 트레이' 담기 판정 — 순수 함수.
                  장비 · 그룹을 끌어 담을 때 무엇을 받고 무엇을 왜 빼는지 한 곳에서 정한다.
   Created By   : GHLee
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>대상 칩의 종류 — 서버 <c>target_type</c> 의 <c>device</c> / <c>group</c> 과 1:1.</summary>
public enum SuppressionTargetKind
{
    /// <summary>장비 한 대(<c>target_device_ids[]</c>).</summary>
    Device,
    /// <summary>장비 그룹 하나(<c>target_group_ids[]</c>).</summary>
    Group,
}

/// <summary>
/// 대상 트레이에 담긴 칩 한 개.
/// </summary>
/// <param name="Kind">장비인가 그룹인가.</param>
/// <param name="Id">서버 id. 0 이하는 아직 서버에 없는 것이라 담지 않는다.</param>
/// <param name="Label">칩에 찍는 이름.</param>
/// <param name="Detail">칩 툴팁 한 줄(구역 · 소속 등). 없으면 빈 문자열.</param>
public sealed record SuppressionTargetChip(SuppressionTargetKind Kind, int Id, string Label, string Detail = "")
{
    /// <summary>중복 판정 키 — 장비 3번과 그룹 3번이 섞이지 않게 종류를 같이 쓴다.</summary>
    public string Key => $"{(Kind == SuppressionTargetKind.Device ? "device" : "group")}:{Id}";

    /// <summary>아직 서버에 저장되지 않은 것(담을 수 없다).</summary>
    public bool IsUnsaved => Id <= 0;

    /// <summary>화면 · 자동화가 쓰는 짧은 표기.</summary>
    public override string ToString() => Label;
}

/// <summary>
/// 담기 한 번의 계획 — 무엇을 담고 무엇을 왜 뺐는가.
/// </summary>
/// <param name="Accepted">담을 것(고른 순서 유지 · 같은 대상은 한 번만).</param>
/// <param name="DuplicateExcluded">이미 트레이에 있어 뺀 수.</param>
/// <param name="WrongKindExcluded">지금 대상 유형과 종류가 달라 뺀 수(장비 모드에 그룹 등).</param>
/// <param name="UnsavedExcluded">서버 id 가 없어 뺀 수.</param>
/// <param name="OverLimitExcluded">상한을 넘어 뺀 수.</param>
/// <param name="BlockReason">하나도 담을 수 없는 까닭. 담을 수 있으면 null.</param>
public sealed record SuppressionTargetPlan(IReadOnlyList<SuppressionTargetChip> Accepted,
                                           int DuplicateExcluded,
                                           int WrongKindExcluded,
                                           int UnsavedExcluded,
                                           int OverLimitExcluded,
                                           string? BlockReason)
{
    /// <summary>담을 것이 하나라도 있는가.</summary>
    public bool CanAdd => BlockReason is null && Accepted.Count > 0;
}

/// <summary>
/// 장비 · 그룹 → <b>대상 칩 트레이</b> 담기의 판정(순수 함수). 화면 없이 단위 테스트한다.
/// </summary>
/// <remarks>
/// <para>정본: all-windows-drag-wireframe.html L304-306(억제 스케줄 = 장비 팔레트 · 그룹 → 대상 칩) ·
/// L420(서버 호출 = "스케줄 저장에 포함 1회" · 판정 "폼 저장 때 1회").</para>
/// <para>서버 계약이 <b>대상 배열</b>(<c>target_device_ids[]</c> · <c>target_group_ids[]</c>)이라
/// 대상이 몇이든 저장은 호출 <b>한 번</b>이다 — 그래서 드롭은 서버를 아예 부르지 않고 Draft 만 쌓는다.</para>
/// </remarks>
public static class SuppressionTargetDrop
{
    /// <summary>드롭존 키.</summary>
    public const string ZoneKey = "suppression-target-tray";

    /// <summary>대상 유형 — 서버 <c>target_type</c> 원값.</summary>
    public const string ModeDevice = "device";
    public const string ModeGroup = "group";
    public const string ModeAll = "all";

    /// <summary>
    /// 한 스케줄에 담을 수 있는 최대 대상 수(<b>클라 방어선</b>).
    /// <para>서버에는 상한이 없다 — 그러나 ① PATCH 는 대상 배열을 <b>통째로 교체</b>하므로 목록이 길수록
    /// 잘못 보낼 때 잃는 것이 커지고 ② 목록 '대상' 열 · 생성 확인 문구가 읽을 수 없게 된다.
    /// 상한을 넘는 억제가 필요하면 <c>target_type=all</c> 이나 그룹을 쓰는 것이 맞다.</para>
    /// </summary>
    public const int MaxTargets = 100;

    /// <summary>그 대상 유형이 개별 대상을 받는가 — <c>all</c> 은 받지 않는다.</summary>
    public static bool AcceptsTargets(string? mode)
        => mode is ModeDevice or ModeGroup;

    /// <summary>그 대상 유형이 받는 칩 종류.</summary>
    public static SuppressionTargetKind? KindFor(string? mode) => mode switch
    {
        ModeDevice => SuppressionTargetKind.Device,
        ModeGroup => SuppressionTargetKind.Group,
        _ => null,
    };

    /// <summary>
    /// 담기 계획을 세운다. <paramref name="existing"/> 은 지금 트레이에 있는 칩(중복 판정용).
    /// </summary>
    /// <param name="candidates">끌어 온 · 골라 온 대상들.</param>
    /// <param name="mode">지금 폼의 대상 유형(<c>device</c>/<c>group</c>/<c>all</c>).</param>
    /// <param name="existing">트레이에 이미 있는 칩.</param>
    /// <param name="canEdit">편집 권한(events:edit). 없으면 통째로 막는다.</param>
    public static SuppressionTargetPlan Plan(IEnumerable<SuppressionTargetChip>? candidates,
                                             string? mode,
                                             IEnumerable<SuppressionTargetChip>? existing,
                                             bool canEdit)
    {
        var rows = candidates?.Where(c => c is not null).ToList() ?? new List<SuppressionTargetChip>();
        var have = existing?.Where(c => c is not null).Select(c => c.Key).ToHashSet(StringComparer.Ordinal)
                   ?? new HashSet<string>(StringComparer.Ordinal);

        if (!canEdit) return Blocked("이벤트 편집 권한(events:edit)이 없습니다.");
        if (!AcceptsTargets(mode))
            return Blocked("'전체 대상' 스케줄에는 개별 대상을 담지 않습니다 — 대상 유형을 장비나 그룹으로 바꾸세요.");
        if (rows.Count == 0) return Blocked("담을 대상이 없습니다.");

        var want = KindFor(mode)!.Value;

        var wrongKind = rows.Count(r => r.Kind != want);
        var sameKind = rows.Where(r => r.Kind == want).ToList();

        var unsaved = sameKind.Count(r => r.IsUnsaved);
        var saved = sameKind.Where(r => !r.IsUnsaved).ToList();

        var duplicate = 0;
        var accepted = new List<SuppressionTargetChip>();
        var seen = new HashSet<string>(have, StringComparer.Ordinal);
        foreach (var row in saved)
        {
            if (seen.Add(row.Key)) accepted.Add(row);
            else duplicate++;
        }

        var room = Math.Max(0, MaxTargets - have.Count);
        var overLimit = 0;
        if (accepted.Count > room)
        {
            overLimit = accepted.Count - room;
            accepted = accepted.Take(room).ToList();
        }

        string? reason = null;
        if (accepted.Count == 0)
        {
            reason = overLimit > 0
                ? $"대상은 한 스케줄에 {MaxTargets}개까지입니다."
                : duplicate > 0 && wrongKind == 0 && unsaved == 0
                    ? "이미 담긴 대상입니다."
                    : wrongKind > 0 && sameKind.Count == 0
                        ? want == SuppressionTargetKind.Device
                            ? "장비 대상 스케줄에는 장비만 담을 수 있습니다."
                            : "그룹 대상 스케줄에는 그룹만 담을 수 있습니다."
                        : "저장되지 않은 대상입니다.";
        }

        return new SuppressionTargetPlan(accepted, duplicate, wrongKind, unsaved, overLimit, reason);

        static SuppressionTargetPlan Blocked(string why)
            => new(Array.Empty<SuppressionTargetChip>(), 0, 0, 0, 0, why);
    }

    /// <summary>상태 줄에 남길 한 줄.</summary>
    public static string ResultLine(SuppressionTargetPlan plan)
    {
        if (plan is null) return "담을 것이 없습니다.";
        if (!plan.CanAdd) return plan.BlockReason ?? "담을 것이 없습니다.";

        var parts = new List<string> { $"대상 {plan.Accepted.Count}개를 담았습니다" };
        if (plan.DuplicateExcluded > 0) parts.Add($"이미 있던 {plan.DuplicateExcluded}개는 뺐습니다");
        if (plan.WrongKindExcluded > 0) parts.Add($"종류가 다른 {plan.WrongKindExcluded}개는 뺐습니다");
        if (plan.UnsavedExcluded > 0) parts.Add($"저장 전 {plan.UnsavedExcluded}개는 뺐습니다");
        if (plan.OverLimitExcluded > 0) parts.Add($"{MaxTargets}개 상한이라 {plan.OverLimitExcluded}개는 남겼습니다");
        return string.Join(" · ", parts);
    }
}
