using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System.Collections.ObjectModel;
using System.IO;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;

/// <summary>
/// 보고서 목록 탭(S2) — 생성 이력 조회·검색·PDF 다운로드·미리보기 트리거.
/// </summary>
public class ReportListViewModel : BasePanelViewModel
{
    #region - Ctors -
    public ReportListViewModel(IEventAggregator eventAggregator, ILogService log, IReportApiService api)
        : base(eventAggregator, log)
    {
        _api = api;
        StatusFilters = new ObservableCollection<string> { "전체", "COMPLETED", "GENERATING", "PENDING", "FAILED" };
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
    /// <summary>이력 조회(GET /generations). 검색 필터 적용.</summary>
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
                foreach (var it in res.Data) Items.Add(it);
            else
                _log?.Warning($"[ReportList] 이력 조회 실패: {res.Message}");
            NotifyOfPropertyChange(nameof(IsEmpty));
        }
        catch (Exception ex) { _log?.Error($"[ReportList] LoadAsync: {ex.Message}"); }
        finally { IsBusy = false; }
    }

    /// <summary>선택 보고서 PDF 다운로드 → SaveFileDialog → 저장.</summary>
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

    /// <summary>선택 보고서 미리보기 요청(콘솔이 오버레이로 표시).</summary>
    public void Preview()
    {
        var item = SelectedItem;
        if (item is null || !item.IsCompleted) return;
        PreviewRequested?.Invoke(item.Id);
    }
    #endregion

    #region - Properties -
    public ObservableCollection<ReportGenerationDto> Items { get; } = new();
    public ObservableCollection<string> StatusFilters { get; }
    public bool IsEmpty => Items.Count == 0 && !IsBusy;

    private ReportGenerationDto? _selectedItem;
    public ReportGenerationDto? SelectedItem
    {
        get => _selectedItem;
        set { _selectedItem = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CanDownload)); }
    }

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
    #endregion
}
