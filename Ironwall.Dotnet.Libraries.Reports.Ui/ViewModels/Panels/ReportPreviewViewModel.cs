using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Lists;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Preview;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;

/// <summary>
/// 미리보기 — 이제 <b>상세 칸</b>에 산다(설계 정본 L1267-1269 · 결정 L-D8 "채택").
/// 서버 자립형 HTML(<c>/api/reports/preview/{id}</c>, 인라인 CSS/Chart.js)을 WebView2 로 그린다.
/// </summary>
/// <remarks>
/// <para>★ <b>공역</b>: WebView2 는 네이티브 창이라 같은 최상위 창의 WPF 위에 그려진다. 그래서 "지금 살아 있는
/// WebView2 를 만들어도 되는가" 를 <see cref="ReportPreviewSurfaceRules"/> 순수 함수가 정하고,
/// 이 뷰모델은 그 판정에 필요한 신호(<see cref="LayoutMode"/> · <see cref="IsLargeViewOpen"/> ·
/// <see cref="IsOverlayOpen"/> · <see cref="IsRuntimeReady"/>)를 모아 <see cref="Surface"/> 로 내놓는다.</para>
/// <para>자리표시자일 때는 뷰가 WebView2 를 <b>만들지 않는다</b>. "빈 칸"이 아니라 까닭과 다음 할 일을 적는다.</para>
/// </remarks>
public class ReportPreviewViewModel : BasePanelViewModel
{
    #region - Ctors -
    public ReportPreviewViewModel(IEventAggregator eventAggregator, ILogService log, IReportApiService api)
        : base(eventAggregator, log)
    {
        _api = api;
        CsvTypes = new ObservableCollection<CsvTypeOption>
        {
            new("탐지", "detection"), new("장애", "malfunction"), new("조치", "action"), new("시스템", "system"),
            new("설정", "config"), new("감사", "audit"), new("로그인", "login"), new("세션", "session"),
        };
        SelectedCsvType = CsvTypes[0];
    }
    #endregion

    #region - Processes -
    /// <summary>고른 줄을 받는다. 완료된 보고서면 HTML 을 받아 오고, 아니면 사유가 적힌 자리표시자가 된다.</summary>
    public async Task ShowAsync(ReportGenerationRow? row)
    {
        Row = row;
        Html = null;
        GenerationId = row?.Id ?? 0;
        RaiseSurface();

        if (row is null || !row.IsCompleted) return;
        await LoadAsync(row.Id);
    }

    /// <summary>미리보기 HTML 로드.</summary>
    public async Task LoadAsync(int generationId)
    {
        try
        {
            IsBusy = true;
            GenerationId = generationId;
            Html = null;   // 로딩은 WPF 로 표시 — WebView2 는 실제 HTML 1회만 네비게이트(이중 네비게이트 레이스 방지)
            RaiseSurface();

            // 15초 타임아웃 가드 — 조회가 멈춰도 무한 로딩 방지
            var fetchTask = _api.GetPreviewHtmlAsync(generationId);
            var done = await Task.WhenAny(fetchTask, Task.Delay(15000));
            string? html = done == fetchTask ? await fetchTask : null;
            if (done != fetchTask) _log?.Warning($"[ReportPreview] HTML 조회 15초 시간초과 (id={generationId}) — 서버/토큰/파이프라인 확인");
            else _log?.Info($"[ReportPreview] HTML fetched: {(html?.Length ?? 0)} chars (id={generationId})");
            Html = string.IsNullOrWhiteSpace(html) ? FailHtml : html;
        }
        catch (Exception ex)
        {
            _log?.Error($"[ReportPreview] Load: {ex.Message}");
            Html = FailHtml;
        }
        finally { IsBusy = false; RaiseSurface(); }
    }

    /// <summary>상세 칸을 비운다(선택 없음 · 콘솔 닫힘).</summary>
    public void Clear()
    {
        Row = null;
        GenerationId = 0;
        Html = null;
        IsBusy = false;
        IsLargeViewOpen = false;
        RaiseSurface();
    }

    // 미리보기 확대/축소 — WebView2.ZoomFactor 에 바인딩(0.5~3.0, 10%씩)
    public void ZoomIn() => ZoomFactor = Math.Min(3.0, Math.Round(ZoomFactor + 0.1, 2));
    public void ZoomOut() => ZoomFactor = Math.Max(0.5, Math.Round(ZoomFactor - 0.1, 2));
    public void ZoomReset() => ZoomFactor = 1.0;
    #endregion

    #region - Properties -
    public int GenerationId { get; private set; }

