using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System.IO;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;

/// <summary>
/// 미리보기(S5) — 서버 자립형 HTML(/api/reports/preview/{id}, 인라인 CSS/Chart.js)을 WebView2로 embed.
/// Bearer는 API 파이프라인 자동부착, reports:view 서버 인가. 오프라인 렌더(외부 리소스 없음).
/// </summary>
public class ReportPreviewViewModel : BasePanelViewModel
{
    #region - Ctors -
    public ReportPreviewViewModel(IEventAggregator eventAggregator, ILogService log, IReportApiService api)
        : base(eventAggregator, log)
    {
        _api = api;
    }
    #endregion

    #region - Processes -
    /// <summary>미리보기 HTML 로드. 콘솔이 generation id로 호출 → 뷰(WebView2)가 Html 변경을 구독해 NavigateToString.</summary>
    public async Task LoadAsync(int generationId)
    {
        try
        {
            IsBusy = true;
            DownloadStatus = null;
            GenerationId = generationId;
            Html = null;   // 로딩은 WPF(IsBusy)로 표시 — WebView2는 실제 HTML 1회만 네비게이트(이중 네비게이트 레이스 방지)

            // 15초 타임아웃 가드 — 조회가 멈춰도 무한 로딩 방지(레이스면 즉시, 멈춤이면 실패표시+경고로그)
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
        finally { IsBusy = false; }
    }

    /// <summary>선택 보고서 PDF 다운로드.</summary>
    public async Task Download()
    {
        if (GenerationId <= 0) return;
        try
        {
            DownloadStatus = null;
            var result = await _api.DownloadPdfAsync(GenerationId);
            if (!result.Success || result.Bytes is null)
            {
                _log?.Warning($"[ReportPreview] 다운로드 실패: {result.Error}");
                // WebView2(네이티브 HwndHost)는 airspace로 같은 창의 WPF 팝업 위에 그려진다 → 앱 InfoPopup이 미리보기 뒤로 깔림.
                // 미리보기에선 팝업 대신 툴바 인라인 상태로 안내(레이어 충돌 회피). 목록 다운로드는 WebView2 없어 팝업 유지.
                DownloadStatus = "다운로드 실패 — " + (result.Error ?? "다운로드하지 못했습니다.");
                return;
            }
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF 파일 (*.pdf)|*.pdf",
                FileName = string.IsNullOrWhiteSpace(result.FileName) ? $"report_{GenerationId}.pdf" : result.FileName,
                RestoreDirectory = true,
            };
            if (dlg.ShowDialog() == true)
            {
                await File.WriteAllBytesAsync(dlg.FileName, result.Bytes);
                _log?.Info($"[ReportPreview] 저장 완료: {dlg.FileName}");
            }
        }
        catch (Exception ex) { _log?.Error($"[ReportPreview] Download: {ex.Message}"); }
    }

    // 미리보기 확대/축소 — WebView2.ZoomFactor 에 바인딩(0.5~3.0, 10%씩)
    public void ZoomIn() => ZoomFactor = Math.Min(3.0, Math.Round(ZoomFactor + 0.1, 2));
    public void ZoomOut() => ZoomFactor = Math.Max(0.5, Math.Round(ZoomFactor - 0.1, 2));
    public void ZoomReset() => ZoomFactor = 1.0;
    #endregion

    #region - Properties -
    public int GenerationId { get; private set; }

    private string? _html;
    /// <summary>WebView2 로 NavigateToString 할 자립형 HTML.</summary>
    public string? Html { get => _html; private set { _html = value; NotifyOfPropertyChange(); } }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set { _isBusy = value; NotifyOfPropertyChange(); } }

    private string? _downloadStatus;
    /// <summary>미리보기 내 다운로드 실패 안내 — WebView2 airspace로 앱 팝업이 가려지므로 툴바 인라인 표기.</summary>
    public string? DownloadStatus { get => _downloadStatus; set { _downloadStatus = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(HasDownloadStatus)); } }
    public bool HasDownloadStatus => !string.IsNullOrEmpty(DownloadStatus);

    private double _zoomFactor = 1.0;
    /// <summary>WebView2 확대율(0.5~3.0) — 뷰의 WebView2.ZoomFactor 에 TwoWay 바인딩.</summary>
    public double ZoomFactor { get => _zoomFactor; set { _zoomFactor = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(ZoomText)); } }
    public string ZoomText => $"{ZoomFactor * 100:0}%";
    #endregion

    #region - Attributes -
    private readonly IReportApiService _api;

    private const string FailHtml =
        "<html><head><meta charset='utf-8'></head><body style='margin:0;background:#0c1117;color:#e06c75;" +
        "font-family:\"Malgun Gothic\",sans-serif;display:flex;align-items:center;justify-content:center;height:100vh'>" +
        "미리보기를 불러오지 못했습니다. (권한 또는 서버 상태 확인)</body></html>";
    #endregion
}
