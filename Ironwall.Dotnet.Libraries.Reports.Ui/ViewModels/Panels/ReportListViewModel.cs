using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System.Collections.ObjectModel;
using System.IO;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;

/// <summary>
/// 보고서 목록 탭(S2) — 조회·검색·갱신·다운로드(PDF)·미리보기·취소·삭제.
/// 취소/삭제 확인·결과는 앱 표준 EventAggregator 팝업(OpenConfirmPopupMessageModel/OpenInfoPopupMessageModel) 사용 — MessageBox 금지.
/// </summary>
public class ReportListViewModel : BasePanelViewModel
                                 , IHandle<CallCancelReportGenerationProcessMessageModel>
                                 , IHandle<CallDeleteReportGenerationProcessMessageModel>
{
    #region - Ctors -
    public ReportListViewModel(IEventAggregator eventAggregator, ILogService log, IReportApiService api)
        : base(eventAggregator, log)
    {
        _api = api;
        // 상태 어휘는 서버 닫힌 어휘(ReportGenerationStatus.All) 하나에서만 만든다 —
        //   ⚠ 여기에 소문자·한글·빈 문자열을 넣으면 v8.0 서버가 그 자리에서 422 로 거부한다(실측).
        //   "전체"는 화면 전용 표지이고 API 에는 파라미터 자체를 보내지 않는다(빈 값 ≠ 필터 없음).
        StatusFilters = new ObservableCollection<string>(new[] { AllFilterLabel }.Concat(ReportGenerationStatus.All));
        SelectedStatusFilter = AllFilterLabel;
    }
    #endregion

    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        await LoadAsync();
    }
    #endregion

    #region - Processes -
    /// <summary>이력 조회(GET /generations). 검색 필터 적용. (검색·갱신 공용)</summary>
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            // "전체"(화면 표지)는 null 로 — 서비스가 파라미터 자체를 붙이지 않는다.
            var status = SelectedStatusFilter is AllFilterLabel or null ? null : SelectedStatusFilter;
            var res = await _api.GetGenerationsAsync(page: 1, limit: PageLimit, status: status);
            Items.Clear();
            if (res.Success && res.Data != null)
            {
                foreach (var it in res.Data) Items.Add(it);
                LoadError = null;
                // v8.0 배포본은 pagination 을 실제 총계로 채운다(실측). 구 판본은 null → 화면 건수로 대체한다.
                TotalCount = res.Pagination?.Total ?? res.Total;
            }
            else
            {
                // 사유는 res.Message 가 아니라 ApiErrorTextHelper 로 읽는다 — 배포본 400·404 봉투에는 top-level message 가 없다.
                _log?.Warning($"[ReportList] 이력 조회 실패: {res.ErrorText()}");
                TotalCount = null;
                LoadError = res.Error?.Code == "VALUE_NOT_ALLOWED"
                    ? "상태 필터 값이 올바르지 않습니다. 목록에서 다시 선택하세요."
                    : "서버에 연결하지 못했습니다. 잠시 후 [갱신]을 눌러 다시 시도하세요.";
            }
            NotifyOfPropertyChange(nameof(IsEmpty));
            NotifyOfPropertyChange(nameof(CountText));
            var completed = 0; foreach (var it in Items) if (it.IsCompleted) completed++;
            _log?.Info($"[ReportList] 목록 로드 — 표시 {Items.Count}건 / 총 {TotalCount?.ToString() ?? "미제공"}(완료 {completed}건, 필터={status ?? AllFilterLabel})");
        }
        catch (Exception ex) { _log?.Error($"[ReportList] LoadAsync: {ex.Message}"); }
        finally { IsBusy = false; }
    }

    /// <summary>갱신 — 현재 필터로 목록 재조회. (Caliburn PropertyChangedBase.Refresh 섀도잉 회피 위해 Reload 명명)</summary>
    public Task Reload() => LoadAsync();

    /// <summary>선택 보고서 다운로드(PDF) → SaveFileDialog → 저장.</summary>
    public async Task Download()
    {
        var item = SelectedItem;
        if (item is null || !item.IsCompleted) return;
        try
        {
            var result = await _api.DownloadPdfAsync(item.Id);
            if (!result.Success || result.Bytes is null)
            {
                _log?.Warning($"[ReportList] 다운로드 실패: {result.Error}");
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "다운로드 실패",
                    Explain = ApiErrorTextHelper.Or(result.Error, "다운로드하지 못했습니다.")
                });
                return;
            }
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF 파일 (*.pdf)|*.pdf",
                FileName = string.IsNullOrWhiteSpace(result.FileName) ? $"{item.Title}.pdf" : result.FileName,
                RestoreDirectory = true,
            };
            if (dlg.ShowDialog() == true)
            {
                await File.WriteAllBytesAsync(dlg.FileName, result.Bytes);
                _log?.Info($"[ReportList] 저장 완료: {dlg.FileName}");
            }
        }
        catch (Exception ex) { _log?.Error($"[ReportList] Download: {ex.Message}"); }
    }

    /// <summary>선택 보고서 미리보기 요청(콘솔이 오버레이로 표시). 완료(COMPLETED) 보고서만 가능.</summary>
    public void Preview()
    {
        var item = SelectedItem;
        _log?.Info($"[ReportList] Preview 클릭 — {(item is null ? "선택 없음" : $"id={item.Id}, status={item.Status}, IsCompleted={item.IsCompleted}")}");
        if (item is null || !item.IsCompleted) return;
        _log?.Info($"[ReportList] Preview → PreviewRequested 발화(id={item.Id}, 구독자 {(PreviewRequested is null ? "없음" : "있음")})");
        PreviewRequested?.Invoke(item.Id);
    }

    /// <summary>삭제 — 확인 팝업(확인 시 CallDelete… 발행 → IHandle에서 수행).</summary>
    public async Task Delete()
    {
        var item = SelectedItem;
        if (item is null) return;
        _log?.Info($"[ReportList] Delete 클릭 — id={item.Id}, 확인 팝업 발행");
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Explain = $"'{item.Title}' 보고서를 삭제하시겠습니까?",
            MessageModel = new CallDeleteReportGenerationProcessMessageModel()
        });
    }

    /// <summary>취소 — 상태 컬럼 인라인 x(진행중 행). 확인 팝업(확인 시 CallCancel… 발행 → IHandle에서 수행).
    /// item은 행 DataContext($dataContext) 전달 — SelectedItem에 의존하지 않음.</summary>
    public async Task CancelRow(ReportGenerationDto item)
    {
        item ??= SelectedItem!;
        if (item is null || !item.IsInProgress) return;
        _pendingCancelItem = item;
        _log?.Info($"[ReportList] Cancel 클릭(행 x) — id={item.Id}, status={item.Status}");
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Explain = $"'{item.Title}' 보고서 생성을 취소하시겠습니까?",
            MessageModel = new CallCancelReportGenerationProcessMessageModel()
        });
    }
    #endregion

    #region - IHandles (확인 팝업 '예' → 실제 수행 + 결과 안내) -
    public async Task HandleAsync(CallCancelReportGenerationProcessMessageModel message, CancellationToken cancellationToken)
    {
        var item = _pendingCancelItem ?? SelectedItem;
        _pendingCancelItem = null;
        if (item is null) return;
        // Confirm → ProgressCircle(결과 대기) → Inform
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel(), cancellationToken);
        OpenInfoPopupMessageModel result;
        try
        {
            var res = await _api.CancelGenerationAsync(item.Id, cancellationToken);
            await LoadAsync();   // CANCELLED 반영
            // task_cancelled=false 는 "DB 상태만 CANCELLED 로 마킹"이다(진행 태스크를 못 끊음) — 단정하지 않는다.
            var taskCancelled = res.Data?.TaskCancelled;
            result = res.Success
                ? new OpenInfoPopupMessageModel
                {
                    Title = "생성 취소",
                    Explain = taskCancelled == false
                        ? "취소로 표시했습니다. 진행 중이던 작업이 곧바로 멈추지 않을 수 있어 목록에서 상태를 확인하세요."
                        : "보고서 생성을 취소했습니다."
                }
                : new OpenInfoPopupMessageModel { Title = "취소 실패", Explain = res.ErrorText("취소하지 못했습니다.") };
        }
        catch (Exception ex)
        {
            _log?.Error($"[ReportList] Cancel: {ex.Message}");
            result = new OpenInfoPopupMessageModel { Title = "취소 실패", Explain = "취소 중 오류가 발생했습니다." };
        }
        await _eventAggregator.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
        await _eventAggregator.PublishOnCurrentThreadAsync(result, cancellationToken);
    }

    public async Task HandleAsync(CallDeleteReportGenerationProcessMessageModel message, CancellationToken cancellationToken)
    {
        var item = SelectedItem;
        if (item is null) return;
        _log?.Info($"[ReportList] Delete 확인됨 → API 삭제(id={item.Id})");
        // Confirm → ProgressCircle(결과 대기) → Inform
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel(), cancellationToken);
        OpenInfoPopupMessageModel result;
        try
        {
            var res = await _api.DeleteGenerationAsync(item.Id, cancellationToken);
            if (res.Success) { Items.Remove(item); NotifyOfPropertyChange(nameof(IsEmpty)); }
            result = res.Success
                ? new OpenInfoPopupMessageModel { Title = "삭제 완료", Explain = "보고서를 삭제했습니다." }
                : new OpenInfoPopupMessageModel { Title = "삭제 실패", Explain = res.ErrorText("삭제하지 못했습니다.") };
        }
        catch (Exception ex)
        {
            _log?.Error($"[ReportList] Delete: {ex.Message}");
            result = new OpenInfoPopupMessageModel { Title = "삭제 실패", Explain = "삭제 중 오류가 발생했습니다." };
        }
        await _eventAggregator.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
        await _eventAggregator.PublishOnCurrentThreadAsync(result, cancellationToken);
    }
    #endregion

    #region - Properties -
    public ObservableCollection<ReportGenerationDto> Items { get; } = new();
    public ObservableCollection<string> StatusFilters { get; }
    public bool IsEmpty => Items.Count == 0 && !IsBusy;

    private int? _totalCount;
    /// <summary>서버가 알려준 전체 건수(현재 필터 적용). 구 판본은 pagination 이 null 이라 <c>null</c>.</summary>
    public int? TotalCount
    {
        get => _totalCount;
        private set { _totalCount = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CountText)); NotifyOfPropertyChange(nameof(HasMore)); }
    }

    /// <summary>표시 건수가 전체보다 적은가 — 한 페이지(<see cref="PageLimit"/>)를 넘긴 이력이 있다는 뜻.</summary>
    public bool HasMore => TotalCount.HasValue && TotalCount.Value > Items.Count;

    /// <summary>건수 표시 — 총계를 아는 경우에만 "N건 중 M건".</summary>
    public string CountText
        => TotalCount.HasValue
            ? (HasMore ? $"{TotalCount.Value}건 중 {Items.Count}건 표시(최근 {PageLimit}건)" : $"{Items.Count}건")
            : $"{Items.Count}건";

    private string? _loadError;
    /// <summary>목록 조회 실패 사유(SSL/연결 실패 등) — 빈 목록을 "보고서 없음"과 구분해 안내.</summary>
    public string? LoadError { get => _loadError; set { _loadError = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(EmptyStateText)); } }
    /// <summary>빈 상태 문구 — 조회 실패면 실패 사유, 아니면 "보고서 없음".</summary>
    public string EmptyStateText => LoadError ?? "생성된 보고서가 없습니다.";

    private ReportGenerationDto? _selectedItem;
    public ReportGenerationDto? SelectedItem
    {
        get => _selectedItem;
        set { _selectedItem = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CanDownload)); NotifyOfPropertyChange(nameof(CanDelete)); NotifyOfPropertyChange(nameof(CanCancel)); }
    }
    public bool CanDelete => SelectedItem != null;
    public bool CanCancel => SelectedItem?.IsInProgress == true;

    private string? _selectedStatusFilter;
    public string? SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set { _selectedStatusFilter = value; NotifyOfPropertyChange(); }
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(IsEmpty)); }
    }

    public bool CanDownload => SelectedItem?.IsCompleted == true;

    /// <summary>미리보기 요청(generation id) — 콘솔이 구독.</summary>
    public event Action<int>? PreviewRequested;
    #endregion

    #region - Attributes -
    /// <summary>상태 필터 콤보의 "필터 없음" 표지 — <b>서버 어휘가 아니다</b>(API 에는 null 로 전달).</summary>
    public const string AllFilterLabel = "전체";
    /// <summary>한 번에 가져오는 이력 수(서버 limit 최대 100).</summary>
    public const int PageLimit = 100;

    private readonly IReportApiService _api;
    /// <summary>인라인 x 취소 대상 — 확인 팝업 왕복 동안 대상 행 보관(SelectedItem 비의존).</summary>
    private ReportGenerationDto? _pendingCancelItem;
    #endregion
}
