using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;

/// <summary>
/// 운영 이벤트 목록(이벤트 콘솔 '운영' 레일) — 함체 · 통문 개폐와 함체 환경 임계 경보를 기간으로 조회한다.
/// </summary>
/// <remarks>
/// <para>이벤트 자체는 <b>조회 전용</b>이다. 운영 이벤트는 서버가 매니저 보고로 스스로 만든다 — 추가 · 저장 · 삭제 경로를 두지 않는다
/// (<c>OperationEventDto</c> 주석: 클라가 POST 하면 같은 사건이 2건이 된다. API 에도 DELETE 가 없다).
/// 조치보고는 한다(<see cref="ReportCommand"/> — 서버 FR-07, operation-event-card PRD v1.1).</para>
/// <para>서버 7.0 미만(운영 6.3.2)에는 경로가 없다 — 오류 팝업 대신 빈 목록과 <see cref="UnsupportedNotice"/> 를 보인다.</para>
/// <para>조회 · 쪽 넘김 · 기간 캐시 · 쪽 겹침 중복 제거는 연결 목록(<see cref="ConnectionEventPanelViewModel"/>)과 같은 규칙이다.</para>
/// </remarks>
public class OperationEventPanelViewModel : BaseDataGridMultiPanelViewModel<OperationEventViewModel>
{
    public const int PageSize = 100;

    public OperationEventPanelViewModel(IEventAggregator eventAggregator, ILogService log, EventProviderService providerService)
        : base(eventAggregator, log)
    {
        _providerService = providerService ?? throw new ArgumentNullException(nameof(providerService));
        LoadMoreCommand = new SimpleCommand(async () => await LoadNextPageAsync(_cancellationTokenSource?.Token ?? CancellationToken.None));
        ReportCommand = new SimpleParamCommand(ReportRowAsync);
    }

    #region - 조치보고 (operation-event-card PRD v1.1 §3 — 서버 FR-07: 운영 이벤트도 조치 대상) -
    /// <summary>조치보고 = 서버 events:edit — 탐지 · 장애 행과 같은 규칙(ActionReportRules).</summary>
    public bool CanReportRow => ActionReportRules.CanReport(ResolvePermissionService());

    /// <summary>행 우클릭 · 상세 [조치보고] — 탐지 · 장애 행과 같은 길(행과 독립된 임시 카드 → 조치보고 창).</summary>
    public ICommand ReportCommand { get; }

