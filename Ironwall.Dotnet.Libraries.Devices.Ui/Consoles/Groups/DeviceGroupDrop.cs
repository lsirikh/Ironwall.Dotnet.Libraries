using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Dialogs;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Groups;

/// <summary>장비를 그룹에 넣기 전에 세운 계획 — 무엇을 보내고 무엇을 뺐는지.</summary>
/// <param name="GroupId">대상 그룹.</param>
/// <param name="DeviceIds">서버에 보낼 장비 Id(저장된 것 · 아직 그 그룹에 없는 것).</param>
/// <param name="DraftExcluded">아직 저장되지 않아(Id≤0) 뺀 수.</param>
/// <param name="AlreadyIn">이미 그 그룹에 있어 뺀 수.</param>
/// <param name="BlockReason">보낼 수 없는 까닭. 보낼 수 있으면 null.</param>
public sealed record GroupDropPlan(int GroupId, IReadOnlyList<int> DeviceIds, int DraftExcluded, int AlreadyIn, string? BlockReason)
{
    public bool CanSend => BlockReason is null;
}

/// <summary>되돌리기에 필요한 것 — 방금 서버가 실제로 넣은 장비들.</summary>
public sealed record GroupDropUndo(int GroupId, string GroupName, IReadOnlyList<int> DeviceIds);

/// <summary>그룹 넣기 · 되돌리기 한 번의 결과.</summary>
/// <param name="Line">상태 띠에 남길 한 줄.</param>
/// <param name="Undo">되돌릴 수 있으면 그 정보.</param>
/// <param name="GroupId">바뀐 그룹(바뀐 것이 없으면 0).</param>
/// <param name="DeviceIds">소속이 바뀐 장비들 — 화면은 이 행들의 그룹 글자와 그 그룹의 개수를 다시 그린다.</param>
/// <param name="Delta">그 그룹의 장비 수 변화(넣으면 +, 되돌리면 −).</param>
public sealed record GroupDropResult(string Line, GroupDropUndo? Undo, int GroupId, IReadOnlyList<int> DeviceIds, int Delta);

/// <summary>
/// 장비 → 그룹 끌어 놓기의 판정(순수 함수). 화면 없이 단위 테스트한다.
/// </summary>
public static class DeviceGroupDrop
{
    public const string ZoneKey = "device-group";

    public static GroupDropPlan Plan(int groupId, IEnumerable<IBaseDeviceModel> devices)
    {
        var list = devices?.Where(d => d is not null).ToList() ?? new List<IBaseDeviceModel>();

        if (groupId <= 0) return new GroupDropPlan(groupId, Array.Empty<int>(), 0, 0, "아직 저장되지 않은 그룹입니다 — 그룹을 먼저 등록하세요.");
        if (list.Count == 0) return new GroupDropPlan(groupId, Array.Empty<int>(), 0, 0, "끌어 온 장비가 없습니다.");

        var drafts = list.Count(d => d.Id <= 0);
        var saved = list.Where(d => d.Id > 0).ToList();
        var already = saved.Count(d => d.DeviceGroups?.Contains(groupId) == true);
        var ids = saved.Where(d => d.DeviceGroups?.Contains(groupId) != true).Select(d => d.Id).Distinct().ToList();

        string? reason = null;
        if (ids.Count == 0)
            reason = saved.Count == 0 ? "아직 저장되지 않은 장비입니다 — 장비를 먼저 등록하세요." : "이미 이 그룹에 들어 있습니다.";

        return new GroupDropPlan(groupId, ids, drafts, already, reason);
    }

    /// <summary>상태 띠에 남길 한 줄.</summary>
    public static string ResultLine(string groupName, GroupDropPlan plan, IReadOnlyCollection<int> assigned, IReadOnlyCollection<int> skipped)
    {
        var parts = new List<string> { $"'{groupName}'에 {assigned.Count}대를 넣었습니다" };
        if (skipped.Count > 0) parts.Add($"{skipped.Count}대는 넣지 못했습니다");
        if (plan.AlreadyIn > 0) parts.Add($"{plan.AlreadyIn}대는 이미 들어 있어 건너뛰었습니다");
        if (plan.DraftExcluded > 0) parts.Add($"저장 전 {plan.DraftExcluded}대는 뺐습니다");
        return string.Join(" · ", parts);
    }
}

