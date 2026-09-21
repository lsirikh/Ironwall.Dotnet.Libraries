using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Dialogs;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs;

/// <summary>왼쪽 · 오른쪽 어느 목록을 가리키는가.</summary>
public enum AssignSide
{
    Available,
    Assigned,
}

/// <summary>옮긴 뒤 이 행들을 저쪽 목록에서 다시 고르라는 부탁 — 고른 것이 드롭 한 번에 흩어지지 않게.</summary>
public sealed class AssignSelectionRequest : EventArgs
{
    public AssignSelectionRequest(AssignSide side, IReadOnlyList<DeviceAssignItemViewModel> items)
    {
        Side = side;
        Items = items;
    }

    public AssignSide Side { get; }
    public IReadOnlyList<DeviceAssignItemViewModel> Items { get; }
}

/// <summary>
/// 장비 배정 창 — <b>T4 · L 720</b>. 왼쪽이 후보, 오른쪽이 이 그룹에 든 장비이고 둘 사이를 <b>끌어서</b> 옮긴다.
/// </summary>
/// <remarks>
/// <para>정본: 스토리보드 #h-dlg L1436("장비 추가(배정) 600×600 → L 720 — 좌→우 이동형 두 목록이라 넓게") ·
/// 드래그 와이어프레임 L392-396("좌 → 우 끌어 배정 · 우 → 좌 끌어 해제 · <b>▶ ◀ 버튼도 그대로 둡니다</b> — 키보드·자동화 경로입니다") ·
/// 드래그 카탈로그 L424(장비 배정 창: 서버 호출은 <b>저장 때 1회</b>, 폴백은 ▶ ◀).</para>
/// <para><b>창 안은 전부 Draft</b>다. 끌어 옮겨도 서버는 조용하다. [저장] 한 번에 <b>바뀐 것만</b>
/// (<see cref="AssignDelta.Plan"/>) 방향마다 배치 한 번씩 — 최대 두 번 부른다. 장비마다 부르지 않는다.</para>
/// <para>보내기 직전에 그룹을 다시 읽어 견준다. 그 사이 다른 곳에서 바뀌었으면 <b>아무것도 보내지 않고</b> 다시 읽기를 권한다.</para>
/// <para>▶ ◀ 는 드롭과 <b>같은 메서드</b>를 부른다 — 둘이 갈라지면 폴백이 조용히 다른 일을 한다.</para>
/// <para>호출 스레드: UI. 서버 호출만 비동기로 나갔다 돌아온다.</para>
/// </remarks>
public class DeviceAssignDialogViewModel : Screen, IDragDropHandler
{
    #region - Ctors -
    public DeviceAssignDialogViewModel(IDeviceApiService apiService
                                      , Func<IEnumerable<IBaseDeviceModel>> deviceSource
                                      , Func<CancellationToken, Task>? refreshAsync = null
                                      , ILogService? log = null)
    {
        _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
        _deviceSource = deviceSource ?? throw new ArgumentNullException(nameof(deviceSource));
        _refreshAsync = refreshAsync;
        _log = log;

        Available = new BindableCollection<DeviceAssignItemViewModel>();
        Assigned = new BindableCollection<DeviceAssignItemViewModel>();
        SelectedDevices = new BindableCollection<DeviceAssignItemViewModel>();
        SelectedAssignedDevices = new BindableCollection<DeviceAssignItemViewModel>();
        SelectedDevices.CollectionChanged += (_, _) => OnSelectionChanged();
        SelectedAssignedDevices.CollectionChanged += (_, _) => OnSelectionChanged();
        DisplayName = "장비 배정";
    }
    #endregion

    #region - Properties -
    /// <summary>왼쪽 — 이 그룹에 들어 있지 않은 장비.</summary>
    public BindableCollection<DeviceAssignItemViewModel> Available { get; }

    /// <summary>오른쪽 — 이 그룹에 든 장비(Draft 포함).</summary>
    public BindableCollection<DeviceAssignItemViewModel> Assigned { get; }

