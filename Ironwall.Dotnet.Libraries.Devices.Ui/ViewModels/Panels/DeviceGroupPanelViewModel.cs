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
using System.Windows.Threading;

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
        // (FR-EN-11) 역할강등 재평가 구독
        { var _pgs = DevicePermissionGate.Resolve(); if (_pgs != null) _pgs.PermissionsChanged += OnPermissionsChanged; }
        OnPermissionsChanged();   // (FR-EN-09 초기화 수정) 로그인 후 활성화 시 PermissionsChanged 이미 발화됨 → 초기 IsButtonEnable/SaveButtonEnable 권한 미반영(버튼 살아있는 버그). 활성화 시 1회 계산.
        // (리뷰 MEDIUM) 초기 로딩 DataInitialize 도 _processGate 로 직렬화 → 로딩 중 Insert/Save/Delete 경합 차단.
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
            Explain = "선택한 장비그룹을 삭제하시겠습니까?",
            MessageModel = new CallDeleteDeviceGroupProcessMessageModel()
        });
    }

    public override async void OnClickInsertButton(object sender, RoutedEventArgs e)
    {
        // (FR-EN-09) 추가 권한 게이트
        if (!DevicePermissionGate.CanEdit()) { _log?.Warning("[FR-EN-09] 추가 권한 없음(devices)"); return; }
        // (C1) Save 진행 중 Insert 끼어들기 경합 차단 — _processGate로 직렬화(타 핸들러와 동일).
        if (!await _processGate.WaitAsync(0)) return;
        try
        {
            // (Temp-state) 로컬 Draft 추가만 — 서버 미반영. 이름/설명 입력 후 Save 시 등록. Draft는 ViewModelProvider에만.
            var existing = ViewModelProvider.Select(vm => vm.Model.Name).ToHashSet();
            var name = "새 그룹";
            for (int n = 2; existing.Contains(name); n++) name = $"새 그룹 ({n})";
            var model = new DeviceGroupModel { Name = name, Description = string.Empty };
            ViewModelProvider.Add(new DeviceGroupViewModel(model));
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
            var token = _pCancellationTokenSource!.Token;

            // (FR-B4) Reload = 서버 재조회(swap-on-success, 전 페이지). 완전 성공 시에만 _deviceGroupProvider를
            //   서버 기준으로 재구성(유령 Id=0 포함 청소). 불완전/실패 시 기존 목록 유지 + 알림.
            var (serverList, complete) = await FetchDeviceGroupsAsync(token);
            if (complete)
            {
                _deviceGroupProvider.Clear();
                foreach (var s in serverList) _deviceGroupProvider.Add(s);
            }
            else
            {
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "새로고침 실패",
                    Explain = "장비그룹을 완전히 불러오지 못했습니다. 서버 상태를 확인하세요. 기존 목록을 유지합니다."
                });
            }

            await DataInitialize(token);
            await Task.Delay(2000, token);
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

            // (Temp-state) Save = Create(Draft Id≤0) + Update(Id>0 변경분). Draft는 ViewModelProvider에만.
            var (serverList, complete) = await FetchDeviceGroupsAsync(token);
            if (!complete)
            {
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "저장 보류",
                    Explain = "서버 그룹 목록을 완전히 불러오지 못해 저장을 보류합니다. 잠시 후 다시 시도하세요."
                });
                return;
            }
            var failures = new List<string>();
            var draftVMs = ViewModelProvider.Where(vm => vm.Model.Id <= 0).ToList();
            var committed = new List<DeviceGroupViewModel>();

            // 루프A: Draft 그룹 생성 (그룹은 서버참조 필수필드 없음 — 이름만)
            int held = await ExecuteCreateAsync(
                draftVMs,
                vm => true,
                (vm, ct) => CreateDeviceGroupLite((DeviceGroupModel)vm.Model, ct),
                vm => committed.Add(vm),
                vm => $"{vm.Model.Name}(신규)",
                failures, token);

            // 루프B: Id>0 변경분 Update
            var updateVMs = ViewModelProvider.Where(vm =>
            {
                if (vm.Model.Id <= 0) return false;
                var srv = serverList.FirstOrDefault(s => s.Id == vm.Model.Id);
                return srv != null && !DeviceGroupEquals(vm.Model, srv);
            }).ToList();
            await ExecuteSaveUpdatesAsync(
                updateVMs,
                (vm, ct) => UpdateDeviceGroupLite((DeviceGroupModel)vm.Model, ct),
                vm => $"{vm.Model.Name}(Id={vm.Model.Id})",
                failures, token);

            // 독립 provider 재동기 — 서버 재조회 후 swap-on-success(Clear+rebuild) → DataInitialize → 생존자 복원
            var (refreshed, ok2) = await FetchDeviceGroupsAsync(token);
            if (ok2)
            {
                _deviceGroupProvider.Clear();
                foreach (var g in refreshed) _deviceGroupProvider.Add(g);
            }
            await DataInitialize(token);
            foreach (var d in draftVMs.Except(committed)) { d.Index = ViewModelProvider.Count + 1; ViewModelProvider.Add(d); }

            await NotifySaveResultAsync("그룹 저장 안내", failures, held, token);
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

    // (Temp-state) Create/Update 헬퍼 — ApiResultLite 반환.
    private async Task<ApiResultLite> CreateDeviceGroupLite(DeviceGroupModel model, CancellationToken token)
    {
        try
        {
            var r = await _apiService.CreateDeviceGroupAsync(model.ToDeviceGroupDto(), token);
            if (r.Success && r.Data != null)
                model.Id = r.Data.ToDeviceGroupModel().Id;   // (R2/H2) 서버 Id write-back — 선택 에디터의 stale Id(0) 방지 → CanAddAssign 정상화
            return new ApiResultLite(r.Success && r.Data != null, r.StatusCode, r.Error?.Details);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _log?.Error($"CreateDeviceGroupLite: {ex.Message}"); return new ApiResultLite(false, 0, ex.Message); }
    }

    private async Task<ApiResultLite> UpdateDeviceGroupLite(DeviceGroupModel model, CancellationToken token)
    {
        try
        {
            var r = await _apiService.UpdateDeviceGroupAsync(model.Id, model.ToDeviceGroupDto(), token);
            return new ApiResultLite(r.Success, r.StatusCode, r.Error?.Details);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _log?.Error($"UpdateDeviceGroupLite: {ex.Message}"); return new ApiResultLite(false, 0, ex.Message); }
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
                // (Draft 격리) Id≤0 미저장 Draft는 provider 미투영.
                if (e.NewItems == null) return;
                foreach (IDeviceGroupViewModel newItem in e.NewItems)
                {
                    if (!ShouldProjectToProvider(newItem.Model.Id)) continue;
                    _deviceGroupProvider.Add(newItem.Model);
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems == null) return;
                foreach (IDeviceGroupViewModel oldItem in e.OldItems)
                    _deviceGroupProvider.Remove(oldItem.Model);
                break;
            case NotifyCollectionChangedAction.Replace:
                if (e.OldItems != null)
                    foreach (IDeviceGroupViewModel oldItem in e.OldItems)
                        _deviceGroupProvider.Remove(oldItem.Model);
                if (e.NewItems != null)
                    foreach (IDeviceGroupViewModel newItem in e.NewItems)
                    {
                        if (!ShouldProjectToProvider(newItem.Model.Id)) continue;   // (Draft 격리)
                        _deviceGroupProvider.Add(newItem.Model);
                    }
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
    // 전 페이지 순회 + 완전성 신호. (리뷰 MEDIUM) 단일 page:1,limit:100 은 100건 초과 누락 + 중간 페이지
    //   실패를 '정상 빈/부분'과 구분 못 함 → complete=false 로 호출부가 Save 보류/Reload 미스왑 판단.
    private async Task<(List<DeviceGroupModel> list, bool complete)> FetchDeviceGroupsAsync(CancellationToken token = default)
    {
        var all = new List<DeviceGroupModel>();
        try
        {
            const int limit = 100, maxPages = 100;
            for (int page = 1; page <= maxPages; page++)
            {
                var response = await _apiService.GetDeviceGroupsAsync(page: page, limit: limit, token: token);
                if (!response.Success || response.Data == null)
                {
                    _log?.Error($"Failed to fetch device groups (page {page}): {response.Error?.Message}");
                    return (all, false);
                }
                var batch = response.Data.Select(dto => dto.ToDeviceGroupModel()).ToList();
                all.AddRange(batch);
                if (batch.Count < limit) return (all, true);   // 마지막 페이지 = 완전
            }
            return (all, true);
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in FetchDeviceGroupsAsync: {ex.Message}");
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

                var items = _deviceGroupProvider.OfType<IDeviceGroupModel>()
                    .Where(m => m.Id > 0).ToList();
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
                            ViewModelProvider.Add(new DeviceGroupViewModel(item) { Index = startIndex + idx + 1 });
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
    public async Task HandleAsync(CallDeleteDeviceGroupProcessMessageModel message, CancellationToken cancellationToken)
    {
        // (FR-EN-09) DeviceGroup 삭제는 인라인 루프 — OnClickDeleteButton 게이트 우회 가능, 여기서도 재확인.
        if (!DevicePermissionGate.CanDelete()) { _log?.Warning("[FR-EN-09] 삭제 권한 없음(devices/group-handle)"); return; }
        // (리뷰 LOW) 삭제도 Insert/Save/Reload/OnActivate 와 동일 _processGate 로 직렬화 — 공유 CTS 조기취소/경합 방지.
        if (!await _processGate.WaitAsync(0)) return;
        try
        {
        await _eventAggregator.PublishOnCurrentThreadAsync(
            new OpenProgressPopupMessageModel(),
            cancellationToken);

        var failures = new List<string>();
        await Task.Run(async () =>
        {
            foreach (var item in SelectedItems.ToList())
            {
                var model = (IDeviceGroupModel)item.Model;
                if (model.Id <= 0)
                {
                    _deviceGroupProvider.Remove(model);   // 잔존 pending 로컬 제거
                    continue;
                }
                var response = await _apiService.DeleteDeviceGroupAsync(model.Id, cancellationToken);
                if (response.Success)
                {
                    _deviceGroupProvider.Remove(model);   // (FR-B2) verify-after-success: 성공 시에만 로컬 제거 (#2 desync 해소)
                }
                else
                {
                    failures.Add($"{model.Name}(Id={model.Id})");
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

        if (failures.Count > 0)
        {
            await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            {
                Title = "그룹 삭제 일부 실패",
                Explain = $"{failures.Count}건 삭제에 실패했습니다. 목록은 서버 기준으로 유지됩니다.\n" + string.Join("\n", failures)
            });
        }
        }
        finally { _processGate.Release(); }
    }
    #endregion
    #region - Properties -
    public event System.Action? UpdateAction;
    #endregion
    #region - Helpers -
    /// <summary>(FR-EN-11) 역할강등 재평가 콜백 — NATS 배경스레드 발화 대응 OnUIThread 필수.</summary>
    private void OnPermissionsChanged()
    {
        Execute.OnUIThread(() =>
        {
            IsButtonEnable = DevicePermissionGate.CanEdit() || DevicePermissionGate.CanDelete();
            // SaveButtonEnable = DevicePermissionGate.CanEdit(); // 제거: IsSaving 플래그로 전환됨
        });
    }

    // (PR-C) 미저장 Draft(Id≤0) 개수 — 화면 전환/종료 시 미저장 손실 경고용.
    protected override int CountUnsavedDrafts() => ViewModelProvider.Count(vm => vm.Model.Id <= 0);

    internal static bool DeviceGroupEquals(IDeviceGroupModel a, IDeviceGroupModel b)
        => a.Name == b.Name && a.Description == b.Description;
    #endregion
    #region - Attributes -
    private readonly IDeviceApiService _apiService;
    private readonly DeviceGroupProvider _deviceGroupProvider;
    #endregion
}
