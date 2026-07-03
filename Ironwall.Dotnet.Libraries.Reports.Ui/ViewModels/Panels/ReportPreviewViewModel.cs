using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System.Collections.ObjectModel;
using System.IO;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;

/// <summary>
/// 미리보기(S5, 옵션 A 네이티브) — GET /preview 구조화 JSON을 섹션(표/차트/요약)으로 렌더.
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
    /// <summary>미리보기 데이터 로드. 콘솔이 generation id로 호출.</summary>
    public async Task LoadAsync(int generationId)
    {
        try
        {
            IsBusy = true;
            GenerationId = generationId;
            var res = await _api.GetPreviewAsync(generationId);
            Sections.Clear();
            if (res.Success && res.Data != null)
            {
                Title = res.Data.Title;
                MetaText = $"{res.Data.ReportType} · {res.Data.StartDate} ~ {res.Data.EndDate}";
                if (res.Data.Sections != null)
                    foreach (var s in res.Data.Sections) Sections.Add(s);
            }
            else
            {
                Title = "미리보기";
                MetaText = $"미리보기 조회 실패: {res.Message}";
            }
            NotifyOfPropertyChange(nameof(Title));
            NotifyOfPropertyChange(nameof(MetaText));
            NotifyOfPropertyChange(nameof(IsEmpty));
        }
        catch (Exception ex) { _log?.Error($"[ReportPreview] Load: {ex.Message}"); }
        finally { IsBusy = false; }
    }

    /// <summary>선택 보고서 PDF 다운로드.</summary>
    public async Task Download()
    {
        if (GenerationId <= 0) return;
        try
        {
            var result = await _api.DownloadPdfAsync(GenerationId);
            if (!result.Success || result.Bytes is null) { _log?.Warning($"[ReportPreview] 다운로드 실패: {result.Error}"); return; }
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF 파일 (*.pdf)|*.pdf",
                FileName = string.IsNullOrWhiteSpace(result.FileName) ? $"{Title}.pdf" : result.FileName,
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
    #endregion

    #region - Properties -
    public ObservableCollection<ReportSectionDto> Sections { get; } = new();
    public int GenerationId { get; private set; }
    public string Title { get; private set; } = "미리보기";
    public string MetaText { get; private set; } = string.Empty;
    public bool IsEmpty => Sections.Count == 0 && !IsBusy;

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set { _isBusy = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(IsEmpty)); } }
    #endregion

    #region - Attributes -
    private readonly IReportApiService _api;
    #endregion
}
