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

public class SpeakerDevicePanelViewModel : BaseDataGridMultiPanelViewModel<SpeakerDeviceViewModel>
                                        , IHandle<CallDeleteSpeakerDeviceProcessMessageModel>
{
    #region - Ctors -
    public SpeakerDevicePanelViewModel(IEventAggregator eventAggregator
                                      , ILogService log
                                      , IDeviceApiService apiService
                                      , SpeakerDeviceProvider deviceProvider
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
            Explain = "선택한 스피커 장비를 정말로 삭제하시겠습니까?",
            MessageModel = new CallDeleteSpeakerDeviceProcessMessageModel()
        });
    }

    public override void OnClickInsertButton(object sender, RoutedEventArgs e)
    {
        var vm = new SpeakerDeviceViewModel(new SpeakerDeviceModel());
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

            var currentList = _deviceProvider.OfType<SpeakerDeviceModel>().ToList();
            var serverList = await FetchSpeakersAsync(token);

            var insertList = currentList.Where(m => m.Id <= 0).ToList();
            var updateList = currentList
                .Where(m => m.Id > 0)
                .Join(serverList, m => m.Id, d => d.Id,
                    (m, d) => new { updated = m, original = d })
                .Where(p => !DeviceEquals(p.updated, p.original))
                .Select(p => p.updated)
                .ToList();

            foreach (var model in insertList)
                await CreateSpeakerAsync(model, token);

            foreach (var model in updateList)
                await UpdateSpeakerAsync(model, token);

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

    public override void OnSelectionChanged(IList<SpeakerDeviceViewModel> rows)
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
                foreach (ISpeakerDeviceViewModel newItem in e.NewItems)
                    _deviceProvider.Add((ISpeakerDeviceModel)newItem.Model);
                break;
            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems == null) return;
                foreach (ISpeakerDeviceViewModel oldItem in e.OldItems)
                    _deviceProvider.Remove((ISpeakerDeviceModel)oldItem.Model);
                break;
            case NotifyCollectionChangedAction.Reset:
                ViewModelProvider.Clear();
                foreach (var item in _deviceProvider.OfType<ISpeakerDeviceModel>())
                    ViewModelProvider.Add(new SpeakerDeviceViewModel(item));
                break;
        }
    }
    #endregion
    #region - Binding Methods -
    internal static bool DeviceEquals(ISpeakerDeviceModel a, ISpeakerDeviceModel b)
    {
        return a.DeviceNumber == b.DeviceNumber &&
        (a.DeviceGroups ?? new()).SequenceEqual(b.DeviceGroups ?? new()) &&
        a.DeviceName == b.DeviceName &&
        a.DeviceType == b.DeviceType &&
        a.Version == b.Version &&
        a.Status == b.Status &&
        a.SpeakerType == b.SpeakerType &&
        a.Description == b.Description &&
        a.IsEnable == b.IsEnable &&
        a.Location == b.Location &&
        a.Latitude == b.Latitude &&
        a.Longitude == b.Longitude;
    }
    #endregion
    #region - Helper Methods -
    private async Task<bool> CreateSpeakerAsync(SpeakerDeviceModel model, CancellationToken token = default)
    {
        try
        {
            var dto = model.ToSpeakerDeviceDto();
            var response = await _apiService.CreateSpeakerAsync(dto, token);
            if (response.Success)
            {
                _log?.Info($"Speaker created successfully: {response.Data?.Id}");
                return true;
            }
            _log?.Error($"Failed to create speaker: {response.Error?.Message}");
            return false;
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in CreateSpeakerAsync: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> UpdateSpeakerAsync(SpeakerDeviceModel model, CancellationToken token = default)
    {
        try
        {
            var dto = model.ToSpeakerDeviceDto();
            var response = await _apiService.UpdateSpeakerAsync(model.Id, dto, token);
            if (response.Success)
            {
                _log?.Info($"Speaker updated successfully: {model.Id}");
                return true;
            }
            _log?.Error($"Failed to update speaker {model.Id}: {response.Error?.Message}");
            return false;
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in UpdateSpeakerAsync: {ex.Message}");
            return false;
        }
    }

    private async Task<List<SpeakerDeviceModel>> FetchSpeakersAsync(CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetSpeakersAsync(page: 1, limit: 100, token: token);
            if (response.Success && response.Data != null)
                return response.Data.Select(dto => dto.ToSpeakerDeviceModel()).ToList();

            _log?.Error($"Failed to fetch speakers: {response.Error?.Message}");
            return new List<SpeakerDeviceModel>();
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in FetchSpeakersAsync: {ex.Message}");
            return new List<SpeakerDeviceModel>();
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

                await DispatcherService.BeginInvoke(() => ViewModelProvider.Clear());

                var items = _deviceProvider.OfType<ISpeakerDeviceModel>().ToList();
                const int batchSize = 50;

                for (int i = 0; i < items.Count; i += batchSize)
                {
                    if (cancellationToken.IsCancellationRequested)
                        throw new TaskCanceledException("Task was cancelled!");

                    var batch = items.Skip(i).Take(batchSize).ToList();
                    var startIndex = i;
                    await DispatcherService.BeginInvoke(() =>
                    {
                        foreach (var (item, idx) in batch.Select((item, idx) => (item, idx)))
                            ViewModelProvider.Add(new SpeakerDeviceViewModel((SpeakerDeviceModel)item) { Index = startIndex + idx + 1 });
                    });
                    await DispatcherService.BeginInvoke(() => { }, DispatcherPriority.Render);
                }

                await DispatcherService.BeginInvoke(() => NotifyOfPropertyChange(() => ViewModelProvider));

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
    public async Task HandleAsync(CallDeleteSpeakerDeviceProcessMessageModel message, CancellationToken cancellationToken)
    {
        await _eventAggregator.PublishOnCurrentThreadAsync(
            new OpenProgressPopupMessageModel(), cancellationToken);

        await Task.Run(async () =>
        {
            foreach (var item in SelectedItems.ToList())
            {
                var model = (ISpeakerDeviceModel)item.Model;
                if (model.Id <= 0)
                {
                    _deviceProvider.Remove(model);
                    continue;
                }
                var response = await _apiService.DeleteSpeakerAsync(model.Id, cancellationToken);
                if (!response.Success)
                    _log?.Error($"Failed to delete speaker {model.Id}: {response.Error?.Message}");
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
    private readonly SpeakerDeviceProvider _deviceProvider;
    private readonly IDeviceProviderService _deviceProviderService;
    #endregion
}
