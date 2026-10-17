using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Templates;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;

/// <summary>
/// 템플릿 편집 — 560 오버레이를 없애고 <b>템플릿 레일의 오른쪽 칸</b>에서 고친다
/// (설계 정본 window-layout-system-storyboard.html L1282: 이름 · 설명 · 기본기간 · 구성 체크리스트).
/// </summary>
/// <remarks>
/// <para><b>손댄 칸만 적용</b>한다 — 커널 <see cref="DirtyFieldTracker"/> 를 콘솔의 상세 발표자와 함께 쓴다.
/// 원래 값으로 되돌려 놓으면 손대지 않은 것으로 친다(커널 계약).</para>
/// <para><b>구성 순서</b>는 <see cref="Board"/> 가 쥔다. 끌어서 바꾸는 동안 <b>서버를 부르지 않고</b>
/// [적용] 한 번에 PATCH 1건으로 나간다(드래그 규칙: 행마다 순차 PATCH 금지).</para>
/// <para><c>is_public</c> 은 화면에 두지 않고 <b>전송도 생략</b>한다(<c>NullValueHandling.Ignore</c>).
/// 서버가 소유자 · 공개 스코핑을 집행하지 않고 <c>owner_id</c> 가 전량 NULL 이라, <c>false</c> 를 실어 보내면
/// 시드 템플릿의 <c>true</c> 를 저장할 때마다 덮어쓴다(docs/analyses/report-template-is-public-analysis.md).</para>
/// </remarks>
public class ReportTemplateEditViewModel : BasePanelViewModel
{
    public const string FieldName = "name";
    public const string FieldDescription = "description";
    public const string FieldPeriod = "default_period";
    public const string FieldComponents = "components";

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

