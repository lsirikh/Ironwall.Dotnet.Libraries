using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System.Collections.ObjectModel;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;

/// <summary>
/// 보고서 생성 탭(S3) — 정형(STANDARD) / 비정형(CUSTOM 컴포넌트 선택) + 생성 요청 + 폴링(S4).
/// </summary>
public class ReportCreateViewModel : BasePanelViewModel
{
    #region - Ctors -
    public ReportCreateViewModel(IEventAggregator eventAggregator, ILogService log, IReportApiService api)
        : base(eventAggregator, log)
    {
        _api = api;
        Periods = new ObservableCollection<PeriodOption>
        {
            new("최근 7일", "7d"), new("최근 30일", "30d"), new("최근 90일", "90d"), new("최근 1년", "1y"),
        };
        SelectedPeriod = Periods[0];
    }
    #endregion

    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        if (Components.Count == 0) await LoadComponentsAsync();
    }
    #endregion

    #region - Processes -
    /// <summary>비정형 컴포넌트 카탈로그 로드(GET /components).</summary>
    public async Task LoadComponentsAsync()
    {
        try
        {
            var res = await _api.GetComponentsAsync();
            Components.Clear();
            if (res.Success && res.Data != null)
            {
                foreach (var cat in res.Data)
                {
                    var items = cat.Components ?? cat.Items;
                    if (items == null) continue;
                    foreach (var it in items)
                        Components.Add(new ComponentPick(it.Id, it.Title ?? it.Name ?? it.Id, cat.Label ?? cat.Category));
                }
            }
        }
        catch (Exception ex) { _log?.Error($"[ReportCreate] LoadComponents: {ex.Message}"); }
    }

    /// <summary>보고서 생성 요청 → 폴링(COMPLETED/FAILED).</summary>
    public async Task Generate()
    {
        if (IsGenerating) return;
        var title = (Title ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(title)) { StatusText = "제목을 입력하세요."; NotifyOfPropertyChange(nameof(StatusText)); return; }

        try
        {
            IsGenerating = true;
            StatusText = "보고서 생성 요청 중…";
            int? templateId = null;

            if (IsCustom)
            {
                // 선택 컴포넌트로 임시 템플릿 생성 → template_id 확보
                var picks = Components.Where(c => c.Enabled).ToList();
                if (picks.Count == 0) { StatusText = "컴포넌트를 1개 이상 선택하세요."; IsGenerating = false; return; }
                var tplRes = await _api.CreateTemplateAsync(new ReportTemplateCreateDto
                {
                    Name = $"{title} ({SelectedPeriod.Value})",
                    ReportType = "CUSTOM",
                    DefaultPeriod = SelectedPeriod.Value,
                    Components = picks.Select((c, i) => new ReportComponentConfigDto { Id = c.Id, Order = i, Enabled = true }).ToList(),
                });
                if (!tplRes.Success || tplRes.Data is null) { StatusText = $"템플릿 생성 실패: {tplRes.Message}"; IsGenerating = false; return; }
                templateId = tplRes.Data.Id;
            }

            var req = new ReportGenerateRequestDto
            {
                ReportType = IsCustom ? "CUSTOM" : "STANDARD",
                Title = title,
                PeriodType = SelectedPeriod.Value,
                TemplateId = templateId,
            };
            var genRes = await _api.GenerateAsync(req);
            if (!genRes.Success || genRes.Data is null) { StatusText = $"생성 요청 실패: {genRes.Message}"; IsGenerating = false; return; }

            var id = genRes.Data.Id;
            StatusText = "생성 중… (GENERATING)";
            var completed = await PollUntilDoneAsync(id);
            if (completed != null && completed.IsCompleted)
            {
                StatusText = "완료됨.";
                Generated?.Invoke(id);
            }
            else if (completed != null && completed.IsFailed)
                StatusText = $"실패: {completed.ErrorMessage}";
            else
                StatusText = "시간 초과(폴링 중단).";
        }
        catch (Exception ex) { _log?.Error($"[ReportCreate] Generate: {ex.Message}"); StatusText = $"오류: {ex.Message}"; }
        finally { IsGenerating = false; }
    }

    /// <summary>폴링(1.5s 간격, ≤120s). 완료/실패 시 DTO 반환, 시간초과 null.</summary>
    private async Task<ReportGenerationDto?> PollUntilDoneAsync(int id)
    {
        var deadline = 120; var waited = 0;
        while (waited < deadline)
        {
            await Task.Delay(1500);
            waited += 2;
            var res = await _api.GetGenerationByIdAsync(id);
            if (res.Success && res.Data != null)
            {
                if (res.Data.IsCompleted || res.Data.IsFailed) return res.Data;
            }
        }
        return null;
    }
    #endregion

    #region - Properties -
    public ObservableCollection<PeriodOption> Periods { get; }
    public ObservableCollection<ComponentPick> Components { get; } = new();

    private bool _isCustom;
    public bool IsCustom { get => _isCustom; set { _isCustom = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(IsStandard)); } }
    public bool IsStandard => !_isCustom;

    private string? _title;
    public string? Title { get => _title; set { _title = value; NotifyOfPropertyChange(); } }

    private PeriodOption _selectedPeriod = null!;
    public PeriodOption SelectedPeriod { get => _selectedPeriod; set { _selectedPeriod = value; NotifyOfPropertyChange(); } }

    private bool _isGenerating;
    public bool IsGenerating { get => _isGenerating; set { _isGenerating = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CanGenerate)); } }
    public bool CanGenerate => !_isGenerating;

    private string _statusText = string.Empty;
    public string StatusText { get => _statusText; set { _statusText = value; NotifyOfPropertyChange(); } }

    /// <summary>생성 완료(generation id) — 콘솔이 구독(미리보기 오픈 등).</summary>
    public event Action<int>? Generated;
    #endregion

    #region - Attributes -
    private readonly IReportApiService _api;
    #endregion
}

/// <summary>기간 선택 옵션(표시명/값).</summary>
public sealed record PeriodOption(string Display, string Value);

/// <summary>비정형 컴포넌트 선택 항목.</summary>
public sealed class ComponentPick : PropertyChangedBase
{
    public ComponentPick(string id, string display, string? category) { Id = id; Display = display; Category = category; }
    public string Id { get; }
    public string Display { get; }
    public string? Category { get; }
    private bool _enabled;
    public bool Enabled { get => _enabled; set { _enabled = value; NotifyOfPropertyChange(); } }
}
