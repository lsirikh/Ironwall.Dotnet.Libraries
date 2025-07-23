using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Db.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Specialized;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/28/2025 2:07:14 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class SensorDevicePanelViewModel : BaseDataGridMultiPanelViewModel<SensorDeviceViewModel>
                                        ,IHandle<CallDeleteSensorDeviceProcessMessageModel>
{
    #region - Ctors -
    public SensorDevicePanelViewModel(IEventAggregator eventAggregator
                                        , ILogService log
                                        , IDeviceDbService dbService
                                        , SensorDeviceProvider deviceProvider
                                        , ControllerDeviceProvider controllerDeviceProvider
                                        ) : base(eventAggregator, log)
    {
        _dbService = dbService;
        _deviceProvider = deviceProvider;
        _controllerProvider = controllerDeviceProvider;
    }
    #endregion
    #region - Implementation of Interface -
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
            Explain = "선택한 센서 장비를 정말로 삭제하시겠습니까?",
            MessageModel = new CallDeleteSensorDeviceProcessMessageModel()
        });
    }

    public override void OnClickInsertButton(object sender, RoutedEventArgs e)
    {
        var vm = new SensorDeviceViewModel(new SensorDeviceModel());
        ViewModelProvider.Add(vm);
    }

    public override async void OnClickReloadButton(object sender, RoutedEventArgs e)
    {
        if (!await _processGate.WaitAsync(0))       // 0 → “비동기로 테스트-후-입장”
            return;
        try
        {
            ReloadButtonEnable = false;
            if (_pCancellationTokenSource != null && !_pCancellationTokenSource!.IsCancellationRequested)
            {
                _pCancellationTokenSource.Cancel();
                _pCancellationTokenSource.Dispose();
            }
            _pCancellationTokenSource = new CancellationTokenSource();
            var token = _pCancellationTokenSource!.Token;

            await DataInitialize(token);
            await Task.Delay(2000, token);
        }
        catch (OperationCanceledException) { /* 무시 */ }
        catch (Exception ex) { _log?.Error(ex.Message); }
        finally
        {
            ReloadButtonEnable = true;            // UI Enable
            UpdateAction?.Invoke();
            _processGate.Release();                // 뮤텍스 해제
        }
    }

    public override async void OnClickSaveButton(object sender, RoutedEventArgs e)
    {
        if(!await _processGate.WaitAsync(0))       // 0 → “비동기로 테스트-후-입장”
            return;

        try
        {
            SaveButtonEnable = false;

            if (_pCancellationTokenSource != null && !_pCancellationTokenSource!.IsCancellationRequested)
            {
                _pCancellationTokenSource.Cancel();
                _pCancellationTokenSource.Dispose();
            }
            _pCancellationTokenSource = new CancellationTokenSource();

            var token = _pCancellationTokenSource.Token;
            var currentList = _deviceProvider;

            var dbList = await _dbService.FetchSensorsAsync(token);

            var insertList = currentList.Where(m => m.Id <= 0).ToList();
            var updateList = currentList
                .Where(m => m.Id > 0)
                .Join(dbList, m => m.Id, d => d.Id,
                    (m, d) => new { updated = m, original = d })
                .Where(p => !DeviceEquals(p.updated, p.original))
                .Select(p => p.updated)
                .ToList();
            
            foreach (var model in updateList)
                await _dbService.UpdateSensorAsync(model, token);
            
            foreach (var model in insertList)
                await _dbService.InsertSensorAsync(model, token);

            await DataInitialize().ConfigureAwait(false);
            await Task.Delay(2000, token);
        }
        catch (TaskCanceledException ex) { _log?.Warning(ex.Message); }
        catch (OperationCanceledException) { /* 무시 */ }
        catch (Exception ex) { _log?.Error(ex.Message); }
        finally
        {
            UpdateAction?.Invoke();
            SaveButtonEnable = true;
            _processGate.Release();                // 뮤텍스 해제
        }
    }

    public override void OnSelectionChanged(IList<SensorDeviceViewModel> rows)
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
                // New items added
                if (e.NewItems == null) return;
                foreach (ISensorDeviceViewModel newItem in e.NewItems)
                {
                    _deviceProvider.Add((ISensorDeviceModel)newItem.Model);
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                // Items removed
                if (e.OldItems == null) return;
                foreach (ISensorDeviceViewModel oldItem in e.OldItems)
                {
                    _deviceProvider.Remove((ISensorDeviceModel)oldItem.Model);
                }
                break;

            case NotifyCollectionChangedAction.Replace:
                // Some items replaced
                if (e.OldItems == null) return;
                foreach (ISensorDeviceViewModel oldItem in e.OldItems)
                {
                    _deviceProvider.Remove((ISensorDeviceModel)oldItem.Model);
                }
                if (e.NewItems == null) return;
                foreach (ISensorDeviceViewModel newItem in e.NewItems)
                {
                    _deviceProvider.Add((ISensorDeviceModel)newItem.Model);
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                // The whole list is refreshed
                ViewModelProvider.Clear();
                foreach (var item in _deviceProvider.OfType<ISensorDeviceModel>())
                {
                    ViewModelProvider.Add(new SensorDeviceViewModel(item));
                }

                break;
        }
    }
    #endregion
    #region - Binding Methods -
    private static bool DeviceEquals(ISensorDeviceModel a, ISensorDeviceModel b)
    {
        return a.DeviceNumber == b.DeviceNumber &&
               a.DeviceGroup == b.DeviceGroup &&
               a.DeviceName == b.DeviceName &&
               a.DeviceType == b.DeviceType &&
               a.Version == b.Version &&
               a.Status == b.Status &&
               a.Controller?.Id == b.Controller?.Id;
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
                //await Task.Delay(300, cancellationToken);

                //DB Fetching
                await _dbService.FetchInstanceAsync(cancellationToken);

                ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;

                //ViewModelProvider Setting
                if (cancellationToken.IsCancellationRequested) new TaskCanceledException("Task was cancelled!");


                DispatcherService.Invoke(() =>
                {
                    ViewModelProvider.Clear();
                    foreach (var (item, index) in _deviceProvider.OfType<ISensorDeviceModel>().Select((item, index) => (item, index)))
                    {
                        if (cancellationToken.IsCancellationRequested) new TaskCanceledException("Task was cancelled!");
                        ViewModelProvider.Add(new SensorDeviceViewModel(item) { Index = index + 1});
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
    public async Task HandleAsync(CallDeleteSensorDeviceProcessMessageModel message, CancellationToken cancellationToken)
    {
        // 1. 진행중 UI 표시
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel(), cancellationToken);

        // 2. 비동기 작업 (UI 스레드와 분리)
        await Task.Run(async () =>
        {
            foreach (var item in SelectedItems.ToList())
            {
                var ret = await _dbService.DeleteSensorAsync((ISensorDeviceModel)item.Model, cancellationToken);
            }
        }, cancellationToken);

        await DataInitialize().ConfigureAwait(false);
        UpdateAction?.Invoke();

        // 4. 진행중 UI 닫기
        await _eventAggregator.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
    }
    #endregion
    #region - Properties -
    public IEnumerable<IControllerDeviceModel> Controllers => _controllerProvider;
    public event System.Action? UpdateAction;
    #endregion
    #region - Attributes -
    private readonly ControllerDeviceProvider _controllerProvider;
    private IDeviceDbService _dbService;
    private SensorDeviceProvider _deviceProvider;
    #endregion
}