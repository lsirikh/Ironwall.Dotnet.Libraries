using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
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
   Created On   : 6/22/2025 6:48:21 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class MalfunctionEventPanelViewModel : BaseDataGridMultiPanelViewModel<MalfunctionEventViewModel>
                                        , IHandle<CallDeleteMalfunctionEventProcessMessageModel>
{
    
    #region - Ctors -
    public MalfunctionEventPanelViewModel(IEventAggregator eventAggregator
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
            LoadFromCache();
            IsVisible = true;
            UpdateAction?.Invoke(_startDate, _endDate);
        }
        else
        {
            await DataInitialize(cancellationToken);
        }
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
    public override void OnClickInsertButton(object sender, RoutedEventArgs e)
    {
        var vm = new MalfunctionEventViewModel(new MalfunctionEventModel());
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

    public override async void OnClickDeleteButton(object sender, RoutedEventArgs e)
    {
        if (SelectedItemCount == 0) return;
        _pendingDeleteItems = SelectedItems.ToList();
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Explain = "선택한 이벤트를 정말로 삭제하시겠습니까? 해당이벤트의 조치보고도 함께 삭제 됩니다.",
            MessageModel = new CallDeleteMalfunctionEventProcessMessageModel()
        });
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

            var insertList = currentList.Where(m => m.Id <= 0).ToList();
            var updateList = ViewModelProvider
                             .Where(vm => vm.IsEdited && vm.Model.Id > 0)
                             .Select(vm => (IMalfunctionEventModel)vm.Model)
                             .ToList();

            foreach (var model in updateList)
                await _providerService.UpdateMalfunctionEventAsync(model, token);

            foreach (var model in insertList.OfType<IMalfunctionEventModel>())
                await _providerService.InsertMalfunctionEventAsync(model, token);

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
    public override void OnSelectionChanged(IList<MalfunctionEventViewModel> rows)
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
                foreach (IMalfunctionEventViewModel newItem in e.NewItems)
                {
                    _eventProvider.Add((IMalfunctionEventModel)newItem.Model);
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                // Items removed
                if (e.OldItems == null) return;
                foreach (IMalfunctionEventViewModel oldItem in e.OldItems)
                {
                    _eventProvider.Remove((IMalfunctionEventModel)oldItem.Model);
                }
                break;

            case NotifyCollectionChangedAction.Replace:
                // Some items replaced
                if (e.OldItems == null) return;
                foreach (IMalfunctionEventViewModel oldItem in e.OldItems)
                {
                    _eventProvider.Remove((IMalfunctionEventModel)oldItem.Model);
                }
                if (e.NewItems == null) return;
                foreach (IMalfunctionEventViewModel newItem in e.NewItems)
                {
                    _eventProvider.Add((IMalfunctionEventModel)newItem.Model);
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                // The whole list is refreshed
                ViewModelProvider.Clear();
                foreach (var item in _eventProvider.OfType<IMalfunctionEventModel>())
                {
                    ViewModelProvider.Add(new MalfunctionEventViewModel(item));
                }

                break;
        }
    }

    private static bool EventEquals(IMalfunctionEventModel a, IMalfunctionEventModel b)
    {
        return a?.Device?.Id == b?.Device?.Id &&
               a.MessageType == b.MessageType &&
               a.Status == b.Status &&
               a.Reason == b.Reason &&
               a.FirstStart == b.FirstStart &&
               a.SecondStart == b.SecondStart &&
               a.FirstEnd == b.FirstEnd &&
               a.SecondEnd == b.SecondEnd &&
               a.DateTime == b.DateTime;
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
        catch (Exception ex)
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

                // 기존 Malfunction 이벤트 제거
                var existingMalfunction = _eventProvider.OfType<IMalfunctionEventModel>().ToList();
                foreach (var e in existingMalfunction) _eventProvider.Remove(e);

                ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;

                DispatcherService.Invoke(() =>
                {
                    ViewModelProvider.Clear();
                });

                // CollectionChanged 구독을 LoadNextPageAsync 전에 설정
                // → VP.Add 시 CollectionChanged가 EP 동기화 담당 (double-add 방지)
                ViewModelProvider.CollectionChanged += CollectionEntity_CollectionChanged;

                cancellationToken.ThrowIfCancellationRequested();

                // 첫 페이지 로드
                await LoadNextPageAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

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
            var result = await _providerService.FetchMalfunctionEventsPageAsync(
                StartDate, EndDate, _currentPage + 1, 100, token);

            _currentPage = result.Page;
            _totalPages = result.TotalPages;
            _totalCount = result.Total;

            DispatcherService.Invoke(() =>
            {
                foreach (var item in result.Items)
                {
                    // _eventProvider 동기화는 CollectionChanged 핸들러가 담당
                    ViewModelProvider.Add(new MalfunctionEventViewModel(item)
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

    private void LoadFromCache()
    {
        ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;
        DispatcherService.Invoke(() =>
        {
            ViewModelProvider.Clear();
            foreach (var (item, index) in _eventProvider.OfType<IMalfunctionEventModel>().OrderBy(item => item.Id).Select((item, index) => (item, index)))
            {
                ViewModelProvider.Add(new MalfunctionEventViewModel(item) { Index = index + 1 });
            }
            NotifyOfPropertyChange(() => ViewModelProvider);
            NotifyOfPropertyChange(() => LoadedCountText);
            NotifyOfPropertyChange(() => HasMorePages);
        });
        ViewModelProvider.CollectionChanged += CollectionEntity_CollectionChanged;
    }

    internal bool IsCacheValid()
    {
        return _isCacheValid
            && _cachedStartDate == _startDate
            && _cachedEndDate == _endDate
            && _eventProvider.OfType<IMalfunctionEventModel>().Any();
    }

    public void SetCachedDate(DateTime startDate, DateTime endDate)
    {
        _cachedStartDate = startDate;
        _cachedEndDate = endDate;
        _isCacheValid = true;
    }

    public void InvalidateCache()
    {
        _isCacheValid = false;
    }
    #endregion
    #region - IHanldes -
    public async Task HandleAsync(CallDeleteMalfunctionEventProcessMessageModel message, CancellationToken cancellationToken)
    {
        // 1. 진행중 UI 표시
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel(), cancellationToken);

        // 2. 비동기 작업 (UI 스레드와 분리)
        await Task.Run(async () =>
        {
            foreach (var item in _pendingDeleteItems)
            {
                var ret = await _providerService.DeleteMalfunctionEventAsync(item.Model.Id, cancellationToken);
            }
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
    private IList<MalfunctionEventViewModel> _pendingDeleteItems = [];
    #endregion
}