        Board = new TemplateComponentBoard();
        Board.Changed += OnBoardChanged;
        Drop = new TemplateComponentDropHandler(Board, () => CanEdit);
    }
    #endregion

    #region - Wiring -
    /// <summary>콘솔이 자기 상세 발표자를 꽂아 준다 — 손댄 칸 · 여섯 상태 · 고정 막대가 한 장부를 본다.</summary>
    public void Attach(ConsoleDetailPresenter detail)
    {
        _detail = detail ?? throw new ArgumentNullException(nameof(detail));
        _tracker = detail.Tracker;
        _tracker.MarkIdentity(FieldName);   // 여럿을 골랐을 때 이름을 한꺼번에 덮어쓰지 않는다
    }
    #endregion

    #region - Processes -
    /// <summary>
    /// 고른 템플릿을 오른쪽 칸에 올린다. 목록 DTO 는 <c>components</c> 가 비어 있으므로
    /// <b>상세 조회</b>로 실제 구성을 받아 온다(목록의 <c>Components.Count == 0</c> 은 "안 실린 것"이다).
    /// </summary>
    public async Task LoadAsync(ReportTemplateDto? tpl)
    {
        if (tpl is null) { Clear(); return; }

        _isLoading = true;
        try
        {
            IsCreate = false;
            TemplateId = tpl.Id;
            StatusText = string.Empty;

            var src = tpl;
            try
            {
                var full = await _api.GetTemplateByIdAsync(tpl.Id);
                if (full.Success && full.Data != null) src = full.Data;
                else _log?.Warning($"[ReportTemplateEdit] 상세 조회 실패(id={tpl.Id}): {full.ErrorText()}");
            }
            catch (Exception ex) { _log?.Error($"[ReportTemplateEdit] 상세 조회: {ex.Message}"); }

            _originalName = src.Name ?? string.Empty;
            _originalDescription = src.Description ?? string.Empty;
            // ★ 서버가 null 을 주면 null 로 둔다 — "7d" 로 채우면 고르지 않은 기간을 고른 것처럼 보이고,
            //   기준값까지 "7d" 가 돼 사용자가 7일을 골라도 "손댄 것 없음" 으로 삼켜진다.
            _originalPeriod = src.DefaultPeriod;

            SetQuiet(ref _name, _originalName, nameof(Name));
            SetQuiet(ref _description, _originalDescription, nameof(Description));
            _selectedPeriod = Periods.FirstOrDefault(p => p.Value == _originalPeriod);   // 모르는 코드 · null → 선택 없음
            NotifyOfPropertyChange(nameof(SelectedPeriod));
            NotifyOfPropertyChange(nameof(IsPeriodUnset));

            await LoadCatalogAsync(src.Components);
        }
        finally { _isLoading = false; RaiseBoard(); }
    }

    /// <summary>새 템플릿 — 빈 폼 + 카탈로그 전부 미체크. 다이얼로그를 띄우지 않고 같은 자리에서 등록한다.</summary>
    public async Task LoadNewAsync()
    {
        _isLoading = true;
        try
        {
            IsCreate = true;
            TemplateId = 0;
            StatusText = string.Empty;
            _originalName = string.Empty;
            _originalDescription = string.Empty;
            _originalPeriod = "7d";

            SetQuiet(ref _name, string.Empty, nameof(Name));
            SetQuiet(ref _description, string.Empty, nameof(Description));
            _selectedPeriod = Periods[0];
            NotifyOfPropertyChange(nameof(SelectedPeriod));
            NotifyOfPropertyChange(nameof(IsPeriodUnset));

            await LoadCatalogAsync(null);
        }
        finally { _isLoading = false; RaiseBoard(); }
    }

    /// <summary>상세 칸을 비운다.</summary>
    public void Clear()
    {
        _isLoading = true;
        try
        {
            IsCreate = false;
            TemplateId = 0;
            StatusText = string.Empty;
            _originalName = _originalDescription = string.Empty;
            _originalPeriod = "7d";
            SetQuiet(ref _name, string.Empty, nameof(Name));
            SetQuiet(ref _description, string.Empty, nameof(Description));
            Board.Load(_catalog, null);
        }
        finally { _isLoading = false; RaiseBoard(); }
    }

    private async Task LoadCatalogAsync(IEnumerable<ReportComponentConfigDto>? saved)
    {
        if (_catalog is null)
        {
            try
            {
                var res = await _api.GetComponentsAsync();
                if (res.Success && res.Data != null) _catalog = res.Data;
                else _log?.Warning($"[ReportTemplateEdit] 구성 카탈로그 조회 실패: {res.ErrorText()}");
            }
            catch (Exception ex) { _log?.Error($"[ReportTemplateEdit] LoadComponents: {ex.Message}"); }
        }
        // R28 — 카탈로그를 못 받았으면 목록이 말없이 비지 않게 표시한다(다음 적재 때 다시 시도한다).
        CatalogLoadFailed = _catalog is null;
        Board.Load(_catalog, saved);
    }

    /// <summary>
    /// [적용] · [등록] — 신규는 POST, 수정은 <b>손댄 칸만</b> PATCH. 성공하면 기준을 새로 굳힌다.
    /// </summary>
    /// <returns>성공한 템플릿 id, 실패면 null.</returns>
    public async Task<int?> ApplyAsync()
    {
        if (IsBusy) return null;

        var name = (Name ?? string.Empty).Trim();
        if (IsCreate || IsTouched(FieldName))
        {
            if (string.IsNullOrEmpty(name)) { StatusText = "이름을 입력하세요."; return null; }
        }

        try
        {
            IsBusy = true;
            var comps = Board.ToConfig();

            if (IsCreate)
            {
                if (comps.Count == 0) { StatusText = "구성 요소를 1개 이상 고르세요."; return null; }
                var dto = new ReportTemplateCreateDto
                {
                    Name = name,
                    Description = string.IsNullOrWhiteSpace(Description) ? null : Description,
                    ReportType = "CUSTOM",
                    // IsPublic 미지정 → 서버 기본값(false). UI 를 두지 않는 사유는 클래스 주석 참조.
                    // 등록 폼은 늘 한 기간을 고른 채 시작한다(LoadNewAsync) — 서버 생성 스키마가 null 을 받지 않는다.
                    DefaultPeriod = (SelectedPeriod ?? Periods[0]).Value,
                    Components = comps,
                };
                var res = await _api.CreateTemplateAsync(dto);
                if (res.Success && res.Data != null)
                {
                    StatusText = "등록했습니다.";
                    return res.Data.Id;
                }
                // 서버 원문은 로그로만 — 화면엔 무엇이 안 됐고 무엇을 하면 되는지.
                _log?.Warning($"[ReportTemplateEdit] 등록 실패: {res.ErrorText()}");
                StatusText = "템플릿을 등록하지 못했습니다. 입력을 확인하고 잠시 후 다시 시도하세요.";
                return null;
            }
            else
            {
                // ★ 손댄 칸만 싣는다 — 안 건드린 칸을 함께 보내면 다른 세션의 변경을 덮어쓴다(lost update).
                var dto = new ReportTemplateUpdateDto
                {
                    Name = IsTouched(FieldName) ? name : null,
                    Description = IsTouched(FieldDescription) ? (Description ?? string.Empty) : null,
                    // IsPublic 은 의도적으로 null — 전송 자체를 생략한다(클래스 주석).
                    // 손대지 않은 기간은 싣지 않는다 — 서버의 null 을 그대로 둔다(NullValueHandling.Ignore 로 키째 빠진다).
                    DefaultPeriod = IsTouched(FieldPeriod) ? SelectedPeriod?.Value : null,
                    Components = IsTouched(FieldComponents) ? comps : null,
                };
                if (dto.Name is null && dto.Description is null && dto.DefaultPeriod is null && dto.Components is null)
                {
                    StatusText = "바뀐 것이 없습니다.";
                    return TemplateId;
                }
                var res = await _api.UpdateTemplateAsync(TemplateId, dto);
                if (res.Success)
                {
                    StatusText = "저장했습니다.";
                    return TemplateId;
                }
                _log?.Warning($"[ReportTemplateEdit] 저장 실패: {res.ErrorText()}");
                StatusText = "템플릿을 저장하지 못했습니다. 잠시 후 다시 시도하세요.";
                return null;
            }
        }
        catch (Exception ex)
        {
            // 날 예외 문구에는 호스트 · URL · TLS 사정이 묻어 나온다 — 로그로만 남기고 화면엔 고정 문장.
            _log?.Error($"[ReportTemplateEdit] Apply: {ex}");
            StatusText = "저장 중 오류가 발생했습니다. 잠시 후 다시 시도하세요.";
            return null;
        }
        finally { IsBusy = false; }
    }

    /// <summary>저장에 성공했다 — 지금 값을 "저장된 값"으로 굳힌다.</summary>
    public void Settle()
    {
        _originalName = (Name ?? string.Empty).Trim();
        _originalDescription = Description ?? string.Empty;
        _originalPeriod = SelectedPeriod?.Value;
        SetQuiet(ref _name, _originalName, nameof(Name));
        Board.MarkBaseline();
        RaiseBoard();
    }

    /// <summary>[되돌리기] — 칸과 구성 순서를 마지막 기준으로 돌려놓는다(서버 미호출).</summary>
    public void Revert()
    {
        _isLoading = true;
        try
        {
            SetQuiet(ref _name, _originalName, nameof(Name));
            SetQuiet(ref _description, _originalDescription, nameof(Description));
            _selectedPeriod = Periods.FirstOrDefault(p => p.Value == _originalPeriod);
            NotifyOfPropertyChange(nameof(SelectedPeriod));
            NotifyOfPropertyChange(nameof(IsPeriodUnset));
            Board.Revert();
            StatusText = string.Empty;
        }
        finally { _isLoading = false; RaiseBoard(); }
    }

    /// <summary>키보드 폴백(Alt+위/아래)의 테스트 · 단추용 진입점 — 드래그와 <b>같은 담당</b>을 부른다.</summary>
    public bool MoveComponent(TemplateComponentItem item, int direction)
    {
        if (item is null || !CanEdit) return false;
        var from = Board.Items.IndexOf(item);
        if (from < 0) return false;
        var insertion = direction < 0 ? from - 1 : from + 2;
        if (insertion < 0 || insertion > Board.Items.Count) return false;
        return Board.Move(new[] { from }, insertion);
    }
    #endregion

    #region - Properties -
    public ObservableCollection<PeriodOption> Periods { get; }

    /// <summary>구성 체크리스트 + 순서(WL L1282).</summary>
    public TemplateComponentBoard Board { get; }

    /// <summary>끌어 놓기 담당 — 뷰가 커널 동작에 물린다.</summary>
    public TemplateComponentDropHandler Drop { get; }

    public int TemplateId { get; private set; }

    private bool _isCreate;
    public bool IsCreate { get => _isCreate; private set { _isCreate = value; NotifyOfPropertyChange(); } }

    /// <summary>편집 권한이 있고 적재 중이 아닌가 — 끌기 · 체크를 켤지 정한다.</summary>
    public bool CanEdit
    {
        get => _canEdit && !_isLoading;
        set { _canEdit = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CanEdit)); NotifyOfPropertyChange(nameof(CanMoveComponent)); NotifyOfPropertyChange(nameof(MoveComponentToolTip)); }
    }

    private string? _name;
    public string? Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            NotifyOfPropertyChange();
            Touch(FieldName, _originalName, (_name ?? string.Empty).Trim());
        }
    }

    private string? _description;
    public string? Description
    {
        get => _description;
        set
        {
            if (_description == value) return;
            _description = value;
            NotifyOfPropertyChange();
            Touch(FieldDescription, _originalDescription, _description ?? string.Empty);
        }
    }

    private PeriodOption? _selectedPeriod;
    /// <summary>
    /// 기본 기간. 서버 템플릿에 기간이 없으면(<c>default_period: null</c>) <c>null</c> — 콤보가 아무것도 고르지 않고
    /// "지정 안 함" 을 보인다. 사용자가 고르면 그 값이 손댄 칸이 된다. 화면에서 null 로 되돌리는 길은 없다
    /// (PATCH 가 null 을 싣지 않는다 — 키째 빠진다).
    /// </summary>
    public PeriodOption? SelectedPeriod
    {
        get => _selectedPeriod;
        set
        {
            if (Equals(_selectedPeriod, value) || value is null) return;
            _selectedPeriod = value;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(IsPeriodUnset));
            Touch(FieldPeriod, _originalPeriod, value.Value);
        }
    }

    /// <summary>서버 템플릿에 기본 기간이 없다 — 콤보 자리표시자 "지정 안 함" 을 켠다.</summary>
    public bool IsPeriodUnset => _selectedPeriod is null;

    /// <summary>기본 기간이 없을 때 콤보에 보일 글(<see cref="Consoles.Lists.ReportGenerationRow.UnsetPeriodText"/> 와 같다).</summary>
    public string UnsetPeriodText => Consoles.Lists.ReportGenerationRow.UnsetPeriodText;

    private string _statusText = string.Empty;
    /// <summary>저장 결과 한 줄 — 팝업이 아니다(상세 칸 안에 인라인).</summary>
    public string StatusText { get => _statusText; set { _statusText = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(HasStatus)); } }
    public bool HasStatus => !string.IsNullOrEmpty(_statusText);

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set { _isBusy = value; NotifyOfPropertyChange(); } }

    public string ComponentSummary => $"구성 {Board.EnabledCount}개 / 전체 {Board.Count}개";

    // 구성 순서를 끌어 바꿀 수 있다는 안내는 '구성 요소' 절 "?"(Reports.Template.Components)로 옮겼다(help-callout H-3).

    private bool _catalogLoadFailed;
    /// <summary>구성 요소 목록(서버 카탈로그)을 받지 못했다 — 상세 칸이 까닭을 말한다(R28).</summary>
    public bool CatalogLoadFailed
    {
        get => _catalogLoadFailed;
        private set { _catalogLoadFailed = value; NotifyOfPropertyChange(); }
    }

    public const string CatalogLoadFailedText = "구성 요소 목록을 불러오지 못했습니다. 잠시 후 템플릿을 다시 골라 보세요.";

    private bool _hasSelectedComponent;
    /// <summary>구성 목록에서 고른 줄이 있는가 — 뷰가 목록 선택이 바뀔 때 알려 준다.</summary>
    public bool HasSelectedComponent
    {
        get => _hasSelectedComponent;
        set { _hasSelectedComponent = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CanMoveComponent)); NotifyOfPropertyChange(nameof(MoveComponentToolTip)); }
    }

    /// <summary>R29 — ▲▼ 는 고른 줄이 있을 때만 켠다(예전엔 늘 켜져 눌러도 아무 일이 없었다).</summary>
    public bool CanMoveComponent => CanEdit && HasSelectedComponent;

    public string MoveComponentToolTip => !CanEdit ? "편집 권한이 없습니다."
        : HasSelectedComponent ? "고른 줄을 옮깁니다 (Alt+↑ / Alt+↓)" : "구성 요소를 먼저 고르세요.";
    #endregion

    #region - Attributes -
    private readonly IReportApiService _api;
    private ConsoleDetailPresenter? _detail;
    private DirtyFieldTracker? _tracker;
    private List<ReportComponentCategoryDto>? _catalog;

    private string _originalName = string.Empty;
    private string _originalDescription = string.Empty;
    private string? _originalPeriod = "7d";
    private bool _isLoading;
    private bool _canEdit = true;

    private bool IsTouched(string key) => _tracker?.IsTouched(key) ?? false;

    private void Touch(string key, object? original, object? current)
    {
        if (_isLoading) return;
        _tracker?.Touch(key, original, current);
    }

    private void OnBoardChanged(object? sender, EventArgs e)
    {
        // 끈 줄을 옮기면 보낼 것이 달라지지 않는다 → 미적용으로 세지 않는다(보드의 서명이 그것을 안다).
        Touch(FieldComponents, Board.BaselineSignature, Board.Signature);
        RaiseBoard();
    }

    private void RaiseBoard()
    {
        NotifyOfPropertyChange(nameof(ComponentSummary));
        NotifyOfPropertyChange(nameof(CanEdit));
        NotifyOfPropertyChange(nameof(CanMoveComponent));
        NotifyOfPropertyChange(nameof(MoveComponentToolTip));
    }

    private void SetQuiet(ref string? field, string value, string propertyName)
    {
        field = value;
        NotifyOfPropertyChange(propertyName);
    }
    #endregion
}
