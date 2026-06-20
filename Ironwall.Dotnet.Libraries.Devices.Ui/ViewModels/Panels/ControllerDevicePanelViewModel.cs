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
            Explain = "선택한 제어기를 참조하는 센서도 함께 삭제 될 수 있습니다. 정말 삭제하시겠습니까?",
            MessageModel = new CallDeleteControllerDeviceProcessMessageModel()
        });
    }

    public override async void OnClickInsertButton(object sender, RoutedEventArgs e)
    {
        // (P2-S1) 추가 즉시 Create — pending(Id=0) 미발생. 기본값으로 서버 생성 후 서버 Id를 그리드에 반영.
        if (!await _processGate.WaitAsync(0)) return;
        try
        {
            var existing = _deviceProvider.OfType<ControllerDeviceModel>().Select(m => m.DeviceNumber).ToHashSet();
            int number = 1; while (existing.Contains(number)) number++;
            var model = new ControllerDeviceModel { DeviceNumber = number, DeviceName = $"새 제어기 {number}" };
            var dto = model.ToControllerDeviceDto();
            _log?.Info($"[Controller Insert] 요청 POST DTO: {Newtonsoft.Json.JsonConvert.SerializeObject(dto)}");
            var resp = await _apiService.CreateControllerAsync(dto, CancellationToken.None);
            if (!resp.Success || resp.Data == null)
            {
                // (422 디버깅) 서버 응답 본문(어느 필드가 왜 거부됐는지 = FastAPI detail) + HTTP status 보존 로깅.
                _log?.Error($"[Controller Insert] 추가 실패 HTTP {resp.StatusCode}: {resp.Message} | 서버 detail={resp.Error?.Details}");
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "제어기 추가 실패",
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
            var currentList = _deviceProvider.OfType<ControllerDeviceModel>().ToList();

            // 서버 목록 조회 (비교를 위해)
            var (serverList, complete) = await FetchControllersAsync(token);
            if (!complete)
            {
                // (리뷰 MEDIUM) 서버 목록 불완전 → 부분 비교로 편집 소실 방지: 저장 보류.
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "저장 보류",
                    Explain = "서버 제어기 목록을 완전히 불러오지 못해 저장을 보류합니다. 잠시 후 다시 시도하세요."
                });
                return;
            }

            // (P2-S2) Save = Update 전용. 추가는 OnClickInsertButton(즉시 Create), 삭제는 HandleAsync에서 처리.
            //   Insert(Id<=0 Create) 루프 제거 → pending 유령 재생성/중복(#13) 원천 차단.
            var failures = new List<string>();
            foreach (var model in currentList.Where(m => m.Id > 0))
            {
                var server = serverList.FirstOrDefault(s => s.Id == model.Id);
                if (server != null && !DeviceEquals(model, server))
                    if (!await UpdateControllerAsync(model, token))
                        failures.Add($"{model.DeviceName}(Id={model.Id})");
            }

            if (failures.Count > 0)
            {
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "제어기 수정 일부 실패",
                    Explain = $"{failures.Count}건 수정에 실패했습니다.\n" + string.Join("\n", failures)
                });
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

    public override void OnSelectionChanged(IList<ControllerDeviceViewModel> rows)
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
                foreach (IControllerDeviceViewModel newItem in e.NewItems)
                {
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
    #endregion
    #region - Binding Methods -
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

    // (P2-S1) Create 헬퍼 제거 — 추가는 OnClickInsertButton 즉시 Create로 일원화. Update만 유지.
    /// <summary>
    /// GOP API를 통해 Controller 업데이트
    /// </summary>
    private async Task<bool> UpdateControllerAsync(ControllerDeviceModel model, CancellationToken token = default)
    {
        try
        {
            var dto = model.ToControllerDeviceDto();
            var response = await _apiService.UpdateControllerAsync(model.Id, dto, token);

            if (response.Success)
            {
                _log?.Info($"Controller updated successfully: {model.Id}");
                return true;
            }
            else
            {
                _log?.Error($"Failed to update controller {model.Id}: {response.Error?.Message}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in UpdateControllerAsync: {ex.Message}");
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
                // 1. UI 스레드에서 IsVisible = false 설정
                DispatcherService.Invoke(() => IsVisible = false);

                // 2. Render 우선순위까지 큐 소진 — ProgressCircle 렌더링 보장
                await DispatcherService.BeginInvoke(() => { }, DispatcherPriority.Render);

                ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;
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
    #endregion

}