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

public class EnclosureDevicePanelViewModel : BaseDataGridMultiPanelViewModel<EnclosureDeviceViewModel>
                                           , IHandle<CallDeleteEnclosureDeviceProcessMessageModel>
{
    #region - Ctors -
    public EnclosureDevicePanelViewModel(IEventAggregator eventAggregator
                                        , ILogService log
                                        , IDeviceApiService apiService
                                        , EnclosureDeviceProvider deviceProvider
                                        , IDeviceProviderService deviceProviderService
                                        ) : base(eventAggregator, log)
    {
        _apiService = apiService;
        _deviceProvider = deviceProvider;
        _deviceProviderService = deviceProviderService;
    }
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
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Explain = "선택한 함체 장비를 정말로 삭제하시겠습니까?",
            MessageModel = new CallDeleteEnclosureDeviceProcessMessageModel()
        });
    }

    public override async void OnClickInsertButton(object sender, RoutedEventArgs e)
    {
        // (FR-EN-09) 추가 권한 게이트
        if (!DevicePermissionGate.CanEdit()) { _log?.Warning("[FR-EN-09] 추가 권한 없음(devices)"); return; }
        if (!await _processGate.WaitAsync(0)) return;
        try
        {
            // (Temp-state) 로컬 Draft 추가만 — 서버 미반영. 입력 후 Save 시 등록.
            //   Draft(Id≤0)는 ViewModelProvider에만(provider 미진입, CollectionChanged 가드).
            var existing = ViewModelProvider.Select(vm => vm.Model.DeviceNumber).ToHashSet();
            int number = 1; while (existing.Contains(number)) number++;
            var model = new EnclosureDeviceModel { DeviceNumber = number, DeviceName = $"새 함체 {number}" };
            ViewModelProvider.Add(new EnclosureDeviceViewModel(model));
        }
        finally { _processGate.Release(); }
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
            await _deviceProviderService.FetchAllDevicesAsync(_pCancellationTokenSource!.Token);
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
        // (FR-EN-09) 저장 권한 게이트
        if (!DevicePermissionGate.CanEdit()) { _log?.Warning("[FR-EN-09] 저장 권한 없음(devices)"); return; }
        if (!await _processGate.WaitAsync(0)) return;
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
            var (serverList, complete) = await FetchEnclosuresAsync(token);
            if (!complete)
            {
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "저장 보류",
                    Explain = "서버 함체 목록을 완전히 불러오지 못해 저장을 보류합니다. 잠시 후 다시 시도하세요."
                });
                return;
            }
            var failures = new List<string>();
            var draftVMs = ViewModelProvider.Where(vm => vm.Model.Id <= 0).ToList();
            var committed = new List<EnclosureDeviceViewModel>();

            // 루프A: Draft 생성 (서버 필수필드 없음 — 항상 생성 시도)
            int held = await ExecuteCreateAsync(
                draftVMs,
                vm => true  /* 서버 필수필드 없음(ip_port 없음) */,
                (vm, ct) => CreateEnclosureAsync((EnclosureDeviceModel)vm.Model, ct),
                vm => committed.Add(vm),
                vm => $"{vm.Model.DeviceName}(신규)",
                failures, token);

            // 루프B: Id>0 변경분 Update
            var updateVMs = ViewModelProvider.Where(vm =>
            {
                if (vm.Model.Id <= 0) return false;
                var srv = serverList.FirstOrDefault(s => s.Id == vm.Model.Id);
                return srv != null && !DeviceEquals((IEnclosureDeviceModel)vm.Model, srv);
            }).ToList();
            await ExecuteSaveUpdatesAsync(
                updateVMs,
                (vm, ct) => UpdateEnclosureAsync((EnclosureDeviceModel)vm.Model, ct),
                vm => $"{vm.Model.DeviceName}(Id={vm.Model.Id})",
                failures, token);

            // 재조회 + 재구성 + 생존자(실패/보류 Draft) 복원
            await _deviceProviderService.FetchAllDevicesAsync(token);
            await DataInitialize(token);
            foreach (var d in draftVMs.Except(committed)) { d.Index = ViewModelProvider.Count + 1; ViewModelProvider.Add(d); }

            await NotifySaveResultAsync("함체 저장 안내", failures, held, token);
            await Task.Delay(2000, token);
        }
        catch (TaskCanceledException ex) { _log?.Warning(ex.Message); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log?.Error(ex.Message); }
        finally
        {
            UpdateAction?.Invoke();
            IsSaving = false;
            _processGate.Release();
        }
    }

    public override void OnSelectionChanged(IList<EnclosureDeviceViewModel> rows)
    {
        SelectedItems = rows;
        rows.ToList().ForEach(entity => entity.IsSelected = true);
        NotifyOfPropertyChange(() => SelectedItems);
    }

    // (Draft 경고) 미저장 Draft(Id≤0) 개수 — 화면 전환/종료 시 손실 경고용.
    protected override int CountUnsavedDrafts() => ViewModelProvider.Count(vm => vm.Model.Id <= 0);

    private void CollectionEntity_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                // (Draft 격리) Id≤0 미저장 Draft는 provider 미투영(공유 오염/이중행 차단).
                if (e.NewItems == null) return;
                foreach (IEnclosureDeviceViewModel newItem in e.NewItems)
                {
                    if (!ShouldProjectToProvider(newItem.Model.Id)) continue;
                    _deviceProvider.Add((IEnclosureDeviceModel)newItem.Model);
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems == null) return;
                foreach (IEnclosureDeviceViewModel oldItem in e.OldItems)
                    _deviceProvider.Remove((IEnclosureDeviceModel)oldItem.Model);
                break;
            case NotifyCollectionChangedAction.Replace:
                if (e.OldItems != null)
                    foreach (IEnclosureDeviceViewModel oldItem in e.OldItems)
                        _deviceProvider.Remove((IEnclosureDeviceModel)oldItem.Model);
                if (e.NewItems != null)
                    foreach (IEnclosureDeviceViewModel newItem in e.NewItems)
                    {
                        if (!ShouldProjectToProvider(newItem.Model.Id)) continue;
                        _deviceProvider.Add((IEnclosureDeviceModel)newItem.Model);
                    }
                break;
            case NotifyCollectionChangedAction.Reset:
                ViewModelProvider.Clear();
                foreach (var item in _deviceProvider.OfType<IEnclosureDeviceModel>())
                    ViewModelProvider.Add(new EnclosureDeviceViewModel(item));
                break;
        }
    }
    #endregion
    #region - Binding Methods -
    /// <summary>(FR-EN-11) 역할강등 재평가 콜백 — NATS 배경스레드 발화 대응 OnUIThread 필수.</summary>
    private void OnPermissionsChanged()
    {
        Execute.OnUIThread(() =>
        {
            IsButtonEnable = DevicePermissionGate.CanEdit() || DevicePermissionGate.CanDelete();
            // SaveButtonEnable = DevicePermissionGate.CanEdit(); // IsSaving 전용 플래그로 대체됨
        });
    }

    internal static bool DeviceEquals(IEnclosureDeviceModel a, IEnclosureDeviceModel b)
    {
        return a.DeviceNumber == b.DeviceNumber &&
        (a.DeviceGroups ?? new()).SequenceEqual(b.DeviceGroups ?? new()) &&
        a.DeviceName == b.DeviceName &&
        a.DeviceType == b.DeviceType &&
        a.Version == b.Version &&
        a.Status == b.Status &&
        a.DoorStatus == b.DoorStatus &&
        a.HeaterEnabled == b.HeaterEnabled &&
        a.FanEnabled == b.FanEnabled &&
        a.IsEnable == b.IsEnable &&
        a.Location == b.Location &&
        a.Latitude == b.Latitude &&
        a.Longitude == b.Longitude &&
        a.Heading == b.Heading &&
        a.Altitude == b.Altitude &&
        ThresholdEquals(a.ThresholdConfig, b.ThresholdConfig);   // 임계값 변경 감지(없으면 Update 미트리거)
    }

    // null 설정과 '모든 필드 null'인 빈 설정을 동등 취급(다이얼로그가 빈 임계값 생성 시 허위 변경감지 방지)
    private static bool ThresholdEquals(IEnclosureThresholdConfigModel? a, IEnclosureThresholdConfigModel? b)
    {
        return a?.TempHigh == b?.TempHigh
            && a?.TempLow == b?.TempLow
            && a?.HumidityHigh == b?.HumidityHigh
            && a?.CurrentHigh == b?.CurrentHigh
            && a?.VoltageLow == b?.VoltageLow
            && a?.VibrationHigh == b?.VibrationHigh;
    }
    #endregion
    #region - Helper Methods -
    // (Temp-state) Create/Update 헬퍼 — ApiResultLite 반환(베이스 ExecuteCreate/SaveUpdates 연동).
    private async Task<ApiResultLite> CreateEnclosureAsync(EnclosureDeviceModel model, CancellationToken token = default)
    {
        try
        {
            var r = await _apiService.CreateEnclosureAsync(model.ToEnclosureDeviceDto(), token);
            return new ApiResultLite(r.Success && r.Data != null, r.StatusCode, r.Error?.Details);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _log?.Error($"CreateEnclosureAsync: {ex.Message}"); return new ApiResultLite(false, 0, ex.Message); }
    }

    private async Task<ApiResultLite> UpdateEnclosureAsync(EnclosureDeviceModel model, CancellationToken token = default)
    {
        try
        {
            var r = await _apiService.UpdateEnclosureAsync(model.Id, model.ToEnclosureDeviceDto(), token);
            return new ApiResultLite(r.Success, r.StatusCode, r.Error?.Details);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _log?.Error($"UpdateEnclosureAsync: {ex.Message}"); return new ApiResultLite(false, 0, ex.Message); }
    }

    // (P2-S4) 전 페이지 순회 + 완전성 신호.
    private async Task<(List<EnclosureDeviceModel> list, bool complete)> FetchEnclosuresAsync(CancellationToken token = default)
    {
        var all = new List<EnclosureDeviceModel>();
        try
        {
            const int limit = 100, maxPages = 100;
            for (int page = 1; page <= maxPages; page++)
            {
                var response = await _apiService.GetEnclosuresAsync(page: page, limit: limit, token: token);
                if (!response.Success || response.Data == null)
                {
                    _log?.Error($"Failed to fetch enclosures (page {page}): {response.Error?.Message}");
                    return (all, false);
                }
                var batch = response.Data.Select(dto => dto.ToEnclosureDeviceModel()).ToList();
                all.AddRange(batch);
                if (batch.Count < limit) return (all, true);
            }
            return (all, true);
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in FetchEnclosuresAsync: {ex.Message}");
            return (all, false);
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

                var items = _deviceProvider.OfType<IEnclosureDeviceModel>().ToList();
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
                            ViewModelProvider.Add(new EnclosureDeviceViewModel((EnclosureDeviceModel)item) { Index = startIndex + idx + 1 });
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
    #region - IHandles -
    public async Task HandleAsync(CallDeleteEnclosureDeviceProcessMessageModel message, CancellationToken cancellationToken)
    {
        // (P2-S3) 삭제 _processGate 직렬화 + 베이스 ExecuteDeleteAsync(Id<=0 로컬/Id>0 verify-after-success/부분실패 통지).
        if (!await _processGate.WaitAsync(0)) return;
        try
        {
            await _eventAggregator.PublishOnCurrentThreadAsync(
                new OpenProgressPopupMessageModel(), cancellationToken);

            await Task.Run(async () =>
            {
                await ExecuteDeleteAsync(
                    SelectedItems.ToList(),
                    r => r.Model.Id,
                    async (id, ct) => (await _apiService.DeleteEnclosureAsync(id, ct)).Success,
                    r => _deviceProvider.Remove((IEnclosureDeviceModel)r.Model),
                    cancellationToken);
            }, cancellationToken);

            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();
            }

            await _deviceProviderService.FetchAllDevicesAsync(_cancellationTokenSource!.Token);
            await DataInitialize(_cancellationTokenSource!.Token).ConfigureAwait(false);
            UpdateAction?.Invoke();

            await _eventAggregator.PublishOnCurrentThreadAsync(
                new ClosePopupMessageModel(), cancellationToken);
        }
        finally { _processGate.Release(); }
    }
    #endregion
    #region - Properties -
    public event System.Action? UpdateAction;
    #endregion
    #region - Attributes -
    private readonly IDeviceApiService _apiService;
    private readonly EnclosureDeviceProvider _deviceProvider;
    private readonly IDeviceProviderService _deviceProviderService;
    #endregion
}
