using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Lists;
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
        // 심각도 필터(서버 닫힌 어휘 4종) — 전부 해제 = 전 심각도(파라미터 미전송).
        //   ⚠ 어휘는 ReportSeverity 하나에서만 만든다. 서버는 이 필터를 시스템 이벤트 계열의
        //     집계·그리드·CSV 까지 전파한다(탐지/장애/조치 이벤트는 대상 아님).
        Severities = new ObservableCollection<SeverityPick>(
            ReportSeverity.All.Select(s => new SeverityPick(s, SeverityDisplay(s))));
        foreach (var s in Severities) s.PropertyChanged += (_, __) => NotifyOfPropertyChange(nameof(SeveritySummary));
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
    /// <summary>
    /// 템플릿 드롭다운 로드(GET /templates) — 템플릿 기반 선택용.
    /// <para>탭 전환은 Caliburn 활성화를 일으키지 않는다(콘솔 호스트가 평범한 TabControl 이라
    /// 세 탭 VM 이 콘솔 열 때 한 번에 활성화된다). 따라서 템플릿을 추가·삭제한 뒤에는
    /// 콘솔이 이 메서드를 다시 불러줘야 콤보가 최신이 된다 — <see cref="ReportConsoleViewModel"/> 참조.</para>
    /// <para>선택은 <b>Id 로 복원</b>한다. <c>Templates.Clear()</c> 가 ComboBox 바인딩을 통해
    /// SelectedTemplate 을 null 로 되돌리고, 재적재된 항목은 <b>다른 인스턴스</b>라서
    /// 참조로 두면 콤보가 빈칸으로 보인다.</para>
    /// </summary>
    public async Task LoadTemplatesAsync()
    {
        try
        {
            var prevId = SelectedTemplate?.Id;          // Clear 이전에 확보(바인딩이 null 로 되돌린다)
            var res = await _api.GetTemplatesAsync(1, 100);
            Templates.Clear();
            TemplatesLoaded = res.Success && res.Data != null;
            if (res.Success && res.Data != null)
                foreach (var t in res.Data) Templates.Add(t);
            else _log?.Warning($"[ReportCreate] 템플릿 목록 조회 실패: {res.ErrorText()}");

            SelectedTemplate = (prevId.HasValue ? Templates.FirstOrDefault(t => t.Id == prevId.Value) : null)
                               ?? (Templates.Count > 0 ? Templates[0] : null);
            NotifyOfPropertyChange(nameof(HasTemplates));
        }
        catch (Exception ex) { TemplatesLoaded = false; _log?.Error($"[ReportCreate] LoadTemplates: {ex.Message}"); }
    }

    /// <summary>
    /// 템플릿 목록을 제대로 받았는가(마지막 시도 기준). 거짓이면 <see cref="Templates"/> 가 비어 있어도 "템플릿이 없다" 가
    /// 아니라 "모른다" 다 — 생성 이력의 템플릿 이름 표시가 "삭제된 템플릿" 이라고 단정하지 않게 콘솔이 본다.
    /// </summary>
    public bool TemplatesLoaded { get; private set; }

    /// <summary>보고서 생성 요청 → 폴링(COMPLETED/FAILED).</summary>
    public async Task Generate()
    {
        if (IsGenerating) return;
        var title = (Title ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(title)) { StatusText = "제목을 입력하세요."; return; }
        if (IsTemplateBased && SelectedTemplate is null) { StatusText = "템플릿을 선택하세요."; return; }
        if (IsCustomRange)
        {
            if (StartDate is null || EndDate is null) { StatusText = "시작일과 종료일을 지정하세요."; return; }
            if (EndDate < StartDate) { StatusText = "종료일이 시작일보다 빠릅니다. 날짜를 다시 고르세요."; return; }
        }

        try
        {
            IsGenerating = true;
            GenProgress = 0;
            StatusText = "보고서 생성을 요청하는 중…";
            var req = new ReportGenerateRequestDto
            {
                ReportType = IsTemplateBased ? "CUSTOM" : "STANDARD",
                Title = title,
                PeriodType = IsCustomRange ? "custom" : SelectedPeriod.Value,
                TemplateId = IsTemplateBased ? SelectedTemplate!.Id : null,
                // aware(+09:00) 자정 경계 — 서버가 end 자정을 23:59:59로 확장해 끝일 포함([start,end] 닫힌구간, 서버팀 확인 2026-07-31)
                StartDate = IsCustomRange ? KoreaTimeHelper.ToServerIso8601(StartDate?.Date) : null,
                EndDate = IsCustomRange ? KoreaTimeHelper.ToServerIso8601(EndDate?.Date) : null,
                // 어휘 밖 값·중복을 걸러 넣는다. 아무것도 안 고르면 null → 키 자체를 안 보낸다(= 전 심각도).
                //   운영 6.3.2 도 array[string] 로 받으므로 대문자 4종만 보내면 양쪽 안전하다(openapi 실측).
                SeverityFilter = ReportSeverity.Sanitize(Severities.Where(s => s.IsSelected).Select(s => s.Value)),
            };
            var genRes = await _api.GenerateAsync(req);
            // 사유는 ApiErrorTextHelper 로 — 배포본 400·404 봉투에는 top-level message 가 없고(error.message 에만 있다)
            // 빈 문자열은 ?? 를 통과해 "생성 요청 실패: " 로 끝나 버린다.
            if (!genRes.Success || genRes.Data is null)
            {
                // 서버 원문은 로그로만 — 화면에는 무엇이 안 됐고 무엇을 하면 되는지(완성도 패스 문구 규칙).
                _log?.Warning($"[ReportCreate] 생성 요청 거부: {genRes.ErrorText()}");
                StatusText = GenerateRejectedText;
                IsGenerating = false;
                return;
            }

            var id = genRes.Data.Id;
            StatusText = "생성 중…";
            var completed = await PollUntilDoneAsync(id);
            if (completed != null && completed.IsCompleted) { GenProgress = 100; StatusText = "보고서를 만들었습니다."; Generated?.Invoke(id); }
            else if (completed != null && completed.IsCancelled) StatusText = CancelledStatus(completed);
            else if (completed != null && completed.IsFailed) StatusText = $"생성에 실패했습니다: {new ReportGenerationRow(completed).FailureText}";
            else StatusText = PollTimeoutText;
        }
        catch (Exception ex)
        {
            // 화면엔 고정 문장만 — 예외 본문은 로그로.
            _log?.Error($"[ReportCreate] Generate: {ex}");
            StatusText = "생성 요청 중 오류가 발생했습니다. 잠시 후 다시 시도하세요.";
        }
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
                if (d.IsInProgress) { GenProgress = d.ProgressPct; StatusText = ProgressStatus(d); }
                if (d.IsCompleted || d.IsFailed || d.IsCancelled) return d;
            }
        }
        return null;
    }

    /// <summary>
    /// 취소로 끝났다 — 서버가 사유(예: "사용자 admin 가 취소했습니다")를 실었으면 함께 보인다.
    /// </summary>
    /// <remarks>
    /// 실패 · 취소 사유는 <see cref="ReportGenerationRow.FailureText"/> 한 곳에서 만든다(목록 상세 칸과 같은 글).
    /// 종전의 영문 원문 대조(<c>"stalled"</c> · <c>"server restarted"</c> · <c>"Cancelled"</c>)는 지웠다 —
    /// 8.0.2 서버는 원문을 내보내지 않고 한국어 한 줄로 정규화해 보낸다(routers/reports.py <c>_public_error_message</c>).
    /// </remarks>
    private static string CancelledStatus(ReportGenerationDto dto)
    {
        var reason = new ReportGenerationRow(dto).FailureText;
        return string.IsNullOrEmpty(reason) ? "취소됨." : $"취소됨: {reason}";
    }

    /// <summary>
    /// 폴링 중 진행 한 줄 — "생성 중… 30% · 자료 모으는 중". 단계 이름이 비면 꼬리 " · " 를 붙이지 않는다(R26).
    /// 단계 이름은 목록 · 상세와 같은 사전(<see cref="ReportGenerationRow.StageDisplay"/>)에서 온다.
    /// </summary>
    internal static string ProgressStatus(ReportGenerationDto d)
    {
        var stage = string.IsNullOrWhiteSpace(d.ProgressStage) ? string.Empty : ReportGenerationRow.StageDisplay(d.ProgressStage, d.ProgressStageLabel);
        return string.IsNullOrEmpty(stage) ? $"생성 중… {d.ProgressPct}%" : $"생성 중… {d.ProgressPct}% · {stage}";
    }

    /// <summary>R25 — 기다리기를 멈췄을 때. "폴링" 같은 구현어를 쓰지 않는다.</summary>
    public const string PollTimeoutText = "응답이 늦어 기다리기를 멈췄습니다. 왼쪽 목록에서 상태를 확인하세요.";

    /// <summary>서버가 생성 요청을 받지 않았을 때(원문은 로그).</summary>
    public const string GenerateRejectedText = "보고서 생성을 요청하지 못했습니다. 입력을 확인하고 잠시 후 다시 시도하세요.";

    /// <summary>
    /// [비우기] — 폼을 처음 상태로 돌린다(서버 미호출). 상세 칸 고정 막대의 되돌리기 자리다(WL L1499 T2).
    /// </summary>
    public void Reset()
    {
        Title = null;
        IsTemplateBased = false;
        IsCustomRange = false;
        SelectedPeriod = Periods[0];
        EndDate = DateTime.Today;
        StartDate = DateTime.Today.AddDays(-7);
        foreach (var s in Severities) s.IsSelected = false;
        StatusText = string.Empty;
        GenProgress = 0;
        NotifyOfPropertyChange(nameof(HasInput));
    }

    /// <summary>심각도 코드 → 화면 문구(서버로는 코드를 보낸다).</summary>
    private static string SeverityDisplay(string code) => code switch
    {
        ReportSeverity.Info => "정보",
        ReportSeverity.Warning => "경고",
        ReportSeverity.Error => "오류",
        ReportSeverity.Critical => "심각",
        _ => code
    };
    #endregion

    #region - Properties -
    public ObservableCollection<PeriodOption> Periods { get; }

    /// <summary>
    /// 심각도 필터 후보(닫힌 어휘 4종). <b>아무것도 선택하지 않으면 전 심각도</b>(서버에 키를 보내지 않는다).
    /// 시스템 이벤트 계열 집계·그리드·CSV 에만 적용된다.
    /// </summary>
    public ObservableCollection<SeverityPick> Severities { get; }

    /// <summary>선택 요약 — 화면 안내용.</summary>
    public string SeveritySummary
    {
        get
        {
            var picked = Severities.Where(s => s.IsSelected).Select(s => s.Display).ToList();
            return picked.Count == 0 ? "전 심각도" : string.Join(", ", picked);
        }
    }
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
            // 실제로 '다른 템플릿'으로 바뀔 때만 기간을 덮어쓴다.
            // 목록 새로고침은 같은 Id 를 재대입하므로, 이 검사가 없으면 사용자가 고른 기간이 매번 초기화된다.
            var changed = _selectedTemplate?.Id != value?.Id;
            _selectedTemplate = value;
            NotifyOfPropertyChange();
            // 템플릿의 기본기간을 기간에 반영(사용자가 다시 바꿀 수 있음)
            if (changed && value != null)
            {
                var p = Periods.FirstOrDefault(x => x.Value == value.DefaultPeriod);
                if (p != null) SelectedPeriod = p;
            }
        }
    }

    private string? _title;
    public string? Title { get => _title; set { _title = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(HasInput)); } }

    /// <summary>
    /// 무언가 채워졌는가 — 고정 막대의 [생성] · [비우기] 를 켤지 정한다.
    /// 제목이 비면 서버가 받지 않으므로 그것 하나로 판정한다(검증 문구는 <see cref="Generate"/> 가 낸다).
    /// </summary>
    public bool HasInput => !string.IsNullOrWhiteSpace(_title);

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

/// <summary>
/// 심각도 필터 체크 항목 — <see cref="Value"/> 는 <b>서버 어휘</b>(대문자), <see cref="Display"/> 는 화면 문구.
/// </summary>
public sealed class SeverityPick : PropertyChangedBase
{
    public SeverityPick(string value, string display) { Value = value; Display = display; }
    /// <summary>서버로 보내는 코드(INFO·WARNING·ERROR·CRITICAL).</summary>
    public string Value { get; }
    public string Display { get; }
    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set { _isSelected = value; NotifyOfPropertyChange(); } }
}
