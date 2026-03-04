using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels
{
    public class DeviceGroupSelectionViewModel : BasePanelViewModel
    {
        #region - Ctors -
        public DeviceGroupSelectionViewModel(IList<DeviceGroupViewModel> selection
                                            , IEventAggregator eventAggregator
                                            , IDeviceApiService apiService
                                            , DeviceProvider deviceProvider
                                            , ILogService? log = null)
        {
            DevicePanelViewModel = IoC.Get<DeviceGroupPanelViewModel>();
            _selection = selection;
            _eventAggregator = eventAggregator;
            _apiService = apiService;
            _deviceProvider = deviceProvider;
            _log = log;
            AssignedDevices = new BindableCollection<DeviceAssignItemViewModel>();
            RefreshAll();
        }
        #endregion

        #region - Processes -
        public void ApplyButton()
        {
            foreach (var item in _selection)
            {
                item.Name = Name ?? item.Name;
                item.Description = Description ?? item.Description;
            }
        }

        public Task LoadAssignedDevicesAsync(CancellationToken token = default)
        {
            if (!IsSingleSelected) { AssignedDevices.Clear(); return Task.CompletedTask; }
            try
            {
                var groupId = _selection[0].Id;
                AssignedDevices.Clear();
                foreach (var model in _deviceProvider.OfType<IBaseDeviceModel>()
                    .Where(d => d.DeviceGroups != null && d.DeviceGroups.Contains(groupId)))
                {
                    AssignedDevices.Add(new DeviceAssignItemViewModel
                    {
                        Id = model.Id,
                        DeviceName = model.DeviceName,
                        DeviceType = model.DeviceType,
                        DeviceNumber = model.DeviceNumber,
                        IsChecked = true,
                        IsAlreadyAssigned = true,
                    });
                }
            }
            catch (Exception ex) { _log?.Error($"LoadAssignedDevicesAsync: {ex.Message}"); }
            return Task.CompletedTask;
        }

        public async Task AddDeviceButton()
        {
            if (!IsSingleSelected) return;
            var groupId = _selection[0].Id;
            var assignedIds = AssignedDevices.Select(d => d.Id);

            var dialog = new DeviceAssignDialogViewModel(_apiService, _deviceProvider, _log);
            dialog.Initialize(groupId, assignedIds);

            await _eventAggregator.PublishOnUIThreadAsync(
                new OpenDeviceAssignDialogMessageModel { Dialog = dialog });

            await LoadAssignedDevicesAsync();
        }

        public async Task RemoveDeviceButton(DeviceAssignItemViewModel? item)
        {
            if (!IsSingleSelected || item == null) return;
            try
            {
                var groupId = _selection[0].Id;
                var resp = await _apiService.RemoveDeviceFromGroupAsync(groupId, item.Id);
                if (!resp.Success) _log?.Warning($"RemoveDevice failed: {resp.Message}");
                await LoadAssignedDevicesAsync();
            }
            catch (Exception ex) { _log?.Error($"RemoveDeviceButton: {ex.Message}"); }
        }

        public void RefreshAll()
        {
            Name = CommonOrNullString(_selection, m => m.Name);
            Description = CommonOrNullString(_selection, m => m.Description);
            NotifyOfPropertyChange(nameof(IsSingleSelected));
        }

        private static T? CommonOrNullString<T>(IEnumerable<DeviceGroupViewModel> list, Func<IDeviceGroupModel, T> selector) where T : class?
        {
            if (!list.Any()) return null;
            var firstModel = list.FirstOrDefault()?.Model;
            if (firstModel == null) return null;
            T firstValue = selector(firstModel);
            return list
                .Select(x => x.Model)
                .All(m => EqualityComparer<T>.Default.Equals(selector(m), firstValue)) ? firstValue : null;
        }
        #endregion

        #region - Properties -
        public string? Name { get; set; }
        public string? Description { get; set; }
        public DeviceGroupPanelViewModel DevicePanelViewModel { get; }
        public BindableCollection<DeviceAssignItemViewModel> AssignedDevices { get; }
        public bool IsSingleSelected => _selection.Count == 1;
        #endregion

        #region - Attributes -
        private readonly IList<DeviceGroupViewModel> _selection;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeviceApiService _apiService;
        private readonly DeviceProvider _deviceProvider;
        private readonly ILogService? _log;
        #endregion
    }
}
