using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System.Collections.ObjectModel;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;

/// <summary>
/// 템플릿 관리 탭(S6) — 템플릿 목록 조회·추가·수정·삭제. 삭제 확인·결과는 앱 표준 팝업(EventAggregator).
/// </summary>
public class ReportTemplateViewModel : BasePanelViewModel
                                     , IHandle<CallDeleteReportTemplateProcessMessageModel>
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
            {
                foreach (var t in res.Data) Items.Add(t);
                LoadError = null;
            }
            else
            {
                _log?.Warning($"[ReportTemplate] 조회 실패: {res.Message}");
                LoadError = "서버에 연결하지 못했습니다. 잠시 후 [새로고침]을 눌러 다시 시도하세요.";
            }
            NotifyOfPropertyChange(nameof(IsEmpty));
        }
        catch (Exception ex) { _log?.Error($"[ReportTemplate] Load: {ex.Message}"); }
        finally { IsBusy = false; }
    }

    /// <summary>선택 템플릿 삭제 — 확인 팝업(확인 시 CallDelete… 발행 → IHandle에서 수행).</summary>
    public async Task Delete()
    {
        var item = SelectedItem;
        if (item is null) return;
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Explain = $"'{item.Name}' 템플릿을 삭제하시겠습니까?",
            MessageModel = new CallDeleteReportTemplateProcessMessageModel()
        });
    }

    /// <summary>삭제 확인됨 → DELETE /templates/{id} → 결과 안내.</summary>
    public async Task HandleAsync(CallDeleteReportTemplateProcessMessageModel message, CancellationToken cancellationToken)
    {
        var item = SelectedItem;
        if (item is null) return;
        // Confirm → ProgressCircle(결과 대기) → Inform
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel(), cancellationToken);
        OpenInfoPopupMessageModel result;
        try
        {
            var res = await _api.DeleteTemplateAsync(item.Id, cancellationToken);
            if (res.Success) { Items.Remove(item); NotifyOfPropertyChange(nameof(IsEmpty)); }
            result = res.Success
                ? new OpenInfoPopupMessageModel { Title = "삭제 완료", Explain = "템플릿을 삭제했습니다." }
                : new OpenInfoPopupMessageModel { Title = "삭제 실패", Explain = res.Message ?? "삭제하지 못했습니다." };
        }
        catch (Exception ex)
        {
            _log?.Error($"[ReportTemplate] Delete: {ex.Message}");
            result = new OpenInfoPopupMessageModel { Title = "삭제 실패", Explain = "삭제 중 오류가 발생했습니다." };
        }
        await _eventAggregator.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
        await _eventAggregator.PublishOnCurrentThreadAsync(result, cancellationToken);
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

    private string? _loadError;
    /// <summary>조회 실패 사유(연결 실패 등) — 빈 목록을 "템플릿 없음"과 구분해 안내(목록 탭과 동일 패턴).</summary>
    public string? LoadError { get => _loadError; set { _loadError = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(EmptyStateText)); } }
    public string EmptyStateText => LoadError ?? "저장된 템플릿이 없습니다.";

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
