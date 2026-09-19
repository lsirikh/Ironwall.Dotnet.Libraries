using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray;

/// <summary>조치 트레이에 담을 수 있는 이벤트 한 건(원본).</summary>
/// <param name="EventId">서버 이벤트 Id. 0 이하면 아직 저장되지 않은 Draft 다.</param>
/// <param name="Kind"><see cref="ActionTrayDrop.KindDetection"/> 또는 <see cref="ActionTrayDrop.KindMalfunction"/>.</param>
/// <param name="Label">트레이 줄에 찍는 한 줄 — "정문 센서 · 2026-09-20 14:02".</param>
/// <param name="AlreadyReported">이미 조치가 있는가. <b>담는 것을 막지 않는다</b> — 중복 조치보고는 허용이다.</param>
public readonly record struct ActionTrayCandidate(int EventId, string Kind, string Label, bool AlreadyReported)
{
    public bool IsDraft => EventId <= 0;

    /// <summary>Draft 의 대상 키 — Id 만으로 맞추면 탐지 3번과 장애 3번이 섞인다(Id + 타입).</summary>
    public string TargetKey => $"{Kind}:{EventId}";
}

/// <summary>담기 한 번의 계획 — 무엇을 담고 무엇을 왜 뺐는가.</summary>
/// <param name="Accepted">담을 것(원래 순서 유지 · 같은 대상은 한 번만).</param>
/// <param name="DraftExcluded">아직 저장되지 않아 뺀 수(<c>from_event_id</c> FK 가 없다).</param>
/// <param name="WrongKindExcluded">원본이 아니라 뺀 수(연결 · 조치 행).</param>
/// <param name="OverLimitExcluded">한 번에 담을 수 있는 수를 넘어 뺀 수.</param>
/// <param name="BlockReason">하나도 담을 수 없는 까닭. 담을 수 있으면 null.</param>
public sealed record TrayPlan(IReadOnlyList<ActionTrayCandidate> Accepted,
                              int DraftExcluded,
                              int WrongKindExcluded,
                              int OverLimitExcluded,
                              string? BlockReason)
{
    public bool CanQueue => BlockReason is null && Accepted.Count > 0;
}

/// <summary>
/// 목록 행 → <b>조치 트레이</b> 담기의 판정(순수 함수). 화면 없이 단위 테스트한다.
/// </summary>
/// <remarks>
/// <para>정본: all-windows-drag-wireframe.html L283(선택 → 조치 트레이) · L423(조치 생성 <b>N회</b> · 벌크 없음 → Draft + 진행률).</para>
/// <para>조치 생성은 이벤트 1건당 호출 1회다 — 그래서 드롭은 서버를 부르지 않고 Draft 만 쌓는다.</para>
/// </remarks>
public static class ActionTrayDrop
{
    /// <summary>드롭존 키.</summary>
    public const string ZoneKey = "events-action-tray";

    public const string KindDetection = "detection";
    public const string KindMalfunction = "malfunction";

    /// <summary>
    /// 한 번에 담을 수 있는 최대 건수. 조치 생성에 벌크가 없어 N건이면 N회이고, API 타임아웃(기본 10초)이 곱해진다
    /// — 상한을 두지 않으면 한 번의 드롭이 수백 초를 막는다(PRD R-01).
    /// </summary>
    public const int MaxPerDrop = 50;

    /// <summary>담을 수 있는 종류인가 — 원본(탐지 · 장애)만. 연결에는 <c>action_reported</c> 필드가 없다.</summary>
    public static bool IsReportableKind(string? kind)
        => kind is KindDetection or KindMalfunction;

    public static TrayPlan Plan(IEnumerable<ActionTrayCandidate> rows, bool canControl)
    {
        var list = rows?.ToList() ?? new List<ActionTrayCandidate>();

        if (!canControl)
            return new TrayPlan(System.Array.Empty<ActionTrayCandidate>(), 0, 0, 0, "조치보고 권한이 없습니다.");
        if (list.Count == 0)
            return new TrayPlan(System.Array.Empty<ActionTrayCandidate>(), 0, 0, 0, "담을 이벤트가 없습니다.");

        var wrongKind = list.Count(r => !IsReportableKind(r.Kind));
        var reportable = list.Where(r => IsReportableKind(r.Kind)).ToList();

        var drafts = reportable.Count(r => r.IsDraft);
        var saved = reportable.Where(r => !r.IsDraft).ToList();

        // 같은 이벤트를 두 번 고를 수는 없지만, 드로어 카드와 목록 행이 섞여 들어올 수 있다 — 대상 키로 한 번만 남긴다.
        var unique = new List<ActionTrayCandidate>();
        var seen = new HashSet<string>();
        foreach (var row in saved)
            if (seen.Add(row.TargetKey)) unique.Add(row);

        var overLimit = 0;
        if (unique.Count > MaxPerDrop)
        {
            overLimit = unique.Count - MaxPerDrop;
            unique = unique.Take(MaxPerDrop).ToList();
        }

        string? reason = null;
        if (unique.Count == 0)
            reason = wrongKind > 0 && reportable.Count == 0
                ? "이 목록의 행은 조치보고 원본이 아닙니다 — 탐지 · 장애에서 고르세요."
                : "저장되지 않은 이벤트입니다 — 저장 후 조치보고할 수 있습니다.";

        return new TrayPlan(unique, drafts, wrongKind, overLimit, reason);
    }

    /// <summary>상태 띠에 남길 한 줄.</summary>
    public static string ResultLine(TrayPlan plan)
    {
        if (!plan.CanQueue) return plan.BlockReason ?? "담을 것이 없습니다.";

        var parts = new List<string> { $"조치 트레이에 {plan.Accepted.Count}건을 담았습니다" };
        var already = plan.Accepted.Count(r => r.AlreadyReported);
        if (already > 0) parts.Add($"이미 조치가 있는 {already}건에는 한 건씩 더 쌓입니다");
        if (plan.DraftExcluded > 0) parts.Add($"저장 전 {plan.DraftExcluded}건은 뺐습니다");
        if (plan.WrongKindExcluded > 0) parts.Add($"원본이 아닌 {plan.WrongKindExcluded}건은 뺐습니다");
        if (plan.OverLimitExcluded > 0) parts.Add($"한 번에 {MaxPerDrop}건까지라 {plan.OverLimitExcluded}건은 남겼습니다");
        return string.Join(" · ", parts);
    }
}
