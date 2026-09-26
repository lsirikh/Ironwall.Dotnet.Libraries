using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Lists;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Preview;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
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
public class ReportConsoleViewModel : BasePanelViewModel, IPreviewAirspaceGate, IHandle<ClosePopupMessageModel>
{
    public const string ConsoleKey = "Reports";
    /// <summary>서버 권한 모듈 — 조회 view · 생성/템플릿 쓰기 edit · 삭제/생성 취소 delete.</summary>
    public const string PermissionModuleKey = "reports";

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
        // 런타임 유무는 화면 모드와 무관하게 한 번 묻는다 - 좁은 창에서는 시도 자체가 없어 영영 알 수 없다.
        PreviewViewModel.ProbeRuntime();
        RefreshPermissions();
        await SelectRailAsync(ReportConsoleRails.List, force: true);
        // R17 — 생성 이력의 "템플릿" 을 번호(#5)가 아니라 이름으로 보이려면 템플릿 목록이 있어야 한다.
        //        생성 화면 콤보가 쓰는 목록을 한 번 받아 두고 이력에 이름을 꽂는다(GET /templates 1회).
        await RefreshCreateTemplatesAsync();
        RaiseAll();
    }

    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        await ScreenExtensions.TryDeactivateAsync(ListViewModel, close, cancellationToken);
        await ScreenExtensions.TryDeactivateAsync(CreateViewModel, close, cancellationToken);
        await ScreenExtensions.TryDeactivateAsync(TemplateViewModel, close, cancellationToken);
        Unsubscribe();

        // 싱글턴이라 다음에 열 때 옛 선택 · 미적용 변경 · 미리보기가 남아 있으면 안 된다.
        Detail.Reset();
        ReleaseAllAirspaceHolds();     // 자식이 쥔 표까지 내려놓는다 - 남으면 다음 세션의 첫 확인 창이 안 잠긴다
        PreviewViewModel.Clear();
        EditViewModel.Clear();
        _isSwitching = false;

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
                // 막았다 — 목록 선택을 지금 레일로 되돌린다. ListBox 가 자기 TwoWay 갱신 도중이라
                // 그 자리의 알림만으로는 되돌아가지 않을 수 있어, 한 박자 뒤에 한 번 더 울린다.
                NotifyOfPropertyChange();
                Execute.BeginOnUIThread(() => NotifyOfPropertyChange(nameof(SelectedRail)));
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

        // 두 전환이 await 사이에 끼어들면 목록은 C 인데 열은 B 인 화면이 된다(장비 콘솔 선례).
        if (_isSwitching) return;
        _isSwitching = true;

        ReleaseAllAirspaceHolds();     // 전환은 남은 팝업 표를 들고 가지 않는다
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
                    // 활성화가 이미 LoadAsync 를 부른다 - 여기서 또 부르면 첫 열기에 두 번 조회한다.
                    // 이미 활성이면 활성화가 아무것도 안 하므로 그때만 직접 부른다.
                    if (ListViewModel.IsActive) await ListViewModel.LoadAsync();
                    else await ScreenExtensions.TryActivateAsync(ListViewModel);
                    break;

                case ReportConsoleRails.Create:
                    // 템플릿 목록을 여기서 다시 받는다 — 레일 전환이 곧 활성화다.
                    if (!CreateViewModel.IsActive) await ScreenExtensions.TryActivateAsync(CreateViewModel);
                    else await CreateViewModel.LoadTemplatesAsync();
                    if (ListViewModel.IsActive) await ListViewModel.LoadAsync();   // 왼쪽 칸에 최근 생성 이력
                    else await ScreenExtensions.TryActivateAsync(ListViewModel);
                    PushTemplateNames();   // 방금 받은 템플릿 목록으로 이력의 템플릿 이름도 맞춘다
                    break;

                case ReportConsoleRails.Template:
                    if (TemplateViewModel.IsActive) await TemplateViewModel.LoadAsync();
                    else await ScreenExtensions.TryActivateAsync(TemplateViewModel);
                    break;
            }
        }
        catch (Exception ex) { _log?.Error($"[ReportConsole] 레일 전환({key}): {ex.Message}"); }
        finally { _isSwitching = false; }

        RefreshRailCounts();
        RaiseAll();
    }

    /// <summary>진행 중 건수 배지(WL L1262).</summary>
    public void RefreshRailCounts()
    {
        var listEntry = RailEntries.First(r => r.Key == ReportConsoleRails.List);
        listEntry.Count = ListViewModel.InProgressCount;
        // R4 — 0 은 보이지 않는다. "생성 이력 0" 이 "보고서 0건" 으로 읽혔다(배지는 진행 중 건수다 — 상태 띠가 글로도 말한다).
        listEntry.ShowCount = listEntry.Count > 0;
    }

    /// <summary>머리 부제 — 짧게. 같은 문장을 레일 바닥 · 자리표시자와 겹쳐 놓지 않는다.</summary>
    public string RailSubtitle => IsTemplateRail ? "템플릿" : IsCreateRail ? "새 보고서" : "생성 이력";

    /// <summary>레일 바닥(184 폭) — 한 줄에 들어가는 길이로.</summary>
    /// <remarks>R5 — 끊긴 메모("오른쪽 아래 [생성]") 대신 할 일을 존댓말로. 서랍 폭에서는 오른쪽 칸이 없으므로 방향을 말하지 않는다.</remarks>
    /// 두 줄이 되는 글은 뜻 단위로 직접 끊는다 — 맡겨 두면 184 폭에서 "…미리보기 / 가 열립니다" 로 갈렸다.
    public string RailFooterText => IsTemplateRail
        ? "템플릿을 고르면\n편집 칸이 열립니다"
        : IsCreateRail ? "입력 후 [생성]을 누르세요"
        : "보고서를 고르면\n미리보기가 열립니다";
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

    /// <summary>R6 — 검색창 안내 글은 지금 목록의 이름 칸을 따른다(템플릿 목록은 "이름").</summary>
    public string SearchPlaceholder => IsTemplateRail ? "이름 검색" : "제목 검색";

    public string AddText => IsTemplateRail ? "새 템플릿" : "새 보고서";

    public bool CanAdd => CanEditReports && !IsCreateRail;

    public string AddBlockedReason => !CanEditReports
        ? "보고서를 만들 권한이 없습니다."
        : "이미 새 보고서 화면입니다.";

    /// <summary>
    /// 툴바 [삭제] — 서버는 템플릿 · 생성 이력 삭제를 <c>reports:delete</c> 로 거른다
    /// (api-test-server routers/reports.py delete_template · delete_generation, permission_map.py).
    /// <c>reports:edit</c> 로 켜면 편집만 가진 사용자에게 눌러도 403 인 단추가 선다(실측).
    /// </summary>
    public bool CanDelete => CanDeleteReports && (IsTemplateRail ? TemplateViewModel.CanDelete : IsListRail && ListViewModel.CanDelete);

    public string DeleteBlockedReason => !CanDeleteReports
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
    /// <summary>
    /// 툴바 · 상세의 [삭제].
    /// </summary>
    /// <remarks>
    /// ★ 삭제는 <b>선택을 없애는 이동</b>이다. 막지 않으면 미적용 변경이 있는 채로 지워져
    /// 선택은 0 인데 장부는 더러운 상태가 되고, 그때부터 [적용] · [되돌리기] 는 꺼져 있는데
    /// 이동은 전부 막히는 <b>막다른 골목</b>이 된다.
    /// </remarks>
    public async Task DeleteAsync()
    {
        if (!CanDelete) return;
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.SelectRow)) return;

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
            // 생성 이력 칸은 고칠 것이 없는 미리보기다 — "변경 없음" 은 뜻이 없다(적용 막대도 없다).
            if (IsListRail) return string.Empty;
            if (Detail.State == ConsoleDetailState.Create
                && Detail.FooterText == ConsoleDetailStateMachine.FooterText(ConsoleDetailState.Create, 0))
                return CreateFooterText;
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
            if (!CanEditReports) { CreateViewModel.StatusText = "보고서를 만들 권한이 없습니다."; return; }
            await CreateViewModel.Generate();
            RaiseAll();
            return;
        }
        if (!IsTemplateRail) return;
        // 단추를 끄는 것만으로는 부족하다 — 뷰모델 경로에서도 거절한다.
        if (!CanEditReports) { EditViewModel.StatusText = "편집 권한이 없습니다."; return; }

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

    /// <summary>
    /// 상세 하단의 [취소]. 단추를 끄는 것만으로는 부족하다 — 뷰모델 경로에서도 거절한다
    /// (서버는 취소를 삭제와 같은 <c>reports:delete</c> 로 거른다).
    /// </summary>
    public Task CancelGenerationAsync() => CanCancelGeneration ? ListViewModel.Cancel() : Task.CompletedTask;

    /// <summary>상세 하단의 [삭제] — 툴바와 같은 길(이동 차단 포함).</summary>
    public Task DeleteGenerationAsync() => DeleteAsync();

    public bool CanCancelGeneration => CanDeleteReports && ListViewModel.CanCancel;
    public bool CanDeleteGeneration => CanDeleteReports && ListViewModel.CanDelete;
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
        if (PreviewViewModel.IsLargeViewOpen) { LargePreviewActivateRequested?.Invoke(); return; }
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

    /// <summary>이미 열려 있다 — 새로 만들지 말고 그 창을 앞으로 가져온다.</summary>
    public event System.Action? LargePreviewActivateRequested;

    /// <summary>
    /// ★ 팝업이 닫혔다 — 자식이 쥔 공역 표를 내려놓는다.
    /// </summary>
    /// <remarks>
    /// 호스트의 확인 팝업은 <b>[확인] 에서만</b> 우리 메시지를 낸다([취소] 는
    /// <c>ClosePopupMessageModel</c> 만 낸다). 그래서 확인 왕복의 끝에서만 풀면 <b>[취소] 를 누른 순간
    /// 미리보기가 세션 내내 자리표시자로 굳고</b>, 그 표가 남은 채 콘솔을 닫으면 다음 세션의 첫 확인 창이
    /// 아예 안 잠긴다(원래 결함이 되살아난다). 닫힘 신호 하나로 항상 되돌린다.
    /// </remarks>
    public Task HandleAsync(ClosePopupMessageModel message, CancellationToken cancellationToken)
    {
        ReleaseAllAirspaceHolds();
        return Task.CompletedTask;
    }

    /// <summary>자식이 쥔 표와 깊이를 한꺼번에 내려놓는다.</summary>
    private void ReleaseAllAirspaceHolds()
    {
        ListViewModel.ReleaseAirspace();
        TemplateViewModel.ReleaseAirspace();
        _overlayDepth = 0;
        PreviewViewModel.IsOverlayOpen = false;
    }

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
    /// <remarks>R4 — 레일 배지가 무엇을 세는지 글로도 말한다("6건 · 진행 중 2건").</remarks>
    public string ListStatusText => IsTemplateRail
        ? TemplateViewModel.CountText
        : ListViewModel.InProgressCount > 0
            ? $"{ListViewModel.CountText} · 진행 중 {ListViewModel.InProgressCount}건"
            : ListViewModel.CountText;

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
        CanViewReports = PermissionUiPolicy.Allowed(_permission, PermissionModuleKey, EnumPermissionVerb.View);
        CanEditReports = PermissionUiPolicy.Allowed(_permission, PermissionModuleKey, EnumPermissionVerb.Edit);
        // 삭제 · 생성 취소는 서버가 edit 가 아니라 delete 로 거른다 — 편집 권한에 얹지 않는다.
        CanDeleteReports = PermissionUiPolicy.Allowed(_permission, PermissionModuleKey, EnumPermissionVerb.Delete);
        // 편집 권한이 없으면 상세 칸은 여섯 상태의 "읽기 전용"이다(WL L929).
        Detail.IsReadOnly = !CanEditReports;
        EditViewModel.CanEdit = CanEditReports;
        // 조회 권한은 단추만 끄지 않는다 — 목록 · 미리보기 · 내려받기를 뷰모델에서 막는다.
        ListViewModel.CanView = CanViewReports;
        TemplateViewModel.CanView = CanViewReports;
        RaiseAll();
    }

    public bool CanViewReports { get; private set; } = true;
    public bool CanEditReports { get; private set; } = true;
    /// <summary>템플릿 · 생성 이력 삭제와 생성 취소 — 서버 <c>reports:delete</c>.</summary>
    public bool CanDeleteReports { get; private set; } = true;
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
        StatusText = $"보고서(#{id})를 삭제했습니다";
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
        PushTemplateNames();
    }

    /// <summary>
    /// R17 — 받은 템플릿 목록을 생성 이력에 이름으로 꽂는다. 목록을 못 받았으면 <c>null</c> 을 꽂아
    /// "삭제된 템플릿" 이라고 단정하지 않게 한다(번호만 보인다).
    /// </summary>
    private void PushTemplateNames()
    {
        ListViewModel.TemplateNames = CreateViewModel.TemplatesLoaded
            ? CreateViewModel.Templates.GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First().Name ?? string.Empty)
            : null;
        PreviewViewModel.RefreshMeta();
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
                StatusText = $"보고서(#{generationId})를 만들었습니다";
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

            // 목록 · 템플릿의 SelectedItem 은 이름이 같아 한 가지로 받는다.
            // 뷰모델이 스스로 줄을 고르는 길(생성 완료 · 되돌리기 · 재조회 화해)에서도
            // 그리드가 따라오게 하는 유일한 신호다 — 없으면 상세 칸만 바뀌고 목록은 아무 줄도 안 켜진다.
            case nameof(ReportListViewModel.SelectedItem):
                NotifyOfPropertyChange(nameof(CurrentRow));
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
        NotifyOfPropertyChange(nameof(CurrentRow));   // 레일이 바뀌면 '지금 고른 줄' 의 출처 자체가 바뀐다
        NotifyOfPropertyChange(nameof(Columns));
        NotifyOfPropertyChange(nameof(ListCaption));
        NotifyOfPropertyChange(nameof(HasListCaption));
        NotifyOfPropertyChange(nameof(RailFooterText));
        NotifyOfPropertyChange(nameof(RailSubtitle));
        NotifyOfPropertyChange(nameof(SearchText));
        NotifyOfPropertyChange(nameof(ShowSearch));
        NotifyOfPropertyChange(nameof(SearchPlaceholder));
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
        NotifyOfPropertyChange(nameof(CanDeleteReports));
    }
    #endregion

    #region - Properties -
    public ReportListViewModel ListViewModel { get; }
    public ReportCreateViewModel CreateViewModel { get; }
    public ReportTemplateViewModel TemplateViewModel { get; }
    public ReportPreviewViewModel PreviewViewModel { get; }
    public ReportTemplateEditViewModel EditViewModel { get; }

    public string PanelTitle => "보고서";

    public Task Close() => TryCloseAsync();

    /// <summary>
    /// 템플릿 편집 칸에 적용하지 않은 변경이 있으면 닫지 않는다 — 바닥 막대가 흔들리며 "적용하거나 되돌린 뒤 이동하세요" 라고
    /// 말한다(저장 안 한 변경을 말없이 버리지 않는다. 선례: 이벤트 매핑 워크벤치). 편집 권한이 없으면 붙잡지 않는다.
    /// </summary>
    public override Task<bool> CanCloseAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(!Detail.Tracker.IsDirty || !CanEditReports || Detail.Guard.TryNavigate(ConsoleNavigation.SelectRow));
    #endregion

    #region - Attributes -
    internal const string CreateFormFooter = "왼쪽 목록에서 진행됩니다";
    // 안내 띠는 상세 칸(서랍 360 · 도킹 380)에서 두 줄이 된다 — 한글이 음절 중간("진 / 행", "있습 / 니다")에서
    // 갈리지 않게 문장 단위로 직접 끊는다(한 줄은 22자 안쪽).
    internal const string CreateFormBanner = "제목과 기간을 정하고 [생성]을 누르세요.\n진행 상황은 왼쪽 목록에 나옵니다.";
    internal const string TemplateCreateBanner = "이름을 적고 구성 요소를 고른 뒤\n[등록]을 누르세요.";

    /// <summary>등록 폼 바닥 막대 — 커널 글("등록 전에는 목록에 나타나지 않습니다")이 막대에서 "않습 / 니다" 로 갈렸다.</summary>
    internal const string CreateFooterText = "등록해야 목록에 나타납니다";

    private readonly IPermissionService _permission;
    private int _overlayDepth;
    /// <summary>레일 전환 재진입 가드.</summary>
    private bool _isSwitching;
    #endregion
}

/// <summary>
/// 레일 아이콘 토큰 — 싱글턴 뷰모델이 <c>PackIcon</c> 같은 시각 요소를 쥐면 뷰가 새로 만들어질 때
/// 옛 트리에 묶인다. 이름만 쥐고 그리기는 뷰가 한다(장비 콘솔 선례).
/// </summary>
public sealed record ReportRailIcon(string Kind);
