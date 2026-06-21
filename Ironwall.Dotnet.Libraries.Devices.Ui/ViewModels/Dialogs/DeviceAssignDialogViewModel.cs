using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs;

public class DeviceAssignDialogViewModel : Screen
{
    #region - Ctors -
    public DeviceAssignDialogViewModel(IDeviceApiService apiService
                                      , DeviceProvider deviceProvider
                                      , ILogService? log = null)
    {
        _apiService = apiService;
        _deviceProvider = deviceProvider;
        _log = log;
        AllDevices = new BindableCollection<DeviceAssignItemViewModel>();
        SelectedDevices = new BindableCollection<DeviceAssignItemViewModel>();
    }
    #endregion

    #region - Processes -
    public void Initialize(int groupId, IEnumerable<int> assignedDeviceIds)
    {
        _groupId = groupId;
        _assignedIds = new HashSet<int>(assignedDeviceIds);

        AllDevices.Clear();
        // (G3) 미저장 그룹(Id≤0)엔 후보를 채우지 않음 — ConfirmButton 가드와 동일 방어(🔴 강제계층, PRD 지정).
        if (groupId <= 0) { _log?.Warning($"[Assign] Initialize: 미저장 그룹(Id={groupId}) → 후보 미표시"); return; }

        // (진단) 공유 provider 상태 + 후보 산출 로깅 — 신규 장비가 provider에 들어왔는지/후보로 떴는지 확인용.
        var allModels = _deviceProvider.OfType<IBaseDeviceModel>().ToList();
        var candidates = allModels.Where(m => m.Id > 0 && !_assignedIds.Contains(m.Id)).ToList();   // (G4) Temp(미저장) 장비 후보 제외
        _log?.Info($"[Assign] Initialize group={groupId}: provider총={allModels.Count}, 미저장(Id≤0)={allModels.Count(m => m.Id <= 0)}, 이미배정={_assignedIds.Count}, 후보={candidates.Count} | 후보Id=[{string.Join(",", candidates.Select(m => m.Id))}] | 전체Id=[{string.Join(",", allModels.Select(m => m.Id))}]");

        foreach (var model in candidates)
        {
            AllDevices.Add(new DeviceAssignItemViewModel
            {
                Id = model.Id,
                DeviceName = model.DeviceName,
                DeviceType = model.DeviceType,
                DeviceNumber = model.DeviceNumber,
                Status = model.Status,
                IsEnable = model.IsEnable,
            });
        }
    }

    public async Task ConfirmButton(CancellationToken token = default)
    {
        // (G3) Temp 그룹(Id≤0)에는 배정 불가 — 2차 방어(groups/0/devices 호출 차단).
        if (_groupId <= 0) { _log?.Warning("ConfirmButton: 미저장 그룹(Id≤0)에 장비 배정 시도 차단"); await TryCloseAsync(false); return; }

        // (G5) Temp(미저장) 장비 제외.
        var selectedIds = SelectedDevices.Select(d => d.Id).ToList();
        var newIds = selectedIds.Where(id => id > 0).ToList();
        int draftExcluded = selectedIds.Count - newIds.Count;
        _log?.Info($"[Assign] ConfirmButton group={_groupId}: 선택={selectedIds.Count} [{string.Join(",", selectedIds)}], 전송={newIds.Count} [{string.Join(",", newIds)}], 미저장제외={draftExcluded}");

        if (newIds.Count == 0) { await TryCloseAsync(true); return; }

        try
        {
            var dto = new DeviceGroupAssignRequestDto { DeviceIds = newIds };
            var resp = await _apiService.AssignDevicesToGroupAsync(_groupId, dto, token);
            _log?.Info($"[Assign] 응답 group={_groupId}: Success={resp.Success}, assigned=[{string.Join(",", resp.Data?.AssignedDeviceIds ?? new List<int>())}], skipped=[{string.Join(",", resp.Data?.SkippedDeviceIds ?? new List<int>())}], msg=\"{resp.Data?.Message ?? resp.Message}\"");
            if (!resp.Success)
            {
                _log?.Warning($"AssignDevicesToGroup failed: {resp.Message}");
            }
            else
            {
                // (M4) verify-after-success — 서버가 실제 배정한 ID(AssignedDeviceIds) 기준으로만 로컬 갱신.
                var assigned = resp.Data?.AssignedDeviceIds ?? newIds;
                foreach (var model in _deviceProvider.OfType<IBaseDeviceModel>()
                             .Where(m => assigned.Contains(m.Id)))
                {
                    model.DeviceGroups ??= new List<int>();
                    if (!model.DeviceGroups.Contains(_groupId))
                        model.DeviceGroups.Add(_groupId);
                }
            }
        }
        catch (Exception ex) { _log?.Error($"ConfirmButton: {ex.Message}"); }

        await TryCloseAsync(true);
    }

    public async Task CancelButton()
    {
        await TryCloseAsync(false);
    }
    #endregion

    #region - Properties -
    public BindableCollection<DeviceAssignItemViewModel> AllDevices { get; }
    public BindableCollection<DeviceAssignItemViewModel> SelectedDevices { get; }
    #endregion

    #region - Attributes -
    private int _groupId;
    private HashSet<int> _assignedIds = new();
    private readonly IDeviceApiService _apiService;
    private readonly DeviceProvider _deviceProvider;
    private readonly ILogService? _log;
    #endregion
}
