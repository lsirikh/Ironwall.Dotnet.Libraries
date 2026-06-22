using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;
using System.Collections.Specialized;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/22/2025 6:48:06 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class DetectionEventPanelViewModel : BaseDataGridMultiPanelViewModel<DetectionEventViewModel>
                                        , IHandle<CallDeleteDetectionEventProcessMessageModel>
{
    
    #region - Ctors -
    public DetectionEventPanelViewModel(IEventAggregator eventAggregator
                                        , ILogService log
                                        , EventProviderService providerService
                                        , DeviceProvider deviceProvider
                                        , EventProvider eventProvider)
                                        : base(eventAggregator, log)
    {
        _providerService = providerService;
        _eventProvider = eventProvider;
        DeviceProvider = deviceProvider;
        LoadMoreCommand = new SimpleCommand(async () => await LoadNextPageAsync(_cancellationTokenSource?.Token ?? CancellationToken.None));
        ReportCommand = new SimpleParamCommand(ReportRowAsync);
    }
    #endregion

    #region - Action Report (우클릭 조치보고) -
    /// <summary>행 우클릭 '조치보고' — 미저장 Draft(Id≤0)는 차단(FromEventId FK 필요). 기존행은 1:N 후속 조치보고 허용(중복 가드 없음). 기존 ReportDialog 재사용.</summary>
    private async Task ReportRowAsync(object? param)
    {
        if (param is not DetectionEventViewModel row)
            return;

        if (row.IsDraft)
        {
            await _eventAggregator.PublishOnUIThreadAsync(new OpenInfoPopupMessageModel
            {
                Title = "조치보고 불가",
                Explain = "미저장 이벤트입니다. 저장 후 조치보고할 수 있습니다."
            });
            return;
        }

        if (row.Model is not IDetectionEventModel model)
            return;

        // 행 VM과 독립된 임시 카드 VM 재구성(SendAction/멱등가드는 카드 VM 보유) — 패널 행 Dispose 무영향
        var card = new DetectionEventCardViewModel(_eventAggregator, _log, model);
        IoC.Get<DetectionReportDialogViewModel>().UpdateData(card, IoC.Get<IAccountModel>());
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenEventReportDialogMessageModel { EventType = "DETECTION" });
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        if (IsCacheValid())
        {
            // 캐시 유효: EventProvider에서 필터링만 수행 (API 미호출)
            LoadFromCache();
            IsVisible = true;
            UpdateAction?.Invoke(_startDate, _endDate);
        }
        else
        {
            await DataInitialize(cancellationToken);  // DataInitialize 내부에서 false→true 관리
        }
    }

    protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;
        _log.Info("OnDeactivateAsync in DetectionEventPanelViewModel");
        return base.OnDeactivateAsync(close, cancellationToken);
    }

    public override async void OnClickDeleteButton(object sender, RoutedEventArgs e)
    {
        if (SelectedItemCount == 0) return;
        _pendingDeleteItems = SelectedItems.ToList();
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Explain = "선택한 이벤트를 정말로 삭제하시겠습니까? 해당이벤트의 조치보고도 함께 삭제 됩니다.",
            MessageModel = new CallDeleteDetectionEventProcessMessageModel()
        });
    }

    public override void OnClickInsertButton(object sender, RoutedEventArgs e)
    {
        var vm = new DetectionEventViewModel(new DetectionEventModel { MessageType = Ironwall.Dotnet.Libraries.Enums.EnumEventType.Intrusion });
        ViewModelProvider.Add(vm);
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

            await DataInitialize(token);
            await Task.Delay(2000, token);
        }
        catch (OperationCanceledException) { /* 무시 */ }
        catch (Exception ex) { _log?.Error(ex.Message); }
        finally
        {
            ReloadButtonEnable = true;            // UI Enable
            //UpdateAction?.Invoke(_startDate, _endDate);
            _processGate.Release();                // 뮤텍스 해제
        }
    }

    public override async void OnClickSaveButton(object sender, RoutedEventArgs e)
    {
        if (!await _processGate.WaitAsync(0))       // 0 → “비동기로 테스트-후-입장”
            return;

        try
        {
            SaveButtonEnable = false;

            if (_pCancellationTokenSource != null && !_pCancellationTokenSource!.IsCancellationRequested)
            {
                _pCancellationTokenSource.Cancel();
                _pCancellationTokenSource.Dispose();
            }
            _pCancellationTokenSource = new CancellationTokenSource();

            var token = _pCancellationTokenSource.Token;
            var currentList = _eventProvider;

            var insertList = currentList
                            .Where(m => m.Id <= 0)
                            .OfType<IDetectionEventModel>()
                            .ToList();
            var updateRows = ViewModelProvider
                            .Where(vm => vm.IsEdited && vm.Model.Id > 0)
                            .ToList();

            // (EA7) 부분 실패 수집 — 한 건 실패로 전체 중단/무응답 방지. 실패/보류 시 재로드 생략해 편집 보존.
            var saveFailures = new List<string>();
            int held = 0;

            foreach (var vm in updateRows)
            {
                var model = (IDetectionEventModel)vm.Model;
                try { await _providerService.UpdateDetectionEventAsync(model, token); vm.IsEdited = false; }   // 성공 행은 dirty 해제(재-PUT 누적 방지)
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    saveFailures.Add($"수정(Id={model.Id}): {ex.Message}");
                    _log?.Error($"UpdateDetectionEventAsync 실패 Id={model.Id}: {ex.Message}");
                }
            }

            foreach (var model in insertList)
            {
                if (model.Device == null || model.Device.Id <= 0) { held++; continue; }   // 장비 미선택 Draft 보류(서버 device_id FK 필수)
                try { var created = await _providerService.InsertDetectionEventAsync(model, token); if (created != null && created.Id > 0) model.Id = created.Id; }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    saveFailures.Add($"추가: {ex.Message}");
                    _log?.Error($"InsertDetectionEventAsync 실패: {ex.Message}");
                }
            }

            if (saveFailures.Count > 0 || held > 0)
            {
                // 일부 저장 실패/보류 → 사용자 알림 + 재로드 생략(미저장 편집·Draft 보존)
                var sb = new System.Text.StringBuilder();
                if (saveFailures.Count > 0)
                    sb.AppendLine($"{saveFailures.Count}건 저장에 실패했습니다. 편집 내용을 유지합니다.\n" + string.Join("\n", saveFailures.Take(5)));
                if (held > 0)
                    sb.AppendLine($"{held}건은 장비 미선택으로 보류했습니다. 장비를 선택한 뒤 다시 저장하세요.");
                await _eventAggregator.PublishOnUIThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "이벤트 저장 일부 보류/실패",
                    Explain = sb.ToString().TrimEnd()
                });
                return;
            }

            await DataInitialize().ConfigureAwait(false);
            await Task.Delay(2000, token);
        }
        catch (TaskCanceledException ex) { _log?.Warning(ex.Message); }
        catch (OperationCanceledException) { /* 무시 */ }
        catch (Exception ex) { _log?.Error(ex.Message); }
        finally
        {
            //UpdateAction?.Invoke(_startDate, _endDate);
            SaveButtonEnable = true;
            _processGate.Release();                // 뮤텍스 해제
        }
    }
    #endregion
    #region - Binding Methods -
    public override void OnSelectionChanged(IList<DetectionEventViewModel> rows)
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
                foreach (IDetectionEventViewModel newItem in e.NewItems)
                {
                    _eventProvider.Add((IDetectionEventModel)newItem.Model);
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                // Items removed
                if (e.OldItems == null) return;
                foreach (IDetectionEventViewModel oldItem in e.OldItems)
                {
                    _eventProvider.Remove((IDetectionEventModel)oldItem.Model);
                }
                break;

            case NotifyCollectionChangedAction.Replace:
                // Some items replaced
                if (e.OldItems == null) return;
                foreach (IDetectionEventViewModel oldItem in e.OldItems)
                {
                    _eventProvider.Remove((IDetectionEventModel)oldItem.Model);
                }
                if (e.NewItems == null) return;
                foreach (IDetectionEventViewModel newItem in e.NewItems)
                {
                    _eventProvider.Add((IDetectionEventModel)newItem.Model);
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                // The whole list is refreshed
                ViewModelProvider.Clear();
                foreach (var item in _eventProvider.OfType<IDetectionEventModel>())
                {
                    ViewModelProvider.Add(new DetectionEventViewModel(item));
                }

                break;
        }
    }

    private static bool EventEquals(IDetectionEventModel a, IDetectionEventModel b)
    {
        return a?.Device?.Id == b?.Device?.Id &&
               a.MessageType == b.MessageType &&
               a.Status == b.Status && 
               a.Result == b.Result &&
               Math.Abs((a.DateTime - b.DateTime).TotalSeconds) < 1;
    }
    #endregion
    #region - Processes -
    public void SetDate(DateTime startDate, DateTime endDate)
    {
        _startDate = startDate;
        _endDate = endDate;
        EndDateDisplay = StartDate;
    }

    public bool CanClckSearch => true;
    public async void ClickSearch()
    {
        try
        {
            if (_cancellationTokenSource != null)
                _cancellationTokenSource.Cancel();

            InvalidateCache();
            _cancellationTokenSource = new CancellationTokenSource();
            await DataInitialize(_cancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (TaskCanceledException) { }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
        }
        finally
        {
            IsVisible = true;
        }

    }
    public bool CanClickCancel => true;
    public void ClickCancel()
    {
        try
        {
            if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
                return;

            _cancellationTokenSource.Cancel();
        }
        catch (TaskCanceledException) { }
        catch(Exception ex)
        {
            _log?.Error(ex.Message);
        }
    }

    private Task DataInitialize(CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            try
            {
                IsVisible = false;

                // 페이지네이션 상태 초기화
                _currentPage = 0;
                _totalPages = 1;
                _totalCount = 0;

                cancellationToken.ThrowIfCancellationRequested();

                // (EA2) swap-on-success: 첫 페이지를 먼저 가져와 성공을 확인한 뒤에만 기존 컬렉션을 교체.
                //   fetch 실패 시 Remove/Clear 를 하지 않아 기존 이벤트가 소실되지 않는다.
                PagedResult<IDetectionEventModel> firstPage;
                try
                {
                    firstPage = await _providerService.FetchDetectionEventsPageAsync(
                        StartDate, EndDate, 1, 100, cancellationToken);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    _log?.Error($"DataInitialize 첫 페이지 fetch 실패: {ex.Message}");
                    firstPage = new PagedResult<IDetectionEventModel> { Success = false };
                }

                if (!firstPage.Success)
                {
                    // 기존 컬렉션 보존 + 사용자 알림 (화면 공백/복구불가 루프 방지)
                    await _eventAggregator.PublishOnUIThreadAsync(new OpenInfoPopupMessageModel
                    {
                        Explain = "탐지 이벤트를 불러오지 못했습니다. 서버 연결 상태를 확인하세요. 기존 목록은 유지됩니다."
                    }, cancellationToken);
                    return; // finally 에서 IsVisible=true
                }

                // 성공 → 기존 컬렉션 교체
                _currentPage = firstPage.Page;
                _totalPages = firstPage.TotalPages;
                _totalCount = firstPage.Total;

                var existingDetection = _eventProvider.OfType<IDetectionEventModel>().ToList();
                foreach (var e in existingDetection) _eventProvider.Remove(e);

                ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;
                DispatcherService.Invoke(() =>
                {
                    ViewModelProvider.Clear();
                });
                // CollectionChanged 구독을 Add 전에 설정 → VP.Add 시 EP 동기화(double-add 방지)
                ViewModelProvider.CollectionChanged += CollectionEntity_CollectionChanged;

                cancellationToken.ThrowIfCancellationRequested();

                DispatcherService.Invoke(() =>
                {
                    foreach (var item in firstPage.Items)
                    {
                        ViewModelProvider.Add(new DetectionEventViewModel(item)
                        {
                            Index = ViewModelProvider.Count + 1
                        });
                    }
                    NotifyOfPropertyChange(() => LoadedCountText);
                    NotifyOfPropertyChange(() => HasMorePages);
                });

                // 캐시 날짜범위 기록
                SetCachedDate(_startDate, _endDate);
            }
            catch (OperationCanceledException ex)
            {
                _log?.Warning($"Raised {nameof(OperationCanceledException)}({nameof(DataInitialize)}) : {ex.Message}");
            }
            finally
            {
                IsVisible = true;
                UpdateAction?.Invoke(_startDate, _endDate);
            }
        });
    }

    public async Task LoadNextPageAsync(CancellationToken token = default)
    {
        if (_isLoadingMore || !HasMorePages) return;

        token.ThrowIfCancellationRequested();

        _isLoadingMore = true;
        IsLoadingMore = true;

        try
        {
            var result = await _providerService.FetchDetectionEventsPageAsync(
                StartDate, EndDate, _currentPage + 1, 100, token);

            if (token.IsCancellationRequested) return;

            _currentPage = result.Page;
            _totalPages = result.TotalPages;
            _totalCount = result.Total;

            DispatcherService.Invoke(() =>
            {
                foreach (var item in result.Items)
                {
                    // _eventProvider 동기화는 CollectionChanged 핸들러가 담당
                    ViewModelProvider.Add(new DetectionEventViewModel(item)
                    {
                        Index = ViewModelProvider.Count + 1
                    });
                }
                NotifyOfPropertyChange(() => LoadedCountText);
                NotifyOfPropertyChange(() => HasMorePages);
            });
        }
        catch (TaskCanceledException) { }
        catch (Exception ex) { _log?.Error($"LoadNextPageAsync failed: {ex.Message}"); }
        finally
        {
            _isLoadingMore = false;
            IsLoadingMore = false;
        }
    }

    /// <summary>
    /// EventProvider 캐시에서 Detection 이벤트를 필터링하여 ViewModelProvider에 채움 (API 미호출)
    /// </summary>
    private void LoadFromCache()
    {
        ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;

        DispatcherService.Invoke(() =>
        {
            ViewModelProvider.Clear();
            foreach (var (item, index) in _eventProvider.OfType<IDetectionEventModel>().OrderBy(item => item.Id).Select((item, index) => (item, index)))
            {
                ViewModelProvider.Add(new DetectionEventViewModel(item) { Index = index + 1 });
            }
            NotifyOfPropertyChange(() => ViewModelProvider);
            NotifyOfPropertyChange(() => LoadedCountText);
            NotifyOfPropertyChange(() => HasMorePages);
        });

        ViewModelProvider.CollectionChanged += CollectionEntity_CollectionChanged;
    }

    /// <summary>
    /// 캐시 유효성 검증: 현재 날짜범위와 캐시된 날짜범위가 동일하고 EventProvider에 데이터가 있으면 유효
    /// </summary>
    internal bool IsCacheValid()
    {
        return _isCacheValid
            && _cachedStartDate == _startDate
            && _cachedEndDate == _endDate
            && _eventProvider.OfType<IDetectionEventModel>().Any();
    }

    /// <summary>
    /// 캐시 날짜범위를 설정하고 캐시를 유효 상태로 전환
    /// </summary>
    public void SetCachedDate(DateTime startDate, DateTime endDate)
    {
        _cachedStartDate = startDate;
        _cachedEndDate = endDate;
        _isCacheValid = true;
    }

    /// <summary>
    /// 캐시를 무효화 (ClickSearch, 날짜 변경 시 호출)
    /// </summary>
    public void InvalidateCache()
    {
        _isCacheValid = false;
    }

    #endregion
    #region - IHanldes -
    public async Task HandleAsync(CallDeleteDetectionEventProcessMessageModel message, CancellationToken cancellationToken)
    {
        // 1. 진행중 UI 표시
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel(), cancellationToken);

        // 2. 비동기 작업 (UI 스레드와 분리)
        await Task.Run(async () =>
        {
            await ExecuteDeleteAsync(
                _pendingDeleteItems,
                r => r.Model.Id,
                (id, t) => _providerService.DeleteDetectionEventAsync(id, t),
                r => _eventProvider.Remove(r.Model),
                cancellationToken);
            _pendingDeleteItems = [];
        }, cancellationToken);

        await DataInitialize().ConfigureAwait(false);
        UpdateAction?.Invoke(_startDate, _endDate);

        // 4. 진행중 UI 닫기
        await _eventAggregator.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
    }
    #endregion
    #region - Properties -
    public DateTime StartDate
    {
        get { return _startDate; }
        set
        {
            _startDate = value;
            NotifyOfPropertyChange(() => StartDate);
            EndDateDisplay = _startDate;
        }
    }

    public DateTime EndDate
    {
        get { return _endDate; }
        set
        {
            _endDate = value;
            NotifyOfPropertyChange(() => EndDate);
        }
    }

    public DateTime EndDateDisplay
    {
        get { return _endDateDisplay; }
        set
        {
            _endDateDisplay = value;
            NotifyOfPropertyChange(() => EndDateDisplay);
        }
    }
    public DeviceProvider DeviceProvider { get; }
    public delegate void SendDate(DateTime start, DateTime end);
    public event SendDate? UpdateAction;

    // ─── Pagination Properties ───
    public bool IsLoadingMore
    {
        get => _isLoadingMore;
        set { _isLoadingMore = value; NotifyOfPropertyChange(() => IsLoadingMore); }
    }

    public string LoadedCountText => $"{ViewModelProvider.Count} / {_totalCount}건";
    public bool HasMorePages => _currentPage < _totalPages;

    public ICommand LoadMoreCommand { get; }

    /// <summary>행 우클릭 '조치보고' 커맨드(CommandParameter=행 VM). 기존 조치보고 다이얼로그 재사용.</summary>
    public ICommand ReportCommand { get; }
    #endregion
    #region - Attributes -
    protected DateTime _startDate;
    protected DateTime _endDate;
    protected DateTime _endDateDisplay;
    private EventProviderService _providerService;
    private EventProvider _eventProvider;
    private DateTime _cachedStartDate;
    private DateTime _cachedEndDate;
    private bool _isCacheValid;

    // ─── Pagination State ───
    private int _currentPage;
    private int _totalPages = 1;
    private int _totalCount;
    private bool _isLoadingMore;
    private IList<DetectionEventViewModel> _pendingDeleteItems = [];
    #endregion

}