    private ReportGenerationRow? _row;
    /// <summary>지금 고른 줄 — 메타 · 진행 · 실패 안내가 여기서 나온다(WL L1268).</summary>
    public ReportGenerationRow? Row
    {
        get => _row;
        private set
        {
            _row = value;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(HasRow));
            NotifyOfPropertyChange(nameof(MetaTypeText));
            NotifyOfPropertyChange(nameof(MetaPeriodText));
            NotifyOfPropertyChange(nameof(MetaTemplateText));
            NotifyOfPropertyChange(nameof(MetaRequesterText));
            NotifyOfPropertyChange(nameof(ProgressDetailText));
            NotifyOfPropertyChange(nameof(HasProgress));
            NotifyOfPropertyChange(nameof(FailureText));
            NotifyOfPropertyChange(nameof(HasFailure));
        }
    }

    public bool HasRow => _row != null;

    // 메타(WL L1268): 유형 · 기간 · 템플릿 · 요청자
    public string MetaTypeText => _row?.ReportTypeLabel ?? "—";
    public string MetaPeriodText => _row?.PeriodLabel ?? "—";
    public string MetaTemplateText => _row?.TemplateLabel ?? "—";
    public string MetaRequesterText => string.IsNullOrWhiteSpace(_row?.GeneratorName) ? "—" : _row!.GeneratorName!;

    // 진행(WL L1291): 단계 + 퍼센트 + 갱신 시각
    public string ProgressDetailText => _row?.ProgressDetailText ?? string.Empty;
    public bool HasProgress => !string.IsNullOrEmpty(ProgressDetailText);

    public string FailureText => _row?.FailureText ?? string.Empty;
    public bool HasFailure => !string.IsNullOrEmpty(FailureText);

    public ObservableCollection<CsvTypeOption> CsvTypes { get; }

    private CsvTypeOption? _selectedCsvType;
    /// <summary>상세 CSV 유형(8종 닫힌 값 — WL L1283).</summary>
    public CsvTypeOption? SelectedCsvType { get => _selectedCsvType; set { _selectedCsvType = value; NotifyOfPropertyChange(); } }

    private string? _html;
    /// <summary>WebView2 로 NavigateToString 할 자립형 HTML.</summary>
    public string? Html { get => _html; private set { _html = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(HasHtml)); } }
    public bool HasHtml => !string.IsNullOrEmpty(_html);

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set { _isBusy = value; NotifyOfPropertyChange(); } }

    private double _zoomFactor = 1.0;
    /// <summary>WebView2 확대율(0.5~3.0) — 뷰의 WebView2.ZoomFactor 에 TwoWay 바인딩.</summary>
    public double ZoomFactor { get => _zoomFactor; set { _zoomFactor = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(ZoomText)); } }
    public string ZoomText => $"{ZoomFactor * 100:0}%";

    #region - 공역(airspace) 신호 -
    private ConsoleLayoutMode _layoutMode = ConsoleLayoutMode.Docked;
    /// <summary>콘솔의 폭 판정. 도킹이 아니면 살아 있는 WebView2 를 만들지 않는다(FR-19).</summary>
    public ConsoleLayoutMode LayoutMode
    {
        get => _layoutMode;
        set { if (_layoutMode == value) return; _layoutMode = value; RaiseSurface(); }
    }

    private bool _isLargeViewOpen;
    /// <summary>[크게 보기] 창(별도 HWND)이 열려 있다.</summary>
    public bool IsLargeViewOpen
    {
        get => _isLargeViewOpen;
        set { if (_isLargeViewOpen == value) return; _isLargeViewOpen = value; NotifyOfPropertyChange(); RaiseSurface(); }
    }

    private bool _isOverlayOpen;
    /// <summary>같은 창에 WPF 팝업(확인 · 안내 · 진행)이 떠 있다 — 미리보기가 그것을 가린다.</summary>
    public bool IsOverlayOpen
    {
        get => _isOverlayOpen;
        set { if (_isOverlayOpen == value) return; _isOverlayOpen = value; NotifyOfPropertyChange(); RaiseSurface(); }
    }

    private bool _isRuntimeReady = true;
    /// <summary>
    /// WebView2 런타임을 초기화할 수 있는가. 뷰가 <c>EnsureCoreWebView2Async</c> 에 실패하면 거짓으로 내린다
    /// — 그때도 콘솔은 정상이고 자리표시자가 까닭을 적는다(FR-23).
    /// </summary>
    public bool IsRuntimeReady
    {
        get => _isRuntimeReady;
        set { if (_isRuntimeReady == value) return; _isRuntimeReady = value; NotifyOfPropertyChange(); RaiseSurface(); }
    }

    /// <summary>지금 이 자리에 무엇을 그릴 것인가 — 순수 함수의 판정.</summary>
    public ReportPreviewSurface Surface => ReportPreviewSurfaceRules.Resolve(
        LayoutMode, IsLargeViewOpen, IsOverlayOpen, IsRuntimeReady, Content);

    /// <summary>고른 줄의 미리보기 상태.</summary>
    public ReportPreviewContent Content => _row is null
        ? ReportPreviewContent.NoSelection
        : _row.PreviewContent(IsBusy, HasHtml);

    public bool IsSurfaceLive => Surface.IsLive;
    public string SurfaceReason => Surface.Reason;
    public string SurfaceHint => Surface.Hint;
    public bool HasSurfaceHint => Surface.HasHint;

    /// <summary>[크게 보기] 를 켤 것인가 — 좁은 창에서도 켠다(별도 HWND 라 공역 제약이 없다).</summary>
    public bool CanOpenLargeView => ReportPreviewSurfaceRules.CanOpenLargeView(IsRuntimeReady, Content);

    private void RaiseSurface()
    {
        NotifyOfPropertyChange(nameof(Surface));
        NotifyOfPropertyChange(nameof(IsSurfaceLive));
        NotifyOfPropertyChange(nameof(SurfaceReason));
        NotifyOfPropertyChange(nameof(SurfaceHint));
        NotifyOfPropertyChange(nameof(HasSurfaceHint));
        NotifyOfPropertyChange(nameof(Content));
        NotifyOfPropertyChange(nameof(CanOpenLargeView));
    }
    #endregion
    #endregion

    #region - Attributes -
    private readonly IReportApiService _api;

    internal const string FailHtml =
        "<html><head><meta charset='utf-8'></head><body style='margin:0;background:#0c1117;color:#e06c75;" +
        "font-family:\"Malgun Gothic\",sans-serif;display:flex;align-items:center;justify-content:center;height:100vh'>" +
        "미리보기를 불러오지 못했습니다. (권한 또는 서버 상태 확인)</body></html>";
    #endregion
}

/// <summary>상세 CSV 유형 옵션(표시명/서버 type 값).</summary>
public sealed record CsvTypeOption(string Display, string Value);
