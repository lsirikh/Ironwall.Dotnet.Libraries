using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;

/// <summary>
/// 보고서 관리 콘솔(S1) — 독립 모달. 목록/생성/템플릿 탭 호스트 + 미리보기 오버레이 + RBAC 게이팅.
/// </summary>
public class ReportConsoleViewModel : BasePanelViewModel
{
    #region - Ctors -
    public ReportConsoleViewModel(IEventAggregator eventAggregator,
                                  ILogService log,
                                  IPermissionService permission,
                                  ReportListViewModel list,
                                  ReportCreateViewModel create,
                                  ReportTemplateViewModel template,
                                  ReportPreviewViewModel preview,
                                  ReportTemplateEditViewModel edit)
        : base(eventAggregator, log)
    {
        _permission = permission;
        ListViewModel = list;
        CreateViewModel = create;
        TemplateViewModel = template;
        PreviewViewModel = preview;
        EditViewModel = edit;

        // 목록 미리보기 요청 → 미리보기 오버레이 / 생성 완료 → 목록 새로고침 + 미리보기
        ListViewModel.PreviewRequested += OnPreviewRequested;
        CreateViewModel.Generated += OnReportGenerated;
        // 템플릿 추가/수정 요청 → 편집 오버레이
        TemplateViewModel.EditRequested += OnEditRequested;
        TemplateViewModel.CreateRequested += OnTemplateCreateRequested;
        EditViewModel.Saved += OnEditSaved;
        EditViewModel.Cancelled += OnEditCancelled;
        _permission.PermissionsChanged += OnPermissionsChanged;
    }
    #endregion

    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        await ScreenExtensions.TryActivateAsync(ListViewModel, cancellationToken);
        await ScreenExtensions.TryActivateAsync(CreateViewModel, cancellationToken);
        await ScreenExtensions.TryActivateAsync(TemplateViewModel, cancellationToken);
        RefreshPermissions();
    }

    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        await ScreenExtensions.TryDeactivateAsync(ListViewModel, close, cancellationToken);
        await ScreenExtensions.TryDeactivateAsync(CreateViewModel, close, cancellationToken);
        await ScreenExtensions.TryDeactivateAsync(TemplateViewModel, close, cancellationToken);
        if (close)
        {
            ListViewModel.PreviewRequested -= OnPreviewRequested;
            CreateViewModel.Generated -= OnReportGenerated;
            TemplateViewModel.EditRequested -= OnEditRequested;
            TemplateViewModel.CreateRequested -= OnTemplateCreateRequested;
            EditViewModel.Saved -= OnEditSaved;
            EditViewModel.Cancelled -= OnEditCancelled;
            _permission.PermissionsChanged -= OnPermissionsChanged;
        }
        await base.OnDeactivateAsync(close, cancellationToken);
    }
    #endregion

    #region - Processes -
    private async void OnPreviewRequested(int generationId)
    {
        try
        {
            IsPreviewVisible = true;
            await ScreenExtensions.TryActivateAsync(PreviewViewModel);
            await PreviewViewModel.LoadAsync(generationId);
        }
        catch (Exception ex) { _log?.Error($"[ReportConsole] Preview: {ex.Message}"); }
    }

    public void ClosePreview() => IsPreviewVisible = false;

    /// <summary>생성 완료 → 목록 새로고침(완료 뱃지 반영) + 미리보기 오픈.</summary>
    private async void OnReportGenerated(int generationId)
    {
        try { await ListViewModel.LoadAsync(); } catch (Exception ex) { _log?.Error($"[ReportConsole] 목록 새로고침: {ex.Message}"); }
        OnPreviewRequested(generationId);
    }

    private async void OnEditRequested(ReportTemplateDto tpl)
    {
        try
        {
            IsEditVisible = true;
            await ScreenExtensions.TryActivateAsync(EditViewModel);
            await EditViewModel.Load(tpl);
        }
        catch (Exception ex) { _log?.Error($"[ReportConsole] Edit: {ex.Message}"); }
    }

    private async void OnTemplateCreateRequested()
    {
        try
        {
            IsEditVisible = true;
            await ScreenExtensions.TryActivateAsync(EditViewModel);
            await EditViewModel.LoadNew();
        }
        catch (Exception ex) { _log?.Error($"[ReportConsole] Create: {ex.Message}"); }
    }

    private async void OnEditSaved(int templateId)
    {
        IsEditVisible = false;
        await TemplateViewModel.LoadAsync();          // 목록 새로고침
        TemplateViewModel.SelectById(templateId);     // 선택 유지 → 수정 버튼 계속 사용 가능(재오픈 가능)
    }

    private void OnEditCancelled() => IsEditVisible = false;

    /// <summary>편집 오버레이 헤더 ✕ — 저장 없이 닫기.</summary>
    public void CloseEdit() => IsEditVisible = false;

    public Task Close() => TryCloseAsync();

    private void OnPermissionsChanged() => RefreshPermissions();

    private void RefreshPermissions()
    {
        CanViewReports = _permission?.CanView("reports") ?? true;
        CanEditReports = _permission?.CanEdit("reports") ?? true;
        NotifyOfPropertyChange(nameof(CanViewReports));
        NotifyOfPropertyChange(nameof(CanEditReports));
    }
    #endregion

    #region - Properties -
    public ReportListViewModel ListViewModel { get; }
    public ReportCreateViewModel CreateViewModel { get; }
    public ReportTemplateViewModel TemplateViewModel { get; }
    public ReportPreviewViewModel PreviewViewModel { get; }
    public ReportTemplateEditViewModel EditViewModel { get; }

    private bool _isPreviewVisible;
    public bool IsPreviewVisible { get => _isPreviewVisible; set { _isPreviewVisible = value; NotifyOfPropertyChange(); } }

    private bool _isEditVisible;
    public bool IsEditVisible { get => _isEditVisible; set { _isEditVisible = value; NotifyOfPropertyChange(); } }

    public bool CanViewReports { get; private set; } = true;
    public bool CanEditReports { get; private set; } = true;

    public string PanelTitle => "보고서 관리";
    #endregion

    #region - Attributes -
    private readonly IPermissionService _permission;
    #endregion
}
