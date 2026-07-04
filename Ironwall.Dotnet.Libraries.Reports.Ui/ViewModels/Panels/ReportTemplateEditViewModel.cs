using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;

/// <summary>
/// 템플릿 추가/수정 다이얼로그(S6) — 이름/설명/공개/기본기간/컴포넌트 편집.
/// 신규 = POST /templates(CUSTOM), 수정 = PATCH /templates/{id}. 콘솔이 오버레이로 호스팅.
/// </summary>
public class ReportTemplateEditViewModel : BasePanelViewModel
{
    #region - Ctors -
    public ReportTemplateEditViewModel(IEventAggregator eventAggregator, ILogService log, IReportApiService api)
        : base(eventAggregator, log)
    {
        _api = api;
        Periods = new ObservableCollection<PeriodOption>
        {
            new("최근 7일", "7d"), new("최근 30일", "30d"), new("최근 90일", "90d"), new("최근 1년", "1y"),
        };
        _selectedPeriod = Periods[0];
    }
    #endregion

    #region - Processes -
    /// <summary>수정 모드 — 전체 조회(GET /templates/{id})로 실제 구성 반영.</summary>
    public async Task Load(ReportTemplateDto tpl)
    {
        IsCreate = false;
        DialogTitle = "템플릿 수정";
        _templateId = tpl.Id;
        StatusText = string.Empty;

        // 목록 DTO는 components가 비어있음(component_count만) → 전체 조회로 실제 구성 확보
        var src = tpl;
        try
        {
            var full = await _api.GetTemplateByIdAsync(tpl.Id);
            if (full.Success && full.Data != null) src = full.Data;
        }
        catch (Exception ex) { _log?.Error($"[ReportTemplateEdit] 전체조회: {ex.Message}"); }

        Name = src.Name;
        Description = src.Description;
        IsPublic = src.IsPublic;
        SelectedPeriod = Periods.FirstOrDefault(p => p.Value == src.DefaultPeriod) ?? Periods[0];
        await LoadCatalogAsync(new HashSet<string>(src.Components.Where(c => c.Enabled).Select(c => c.Id)));
    }

    /// <summary>신규 모드 — 빈 폼 + 카탈로그 전부 미체크.</summary>
    public async Task LoadNew()
    {
        IsCreate = true;
        DialogTitle = "새 템플릿";
        _templateId = 0;
        Name = null;
        Description = null;
        IsPublic = false;
        SelectedPeriod = Periods[0];
        StatusText = string.Empty;
        await LoadCatalogAsync(new HashSet<string>());
    }

    private async Task LoadCatalogAsync(HashSet<string> enabledIds)
    {
        Components.Clear();
        try
        {
            var res = await _api.GetComponentsAsync();
            if (res.Success && res.Data != null)
                foreach (var cat in res.Data)
                {
                    var items = cat.Components ?? cat.Items;
                    if (items == null) continue;
                    foreach (var it in items)
                        Components.Add(new ComponentPick(it.Id, it.Title ?? it.Name ?? it.Id, cat.Label ?? cat.Category)
                        {
                            Enabled = enabledIds.Contains(it.Id),
                        });
                }
        }
        catch (Exception ex) { _log?.Error($"[ReportTemplateEdit] LoadComponents: {ex.Message}"); }
    }

    /// <summary>저장 — 신규는 POST, 수정은 PATCH. 성공 시 Saved(templateId) 통지.</summary>
    public async Task Save()
    {
        if (IsBusy) return;
        var name = (Name ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(name)) { StatusText = "이름을 입력하세요."; return; }
        try
        {
            IsBusy = true;
            var comps = Components.Where(c => c.Enabled)
                                  .Select((c, i) => new ReportComponentConfigDto { Id = c.Id, Order = i, Enabled = true })
                                  .ToList();

            if (IsCreate)
            {
                if (comps.Count == 0) { StatusText = "컴포넌트를 1개 이상 선택하세요."; IsBusy = false; return; }
                var dto = new ReportTemplateCreateDto
                {
                    Name = name,
                    Description = Description,
                    ReportType = "CUSTOM",
                    IsPublic = IsPublic,
                    DefaultPeriod = SelectedPeriod.Value,
                    Components = comps,
                };
                var res = await _api.CreateTemplateAsync(dto);
                if (res.Success && res.Data != null) { StatusText = "생성됨."; Saved?.Invoke(res.Data.Id); }
                else { StatusText = $"생성 실패: {res.Message}"; _log?.Warning($"[ReportTemplateEdit] 생성 실패: {res.Message}"); }
            }
            else
            {
                var dto = new ReportTemplateUpdateDto
                {
                    Name = name,
                    Description = Description,
                    IsPublic = IsPublic,
                    DefaultPeriod = SelectedPeriod.Value,
                    Components = comps.Count > 0 ? comps : null,   // 미선택이면 컴포넌트 변경 안 함
                };
                var res = await _api.UpdateTemplateAsync(_templateId, dto);
                if (res.Success) { StatusText = "저장됨."; Saved?.Invoke(_templateId); }
                else { StatusText = $"저장 실패: {res.Message}"; _log?.Warning($"[ReportTemplateEdit] 저장 실패: {res.Message}"); }
            }
        }
        catch (Exception ex) { _log?.Error($"[ReportTemplateEdit] Save: {ex.Message}"); StatusText = $"오류: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    public void Cancel() => Cancelled?.Invoke();
    #endregion

    #region - Properties -
    public ObservableCollection<PeriodOption> Periods { get; }
    public ObservableCollection<ComponentPick> Components { get; } = new();

    private bool _isCreate;
    public bool IsCreate { get => _isCreate; private set { _isCreate = value; NotifyOfPropertyChange(); } }

    private string _dialogTitle = "템플릿 수정";
    public string DialogTitle { get => _dialogTitle; private set { _dialogTitle = value; NotifyOfPropertyChange(); } }

    private string? _name;
    public string? Name { get => _name; set { _name = value; NotifyOfPropertyChange(); } }

    private string? _description;
    public string? Description { get => _description; set { _description = value; NotifyOfPropertyChange(); } }

    private bool _isPublic;
    public bool IsPublic { get => _isPublic; set { _isPublic = value; NotifyOfPropertyChange(); } }

    private PeriodOption _selectedPeriod;
    public PeriodOption SelectedPeriod { get => _selectedPeriod; set { _selectedPeriod = value; NotifyOfPropertyChange(); } }

    private string _statusText = string.Empty;
    public string StatusText { get => _statusText; set { _statusText = value; NotifyOfPropertyChange(); } }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set { _isBusy = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CanSave)); } }
    public bool CanSave => !_isBusy;

    /// <summary>저장 성공(templateId) — 콘솔이 구독(오버레이 닫기 + 목록 새로고침 + 선택).</summary>
    public event System.Action<int>? Saved;
    /// <summary>취소 — 콘솔이 구독(오버레이 닫기).</summary>
    public event System.Action? Cancelled;
    #endregion

    #region - Attributes -
    private readonly IReportApiService _api;
    private int _templateId;
    #endregion
}