    /// <summary>옛 이름 — 호스트 래퍼(<c>DeviceAssignPropertyDialogView</c>)가 아직 이 이름으로 묶여 있다. <see cref="Available"/> 과 같은 목록이다.</summary>
    public BindableCollection<DeviceAssignItemViewModel> AllDevices => Available;

    /// <summary>왼쪽에서 고른 행. 이름은 옛 계약 그대로다(호스트의 <c>DeviceAssignSelectedItemsBehavior</c> 가 여기에 쓴다).</summary>
    public BindableCollection<DeviceAssignItemViewModel> SelectedDevices { get; }

    /// <summary>오른쪽에서 고른 행.</summary>
    public BindableCollection<DeviceAssignItemViewModel> SelectedAssignedDevices { get; }

    public string GroupName { get; private set; } = string.Empty;
    public int GroupId => _groupId;

    /// <summary>머리 아래 한 줄 — 무엇에 대한 창인가.</summary>
    public string Kind => _groupId > 0 ? $"그룹 '{GroupName}' · 후보 {Available.Count} · 배정 {Assigned.Count}" : "아직 저장되지 않은 그룹";

    /// <summary>버튼 줄 왼쪽 글. 거절한 까닭도 여기로 말한다 — 말없이 아무 일도 안 하지 않는다.</summary>
    public string Message
    {
        get => _message;
        private set { if (_message == value) return; _message = value ?? string.Empty; NotifyOfPropertyChange(); }
    }

    /// <summary>그 글의 무게 — 거절 · 부분 실패는 회색 안내가 아니라 눈에 띄어야 한다.</summary>
    public DialogMessageSeverity MessageSeverity
    {
        get => _severity;
        private set { if (_severity == value) return; _severity = value; NotifyOfPropertyChange(); }
    }

    /// <summary>비었을 때 그 칸에 적을 말 — 까닭에 따라 다르다.</summary>
    public string AvailableEmptyText => _groupId <= 0
        ? "그룹을 먼저 저장해야 후보가 뜹니다"
        : "넣을 수 있는 장비가 없습니다";

    public string AssignedEmptyText => _groupId <= 0
        ? "저장되지 않은 그룹에는 배정할 수 없습니다"
        : "아직 배정된 장비가 없습니다 — 왼쪽에서 끌어 놓거나 ▶ 를 누르세요";

