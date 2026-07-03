using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System.Collections.ObjectModel;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;

/// <summary>
/// 템플릿 관리 탭(S6) — 비정형 템플릿 목록 조회·삭제.
/// </summary>
public class ReportTemplateViewModel : BasePanelViewModel
{
    #region - Ctors -
    public ReportTemplateViewModel(IEventAggregator eventAggregator, ILogService log, IReportApiService api)
        : base(eventAggregator, log)
    {
        _api = api;
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
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            var res = await _api.GetTemplatesAsync(1, 100);
            Items.Clear();
            if (res.Success && res.Data != null)
                foreach (var t in res.Data) Items.Add(t);
            NotifyOfPropertyChange(nameof(IsEmpty));
        }
        catch (Exception ex) { _log?.Error($"[ReportTemplate] Load: {ex.Message}"); }
        finally { IsBusy = false; }
    }

    /// <summary>선택 템플릿 삭제(DELETE /templates/{id}).</summary>
    public async Task Delete()
    {
        var item = SelectedItem;
        if (item is null) return;
        try
        {
            var res = await _api.DeleteTemplateAsync(item.Id);
            if (res.Success) { Items.Remove(item); NotifyOfPropertyChange(nameof(IsEmpty)); }
            else _log?.Warning($"[ReportTemplate] 삭제 실패: {res.Message}");
        }
        catch (Exception ex) { _log?.Error($"[ReportTemplate] Delete: {ex.Message}"); }
    }
    #endregion

    #region - Properties -
    public ObservableCollection<ReportTemplateDto> Items { get; } = new();
    public bool IsEmpty => Items.Count == 0 && !IsBusy;

    private ReportTemplateDto? _selectedItem;
    public ReportTemplateDto? SelectedItem { get => _selectedItem; set { _selectedItem = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CanDelete)); } }
    public bool CanDelete => SelectedItem != null;

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set { _isBusy = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(IsEmpty)); } }
    #endregion

    #region - Attributes -
    private readonly IReportApiService _api;
    #endregion
}
