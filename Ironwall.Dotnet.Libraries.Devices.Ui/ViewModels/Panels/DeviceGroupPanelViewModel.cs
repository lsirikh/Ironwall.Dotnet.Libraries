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
            Explain = "선택한 장비그룹을 삭제하시겠습니까?",
            MessageModel = new CallDeleteDeviceGroupProcessMessageModel()
        });
    }

    public override async void OnClickInsertButton(object sender, RoutedEventArgs e)
    {
        // (FR-B1) 추가 즉시 Create — pending(Id=0) 미발생. 기본 이름으로 서버 생성 후,
        //   이름/설명 인라인 편집은 Save(Update)로 반영. 서버가 부여한 Id를 즉시 그리드에 반영.
        if (!await _processGate.WaitAsync(0)) return;
        try
        {
            // 고유 기본명(연타 시 동명 빈 그룹 양산 완화). 서버가 동명 거부 시엔 아래 실패팝업 처리.
            var existing = _deviceGroupProvider.OfType<DeviceGroupModel>()
                .Select(g => g.Name).ToHashSet();
            var name = "새 그룹";
            for (int n = 2; existing.Contains(name); n++) name = $"새 그룹 ({n})";
            var model = new DeviceGroupModel { Name = name, Description = string.Empty };
            var resp = await _apiService.CreateDeviceGroupAsync(model.ToDeviceGroupDto(), CancellationToken.None);
            if (!resp.Success || resp.Data == null)
            {
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "그룹 추가 실패",
                    Explain = $"장비그룹을 추가하지 못했습니다. 서버 상태를 확인하세요.\n{resp.Message}"
                });
                return;
            }
            // 서버 Id 포함 모델로 그리드 반영 (CollectionChanged → _deviceGroupProvider 동기화)
            ViewModelProvider.Add(new DeviceGroupViewModel(resp.Data.ToDeviceGroupModel()));
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

            // (FR-B3) Save = Update 전용. 추가는 OnClickInsertButton(즉시 Create), 삭제는 HandleAsync(즉시 Delete)에서 처리.
            //   기존 Insert(Where Id<=0 Create)·Delete diff 루프 제거 → pending 유령 재생성/중복(#4/#13) 원천 차단.
            var currentList = _deviceGroupProvider.OfType<DeviceGroupModel>().ToList();
            var (serverList, complete) = await FetchDeviceGroupsAsync(token);
            if (!complete)
            {
                // 서버 목록 불완전 → 비교 기준 부족 → 수정 보류(편집 보존). (리뷰 MEDIUM)
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "저장 보류",
                    Explain = "서버 그룹 목록을 완전히 불러오지 못해 수정 저장을 보류합니다. 잠시 후 다시 시도하세요."
                });
                return;
            }
            var failures = new List<string>();

            foreach (var model in currentList.Where(m => m.Id > 0))
            {
                var server = serverList.FirstOrDefault(s => s.Id == model.Id);
                if (server != null && !DeviceGroupEquals(model, server))
                {
                    if (!await UpdateDeviceGroupHelper(model, token))
                        failures.Add($"{model.Name}(Id={model.Id})");
                }
            }

            if (failures.Count > 0)
            {
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "그룹 수정 일부 실패",
                    Explain = $"{failures.Count}건 수정에 실패했습니다.\n" + string.Join("\n", failures)
                });
            }

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

    // (FR-B3) Create 헬퍼 제거 — 추가는 OnClickInsertButton 즉시 Create로 일원화. Update만 유지(성공여부 반환).
    private async Task<bool> UpdateDeviceGroupHelper(DeviceGroupModel model, CancellationToken token)
    {
        try
        {
            var resp = await _apiService.UpdateDeviceGroupAsync(model.Id, model.ToDeviceGroupDto(), token);
            if (!resp.Success)
            {
                _log?.Warning($"DeviceGroup update failed: id={model.Id} {resp.Message}");
                return false;
            }
            return true;
        }
        catch (Exception ex) { _log?.Error($"UpdateDeviceGroupHelper: {ex.Message}"); return false; }
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
                if (e.NewItems == null) return;
                foreach (IDeviceGroupViewModel newItem in e.NewItems)
                    _deviceGroupProvider.Add(newItem.Model);
                break;
            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems == null) return;
                foreach (IDeviceGroupViewModel oldItem in e.OldItems)
                    _deviceGroupProvider.Remove(oldItem.Model);
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
    internal static bool DeviceGroupEquals(IDeviceGroupModel a, IDeviceGroupModel b)
        => a.Name == b.Name && a.Description == b.Description;
    #endregion
    #region - Attributes -
    private readonly IDeviceApiService _apiService;
    private readonly DeviceGroupProvider _deviceGroupProvider;
    #endregion
}
