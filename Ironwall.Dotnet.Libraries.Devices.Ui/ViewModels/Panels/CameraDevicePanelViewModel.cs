using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Db.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
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
   Created On   : 5/28/2025 2:07:39 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class CameraDevicePanelViewModel : BaseDataGridMultiPanelViewModel<CameraDeviceViewModel>
                                    , IHandle<CallDeleteCameraDeviceProcessMessageModel>
{
    
    #region - Ctors -
    public CameraDevicePanelViewModel(IEventAggregator eventAggregator
                                       , ILogService log
                                       , IDeviceDbService dbService
                                       , CameraOnvifService cameraOnvifService
                                       , CameraDeviceProvider deviceProvider
                                       ) : base(eventAggregator, log)
    {
        _dbService = dbService;
        _deviceProvider = deviceProvider;
        _cameraOnvifService = cameraOnvifService;
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
            Explain = "선택한 카메라 장비를 정말로 삭제하시겠습니까?",
            MessageModel = new CallDeleteCameraDeviceProcessMessageModel()
        });
    }

    public override void OnClickInsertButton(object sender, RoutedEventArgs e)
    {
        var vm = new CameraDeviceViewModel(new CameraDeviceModel());
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
        if (!await _processGate.WaitAsync(0))       // 0 → “비동기로 테스트-후-입장”
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
            var currentList = _deviceProvider; // 또는 _deviceProvider.CollectionEntity

            var dbList = await _dbService.FetchCamerasAsync(token);

            // Insert 대상: ID가 없는 경우 (신규)
            var insertList = currentList.Where(m => m.Id <= 0).ToList();

            // Update 대상: ID가 있고, 변경된 경우
            var updateList = currentList
                .Where(m => m.Id > 0)
                .Join(dbList, m => m.Id, d => d.Id,
                    (m, d) => new { updated = m, original = d })
                .Where(p => !DeviceEquals(p.updated, p.original))
                .Select(p => p.updated)
                .ToList();


            // 순차 처리
            foreach (var model in updateList)
                await _dbService.UpdateCameraAsync(model, token);

            foreach (var model in insertList)
                await _dbService.InsertCameraAsync(model, token);

            await DataInitialize(_cancellationTokenSource!.Token);
            await Task.Delay(2000);
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

    public override void OnSelectionChanged(IList<CameraDeviceViewModel> rows)
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
                foreach (ICameraDeviceViewModel newItem in e.NewItems)
                {
                    _deviceProvider.Add((ICameraDeviceModel)newItem.Model);
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                // Items removed
                if (e.OldItems == null) return;
                foreach (ICameraDeviceViewModel oldItem in e.OldItems)
                {
                    _deviceProvider.Remove((ICameraDeviceModel)oldItem.Model);
                }
                break;

            case NotifyCollectionChangedAction.Replace:
                // Some items replaced
                if (e.OldItems == null) return;
                foreach (ICameraDeviceViewModel oldItem in e.OldItems)
                {
                    _deviceProvider.Remove((ICameraDeviceModel)oldItem.Model);
                }
                if (e.NewItems == null) return;
                foreach (ICameraDeviceViewModel newItem in e.NewItems)
                {
                    _deviceProvider.Add((ICameraDeviceModel)newItem.Model);
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                // The whole list is refreshed
                ViewModelProvider.Clear();
                foreach (var item in _deviceProvider.OfType<ICameraDeviceModel>())
                {
                    ViewModelProvider.Add(new CameraDeviceViewModel(item));
                }

                break;
        }
    }
    #endregion
    #region - Binding Methods -
    private static bool DeviceEquals(ICameraDeviceModel a, ICameraDeviceModel b)
    {
        return a.DeviceNumber == b.DeviceNumber &&
        a.DeviceGroup == b.DeviceGroup &&
        a.DeviceName == b.DeviceName &&
        a.DeviceType == b.DeviceType &&
        a.Version == b.Version &&
        a.Status == b.Status &&
        a.IpAddress == b.IpAddress &&
        a.Port == b.Port &&
        a.Username == b.Username &&
        a.Password == b.Password &&
        a.RtspUri == b.RtspUri &&
        a.RtspPort == b.RtspPort &&
        a.Mode == b.Mode &&
        a.Category == b.Category;
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

                //DB Fetching
                await _dbService.FetchInstanceAsync(cancellationToken);

                ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;
                //ViewModelProvider Setting
                if (cancellationToken.IsCancellationRequested) new TaskCanceledException("Task was cancelled!");

                DispatcherService.Invoke(() =>
                {
                    ViewModelProvider.Clear();
                    foreach (var (item, index) in _deviceProvider.OfType<ICameraDeviceModel>().Select((item, index) => (item, index)))
                    {
                        if (cancellationToken.IsCancellationRequested) new TaskCanceledException("Task was cancelled!");
                        var viewModel = new CameraDeviceViewModel(item) { Index = index + 1 };

                        ViewModelProvider.Add(viewModel);

                        if(viewModel.Mode == Enums.EnumCameraMode.ONVIF)
                        {
                            _ = Task.Run(async () => 
                            {
                                viewModel.Status = Enums.EnumDeviceStatus.DEACTIVATED;  
                                //Onvif가 처리된 모델로 새로 입힌다.
                                var onvifInstance = await _cameraOnvifService.CreateOnvifInstance(item, cancellationToken);
                                if (onvifInstance == null) return;
                                var onvifedModel = CameraMappingHelper.ToDeviceModel(onvifInstance, item);
                                if (onvifedModel == null) return;

                                viewModel.Status = Enums.EnumDeviceStatus.ACTIVATED;  
                                viewModel.Refresh();
                            });
                        }
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
    public async Task HandleAsync(CallDeleteCameraDeviceProcessMessageModel message, CancellationToken cancellationToken)
    {
        // 1. 진행중 UI 표시
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel(), cancellationToken);

        // 2. 비동기 작업 (UI 스레드와 분리)
        await Task.Run(async () =>
        {
            foreach (var item in SelectedItems.ToList())
            {
                var ret = await _dbService.DeleteCameraAsync((ICameraDeviceModel)item.Model, cancellationToken);
            }
        }, cancellationToken);

        if (_cancellationTokenSource != null || !_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        await DataInitialize(_cancellationTokenSource.Token).ConfigureAwait(false);
        UpdateAction?.Invoke();

        // 4. 진행중 UI 닫기
        await _eventAggregator.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
    }
    #endregion
    #region - Properties -
    public event System.Action? UpdateAction;
    #endregion
    #region - Attributes -
    private IDeviceDbService _dbService;
    private CameraDeviceProvider _deviceProvider;
    private CameraOnvifService _cameraOnvifService;
    #endregion

}