using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Common;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
/****************************************************************************
   Purpose      : 감사 로그 뷰어(읽기전용) — GOP /api/audit-logs 조회 + 날짜필터 + 무한스크롤
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : IAccountApiService 직접 주입(게이트웨이 미노출, GOP 모드 전용). append-only 서버 로그라 read-only.
                  날짜범위(기본 최근 7일) 필터 + 페이지네이션(무한 스크롤, DataGridScrollEndBehavior).
                  이벤트 패널(DetectionEventPanelViewModel) 패턴 이식 — swap-on-success + DispatcherService 마셜.
                  종료일은 T23:59:59 상향 전송(DatePicker 날짜-only라 당일 로그 포함).
****************************************************************************/
public class AuditLogPanelViewModel : BasePanelViewModel
{
    private readonly IAccountApiService _api;

    public AuditLogPanelViewModel(IEventAggregator eventAggregator, ILogService log, IAccountApiService api)
        : base(eventAggregator, log)
    {
        _api = api;
        // 기본 날짜범위 = 최근 7일 (ctor 1회 세팅 — 탭 재활성 시 사용자 선택 날짜 보존)
        _startDate = DateTime.Now.AddDays(-7);
        _endDate = DateTime.Now;
        _endDateDisplay = _startDate;
        // 람다가 _cancellationTokenSource '필드'를 캡처 — 매 발화 시 재평가되어 재활성 후 새 CTS 토큰을 읽는다(값 캡처 아님).
        LoadMoreCommand = new AsyncRelayCommand(() => LoadNextPageAsync(_cancellationTokenSource?.Token ?? CancellationToken.None));
    }

    /// <summary>감사 로그 목록. DataGrid ItemsSource. 무한 스크롤로 다음 페이지를 append.</summary>
    public ObservableCollection<AuditLogDto> Items { get; } = new();

    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        await ReloadAsync(_cancellationTokenSource?.Token ?? cancellationToken);
    }

    /// <summary>갱신 버튼 — 현재 날짜범위로 첫 페이지부터 재조회.</summary>
    public async Task OnClickReloadButton() => await ReloadAsync(_cancellationTokenSource?.Token ?? CancellationToken.None);

    /// <summary>검색 버튼 — 선택한 날짜범위를 적용하고 첫 페이지부터 재조회.</summary>
    public async Task OnClickSearch() => await ReloadAsync(_cancellationTokenSource?.Token ?? CancellationToken.None);

    /// <summary>첫 페이지 조회 + swap-on-success. 페이지 상태를 리셋한다.</summary>
    private async Task ReloadAsync(CancellationToken ct)
    {
        _currentPage = 0;
        _totalPages = 1;
        _totalCount = 0;
        try
        {
            var (sd, ed) = BuildDateRange();
            var res = await _api.GetAuditLogsAsync(1, PAGE_SIZE, sd, ed, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;

            if (res.Success && res.Data is not null)
            {
                _currentPage = res.Pagination?.Page ?? 1;
                _totalPages = res.Pagination?.TotalPages ?? 1;
                _totalCount = res.Pagination?.Total ?? res.Data.Count;

                DispatcherService.Invoke(() =>
                {
                    Items.Clear();
                    foreach (var d in res.Data) Items.Add(d);
                    NotifyOfPropertyChange(() => LoadedCountText);
                    NotifyOfPropertyChange(() => HasMorePages);
                });
            }
            else
            {
                await _eventAggregator!.PublishOnUIThreadAsync(new OpenInfoPopupMessageModel
                { Title = "감사 로그", Explain = $"불러오기 실패: {res.Error?.Message ?? res.Message}" }, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log?.Error($"[AuditLog] 로드 실패: {ex.Message}"); }
    }

    /// <summary>다음 페이지 조회 후 기존 목록에 append (무한 스크롤). 중복 로드 가드.</summary>
    public async Task LoadNextPageAsync(CancellationToken ct = default)
    {
        if (_isLoadingMore || !HasMorePages) return;
        if (ct.IsCancellationRequested) return;

        _isLoadingMore = true;   // 가드 필드 — 프로퍼티 세터와 함께 명시(세터 변경 시 가드 무음파손 방지)
        IsLoadingMore = true;
        try
        {
            var (sd, ed) = BuildDateRange();
            var res = await _api.GetAuditLogsAsync(_currentPage + 1, PAGE_SIZE, sd, ed, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;
            if (!res.Success || res.Data is null) return;

            _currentPage = res.Pagination?.Page ?? (_currentPage + 1);
            _totalPages = res.Pagination?.TotalPages ?? _totalPages;
            _totalCount = res.Pagination?.Total ?? _totalCount;

            DispatcherService.Invoke(() =>
            {
                foreach (var d in res.Data) Items.Add(d);
                NotifyOfPropertyChange(() => LoadedCountText);
                NotifyOfPropertyChange(() => HasMorePages);
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log?.Error($"[AuditLog] 다음 페이지 로드 실패: {ex.Message}"); }
        finally
        {
            _isLoadingMore = false;
            IsLoadingMore = false;
        }
    }

    /// <summary>날짜범위 → ISO(yyyy-MM-ddTHH:mm:ss). 종료일은 당일 끝(23:59:59)까지 포함.</summary>
    private (string startDate, string endDate) BuildDateRange()
    {
        var sd = _startDate.Date.ToString("yyyy-MM-ddTHH:mm:ss");
        var ed = _endDate.Date.AddDays(1).AddSeconds(-1).ToString("yyyy-MM-ddTHH:mm:ss");
        return (sd, ed);
    }

    #region - Properties -
    public DateTime StartDate
    {
        get => _startDate;
        set { _startDate = value; NotifyOfPropertyChange(() => StartDate); EndDateDisplay = _startDate; }
    }

    public DateTime EndDate
    {
        get => _endDate;
        set { _endDate = value; NotifyOfPropertyChange(() => EndDate); }
    }

    /// <summary>종료일 DatePicker의 최소 표시일 — 시작일 이전 선택 방지.</summary>
    public DateTime EndDateDisplay
    {
        get => _endDateDisplay;
        set { _endDateDisplay = value; NotifyOfPropertyChange(() => EndDateDisplay); }
    }

    public bool IsLoadingMore
    {
        get => _isLoadingMore;
        set { _isLoadingMore = value; NotifyOfPropertyChange(() => IsLoadingMore); }
    }

    /// <summary>로드된 건수 / 전체 건수 표시.</summary>
    public string LoadedCountText => $"{Items.Count} / {_totalCount}건";

    /// <summary>다음 페이지 존재 여부 — 무한 스크롤 종료 판정.</summary>
    public bool HasMorePages => _currentPage < _totalPages;

    /// <summary>스크롤 하단 도달 시 발화(DataGridScrollEndBehavior 바인딩).</summary>
    public ICommand LoadMoreCommand { get; }
    #endregion
    #region - Attributes -
    private const int PAGE_SIZE = 100;   // 서버 limit 최대치
    private DateTime _startDate;
    private DateTime _endDate;
    private DateTime _endDateDisplay;
    private int _currentPage;
    private int _totalPages = 1;
    private int _totalCount;
    private bool _isLoadingMore;
    #endregion
}
