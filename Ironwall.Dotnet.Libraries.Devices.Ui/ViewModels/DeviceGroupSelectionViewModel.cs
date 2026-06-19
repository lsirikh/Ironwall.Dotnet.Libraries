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
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels
{
    public class DeviceGroupSelectionViewModel : BasePanelViewModel
                                               , IHandle<CallRemoveDeviceFromGroupProcessMessageModel>
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
            _apiService = apiService;
            _deviceProvider = deviceProvider;
            AssignedDevices = new BindableCollection<DeviceAssignItemViewModel>();
            SelectedAssignedDevices = new BindableCollection<DeviceAssignItemViewModel>();
            SelectedAssignedDevices.CollectionChanged += (_, _) => NotifyOfPropertyChange(nameof(CanRemoveDeviceButton));
            _eventAggregator.SubscribeOnUIThread(this);
            _log?.Info($"[DeviceGroupSelectionVM] Constructed, subscribed to EventAggregator");
            RefreshAll();
            _ = LoadAssignedDevicesAsync();
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

        public async Task LoadAssignedDevicesAsync(CancellationToken token = default)
        {
            if (!IsSingleSelected) { AssignedDevices.Clear(); return; }
            try
            {
                DispatcherService.Invoke(() => IsVisible = false);
                await DispatcherService.BeginInvoke(() => { }, DispatcherPriority.Render);

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
                        Status = model.Status,
                        IsEnable = model.IsEnable,
                        IsChecked = true,
                        IsAlreadyAssigned = true,
                    });
                }
            }
            catch (Exception ex) { _log?.Error($"LoadAssignedDevicesAsync: {ex.Message}"); }
            finally
            {
                DispatcherService.Invoke(() => IsVisible = true);
            }
        }

        public async Task RefreshButton()
        {
            ReloadButtonEnable = false;
            NotifyOfPropertyChange(nameof(ReloadButtonEnable));
            try
            {
                await LoadAssignedDevicesAsync();
            }
            finally
            {
                ReloadButtonEnable = true;
                NotifyOfPropertyChange(nameof(ReloadButtonEnable));
            }
        }

        public async Task AddDeviceButton()
        {
            if (!IsSingleSelected) return;
            AddButtonEnable = false;
            NotifyOfPropertyChange(nameof(AddButtonEnable));

            try
            {
                var groupId = _selection[0].Id;
                var assignedIds = AssignedDevices.Select(d => d.Id);

                var dialog = new DeviceAssignDialogViewModel(_apiService, _deviceProvider, _log);
                dialog.Initialize(groupId, assignedIds);

                await _eventAggregator.PublishOnUIThreadAsync(
                    new OpenDeviceAssignDialogMessageModel
                    {
                        Dialog = dialog,
                        OnCompleted = () => _ = LoadAssignedDevicesAsync()
                    });
            }
            finally
            {
                AddButtonEnable = true;
                NotifyOfPropertyChange(nameof(AddButtonEnable));
            }
        }

        public async Task RemoveDeviceButton()
        {
            _log?.Info($"[DeviceGroupSelectionVM] RemoveDeviceButton called, IsSingleSelected={IsSingleSelected}, SelectedCount={SelectedAssignedDevices.Count}");
            if (!IsSingleSelected || SelectedAssignedDevices.Count == 0) return;
            _pendingRemoveTargets = SelectedAssignedDevices.ToList();
            _log?.Info($"[DeviceGroupSelectionVM] _pendingRemoveTargets saved: {_pendingRemoveTargets.Count} items (Ids: {string.Join(",", _pendingRemoveTargets.Select(d => d.Id))})");
            await _eventAggregator.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
            {
                Explain = $"선택한 장비 {_pendingRemoveTargets.Count}개를 그룹에서 제거하시겠습니까?",
                MessageModel = new CallRemoveDeviceFromGroupProcessMessageModel()
            });
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

        #region - IHandles -
        public async Task HandleAsync(CallRemoveDeviceFromGroupProcessMessageModel message, CancellationToken cancellationToken)
        {
            var targets = _pendingRemoveTargets;
            _pendingRemoveTargets = null;
            _log?.Info($"[DeviceGroupSelectionVM] HandleAsync entered, IsSingleSelected={IsSingleSelected}, targets={targets?.Count ?? -1}");
            if (!IsSingleSelected || targets == null || targets.Count == 0)
            {
                _log?.Warning($"[DeviceGroupSelectionVM] HandleAsync early return — no targets");
                return;
            }
            try
            {
                RemoveButtonEnable = false;
                NotifyOfPropertyChange(nameof(RemoveButtonEnable));
                DispatcherService.Invoke(() => IsVisible = false);
                await DispatcherService.BeginInvoke(() => { }, DispatcherPriority.Render);

                var groupId = _selection[0].Id;
                _log?.Info($"[DeviceGroupSelectionVM] Removing {targets.Count} devices from group {groupId}");
                foreach (var device in targets)
                {
                    _log?.Info($"[DeviceGroupSelectionVM] API RemoveDeviceFromGroupAsync(groupId={groupId}, deviceId={device.Id})");
                    var resp = await _apiService.RemoveDeviceFromGroupAsync(groupId, device.Id);
                    if (!resp.Success)
                        _log?.Warning($"RemoveDevice failed (Id={device.Id}): {resp.Message}");
                    else
                    {
                        _log?.Info($"[DeviceGroupSelectionVM] Removed device {device.Id} from group {groupId} OK");
                        var model = _deviceProvider.OfType<IBaseDeviceModel>().FirstOrDefault(m => m.Id == device.Id);
                        model?.DeviceGroups?.Remove(groupId);
                    }
                }
                SelectedAssignedDevices.Clear();
                await LoadAssignedDevicesAsync();
                _log?.Info($"[DeviceGroupSelectionVM] Remove completed, AssignedDevices.Count={AssignedDevices.Count}");
            }
            catch (Exception ex) { _log?.Error($"[DeviceGroupSelectionVM] RemoveDeviceButton exception: {ex.Message}"); }
            finally
            {
                RemoveButtonEnable = true;
                NotifyOfPropertyChange(nameof(RemoveButtonEnable));
                await _eventAggregator.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
            }
        }
        #endregion

        #region - Properties -
        public string? Name { get; set; }
        public string? Description { get; set; }
        public DeviceGroupPanelViewModel DevicePanelViewModel { get; }
        public BindableCollection<DeviceAssignItemViewModel> AssignedDevices { get; }
        public BindableCollection<DeviceAssignItemViewModel> SelectedAssignedDevices { get; }
        public bool IsSingleSelected => _selection.Count == 1;
        public bool CanRemoveDeviceButton => IsSingleSelected && SelectedAssignedDevices.Count > 0;
        public bool IsVisible { get; set; } = true;
        public bool AddButtonEnable { get; set; } = true;
        public bool RemoveButtonEnable { get; set; } = true;
        public bool ReloadButtonEnable { get; set; } = true;
        #endregion

        #region - Attributes -
        private readonly IList<DeviceGroupViewModel> _selection;
        private readonly IDeviceApiService _apiService;
        private readonly DeviceProvider _deviceProvider;
        private List<DeviceAssignItemViewModel>? _pendingRemoveTargets;
        #endregion
    }
}
