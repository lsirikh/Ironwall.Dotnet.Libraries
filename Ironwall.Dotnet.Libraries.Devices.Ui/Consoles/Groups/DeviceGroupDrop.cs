using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
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

/// <summary>
/// 장비 → 그룹 끌어 놓기의 판정(순수 함수). 화면 없이 단위 테스트한다.
/// </summary>
public static class DeviceGroupDrop
{
    public const string ZoneKey = "device-group";

    public static GroupDropPlan Plan(int groupId, IEnumerable<IBaseDeviceModel> devices)
    {
        var list = devices?.Where(d => d is not null).ToList() ?? new List<IBaseDeviceModel>();

        if (groupId <= 0) return new GroupDropPlan(groupId, Array.Empty<int>(), 0, 0, "아직 저장되지 않은 그룹이다 — 그룹을 먼저 저장한다");
        if (list.Count == 0) return new GroupDropPlan(groupId, Array.Empty<int>(), 0, 0, "끌어 온 장비가 없다");

        var drafts = list.Count(d => d.Id <= 0);
        var saved = list.Where(d => d.Id > 0).ToList();
        var already = saved.Count(d => d.DeviceGroups?.Contains(groupId) == true);
        var ids = saved.Where(d => d.DeviceGroups?.Contains(groupId) != true).Select(d => d.Id).Distinct().ToList();

        string? reason = null;
        if (ids.Count == 0)
            reason = saved.Count == 0 ? "아직 저장되지 않은 장비다 — 장비를 먼저 등록한다" : "이미 이 그룹에 들어 있다";

        return new GroupDropPlan(groupId, ids, drafts, already, reason);
    }

    /// <summary>상태 띠에 남길 한 줄.</summary>
    public static string ResultLine(string groupName, GroupDropPlan plan, IReadOnlyCollection<int> assigned, IReadOnlyCollection<int> skipped)
    {
        var parts = new List<string> { $"'{groupName}' 에 {assigned.Count}대를 넣었다" };
        if (skipped.Count > 0) parts.Add($"서버가 {skipped.Count}대를 건너뛰었다");
        if (plan.AlreadyIn > 0) parts.Add($"이미 들어 있던 {plan.AlreadyIn}대는 보내지 않았다");
        if (plan.DraftExcluded > 0) parts.Add($"저장 전 {plan.DraftExcluded}대는 뺐다");
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

    public DeviceGroupDropHandler(IDeviceApiService api, Func<IEnumerable<IBaseDeviceModel>> allDevices, ILogService? log = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _allDevices = allDevices ?? throw new ArgumentNullException(nameof(allDevices));
        _log = log;
    }

    /// <summary>끝났다 — 상태 띠에 남길 한 줄과, 되돌릴 수 있으면 그 정보.</summary>
    public event Action<string, GroupDropUndo?>? Completed;

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
        if (IsBusy) return Finish("앞선 그룹 넣기가 아직 끝나지 않았다", null);

        IsBusy = true;
        try
        {
            var response = await _api.AssignDevicesToGroupAsync(groupId, new DeviceGroupAssignRequestDto { DeviceIds = plan.DeviceIds.ToList() }, token).ConfigureAwait(true);
            if (!response.Success)
                return Finish($"'{groupName}' 에 넣지 못했다 — {response.Message}", null);

            // 서버가 실제로 넣었다고 답한 것만 로컬에 반영한다(응답에 없으면 보낸 것 전부).
            var assigned = response.Data?.AssignedDeviceIds ?? plan.DeviceIds.ToList();
            var skipped = response.Data?.SkippedDeviceIds ?? new List<int>();
            Reflect(groupId, assigned, add: true);

            var undo = assigned.Count > 0 ? new GroupDropUndo(groupId, groupName, assigned.ToList()) : null;
            return Finish(DeviceGroupDrop.ResultLine(groupName, plan, assigned, skipped), undo);
        }
        catch (OperationCanceledException) { return Finish("그룹 넣기를 취소했다", null); }
        catch (Exception ex)
        {
            _log?.Error($"[GroupDrop] group={groupId}: {ex.Message}");
            return Finish($"'{groupName}' 에 넣지 못했다 — 서버에 닿지 못했다", null);
        }
        finally { IsBusy = false; }
    }

    /// <summary>방금 넣은 것을 뺀다(일괄 제거 한 번).</summary>
    public async Task<string> UndoAsync(GroupDropUndo undo, CancellationToken token = default)
    {
        if (undo is null || undo.DeviceIds.Count == 0) return Finish("되돌릴 것이 없다", null);
        if (IsBusy) return Finish("앞선 그룹 넣기가 아직 끝나지 않았다", undo);

        IsBusy = true;
        try
        {
            var response = await _api.RemoveDevicesFromGroupAsync(undo.GroupId, new DeviceGroupAssignRequestDto { DeviceIds = undo.DeviceIds.ToList() }, token).ConfigureAwait(true);
            if (!response.Success) return Finish($"되돌리지 못했다 — {response.Message}", undo);

            Reflect(undo.GroupId, undo.DeviceIds, add: false);
            return Finish($"'{undo.GroupName}' 에 넣은 {undo.DeviceIds.Count}대를 되돌렸다", null);
        }
        catch (OperationCanceledException) { return Finish("되돌리기를 취소했다", undo); }
        catch (Exception ex)
        {
            _log?.Error($"[GroupDrop] undo group={undo.GroupId}: {ex.Message}");
            return Finish("되돌리지 못했다 — 서버에 닿지 못했다", undo);
        }
        finally { IsBusy = false; }
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
        foreach (var model in _allDevices().Where(m => ids.Contains(m.Id)))
        {
            model.DeviceGroups ??= new List<int>();
            if (add) { if (!model.DeviceGroups.Contains(groupId)) model.DeviceGroups.Add(groupId); }
            else model.DeviceGroups.Remove(groupId);
        }
    }

    private string Finish(string line, GroupDropUndo? undo)
    {
        Completed?.Invoke(line, undo);
        return line;
    }
}
