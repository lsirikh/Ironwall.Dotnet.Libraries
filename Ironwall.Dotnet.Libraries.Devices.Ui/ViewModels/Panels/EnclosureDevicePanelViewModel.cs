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
            Explain = "선택한 함체 장비를 정말로 삭제하시겠습니까?",
            MessageModel = new CallDeleteEnclosureDeviceProcessMessageModel()
        });
    }

    public override async void OnClickInsertButton(object sender, RoutedEventArgs e)
    {
        // (P2-S1) 추가 즉시 Create — pending(Id=0) 미발생. 기본값으로 서버 생성 후 서버 Id를 그리드에 반영.
        if (!await _processGate.WaitAsync(0)) return;
        try
        {
            var existing = _deviceProvider.OfType<EnclosureDeviceModel>().Select(m => m.DeviceNumber).ToHashSet();
            int number = 1; while (existing.Contains(number)) number++;
            var model = new EnclosureDeviceModel { DeviceNumber = number, DeviceName = $"새 함체 {number}" };
            var resp = await _apiService.CreateEnclosureAsync(model.ToEnclosureDeviceDto(), CancellationToken.None);
            if (!resp.Success || resp.Data == null)
            {
                _log?.Error($"[Enclosure Insert] 추가 실패 HTTP {resp.StatusCode}: {resp.Message} | 서버 detail={resp.Error?.Details}");
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "함체 추가 실패",
                    Explain = $"장비를 추가하지 못했습니다. 서버 상태를 확인하세요.\n{resp.Message}"
                });
                return;
            }
            // (코드리뷰 C1) 타입드 provider는 공유 DeviceProvider의 단방향 투영 → 수동 Add 시 FetchAll 투영과
            //   이중행. 서버 생성분은 재조회로만 반영(단일 원천).
            if (_pCancellationTokenSource != null && !_pCancellationTokenSource.IsCancellationRequested)
            {
                _pCancellationTokenSource.Cancel();
                _pCancellationTokenSource.Dispose();
            }
            _pCancellationTokenSource = new CancellationTokenSource();
            await _deviceProviderService.FetchAllDevicesAsync(_pCancellationTokenSource.Token);
            await DataInitialize(_pCancellationTokenSource.Token);
        }
        catch (Exception ex) { _log?.Error($"OnClickInsertButton: {ex.Message}"); }
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

            // (P2-S2) Save = Update 전용. 추가는 OnClickInsertButton(즉시 Create), 삭제는 HandleAsync에서 처리.
            var currentList = _deviceProvider.OfType<EnclosureDeviceModel>().ToList();
            var (serverList, complete) = await FetchEnclosuresAsync(token);
            if (!complete)
            {
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "저장 보류",
                    Explain = "서버 함체 목록을 완전히 불러오지 못해 수정 저장을 보류합니다. 잠시 후 다시 시도하세요."
                });
                return;
            }
            var failures = new List<string>();

            foreach (var model in currentList.Where(m => m.Id > 0))
            {
                var server = serverList.FirstOrDefault(s => s.Id == model.Id);
                if (server != null && !DeviceEquals(model, server))
                    if (!await UpdateEnclosureAsync(model, token))
                        failures.Add($"{model.DeviceName}(Id={model.Id})");
            }

            if (failures.Count > 0)
            {
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "함체 수정 일부 실패",
                    Explain = $"{failures.Count}건 수정에 실패했습니다.\n" + string.Join("\n", failures)
                });
            }

            await _deviceProviderService.FetchAllDevicesAsync(_pCancellationTokenSource.Token);
            await DataInitialize(_pCancellationTokenSource.Token);
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

    public override void OnSelectionChanged(IList<EnclosureDeviceViewModel> rows)
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
                if (e.NewItems == null) return;
                foreach (IEnclosureDeviceViewModel newItem in e.NewItems)
                    _deviceProvider.Add((IEnclosureDeviceModel)newItem.Model);
                break;
            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems == null) return;
                foreach (IEnclosureDeviceViewModel oldItem in e.OldItems)
                    _deviceProvider.Remove((IEnclosureDeviceModel)oldItem.Model);
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
        a.Longitude == b.Longitude;
    }
    #endregion
    #region - Helper Methods -
    // (P2-S1) Create 헬퍼 제거 — 추가는 OnClickInsertButton 즉시 Create로 일원화. Update만 유지.
    private async Task<bool> UpdateEnclosureAsync(EnclosureDeviceModel model, CancellationToken token = default)
    {
        try
        {
            var dto = model.ToEnclosureDeviceDto();
            var response = await _apiService.UpdateEnclosureAsync(model.Id, dto, token);
            if (response.Success)
            {
                _log?.Info($"Enclosure updated successfully: {model.Id}");
                return true;
            }
            _log?.Error($"Failed to update enclosure {model.Id}: {response.Error?.Message}");
            return false;
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in UpdateEnclosureAsync: {ex.Message}");
            return false;
        }
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
