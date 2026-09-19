using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Lists;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Preview;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;

/// <summary>
/// 보고서 콘솔 — <b>레일 · 목록 · 상세</b> 3단(설계 정본 window-layout-system-storyboard.html L1250-1295).
/// </summary>
/// <remarks>
/// <para>탭 3 이 레일 3 이 되고(L1260-1262), 미리보기는 본문을 덮던 오버레이에서 <b>오른쪽 칸</b>으로 옮겨
/// [크게 보기]만 별도 창으로 뜬다(L1278 · 결정 L-D8 "채택"). 템플릿 수정 560 오버레이는 사라지고
/// <b>오른쪽 칸 편집</b>이 된다(L1282).</para>
/// <para><b>이 뷰모델은 전송 경로를 갖지 않는다</b> — 네 화면의 기존 API 호출을 그대로 부른다.
/// 치수 · 폭별 도킹/서랍/접힘 · 여섯 상태 · 고정 막대는 커널(<c>ConsoleShell</c> · <c>ConsoleDetailHost</c>)이 맡는다.</para>
/// <para>★ <b>레일 전환이 곧 활성화다.</b> 평범한 <c>TabControl</c> 은 탭을 바꿔도 Caliburn 활성화를 일으키지
/// 않아 생성 화면의 템플릿 목록이 "콘솔을 연 순간의 스냅샷"으로 굳었고, 그 탓에 <b>지운 템플릿으로 생성</b>이
/// 가능했다. 레일로 바꾸면서 전환마다 그 화면을 실제로 재적재해 그 결함이 구조적으로 재발할 수 없게 한다.</para>
/// <para>★ <b>공역</b>: WebView2 는 네이티브 창이라 같은 창의 WPF 팝업 위에 그려진다. 이 뷰모델이
/// <see cref="IPreviewAirspaceGate"/> 를 구현해 팝업 · 크게 보기 창 · 좁은 폭에서 미리보기를 내린다.</para>
/// <para>싱글턴이다 — 닫을 때 선택 · 미적용 변경 · 미리보기를 전부 내려놓는다.</para>
/// </remarks>
public class ReportConsoleViewModel : BasePanelViewModel, IPreviewAirspaceGate
{
    public const string ConsoleKey = "Reports";

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

        Detail = new ConsoleDetailPresenter { TypeName = ReportConsoleRails.TypeNameOf(ReportConsoleRails.List) };
        EditViewModel.Attach(Detail);

        // 공역 게이트를 꽂는다 — 팝업을 띄우는 두 화면이 이것을 통해 미리보기를 내린다.
        ListViewModel.Airspace = this;
        TemplateViewModel.Airspace = this;
        // 검색이 고치던 템플릿을 목록 밖으로 밀어내 미적용 변경을 삼키지 않게 한다.
        TemplateViewModel.HasUnappliedChanges = () => Detail.Tracker.IsDirty;

        RailEntries = new ObservableCollection<ConsoleRailEntry>(
            ReportConsoleRails.Order.Select(key => new ConsoleRailEntry(key, ReportConsoleRails.LabelOf(key), new ReportRailIcon(ReportConsoleRails.IconOf(key)))
            {
                Tag = key,
                ShowCount = key == ReportConsoleRails.List,   // 진행 중 건수 배지(WL L1262)
            }));
        _selectedRail = RailEntries[0];

