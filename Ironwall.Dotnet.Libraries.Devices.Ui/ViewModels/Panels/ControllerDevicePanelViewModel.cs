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
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/28/2025 2:06:53 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class ControllerDevicePanelViewModel : BaseDataGridMultiPanelViewModel<ControllerDeviceViewModel>
                                            , IHandle<CallDeleteControllerDeviceProcessMessageModel>
{
    #region - Ctors -
    public ControllerDevicePanelViewModel(IEventAggregator eventAggregator
                                        , ILogService log
                                        , IDeviceApiService apiService
                                        , ControllerDeviceProvider deviceProvider
                                        , IDeviceProviderService deviceProviderService
                                        ) : base(eventAggregator, log)
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
        // (FR-EN-11) 역할강등 재평가 구독
        { var _pgs = DevicePermissionGate.Resolve(); if (_pgs != null) _pgs.PermissionsChanged += OnPermissionsChanged; }
        OnPermissionsChanged();   // (FR-EN-09 초기화 수정) 로그인 후 활성화 시 PermissionsChanged 이미 발화됨 → 초기 IsButtonEnable/SaveButtonEnable 권한 미반영(버튼 살아있는 버그). 활성화 시 1회 계산.
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
        // (FR-EN-11) 역할강등 재평가 구독 해제
        { var _pgs = DevicePermissionGate.Resolve(); if (_pgs != null) _pgs.PermissionsChanged -= OnPermissionsChanged; }
        ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;
        _deviceProvider.CollectionEntity.CollectionChanged -= DeviceProvider_CollectionChanged; // (FR-D1) 역방향 구독 해제
        if (_pCancellationTokenSource != null && !_pCancellationTokenSource!.IsCancellationRequested)
        {
            _pCancellationTokenSource.Cancel();
            _pCancellationTokenSource.Dispose();
        }
        return base.OnDeactivateAsync(close, cancellationToken);
    }

    public override async void OnClickDeleteButton(object sender, RoutedEventArgs e)
    {
        // (FR-EN-09) 삭제 권한 게이트
        if (!DevicePermissionGate.CanDelete()) { _log?.Warning("[FR-EN-09] 삭제 권한 없음(devices)"); return; }
        if (SelectedItemCount == 0) return;
        if (IsDeleteBatchExceeded(SelectedItemCount)) return;   // (CRUD 표준) 한 번에 최대 MAX_DELETE_COUNT건 초과 차단
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Explain = "선택한 제어기를 참조하는 센서도 함께 삭제 될 수 있습니다. 정말 삭제하시겠습니까?",
            MessageModel = new CallDeleteControllerDeviceProcessMessageModel()
        });
    }

    public override async void OnClickInsertButton(object sender, RoutedEventArgs e)
    {
        // (FR-EN-09) 추가 권한 게이트
        if (!DevicePermissionGate.CanEdit()) { _log?.Warning("[FR-EN-09] 추가 권한 없음(devices)"); return; }
        if (!await _processGate.WaitAsync(0)) return;
        try
        {
            // (Temp-state) 로컬 Draft 추가만 — 서버 미반영. IP/포트 등 입력 후 Save 시 등록.
            //   Draft(Id≤0)는 ViewModelProvider에만(provider 미진입, CollectionChanged 가드).
            var existing = ViewModelProvider.Select(vm => vm.Model.DeviceNumber).ToHashSet();
            int number = 1; while (existing.Contains(number)) number++;
            var model = new ControllerDeviceModel { DeviceNumber = number, DeviceName = $"새 제어기 {number}" };
            ViewModelProvider.Add(new ControllerDeviceViewModel(model));
        }
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
        // (FR-EN-09) 저장 권한 게이트
        if (!DevicePermissionGate.CanEdit()) { _log?.Warning("[FR-EN-09] 저장 권한 없음(devices)"); return; }
        if (!await _processGate.WaitAsync(0))
            return;

        try
        {
            IsSaving = true;

            if (_pCancellationTokenSource != null && !_pCancellationTokenSource.IsCancellationRequested)
            {
                _pCancellationTokenSource.Cancel();
                _pCancellationTokenSource.Dispose();
            }
            _pCancellationTokenSource = new CancellationTokenSource();

            var token = _pCancellationTokenSource.Token;

            // (Temp-state) Save = Create(Draft Id≤0) + Update(Id>0 변경분). Draft는 ViewModelProvider에만 존재.
            var (serverList, complete) = await FetchControllersAsync(token);
            if (!complete)
            {
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "저장 보류",
                    Explain = "서버 제어기 목록을 완전히 불러오지 못해 저장을 보류합니다. 잠시 후 다시 시도하세요."
                });
                return;
            }
            var failures = new List<string>();
            var draftVMs = ViewModelProvider.Where(vm => vm.Model.Id <= 0).ToList();
            var committed = new List<ControllerDeviceViewModel>();

            // 루프A: Draft 생성 (Port≥1 필수 — 미충족 시 보류)
            int held = await ExecuteCreateAsync(
                draftVMs,
                vm => ((IControllerDeviceModel)vm.Model).Port >= 1,
                (vm, ct) => CreateControllerAsync((ControllerDeviceModel)vm.Model, ct),
                vm => committed.Add(vm),
                vm => $"{vm.Model.DeviceName}(신규)",
                failures, token);

            // 루프B: Id>0 변경분 Update
            var updateVMs = ViewModelProvider.Where(vm =>
            {
                if (vm.Model.Id <= 0) return false;
                var srv = serverList.FirstOrDefault(s => s.Id == vm.Model.Id);
                return srv != null && !DeviceEquals((IControllerDeviceModel)vm.Model, srv);
            }).ToList();
            await ExecuteSaveUpdatesAsync(
                updateVMs,
                (vm, ct) => UpdateControllerAsync((ControllerDeviceModel)vm.Model, ct),
                vm => $"{vm.Model.DeviceName}(Id={vm.Model.Id})",
                failures, token);

            // 재조회 + 재구성 + 생존자(실패/보류 Draft) 복원
            await _deviceProviderService.FetchAllDevicesAsync(token);
            await DataInitialize(token);
            foreach (var d in draftVMs.Except(committed)) { d.Index = ViewModelProvider.Count + 1; ViewModelProvider.Add(d); }

            await NotifySaveResultAsync("제어기 저장 안내", failures, held, token);
            await Task.Delay(2000, token);
        }
        catch (TaskCanceledException ex) { _log?.Warning(ex.Message); }
        catch (OperationCanceledException) { /* 무시 */ }
        catch (Exception ex) { _log?.Error(ex.Message); }
        finally
        {
            UpdateAction?.Invoke();
            IsSaving = false;
            _processGate.Release();
        }
    }

    public override void OnSelectionChanged(IList<ControllerDeviceViewModel> rows)
    {
        SelectedItems = rows;
        rows.ToList().ForEach(entity => entity.IsSelected = true);
        NotifyOfPropertyChange(() => SelectedItems);
    }

    // (PR-C) 화면 전환/종료 시 미저장 Draft(Id≤0) 손실 경고용 — 베이스 CountUnsavedDrafts override.
    protected override int CountUnsavedDrafts() => ViewModelProvider.Count(vm => vm.Model.Id <= 0);

    private void CollectionEntity_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // (FR-D1) 역방향(provider→panel) 동기화 중 발생한 ViewModelProvider 변경은 이미 _deviceProvider에
        //   반영돼 있으므로 순방향 재투영을 건너뛴다. 순방향 자신도 전파 중 플래그를 세워 역방향 재진입(이중행/무한루프)을 막는다.
        if (_isSyncingFromProvider) return;
        _isSyncingFromProvider = true;
        try
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    // (Draft 격리) Id≤0 미저장 Draft는 provider 미투영(공유 오염/이중행 차단).
                    if (e.NewItems == null) return;
                    foreach (IControllerDeviceViewModel newItem in e.NewItems)
                    {
                        if (!ShouldProjectToProvider(newItem.Model.Id)) continue;
                        _deviceProvider.Add((IControllerDeviceModel)newItem.Model);
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    // Items removed
                    if (e.OldItems == null) return;
                    foreach (IControllerDeviceViewModel oldItem in e.OldItems)
                    {
                        _deviceProvider.Remove((IControllerDeviceModel)oldItem.Model);
                    }
                    break;

                case NotifyCollectionChangedAction.Replace:
                    // Some items replaced
                    if (e.OldItems == null) return;
                    foreach (IControllerDeviceViewModel oldItem in e.OldItems)
                    {
                        _deviceProvider.Remove((IControllerDeviceModel)oldItem.Model);
                    }
                    if (e.NewItems == null) return;
                    foreach (IControllerDeviceViewModel newItem in e.NewItems)
                    {
                        if (!ShouldProjectToProvider(newItem.Model.Id)) continue;
                        _deviceProvider.Add((IControllerDeviceModel)newItem.Model);
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:
                    // The whole list is refreshed
                    ViewModelProvider.Clear();
                    foreach (var item in _deviceProvider.OfType<IControllerDeviceModel>())
                    {
                        ViewModelProvider.Add(new ControllerDeviceViewModel(item));
                    }

                    break;
            }
        }
        finally { _isSyncingFromProvider = false; }
    }

    /// <summary>
    /// (FR-D1) provider→panel 역방향 동기화. NATS SYNC_DEVICE 등으로 _deviceProvider(캐시)가
    /// 변경되면 열린 패널의 ViewModelProvider(DataGrid)에 즉시 반영한다.
    /// - Id≤0(Draft) 격리, Id 기준 멱등(존재 시 Add no-op / 부재 시 Remove no-op)으로 이중행·잔류 방지.
    /// - 순방향(패널發) 전파 중이면 _isSyncingFromProvider 가드로 skip(무한루프 차단).
    /// - Reset/Replace는 다루지 않는다(전량 재구성은 Reload/Save/Delete의 DataInitialize가 담당하며
    ///   그 구간에는 본 핸들러가 구독 해제됨). 구독 수명은 순방향 핸들러와 동일 3지점에서 토글.
    /// - 본 핸들러는 EntityCollectionProvider의 UI 스레드 마샬(DispatcherService.Invoke) 안에서 발화되므로 UI 스레드에서 실행된다.
    /// </summary>
    private void DeviceProvider_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_isSyncingFromProvider) return;
        _isSyncingFromProvider = true;
        try
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems == null) return;
                    foreach (IControllerDeviceModel m in e.NewItems.OfType<IControllerDeviceModel>())
                    {
                        if (m.Id <= 0) continue;                                     // Draft 격리
                        if (ViewModelProvider.Any(vm => vm.Model.Id == m.Id)) continue; // 이미 존재 → no-op
                        ViewModelProvider.Add(new ControllerDeviceViewModel((ControllerDeviceModel)m) { Index = ViewModelProvider.Count + 1 });
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems == null) return;
                    foreach (IControllerDeviceModel m in e.OldItems.OfType<IControllerDeviceModel>())
                    {
                        var vm = ViewModelProvider.FirstOrDefault(v => v.Model.Id == m.Id);
                        if (vm != null) ViewModelProvider.Remove(vm);
                    }
                    break;
            }
        }
        finally { _isSyncingFromProvider = false; }
    }
    #endregion
    #region - Binding Methods -
    /// <summary>(FR-EN-11) 역할강등 재평가 콜백 — NATS 배경스레드 발화 대응 OnUIThread 필수.</summary>
    private void OnPermissionsChanged()
    {
        Execute.OnUIThread(() =>
        {
            IsButtonEnable = DevicePermissionGate.CanEdit() || DevicePermissionGate.CanDelete();
            // SaveButtonEnable = DevicePermissionGate.CanEdit(); // removed: IsSaving 전용 플래그로 대체
        });
    }

    private static bool DeviceEquals(IControllerDeviceModel a, IControllerDeviceModel b)
    {
        return a.DeviceNumber == b.DeviceNumber &&
               (a.DeviceGroups ?? new()).SequenceEqual(b.DeviceGroups ?? new()) &&
               a.DeviceName == b.DeviceName &&
               a.DeviceType == b.DeviceType &&
               a.Version == b.Version &&
               a.Status == b.Status &&
               a.IpAddress == b.IpAddress &&
               a.Port == b.Port &&
               a.Location == b.Location &&
               a.Latitude == b.Latitude &&
               a.Longitude == b.Longitude &&
               a.Heading == b.Heading &&
               a.Altitude == b.Altitude &&
               a.IsEnable == b.IsEnable;
    }

    #endregion
    #region - Helper Methods -
    /// <summary>
    /// GOP API를 통해 Controller 목록 조회하고 Model로 변환
    /// </summary>
    // (#11/FR-3 + 리뷰 MEDIUM) 전 페이지 순회 + 완전성 신호. 기존 limit:20 단건은 20건 초과 누락.
    //   중간 페이지 실패 시 부분목록을 Save 비교에 쓰면 편집 소실 → complete=false 로 호출부가 보류 판단.
    private async Task<(List<ControllerDeviceModel> list, bool complete)> FetchControllersAsync(CancellationToken token = default)
    {
        var all = new List<ControllerDeviceModel>();
        try
        {
            const int limit = 100, maxPages = 100;
            for (int page = 1; page <= maxPages; page++)
            {
                var response = await _apiService.GetControllersAsync(page: page, limit: limit, token: token);
                if (!response.Success || response.Data == null)
                {
                    _log?.Error($"Failed to fetch controllers (page {page}): {response.Error?.Message}");
                    return (all, false);
                }
                var batch = response.Data.Select(dto => dto.ToControllerDeviceModel()).ToList();
                all.AddRange(batch);
                if (batch.Count < limit) return (all, true);   // 마지막 페이지 = 완전
            }
            return (all, true);
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in FetchControllersAsync: {ex.Message}");
            return (all, false);
        }
    }

    // (Temp-state) Create/Update 헬퍼 — ApiResultLite 반환(베이스 ExecuteCreate/SaveUpdates 연동).
    private async Task<ApiResultLite> CreateControllerAsync(ControllerDeviceModel model, CancellationToken token = default)
    {
        try
        {
            var r = await _apiService.CreateControllerAsync(model.ToControllerDeviceDto(), token);
            return new ApiResultLite(r.Success && r.Data != null, r.StatusCode, r.Error?.Details);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _log?.Error($"CreateControllerAsync: {ex.Message}"); return new ApiResultLite(false, 0, ex.Message); }
    }

    private async Task<ApiResultLite> UpdateControllerAsync(ControllerDeviceModel model, CancellationToken token = default)
    {
        try
        {
            var r = await _apiService.UpdateControllerAsync(model.Id, model.ToControllerDeviceDto(), token);
            return new ApiResultLite(r.Success, r.StatusCode, r.Error?.Details);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _log?.Error($"UpdateControllerAsync: {ex.Message}"); return new ApiResultLite(false, 0, ex.Message); }
    }
    #endregion
    #region - Processes -
    private Task DataInitialize(CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            try
            {
                // 1. UI 스레드에서 IsVisible = false 설정
                DispatcherService.Invoke(() => IsVisible = false);

                // 2. Render 우선순위까지 큐 소진 — ProgressCircle 렌더링 보장
                await DispatcherService.BeginInvoke(() => { }, DispatcherPriority.Render);

                ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;
                _deviceProvider.CollectionEntity.CollectionChanged -= DeviceProvider_CollectionChanged; // (FR-D1) 재구성 구간 역방향 정지
                await DispatcherService.BeginInvoke(() => ViewModelProvider.Clear());

                var items = _deviceProvider.OfType<IControllerDeviceModel>().ToList();
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
                            ViewModelProvider.Add(new ControllerDeviceViewModel((ControllerDeviceModel)item) { Index = startIndex + idx + 1 });
                    });
                    await DispatcherService.BeginInvoke(() => { }, DispatcherPriority.Render);
                }
                await DispatcherService.BeginInvoke(() => NotifyOfPropertyChange(() => ViewModelProvider));

                ViewModelProvider.CollectionChanged += CollectionEntity_CollectionChanged;
                _deviceProvider.CollectionEntity.CollectionChanged += DeviceProvider_CollectionChanged; // (FR-D1) 정상상태 역방향 재구독
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
    public async Task HandleAsync(CallDeleteControllerDeviceProcessMessageModel message, CancellationToken cancellationToken)
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
                    async (id, ct) => (await _apiService.DeleteControllerAsync(id, ct)).Success,
                    r => _deviceProvider.Remove((IControllerDeviceModel)r.Model),
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
    public event System.Action? UpdateAction;
    #endregion
    #region - Attributes -
    private readonly IDeviceApiService _apiService;
    private readonly ControllerDeviceProvider _deviceProvider;
    private readonly IDeviceProviderService _deviceProviderService;
    private bool _isSyncingFromProvider;   // (FR-D1) 순방향↔역방향 상호 재진입 가드(UI 스레드 전용)
    #endregion

}