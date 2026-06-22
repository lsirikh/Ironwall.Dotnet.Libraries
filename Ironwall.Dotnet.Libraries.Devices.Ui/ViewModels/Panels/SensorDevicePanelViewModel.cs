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
            var existing = ViewModelProvider.Select(vm => vm.Model.DeviceNumber).ToHashSet();
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

            // (Temp-state) Save = Create(Draft Id≤0, 제어기 선택분만) + Update(Id>0 변경분). Draft는 ViewModelProvider에만.
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
            var draftVMs = ViewModelProvider.Where(vm => vm.Model.Id <= 0).ToList();
            var committed = new List<SensorDeviceViewModel>();

            // 루프A: Draft 생성 (controller_id>0 필수 — 미선택 시 보류)
            int held = await ExecuteCreateAsync(
                draftVMs,
                vm => { var c = ((ISensorDeviceModel)vm.Model).Controller; return c != null && c.Id > 0; },
                (vm, ct) => CreateSensorAsync((SensorDeviceModel)vm.Model, ct),
                vm => committed.Add(vm),
                vm => $"{vm.Model.DeviceName}(신규)",
                failures, token);

            // 루프B: Id>0 변경분 Update
            var updateVMs = ViewModelProvider.Where(vm =>
            {
                if (vm.Model.Id <= 0) return false;
                var srv = serverList.FirstOrDefault(s => s.Id == vm.Model.Id);
                return srv != null && !DeviceEquals((ISensorDeviceModel)vm.Model, srv);
            }).ToList();
            await ExecuteSaveUpdatesAsync(
                updateVMs,
                (vm, ct) => UpdateSensorAsync((SensorDeviceModel)vm.Model, ct),
                vm => $"{vm.Model.DeviceName}(Id={vm.Model.Id})",
                failures, token);

            // 재조회 + 재구성 + 생존자(실패/보류 Draft=제어기 미선택) 복원
            await _deviceProviderService.FetchAllDevicesAsync(token);
            await DataInitialize(token);
            foreach (var d in draftVMs.Except(committed)) { d.Index = ViewModelProvider.Count + 1; ViewModelProvider.Add(d); }

            await NotifySaveResultAsync("센서 저장 안내", failures, held, token);
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

    // (PR-C) 미저장 Draft(Id≤0) 개수 — 화면 전환/종료 시 손실 경고용. 베이스 가상메서드 override.
    protected override int CountUnsavedDrafts() => ViewModelProvider.Count(vm => vm.Model.Id <= 0);

    private void CollectionEntity_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                // (Draft 격리) Id≤0 미저장 Draft는 provider 미투영(공유 오염/이중행 차단).
                if (e.NewItems == null) return;
                foreach (ISensorDeviceViewModel newItem in e.NewItems)
                {
                    if (!ShouldProjectToProvider(newItem.Model.Id)) continue;
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
                    if (!ShouldProjectToProvider(newItem.Model.Id)) continue;
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
               a.Heading == b.Heading &&
               a.Altitude == b.Altitude &&
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

    // (Temp-state) Create/Update 헬퍼 — ApiResultLite 반환(베이스 ExecuteCreate/SaveUpdates 연동).
    private async Task<ApiResultLite> CreateSensorAsync(SensorDeviceModel model, CancellationToken token = default)
    {
        try
        {
            var r = await _apiService.CreateSensorAsync(model.ToSensorDeviceDto(), token);
            return new ApiResultLite(r.Success && r.Data != null, r.StatusCode, r.Error?.Details);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _log?.Error($"CreateSensorAsync: {ex.Message}"); return new ApiResultLite(false, 0, ex.Message); }
    }

    private async Task<ApiResultLite> UpdateSensorAsync(SensorDeviceModel model, CancellationToken token = default)
    {
        try
        {
            var r = await _apiService.UpdateSensorAsync(model.Id, model.ToSensorDeviceDto(), token);
            return new ApiResultLite(r.Success, r.StatusCode, r.Error?.Details);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _log?.Error($"UpdateSensorAsync: {ex.Message}"); return new ApiResultLite(false, 0, ex.Message); }
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
    public IEnumerable<IControllerDeviceModel> Controllers => _controllerProvider.Where(c => c.Id > 0);   // (G6) Temp 제어기 제외
    public event System.Action? UpdateAction;
    #endregion
    #region - Attributes -
    private readonly ControllerDeviceProvider _controllerProvider;
    private readonly IDeviceApiService _apiService;
    private readonly SensorDeviceProvider _deviceProvider;
    private readonly IDeviceProviderService _deviceProviderService;
    #endregion
}