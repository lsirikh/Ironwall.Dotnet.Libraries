using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Specialized;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;

public class LampDevicePanelViewModel : BaseDataGridMultiPanelViewModel<LampDeviceViewModel>
                                     , IHandle<CallDeleteLampDeviceProcessMessageModel>
{
    #region - Ctors -
    public LampDevicePanelViewModel(IEventAggregator eventAggregator
                                   , ILogService log
                                   , IDeviceApiService apiService
                                   , LampDeviceProvider deviceProvider
                                   , IDeviceProviderService deviceProviderService
                                   ) : base(eventAggregator, log)
    {
        _apiService = apiService;
        _deviceProvider = deviceProvider;
        _deviceProviderService = deviceProviderService;
    }
    #endregion
    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        _pCancellationTokenSource = new CancellationTokenSource();
        await DataInitialize(_pCancellationTokenSource!.Token).ConfigureAwait(false);
    }

    protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;
        if (_pCancellationTokenSource != null && !_pCancellationTokenSource!.IsCancellationRequested)
        {
            _pCancellationTokenSource.Cancel();
            _pCancellationTokenSource.Dispose();
        }
        return base.OnDeactivateAsync(close, cancellationToken);
    }

    public override async void OnClickDeleteButton(object sender, RoutedEventArgs e)
    {
        if (SelectedItemCount == 0) return;
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Explain = "선택한 경고등 장비를 정말로 삭제하시겠습니까?",
            MessageModel = new CallDeleteLampDeviceProcessMessageModel()
        });
    }

    public override void OnClickInsertButton(object sender, RoutedEventArgs e)
    {
        var vm = new LampDeviceViewModel(new LampDeviceModel());
        ViewModelProvider.Add(vm);
    }

    public override async void OnClickReloadButton(object sender, RoutedEventArgs e)
    {
        if (!await _processGate.WaitAsync(0)) return;
        try
        {
            ReloadButtonEnable = false;
            if (_pCancellationTokenSource != null && !_pCancellationTokenSource!.IsCancellationRequested)
            {
                _pCancellationTokenSource.Cancel();
                _pCancellationTokenSource.Dispose();
            }
            _pCancellationTokenSource = new CancellationTokenSource();
            await _deviceProviderService.FetchAllDevicesAsync(_pCancellationTokenSource!.Token);
            await DataInitialize(_pCancellationTokenSource!.Token);
            await Task.Delay(2000, _pCancellationTokenSource.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log?.Error(ex.Message); }
        finally
        {
            ReloadButtonEnable = true;
            UpdateAction?.Invoke();
            _processGate.Release();
        }
    }

    public override async void OnClickSaveButton(object sender, RoutedEventArgs e)
    {
        if (!await _processGate.WaitAsync(0)) return;
        try
        {
            SaveButtonEnable = false;
            if (_pCancellationTokenSource != null && !_pCancellationTokenSource.IsCancellationRequested)
            {
                _pCancellationTokenSource.Cancel();
                _pCancellationTokenSource.Dispose();
            }
            _pCancellationTokenSource = new CancellationTokenSource();
            var token = _pCancellationTokenSource.Token;

            var currentList = _deviceProvider.OfType<LampDeviceModel>().ToList();
            var serverList = await FetchLampsAsync(token);

            var insertList = currentList.Where(m => m.Id <= 0).ToList();
            var updateList = currentList
                .Where(m => m.Id > 0)
                .Join(serverList, m => m.Id, d => d.Id,
                    (m, d) => new { updated = m, original = d })
                .Where(p => !DeviceEquals(p.updated, p.original))
                .Select(p => p.updated)
                .ToList();

            foreach (var model in insertList)
                await CreateLampAsync(model, token);

            foreach (var model in updateList)
                await UpdateLampAsync(model, token);

            await _deviceProviderService.FetchAllDevicesAsync(_pCancellationTokenSource.Token);
            await DataInitialize(_pCancellationTokenSource.Token);
            await Task.Delay(2000, token);
        }
        catch (TaskCanceledException ex) { _log?.Warning(ex.Message); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log?.Error(ex.Message); }
        finally
        {
            UpdateAction?.Invoke();
            SaveButtonEnable = true;
            _processGate.Release();
        }
    }

    public override void OnSelectionChanged(IList<LampDeviceViewModel> rows)
    {
        SelectedItems = rows;
        rows.ToList().ForEach(entity => entity.IsSelected = true);
        NotifyOfPropertyChange(() => SelectedItems);
    }

    private void CollectionEntity_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems == null) return;
                foreach (ILampDeviceViewModel newItem in e.NewItems)
                    _deviceProvider.Add((ILampDeviceModel)newItem.Model);
                break;
            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems == null) return;
                foreach (ILampDeviceViewModel oldItem in e.OldItems)
                    _deviceProvider.Remove((ILampDeviceModel)oldItem.Model);
                break;
            case NotifyCollectionChangedAction.Reset:
                ViewModelProvider.Clear();
                foreach (var item in _deviceProvider.OfType<ILampDeviceModel>())
                    ViewModelProvider.Add(new LampDeviceViewModel(item));
                break;
        }
    }
    #endregion
    #region - Binding Methods -
    internal static bool DeviceEquals(ILampDeviceModel a, ILampDeviceModel b)
    {
        return a.DeviceNumber == b.DeviceNumber &&
        (a.DeviceGroups ?? new()).SequenceEqual(b.DeviceGroups ?? new()) &&
        a.DeviceName == b.DeviceName &&
        a.DeviceType == b.DeviceType &&
        a.Version == b.Version &&
        a.Status == b.Status &&
        a.IpAddress == b.IpAddress &&
        a.IpPort == b.IpPort &&
        a.UserName == b.UserName &&
        a.UserPassword == b.UserPassword &&
        a.Description == b.Description &&
        a.IsEnable == b.IsEnable &&
        a.Location == b.Location &&
        a.Latitude == b.Latitude &&
        a.Longitude == b.Longitude;
    }
    #endregion
    #region - Helper Methods -
    private async Task<bool> CreateLampAsync(LampDeviceModel model, CancellationToken token = default)
    {
        try
        {
            var dto = model.ToLampDeviceDto();
            var response = await _apiService.CreateLampAsync(dto, token);
            if (response.Success)
            {
                _log?.Info($"Lamp created successfully: {response.Data?.Id}");
                return true;
            }
            _log?.Error($"Failed to create lamp: {response.Error?.Message}");
            return false;
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in CreateLampAsync: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> UpdateLampAsync(LampDeviceModel model, CancellationToken token = default)
    {
        try
        {
            var dto = model.ToLampDeviceDto();
            var response = await _apiService.UpdateLampAsync(model.Id, dto, token);
            if (response.Success)
            {
                _log?.Info($"Lamp updated successfully: {model.Id}");
                return true;
            }
            _log?.Error($"Failed to update lamp {model.Id}: {response.Error?.Message}");
            return false;
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in UpdateLampAsync: {ex.Message}");
            return false;
        }
    }

    private async Task<List<LampDeviceModel>> FetchLampsAsync(CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetLampsAsync(page: 1, limit: 100, token: token);
            if (response.Success && response.Data != null)
                return response.Data.Select(dto => dto.ToLampDeviceModel()).ToList();

            _log?.Error($"Failed to fetch lamps: {response.Error?.Message}");
            return new List<LampDeviceModel>();
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in FetchLampsAsync: {ex.Message}");
            return new List<LampDeviceModel>();
        }
    }
    #endregion
    #region - Processes -
    private Task DataInitialize(CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            try
            {
                DispatcherService.Invoke(() => IsVisible = false);
                await DispatcherService.BeginInvoke(() => { }, DispatcherPriority.Render);

                ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;

                DispatcherService.Invoke(() =>
                {
                    ViewModelProvider.Clear();
                    foreach (var (item, index) in _deviceProvider
                        .OfType<ILampDeviceModel>()
                        .Select((item, index) => (item, index)))
                    {
                        if (cancellationToken.IsCancellationRequested)
                            throw new TaskCanceledException("Task was cancelled!");
                        ViewModelProvider.Add(new LampDeviceViewModel((LampDeviceModel)item) { Index = index + 1 });
                    }
                    NotifyOfPropertyChange(() => ViewModelProvider);
                });

                ViewModelProvider.CollectionChanged += CollectionEntity_CollectionChanged;
                DispatcherService.Invoke(() => IsVisible = true);
            }
            catch (TaskCanceledException ex)
            {
                _log?.Warning($"Raised {nameof(TaskCanceledException)}({nameof(DataInitialize)}) : {ex.Message}");
            }
        });
    }
    #endregion
    #region - IHandles -
    public async Task HandleAsync(CallDeleteLampDeviceProcessMessageModel message, CancellationToken cancellationToken)
    {
        await _eventAggregator.PublishOnCurrentThreadAsync(
            new OpenProgressPopupMessageModel(), cancellationToken);

        await Task.Run(async () =>
        {
            foreach (var item in SelectedItems.ToList())
            {
                var model = (ILampDeviceModel)item.Model;
                var response = await _apiService.DeleteLampAsync(model.Id, cancellationToken);
                if (!response.Success)
                    _log?.Error($"Failed to delete lamp {model.Id}: {response.Error?.Message}");
            }
        }, cancellationToken);

        if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        await _deviceProviderService.FetchAllDevicesAsync(_cancellationTokenSource!.Token);
        await DataInitialize(_cancellationTokenSource!.Token).ConfigureAwait(false);
        UpdateAction?.Invoke();

        await _eventAggregator.PublishOnCurrentThreadAsync(
            new ClosePopupMessageModel(), cancellationToken);
    }
    #endregion
    #region - Properties -
    public event System.Action? UpdateAction;
    #endregion
    #region - Attributes -
    private readonly IDeviceApiService _apiService;
    private readonly LampDeviceProvider _deviceProvider;
    private readonly IDeviceProviderService _deviceProviderService;
    #endregion
}