        // ⚠ 이벤트 구독은 생성자가 아니라 OnActivateAsync 에서 한다.
        //    이 VM 들은 전부 SingleInstance(ReportUiModule) → 생성자는 앱 수명당 1회만 실행된다.
        //    "생성자 구독 + OnDeactivate 해제" 조합이면 두 번째 열 때 재구독이 안 돼 화면이 전부 죽는다.
    }
    #endregion

    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        Subscribe();
        RefreshPermissions();
        await SelectRailAsync(ReportConsoleRails.List, force: true);
    }

    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        await ScreenExtensions.TryDeactivateAsync(ListViewModel, close, cancellationToken);
        await ScreenExtensions.TryDeactivateAsync(CreateViewModel, close, cancellationToken);
        await ScreenExtensions.TryDeactivateAsync(TemplateViewModel, close, cancellationToken);
        Unsubscribe();

        // 싱글턴이라 다음에 열 때 옛 선택 · 미적용 변경 · 미리보기가 남아 있으면 안 된다.
        Detail.Reset();
        PreviewViewModel.Clear();
        EditViewModel.Clear();
        _overlayDepth = 0;
        PreviewViewModel.IsOverlayOpen = false;

        await base.OnDeactivateAsync(close, cancellationToken);
    }
    #endregion

    #region - Wiring -
    private void Subscribe()
    {
        Unsubscribe();
        ListViewModel.SelectionChanged += OnGenerationSelected;
        ListViewModel.Deleted += OnGenerationDeleted;
        ListViewModel.PropertyChanged += OnChildPropertyChanged;
        TemplateViewModel.SelectionChanged += OnTemplateSelected;
        TemplateViewModel.TemplatesChanged += OnTemplatesChanged;
        TemplateViewModel.PropertyChanged += OnChildPropertyChanged;
        CreateViewModel.Generated += OnReportGenerated;
        CreateViewModel.PropertyChanged += OnChildPropertyChanged;
        EditViewModel.Drop.Reordered += OnComponentsReordered;
        Detail.PropertyChanged += OnDetailChanged;
        _permission.PermissionsChanged += OnPermissionsChanged;
    }

    private void Unsubscribe()
    {
        ListViewModel.SelectionChanged -= OnGenerationSelected;
        ListViewModel.Deleted -= OnGenerationDeleted;
        ListViewModel.PropertyChanged -= OnChildPropertyChanged;
        TemplateViewModel.SelectionChanged -= OnTemplateSelected;
        TemplateViewModel.TemplatesChanged -= OnTemplatesChanged;
        TemplateViewModel.PropertyChanged -= OnChildPropertyChanged;
        CreateViewModel.Generated -= OnReportGenerated;
        CreateViewModel.PropertyChanged -= OnChildPropertyChanged;
        EditViewModel.Drop.Reordered -= OnComponentsReordered;
        Detail.PropertyChanged -= OnDetailChanged;
        _permission.PermissionsChanged -= OnPermissionsChanged;
    }
    #endregion

    #region - Rail -
    public ObservableCollection<ConsoleRailEntry> RailEntries { get; }

    private ConsoleRailEntry _selectedRail;
    /// <summary>
    /// 지금 레일. 미적용 변경이 있으면 <b>바꾸지 않고</b> 바닥 막대가 까닭을 말한다(WL L928).
    /// 뷰의 <c>ConsoleRail</c> 이 TwoWay 로 묶으므로, 막았을 때 알림을 다시 울려 목록 선택을 되돌린다.
    /// </summary>
    public ConsoleRailEntry SelectedRail
    {
        get => _selectedRail;
        set
        {
            if (value is null || ReferenceEquals(value, _selectedRail)) return;
            if (!Detail.Guard.TryNavigate(ConsoleNavigation.SwitchRail))
            {
                NotifyOfPropertyChange();   // 막았다 — 목록 선택을 지금 레일로 되돌린다
                return;
            }
            _ = SelectRailAsync(value.Key);
        }
    }

    public string SelectedRailKey => _selectedRail.Key;
    public bool IsListRail => SelectedRailKey == ReportConsoleRails.List;
    public bool IsCreateRail => SelectedRailKey == ReportConsoleRails.Create;
    public bool IsTemplateRail => SelectedRailKey == ReportConsoleRails.Template;

    /// <summary>
    /// 레일을 바꾼다 — 그 화면을 <b>실제로 재적재</b>한다(탭 호스트의 "연 순간 스냅샷" 결함 재발 방지).
    /// </summary>
    public async Task SelectRailAsync(string key, bool force = false)
    {
        if (!force && string.Equals(_selectedRail.Key, key, StringComparison.Ordinal)) return;

        var entry = RailEntries.FirstOrDefault(r => string.Equals(r.Key, key, StringComparison.Ordinal));
        if (entry is null) return;

        _selectedRail = entry;
        Detail.Reset();
        Detail.TypeName = ReportConsoleRails.TypeNameOf(key);
        Detail.IsCreating = key == ReportConsoleRails.Create;
        Detail.CreateBanner = key == ReportConsoleRails.Create ? CreateFormBanner : TemplateCreateBanner;
        PreviewViewModel.Clear();
        EditViewModel.Clear();
        StatusText = string.Empty;

        RaiseRail();

        try
        {
            switch (key)
            {
                case ReportConsoleRails.List:
                    await ScreenExtensions.TryActivateAsync(ListViewModel);
                    await ListViewModel.LoadAsync();
                    break;

                case ReportConsoleRails.Create:
                    // 템플릿 목록을 여기서 다시 받는다 — 레일 전환이 곧 활성화다.
                    await ScreenExtensions.TryActivateAsync(CreateViewModel);
                    await CreateViewModel.LoadTemplatesAsync();
                    await ListViewModel.LoadAsync();    // 왼쪽 칸에 최근 생성 이력을 보인다
                    break;

                case ReportConsoleRails.Template:
                    await ScreenExtensions.TryActivateAsync(TemplateViewModel);
                    await TemplateViewModel.LoadAsync();
                    break;
            }
        }
        catch (Exception ex) { _log?.Error($"[ReportConsole] 레일 전환({key}): {ex.Message}"); }

        RefreshRailCounts();
        RaiseAll();
    }

    /// <summary>진행 중 건수 배지(WL L1262).</summary>
    public void RefreshRailCounts()
    {
        var listEntry = RailEntries.First(r => r.Key == ReportConsoleRails.List);
        listEntry.Count = ListViewModel.InProgressCount;
    }

    public string RailFooterText => IsTemplateRail
        ? "템플릿을 고르면 오른쪽 칸에서 바로 고칩니다"
        : IsCreateRail ? "제목과 기간을 정하면 오른쪽 아래에서 생성합니다"
        : "보고서를 고르면 오른쪽 칸에 미리보기가 나옵니다";
    #endregion

    #region - List slot -
    /// <summary>목록 칸이 보여 줄 줄들. 생성 이력이거나 템플릿이다.</summary>
    public IEnumerable Rows => IsTemplateRail ? TemplateViewModel.Rows : ListViewModel.Rows;

    /// <summary>지금 화면의 열 명세 — 뷰가 이것으로 DataGrid 열을 만든다.</summary>
    public IReadOnlyList<ReportColumnSpec> Columns => ReportColumnCatalog.For(SelectedRailKey);

    /// <summary>목록 칸 위에 붙는 설명(생성 화면의 왼쪽 칸이 무엇인지 알린다).</summary>
    public string ListCaption => IsCreateRail ? "최근 생성 이력 — 요청한 보고서가 여기서 진행됩니다" : string.Empty;
    public bool HasListCaption => !string.IsNullOrEmpty(ListCaption);

    /// <summary>줄을 골랐다. 막혔으면 거짓 — 뷰가 선택을 되돌린다.</summary>
    public bool OnRowSelected(object? row)
    {
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.SelectRow)) return false;

        if (IsTemplateRail) TemplateViewModel.SelectedItem = row as ReportTemplateDto;
        else ListViewModel.SelectedItem = row as ReportGenerationRow;
        return true;
    }

    /// <summary>지금 뷰모델이 쥐고 있는 줄 — 막혔을 때 뷰가 여기로 선택을 되돌린다.</summary>
    public object? CurrentRow => IsTemplateRail ? TemplateViewModel.SelectedItem : ListViewModel.SelectedItem;
    #endregion

    #region - Toolbar -
    public string SearchText
    {
        get => IsTemplateRail ? TemplateViewModel.SearchText ?? string.Empty : ListViewModel.SearchText ?? string.Empty;
        set
        {
            // 검색은 선택을 바꾸지 않으므로 막지 않는다(커널 계약: Search 는 통과).
            if (IsTemplateRail) TemplateViewModel.SearchText = value;
            else ListViewModel.SearchText = value;
            NotifyOfPropertyChange();
            RaiseStatus();
        }
    }

    public bool ShowSearch => !IsCreateRail;

    public string AddText => IsTemplateRail ? "새 템플릿" : "새 보고서";

    public bool CanAdd => CanEditReports && !IsCreateRail;

    public string AddBlockedReason => !CanEditReports
        ? "보고서를 만들 권한이 없습니다."
        : "이미 새 보고서 화면입니다.";

    public bool CanDelete => CanEditReports && (IsTemplateRail ? TemplateViewModel.CanDelete : IsListRail && ListViewModel.CanDelete);

    public string DeleteBlockedReason => !CanEditReports
        ? "지울 권한이 없습니다."
        : IsCreateRail ? "이 화면에는 지울 것이 없습니다." : "지울 줄을 먼저 고르세요.";

    public bool CanReload => !ListViewModel.IsBusy && !TemplateViewModel.IsBusy;

    /// <summary>[새 보고서] · [새 템플릿].</summary>
    public async Task AddAsync()
    {
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.BeginCreate)) return;

        if (IsTemplateRail)
        {
            TemplateViewModel.SelectedItem = null;
            Detail.IsCreating = true;
            Detail.SelectedCount = 0;
            await EditViewModel.LoadNewAsync();
            EditViewModel.CanEdit = CanEditReports;
            RaiseAll();
            return;
        }

        await SelectRailAsync(ReportConsoleRails.Create);
    }

    /// <summary>툴바 [삭제] — 지금 화면의 파괴적 동작. 상세 하단의 [삭제] 와 같은 길이다.</summary>
    public async Task DeleteAsync()
    {
        if (!CanDelete) return;
        if (IsTemplateRail) await TemplateViewModel.Delete();
        else await ListViewModel.Delete();
    }

    public async Task ReloadAsync()
    {
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.Refresh)) return;
        await SelectRailAsync(SelectedRailKey, force: true);
    }
    #endregion

    #region - Detail (여섯 상태 · 고정 막대) -
    public ConsoleDetailPresenter Detail { get; }

    /// <summary>
    /// 고정 막대를 낼 것인가. 생성 이력 화면의 상세는 <b>미리보기</b>라 적용 막대가 없고
    /// 자기 동작 줄([크게 보기] · [PDF 내려받기] · [상세 CSV] · [취소] · [삭제])을 갖는다(WL L1269 · L1280).
    /// </summary>
    public bool ShowDetailButtons => !IsListRail;

    public string DetailApplyText => IsCreateRail ? "생성" : EditViewModel.IsCreate ? "등록" : "적용";

    public string DetailRevertText => IsCreateRail ? "비우기" : EditViewModel.IsCreate ? "취소" : "되돌리기";

    public bool DetailCanApply => IsCreateRail
        ? CreateViewModel.CanGenerate && CreateViewModel.HasInput && CanEditReports
        : IsTemplateRail && CanEditReports && (Detail.CanApply || (EditViewModel.IsCreate && EditViewModel.Board.EnabledCount > 0));

    public bool DetailCanRevert => IsCreateRail
        ? CreateViewModel.HasInput && !CreateViewModel.IsGenerating
        : IsTemplateRail && (Detail.CanRevert || EditViewModel.IsCreate);

    /// <summary>바닥 막대 글 — 생성 화면에서는 그 화면의 진행 문구를 그대로 보인다.</summary>
    public string DetailFooterText
    {
        get
        {
            if (IsCreateRail)
                return string.IsNullOrEmpty(CreateViewModel.StatusText) ? CreateFormFooter : CreateViewModel.StatusText;
            if (IsTemplateRail && EditViewModel.HasStatus && !Detail.IsDirty) return EditViewModel.StatusText;
            return Detail.FooterText;
        }
    }

    public string DetailTitle => IsCreateRail ? "새 보고서" : Detail.Title;

    public string DetailKind => IsCreateRail ? "보고서" : Detail.Kind;

    public string DetailBanner => IsListRail ? string.Empty : Detail.Banner;

    // 손댄 칸 표지 — 커널 ConsoleField 의 IsTouched 에 묶는다(칸 왼쪽에 표지가 선다).
    public bool IsNameTouched => Detail.Tracker.IsTouched(ReportTemplateEditViewModel.FieldName);
    public bool IsDescriptionTouched => Detail.Tracker.IsTouched(ReportTemplateEditViewModel.FieldDescription);
    public bool IsPeriodTouched => Detail.Tracker.IsTouched(ReportTemplateEditViewModel.FieldPeriod);
    public bool IsComponentsTouched => Detail.Tracker.IsTouched(ReportTemplateEditViewModel.FieldComponents);

    /// <summary>드래그 전용 UI 를 내지 않는다 — 같은 일을 하는 키보드 경로를 글로도 알린다.</summary>
    public string ReorderHint => ReportTemplateEditViewModel.ReorderHint;

    /// <summary>[적용] · [생성] · [등록].</summary>
    public async Task ApplyAsync()
    {
        if (IsCreateRail)
        {
            await CreateViewModel.Generate();
            RaiseAll();
            return;
        }
        if (!IsTemplateRail) return;

        var wasCreate = EditViewModel.IsCreate;
        var id = await EditViewModel.ApplyAsync();
        if (id is null) { RaiseAll(); return; }

        var applied = Detail.Tracker.Count;
        EditViewModel.Settle();
        Detail.Settle(wasCreate ? "템플릿을 등록했습니다" : ConsoleDetailStateMachine.AppliedMessage(1, applied));
        Detail.IsCreating = false;

        await TemplateViewModel.LoadAsync();
        TemplateViewModel.SelectById(id.Value);
        await RefreshCreateTemplatesAsync();   // 생성 화면 목록도 그 자리에서 갱신
        RaiseAll();
    }

    /// <summary>[되돌리기] · [비우기] · [취소].</summary>
    public void Revert()
    {
        if (IsCreateRail)
        {
            CreateViewModel.Reset();
            RaiseAll();
            return;
        }
        if (!IsTemplateRail) return;

        if (EditViewModel.IsCreate)
        {
            Detail.IsCreating = false;
            EditViewModel.Clear();
            Detail.Settle("등록을 취소했습니다");
        }
        else
        {
            EditViewModel.Revert();
            Detail.Settle("되돌렸습니다");
        }
        RaiseAll();
    }
    #endregion

    #region - 상세 동작 줄(생성 이력) -
    public async Task DownloadPdfAsync()
    {
        await ListViewModel.DownloadAsync();
        RaiseStatus();
    }

    public async Task DownloadCsvAsync()
    {
        await ListViewModel.DownloadCsvAsync(PreviewViewModel.SelectedCsvType?.Value);
        RaiseStatus();
    }

    public Task CancelGenerationAsync() => ListViewModel.Cancel();

    public Task DeleteGenerationAsync() => ListViewModel.Delete();

    public bool CanCancelGeneration => CanEditReports && ListViewModel.CanCancel;
    public bool CanDeleteGeneration => CanEditReports && ListViewModel.CanDelete;
    public bool CanDownloadPdf => ListViewModel.CanDownload;
    #endregion

    #region - ★ 공역(airspace) -
    private ConsoleLayoutMode _layoutMode = ConsoleLayoutMode.Docked;
    /// <summary>뷰가 커널의 폭 판정을 여기에 알려 준다.</summary>
    public ConsoleLayoutMode LayoutMode
    {
        get => _layoutMode;
        set
        {
            if (_layoutMode == value) return;
            _layoutMode = value;
            PreviewViewModel.LayoutMode = value;
            NotifyOfPropertyChange();
        }
    }

    /// <summary>
    /// 같은 창에 WPF 팝업을 띄우는 동안 미리보기를 내린다. 겹쳐 잠가도 안전하다.
    /// </summary>
    public IDisposable Block()
    {
        _overlayDepth++;
        PreviewViewModel.IsOverlayOpen = true;
        return new OverlayHold(this);
    }

    private void Release()
    {
        if (_overlayDepth > 0) _overlayDepth--;
        if (_overlayDepth == 0) PreviewViewModel.IsOverlayOpen = false;
    }

    /// <summary>
    /// [크게 보기] — <b>자체 HWND 를 갖는 최상위 창</b>으로 띄워 달라고 뷰에 알린다.
    /// 인-윈도우 다이얼로그로 띄우면 같은 공역이라 아무것도 해결되지 않는다.
    /// </summary>
    public void OpenLargePreview()
    {
        if (!PreviewViewModel.CanOpenLargeView) return;
        PreviewViewModel.IsLargeViewOpen = true;
        LargePreviewRequested?.Invoke(PreviewViewModel);
        RaiseStatus();
    }

    /// <summary>큰 창이 닫혔다 — 상세 칸 미리보기를 되돌린다.</summary>
    public void OnLargePreviewClosed()
    {
        PreviewViewModel.IsLargeViewOpen = false;
        RaiseStatus();
    }

    /// <summary>뷰가 구독해 최상위 <c>Window</c> 를 연다.</summary>
    public event Action<ReportPreviewViewModel>? LargePreviewRequested;

    private sealed class OverlayHold : IDisposable
    {
        private ReportConsoleViewModel? _owner;
        public OverlayHold(ReportConsoleViewModel owner) => _owner = owner;
        public void Dispose()
        {
            var owner = _owner;
            _owner = null;
            owner?.Release();
        }
    }
    #endregion

    #region - Status bar -
    /// <summary>상태 띠 왼쪽 — 건수(WL L1266 아래 30px 띠).</summary>
    public string ListStatusText => IsTemplateRail ? TemplateViewModel.CountText : ListViewModel.CountText;

    private string _statusText = string.Empty;
    /// <summary>상태 띠 오른쪽 — 방금 한 일 한 줄.</summary>
    public string StatusText
    {
        get => string.IsNullOrEmpty(ListViewModel.ActionStatus) ? _statusText : ListViewModel.ActionStatus!;
        set { _statusText = value ?? string.Empty; NotifyOfPropertyChange(); }
    }

    /// <summary>지금 상태 필터 이름 — 칩이 무엇을 거르는지 글로도 알린다.</summary>
    public string FilterSummary => ListViewModel.FilterSummary;
    #endregion

    #region - Permissions -
    private void OnPermissionsChanged() => RefreshPermissions();

    private void RefreshPermissions()
    {
        CanViewReports = _permission?.CanView("reports") ?? true;
        CanEditReports = _permission?.CanEdit("reports") ?? true;
        // 편집 권한이 없으면 상세 칸은 여섯 상태의 "읽기 전용"이다(WL L929).
        Detail.IsReadOnly = !CanEditReports;
        EditViewModel.CanEdit = CanEditReports;
        RaiseAll();
    }

    public bool CanViewReports { get; private set; } = true;
    public bool CanEditReports { get; private set; } = true;
    #endregion

    #region - Handlers -
    private async void OnGenerationSelected(ReportGenerationRow? row)
    {
        Detail.SelectedCount = row is null ? 0 : 1;
        Detail.SingleTitle = row?.Title ?? string.Empty;
        Detail.SingleNumber = row is null ? string.Empty : $"#{row.Id}";
        RaiseAll();
        try { await PreviewViewModel.ShowAsync(row); }
        catch (Exception ex) { _log?.Error($"[ReportConsole] 미리보기: {ex.Message}"); }
        RaiseAll();
    }

    private void OnGenerationDeleted(int id)
    {
        StatusText = $"보고서 #{id} 를 지웠습니다";
        RefreshRailCounts();
        RaiseAll();
    }

    private async void OnTemplateSelected(ReportTemplateDto? tpl)
    {
        Detail.IsCreating = false;
        Detail.SelectedCount = tpl is null ? 0 : 1;
        Detail.SingleTitle = tpl?.Name ?? string.Empty;
        Detail.SingleNumber = tpl is null ? string.Empty : $"#{tpl.Id}";
        RaiseAll();
        try { await EditViewModel.LoadAsync(tpl); }
        catch (Exception ex) { _log?.Error($"[ReportConsole] 템플릿 적재: {ex.Message}"); }
        EditViewModel.CanEdit = CanEditReports;
        RaiseAll();
    }

    /// <summary>템플릿 집합이 바뀌었다 — 생성 화면 목록도 그 자리에서 갱신한다.</summary>
    private async void OnTemplatesChanged()
    {
        await RefreshCreateTemplatesAsync();
        RaiseAll();
    }

    private async Task RefreshCreateTemplatesAsync()
    {
        try { await CreateViewModel.LoadTemplatesAsync(); }
        catch (Exception ex) { _log?.Error($"[ReportConsole] 생성 화면 템플릿 갱신: {ex.Message}"); }
    }

    /// <summary>생성이 끝났다 — 목록을 갱신하고 그 보고서를 골라 상세 칸에 미리보기를 올린다.</summary>
    private async void OnReportGenerated(int generationId)
    {
        try
        {
            await ListViewModel.LoadAsync();
            RefreshRailCounts();
            var row = ListViewModel.Rows.FirstOrDefault(r => r.Id == generationId);
            if (row != null)
            {
                ListViewModel.SelectedItem = row;
                StatusText = $"보고서 #{generationId} 를 만들었습니다";
            }
        }
        catch (Exception ex) { _log?.Error($"[ReportConsole] 생성 완료 처리: {ex.Message}"); }
        RaiseAll();
    }

    private void OnComponentsReordered(object? sender, EventArgs e) => RaiseAll();

    private void OnDetailChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => RaiseDetail();

    private void OnChildPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ReportListViewModel.IsBusy):     // 템플릿 화면의 IsBusy 도 같은 이름이라 한 가지로 받는다
            case nameof(ReportCreateViewModel.IsGenerating):
            case nameof(ReportCreateViewModel.HasInput):
            case nameof(ReportCreateViewModel.StatusText):
            case nameof(ReportListViewModel.ActionStatus):
                RaiseAll();
                break;
        }
    }
    #endregion

    #region - Notifications -
    private void RaiseRail()
    {
        NotifyOfPropertyChange(nameof(SelectedRail));
        NotifyOfPropertyChange(nameof(SelectedRailKey));
        NotifyOfPropertyChange(nameof(IsListRail));
        NotifyOfPropertyChange(nameof(IsCreateRail));
        NotifyOfPropertyChange(nameof(IsTemplateRail));
        NotifyOfPropertyChange(nameof(Rows));
        NotifyOfPropertyChange(nameof(Columns));
        NotifyOfPropertyChange(nameof(ListCaption));
        NotifyOfPropertyChange(nameof(HasListCaption));
        NotifyOfPropertyChange(nameof(RailFooterText));
        NotifyOfPropertyChange(nameof(SearchText));
        NotifyOfPropertyChange(nameof(ShowSearch));
        NotifyOfPropertyChange(nameof(AddText));
    }

    private void RaiseDetail()
    {
        NotifyOfPropertyChange(nameof(ShowDetailButtons));
        NotifyOfPropertyChange(nameof(DetailApplyText));
        NotifyOfPropertyChange(nameof(DetailRevertText));
        NotifyOfPropertyChange(nameof(DetailCanApply));
        NotifyOfPropertyChange(nameof(DetailCanRevert));
        NotifyOfPropertyChange(nameof(DetailFooterText));
        NotifyOfPropertyChange(nameof(DetailTitle));
        NotifyOfPropertyChange(nameof(DetailKind));
        NotifyOfPropertyChange(nameof(DetailBanner));
        NotifyOfPropertyChange(nameof(IsNameTouched));
        NotifyOfPropertyChange(nameof(IsDescriptionTouched));
        NotifyOfPropertyChange(nameof(IsPeriodTouched));
        NotifyOfPropertyChange(nameof(IsComponentsTouched));
    }

    private void RaiseStatus()
    {
        NotifyOfPropertyChange(nameof(ListStatusText));
        NotifyOfPropertyChange(nameof(StatusText));
        NotifyOfPropertyChange(nameof(FilterSummary));
        NotifyOfPropertyChange(nameof(CanCancelGeneration));
        NotifyOfPropertyChange(nameof(CanDeleteGeneration));
        NotifyOfPropertyChange(nameof(CanDownloadPdf));
    }

    private void RaiseAll()
    {
        RaiseRail();
        RaiseDetail();
        RaiseStatus();
        NotifyOfPropertyChange(nameof(CanAdd));
        NotifyOfPropertyChange(nameof(CanDelete));
        NotifyOfPropertyChange(nameof(CanReload));
        NotifyOfPropertyChange(nameof(AddBlockedReason));
        NotifyOfPropertyChange(nameof(DeleteBlockedReason));
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

    public string PanelTitle => "보고서";

    /// <summary>상세 칸 기본 폭 — 목업이 "380, 미리보기라 넓게" 라고 적었다(WL L1259 · L1271).</summary>
    public double DefaultDetailWidth => 380;

    public Task Close() => TryCloseAsync();
    #endregion

    #region - Attributes -
    internal const string CreateFormFooter = "[생성] 을 누르면 왼쪽 목록에 나타나고 진행 상황이 보입니다";
    internal const string CreateFormBanner = "제목과 기간을 정하면 보고서를 만들 수 있습니다. 진행 상황은 왼쪽 목록에서 볼 수 있습니다.";
    internal const string TemplateCreateBanner = "이름과 구성 요소를 고르면 템플릿을 등록할 수 있습니다.";

    private readonly IPermissionService _permission;
    private int _overlayDepth;
    #endregion
}

/// <summary>
/// 레일 아이콘 토큰 — 싱글턴 뷰모델이 <c>PackIcon</c> 같은 시각 요소를 쥐면 뷰가 새로 만들어질 때
/// 옛 트리에 묶인다. 이름만 쥐고 그리기는 뷰가 한다(장비 콘솔 선례).
/// </summary>
public sealed record ReportRailIcon(string Kind);
