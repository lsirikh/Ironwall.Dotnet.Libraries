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
                                        , IDeviceApiService apiService
                                        , SensorDeviceProvider deviceProvider
                                        , ControllerDeviceProvider controllerDeviceProvider
                                        , IDeviceProviderService deviceProviderService
                                        ) : base(eventAggregator, log)
    {
        _apiService = apiService;
        _deviceProvider = deviceProvider;
        _controllerProvider = controllerDeviceProvider;
        _deviceProviderService = deviceProviderService;
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

            await _deviceProviderService.FetchAllDevicesAsync(token);
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
        if (!await _processGate.WaitAsync(0))
            return;

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

            // 현재 Provider의 목록
            var currentList = _deviceProvider.OfType<SensorDeviceModel>().ToList();

            // 서버 목록 조회 (비교를 위해)
            var serverList = await FetchSensorsAsync(token);

            // Insert 대상: ID가 없는 경우 (신규)
            var insertList = currentList.Where(m => m.Id <= 0).ToList();

            // Update 대상: ID가 있고 변경된 경우
            var updateList = currentList
                .Where(m => m.Id > 0)
                .Join(serverList, m => m.Id, d => d.Id,
                    (m, d) => new { updated = m, original = d })
                .Where(p => !DeviceEquals(p.updated, p.original))
                .Select(p => p.updated)
                .ToList();

            // Create 처리
            foreach (var model in insertList)
            {
                await CreateSensorAsync(model, token);
            }

            // Update 처리
            foreach (var model in updateList)
            {
                await UpdateSensorAsync(model, token);
            }

            // 재조회
            await _deviceProviderService.FetchAllDevicesAsync(_pCancellationTokenSource.Token);
            await DataInitialize(_pCancellationTokenSource.Token);
            await Task.Delay(2000, token);
        }
        catch (TaskCanceledException ex) { _log?.Warning(ex.Message); }
        catch (OperationCanceledException) { /* 무시 */ }
        catch (Exception ex) { _log?.Error(ex.Message); }
        finally
        {
            UpdateAction?.Invoke();
            SaveButtonEnable = true;
            _processGate.Release();
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
               (a.DeviceGroups ?? new()).SequenceEqual(b.DeviceGroups ?? new()) &&
               a.DeviceName == b.DeviceName &&
               a.DeviceType == b.DeviceType &&
               a.Version == b.Version &&
               a.Status == b.Status &&
               a.IsEnable == b.IsEnable &&
               a.Location == b.Location &&
               a.Latitude == b.Latitude &&
               a.Longitude == b.Longitude &&
               a.Controller?.Id == b.Controller?.Id;
    }
    #endregion
    #region - Helper Methods -
    /// <summary>
    /// GOP API를 통해 Sensor 목록 조회하고 Model로 변환
    /// </summary>
    private async Task<List<SensorDeviceModel>> FetchSensorsAsync(CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetSensorsAsync(
                page: 1,
                limit: 100,
                includeController: true,
                token: token);

            if (response.Success && response.Data != null)
            {
                return response.Data
                    .Select(dto => dto.ToSensorDeviceModel())
                    .ToList();
            }
            else
            {
                _log?.Error($"Failed to fetch sensors: {response.Error?.Message}");
                return new List<SensorDeviceModel>();
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in FetchSensorsAsync: {ex.Message}");
            return new List<SensorDeviceModel>();
        }
    }

    /// <summary>
    /// GOP API를 통해 Sensor 생성
    /// </summary>
    private async Task<bool> CreateSensorAsync(SensorDeviceModel model, CancellationToken token = default)
    {
        try
        {
            var dto = model.ToSensorDeviceDto();
            var response = await _apiService.CreateSensorAsync(dto, token);

            if (response.Success)
            {
                _log?.Info($"Sensor created successfully: {response.Data?.Id}");
                return true;
            }
            else
            {
                _log?.Error($"Failed to create sensor: {response.Error?.Message}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in CreateSensorAsync: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// GOP API를 통해 Sensor 업데이트
    /// </summary>
    private async Task<bool> UpdateSensorAsync(SensorDeviceModel model, CancellationToken token = default)
    {
        try
        {
            var dto = model.ToSensorDeviceDto();
            var response = await _apiService.UpdateSensorAsync(model.Id, dto, token);

            if (response.Success)
            {
                _log?.Info($"Sensor updated successfully: {model.Id}");
                return true;
            }
            else
            {
                _log?.Error($"Failed to update sensor {model.Id}: {response.Error?.Message}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in UpdateSensorAsync: {ex.Message}");
            return false;
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
                        .OfType<ISensorDeviceModel>()
                        .Select((item, index) => (item, index)))
                    {
                        if (cancellationToken.IsCancellationRequested)
                            throw new TaskCanceledException("Task was cancelled!");

                        ViewModelProvider.Add(new SensorDeviceViewModel((SensorDeviceModel)item) { Index = index + 1 });
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
        // Progress Popup 표시
        await _eventAggregator.PublishOnCurrentThreadAsync(
            new OpenProgressPopupMessageModel(),
            cancellationToken);

        // 삭제 처리 (UI 스레드와 분리)
        await Task.Run(async () =>
        {
            foreach (var item in SelectedItems.ToList())
            {
                var model = (ISensorDeviceModel)item.Model;
                var response = await _apiService.DeleteSensorAsync(model.Id, cancellationToken);

                if (!response.Success)
                {
                    _log?.Error($"Failed to delete sensor {model.Id}: {response.Error?.Message}");
                }
            }
        }, cancellationToken);

        // 취소 토큰 재생성
        if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        // 재조회
        await _deviceProviderService.FetchAllDevicesAsync(_cancellationTokenSource!.Token);
        await DataInitialize(_cancellationTokenSource!.Token).ConfigureAwait(false);
        UpdateAction?.Invoke();

        // Progress Popup 닫기
        await _eventAggregator.PublishOnCurrentThreadAsync(
            new ClosePopupMessageModel(),
            cancellationToken);
    }
    #endregion
    #region - Properties -
    public IEnumerable<IControllerDeviceModel> Controllers => _controllerProvider;
    public event System.Action? UpdateAction;
    #endregion
    #region - Attributes -
    private readonly ControllerDeviceProvider _controllerProvider;
    private readonly IDeviceApiService _apiService;
    private readonly SensorDeviceProvider _deviceProvider;
    private readonly IDeviceProviderService _deviceProviderService;
    #endregion
}