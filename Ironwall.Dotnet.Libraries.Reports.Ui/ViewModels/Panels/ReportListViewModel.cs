using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
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
        StatusFilters = new ObservableCollection<string> { "전체", "COMPLETED", "GENERATING", "PENDING", "FAILED", "CANCELLED" };
        SelectedStatusFilter = "전체";
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
            var status = SelectedStatusFilter is "전체" or null ? null : SelectedStatusFilter;
            var res = await _api.GetGenerationsAsync(page: 1, limit: 100, status: status);
            Items.Clear();
            if (res.Success && res.Data != null)
            {
                foreach (var it in res.Data) Items.Add(it);
                LoadError = null;
            }
            else
            {
                _log?.Warning($"[ReportList] 이력 조회 실패: {res.Message}");
                LoadError = "서버에 연결하지 못했습니다. 잠시 후 [갱신]을 눌러 다시 시도하세요.";
            }
            NotifyOfPropertyChange(nameof(IsEmpty));
            var completed = 0; foreach (var it in Items) if (it.IsCompleted) completed++;
            _log?.Info($"[ReportList] 목록 로드 — 총 {Items.Count}건(완료 {completed}건, 필터={status ?? "전체"})");
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
                    Explain = result.Error ?? "다운로드하지 못했습니다."
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
            result = res.Success
                ? new OpenInfoPopupMessageModel { Title = "생성 취소", Explain = "보고서 생성을 취소했습니다." }
                : new OpenInfoPopupMessageModel { Title = "취소 실패", Explain = res.Message ?? "취소하지 못했습니다." };
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
                : new OpenInfoPopupMessageModel { Title = "삭제 실패", Explain = res.Message ?? "삭제하지 못했습니다." };
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
    private readonly IReportApiService _api;
    /// <summary>인라인 x 취소 대상 — 확인 팝업 왕복 동안 대상 행 보관(SelectedItem 비의존).</summary>
    private ReportGenerationDto? _pendingCancelItem;
    #endregion
}
