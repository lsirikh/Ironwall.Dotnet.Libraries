using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Groups;

/// <summary>상태 띠의 권한 그룹 칩 — 끌어 놓을 곳이자, 누르면 같은 일을 하는 버튼(키보드 · 자동화 경로).</summary>
public sealed class AccountGroupChipViewModel : Caliburn.Micro.PropertyChangedBase
{
    private int _userCount;

    public AccountGroupChipViewModel(int id, string name, int userCount)
    {
        Id = id;
        Name = name;
        _userCount = userCount;
    }

    public int Id { get; }
    public string Name { get; }

    public int UserCount
    {
        get => _userCount;
        set { _userCount = Math.Max(0, value); NotifyOfPropertyChange(); }
    }

    public override string ToString() => Name;
}

/// <summary>
/// 사용자 N명을 한 권한 그룹에 넣기 전에 세운 계획 — 무엇을 보내고 무엇을 뺐는지.
/// </summary>
/// <param name="GroupId">대상 그룹.</param>
/// <param name="GroupName">대상 그룹 이름.</param>
/// <param name="Targets">실제로 보낼 사용자(저장된 계정 · 아직 그 그룹이 아닌 계정).</param>
/// <param name="AlreadyIn">이미 그 그룹이라 뺀 수.</param>
/// <param name="Unsaved">아직 저장되지 않아(Id≤0) 뺀 수.</param>
/// <param name="BlockReason">보낼 수 없는 까닭. 보낼 수 있으면 null.</param>
public sealed record GroupAssignPlan(
    int GroupId,
    string GroupName,
    IReadOnlyList<AccountViewModel> Targets,
    int AlreadyIn,
    int Unsaved,
    string? BlockReason)
{
    public bool CanSend => BlockReason is null;
}

/// <summary>
/// 사용자 → 권한 그룹 끌어 놓기의 <b>판정(순수 함수)</b>. 화면 없이 단위 테스트한다.
/// (설계 정본 all-windows-drag-wireframe.html L339-L340 · L421 · L432)
/// </summary>
public static class UserGroupDrop
{
    public const string ZoneKey = AccountConsoleKeys.GroupZone;

    public static GroupAssignPlan Plan(int groupId, string groupName, IEnumerable<AccountViewModel>? users)
    {
        var list = users?.Where(u => u is not null).ToList() ?? new List<AccountViewModel>();

        if (groupId <= 0)
            return new GroupAssignPlan(groupId, groupName, Array.Empty<AccountViewModel>(), 0, 0, "아직 저장되지 않은 그룹입니다.");
        if (list.Count == 0)
            return new GroupAssignPlan(groupId, groupName, Array.Empty<AccountViewModel>(), 0, 0, "끌어 온 계정이 없습니다.");

        var unsaved = list.Count(u => u.Id <= 0);
        var saved = list.Where(u => u.Id > 0).ToList();
        var alreadyIn = saved.Count(u => u.GroupId == groupId);
        var targets = saved.Where(u => u.GroupId != groupId)
                           .GroupBy(u => u.Id)
                           .Select(g => g.First())
                           .ToList();

        string? reason = null;
        if (targets.Count == 0)
            reason = saved.Count == 0 ? "아직 저장되지 않은 계정입니다." : "이미 이 그룹에 들어 있습니다.";

        return new GroupAssignPlan(groupId, groupName, targets, alreadyIn, unsaved, reason);
    }

    /// <summary>드롭 직후 상태 띠에 남길 한 줄 — 호출이 N회로 번지므로 곧바로 보내지 않는다.</summary>
    public static string DropLine(GroupAssignPlan plan, int draftCount)
    {
        var parts = new List<string> { $"Draft {draftCount}건 — 호출은 {draftCount}회로 번지므로 [적용] 때 모아 보냅니다" };
        if (plan.AlreadyIn > 0) parts.Add($"이미 '{plan.GroupName}' 인 {plan.AlreadyIn}명은 담지 않았습니다");
        if (plan.Unsaved > 0) parts.Add($"저장 전 {plan.Unsaved}명은 뺐습니다");
        return string.Join(" · ", parts);
    }
}

/// <summary>되돌리기에 필요한 것 — 방금 바뀐 사용자와 그 이전 그룹.</summary>
/// <param name="UserId">사용자.</param>
/// <param name="Display">사람이 읽는 이름.</param>
/// <param name="PreviousGroupId">바뀌기 전 그룹(없었으면 null).</param>
public sealed record GroupAssignUndo(int UserId, string Display, int? PreviousGroupId);

/// <summary>
/// 권한 그룹 칩 드롭존의 처리기. 서버에 <b>벌크 배정 입구가 없어</b> 사용자 1명당 한 번씩 보낸다 —
/// 그래서 드롭은 곧바로 전송하지 않고 <see cref="DraftTrayViewModel"/> 에 쌓았다가 [적용] 때 모아 보낸다.
/// </summary>
/// <remarks>
/// 칩을 누르는 길(키보드 · 자동화 폴백)도 <see cref="Enqueue"/> 를 그대로 부른다 — 길이 둘이어도 경로는 하나다.
/// 호출 스레드: UI.
/// </remarks>
public sealed class UserGroupDropHandler : IDragDropHandler
{
    private readonly IAccountApiService _api;
    private readonly DraftTrayViewModel _tray;
    private readonly Func<bool> _canAssign;
    private readonly ILogService? _log;
    private readonly List<GroupAssignUndo> _undo = new();