    private void Say(string message, DialogMessageSeverity severity = DialogMessageSeverity.Normal)
    {
        Message = message;
        MessageSeverity = severity;
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(CanSave));
            NotifyOfPropertyChange(nameof(IsEditable));
        }
    }

    /// <summary>끌기 · 버튼을 받을 수 있는가 — 보내는 동안은 얼린다.</summary>
    public bool IsEditable => !_isBusy;

    public bool IsDirty => CurrentPlan.HasChanges;
    public bool CanSave => !_isBusy && CurrentPlan.CanSend;
    public bool CanRevert => !_isBusy && CurrentPlan.HasChanges;

    /// <summary>지금 보낼 참인 차분.</summary>
    public AssignPlan CurrentPlan => AssignDelta.Plan(_groupId, _baseline, Assigned.Select(i => i.Id));

    public bool CanAssignSelected => IsEditable && SelectedDevices.Count > 0;
    public bool CanUnassignSelected => IsEditable && SelectedAssignedDevices.Count > 0;
    public bool CanAssignAll => IsEditable && Available.Count > 0;
    public bool CanUnassignAll => IsEditable && Assigned.Count > 0;

    /// <summary>빈 목록에는 까닭 · 다음 할 일을 적는다 — 텅 빈 칸을 내놓지 않는다.</summary>
    public bool IsAvailableEmpty => Available.Count == 0;
    public bool IsAssignedEmpty => Assigned.Count == 0;

    /// <summary>저장까지 마쳤는가 — 부른 쪽이 목록을 다시 읽을지 판단한다.</summary>
    public bool Saved { get; private set; }

    /// <summary>옮긴 행을 저쪽 목록에서 다시 골라 달라.</summary>
    public event EventHandler<AssignSelectionRequest>? SelectionRequested;
    #endregion

    #region - Setup -
    /// <summary>창을 채운다. <paramref name="assignedIds"/> 가 창을 연 순간의 소속이고 <b>차분의 기준선</b>이다.</summary>
    public void Initialize(int groupId, string? groupName, IEnumerable<int>? assignedIds = null)
    {
        _groupId = groupId;
        GroupName = groupName ?? string.Empty;

        var models = _deviceSource().Where(m => m is not null).ToList();
        var assigned = assignedIds is not null
            ? new HashSet<int>(assignedIds)
            : new HashSet<int>(models.Where(m => m.DeviceGroups?.Contains(groupId) == true).Select(m => m.Id));

        _baseline = assigned.Where(id => id > 0).ToHashSet();

        Available.Clear();
        Assigned.Clear();
        if (groupId <= 0)
        {
            // 미저장 그룹에는 아무것도 배정할 수 없다 — 후보도 띄우지 않고 까닭을 적는다.
            Say("아직 저장되지 않은 그룹입니다 — 그룹을 먼저 저장하세요.", DialogMessageSeverity.Warning);
            NotifyAll();
            return;
        }

        foreach (var model in models.OrderBy(m => m.DeviceNumber).ThenBy(m => m.Id))
        {
            var item = ToItem(model);
            if (assigned.Contains(model.Id)) Assigned.Add(item);
            else if (model.Id > 0) Available.Add(item);      // 저장되지 않은 장비는 후보가 아니다 — 서버가 모르는 Id 다
        }

        Say(AssignDelta.Summary(CurrentPlan));
        NotifyAll();
    }

    private static DeviceAssignItemViewModel ToItem(IBaseDeviceModel model) => new()
    {
        Id = model.Id,
        DeviceName = model.DeviceName,
        DeviceType = model.DeviceType,
        CategoryLabel = DeviceAxesMapper.CategoryOf(model).ToString().ToLowerInvariant(),
        TypeAxisLabel = string.IsNullOrWhiteSpace(model.TypeAxisCode)
            ? model.DeviceType.ToString()
            : TypeAxisPanelSupport.DescribeFor(model),
        DeviceNumber = model.DeviceNumber,
        Status = model.Status,
        IsEnable = model.IsEnable,
        IsAlreadyAssigned = model.DeviceGroups?.Count > 0,
    };
    #endregion

    #region - Selection -
    /// <summary>그쪽 목록에서 고른 것을 이 값으로 바꾼다. 목록 자체를 갈아 끼우지 않는다 — 묶여 있는 컬렉션이다.</summary>
    public void SetSelection(AssignSide side, IEnumerable<DeviceAssignItemViewModel>? items)
    {
        var target = side == AssignSide.Available ? SelectedDevices : SelectedAssignedDevices;
        var next = items?.Where(i => i is not null).ToList() ?? new List<DeviceAssignItemViewModel>();
        if (target.Count == next.Count && target.SequenceEqual(next)) return;

        foreach (var gone in target.Except(next).ToList()) target.Remove(gone);
        foreach (var added in next.Where(i => !target.Contains(i))) target.Add(added);
    }

    public IReadOnlyList<DeviceAssignItemViewModel> SelectionOf(AssignSide side)
        => side == AssignSide.Available ? SelectedDevices : SelectedAssignedDevices;

    private void OnSelectionChanged()
    {
        NotifyOfPropertyChange(nameof(CanAssignSelected));
        NotifyOfPropertyChange(nameof(CanUnassignSelected));
    }
    #endregion

    #region - Moves (buttons and drops call the same one) -
    /// <summary>▶ · 왼쪽 목록 Enter · 왼쪽 → 오른쪽 드롭.</summary>
    public void AssignSelected() => Move(SelectedDevices.ToList(), AssignSide.Assigned);

    /// <summary>◀ · 오른쪽 목록 Delete · 오른쪽 → 왼쪽 드롭.</summary>
    public void UnassignSelected() => Move(SelectedAssignedDevices.ToList(), AssignSide.Available);

    /// <summary>▶▶</summary>
    public void AssignAll() => Move(Available.ToList(), AssignSide.Assigned);

    /// <summary>◀◀</summary>
    public void UnassignAll() => Move(Assigned.ToList(), AssignSide.Available);

    /// <summary>창을 연 순간으로 되돌린다 — 서버는 건드리지 않는다.</summary>
    public void RevertAll()
    {
        if (_isBusy) return;
        Initialize(_groupId, GroupName, _baseline);
        Say("창을 연 상태로 되돌렸다");
    }

    private void Move(IReadOnlyList<DeviceAssignItemViewModel> items, AssignSide target)
    {
        if (_isBusy || items.Count == 0 || _groupId <= 0) return;

        var from = target == AssignSide.Assigned ? Available : Assigned;
        var to = target == AssignSide.Assigned ? Assigned : Available;

        var moved = new List<DeviceAssignItemViewModel>();
        foreach (var item in items.ToList())
        {
            // 바인딩된 목록을 Clear() 하고 다시 채우지 않는다 — 선택도 스크롤도 같이 날아간다.
            if (!from.Remove(item)) continue;
            InsertSorted(to, item);
            moved.Add(item);
        }

        if (moved.Count == 0) return;

        // 옮긴 것은 저쪽에서 그대로 골라 둔다 — 여러 건을 옮기고 곧바로 되돌릴 수 있게.
        SetSelection(target, moved);
        SetSelection(target == AssignSide.Assigned ? AssignSide.Available : AssignSide.Assigned, Array.Empty<DeviceAssignItemViewModel>());
        SelectionRequested?.Invoke(this, new AssignSelectionRequest(target, moved));

        Say(AssignDelta.Summary(CurrentPlan), CurrentPlan.HasChanges ? DialogMessageSeverity.Info : DialogMessageSeverity.Normal);
        NotifyAll();
    }

    private static void InsertSorted(ObservableCollection<DeviceAssignItemViewModel> list, DeviceAssignItemViewModel item)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (Compare(list[i], item) <= 0) continue;
            list.Insert(i, item);
            return;
        }
        list.Add(item);
    }

    private static int Compare(DeviceAssignItemViewModel a, DeviceAssignItemViewModel b)
    {
        var byNumber = a.DeviceNumber.CompareTo(b.DeviceNumber);
        return byNumber != 0 ? byNumber : a.Id.CompareTo(b.Id);
    }
    #endregion

    #region - Drag and drop -
    /// <summary>끄는 동안 자주 불린다 — 가볍게, 서버 호출 없이. 거절한 까닭은 버튼 줄에 남긴다.</summary>
    public bool CanDrop(DragPayload payload, DropTarget target)
    {
        var reason = RefuseReason(payload, target);
        if (reason is not null) { Say(reason, DialogMessageSeverity.Warning); return false; }

        Say(target.ZoneKey == AssignDelta.AssignedZone
            ? $"놓으면 {Countable(payload)}대를 이 그룹에 넣는다(아직 보내지 않는다)"
            : $"놓으면 {Countable(payload)}대를 이 그룹에서 뺀다(아직 보내지 않는다)", DialogMessageSeverity.Info);
        return true;
    }

    public void Drop(DragPayload payload, DropTarget target)
    {
        if (RefuseReason(payload, target) is { } reason) { Say(reason, DialogMessageSeverity.Warning); return; }

        var items = payload.Items.OfType<DeviceAssignItemViewModel>().ToList();
        Move(items, target.ZoneKey == AssignDelta.AssignedZone ? AssignSide.Assigned : AssignSide.Available);
    }

    /// <summary>놓을 수 없는 까닭 — 드롭존마다 다른 말을 한다. 놓을 수 있으면 null.</summary>
    private string? RefuseReason(DragPayload payload, DropTarget target)
    {
        if (_isBusy) return "보내는 중입니다 — 끝난 뒤에 옮기세요.";
        if (_groupId <= 0) return "아직 저장되지 않은 그룹입니다 — 그룹을 먼저 저장하세요.";

        var items = payload.Items.OfType<DeviceAssignItemViewModel>().ToList();
        if (items.Count == 0) return "장비 행이 아닙니다.";

        var toAssigned = target.ZoneKey == AssignDelta.AssignedZone;
        if (!toAssigned && target.ZoneKey != AssignDelta.AvailableZone) return "여기에는 놓을 수 없습니다.";

        var source = toAssigned ? Available : Assigned;
        var destination = toAssigned ? Assigned : Available;

        if (items.All(destination.Contains))
            return toAssigned ? "이미 이 그룹에 들어 있습니다." : "이미 후보 쪽에 있습니다.";
        if (!items.Any(source.Contains)) return "이 목록에서 끌어온 행이 아닙니다.";
        if (toAssigned && items.Where(source.Contains).All(i => i.Id <= 0))
            return "아직 저장되지 않은 장비입니다 — 장비를 먼저 등록하세요.";

        return null;
    }

    private static int Countable(DragPayload payload) => payload.Items.OfType<DeviceAssignItemViewModel>().Count();
    #endregion

    #region - Save -
    /// <summary>
    /// [저장] — 다시 읽어 견주고, 어긋나지 않았으면 방향마다 배치 한 번씩 보낸다.
    /// </summary>
    public async Task SaveAsync(CancellationToken token = default)
    {
        if (_isBusy) return;

        var plan = CurrentPlan;
        if (!plan.CanSend) { Say(plan.BlockReason ?? "보낼 것이 없다", DialogMessageSeverity.Warning); return; }

        IsBusy = true;
        NotifyAll();
        try
        {
            // ① 보내기 전에 다시 읽는다 — 그 사이 다른 창이 이 그룹을 건드렸을 수 있다.
            if (_refreshAsync is not null)
            {
                try { await _refreshAsync(token).ConfigureAwait(true); }
                catch (OperationCanceledException) { Say("취소했다 — 아무것도 보내지 않았다", DialogMessageSeverity.Warning); return; }
                catch (Exception ex)
                {
                    // 사람에게는 까닭만, 날 예외 글은 기록에만.
                    _log?.Error($"[Assign] 재조회 실패: {ex.Message}");
                    Say("그룹을 다시 읽지 못해 보내지 않았다 — 잠시 뒤 다시 시도하세요.", DialogMessageSeverity.Critical);
                    return;
                }

                var server = _deviceSource().Where(m => m?.DeviceGroups?.Contains(_groupId) == true).Select(m => m.Id);
                if (AssignDelta.Drift(_baseline, server) is { } drift)
                {
                    Say(drift, DialogMessageSeverity.Warning);
                    return;
                }
            }

            // ② 방향마다 한 번씩. 장비마다 부르지 않는다 — 10초 타임아웃이 곱해진다.
            var add = plan.Added.Count > 0 ? await SendAsync(plan.Added, assign: true, token).ConfigureAwait(true) : null;
            var remove = plan.Removed.Count > 0 ? await SendAsync(plan.Removed, assign: false, token).ConfigureAwait(true) : null;

            Say(AssignDelta.ResultLine(GroupName, add, remove),
                AssignDelta.ShouldStayOpen(add, remove) ? DialogMessageSeverity.Warning : DialogMessageSeverity.Normal);

            // ③ 서버가 실제로 한 것만 기준선에 반영한다 — 보냈다는 사실은 성공이 아니다.
            if (add is { Failed: false }) foreach (var id in _lastAssigned) _baseline.Add(id);
            if (remove is { Failed: false }) foreach (var id in _lastRemoved) _baseline.Remove(id);

            if (AssignDelta.ShouldStayOpen(add, remove))
            {
                // 남은 것이 보이도록 창을 열어 둔다. 목록은 실제 상태로 다시 세운다.
                RebuildFromBaseline();
                return;
            }

            Saved = true;
            await TryCloseAsync(true).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
            NotifyAll();
        }
    }

    private async Task<AssignLegOutcome> SendAsync(IReadOnlyList<int> ids, bool assign, CancellationToken token)
    {
        var dto = new DeviceGroupAssignRequestDto { DeviceIds = ids.ToList() };
        try
        {
            if (assign)
            {
                var response = await _apiService.AssignDevicesToGroupAsync(_groupId, dto, token).ConfigureAwait(true);
                var applied = response.Success ? response.Data?.AssignedDeviceIds ?? new List<int>() : new List<int>();
                _lastAssigned = applied.ToList();
                _log?.Info($"[Assign] group={_groupId} 넣기 보냄={ids.Count} 처리={applied.Count} 성공={response.Success}");
                return new AssignLegOutcome(ids.Count, applied.Count, response.Data?.SkippedDeviceIds?.Count ?? 0, !response.Success);
            }

            var removeResponse = await _apiService.RemoveDevicesFromGroupAsync(_groupId, dto, token).ConfigureAwait(true);
            var removed = removeResponse.Success ? removeResponse.Data?.RemovedDeviceIds ?? new List<int>() : new List<int>();
            _lastRemoved = removed.ToList();
            _log?.Info($"[Assign] group={_groupId} 빼기 보냄={ids.Count} 처리={removed.Count} 성공={removeResponse.Success}");
            return new AssignLegOutcome(ids.Count, removed.Count, removeResponse.Data?.SkippedDeviceIds?.Count ?? 0, !removeResponse.Success);
        }
        catch (OperationCanceledException)
        {
            if (assign) _lastAssigned = new List<int>(); else _lastRemoved = new List<int>();
            return new AssignLegOutcome(ids.Count, 0, 0, Failed: true);
        }
        catch (Exception ex)
        {
            _log?.Error($"[Assign] {(assign ? "넣기" : "빼기")} 실패: {ex.Message}");
            if (assign) _lastAssigned = new List<int>(); else _lastRemoved = new List<int>();
            return new AssignLegOutcome(ids.Count, 0, 0, Failed: true);
        }
    }

    /// <summary>부분 실패 뒤 — 화면을 서버가 아는 상태(기준선)로 다시 세운다.</summary>
    private void RebuildFromBaseline()
    {
        var keep = Message;
        Initialize(_groupId, GroupName, _baseline);
        Message = keep;
    }

    public Task CancelAsync() => TryCloseAsync(false);

    /// <summary>
    /// 옛 이름 — 호스트 래퍼가 아직 이것을 부른다. 왼쪽에서 고른 것을 오른쪽으로 옮긴 다음 저장한다
    /// (옛 창은 고르기만 하면 확인이 곧 배정이었다). 새 창에서는 ▶ 와 [저장] 이 나뉜다.
    /// </summary>
    public async Task ConfirmButton(CancellationToken token = default)
    {
        if (SelectedDevices.Count > 0) AssignSelected();
        if (!CurrentPlan.HasChanges) { await TryCloseAsync(true).ConfigureAwait(true); return; }
        await SaveAsync(token).ConfigureAwait(true);
    }

    /// <summary>옛 이름 — 호스트 래퍼의 취소.</summary>
    public Task CancelButton() => CancelAsync();
    #endregion

    #region - Notify -
    private void NotifyAll()
    {
        NotifyOfPropertyChange(nameof(Kind));
        NotifyOfPropertyChange(nameof(AvailableEmptyText));
        NotifyOfPropertyChange(nameof(AssignedEmptyText));
        NotifyOfPropertyChange(nameof(IsDirty));
        NotifyOfPropertyChange(nameof(CanSave));
        NotifyOfPropertyChange(nameof(CanRevert));
        NotifyOfPropertyChange(nameof(CanAssignSelected));
        NotifyOfPropertyChange(nameof(CanUnassignSelected));
        NotifyOfPropertyChange(nameof(CanAssignAll));
        NotifyOfPropertyChange(nameof(CanUnassignAll));
        NotifyOfPropertyChange(nameof(IsAvailableEmpty));
        NotifyOfPropertyChange(nameof(IsAssignedEmpty));
        NotifyOfPropertyChange(nameof(CurrentPlan));
    }
    #endregion

    #region - Attributes -
    private readonly IDeviceApiService _apiService;
    private readonly Func<IEnumerable<IBaseDeviceModel>> _deviceSource;
    private readonly Func<CancellationToken, Task>? _refreshAsync;
    private readonly ILogService? _log;

    private int _groupId;
    private HashSet<int> _baseline = new();
    private List<int> _lastAssigned = new();
    private List<int> _lastRemoved = new();
    private string _message = string.Empty;
    private DialogMessageSeverity _severity = DialogMessageSeverity.Normal;
    private bool _isBusy;
    #endregion
}
