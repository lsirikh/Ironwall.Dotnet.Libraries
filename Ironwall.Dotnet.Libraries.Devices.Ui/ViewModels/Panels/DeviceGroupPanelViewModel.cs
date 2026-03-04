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

            foreach (var model in currentList.Where(m => m.Id <= 0))
                await _apiService.CreateDeviceGroupAsync(model.ToDeviceGroupDto(), token);

            foreach (var model in currentList.Where(m => m.Id > 0)
                .Join(serverList, m => m.Id, d => d.Id, (m, d) => m))
                await _apiService.UpdateDeviceGroupAsync(model.Id, model.ToDeviceGroupDto(), token);

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
                IsVisible = false;
                var models = await FetchDeviceGroupsAsync(cancellationToken);
                ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;

                if (cancellationToken.IsCancellationRequested)
                    throw new TaskCanceledException("Task was cancelled!");

                _deviceGroupProvider.Clear();
                foreach (var model in models)
                    _deviceGroupProvider.Add(model);

                DispatcherService.Invoke(() =>
                {
                    ViewModelProvider.Clear();
                    foreach (var (item, index) in models.Select((item, index) => (item, index)))
                    {
                        if (cancellationToken.IsCancellationRequested)
                            throw new TaskCanceledException("Task was cancelled!");
                        ViewModelProvider.Add(new DeviceGroupViewModel(item) { Index = index + 1 });
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
    #region - Attributes -
    private readonly IDeviceApiService _apiService;
    private readonly DeviceGroupProvider _deviceGroupProvider;
    #endregion
}
