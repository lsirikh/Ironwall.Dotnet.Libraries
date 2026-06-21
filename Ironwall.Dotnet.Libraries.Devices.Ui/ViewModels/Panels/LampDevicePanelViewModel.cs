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

public class LampDevicePanelViewModel : BaseDataGridMultiPanelViewModel<LampDeviceViewModel>
                                     , IHandle<CallDeleteLampDeviceProcessMessageModel>
{
    #region - Ctors -
    public LampDevicePanelViewModel(IEventAggregator eventAggregator
                                   , ILogService log
                                   , IDeviceApiService apiService
                                   , LampDeviceProvider deviceProvider
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
            Explain = "선택한 경고등 장비를 정말로 삭제하시겠습니까?",
            MessageModel = new CallDeleteLampDeviceProcessMessageModel()
        });
    }

    public override void OnClickInsertButton(object sender, RoutedEventArgs e)
    {
        // (Temp-state) 로컬 Draft 추가만 — 서버 미반영. IP/포트 등 입력 후 Save 시 등록.
        //   Draft(Id≤0)는 ViewModelProvider에만(provider 미진입, CollectionChanged 가드).
        var existing = ViewModelProvider.Select(vm => vm.Model.DeviceNumber).ToHashSet();
        int number = 1; while (existing.Contains(number)) number++;
        var model = new LampDeviceModel { DeviceNumber = number, DeviceName = $"새 경고등 {number}" };
        ViewModelProvider.Add(new LampDeviceViewModel(model));
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
            var (serverList, complete) = await FetchLampsAsync(token);
            if (!complete)
            {
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "저장 보류",
                    Explain = "서버 경고등 목록을 완전히 불러오지 못해 저장을 보류합니다. 잠시 후 다시 시도하세요."
                });
                return;
            }
            var failures = new List<string>();
            var draftVMs = ViewModelProvider.Where(vm => vm.Model.Id <= 0).ToList();
            var committed = new List<LampDeviceViewModel>();

            // 루프A: Draft 생성 (ip_port≥1 필수 — 미충족 시 보류)
            int held = await ExecuteCreateAsync(
                draftVMs,
                vm => ((ILampDeviceModel)vm.Model).IpPort >= 1,
                (vm, ct) => CreateLampAsync((LampDeviceModel)vm.Model, ct),
                vm => committed.Add(vm),
                vm => $"{vm.Model.DeviceName}(신규)",
                failures, token);

            // 루프B: Id>0 변경분 Update
            var updateVMs = ViewModelProvider.Where(vm =>
            {
                if (vm.Model.Id <= 0) return false;
                var srv = serverList.FirstOrDefault(s => s.Id == vm.Model.Id);
                return srv != null && !DeviceEquals((ILampDeviceModel)vm.Model, srv);
            }).ToList();
            await ExecuteSaveUpdatesAsync(
                updateVMs,
                (vm, ct) => UpdateLampAsync((LampDeviceModel)vm.Model, ct),
                vm => $"{vm.Model.DeviceName}(Id={vm.Model.Id})",
                failures, token);

            // 재조회 + 재구성 + 생존자(실패/보류 Draft) 복원
            await _deviceProviderService.FetchAllDevicesAsync(token);
            await DataInitialize(token);
            foreach (var d in draftVMs.Except(committed)) ViewModelProvider.Add(d);

            await NotifySaveResultAsync("경고등 저장 안내", failures, held, token);
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

    public override void OnSelectionChanged(IList<LampDeviceViewModel> rows)
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
                // (Draft 격리) Id≤0 미저장 Draft는 provider 미투영(공유 오염/이중행 차단).
                if (e.NewItems == null) return;
                foreach (ILampDeviceViewModel newItem in e.NewItems)
                {
                    if (!ShouldProjectToProvider(newItem.Model.Id)) continue;
                    _deviceProvider.Add((ILampDeviceModel)newItem.Model);
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems == null) return;
                foreach (ILampDeviceViewModel oldItem in e.OldItems)
                    _deviceProvider.Remove((ILampDeviceModel)oldItem.Model);
                break;
            case NotifyCollectionChangedAction.Reset:
                ViewModelProvider.Clear();
                foreach (var item in _deviceProvider.OfType<ILampDeviceModel>())
                    ViewModelProvider.Add(new LampDeviceViewModel(item));
                break;
        }
    }
    #endregion
    #region - Binding Methods -
    internal static bool DeviceEquals(ILampDeviceModel a, ILampDeviceModel b)
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
        a.Description == b.Description &&
        a.IsEnable == b.IsEnable &&
        a.Location == b.Location &&
        a.Latitude == b.Latitude &&
        a.Longitude == b.Longitude;
    }
    #endregion
    #region - Helper Methods -
    // (Temp-state) Create/Update 헬퍼 — ApiResultLite 반환(베이스 ExecuteCreate/SaveUpdates 연동).
    private async Task<ApiResultLite> CreateLampAsync(LampDeviceModel model, CancellationToken token = default)
    {
        try
        {
            var r = await _apiService.CreateLampAsync(model.ToLampDeviceDto(), token);
            return new ApiResultLite(r.Success && r.Data != null, r.StatusCode, r.Error?.Details);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _log?.Error($"CreateLampAsync: {ex.Message}"); return new ApiResultLite(false, 0, ex.Message); }
    }

    private async Task<ApiResultLite> UpdateLampAsync(LampDeviceModel model, CancellationToken token = default)
    {
        try
        {
            var r = await _apiService.UpdateLampAsync(model.Id, model.ToLampDeviceDto(), token);
            return new ApiResultLite(r.Success, r.StatusCode, r.Error?.Details);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _log?.Error($"UpdateLampAsync: {ex.Message}"); return new ApiResultLite(false, 0, ex.Message); }
    }

    // (P2-S4) 전 페이지 순회 + 완전성 신호.
    private async Task<(List<LampDeviceModel> list, bool complete)> FetchLampsAsync(CancellationToken token = default)
    {
        var all = new List<LampDeviceModel>();
        try
        {
            const int limit = 100, maxPages = 100;
            for (int page = 1; page <= maxPages; page++)
            {
                var response = await _apiService.GetLampsAsync(page: page, limit: limit, token: token);
                if (!response.Success || response.Data == null)
                {
                    _log?.Error($"Failed to fetch lamps (page {page}): {response.Error?.Message}");
                    return (all, false);
                }
                var batch = response.Data.Select(dto => dto.ToLampDeviceModel()).ToList();
                all.AddRange(batch);
                if (batch.Count < limit) return (all, true);
            }
            return (all, true);
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in FetchLampsAsync: {ex.Message}");
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

                var items = _deviceProvider.OfType<ILampDeviceModel>().ToList();
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
                            ViewModelProvider.Add(new LampDeviceViewModel((LampDeviceModel)item) { Index = startIndex + idx + 1 });
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
    public async Task HandleAsync(CallDeleteLampDeviceProcessMessageModel message, CancellationToken cancellationToken)
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
                    async (id, ct) => (await _apiService.DeleteLampAsync(id, ct)).Success,
                    r => _deviceProvider.Remove((ILampDeviceModel)r.Model),
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
    private readonly LampDeviceProvider _deviceProvider;
    private readonly IDeviceProviderService _deviceProviderService;
    #endregion
}
