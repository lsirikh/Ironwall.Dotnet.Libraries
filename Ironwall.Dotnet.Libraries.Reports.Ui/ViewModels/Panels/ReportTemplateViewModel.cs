using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Lists;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Preview;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;

/// <summary>
/// 템플릿 레일의 <b>목록 칸</b> — 조회 · 검색 · 삭제. 편집은 오른쪽 칸이 한다(WL L1282).
/// </summary>
/// <remarks>
/// 삭제 확인 · 결과는 앱 표준 EventAggregator 팝업이고, 띄우기 전에 <b>공역 게이트</b>로 미리보기를 내린다.
/// 삭제가 끝나면 <see cref="TemplatesChanged"/> 로 알려 <b>생성 화면의 템플릿 목록</b>도 그 자리에서 갱신한다
/// — 안 하면 지운 템플릿으로 생성이 가능하다(실제로 있었던 결함).
/// </remarks>
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
        if (!CanView)
        {
            Items.Clear();
            Rows.Clear();
            SelectedItem = null;
            LoadError = NoViewPermissionText;
            NotifyOfPropertyChange(nameof(IsEmpty));
            NotifyOfPropertyChange(nameof(CountText));
            return;
        }
        try
        {
            IsBusy = true;
            var keepId = SelectedItem?.Id;
            var res = await _api.GetTemplatesAsync(1, 100);
            Items.Clear();
            if (res.Success && res.Data != null)
            {
                foreach (var t in res.Data) Items.Add(t);
                LoadError = null;
            }
            else
            {
                // 사유는 ApiErrorTextHelper 로 — 배포본 400·404 봉투에는 top-level message 가 없다.
                _log?.Warning($"[ReportTemplate] 조회 실패: {res.ErrorText()}");
                LoadError = "서버에 연결하지 못했습니다. 잠시 후 툴바의 새로 고침(⟳)을 눌러 다시 시도하세요.";
            }
            ApplyFilter();
            // 선택은 Id 로 되살린다 — 재적재한 항목은 다른 인스턴스라 참조로 두면 선택이 풀린다.
            if (keepId.HasValue) SelectById(keepId.Value);
        }
        catch (Exception ex)
        {
            _log?.Error($"[ReportTemplate] Load: {ex.Message}");
            LoadError = "템플릿 목록을 불러오지 못했습니다. 잠시 후 툴바의 새로 고침(⟳)을 눌러 다시 시도하세요.";
            ApplyFilter();
        }
        finally { IsBusy = false; }
    }

    public Task Reload() => LoadAsync();

    /// <summary>
    /// 검색어로 보이는 줄을 다시 만든다(서버 왕복 없음).
    /// </summary>
    /// <remarks>
    /// ★ <b>검색이 미적용 변경을 삼키지 않는다.</b> 고치던 템플릿이 검색 밖으로 나가면, 선택을 놓는 순간
    /// 오른쪽 칸이 비면서 손댄 칸이 말없이 사라진다. 커널 계약이 "검색은 이동이 아니다"라고 한 것도
    /// <b>선택을 바꾸지 않는다</b>는 전제 위에 있다. 그래서 고치던 줄은 검색과 맞지 않아도 <b>남겨 둔다</b>
    /// — 보이지 않는 줄이 선택돼 있는 상태(삭제 오폭의 원인)도 함께 피한다.
    /// </remarks>
    public void ApplyFilter()
    {
        var keep = SelectedItem;
        // 고정은 Id 로 본다 - 재조회 뒤의 줄은 다른 인스턴스라 참조로 비교하면 고정이 헛돈다.
        var pinId = keep != null && (HasUnappliedChanges?.Invoke() ?? false) ? keep.Id : (int?)null;

        // 별표: Clear() 하지 않는다 - 묶인 DataGrid 의 선택이 그 자리에서 풀린다.
        ObservableReconcile.Apply(Rows, Items.Where(t => Matches(t) || (pinId.HasValue && t.Id == pinId.Value)).ToList());

        if (keep != null && !Rows.Contains(keep)) SelectedItem = null;
        NotifyOfPropertyChange(nameof(IsEmpty));
        NotifyOfPropertyChange(nameof(CountText));
        NotifyOfPropertyChange(nameof(EmptyStateText));
        NotifyOfPropertyChange(nameof(EmptyStateHint));
    }

    private bool Matches(ReportTemplateDto t)
    {
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        var needle = SearchText!.Trim();
        return (t.Name?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false)
               || (t.Description?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false)
               || t.Id.ToString().Contains(needle, StringComparison.Ordinal);
    }

    /// <summary>선택 템플릿 삭제 — 확인 팝업(확인 시 CallDelete… 발행 → IHandle 에서 수행).</summary>
    public async Task Delete()
    {
        var item = SelectedItem;
        if (item is null) return;
        // 확인 왕복 동안 대상을 고정한다 - 그 사이 선택이 바뀌어도 엉뚱한 템플릿을 지우지 않는다.
        _pendingDeleteItem = item;
        // ★ 팝업 전에 미리보기를 내린다(WebView2 가 확인 창을 가린다).
        HoldAirspace();
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Explain = $"'{item.Name}' 템플릿을 삭제하시겠습니까?",
            MessageModel = new CallDeleteReportTemplateProcessMessageModel()
        });
    }

    /// <summary>삭제 확인됨 → DELETE /templates/{id} → 결과 안내.</summary>
    public async Task HandleAsync(CallDeleteReportTemplateProcessMessageModel message, CancellationToken cancellationToken)
    {
        var item = _pendingDeleteItem ?? SelectedItem;
        _pendingDeleteItem = null;
        if (item is null) { ReleaseAirspace(); return; }

        HoldAirspace();
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel(), cancellationToken);
        OpenInfoPopupMessageModel result;
        try
        {
            var res = await _api.DeleteTemplateAsync(item.Id, cancellationToken);
            if (res.Success)
            {
                Items.Remove(item);
                if (ReferenceEquals(SelectedItem, item)) SelectedItem = null;
                ApplyFilter();
                TemplatesChanged?.Invoke();   // 생성 화면 목록에서도 빠지도록 — 안 하면 지운 템플릿으로 생성 가능
            }
            result = res.Success
                ? new OpenInfoPopupMessageModel { Title = "삭제 완료", Explain = "템플릿을 삭제했습니다." }
                : new OpenInfoPopupMessageModel { Title = "삭제 실패", Explain = "템플릿을 삭제하지 못했습니다. 잠시 후 다시 시도하세요." };
            if (!res.Success) _log?.Warning($"[ReportTemplate] 삭제 실패(id={item.Id}): {res.ErrorText()}");
        }
        catch (Exception ex)
        {
            _log?.Error($"[ReportTemplate] Delete: {ex.Message}");
            result = new OpenInfoPopupMessageModel { Title = "삭제 실패", Explain = "삭제 중 오류가 발생했습니다." };
        }
        await _eventAggregator.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
        HoldAirspace();     // 곧바로 안내 팝업을 연다
        await _eventAggregator.PublishOnCurrentThreadAsync(result, cancellationToken);
    }

    /// <summary>id 로 항목 재선택 — 저장 후 목록을 새로 받아도 선택을 지킨다.</summary>
    /// <remarks>
    /// <b>보이는 줄 안에서만</b> 고른다. 목록에 없는 줄을 고르면 툴바 [삭제] 가 화면에 없는 템플릿을 겨눈다.
    /// </remarks>
    public void SelectById(int id) => SelectedItem = Rows.FirstOrDefault(t => t.Id == id);

    private void HoldAirspace() => _airspaceHold ??= Airspace.Block();

    /// <summary>
    /// 미리보기를 되돌린다. 콘솔이 팝업 닫힘에서 부른다 - 확인 창의 [취소] 는 우리 HandleAsync 를 부르지 않는다.
    /// </summary>
    public void ReleaseAirspace()
    {
        var hold = _airspaceHold;
        _airspaceHold = null;
        hold?.Dispose();
        _pendingDeleteItem = null;
    }
    #endregion

    #region - Properties -
    public ObservableCollection<ReportTemplateDto> Items { get; } = new();
    public ObservableCollection<ReportTemplateDto> Rows { get; } = new();

    public bool IsEmpty => Rows.Count == 0 && !IsBusy;

    public string CountText => Rows.Count == Items.Count ? $"{Items.Count}건" : $"{Items.Count}건 중 {Rows.Count}건 검색";

    private string? _searchText;
    public string? SearchText
    {
        get => _searchText;
        set { _searchText = value; NotifyOfPropertyChange(); ApplyFilter(); }
    }

    private string? _loadError;
    /// <summary>조회 실패 사유 — 빈 목록을 "템플릿 없음"과 구분해 안내.</summary>
    public string? LoadError { get => _loadError; set { _loadError = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(EmptyStateText)); NotifyOfPropertyChange(nameof(EmptyStateHint)); } }
    public string EmptyStateText => LoadError ?? (string.IsNullOrWhiteSpace(SearchText) ? "저장된 템플릿이 없습니다." : "검색과 맞는 템플릿이 없습니다.");

    /// <summary>빈 상태의 둘째 줄 — 다음에 무엇을 하면 되는지.</summary>
    public string EmptyStateHint => !CanView ? "관리자에게 보고서 조회 권한을 요청하세요."
        : LoadError != null ? string.Empty
        : !string.IsNullOrWhiteSpace(SearchText) ? "검색어를 바꾸거나 지워 보세요."
        : "[새 템플릿]으로 자주 쓰는 구성을 저장할 수 있습니다.";

    private ReportTemplateDto? _selectedItem;
    public ReportTemplateDto? SelectedItem
    {
        get => _selectedItem;
        set
        {
            _selectedItem = value;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(CanDelete));
            SelectionChanged?.Invoke(value);
        }
    }

    public bool CanDelete => SelectedItem != null;

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set { _isBusy = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(IsEmpty)); } }

    /// <summary>선택이 바뀌었다 — 콘솔이 구독해 오른쪽 칸에 편집 폼을 올린다.</summary>
    public event Action<ReportTemplateDto?>? SelectionChanged;

    /// <summary>템플릿 집합 변경(삭제 성공) — 콘솔이 구독해 생성 화면 목록을 재적재한다.</summary>
    public event System.Action? TemplatesChanged;

    /// <summary>공역 게이트 — 콘솔이 꽂아 준다.</summary>
    public IPreviewAirspaceGate Airspace { get; set; } = NullPreviewAirspaceGate.Instance;

    /// <summary>
    /// 오른쪽 칸에 미적용 변경이 있는가 — 콘솔이 꽂아 준다. 참이면 고치던 줄을 검색 결과에 붙잡아 둔다.
    /// </summary>
    public Func<bool>? HasUnappliedChanges { get; set; }

    /// <summary>조회 권한 - 콘솔이 꽂아 준다.</summary>
    public bool CanView { get; set; } = true;

    internal const string NoViewPermissionText = "템플릿을 볼 권한이 없습니다.";
    #endregion

    #region - Attributes -
    private readonly IReportApiService _api;
    private IDisposable? _airspaceHold;
    /// <summary>삭제 대상 - 확인 왕복 동안 고정.</summary>
    private ReportTemplateDto? _pendingDeleteItem;
    #endregion
}
