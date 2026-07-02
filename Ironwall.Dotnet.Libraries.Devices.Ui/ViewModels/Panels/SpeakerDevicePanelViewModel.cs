using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Servers;
using System;
using System.Collections.Specialized;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;

public class SpeakerDevicePanelViewModel : BaseDataGridMultiPanelViewModel<SpeakerDeviceViewModel>
                                        , IHandle<CallDeleteSpeakerDeviceProcessMessageModel>
{
    #region - Ctors -
    public SpeakerDevicePanelViewModel(IEventAggregator eventAggregator
                                      , ILogService log
                                      , IDeviceApiService apiService
                                      , SpeakerDeviceProvider deviceProvider
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
            Explain = "선택한 스피커 장비를 정말로 삭제하시겠습니까?",
            MessageModel = new CallDeleteSpeakerDeviceProcessMessageModel()
        });
    }

    public override async void OnClickInsertButton(object sender, RoutedEventArgs e)
    {
        // (FR-EN-09) 추가 권한 게이트
        if (!DevicePermissionGate.CanEdit()) { _log?.Warning("[FR-EN-09] 추가 권한 없음(devices)"); return; }
        if (!await _processGate.WaitAsync(0)) return;
        try
        {
            // (Temp-state) 로컬 Draft 추가만 — 서버 미반영. 이름/속성 입력 후 Save 시 등록.
            //   Draft(Id≤0)는 ViewModelProvider에만(provider 미진입, CollectionChanged 가드).
            var existing = ViewModelProvider.Select(vm => vm.Model.DeviceNumber).ToHashSet();
            int number = 1; while (existing.Contains(number)) number++;
            var model = new SpeakerDeviceModel { DeviceNumber = number, DeviceName = $"새 스피커 {number}" };

            // (D3) 기본 방송서버 자동배정 — 첫 서버(최소 Id). 서버 0개면 Inform 안내(serverless 등록은 API상 허용).
            var servers = IoC.Get<ServerProvider>().OfType<IServerModel>().OrderBy(s => s.Id).ToList();
            if (servers.Count > 0)
                model.Server = servers[0];
            else
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "방송서버 없음",
                    Explain = "등록된 방송서버가 없습니다. 서버를 먼저 등록한 뒤 스피커에 배정하세요."
                });

            ViewModelProvider.Add(new SpeakerDeviceViewModel(model));
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
            SaveButtonEnable = false;
            if (_pCancellationTokenSource != null && !_pCancellationTokenSource.IsCancellationRequested)
            {
                _pCancellationTokenSource.Cancel();
                _pCancellationTokenSource.Dispose();
            }
            _pCancellationTokenSource = new CancellationTokenSource();
            var token = _pCancellationTokenSource.Token;

            // (Temp-state) Save = Create(Draft Id≤0) + Update(Id>0 변경분). Draft는 ViewModelProvider에만 존재.
            var (serverList, complete) = await FetchSpeakersAsync(token);
            if (!complete)
            {
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "저장 보류",
                    Explain = "서버 스피커 목록을 완전히 불러오지 못해 저장을 보류합니다. 잠시 후 다시 시도하세요."
                });
                return;
            }
            var failures = new List<string>();
            var draftVMs = ViewModelProvider.Where(vm => vm.Model.Id <= 0).ToList();
            var committed = new List<SpeakerDeviceViewModel>();

            // 루프A: Draft 생성 (서버 필수필드 없음 → 무조건 생성 시도)
            int held = await ExecuteCreateAsync(
                draftVMs,
                vm => true  /* 서버 필수필드 없음 */,
                (vm, ct) => CreateSpeakerAsync((SpeakerDeviceModel)vm.Model, ct),
                vm => committed.Add(vm),
                vm => $"{vm.Model.DeviceName}(신규)",
                failures, token);

            // 루프B: Id>0 변경분 Update
            var updateVMs = ViewModelProvider.Where(vm =>
            {
                if (vm.Model.Id <= 0) return false;
                var srv = serverList.FirstOrDefault(s => s.Id == vm.Model.Id);
                return srv != null && !DeviceEquals((ISpeakerDeviceModel)vm.Model, srv);
            }).ToList();
            await ExecuteSaveUpdatesAsync(
                updateVMs,
                (vm, ct) => UpdateSpeakerAsync((SpeakerDeviceModel)vm.Model, ct),
                vm => $"{vm.Model.DeviceName}(Id={vm.Model.Id})",
                failures, token);

            // 재조회 + 재구성 + 생존자(실패/보류 Draft) 복원
            await _deviceProviderService.FetchAllDevicesAsync(token);
            await DataInitialize(token);
            foreach (var d in draftVMs.Except(committed)) { d.Index = ViewModelProvider.Count + 1; ViewModelProvider.Add(d); }

            await NotifySaveResultAsync("스피커 저장 안내", failures, held, token);
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

    public override void OnSelectionChanged(IList<SpeakerDeviceViewModel> rows)
    {
        SelectedItems = rows;
        rows.ToList().ForEach(entity => entity.IsSelected = true);
        NotifyOfPropertyChange(() => SelectedItems);
    }

    // (Temp-state) 화면 전환/종료 시 미저장 Draft(Id≤0) 손실 경고용 — 베이스 가드 override.
    protected override int CountUnsavedDrafts() => ViewModelProvider.Count(vm => vm.Model.Id <= 0);

    private void CollectionEntity_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                // (Draft 격리) Id≤0 미저장 Draft는 provider 미투영(공유 오염/이중행 차단).
                if (e.NewItems == null) return;
                foreach (ISpeakerDeviceViewModel newItem in e.NewItems)
                {
                    if (!ShouldProjectToProvider(newItem.Model.Id)) continue;
                    _deviceProvider.Add((ISpeakerDeviceModel)newItem.Model);
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems == null) return;
                foreach (ISpeakerDeviceViewModel oldItem in e.OldItems)
                    _deviceProvider.Remove((ISpeakerDeviceModel)oldItem.Model);
                break;
            case NotifyCollectionChangedAction.Replace:
                if (e.OldItems != null)
                    foreach (ISpeakerDeviceViewModel oldItem in e.OldItems)
                        _deviceProvider.Remove((ISpeakerDeviceModel)oldItem.Model);
                if (e.NewItems != null)
                    foreach (ISpeakerDeviceViewModel newItem in e.NewItems)
                    {
                        if (!ShouldProjectToProvider(newItem.Model.Id)) continue;
                        _deviceProvider.Add((ISpeakerDeviceModel)newItem.Model);
                    }
                break;
            case NotifyCollectionChangedAction.Reset:
                ViewModelProvider.Clear();
                foreach (var item in _deviceProvider.OfType<ISpeakerDeviceModel>())
                    ViewModelProvider.Add(new SpeakerDeviceViewModel(item));
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
            SaveButtonEnable = DevicePermissionGate.CanEdit();
        });
    }

    internal static bool DeviceEquals(ISpeakerDeviceModel a, ISpeakerDeviceModel b)
    {
        return a.DeviceNumber == b.DeviceNumber &&
        (a.DeviceGroups ?? new()).SequenceEqual(b.DeviceGroups ?? new()) &&
        a.DeviceName == b.DeviceName &&
        a.DeviceType == b.DeviceType &&
        a.Version == b.Version &&
        a.Status == b.Status &&
        a.SpeakerType == b.SpeakerType &&
        a.Description == b.Description &&
        a.IsEnable == b.IsEnable &&
        a.Location == b.Location &&
        a.Latitude == b.Latitude &&
        a.Longitude == b.Longitude &&
        a.Heading == b.Heading &&
        a.Altitude == b.Altitude &&
        a.Server?.Id == b.Server?.Id;   // 방송서버 변경 감지(int? Nullable, ?? 0 금지 — null/0 동치화 방지)
    }
    #endregion
    #region - Helper Methods -
    // (Temp-state) Create/Update 헬퍼 — ApiResultLite 반환(베이스 ExecuteCreate/SaveUpdates 연동).
    private async Task<ApiResultLite> CreateSpeakerAsync(SpeakerDeviceModel model, CancellationToken token = default)
    {
        try
        {
            var r = await _apiService.CreateSpeakerAsync(model.ToSpeakerDeviceDto(), token);
            return new ApiResultLite(r.Success && r.Data != null, r.StatusCode, r.Error?.Details);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _log?.Error($"CreateSpeakerAsync: {ex.Message}"); return new ApiResultLite(false, 0, ex.Message); }
    }

    private async Task<ApiResultLite> UpdateSpeakerAsync(SpeakerDeviceModel model, CancellationToken token = default)
    {
        try
        {
            var r = await _apiService.UpdateSpeakerAsync(model.Id, model.ToSpeakerDeviceDto(), token);
            return new ApiResultLite(r.Success, r.StatusCode, r.Error?.Details);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _log?.Error($"UpdateSpeakerAsync: {ex.Message}"); return new ApiResultLite(false, 0, ex.Message); }
    }

    // (P2-S4) 전 페이지 순회 + 완전성 신호. 단일 page:1,limit:100 은 100건 초과 누락 + 중간 페이지
    //   실패를 '정상 빈/부분'과 구분 못 함 → complete=false 로 Save 보류 판단.
    private async Task<(List<SpeakerDeviceModel> list, bool complete)> FetchSpeakersAsync(CancellationToken token = default)
    {
        var all = new List<SpeakerDeviceModel>();
        try
        {
            const int limit = 100, maxPages = 100;
            for (int page = 1; page <= maxPages; page++)
            {
                var response = await _apiService.GetSpeakersAsync(page: page, limit: limit, token: token);
                if (!response.Success || response.Data == null)
                {
                    _log?.Error($"Failed to fetch speakers (page {page}): {response.Error?.Message}");
                    return (all, false);
                }
                var batch = response.Data.Select(dto => dto.ToSpeakerDeviceModel()).ToList();
                all.AddRange(batch);
                if (batch.Count < limit) return (all, true);   // 마지막 페이지 = 완전
            }
            return (all, true);
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in FetchSpeakersAsync: {ex.Message}");
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

                var items = _deviceProvider.OfType<ISpeakerDeviceModel>().ToList();
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
                            ViewModelProvider.Add(new SpeakerDeviceViewModel((SpeakerDeviceModel)item) { Index = startIndex + idx + 1 });
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
    public async Task HandleAsync(CallDeleteSpeakerDeviceProcessMessageModel message, CancellationToken cancellationToken)
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
                    async (id, ct) => (await _apiService.DeleteSpeakerAsync(id, ct)).Success,
                    r => _deviceProvider.Remove((ISpeakerDeviceModel)r.Model),
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
    private readonly SpeakerDeviceProvider _deviceProvider;
    private readonly IDeviceProviderService _deviceProviderService;
    #endregion
}
