using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
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
                                  ReportPreviewViewModel preview)
        : base(eventAggregator, log)
    {
        _permission = permission;
        ListViewModel = list;
        CreateViewModel = create;
        TemplateViewModel = template;
        PreviewViewModel = preview;

        // 목록 미리보기 요청 / 생성 완료 → 미리보기 오버레이
        ListViewModel.PreviewRequested += OnPreviewRequested;
        CreateViewModel.Generated += OnPreviewRequested;
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
            CreateViewModel.Generated -= OnPreviewRequested;
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

    private bool _isPreviewVisible;
    public bool IsPreviewVisible { get => _isPreviewVisible; set { _isPreviewVisible = value; NotifyOfPropertyChange(); } }

    public bool CanViewReports { get; private set; } = true;
    public bool CanEditReports { get; private set; } = true;

    public string PanelTitle => "보고서 관리";
    #endregion

    #region - Attributes -
    private readonly IPermissionService _permission;
    #endregion
}
