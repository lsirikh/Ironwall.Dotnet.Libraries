using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Specialized;
using System.Threading;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;

public class EnclosureDevicePanelViewModel : BaseDataGridMultiPanelViewModel<EnclosureDeviceViewModel>
{
    #region - Ctors -
    public EnclosureDevicePanelViewModel(IEventAggregator eventAggregator
                                        , ILogService log
                                        , IDeviceApiService apiService
                                        , EnclosureDeviceProvider deviceProvider
                                        ) : base(eventAggregator, log)
    {
        _apiService = apiService;
        _deviceProvider = deviceProvider;
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

    public override void OnClickDeleteButton(object sender, RoutedEventArgs e)
    {
        if (SelectedItemCount == 0) return;
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

            foreach (var model in currentList.Where(m => m.Id <= 0))
                await _apiService.CreateEnclosureAsync(model.ToEnclosureDeviceDto(), token);

            foreach (var model in currentList.Where(m => m.Id > 0)
                .Join(serverList, m => m.Id, d => d.Id, (m, d) => m))
                await _apiService.UpdateEnclosureAsync(model.Id, model.ToEnclosureDeviceDto(), token);

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
    #region - Helper Methods -
    private async Task<List<EnclosureDeviceModel>> FetchEnclosuresAsync(CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetEnclosuresAsync(page: 1, limit: 200, token: token);
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
        return Task.Run(async () =>
        {
            try
            {
                IsVisible = false;
                var models = await FetchEnclosuresAsync(cancellationToken);
                ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;

                if (cancellationToken.IsCancellationRequested)
                    throw new TaskCanceledException("Task was cancelled!");

                _deviceProvider.Clear();
                foreach (var model in models)
                    _deviceProvider.Add(model);

                DispatcherService.Invoke(() =>
                {
                    ViewModelProvider.Clear();
                    foreach (var (item, index) in models.Select((item, index) => (item, index)))
                    {
                        if (cancellationToken.IsCancellationRequested)
                            throw new TaskCanceledException("Task was cancelled!");
                        ViewModelProvider.Add(new EnclosureDeviceViewModel(item) { Index = index + 1 });
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
    #region - Properties -
    public event System.Action? UpdateAction;
    #endregion
    #region - Attributes -
    private readonly IDeviceApiService _apiService;
    private readonly EnclosureDeviceProvider _deviceProvider;
    #endregion
}
