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
using System.Windows.Threading;

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
    public CameraDevicePanelViewModel(
        IEventAggregator eventAggregator,
        ILogService log,
        IDeviceApiService apiService,
        CameraDeviceProvider deviceProvider,
        IDeviceProviderService deviceProviderService)
        : base(eventAggregator, log)
    {
        _apiService = apiService;
        _deviceProvider = deviceProvider;
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
            var currentList = _deviceProvider.OfType<CameraDeviceModel>().ToList();

            // 서버 목록 조회 (비교를 위해)
            var serverList = await FetchCamerasAsync(token);

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
                await CreateCameraAsync(model, token);
            }

            // Update 처리
            foreach (var model in updateList)
            {
                await UpdateCameraAsync(model, token);
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
    internal static bool DeviceEquals(ICameraDeviceModel a, ICameraDeviceModel b)
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
        a.Mode == b.Mode &&
        a.Category == b.Category &&
        a.IsRecord == b.IsRecord &&
        a.IsEnable == b.IsEnable &&
        a.Location == b.Location &&
        a.Latitude == b.Latitude &&
        a.Longitude == b.Longitude &&
        CameraUrlsEquals(a.Urls, b.Urls);
    }

    internal static bool CameraUrlsEquals(ICameraUrlsModel? a, ICameraUrlsModel? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return a.HomepageUrl == b.HomepageUrl &&
            a.OnvifDeviceService == b.OnvifDeviceService &&
            a.RtspMain == b.RtspMain &&
            a.RtspSub == b.RtspSub &&
            a.WebrtcMain == b.WebrtcMain &&
            a.SnapshotCh1 == b.SnapshotCh1;
    }

    #endregion
    #region - Helper Methods -
    /// <summary>
    /// GOP API를 통해 Camera 목록 조회하고 Model로 변환
    /// </summary>
    private async Task<List<CameraDeviceModel>> FetchCamerasAsync(CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetCamerasAsync(
                page: 1,
                limit: 100,
                token: token);

            if (response.Success && response.Data != null)
            {
                return response.Data
                    .Select(dto => dto.ToCameraDeviceModel())
                    .ToList();
            }
            else
            {
                _log?.Error($"Failed to fetch cameras: {response.Error?.Message}");
                return new List<CameraDeviceModel>();
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in FetchCamerasAsync: {ex.Message}");
            return new List<CameraDeviceModel>();
        }
    }

    /// <summary>
    /// GOP API를 통해 Camera 생성
    /// </summary>
    private async Task<bool> CreateCameraAsync(CameraDeviceModel model, CancellationToken token = default)
    {
        try
        {
            var dto = model.ToCameraDeviceDto();
            var response = await _apiService.CreateCameraAsync(dto, token);

            if (response.Success)
            {
                _log?.Info($"Camera created successfully: {response.Data?.Id}");
                return true;
            }
            else
            {
                _log?.Error($"Failed to create camera: {response.Error?.Message}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in CreateCameraAsync: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// GOP API를 통해 Camera 업데이트
    /// </summary>
    private async Task<bool> UpdateCameraAsync(CameraDeviceModel model, CancellationToken token = default)
    {
        try
        {
            var dto = model.ToCameraDeviceDto();
            var response = await _apiService.UpdateCameraAsync(model.Id, dto, token);

            if (response.Success)
            {
                _log?.Info($"Camera updated successfully: {model.Id}");
                return true;
            }
            else
            {
                _log?.Error($"Failed to update camera {model.Id}: {response.Error?.Message}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in UpdateCameraAsync: {ex.Message}");
            return false;
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
                        .OfType<ICameraDeviceModel>()
                        .Select((item, index) => (item, index)))
                    {
                        if (cancellationToken.IsCancellationRequested)
                            throw new TaskCanceledException("Task was cancelled!");

                        ViewModelProvider.Add(new CameraDeviceViewModel((CameraDeviceModel)item) { Index = index + 1 });
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
    #region - IHanldes -
    public async Task HandleAsync(CallDeleteCameraDeviceProcessMessageModel message, CancellationToken cancellationToken)
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
                var model = (ICameraDeviceModel)item.Model;
                var response = await _apiService.DeleteCameraAsync(model.Id, cancellationToken);

                if (!response.Success)
                {
                    _log?.Error($"Failed to delete camera {model.Id}: {response.Error?.Message}");
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
    public event System.Action? UpdateAction;
    #endregion
    #region - Attributes -
    private readonly IDeviceApiService _apiService;
    private readonly CameraDeviceProvider _deviceProvider;
    private readonly IDeviceProviderService _deviceProviderService;
    #endregion
}