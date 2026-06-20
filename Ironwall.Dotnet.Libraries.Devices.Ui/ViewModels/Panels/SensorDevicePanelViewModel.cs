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
        // (P2-S5) 초기 로딩도 _processGate 직렬화 — 로딩 중 Insert/Save/Delete 경합 차단.
        if (!await _processGate.WaitAsync(0)) return;
        try
        {
            _pCancellationTokenSource = new CancellationTokenSource();
            await DataInitialize(_pCancellationTokenSource!.Token).ConfigureAwait(false);
        }
        finally { _processGate.Release(); }
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

    public override async void OnClickInsertButton(object sender, RoutedEventArgs e)
    {
        // (P2-S6) 센서는 controller_id(FK) 필수 → 즉시 Create 불가(서버 422). Draft로 추가하고
        //   제어기 선택 후 Save 시 커밋(Draft-commit-when-valid). 다른 5패널과 달리 Draft 유지가 의도.
        // (코드리뷰 M1) Insert도 _processGate 직렬화 + try/catch.
        if (!await _processGate.WaitAsync(0)) return;
        try
        {
            var existing = _deviceProvider.OfType<SensorDeviceModel>().Select(m => m.DeviceNumber).ToHashSet();
            int number = 1; while (existing.Contains(number)) number++;
            var model = new SensorDeviceModel { DeviceNumber = number, DeviceName = $"새 센서 {number}" };
            ViewModelProvider.Add(new SensorDeviceViewModel(model));
        }
        catch (Exception ex) { _log?.Error($"OnClickInsertButton: {ex.Message}"); }
        finally { _processGate.Release(); }
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

            // 서버 목록 조회 (전 페이지 + 완전성)
            var (serverList, complete) = await FetchSensorsAsync(token);
            if (!complete)
            {
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "저장 보류",
                    Explain = "서버 센서 목록을 완전히 불러오지 못해 저장을 보류합니다. 잠시 후 다시 시도하세요."
                });
                return;
            }

            var failures = new List<string>();
            int draftNoController = 0;

            // (P2-S6) Draft-commit-when-valid: 제어기(FK) 선택된 Draft만 즉시 Create + Id write-back(재Save 중복 방지).
            //   미선택 Draft는 서버 422 방지를 위해 보류(행 유지) + 아래 안내.
            foreach (var model in currentList.Where(m => m.Id <= 0))
            {
                if (model.Controller == null || model.Controller.Id <= 0) { draftNoController++; continue; }
                var resp = await _apiService.CreateSensorAsync(model.ToSensorDeviceDto(), token);
                if (resp.Success && resp.Data != null)
                    _deviceProvider.Remove((ISensorDeviceModel)model);   // (코드리뷰 H1) 커밋된 로컬 Draft 제거 → FetchAll 서버본만 반영(타입드 투영 이중행 방지)
                else
                    failures.Add($"{model.DeviceName}(신규)");
            }

            // Update 전용 (Id>0 변경분)
            foreach (var model in currentList.Where(m => m.Id > 0))
            {
                var server = serverList.FirstOrDefault(s => s.Id == model.Id);
                if (server != null && !DeviceEquals(model, server))
                    if (!await UpdateSensorAsync(model, token))
                        failures.Add($"{model.DeviceName}(Id={model.Id})");
            }

            if (failures.Count > 0 || draftNoController > 0)
            {
                var explain = "";
                if (failures.Count > 0) explain += $"{failures.Count}건 저장에 실패했습니다.\n" + string.Join("\n", failures) + "\n";
                if (draftNoController > 0) explain += $"{draftNoController}건은 제어기 미선택으로 저장되지 않았습니다. 제어기를 선택한 뒤 다시 저장하세요.";
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "센서 저장 안내",
                    Explain = explain
                });
            }

            // committed Draft는 로컬 제거되어 FetchAll 서버본으로 단일 반영. 미선택 Id<=0 Draft는 공유 provider에
            //   없으므로(타입드 전용) UpdateOrAddDevices(비-Reset upsert)에 영향받지 않아 그대로 보존된다.
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
    // (P2-S4) 전 페이지 순회 + 완전성 신호.
    private async Task<(List<SensorDeviceModel> list, bool complete)> FetchSensorsAsync(CancellationToken token = default)
    {
        var all = new List<SensorDeviceModel>();
        try
        {
            const int limit = 100, maxPages = 100;
            for (int page = 1; page <= maxPages; page++)
            {
                var response = await _apiService.GetSensorsAsync(page: page, limit: limit, includeController: true, token: token);
                if (!response.Success || response.Data == null)
                {
                    _log?.Error($"Failed to fetch sensors (page {page}): {response.Error?.Message}");
                    return (all, false);
                }
                var batch = response.Data.Select(dto => dto.ToSensorDeviceModel()).ToList();
                all.AddRange(batch);
                if (batch.Count < limit) return (all, true);
            }
            return (all, true);
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in FetchSensorsAsync: {ex.Message}");
            return (all, false);
        }
    }

    // (P2-S6) Create 헬퍼 제거 — Save의 Draft-commit에서 _apiService.CreateSensorAsync 직접 호출(Id write-back). Update만 유지.
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
        return Task.Run(async () =>
        {
            try
            {
                DispatcherService.Invoke(() => IsVisible = false);
                await DispatcherService.BeginInvoke(() => { }, DispatcherPriority.Render);

                ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;
                await DispatcherService.BeginInvoke(() => ViewModelProvider.Clear());

                var items = _deviceProvider.OfType<ISensorDeviceModel>().ToList();
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
                            ViewModelProvider.Add(new SensorDeviceViewModel((SensorDeviceModel)item) { Index = startIndex + idx + 1 });
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
    public async Task HandleAsync(CallDeleteSensorDeviceProcessMessageModel message, CancellationToken cancellationToken)
    {
        // (P2-S3) 삭제 _processGate 직렬화 + 베이스 ExecuteDeleteAsync(Id<=0 로컬/Id>0 verify-after-success/부분실패 통지).
        if (!await _processGate.WaitAsync(0)) return;
        try
        {
            // Progress Popup 표시
            await _eventAggregator.PublishOnCurrentThreadAsync(
                new OpenProgressPopupMessageModel(),
                cancellationToken);

            // 삭제 처리 (UI 스레드와 분리)
            await Task.Run(async () =>
            {
                await ExecuteDeleteAsync(
                    SelectedItems.ToList(),
                    r => r.Model.Id,
                    async (id, ct) => (await _apiService.DeleteSensorAsync(id, ct)).Success,
                    r => _deviceProvider.Remove((ISensorDeviceModel)r.Model),
                    cancellationToken);
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
        finally { _processGate.Release(); }
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