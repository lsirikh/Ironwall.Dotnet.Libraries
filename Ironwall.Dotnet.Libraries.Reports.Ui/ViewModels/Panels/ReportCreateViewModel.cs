using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System.Collections.ObjectModel;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;

/// <summary>
/// 보고서 생성 탭(S3) — ①표준 전체(STANDARD, 전 섹션) 또는 ②템플릿 기반(CUSTOM: 저장 템플릿 선택) + 제목/기간 → 생성 + 폴링(S4).
/// 컴포넌트 구성은 템플릿 탭에서만 관리(여기선 만들어진 템플릿을 고르기만).
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
        EndDate = DateTime.Today;
        StartDate = DateTime.Today.AddDays(-7);
    }
    #endregion

    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        await LoadTemplatesAsync();
    }
    #endregion

    #region - Processes -
    /// <summary>템플릿 드롭다운 로드(GET /templates) — 템플릿 기반 선택용.</summary>
    public async Task LoadTemplatesAsync()
    {
        try
        {
            var res = await _api.GetTemplatesAsync(1, 100);
            Templates.Clear();
            if (res.Success && res.Data != null)
                foreach (var t in res.Data) Templates.Add(t);
            if (SelectedTemplate is null && Templates.Count > 0) SelectedTemplate = Templates[0];
            NotifyOfPropertyChange(nameof(HasTemplates));
        }
        catch (Exception ex) { _log?.Error($"[ReportCreate] LoadTemplates: {ex.Message}"); }
    }

    /// <summary>보고서 생성 요청 → 폴링(COMPLETED/FAILED).</summary>
    public async Task Generate()
    {
        if (IsGenerating) return;
        var title = (Title ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(title)) { StatusText = "제목을 입력하세요."; return; }
        if (IsTemplateBased && SelectedTemplate is null) { StatusText = "템플릿을 선택하세요."; return; }
        if (IsCustomRange)
        {
            if (StartDate is null || EndDate is null) { StatusText = "시작일과 끝일을 지정하세요."; return; }
            if (EndDate < StartDate) { StatusText = "끝일이 시작일보다 빠릅니다."; return; }
        }

        try
        {
            IsGenerating = true;
            GenProgress = 0;
            StatusText = "보고서 생성 요청 중…";
            var req = new ReportGenerateRequestDto
            {
                ReportType = IsTemplateBased ? "CUSTOM" : "STANDARD",
                Title = title,
                PeriodType = IsCustomRange ? "custom" : SelectedPeriod.Value,
                TemplateId = IsTemplateBased ? SelectedTemplate!.Id : null,
                StartDate = IsCustomRange ? StartDate?.ToString("yyyy-MM-dd") : null,
                EndDate = IsCustomRange ? EndDate?.ToString("yyyy-MM-dd") : null,
            };
            var genRes = await _api.GenerateAsync(req);
            if (!genRes.Success || genRes.Data is null) { StatusText = $"생성 요청 실패: {genRes.Message}"; IsGenerating = false; return; }

            var id = genRes.Data.Id;
            StatusText = "생성 중… (GENERATING)";
            var completed = await PollUntilDoneAsync(id);
            if (completed != null && completed.IsCompleted) { GenProgress = 100; StatusText = "완료됨."; Generated?.Invoke(id); }
            else if (completed != null && completed.IsCancelled) StatusText = "취소됨.";
            else if (completed != null && completed.IsFailed) StatusText = $"실패: {FailReason(completed.ErrorMessage)}";
            else StatusText = "시간 초과(폴링 중단). 목록에서 상태를 확인하세요.";
        }
        catch (Exception ex) { _log?.Error($"[ReportCreate] Generate: {ex.Message}"); StatusText = $"오류: {ex.Message}"; }
        finally { IsGenerating = false; }
    }

    /// <summary>폴링(1.5s 간격). 완료/실패 시 DTO 반환, 시간초과 null.</summary>
    private async Task<ReportGenerationDto?> PollUntilDoneAsync(int id)
    {
        var waited = 0;
        while (waited < 180)
        {
            await Task.Delay(1500);
            waited += 2;
            var res = await _api.GetGenerationByIdAsync(id);
            if (res.Success && res.Data != null)
            {
                var d = res.Data;
                if (d.IsInProgress) { GenProgress = d.ProgressPct; StatusText = $"생성 중… {d.ProgressPct}% · {d.ProgressStageLabel}"; }
                if (d.IsCompleted || d.IsFailed || d.IsCancelled) return d;
            }
        }
        return null;
    }

    /// <summary>서버 error_message → 사용자 안내 문구 분화(v6.0).</summary>
    private static string FailReason(string? msg)
    {
        if (string.IsNullOrWhiteSpace(msg)) return "생성 실패";
        if (msg.Contains("server restarted")) return "서버 재시작으로 실패 — 재생성하세요";
        if (msg.Contains("stalled")) return "생성 지연으로 중단 — 재시도하세요";
        if (msg.Contains("Cancelled")) return "취소됨";
        return msg;
    }
    #endregion

    #region - Properties -
    public ObservableCollection<PeriodOption> Periods { get; }
    public ObservableCollection<ReportTemplateDto> Templates { get; } = new();
    public bool HasTemplates => Templates.Count > 0;

    private bool _isTemplateBased;
    /// <summary>false = 표준 전체(STANDARD, 전 섹션) · true = 템플릿 기반(CUSTOM, 저장 템플릿 선택).</summary>
    public bool IsTemplateBased { get => _isTemplateBased; set { _isTemplateBased = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(IsStandard)); } }
    /// <summary>표준 전체 라디오용(settable) — true 설정 시 템플릿모드 해제.</summary>
    public bool IsStandard { get => !_isTemplateBased; set { if (value) IsTemplateBased = false; } }

    private ReportTemplateDto? _selectedTemplate;
    public ReportTemplateDto? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            _selectedTemplate = value;
            NotifyOfPropertyChange();
            // 템플릿의 기본기간을 기간에 반영(사용자가 다시 바꿀 수 있음)
            if (value != null)
            {
                var p = Periods.FirstOrDefault(x => x.Value == value.DefaultPeriod);
                if (p != null) SelectedPeriod = p;
            }
        }
    }

    private string? _title;
    public string? Title { get => _title; set { _title = value; NotifyOfPropertyChange(); } }

    private PeriodOption _selectedPeriod = null!;
    public PeriodOption SelectedPeriod { get => _selectedPeriod; set { _selectedPeriod = value; NotifyOfPropertyChange(); } }

    private bool _isCustomRange;
    /// <summary>false=프리셋(7d…) · true=직접 지정(시작/끝 DatePicker). ⚠ 서버 PRD #6 반영 후 정식 동작(그전엔 custom 전송 시 422).</summary>
    public bool IsCustomRange { get => _isCustomRange; set { _isCustomRange = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(IsPreset)); } }
    /// <summary>프리셋 라디오용(settable).</summary>
    public bool IsPreset { get => !_isCustomRange; set { if (value) IsCustomRange = false; } }

    private DateTime? _startDate;
    public DateTime? StartDate { get => _startDate; set { _startDate = value; NotifyOfPropertyChange(); } }
    private DateTime? _endDate;
    public DateTime? EndDate { get => _endDate; set { _endDate = value; NotifyOfPropertyChange(); } }

    private bool _isGenerating;
    public bool IsGenerating { get => _isGenerating; set { _isGenerating = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CanGenerate)); } }
    public bool CanGenerate => !_isGenerating;

    private int _genProgress;
    /// <summary>생성 진행률 %(0~100) — 폴링이 서버 progress_pct로 갱신. 생성 탭 결정형 진행바.</summary>
    public int GenProgress { get => _genProgress; set { _genProgress = value; NotifyOfPropertyChange(); } }

    private string _statusText = string.Empty;
    public string StatusText { get => _statusText; set { _statusText = value; NotifyOfPropertyChange(); } }

    /// <summary>생성 완료(generation id) — 콘솔이 구독(목록 새로고침 + 미리보기).</summary>
    public event Action<int>? Generated;
    #endregion

    #region - Attributes -
    private readonly IReportApiService _api;
    #endregion
}

/// <summary>기간 선택 옵션(표시명/값).</summary>
public sealed record PeriodOption(string Display, string Value);

/// <summary>템플릿 컴포넌트 선택 항목(템플릿 편집 다이얼로그에서 사용).</summary>
public sealed class ComponentPick : PropertyChangedBase
{
    public ComponentPick(string id, string display, string? category) { Id = id; Display = display; Category = category; }
    public string Id { get; }
    public string Display { get; }
    public string? Category { get; }
    private bool _enabled;
    public bool Enabled { get => _enabled; set { _enabled = value; NotifyOfPropertyChange(); } }
}