    public UserGroupDropHandler(IAccountApiService api, DraftTrayViewModel tray, Func<bool> canAssign, ILogService? log = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _tray = tray ?? throw new ArgumentNullException(nameof(tray));
        _canAssign = canAssign ?? throw new ArgumentNullException(nameof(canAssign));
        _log = log;
    }

    /// <summary>무엇이 쌓였는지 · 무엇이 바뀌었는지 알린다(상태 띠 한 줄).</summary>
    public event Action<string>? Announced;

    /// <summary>되돌릴 것이 있는가(적용이 실제로 바꾼 뒤에만 참).</summary>
    public bool CanUndo => _undo.Count > 0;

    public bool CanDrop(DragPayload payload, DropTarget target)
    {
        if (!_canAssign() || _tray.IsApplying) return false;
        if (target.ZoneKey != UserGroupDrop.ZoneKey || target.ZoneData is not AccountGroupChipViewModel chip) return false;
        return UserGroupDrop.Plan(chip.Id, chip.Name, Accounts(payload.Items)).CanSend;
    }

    public void Drop(DragPayload payload, DropTarget target)
    {
        if (target.ZoneData is not AccountGroupChipViewModel chip) return;
        Enqueue(chip, Accounts(payload.Items));
    }

    /// <summary>드래그 · 칩 클릭이 함께 쓰는 입구. 쌓은 건수를 돌려준다.</summary>
    public int Enqueue(AccountGroupChipViewModel chip, IReadOnlyList<AccountViewModel> users)
    {
        if (chip is null) return 0;
        if (!_canAssign()) { Announce("권한이 없어 그룹을 바꿀 수 없습니다."); return 0; }
        if (_tray.IsApplying) { Announce("적용 중에는 더 담을 수 없습니다."); return 0; }

        var plan = UserGroupDrop.Plan(chip.Id, chip.Name, users);
        if (!plan.CanSend) { Announce(plan.BlockReason!); return 0; }

        foreach (var user in plan.Targets) _tray.Add(EntryFor(user, chip));

        Announce(UserGroupDrop.DropLine(plan, plan.Targets.Count));
        return plan.Targets.Count;
    }

    /// <summary>쌓아 둔 Draft 를 순차 전송한다 — 진행률과 부분 실패 4분류는 트레이가 센다.</summary>
    public async Task<DraftApplySummary> ApplyAsync(CancellationToken token = default)
    {
        _undo.Clear();
        var summary = await _tray.ApplyAsync(token).ConfigureAwait(true);
        Announce(summary.ToMessage() + (summary.Applied > 0 ? " (벌크 입구가 있으면 1회)" : string.Empty));
        return summary;
    }

    /// <summary>Draft 를 버린다 — 서버 호출 0.</summary>
    public void Revert()
    {
        _tray.Revert();
        Announce("Draft 를 버렸습니다 — 서버 호출 0");
    }

    /// <summary>방금 적용한 것을 되돌린다(역방향 N회). 되돌린 뒤에는 다시 되돌릴 것이 없다.</summary>
    public async Task UndoAsync(CancellationToken token = default)
    {
        if (_undo.Count == 0) { Announce("되돌릴 변경이 없습니다."); return; }

        var pending = _undo.ToList();
        _undo.Clear();

        int done = 0, failed = 0;
        foreach (var item in pending)
        {
            try
            {
                var response = await _api.AssignUserGroupAsync(item.UserId, item.PreviousGroupId, token).ConfigureAwait(true);
                if (response.Success) done++; else failed++;
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                failed++;
                _log?.Error($"[AccountGroupDrop] undo user={item.UserId}: {ex.Message}");
            }
        }
        Announce($"되돌렸습니다 — {done}회 호출" + (failed > 0 ? $" · 실패 {failed}" : string.Empty));
    }

    /// <summary>끌어 온 행에서 계정 뷰모델만 고른다.</summary>
    public static IReadOnlyList<AccountViewModel> Accounts(IEnumerable<object> rows)
        => rows.OfType<AccountViewModel>().ToList();

    private DraftEntry EntryFor(AccountViewModel user, AccountGroupChipViewModel chip)
    {
        var before = user.GroupId;
        return new DraftEntry(
            $"user:{user.Id}",
            "PUT users/{id} group_id",
            $"{user.Username} → {chip.Name}",
            async token =>
            {
                // 담은 뒤 다른 곳에서 바뀌었을 수 있다 — 보내기 직전에 다시 판정한다.
                if (user.GroupId == chip.Id) return DraftOutcome.Skipped;
                if (user.Id <= 0) return DraftOutcome.Missing;

                var response = await _api.AssignUserGroupAsync(user.Id, chip.Id, token).ConfigureAwait(true);
                if (!response.Success) return DraftOutcome.Failed;

                // 서버가 실제로 반영했는지 응답으로 확인한다(그룹 해제를 조용히 무시하는 판본이 있다).
                if (response.Data is not null && response.Data.GroupId != chip.Id) return DraftOutcome.Failed;

                user.GroupId = chip.Id;
                user.GroupText = chip.Name;
                chip.UserCount++;
                _undo.Add(new GroupAssignUndo(user.Id, user.Username, before));
                return DraftOutcome.Applied;
            });
    }

    private void Announce(string line) => Announced?.Invoke(line);
}