/// <summary>
/// 그룹 칩 드롭존의 처리기. 그룹 하나에 <b>호출 한 번</b>(배치 엔드포인트) — 장비 수만큼 부르지 않는다.
/// </summary>
/// <remarks>
/// 실패하면 로컬 모델을 건드리지 않는다(서버가 실제로 넣었다고 답한 Id 만 반영). 되돌리기는 방금 넣은 것만 일괄 제거 한 번.
/// 키보드 폴백("그룹에 넣기" 메뉴)도 <see cref="AssignAsync"/> 를 그대로 부른다 — 길이 둘이어도 전송 경로는 하나다.
/// </remarks>
public sealed class DeviceGroupDropHandler : IDragDropHandler
{
    private readonly IDeviceApiService _api;
    private readonly Func<IEnumerable<IBaseDeviceModel>> _allDevices;
    private readonly ILogService? _log;
    private readonly IEventAggregator? _eventAggregator;

    /// <param name="eventAggregator">소속이 실제로 바뀌면 <see cref="DeviceGroupMembershipChangedMessage"/> 를 알릴 곳.
    /// 없으면(단위 테스트 · 라이브 하네스) 알리지 않는다 — 지도의 구역선 조회표는 이 알림으로만 새 소속을 안다.</param>
    public DeviceGroupDropHandler(IDeviceApiService api, Func<IEnumerable<IBaseDeviceModel>> allDevices, ILogService? log = null, IEventAggregator? eventAggregator = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _allDevices = allDevices ?? throw new ArgumentNullException(nameof(allDevices));
        _log = log;
        _eventAggregator = eventAggregator;
    }

    /// <summary>끝났다(성공이든 아니든) — 무엇이 바뀌었는지 담아 알린다.</summary>
    public event Action<GroupDropResult>? Completed;

    public bool IsBusy { get; private set; }

    public bool CanDrop(DragPayload payload, DropTarget target)
    {
        if (IsBusy || target.ZoneKey != DeviceGroupDrop.ZoneKey || target.ZoneData is not DeviceGroupViewModel group) return false;
        return DeviceGroupDrop.Plan(group.Id, ModelsOf(payload.Items)).CanSend;
    }

    public async void Drop(DragPayload payload, DropTarget target)
    {
        if (target.ZoneData is not DeviceGroupViewModel group) return;
        try { await AssignAsync(group.Id, group.Name, ModelsOf(payload.Items)).ConfigureAwait(true); }
        catch (Exception ex) { _log?.Error($"[GroupDrop] {ex.Message}"); }
    }

