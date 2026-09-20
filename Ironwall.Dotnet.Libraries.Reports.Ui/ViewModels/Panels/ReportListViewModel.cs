using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Lists;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Preview;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;

/// <summary>
/// 생성 이력 — 콘솔의 <b>목록 칸</b>(설계 정본 window-layout-system-storyboard.html L1263-1266).
/// 조회 · 상태 칩 필터 · 검색 · 다운로드(PDF · CSV) · 취소 · 삭제.
/// </summary>
/// <remarks>
/// <para>전송 경로는 <b>그대로</b>다 — 이 판은 틀과 배치를 바꾼다. 취소 · 삭제 확인은 앱 표준
/// EventAggregator 팝업(MessageBox 금지)이고, 그 팝업을 띄우기 전에 <b>공역 게이트</b>로 미리보기를 내린다
/// (WebView2 가 팝업을 가린다 — WL L1290).</para>
/// <para>검색은 서버 왕복이 없다(서버에 제목 검색 파라미터가 없다). 상태만 서버로 간다.</para>
/// </remarks>
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
        //   ⚠ 소문자·한글·빈 문자열을 넣으면 v8.0 서버가 그 자리에서 422 로 거부한다(실측).
        //   "전체"는 아무 칩도 안 눌린 상태이고, API 에는 파라미터 자체를 붙이지 않는다.
        StatusChips = new ObservableCollection<ReportStatusChip>(ReportStatusChipRules.Create());
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
    /// <summary>이력 조회(GET /generations). 상태 칩만 서버로 간다.</summary>
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        if (!CanView)
        {
            // 읽을 권한이 없으면 목록을 부르지 않는다 - 단추만 끄면 뷰모델/자동화 경로가 그대로 뚫린다.
            Items.Clear();
            Rows.Clear();
            SelectedItem = null;
            TotalCount = null;
            LoadError = NoViewPermissionText;
            NotifyOfPropertyChange(nameof(IsEmpty));
            NotifyOfPropertyChange(nameof(CountText));
            return;
        }
        try
        {
            IsBusy = true;
            // 아무 칩도 안 눌렸으면 null — 서비스가 파라미터 자체를 붙이지 않는다(빈 값은 422).
            var status = ReportStatusChipRules.ToRequestParameter(StatusChips);
            var res = await _api.GetGenerationsAsync(page: 1, limit: PageLimit, status: status);
            Items.Clear();
            if (res.Success && res.Data != null)
            {
                foreach (var row in ReportGenerationRow.From(res.Data)) Items.Add(row);
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
                    ? "상태 필터 값이 올바르지 않습니다. 칩을 다시 눌러 보세요."
                    : "서버에 연결하지 못했습니다. 잠시 후 [갱신]을 눌러 다시 시도하세요.";
            }
            ApplyFilter();
            _log?.Info($"[ReportList] 목록 로드 — 표시 {Rows.Count}건 / 받은 {Items.Count}건 / 총 {TotalCount?.ToString() ?? "미제공"}(필터={status ?? ReportStatusChipRules.AllLabel})");
        }
        catch (Exception ex) { _log?.Error($"[ReportList] LoadAsync: {ex.Message}"); }
        finally { IsBusy = false; }
    }

    /// <summary>갱신 — 현재 필터로 목록 재조회. (Caliburn PropertyChangedBase.Refresh 섀도잉 회피 위해 Reload 명명)</summary>
    public Task Reload() => LoadAsync();

    /// <summary>상태 칩 하나를 누른다 — 배타 선택 + 같은 칩을 다시 누르면 "전체". 서버 재조회를 동반한다.</summary>
    public async Task ToggleStatusAsync(ReportStatusChip chip)
    {
        if (chip is null) return;
        ReportStatusChipRules.Toggle(StatusChips, chip);
        NotifyOfPropertyChange(nameof(FilterSummary));
        await LoadAsync();
    }

    /// <summary>검색어 · 목록 변화에 따라 보이는 줄을 다시 만든다(서버 왕복 없음).</summary>
    public void ApplyFilter()
    {
        var keep = SelectedItem;

        // 별표: Clear() 하지 않는다 - 묶인 DataGrid 의 선택이 그 자리에서 풀려 미리보기가 비워진다.
        ObservableReconcile.Apply(Rows, Items.Where(r => r.Matches(SearchText)).ToList());

        // 고른 줄이 검색에 가려졌으면 선택을 놓는다 — 안 보이는 줄을 쥔 채로 [삭제] 가 눌리면 안 된다.
        if (keep != null && !Rows.Contains(keep)) SelectedItem = null;

        NotifyOfPropertyChange(nameof(IsEmpty));
        NotifyOfPropertyChange(nameof(CountText));
        NotifyOfPropertyChange(nameof(InProgressCount));
    }

    /// <summary>선택 보고서 다운로드(PDF) → SaveFileDialog → 저장.</summary>
    public async Task DownloadAsync(CancellationToken token = default)
    {
        var item = SelectedItem;
        if (item is null || !item.IsCompleted || !CanView) return;
        try
        {
            ActionStatus = null;
            var result = await _api.DownloadPdfAsync(item.Id, token);
            if (!result.Success || result.Bytes is null)
            {
                // ⚠ 팝업을 쓰지 않는다 — 상세 칸에 미리보기(WebView2)가 떠 있으면 팝업이 그 뒤로 깔린다.
                //    미완료(400)와 파일 유실(410 PDF_FILE_MISSING)은 API 서비스가 이미 다른 문구로 갈라 준다(WL L1281).
                _log?.Warning($"[ReportList] 다운로드 실패: {result.Error}");
                ActionStatus = ApiErrorTextHelper.Or(result.Error, "다운로드하지 못했습니다.");
                return;
            }
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF 파일 (*.pdf)|*.pdf",
                // 서버가 준 이름은 경계 밖 입력이다 - 경로 조각/장치 이름/금지 문자를 다듬는다.
                FileName = SafeFileName.Sanitize(result.FileName, $"{item.Title}.pdf", ".pdf"),
                RestoreDirectory = true,
            };
            if (dlg.ShowDialog() == true)
            {
                await File.WriteAllBytesAsync(dlg.FileName, result.Bytes);
                ActionStatus = "PDF 를 저장했습니다.";
                _log?.Info($"[ReportList] 저장 완료: {dlg.FileName}");
            }
        }
        catch (Exception ex) { _log?.Error($"[ReportList] Download: {ex.Message}"); ActionStatus = "다운로드 중 오류가 발생했습니다."; }
    }

    /// <summary>상세 CSV 다운로드(선택 유형, 8종 닫힌 값 — WL L1283).</summary>
    public async Task DownloadCsvAsync(string? type, CancellationToken token = default)
    {
        var item = SelectedItem;
        if (item is null || !item.IsCompleted || string.IsNullOrEmpty(type) || !CanView) return;
        try
        {
            ActionStatus = null;
            var result = await _api.DownloadDetailCsvAsync(item.Id, type!, token);
            if (!result.Success || result.Bytes is null)
            {
                _log?.Warning($"[ReportList] CSV 실패: {result.Error}");
                ActionStatus = "CSV 실패 — " + ApiErrorTextHelper.Or(result.Error, "다운로드하지 못했습니다.");
                return;
            }
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV 파일 (*.csv)|*.csv",
                FileName = SafeFileName.Sanitize(result.FileName, $"report_{item.Id}_{type}.csv", ".csv"),
                RestoreDirectory = true,
            };
            if (dlg.ShowDialog() == true)
            {
                await File.WriteAllBytesAsync(dlg.FileName, result.Bytes);
                ActionStatus = "CSV 를 저장했습니다.";
                _log?.Info($"[ReportList] CSV 저장: {dlg.FileName}");
            }
        }
        catch (Exception ex) { _log?.Error($"[ReportList] DownloadCsv: {ex.Message}"); ActionStatus = "CSV 다운로드 중 오류가 발생했습니다."; }
    }

    /// <summary>삭제 — 확인 팝업(확인 시 CallDelete… 발행 → IHandle 에서 수행).</summary>
    public async Task Delete()
    {
        var item = SelectedItem;
        if (item is null) return;
        _log?.Info($"[ReportList] Delete 클릭 — id={item.Id}, 확인 팝업 발행");
        // ★ 팝업을 띄우기 전에 미리보기를 내린다 — 안 그러면 확인 창이 WebView2 뒤로 깔린다.
        // 확인 왕복 동안 대상 줄을 고정한다 - 그 사이 선택이 바뀌어도 엉뚱한 보고서를 지우지 않는다.
        _pendingDeleteItem = item;
        HoldAirspace();
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Explain = $"'{item.Title}' 보고서를 삭제하시겠습니까?",
            MessageModel = new CallDeleteReportGenerationProcessMessageModel()
        });
    }

    /// <summary>생성 취소 — 대기 · 생성중만. 상세 칸 하단의 파괴적 동작 묶음에서 부른다(WL L1280).</summary>
    public async Task Cancel()
    {
        var item = SelectedItem;
        if (item is null || !item.IsInProgress) return;
        _pendingCancelItem = item;
        _log?.Info($"[ReportList] Cancel 클릭 — id={item.Id}, status={item.Status}");
        HoldAirspace();
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
        if (item is null) { ReleaseAirspace(); return; }

        HoldAirspace();
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
        HoldAirspace();     // 곧바로 안내 팝업을 연다 - 그 위로 미리보기가 올라오면 안 된다
        await _eventAggregator.PublishOnCurrentThreadAsync(result, cancellationToken);
    }

    public async Task HandleAsync(CallDeleteReportGenerationProcessMessageModel message, CancellationToken cancellationToken)
    {
        var item = _pendingDeleteItem ?? SelectedItem;
        _pendingDeleteItem = null;
        if (item is null) { ReleaseAirspace(); return; }
        HoldAirspace();
        _log?.Info($"[ReportList] Delete 확인됨 → API 삭제(id={item.Id})");

        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel(), cancellationToken);
        OpenInfoPopupMessageModel result;
        try
        {
            var res = await _api.DeleteGenerationAsync(item.Id, cancellationToken);
            if (res.Success)
            {
                Items.Remove(item);
                if (ReferenceEquals(SelectedItem, item)) SelectedItem = null;
                ApplyFilter();
                Deleted?.Invoke(item.Id);
            }
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
        HoldAirspace();     // 곧바로 안내 팝업을 연다 - 그 위로 미리보기가 올라오면 안 된다
        await _eventAggregator.PublishOnCurrentThreadAsync(result, cancellationToken);
    }

    /// <summary>팝업을 띄우기 직전에 미리보기를 내린다(겹쳐 잠가도 안전하다).</summary>
    private void HoldAirspace() => _airspaceHold ??= Airspace.Block();

    /// <summary>
    /// 미리보기를 되돌린다. <b>콘솔이 팝업 닫힘(ClosePopupMessageModel)에서 부른다</b> -
    /// 확인 창에서 [취소] 를 누르면 우리 HandleAsync 는 <b>영영 불리지 않기</b> 때문이다
    /// (호스트 확인 팝업은 [확인] 에서만 메시지를 낸다). 콘솔을 닫을 때도 부른다.
    /// </summary>
    public void ReleaseAirspace()
    {
        var hold = _airspaceHold;
        _airspaceHold = null;
        hold?.Dispose();
        _pendingDeleteItem = null;
        _pendingCancelItem = null;
    }
    #endregion

    #region - Properties -
    /// <summary>서버에서 받은 전부(검색 전).</summary>
    public ObservableCollection<ReportGenerationRow> Items { get; } = new();

    /// <summary>화면에 보이는 줄(검색 적용).</summary>
    public ObservableCollection<ReportGenerationRow> Rows { get; } = new();

    public ObservableCollection<ReportStatusChip> StatusChips { get; }

    public string FilterSummary => ReportStatusChipRules.SummaryOf(StatusChips);

    /// <summary>진행 중 건수 — 레일 배지(WL L1262).</summary>
    public int InProgressCount => Items.Count(i => i.IsInProgress);

    public bool IsEmpty => Rows.Count == 0 && !IsBusy;

    private string? _searchText;
    /// <summary>제목 · 아이디 · 요청자 검색(화면 거르기).</summary>
    public string? SearchText
    {
        get => _searchText;
        set { _searchText = value; NotifyOfPropertyChange(); ApplyFilter(); }
    }

    private int? _totalCount;
    /// <summary>서버가 알려준 전체 건수(현재 필터 적용). 구 판본은 pagination 이 null 이라 <c>null</c>.</summary>
    public int? TotalCount
    {
        get => _totalCount;
        private set { _totalCount = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CountText)); NotifyOfPropertyChange(nameof(HasMore)); }
    }

    /// <summary>표시 건수가 전체보다 적은가 — 한 페이지(<see cref="PageLimit"/>)를 넘긴 이력이 있다는 뜻.</summary>
    public bool HasMore => TotalCount.HasValue && TotalCount.Value > Items.Count;

    /// <summary>상태 띠에 적을 건수(WL L1266 아래 30px 띠).</summary>
    public string CountText
    {
        get
        {
            var shown = Rows.Count;
            var filtered = shown != Items.Count ? $"{Items.Count}건 중 {shown}건 검색" : $"{shown}건";
            if (!TotalCount.HasValue) return filtered;
            return HasMore ? $"{TotalCount.Value}건 중 {Items.Count}건 표시(최근 {PageLimit}건) · {filtered}" : filtered;
        }
    }

    private string? _loadError;
    /// <summary>목록 조회 실패 사유 — 빈 목록을 "보고서 없음"과 구분해 안내.</summary>
    public string? LoadError { get => _loadError; set { _loadError = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(EmptyStateText)); } }

    /// <summary>
    /// 빈 상태 문구. 검색은 <b>받아 온 최근 100건 안에서만</b> 거르므로 "없습니다" 라고 단정하지 않는다
    /// (더 오래된 보고서는 애초에 화면에 와 있지 않다).
    /// </summary>
    public string EmptyStateText => LoadError
        ?? (string.IsNullOrWhiteSpace(SearchText)
            ? "생성된 보고서가 없습니다."
            : $"최근 {PageLimit}건 안에서 찾지 못했습니다. 더 오래된 보고서는 이 목록에 없습니다.");

    /// <summary>조회 권한 - 콘솔이 꽂아 준다. 거짓이면 목록도 내려받기도 하지 않는다.</summary>
    public bool CanView { get; set; } = true;

    internal const string NoViewPermissionText = "보고서를 볼 권한이 없습니다.";

    private string? _actionStatus;
    /// <summary>
    /// 다운로드 · 저장 결과 한 줄. <b>팝업이 아니다</b> — 상세 칸에 WebView2 가 있으면 팝업이 그 뒤로 깔린다.
    /// </summary>
    public string? ActionStatus
    {
        get => _actionStatus;
        set { _actionStatus = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(HasActionStatus)); }
    }
    public bool HasActionStatus => !string.IsNullOrEmpty(ActionStatus);

    private ReportGenerationRow? _selectedItem;
    public ReportGenerationRow? SelectedItem
    {
        get => _selectedItem;
        set
        {
            _selectedItem = value;
            _actionStatus = null;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(HasActionStatus));
            NotifyOfPropertyChange(nameof(ActionStatus));
            NotifyOfPropertyChange(nameof(CanDownload));
            NotifyOfPropertyChange(nameof(CanDelete));
            NotifyOfPropertyChange(nameof(CanCancel));
            SelectionChanged?.Invoke(value);
        }
    }

    public bool CanDelete => SelectedItem != null;
    public bool CanCancel => SelectedItem?.IsInProgress == true;
    public bool CanDownload => SelectedItem?.IsCompleted == true;

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(IsEmpty)); }
    }

    /// <summary>선택이 바뀌었다 — 콘솔이 구독해 상세 칸(미리보기)을 채운다.</summary>
    public event Action<ReportGenerationRow?>? SelectionChanged;

    /// <summary>삭제 성공(generation id) — 콘솔이 구독해 상태 띠에 적는다.</summary>
    public event Action<int>? Deleted;

    /// <summary>공역 게이트 — 콘솔이 꽂아 준다. 없으면 아무 일도 안 한다(단위 테스트).</summary>
    public IPreviewAirspaceGate Airspace { get; set; } = NullPreviewAirspaceGate.Instance;

    /// <summary>화면에서 고를 수 있는 줄(테스트 · 뷰 공용).</summary>
    public IReadOnlyList<ReportGenerationRow> VisibleRows => Rows;
    #endregion

    #region - Attributes -
    /// <summary>한 번에 가져오는 이력 수(서버 limit 최대 100).</summary>
    public const int PageLimit = 100;

    private readonly IReportApiService _api;
    /// <summary>취소 대상 - 확인 팝업 왕복 동안 대상 줄 보관(SelectedItem 비의존).</summary>
    private ReportGenerationRow? _pendingCancelItem;
    /// <summary>삭제 대상 - 같은 까닭. 확인 사이에 선택이 바뀌어도 엉뚱한 줄을 지우지 않는다.</summary>
    private ReportGenerationRow? _pendingDeleteItem;
    /// <summary>팝업이 떠 있는 동안 미리보기를 내려 두는 표.</summary>
    private IDisposable? _airspaceHold;
    #endregion
}
