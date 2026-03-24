using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Specialized;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;

public class DeviceGroupPanelViewModel : BaseDataGridMultiPanelViewModel<DeviceGroupViewModel>
                                       , IHandle<CallDeleteDeviceGroupProcessMessageModel>
{
    #region - Ctors -
    public DeviceGroupPanelViewModel(IEventAggregator eventAggregator
                                      , ILogService log
                                      , IDeviceApiService apiService
                                      , DeviceGroupProvider deviceGroupProvider
                                      ) : base(eventAggregator, log)
    {
        _apiService = apiService;
        _deviceGroupProvider = deviceGroupProvider;
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
            Explain = "선택한 장비그룹을 삭제하시겠습니까?",
            MessageModel = new CallDeleteDeviceGroupProcessMessageModel()
        });
    }

    public override void OnClickInsertButton(object sender, RoutedEventArgs e)
    {
        var vm = new DeviceGroupViewModel(new DeviceGroupModel());
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

            var currentList = _deviceGroupProvider.OfType<DeviceGroupModel>().ToList();
            var serverList = await FetchDeviceGroupsAsync(token);

            // Insert: 로컬에서 새로 추가된 항목 (Id <= 0)
            foreach (var model in currentList.Where(m => m.Id <= 0))
                await CreateDeviceGroupHelper(model, token);

            // Update: 서버에 존재하고 내용이 변경된 항목만
            foreach (var model in currentList.Where(m => m.Id > 0))
            {
                var server = serverList.FirstOrDefault(s => s.Id == model.Id);
                if (server != null && !DeviceGroupEquals(model, server))
                    await UpdateDeviceGroupHelper(model, token);
            }

            // Delete: 서버에는 있지만 로컬에서 제거된 항목
            foreach (var server in serverList.Where(s => !currentList.Any(c => c.Id == s.Id)))
            {
                var resp = await _apiService.DeleteDeviceGroupAsync(server.Id, token);
                if (!resp.Success) _log?.Warning($"DeviceGroup delete failed: id={server.Id} {resp.Message}");
            }

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

    private async Task CreateDeviceGroupHelper(DeviceGroupModel model, CancellationToken token)
    {
        try
        {
            var resp = await _apiService.CreateDeviceGroupAsync(model.ToDeviceGroupDto(), token);
            if (!resp.Success) _log?.Warning($"DeviceGroup create failed: {resp.Message}");
        }
        catch (Exception ex) { _log?.Error($"CreateDeviceGroupHelper: {ex.Message}"); }
    }

    private async Task UpdateDeviceGroupHelper(DeviceGroupModel model, CancellationToken token)
    {
        try
        {
            var resp = await _apiService.UpdateDeviceGroupAsync(model.Id, model.ToDeviceGroupDto(), token);
            if (!resp.Success) _log?.Warning($"DeviceGroup update failed: id={model.Id} {resp.Message}");
        }
        catch (Exception ex) { _log?.Error($"UpdateDeviceGroupHelper: {ex.Message}"); }
    }

    public override void OnSelectionChanged(IList<DeviceGroupViewModel> rows)
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
                foreach (IDeviceGroupViewModel newItem in e.NewItems)
                    _deviceGroupProvider.Add(newItem.Model);
                break;
            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems == null) return;
                foreach (IDeviceGroupViewModel oldItem in e.OldItems)
                    _deviceGroupProvider.Remove(oldItem.Model);
                break;
            case NotifyCollectionChangedAction.Reset:
                ViewModelProvider.Clear();
                foreach (var item in _deviceGroupProvider.OfType<IDeviceGroupModel>())
                    ViewModelProvider.Add(new DeviceGroupViewModel(item));
                break;
        }
    }
    #endregion
    #region - Helper Methods -
    private async Task<List<DeviceGroupModel>> FetchDeviceGroupsAsync(CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetDeviceGroupsAsync(page: 1, limit: 100, token: token);
            if (response.Success && response.Data != null)
                return response.Data.Select(dto => dto.ToDeviceGroupModel()).ToList();

            _log?.Error($"Failed to fetch device groups: {response.Error?.Message}");
            return new List<DeviceGroupModel>();
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in FetchDeviceGroupsAsync: {ex.Message}");
            return new List<DeviceGroupModel>();
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

                var items = _deviceGroupProvider.OfType<IDeviceGroupModel>()
                    .Where(m => m.Id > 0).ToList();
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
                            ViewModelProvider.Add(new DeviceGroupViewModel(item) { Index = startIndex + idx + 1 });
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
    #region - IHanldes -
    public async Task HandleAsync(CallDeleteDeviceGroupProcessMessageModel message, CancellationToken cancellationToken)
    {
        await _eventAggregator.PublishOnCurrentThreadAsync(
            new OpenProgressPopupMessageModel(),
            cancellationToken);

        await Task.Run(async () =>
        {
            foreach (var item in SelectedItems.ToList())
            {
                var model = (IDeviceGroupModel)item.Model;
                if (model.Id <= 0)
                {
                    _deviceGroupProvider.Remove(model);
                    continue;
                }
                var response = await _apiService.DeleteDeviceGroupAsync(model.Id, cancellationToken);

                if (!response.Success)
                {
                    _log?.Error($"Failed to delete device group {model.Id}: {response.Error?.Message}");
                }
            }
        }, cancellationToken);

        if (_pCancellationTokenSource != null && !_pCancellationTokenSource.IsCancellationRequested)
        {
            _pCancellationTokenSource.Cancel();
            _pCancellationTokenSource.Dispose();
            _pCancellationTokenSource = new CancellationTokenSource();
        }

        await DataInitialize(_pCancellationTokenSource!.Token).ConfigureAwait(false);
        UpdateAction?.Invoke();

        await _eventAggregator.PublishOnCurrentThreadAsync(
            new ClosePopupMessageModel(),
            cancellationToken);
    }
    #endregion
    #region - Properties -
    public event System.Action? UpdateAction;
    #endregion
    #region - Helpers -
    internal static bool DeviceGroupEquals(IDeviceGroupModel a, IDeviceGroupModel b)
        => a.Name == b.Name && a.Description == b.Description;
    #endregion
    #region - Attributes -
    private readonly IDeviceApiService _apiService;
    private readonly DeviceGroupProvider _deviceGroupProvider;
    #endregion
}