    private async Task ReportRowAsync(object? param)
    {
        if (!CanReportRow)
        {
            await _eventAggregator.PublishOnUIThreadAsync(new OpenInfoPopupMessageModel
            {
                Title = "권한 없음",
                Explain = ActionReportRules.NO_PERMISSION_TEXT
            });
            return;
        }
        if (param is not OperationEventViewModel { Operation.Id: > 0 } row) return;

        var card = new OperationEventCardViewModel(_eventAggregator, _log, row.Operation);
        IoC.Get<OperationReportDialogViewModel>().UpdateData(card, IoC.Get<IAccountModel>());
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenEventReportDialogMessageModel { EventType = OperationReportDialogViewModel.EventType });
    }

    private static IPermissionService? ResolvePermissionService()
    {
        try { return IoC.Get<IPermissionService>(); }
        catch { return null; }
    }
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

    /// <summary>조회 전용 — 운영 이벤트는 서버가 만든다.</summary>
    public override void OnClickInsertButton(object sender, RoutedEventArgs e) { }

    /// <summary>조회 전용 — 서버에 운영 이벤트 삭제 경로가 없다.</summary>
    public override void OnClickDeleteButton(object sender, RoutedEventArgs e) { }

    /// <summary>조회 전용 — 고칠 칸이 없다.</summary>
    public override void OnClickSaveButton(object sender, RoutedEventArgs e) { }

    public override async void OnClickReloadButton(object sender, RoutedEventArgs e)
    {
        if (!await _processGate.WaitAsync(0)) return;
        try
        {
            ReloadButtonEnable = false;
            if (_pCancellationTokenSource != null && !_pCancellationTokenSource.IsCancellationRequested)
            {
                _pCancellationTokenSource.Cancel();
                _pCancellationTokenSource.Dispose();
            }
            _pCancellationTokenSource = new CancellationTokenSource();
            InvalidateCache();
            await DataInitialize(_pCancellationTokenSource.Token);
        }
        catch (OperationCanceledException) { /* 무시 */ }
        catch (Exception ex) { _log?.Error(ex.Message); }
        finally
        {
            ReloadButtonEnable = true;
            _processGate.Release();
        }
    }

    public override void OnSelectionChanged(IList<OperationEventViewModel> rows)
    {
        SelectedItems = rows;
        rows.ToList().ForEach(entity => entity.IsSelected = true);
        NotifyOfPropertyChange(() => SelectedItems);
    }
    #endregion

    #region - Processes -
    public void SetDate(DateTime startDate, DateTime endDate)
    {
        _startDate = startDate;
        _endDate = endDate;
    }

    public async void ClickSearch()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
            InvalidateCache();
            _cancellationTokenSource = new CancellationTokenSource();
            await DataInitialize(_cancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log?.Error(ex.Message); }
        finally { IsVisible = true; }
    }

    public void ClickCancel()
    {
        try
        {
            if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested) return;
            _cancellationTokenSource.Cancel();
        }
        catch (Exception ex) { _log?.Error(ex.Message); }
    }

    /// <summary>첫 쪽을 다시 읽는다. 시험이 끝남을 기다릴 수 있게 Task 를 돌려준다.</summary>
    public Task DataInitialize(CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            try
            {
                IsVisible = false;
                cancellationToken.ThrowIfCancellationRequested();

                // swap-on-success: 첫 쪽이 성공해야만 기존 목록을 바꾼다(실패하면 보존 + 알림 — 화면 공백 방지).
                PagedResult<OperationEventModel> firstPage;
                try
                {
                    firstPage = await _providerService.FetchOperationEventsPageAsync(
                        _startDate, _endDate, 1, PageSize, cancellationToken);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    _log?.Error($"[{nameof(OperationEventPanelViewModel)}] 첫 쪽 조회 실패: {ex.Message}");
                    firstPage = new PagedResult<OperationEventModel> { Success = false };
                }

                if (!firstPage.Success)
                {
                    await _eventAggregator.PublishOnUIThreadAsync(new OpenInfoPopupMessageModel
                    {
                        Explain = "운영 이벤트를 불러오지 못했습니다. 서버 연결 상태를 확인하세요. 기존 목록은 유지됩니다."
                    }, cancellationToken);
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();

                IsSupported = !firstPage.NotSupported;
                _currentPage = firstPage.Page;
                _totalPages = firstPage.TotalPages;
                _totalCount = firstPage.Total;
                _cache = firstPage.Items.ToList();

                DispatcherService.Invoke(() =>
                {
                    ViewModelProvider.Clear();
                    foreach (var item in firstPage.Items) Append(item);
                    RaiseListState();
                });

                HasLoadedList = true;
                SetCachedDate(_startDate, _endDate);
            }
            catch (OperationCanceledException ex)
            {
                _log?.Warning($"Raised {nameof(OperationCanceledException)}({nameof(OperationEventPanelViewModel)}.{nameof(DataInitialize)}) : {ex.Message}");
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

        IsLoadingMore = true;
        try
        {
            var result = await _providerService.FetchOperationEventsPageAsync(
                _startDate, _endDate, _currentPage + 1, PageSize, token);
            if (token.IsCancellationRequested || !result.Success) return;

            _currentPage = result.Page;
            _totalPages = result.TotalPages;
            _totalCount = result.Total;

            DispatcherService.Invoke(() =>
            {
                // offset 쪽이라 두 쪽 사이에 새 이벤트가 생기면 다음 쪽이 앞 쪽 행을 다시 준다(실서버 왕복 E9b) — 이미 붙은 Id 는 건너뛴다.
                var seen = new HashSet<int>(ViewModelProvider.Select(v => v.Model.Id));
                foreach (var item in result.Items)
                {
                    if (!seen.Add(item.Id)) continue;
                    _cache.Add(item);
                    Append(item);
                }
                RaiseListState();
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log?.Error($"[{nameof(OperationEventPanelViewModel)}] 다음 쪽 실패: {ex.Message}"); }
        finally { IsLoadingMore = false; }
    }

    private void Append(OperationEventModel item)
        => ViewModelProvider.Add(new OperationEventViewModel(item) { Index = ViewModelProvider.Count + 1 });

    private void LoadFromCache()
    {
        DispatcherService.Invoke(() =>
        {
            ViewModelProvider.Clear();
            foreach (var item in _cache) Append(item);
            RaiseListState();
        });
    }

    private void RaiseListState()
    {
        NotifyOfPropertyChange(() => LoadedCountText);
        NotifyOfPropertyChange(() => HasMorePages);
    }

    internal bool IsCacheValid()
        => _isCacheValid && _cachedStartDate == _startDate && _cachedEndDate == _endDate;

    public void SetCachedDate(DateTime startDate, DateTime endDate)
    {
        _cachedStartDate = startDate;
        _cachedEndDate = endDate;
        _isCacheValid = true;
    }

    public void InvalidateCache() => _isCacheValid = false;
    #endregion

    #region - Properties -
    public DateTime StartDate => _startDate;
    public DateTime EndDate => _endDate;

    public delegate void SendDate(DateTime start, DateTime end);
    public event SendDate? UpdateAction;

    public bool IsLoadingMore
    {
        get => _isLoadingMore;
        set { _isLoadingMore = value; NotifyOfPropertyChange(() => IsLoadingMore); }
    }

    public string LoadedCountText => $"{ViewModelProvider.Count} / {_totalCount}건";
    public bool HasMorePages => _currentPage < _totalPages;
    public ICommand LoadMoreCommand { get; }

    /// <summary>성공한 조회가 한 번이라도 있었는가(0건이어도 센 것이다).</summary>
    public bool HasLoadedList { get; private set; }

    /// <summary>이 서버 판본에 운영 이벤트 목록이 있는가. 7.0 미만이면 false.</summary>
    public bool IsSupported
    {
        get => _isSupported;
        private set { if (_isSupported == value) return; _isSupported = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(UnsupportedNotice)); }
    }

    /// <summary>판본이 운영 이벤트를 주지 않을 때의 한 줄. 지원하면 null.</summary>
    public string? UnsupportedNotice => IsSupported ? null : "이 서버 판본(7.0 미만)은 운영 이벤트를 제공하지 않습니다";

    /// <summary>운영 목록은 고칠 수도 지울 수도 없다.</summary>
    public bool CanDeleteEvent => false;
    #endregion

    #region - Attributes -
    private readonly EventProviderService _providerService;
    private DateTime _startDate = DateTime.Now.AddDays(-1);
    private DateTime _endDate = DateTime.Now;
    private DateTime _cachedStartDate;
    private DateTime _cachedEndDate;
    private bool _isCacheValid;
    private bool _isSupported = true;
    private List<OperationEventModel> _cache = new();

    private int _currentPage;
    private int _totalPages = 1;
    private int _totalCount;
    private bool _isLoadingMore;
    #endregion
}
