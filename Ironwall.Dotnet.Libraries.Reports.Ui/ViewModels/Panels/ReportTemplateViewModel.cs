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

    /// <summary>선택 템플릿 수정 요청 → 콘솔이 편집 오버레이를 연다(PATCH /templates/{id}는 편집 VM이 수행).</summary>
    public void Edit()
    {
        var item = SelectedItem;
        if (item is null) return;
        EditRequested?.Invoke(item);
    }

    /// <summary>새 템플릿 추가 요청 → 콘솔이 편집 오버레이를 신규 모드로 연다.</summary>
    public void Create() => CreateRequested?.Invoke();

    /// <summary>id로 항목 재선택 — 저장 후 목록 새로고침해도 선택 유지(수정 버튼 계속 활성).</summary>
    public void SelectById(int id) => SelectedItem = Items.FirstOrDefault(t => t.Id == id);
    #endregion

    #region - Properties -
    public ObservableCollection<ReportTemplateDto> Items { get; } = new();
    public bool IsEmpty => Items.Count == 0 && !IsBusy;

    private ReportTemplateDto? _selectedItem;
    public ReportTemplateDto? SelectedItem { get => _selectedItem; set { _selectedItem = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CanDelete)); NotifyOfPropertyChange(nameof(CanEdit)); } }
    public bool CanDelete => SelectedItem != null;
    public bool CanEdit => SelectedItem != null;

    /// <summary>수정 요청(선택 템플릿) — ReportConsoleViewModel 이 구독하여 편집 오버레이를 연다.</summary>
    public event Action<ReportTemplateDto>? EditRequested;
    /// <summary>추가 요청 — ReportConsoleViewModel 이 구독하여 편집 오버레이를 신규 모드로 연다.</summary>
    public event System.Action? CreateRequested;

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set { _isBusy = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(IsEmpty)); } }
    #endregion

    #region - Attributes -
    private readonly IReportApiService _api;
    #endregion
}
