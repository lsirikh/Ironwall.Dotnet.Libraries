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
    }
    #endregion

    #region - Processes -
    public void Initialize(int groupId, IEnumerable<int> assignedDeviceIds)
    {
        _groupId = groupId;
        _assignedIds = new HashSet<int>(assignedDeviceIds);

        AllDevices.Clear();
        foreach (var model in _deviceProvider.OfType<IBaseDeviceModel>())
        {
            AllDevices.Add(new DeviceAssignItemViewModel
            {
                Id = model.Id,
                DeviceName = model.DeviceName,
                DeviceType = model.DeviceType,
                DeviceNumber = model.DeviceNumber,
                IsAlreadyAssigned = _assignedIds.Contains(model.Id),
                IsChecked = _assignedIds.Contains(model.Id),
            });
        }
    }

    public async Task AssignButton(CancellationToken token = default)
    {
        var newIds = AllDevices
            .Where(d => d.IsChecked && !_assignedIds.Contains(d.Id))
            .Select(d => d.Id)
            .ToList();

        if (newIds.Count == 0) { await TryCloseAsync(true); return; }

        try
        {
            var dto = new DeviceGroupAssignRequestDto { DeviceIds = newIds };
            var resp = await _apiService.AssignDevicesToGroupAsync(_groupId, dto, token);
            if (!resp.Success) _log?.Warning($"AssignDevicesToGroup failed: {resp.Message}");
        }
        catch (Exception ex) { _log?.Error($"AssignButton: {ex.Message}"); }

        await TryCloseAsync(true);
    }

    public async Task CancelButton()
    {
        await TryCloseAsync(false);
    }
    #endregion

    #region - Properties -
    public BindableCollection<DeviceAssignItemViewModel> AllDevices { get; }
    #endregion

    #region - Attributes -
    private int _groupId;
    private HashSet<int> _assignedIds = new();
    private readonly IDeviceApiService _apiService;
    private readonly DeviceProvider _deviceProvider;
    private readonly ILogService? _log;
    #endregion
}
