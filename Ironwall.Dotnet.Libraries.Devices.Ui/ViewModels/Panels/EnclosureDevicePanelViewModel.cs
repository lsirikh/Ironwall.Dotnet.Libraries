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

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;

public class EnclosureDevicePanelViewModel : BaseDataGridMultiPanelViewModel<EnclosureDeviceViewModel>
                                           , IHandle<CallDeleteEnclosureDeviceProcessMessageModel>
{
    #region - Ctors -
    public EnclosureDevicePanelViewModel(IEventAggregator eventAggregator
                                        , ILogService log
                                        , IDeviceApiService apiService
                                        , EnclosureDeviceProvider deviceProvider
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
            Explain = "선택한 함체 장비를 정말로 삭제하시겠습니까?",
            MessageModel = new CallDeleteEnclosureDeviceProcessMessageModel()
        });
    }

    public override void OnClickInsertButton(object sender, RoutedEventArgs e)
    {
        var vm = new EnclosureDeviceViewModel(new EnclosureDeviceModel());
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

            var currentList = _deviceProvider.OfType<EnclosureDeviceModel>().ToList();
            var serverList = await FetchEnclosuresAsync(token);

            var insertList = currentList.Where(m => m.Id <= 0).ToList();
            var updateList = currentList
                .Where(m => m.Id > 0)
                .Join(serverList, m => m.Id, d => d.Id,
                    (m, d) => new { updated = m, original = d })
                .Where(p => !DeviceEquals(p.updated, p.original))
                .Select(p => p.updated)
                .ToList();

            foreach (var model in insertList)
                await CreateEnclosureAsync(model, token);

            foreach (var model in updateList)
                await UpdateEnclosureAsync(model, token);

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

    public override void OnSelectionChanged(IList<EnclosureDeviceViewModel> rows)
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
                foreach (IEnclosureDeviceViewModel newItem in e.NewItems)
                    _deviceProvider.Add((IEnclosureDeviceModel)newItem.Model);
                break;
            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems == null) return;
                foreach (IEnclosureDeviceViewModel oldItem in e.OldItems)
                    _deviceProvider.Remove((IEnclosureDeviceModel)oldItem.Model);
                break;
            case NotifyCollectionChangedAction.Reset:
                ViewModelProvider.Clear();
                foreach (var item in _deviceProvider.OfType<IEnclosureDeviceModel>())
                    ViewModelProvider.Add(new EnclosureDeviceViewModel(item));
                break;
        }
    }
    #endregion
    #region - Binding Methods -
    internal static bool DeviceEquals(IEnclosureDeviceModel a, IEnclosureDeviceModel b)
    {
        return a.DeviceNumber == b.DeviceNumber &&
        (a.DeviceGroups ?? new()).SequenceEqual(b.DeviceGroups ?? new()) &&
        a.DeviceName == b.DeviceName &&
        a.DeviceType == b.DeviceType &&
        a.Version == b.Version &&
        a.Status == b.Status &&
        a.DoorStatus == b.DoorStatus &&
        a.HeaterEnabled == b.HeaterEnabled &&
        a.FanEnabled == b.FanEnabled &&
        a.IsEnable == b.IsEnable &&
        a.Location == b.Location &&
        a.Latitude == b.Latitude &&
        a.Longitude == b.Longitude;
    }
    #endregion
    #region - Helper Methods -
    private async Task<bool> CreateEnclosureAsync(EnclosureDeviceModel model, CancellationToken token = default)
    {
        try
        {
            var dto = model.ToEnclosureDeviceDto();
            var response = await _apiService.CreateEnclosureAsync(dto, token);
            if (response.Success)
            {
                _log?.Info($"Enclosure created successfully: {response.Data?.Id}");
                return true;
            }
            _log?.Error($"Failed to create enclosure: {response.Error?.Message}");
            return false;
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in CreateEnclosureAsync: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> UpdateEnclosureAsync(EnclosureDeviceModel model, CancellationToken token = default)
    {
        try
        {
            var dto = model.ToEnclosureDeviceDto();
            var response = await _apiService.UpdateEnclosureAsync(model.Id, dto, token);
            if (response.Success)
            {
                _log?.Info($"Enclosure updated successfully: {model.Id}");
                return true;
            }
            _log?.Error($"Failed to update enclosure {model.Id}: {response.Error?.Message}");
            return false;
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in UpdateEnclosureAsync: {ex.Message}");
            return false;
        }
    }

    private async Task<List<EnclosureDeviceModel>> FetchEnclosuresAsync(CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetEnclosuresAsync(page: 1, limit: 100, token: token);
            if (response.Success && response.Data != null)
                return response.Data.Select(dto => dto.ToEnclosureDeviceModel()).ToList();

            _log?.Error($"Failed to fetch enclosures: {response.Error?.Message}");
            return new List<EnclosureDeviceModel>();
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in FetchEnclosuresAsync: {ex.Message}");
            return new List<EnclosureDeviceModel>();
        }
    }
    #endregion
    #region - Processes -
    private Task DataInitialize(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                IsVisible = false;

                // 캐시에서 바로 ViewModelProvider 구성 (API 호출 없음)
                ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;

                DispatcherService.Invoke(() =>
                {
                    ViewModelProvider.Clear();
                    foreach (var (item, index) in _deviceProvider
                        .OfType<IEnclosureDeviceModel>()
                        .Select((item, index) => (item, index)))
                    {
                        if (cancellationToken.IsCancellationRequested)
                            throw new TaskCanceledException("Task was cancelled!");
                        ViewModelProvider.Add(new EnclosureDeviceViewModel((EnclosureDeviceModel)item) { Index = index + 1 });
                    }
                    NotifyOfPropertyChange(() => ViewModelProvider);
                });

                ViewModelProvider.CollectionChanged += CollectionEntity_CollectionChanged;
                IsVisible = true;
            }
            catch (TaskCanceledException ex)
            {
                _log?.Warning($"Raised {nameof(TaskCanceledException)}({nameof(DataInitialize)}) : {ex.Message}");
            }
        });
    }
    #endregion
    #region - IHandles -
    public async Task HandleAsync(CallDeleteEnclosureDeviceProcessMessageModel message, CancellationToken cancellationToken)
    {
        await _eventAggregator.PublishOnCurrentThreadAsync(
            new OpenProgressPopupMessageModel(), cancellationToken);

        await Task.Run(async () =>
        {
            foreach (var item in SelectedItems.ToList())
            {
                var model = (IEnclosureDeviceModel)item.Model;
                var response = await _apiService.DeleteEnclosureAsync(model.Id, cancellationToken);
                if (!response.Success)
                    _log?.Error($"Failed to delete enclosure {model.Id}: {response.Error?.Message}");
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
    private readonly EnclosureDeviceProvider _deviceProvider;
    private readonly IDeviceProviderService _deviceProviderService;
    #endregion
}