    public async Task<string> AssignAsync(int groupId, string groupName, IReadOnlyList<IBaseDeviceModel> devices, CancellationToken token = default)
    {
        var plan = DeviceGroupDrop.Plan(groupId, devices);
        if (!plan.CanSend) return Finish(plan.BlockReason!, null);
        if (IsBusy) return Finish("앞선 그룹 넣기가 아직 끝나지 않았습니다. 잠시 후 다시 하세요.", null);

        IsBusy = true;
        try
        {
            var response = await _api.AssignDevicesToGroupAsync(groupId, new DeviceGroupAssignRequestDto { DeviceIds = plan.DeviceIds.ToList() }, token).ConfigureAwait(true);
            if (!response.Success)
                // 오류 봉투에서는 Message 가 비고 까닭은 Error 에 있다 — 예전엔 "넣지 못했다 — " 로 끝나 사람이 까닭을 몰랐다
                // (라이브 하네스 dl.2c: 부대 범위 밖 장비 422 "그룹의 부대(1) 또는 그 예하 부대의 장비만 …" 이 사라졌다).
                return Finish($"'{groupName}'에 넣지 못했습니다 — {ApiErrorTextHelper.Resolve(response.Error, response.Message, "서버가 받지 않았습니다")}", null);

            // 서버가 실제로 넣었다고 답한 것만 로컬에 반영한다(응답에 없으면 보낸 것 전부).
            var assigned = response.Data?.AssignedDeviceIds ?? plan.DeviceIds.ToList();
            var skipped = response.Data?.SkippedDeviceIds ?? new List<int>();
            Reflect(groupId, assigned, add: true);
            if (assigned.Count > 0) await AnnounceAsync(groupId).ConfigureAwait(true);

            var undo = assigned.Count > 0 ? new GroupDropUndo(groupId, groupName, assigned.ToList()) : null;
            return Finish(DeviceGroupDrop.ResultLine(groupName, plan, assigned, skipped), undo, groupId, assigned.ToList(), assigned.Count);
        }
        catch (OperationCanceledException) { return Finish("그룹 넣기를 취소했습니다.", null); }
        catch (Exception ex)
        {
            _log?.Error($"[GroupDrop] group={groupId}: {ex.Message}");
            return Finish($"'{groupName}'에 넣지 못했습니다 — 서버에 연결하지 못했습니다.", null);
        }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// 방금 넣은 것을 뺀다 — 서버가 한 번에 <b>100대</b>까지만 받으므로(<c>DeviceUnassignRequest.device_ids max_length=100</c>)
    /// 배정 창과 같은 크기(<see cref="AssignDelta.ChunkRemovals"/>)로 나눠 보낸다.
    /// </summary>
    /// <remarks>
    /// 넘겨 보내면 422 로 <b>한 대도</b> 빠지지 않았다(라이브 실측 2026-09-24, 101대 → 422 CONSTRAINT).
    /// 일부 묶음만 실패하면 된 것만 반영하고, 남은 것으로 되돌리기 정보를 다시 돌려준다(다시 누르면 나머지만 보낸다).
    /// </remarks>
    public async Task<string> UndoAsync(GroupDropUndo undo, CancellationToken token = default)
    {
        if (undo is null || undo.DeviceIds.Count == 0) return Finish("되돌릴 것이 없습니다.", null);
        if (IsBusy) return Finish("앞선 그룹 넣기가 아직 끝나지 않았습니다. 잠시 후 다시 하세요.", undo);

        IsBusy = true;
        var done = new List<int>();
        string? failure = null;
        try
        {
            foreach (var chunk in AssignDelta.ChunkRemovals(undo.DeviceIds))
            {
                var response = await _api.RemoveDevicesFromGroupAsync(undo.GroupId, new DeviceGroupAssignRequestDto { DeviceIds = chunk.ToList() }, token).ConfigureAwait(true);
                if (!response.Success) { failure = ApiErrorTextHelper.Resolve(response.Error, response.Message, "서버가 받지 않았습니다"); continue; }
                done.AddRange(chunk);   // removed · skipped(이미 아님) · not_found 모두 "이제 그 그룹에 없다"
            }
        }
        catch (OperationCanceledException) { failure ??= "취소했습니다"; }
        catch (Exception ex)
        {
            _log?.Error($"[GroupDrop] undo group={undo.GroupId}: {ex.Message}");
            failure ??= "서버에 연결하지 못했습니다";
        }
        finally { IsBusy = false; }

        if (done.Count > 0)
        {
            Reflect(undo.GroupId, done, add: false);
            await AnnounceAsync(undo.GroupId).ConfigureAwait(true);
        }
        if (failure is null)
            return Finish($"'{undo.GroupName}'에 넣은 {undo.DeviceIds.Count}대를 되돌렸습니다.", null, undo.GroupId, undo.DeviceIds, -undo.DeviceIds.Count);

        var remaining = undo.DeviceIds.Except(done).ToList();
        var left = remaining.Count > 0 ? new GroupDropUndo(undo.GroupId, undo.GroupName, remaining) : null;
        return done.Count == 0
            ? Finish($"되돌리지 못했습니다 — {failure}", undo)
            : Finish($"'{undo.GroupName}'에서 {done.Count}대만 되돌렸고 {remaining.Count}대는 남았습니다 — {failure}", left, undo.GroupId, done, -done.Count);
    }

    /// <summary>끌어 온 행(뷰모델)에서 모델을 꺼낸다.</summary>
    public static IReadOnlyList<IBaseDeviceModel> ModelsOf(IEnumerable<object> rows)
        => rows.Select(row => row as IBaseDeviceModel ?? row.GetType().GetProperty("Model")?.GetValue(row) as IBaseDeviceModel)
               .Where(model => model is not null)
               .Select(model => model!)
               .ToList();

    private void Reflect(int groupId, IReadOnlyCollection<int> deviceIds, bool add)
    {
        var ids = deviceIds.ToHashSet();
        // 서버가 해 준 변화다 — 모델과 소속 기준선을 함께 옮겨, 다음 상세 저장이 이것을 되보내지(덮어쓰지) 않게 한다.
        foreach (var model in _allDevices().Where(m => ids.Contains(m.Id)))
            GroupMembershipBaseline.ApplyConfirmed(model, groupId, add);
    }

    /// <summary>
    /// 소속이 바뀌었다고 알린다 — 지도가 그 그룹의 구역선만 다시 등록(비면 해제)한다.
    /// 알림 실패는 넣기 · 되돌리기의 결과를 바꾸지 않는다(서버와 로컬 모델은 이미 맞다).
    /// </summary>
    private async Task AnnounceAsync(int groupId)
    {
        if (_eventAggregator is null || DeviceGroupMembershipChangedMessage.For(new[] { groupId }) is not { } message) return;
        try { await _eventAggregator.PublishOnCurrentThreadAsync(message).ConfigureAwait(true); }
        catch (Exception ex) { _log?.Error($"[GroupDrop] 소속 변경 알림 실패 group={groupId}: {ex.Message}"); }
    }

    private string Finish(string line, GroupDropUndo? undo, int groupId = 0, IReadOnlyList<int>? deviceIds = null, int delta = 0)
    {
        Completed?.Invoke(new GroupDropResult(line, undo, groupId, deviceIds ?? Array.Empty<int>(), delta));
        return line;
    }
}
