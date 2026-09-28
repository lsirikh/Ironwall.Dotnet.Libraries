using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.ActionReportTemplates;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;

/// <summary>
/// 조치보고 문구 관리 콘솔 — 목록(드래그 재정렬) + 상세(등록 · 수정, 여섯 상태 · 고정 적용 막대).
/// </summary>
/// <remarks>
/// <para><b>독립 콘솔로 둔 이유</b>: 보고서 콘솔(<c>ReportConsoleViewModel</c>)의 목록 칸은 "고른 한 줄의 필드를
/// 오른쪽 칸에서 고치는" 축(예: 템플릿 구성요소 순서)만 갖고 있고, 이 화면은 <b>목록 그 자체의 줄 순서</b>를
/// 바꾼다 — 서로 다른 재정렬 축이다. 그 축을 786행짜리 기존 뷰모델(Rows/Columns/Apply/Revert 가 전부
/// 레일별로 다중화돼 있다)에 얹으면 회귀 반경이 커서, 같은 커널 부품(ConsoleShell · ConsoleDetailHost ·
/// ConsoleDetailPresenter · 드래그 behaviors)을 <b>구성으로 재사용</b>하는 새 콘솔로 둔다.</para>
/// <para>레일은 항목 1개뿐이다 — 다중 모드가 필요 없는 화면에서도 커널 크롬(제목 · 부제)을 일관되게 쓴다.</para>
/// <para>재정렬 커밋은 <see cref="IActionReportTemplateApiService.ReorderTemplatesAsync"/> <b>단일 호출</b>이다 —
/// 행마다 PATCH 를 반복하지 않는다(드래그 규칙). 실패하면 서버를 다시 불러오고, 성공하면 그 직전 순서를
/// 들고 있다가 [되돌리기]에서 같은 엔드포인트로 한 번 더 보낸다(Undo = 두 번째 reorder 호출).</para>
/// </remarks>
public class ActionReportTemplateConsoleViewModel : BasePanelViewModel, IHandle<CallDeleteActionReportTemplateProcessMessageModel>,
                                                    IHandle<ActionReportTemplatesChangedMessage>
{
    public const string ConsoleKey = "ActionReportTemplates";
    public const string RailKey = "templates";
    /// <summary>쓰기(등록 · 수정 · 순서) = edit, 삭제 = delete. 서버에 이 모듈의 view 는 쓰이지 않는다.</summary>
    public const string PermissionModuleKey = "action_report_templates";
    /// <summary>
    /// 목록 읽기 권한 모듈. 서버는 목록 GET 을 <c>events:view</c> 로 거른다
    /// (api-test-server routers/action_report_templates.py list_action_report_templates) —
    /// 문구는 조치보고 입력 드롭다운을 채우는 이벤트 데이터이기 때문이다. 여기서
    /// <see cref="PermissionModuleKey"/> 의 view 를 요구하면 서버가 허락한 사용자를 화면이 막는다.
    /// </summary>
    public const string ReadPermissionModuleKey = "events";
    public const string FieldContent = "content";
    public const int MaxContentLength = 500;

    #region - Ctors -
    public ActionReportTemplateConsoleViewModel(IEventAggregator eventAggregator, ILogService log,
        IPermissionService permission, IActionReportTemplateApiService api)
        : base(eventAggregator, log)
    {
        _permission = permission;
        _api = api;

        Board = new ActionReportTemplateBoard();
        Board.Changed += OnBoardChanged;
        Drop = new ActionReportTemplateDropHandler(Board, () => CanEdit && !IsReordering && !IsBusy);

        // 칸이 하나뿐인 폼이라 커널 기본 안내("나머지는 등록 후 채워도 됩니다")는 맞지 않는다.
        Detail = new ConsoleDetailPresenter { TypeName = "문구", CreateBanner = "문구를 입력하고 [등록]을 누르세요." };
        Detail.Tracker.MarkIdentity(FieldContent);

        RailEntries = new ObservableCollection<ConsoleRailEntry>
        {
            // 아이콘이 없으면 좁은 폭(레일 56 접힘)에서 빈 칸만 남았다 — 보고서 콘솔과 같은 아이콘 토큰을 준다.
            new ConsoleRailEntry(RailKey, "문구 목록", new ReportRailIcon("FormatListNumbered")) { ShowCount = true },
        };
        _selectedRail = RailEntries[0];

        // SYNC_ACTION_REPORT_TEMPLATE 는 몰려올 수 있다 — 창(500 ms) 안의 알림을 다시 읽기 한 번으로 합친다.
        //   지연은 속성(ExternalChangeDelay)을 늦게 읽는다 — 컨테이너가 만드는 VM 이라 생성자 인자를 늘리지 않는다.
        _externalChange = new CoalescingTrigger(OnExternalChangeSettledAsync,
                                                delay: (window, token) => (ExternalChangeDelay ?? Task.Delay)(window, token),
                                                onError: ex => _log?.Error($"[ActionReportTemplate] 외부 변경 재조회: {ex.Message}"));

        // ⚠ 이벤트 구독은 생성자가 아니라 OnActivateAsync 에서 한다 — 이 VM 은 SingleInstance 라
        //   생성자는 앱 수명당 1회만 실행된다(Reports 콘솔과 같은 함정 회피).
    }
    #endregion

    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        Subscribe();
        RefreshPermissions();
        await LoadAsync();
    }

    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        Unsubscribe();
        _externalChange.Cancel();
        IsExternallyChanged = false;
        _lastCommittedOrder = null;
        _rejectedDraft = null;
        Detail.Reset();
        _selectedItem = null;
        _undoOrder = null;
        _pendingDeleteItem = null;
        await base.OnDeactivateAsync(close, cancellationToken);
    }
    #endregion

    #region - Wiring -
    private void Subscribe()
    {
        Unsubscribe();
        Drop.Reordered += OnReordered;
        Detail.PropertyChanged += OnDetailChanged;
        _permission.PermissionsChanged += OnPermissionsChanged;
    }

    private void Unsubscribe()
    {
        Drop.Reordered -= OnReordered;
        Detail.PropertyChanged -= OnDetailChanged;
        _permission.PermissionsChanged -= OnPermissionsChanged;
    }
    #endregion

    #region - Rail (항목 1개 — 커널 크롬 일관성) -
    public ObservableCollection<ConsoleRailEntry> RailEntries { get; }

    private ConsoleRailEntry _selectedRail;
    public ConsoleRailEntry SelectedRail
    {
        get => _selectedRail;
        set { if (value != null) { _selectedRail = value; NotifyOfPropertyChange(); } }
    }

    /// <summary>레일 바닥(184 폭) — 한 줄에 들어가는 길이로(A2: 세 줄로 음절 중간이 갈렸다). 나머지 안내는 목록 위 힌트가 한다.</summary>
    public string RailFooterText => "끌어서 순서를 바꿉니다";
    #endregion

    #region - List · 드래그 재정렬 -
    public ActionReportTemplateBoard Board { get; }
    public ObservableCollection<ActionReportTemplateItem> Items => Board.Items;

    /// <summary>커널 드래그(마우스 · Alt+↑/↓)가 부르는 담당 — 뷰의 <c>drag:DropZone.Handler</c> 에 묶는다.</summary>
    public ActionReportTemplateDropHandler Drop { get; }

    /// <summary>키보드 · 드래그와 <b>같은 결과</b>를 내는 ▲▼ 단추 경로(뷰 코드비하인드가 부른다).</summary>
    public void MoveSelected(ActionReportTemplateItem? item, int direction)
    {
        if (item is null || !CanEdit || IsReordering) return;
        var items = Board.Items;
        var index = items.IndexOf(item);
        if (index < 0) return;
        var target = index + direction;
        if (target < 0 || target >= items.Count) return;
        var insertionIndex = direction > 0 ? target + 1 : target;

        var previousOrder = Board.OrderedIds;
        if (Board.Move(new[] { index }, insertionIndex))
            _ = CommitReorderAsync(Board.ToReorderPayload(), isUndo: false, undoTarget: previousOrder);
    }

    public bool IsEmpty => Board.Count == 0 && !IsBusy;
    public string CountText => $"{Board.Count}건";

    /// <summary>▲▼ 단추 — 고른 줄이 있을 때만 켠다(A9: 아무것도 안 골랐는데 켜져 눌러도 아무 일이 없었다).</summary>
    public bool CanMoveSelected => CanEdit && SelectedItem != null && !IsReordering;
    public string MoveBlockedReason => !CanEdit ? AddBlockedReason : "옮길 문구를 먼저 고르세요.";
    public string MoveUpToolTip => CanMoveSelected ? "고른 문구를 위로 (Alt+↑)" : MoveBlockedReason;
    public string MoveDownToolTip => CanMoveSelected ? "고른 문구를 아래로 (Alt+↓)" : MoveBlockedReason;

    /// <summary>줄 안의 [수정] — 읽기 전용이면 고칠 수 없으므로 "보기" 라고 말한다.</summary>
    public string RowEditText => CanEdit ? "수정" : "보기";

    /// <summary>줄 안의 [삭제] 도움말 — 꺼져 있으면 까닭을 말한다(A4).</summary>
    public string RowDeleteToolTip => CanDeletePermission ? "이 문구를 삭제합니다"
        : IsUnsupported ? UnsupportedText : "문구를 삭제할 권한이 없습니다.";

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(IsEmpty)); NotifyOfPropertyChange(nameof(CanReload)); }
    }

    private bool _isReordering;
    /// <summary>재정렬 커밋 중 — 목록을 잠가 겹친 드래그를 막는다(드래그 규칙: 커밋 중 IsEnabled=false + 진행 표시).</summary>
    public bool IsReordering
    {
        get => _isReordering;
        set { _isReordering = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CanUndoReorder)); }
    }

    private string? _loadError;
    public string? LoadError { get => _loadError; set { _loadError = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(EmptyStateText)); } }

    /// <summary>G1 ⓑ — 조회 실패와 "정말 없음"을 다른 글로 가른다. 하드코딩 폴백은 쓰지 않는다
    /// (서버에서 지운 문구가 되살아나 저장되는 결함을 반복하지 않는다).</summary>
    public string EmptyStateText => LoadError ?? "등록된 문구가 없습니다.";

    /// <summary>빈 상태의 둘째 줄 — 다음에 무엇을 하면 되는지.</summary>
    public string EmptyStateHint
    {
        get
        {
            if (IsUnsupported) return string.Empty;
            if (!CanViewPermission) return "관리자에게 조치보고 조회 권한을 요청하세요.";
            if (LoadError != null) return "툴바의 새로 고침(⟳)을 눌러 다시 시도하세요. 문구 없이도 조치보고는 직접 입력으로 쓸 수 있습니다.";
            return CanAdd
                ? "[새 문구]로 등록하세요. 문구 없이도 조치보고는 직접 입력으로 쓸 수 있습니다."
                : "문구 없이도 조치보고는 직접 입력으로 쓸 수 있습니다.";
        }
    }

    private ActionReportTemplateItem? _selectedItem;
    public ActionReportTemplateItem? SelectedItem
    {
        get => _selectedItem;
        set { _selectedItem = value; NotifyOfPropertyChange(); OnSelectionChanged(); }
    }

    /// <summary>줄을 골랐다 — 미적용 변경이 있으면 막는다(뷰가 그리드 선택을 되돌린다).</summary>
    public bool OnRowSelected(object? row)
    {
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.SelectRow)) return false;
        SelectedItem = row as ActionReportTemplateItem;
        return true;
    }

    private void OnSelectionChanged()
    {
        // 앞 줄에서 한 일("등록을 취소했습니다." 등)을 다른 줄의 바닥 막대에 남겨 두지 않는다.
        // (등록 · 수정 뒤에는 SelectById 다음에 Settle 이 오므로 그 알림은 지워지지 않는다.)
        _rejectedDraft = null;
        Detail.LastMessage = null;
        Detail.IsCreating = false;
        Detail.SelectedCount = SelectedItem is null ? 0 : 1;
        Detail.SingleTitle = SelectedItem?.Content ?? string.Empty;
        Detail.SingleNumber = SelectedItem is null ? string.Empty : $"#{SelectedItem.Id}";
        SetDraftContentQuiet(SelectedItem?.Content ?? string.Empty);
        RaiseAll();
    }

    public void SelectById(int id) => SelectedItem = Items.FirstOrDefault(i => i.Id == id);

    private void OnBoardChanged(object? sender, EventArgs e)
    {
        RailEntries[0].Count = Board.Count;
        NotifyOfPropertyChange(nameof(Items));
        NotifyOfPropertyChange(nameof(CountText));
        NotifyOfPropertyChange(nameof(IsEmpty));
        NotifyOfPropertyChange(nameof(EmptyStateText));
    }
    #endregion

    #region - Undo(재정렬) -
    private IReadOnlyList<int>? _undoOrder;
    public bool CanUndoReorder => _undoOrder != null && CanEdit && !IsReordering;
    public const string UndoReorderText = "방금 바꾼 순서 되돌리기";

    public async Task UndoReorderAsync()
    {
        var previous = _undoOrder;
        if (previous is null || IsReordering) return;
        _undoOrder = null;
        NotifyOfPropertyChange(nameof(CanUndoReorder));
        await CommitReorderAsync(ActionReportTemplateBoard.ToReorderPayload(previous), isUndo: true);
    }

    private async void OnReordered(object? sender, ActionReportTemplateReorderedEventArgs e)
        => await CommitReorderAsync(Board.ToReorderPayload(), isUndo: false, undoTarget: e.PreviousOrderIds);

    /// <summary>재정렬 커밋 — <b>단일</b> <c>ReorderTemplatesAsync</c> 호출. 실패하면 서버를 다시 불러온다(재조회).</summary>
    private async Task CommitReorderAsync(List<ActionReportTemplateReorderItemDto> payload, bool isUndo, IReadOnlyList<int>? undoTarget = null)
    {
        if (IsReordering) return;   // 겹친 커밋 방지
        IsReordering = true;
        RaiseAll();
        try
        {
            var res = await _api.ReorderTemplatesAsync(payload);
            if (res.Success && res.Data != null)
            {
                Board.ApplyServerOrder(res.Data);
                _lastCommittedOrder = Board.OrderedIds;   // 곧 올 SYNC 알림이 "우리 것의 메아리" 인지 가르는 기준
                StatusText = isUndo ? "순서를 되돌렸습니다." : "순서를 바꿨습니다.";
                _undoOrder = isUndo ? null : undoTarget;
            }
            else
            {
                _log?.Warning($"[ActionReportTemplate] Reorder 실패: {res.ErrorText()}");
                StatusText = "순서를 바꾸지 못해 목록을 다시 불러왔습니다. 잠시 후 다시 시도하세요.";
                _undoOrder = null;
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"[ActionReportTemplate] Reorder: {ex.Message}");
            StatusText = "순서를 바꾸지 못해 목록을 다시 불러왔습니다. 잠시 후 다시 시도하세요.";
            _undoOrder = null;
            await LoadAsync();
        }
        finally
        {
            IsReordering = false;
            RaiseAll();
        }
    }
    #endregion

    #region - Toolbar -
    public bool CanAdd => CanEdit;
    public string AddBlockedReason => CanEdit ? string.Empty : IsUnsupported ? UnsupportedText : "문구 편집 권한이 없습니다.";

    public bool CanDelete => CanDeletePermission && SelectedItem != null;
    public string DeleteBlockedReason => IsUnsupported ? UnsupportedText
        : !CanDeletePermission ? "지울 권한이 없습니다." : "지울 문구를 먼저 고르세요.";

    public bool CanReload => !IsBusy;

    public async Task AddAsync()
    {
        if (!CanEdit) return;
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.BeginCreate)) return;
        _selectedItem = null;
        NotifyOfPropertyChange(nameof(SelectedItem));
        _rejectedDraft = null;
        Detail.LastMessage = null;       // 앞 폼의 실패 문구를 새 등록 폼에 남기지 않는다
        Detail.SelectedCount = 0;
        Detail.IsCreating = true;
        SetDraftContentQuiet(string.Empty);
        RaiseAll();
        await Task.CompletedTask;
    }

    public async Task ReloadAsync()
    {
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.Refresh)) return;
        await LoadAsync();
    }

    public async Task DeleteAsync()
    {
        if (!CanDelete) return;
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.SelectRow)) return;
        var item = SelectedItem;
        if (item is null) return;

        _pendingDeleteItem = item;
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Explain = $"'{item.Content}' 문구를 삭제하시겠습니까?\n이미 기록된 조치보고에는 영향이 없습니다.",
            MessageModel = new CallDeleteActionReportTemplateProcessMessageModel(),
        });
    }

    /// <summary>삭제 확인됨 → DELETE → 결과 안내(선례: <c>ReportTemplateViewModel.HandleAsync</c>).</summary>
    public async Task HandleAsync(CallDeleteActionReportTemplateProcessMessageModel message, CancellationToken cancellationToken)
    {
        var item = _pendingDeleteItem ?? SelectedItem;
        _pendingDeleteItem = null;
        if (item is null) return;

        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel(), cancellationToken);
        OpenInfoPopupMessageModel result;
        try
        {
            var res = await _api.DeleteTemplateAsync(item.Id, cancellationToken);
            if (res.Success)
            {
                if (ReferenceEquals(SelectedItem, item)) { _selectedItem = null; Detail.Reset(); }
                await LoadAsync();
                StatusText = "문구를 삭제했습니다.";
            }
            else _log?.Warning($"[ActionReportTemplate] 삭제 실패({res.StatusCode}): {res.ErrorText()}");
            result = res.Success
                ? new OpenInfoPopupMessageModel { Title = "삭제 완료", Explain = "문구를 삭제했습니다." }
                : new OpenInfoPopupMessageModel { Title = "삭제 실패", Explain = MapError(res.StatusCode, "문구를 삭제하지 못했습니다.") };
        }
        catch (Exception ex)
        {
            _log?.Error($"[ActionReportTemplate] Delete: {ex.Message}");
            result = new OpenInfoPopupMessageModel { Title = "삭제 실패", Explain = "삭제 중 오류가 발생했습니다." };
        }
        await _eventAggregator.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
        await _eventAggregator.PublishOnCurrentThreadAsync(result, cancellationToken);
        RaiseAll();
    }
    #endregion

    #region - Detail(여섯 상태 · 고정 막대) · 편집 폼 -
    public ConsoleDetailPresenter Detail { get; }

    private bool _isLoadingDraft;
    private string _draftContent = string.Empty;

    /// <summary>실제 제출 대상 — 목록 선택은 이 칸에 <b>문자열을 복사</b>할 뿐이고, 고치면 그 값이 남는다.</summary>
    public string DraftContent
    {
        get => _draftContent;
        set
        {
            if (_draftContent == value) return;
            var wasCreate = Detail.IsCreating;
            var original = wasCreate ? null : (object?)(SelectedItem?.Content ?? string.Empty);
            _draftContent = value ?? string.Empty;
            if (_rejectedDraft != null)
            {
                // 폼이 더는 거절된 글이 아니다 — 그 실패 문구를 다른 글 옆에 남기지 않는다.
                _rejectedDraft = null;
                Detail.LastMessage = null;
            }
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(DraftContentCountText));
            NotifyOfPropertyChange(nameof(DraftValidationError));
            NotifyOfPropertyChange(nameof(IsContentTouched));
            if (!_isLoadingDraft)
            {
                _showValidation = true;   // 사람이 한 글자라도 손댄 뒤부터 검증 문구를 보인다(A6)
                Detail.Tracker.Touch(FieldContent, original, _draftContent, hasOriginal: !wasCreate);
            }
            NotifyOfPropertyChange(nameof(DraftValidationMessage));
        }
    }

    private void SetDraftContentQuiet(string value)
    {
        _isLoadingDraft = true;
        try
        {
            _showValidation = false;      // 새로 연 폼은 아직 아무것도 안 적었다 — 빨간 글로 맞이하지 않는다
            DraftContent = value;
            NotifyOfPropertyChange(nameof(DraftValidationMessage));
        }
        finally { _isLoadingDraft = false; }
    }

    /// <summary>
    /// 화면에 보일 검증 문구 — 첫 입력 또는 [등록]/[적용] 시도 <b>뒤에만</b> 보인다(A6: 새 문구 폼을 열자마자
    /// "문구를 입력하세요." 가 빨갛게 떴다). [등록] 단추는 그 전에도 <see cref="DetailCanApply"/> 로 조용히 꺼져 있다.
    /// </summary>
    public string? DraftValidationMessage => _showValidation ? DraftValidationError : null;

    private bool _showValidation;

    public string DraftContentCountText => $"{(DraftContent ?? string.Empty).Trim().Length} / {MaxContentLength}자";

    /// <summary>클라 선제 검증 — Trim 후 1~500자 · 목록 내 중복(자기 자신은 제외). 서버는 trim 하지 않으므로
    /// 공백만인 문구가 등록되는 결함(E1)을 여기서 막는다.</summary>
    public string? DraftValidationError
    {
        get
        {
            var trimmed = (DraftContent ?? string.Empty).Trim();
            if (trimmed.Length == 0) return "문구를 입력하세요.";
            if (trimmed.Length > MaxContentLength) return $"최대 {MaxContentLength}자까지 입력할 수 있습니다. (현재 {trimmed.Length}자)";
            var self = Detail.IsCreating ? null : SelectedItem;
            var dup = Items.Any(i => !ReferenceEquals(i, self) && string.Equals(i.Content.Trim(), trimmed, StringComparison.Ordinal));
            return dup ? "이미 등록된 문구입니다." : null;
        }
    }

    public bool IsContentTouched => Detail.Tracker.IsTouched(FieldContent);

    /// <summary>
    /// V-32 — 고른 문구도, 새로 적는 문구도 없다. 빈 문구 칸 · "0 / 500자" 대신 커널 빈 상태를 보인다
    /// (빈 칸은 무엇을 적으라는 것인지 모르는 채 "새 문구" 처럼 읽혔다).
    /// </summary>
    public bool IsDetailEmpty => Detail.SelectedCount == 0 && !Detail.IsCreating;

    /// <summary>문구 폼을 보일 때 — 빈 상태의 반대.</summary>
    public bool IsDetailFormVisible => !IsDetailEmpty;

    public const string DetailEmptyTitle = "문구를 고르세요";

    /// <summary>빈 상세의 둘째 줄 — 쓸 수 없으면(권한 · 서버) "등록하라" 고 하지 않는다.</summary>
    public string DetailEmptyHint => CanEdit
        ? "목록에서 문구를 고르면 여기서 고칩니다. 새로 등록하려면 [새 문구]를 누르세요."
        : "목록에서 문구를 고르면 내용을 여기서 볼 수 있습니다.";

    /// <summary>커널의 [적용] — 미적용 변경이 있어도 <b>클라 검증을 통과해야</b> 켜진다.</summary>
    public bool DetailCanApply => Detail.CanApply && DraftValidationError == null;

    /// <summary>[적용] · [등록].</summary>
    public async Task ApplyAsync()
    {
        if (!CanEdit) { FailApply(IsUnsupported ? UnsupportedText : "편집 권한이 없습니다."); RaiseAll(); return; }
        if (DraftValidationError != null) { _showValidation = true; FailApply(DraftValidationError); RaiseAll(); return; }
        if (IsBusy) return;

        var content = DraftContent.Trim();
        IsBusy = true;
        try
        {
            if (Detail.IsCreating)
            {
                var nextOrder = Items.Count == 0 ? 0 : Items.Max(i => i.DisplayOrder) + 1;
                var res = await _api.CreateTemplateAsync(new ActionReportTemplateCreateDto { Content = content, DisplayOrder = nextOrder });
                if (!res.Success || res.Data is null)
                {
                    _log?.Warning($"[ActionReportTemplate] 등록 실패({res.StatusCode}): {res.ErrorText()}");
                    FailApply(MapError(res.StatusCode, "문구를 등록하지 못했습니다."));
                    return;
                }
                IsBusy = false;   // LoadAsync 가 자기 재진입 가드로 스스로 막지 않도록 먼저 내린다.
                await LoadAsync();
                SelectById(res.Data.Id);
                Detail.IsCreating = false;
                _rejectedDraft = null;
                Detail.Settle("문구를 등록했습니다.");
            }
            else
            {
                var item = SelectedItem;
                if (item is null) return;
                var res = await _api.UpdateTemplateAsync(item.Id, new ActionReportTemplateUpdateDto { Content = content });
                if (!res.Success || res.Data is null)
                {
                    _log?.Warning($"[ActionReportTemplate] 수정 실패({res.StatusCode}): {res.ErrorText()}");
                    FailApply(MapError(res.StatusCode, "문구를 고치지 못했습니다."));
                    return;
                }
                IsBusy = false;
                await LoadAsync();
                SelectById(item.Id);
                _rejectedDraft = null;
                Detail.Settle("적용했습니다.");
            }
        }
        finally { IsBusy = false; RaiseAll(); }
    }

    /// <summary>
    /// [적용] · [등록] 이 거절됐다 — 실패 문구만 남기고 손댄 칸은 <b>비우지 않는다</b>(거절된 글은 여전히 적용되지 않은 글이다).
    /// </summary>
    /// <remarks>
    /// 전에는 <see cref="ConsoleDetailPresenter.Settle"/>(오류)를 불러 손댄 칸을 비웠다 — 거절된 글이 폼에 그대로인데
    /// "미적용 없음"이 되어, 뒤이은 SYNC 다시 읽기가 그 글을 말없이 서버 글로 덮고 실패 문구를 실패하지 않은 글 옆에
    /// 되살렸다(적대 검토 시나리오 B). Settle 의 뜻(끝났다 = 칸을 비운다)은 콘솔 공통이라 커널은 그대로 두고 여기서 가른다
    /// — 이벤트 콘솔의 "고친 칸은 그대로 두었으니"(EventDashboardViewModel.SettlePendingApply)와 같은 길.
    /// 미적용 · 등록 상태의 커널 막대는 LastMessage 를 보이지 않으므로 <see cref="DetailFooterText"/> 가 거절된 글이
    /// 폼에 남아 있는 동안에만 이 문구를 보인다.
    /// </remarks>
    private void FailApply(string message)
    {
        _rejectedDraft = _draftContent;
        Detail.LastMessage = message;
    }

    /// <summary>방금 거절된 글 — 폼이 아직 그 글일 때만 non-null(글이 바뀌면 <see cref="DraftContent"/> 가 지운다).</summary>
    private string? _rejectedDraft;

    /// <summary>[되돌리기] · [취소].</summary>
    public void Revert()
    {
        _rejectedDraft = null;
        if (Detail.IsCreating)
        {
            Detail.IsCreating = false;
            SetDraftContentQuiet(string.Empty);
            Detail.Settle("등록을 취소했습니다.");
        }
        else
        {
            SetDraftContentQuiet(SelectedItem?.Content ?? string.Empty);
            Detail.Settle("되돌렸습니다.");
        }
        RaiseAll();
    }

    private void OnDetailChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        NotifyOfPropertyChange(nameof(DetailCanApply));
        NotifyOfPropertyChange(nameof(DetailFooterText));
        NotifyOfPropertyChange(nameof(IsDetailEmpty));
        NotifyOfPropertyChange(nameof(IsDetailFormVisible));
    }

    /// <summary>
    /// 바닥 막대 글. 등록 폼의 커널 기본 글("등록 전에는 목록에 나타나지 않습니다")은 360 서랍의 막대(단추 둘 옆)에서
    /// "않습 / 니다" 로 갈렸다 — 같은 뜻을 한 줄 길이로 말한다. 나머지 상태는 커널 글 그대로.
    /// </summary>
    public string DetailFooterText
    {
        get
        {
            // 거절된 글이 폼에 그대로 있다 — 실패 문구를 그 글 옆에 보인다(미적용 · 등록 상태의 커널 막대는 LastMessage 를 숨긴다).
            // "적용하거나 되돌린 뒤 이동하세요"(막힌 이동) · 읽기 전용은 커널 글이 이긴다.
            if (_rejectedDraft != null && Detail.LastMessage != null
                && Detail.State != ConsoleDetailState.ReadOnly
                && Detail.FooterText != ConsoleDetailStateMachine.BlockedNotice)
                return Detail.LastMessage;

            return Detail.State == ConsoleDetailState.Create
                   && Detail.FooterText == ConsoleDetailStateMachine.FooterText(ConsoleDetailState.Create, 0)
                ? CreateFooterText
                : Detail.FooterText;
        }
    }

    public const string CreateFooterText = "등록해야 목록에 나타납니다";
    #endregion

    #region - 다른 곳에서 바뀐 목록 (SYNC_ACTION_REPORT_TEMPLATE) -
    /// <summary>다른 곳에서 문구 목록이 바뀌었는데 지금 다시 읽지 못할 때 상태 띠에 보이는 말.</summary>
    public const string ExternalChangeNotice = "다른 곳에서 문구 목록이 바뀌었습니다.";
    public const string ExternalChangeAction = "다시 읽기";

    /// <summary>
    /// 다시 읽었더니 고르고 있던 문구의 글이 <b>다른 곳에서</b> 바뀌어 있었다 — 상세 바닥 막대에 보이는 말.
    /// 그 전의 "방금 한 일"(예: "적용했습니다.")은 우리가 쓰지 않은 글 옆에 남기지 않는다.
    /// </summary>
    public const string SelectedChangedElsewhereText = "다른 곳에서 이 문구가 바뀌었습니다.";

    public string ExternalChangeText => ExternalChangeNotice;
    public string ExternalChangeActionText => ExternalChangeAction;

    private bool _isExternallyChanged;
    /// <summary>
    /// 다른 곳에서 목록이 바뀌었는데 <b>다시 읽지 않았다</b> — 적용하지 않은 문구 · 끌기 · 순서 저장 중이라서.
    /// 상태 띠에 <see cref="ExternalChangeText"/> 와 [<see cref="ExternalChangeAction"/>] 단추가 뜬다.
    /// </summary>
    public bool IsExternallyChanged
    {
        get => _isExternallyChanged;
        private set { if (_isExternallyChanged == value) return; _isExternallyChanged = value; NotifyOfPropertyChange(); }
    }

    /// <summary>창 지연(시험이 바꿔 끼운다). null 이면 실제 시간.</summary>
    internal Func<TimeSpan, CancellationToken, Task>? ExternalChangeDelay { get; set; }

    /// <summary>지금 캡처 드래그 중인가(시험이 바꿔 끼운다).</summary>
    internal Func<bool> IsDragging { get; set; } = () => DragSession.IsActive;

    /// <summary>가장 최근 신호의 작업 — 시험이 창이 끝나기를 기다릴 때 쓴다.</summary>
    internal Task ExternalChangeTask => _externalChangeTask;

    /// <summary>
    /// 서버 <c>SYNC_ACTION_REPORT_TEMPLATE</c> — 호스트가 옮겨 온다(같은 봉투는 한 번만). 신호만 세고 곧바로 돌아간다.
    /// 창이 떠 있을 때만 듣는다(기반 클래스가 활성화 동안만 구독한다).
    /// </summary>
    public Task HandleAsync(ActionReportTemplatesChangedMessage message, CancellationToken cancellationToken)
    {
        if (!IsActive) return Task.CompletedTask;
        _externalChangeTask = _externalChange.Pulse();
        return Task.CompletedTask;
    }

    private async Task OnExternalChangeSettledAsync(CancellationToken token)
    {
        if (!IsActive) return;

        // 끌기 · 순서 저장 · 적재 중이면 잠깐 미룬다 — 곧 끝나고, 끝나면 다음 창에서 알아서 다시 읽는다.
        if (IsDragging() || IsReordering || IsBusy)
        {
            IsExternallyChanged = true;
            _externalChangeTask = _externalChange.Pulse();
            return;
        }

        // 사람이 적고 있는 문구는 덮지 않는다 — 알리고 사람이 [다시 읽기]로 고른다.
        if (Detail.IsDirty)
        {
            IsExternallyChanged = true;
            return;
        }

        var committed = _lastCommittedOrder;
        // 다시 읽기는 같은 줄을 다시 고른다(SelectById → OnSelectionChanged) — 그 길이 바닥 막대의 "방금 한 일"을 지운다.
        // 우리 쓰기의 메아리(등록 · 적용 직후 곧바로 온다)면 "문구를 등록했습니다." 가 "변경 없음" 으로 바뀌었다(헤디드 3회차 SC-ART-005c).
        // 같은 줄로 돌아왔고 그 줄의 글이 다시 읽기 전과 같을 때만 그 알림은 여전히 참이다 — 되살린다.
        // 글이 바뀌었으면(다른 운영자가 고쳤다 — 적대 검토 M1) 폼에는 그 사람의 글이 실렸다: "적용했습니다." 를 그 옆에
        // 되살리면 우리가 쓰지 않은 글을 우리가 적용한 것처럼 말한다 — 알림은 버리고 바뀌었다고만 말한다.
        // (순서 되돌리기가 _lastCommittedOrder 로 "우리 것의 메아리"를 가르는 것과 같은 기준: 우리가 마지막으로 본 값.)
        // 줄이 사라졌으면 남기지 않는다.
        var keptId = SelectedItem?.Id;
        var keptContent = SelectedItem?.Content;
        var keptMessage = Detail.LastMessage;
        await LoadAsync();
        if (keptId.HasValue && SelectedItem is { } again && again.Id == keptId
            && !Detail.IsDirty && !Detail.IsCreating && Detail.LastMessage is null)
        {
            if (string.Equals(again.Content, keptContent, StringComparison.Ordinal))
            {
                if (keptMessage != null) Detail.LastMessage = keptMessage;
            }
            else
            {
                Detail.LastMessage = SelectedChangedElsewhereText;
            }
        }

        // 되돌리기는 "우리가 보낸 순서"가 아직 서버 순서일 때만 산다 — 다른 사람이 또 바꿨다면
        // 되돌리기가 그 사람의 순서를 말없이 덮어쓴다. (우리 reorder 의 메아리면 순서가 같아 그대로 둔다.)
        if (_undoOrder != null && (committed is null || !Board.OrderedIds.SequenceEqual(committed)))
        {
            _undoOrder = null;
            NotifyOfPropertyChange(nameof(CanUndoReorder));
        }
    }

    private readonly CoalescingTrigger _externalChange;
    private Task _externalChangeTask = Task.CompletedTask;
    private IReadOnlyList<int>? _lastCommittedOrder;
    #endregion

    #region - Status bar -
    private string _statusText = string.Empty;
    public string StatusText { get => _statusText; set { _statusText = value ?? string.Empty; NotifyOfPropertyChange(); } }
    #endregion

    #region - Processes -
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        if (!CanViewPermission)
        {
            Board.Load(Enumerable.Empty<ActionReportTemplateDto>());
            _selectedItem = null;
            LoadError = "문구를 볼 권한이 없습니다.";
            RaiseAll();
            return;
        }

        IsBusy = true;
        try
        {
            var keepId = SelectedItem?.Id;
            var res = await _api.GetTemplatesAsync();
            if (res.Success && res.Data != null)
            {
                Board.Load(res.Data);
                LoadError = null;
                IsExternallyChanged = false;      // 방금 서버의 지금 목록을 받았다
            }
            else
            {
                // G1 ⓑ — 빈 목록 + 안내. 하드코딩 5종 폴백을 쓰지 않는다(서버에서 지운/수정한 문구가
                // 되살아나 저장될 수 있다 — scenario-analysis §3 G1).
                Board.Load(Enumerable.Empty<ActionReportTemplateDto>());
                _log?.Warning($"[ActionReportTemplate] 조회 실패: {res.ErrorText()}");
                // 구 서버(운영 6.3.2)는 이 라우터가 없다 — 목록 404 를 서비스가 NOT_SUPPORTED 로 번역한다.
                // "불러오지 못했다"(일시 장애)로 뭉개면 [추가] 가 켜진 채 남아 누를 때마다 404 가 된다.
                LoadError = IsUnsupported ? UnsupportedText : LoadFailedText;
            }
            if (keepId.HasValue) SelectById(keepId.Value);
        }
        catch (Exception ex)
        {
            _log?.Error($"[ActionReportTemplate] Load: {ex.Message}");
            Board.Load(Enumerable.Empty<ActionReportTemplateDto>());
            LoadError = LoadFailedText;
        }
        finally
        {
            IsBusy = false;
            Detail.IsReadOnly = !CanEdit;   // 지원 여부는 조회로만 안다 — 적재가 끝날 때마다 쓰기 상태를 다시 맞춘다
            RaiseAll();
        }
    }

    /// <summary>서버 detail(영문) → 화면 문구(와이어프레임 §6 표).</summary>
    private string MapError(int statusCode, string fallback)
    {
        // 서버가 이 기능을 모르는데 "다른 곳에서 삭제된 문구" 라고 말하면 거짓이다.
        if (IsUnsupported) return UnsupportedText;
        return statusCode switch
        {
            409 => "이미 등록된 문구입니다.",
            404 => "다른 곳에서 삭제된 문구입니다. 목록을 다시 불러옵니다.",
            422 => "문구는 1~500자여야 합니다.",
            403 => "문구 편집 권한이 없습니다.",
            // 서버 원문은 화면에 붙이지 않는다(호출부가 로그에 남긴다) — 무엇이 안 됐고 어떻게 하면 되는지만.
            _ => $"{fallback} 잠시 후 다시 시도하세요.",
        };
    }

    /// <summary>
    /// 서버가 조치보고 문구 API 를 제공하지 않는다(<see cref="IActionReportTemplateApiService.IsSupported"/> == false —
    /// 목록 GET 404, 운영 6.3.2). 그때는 쓰기를 전부 끄고 이 글을 보인다.
    /// <c>null</c>(아직 모름)은 막지 않는다 — 판정의 권위는 실제 404 다.
    /// </summary>
    public bool IsUnsupported => _api.IsSupported == false;

    /// <summary>A8 — 판본 · 업그레이드 같은 공급사 쪽 사정은 말하지 않는다. 운영자가 할 수 있는 일만.</summary>
    public const string UnsupportedText = "이 서버에서는 조치보고 문구 관리를 사용할 수 없습니다. 관리자에게 문의하세요.";

    public const string LoadFailedText = "문구 목록을 불러오지 못했습니다.";
    #endregion

    #region - Permissions -
    private void OnPermissionsChanged() => RefreshPermissions();

    private void RefreshPermissions()
    {
        Detail.IsReadOnly = !CanEdit;
        RaiseAll();
    }

    /// <summary>
    /// 쓰기(등록 · 수정 · 순서) 가능 — 권한 <b>그리고</b> 서버 지원. 끌기 · ▲▼ · [추가] · [적용] 이 전부 이것 하나를 본다.
    /// </summary>
    public bool CanEdit => !IsUnsupported && PermissionUiPolicy.Allowed(_permission, PermissionModuleKey, EnumPermissionVerb.Edit);
    /// <summary>삭제 가능 — 권한 <b>그리고</b> 서버 지원.</summary>
    public bool CanDeletePermission => !IsUnsupported && PermissionUiPolicy.Allowed(_permission, PermissionModuleKey, EnumPermissionVerb.Delete);
    public bool CanViewPermission => PermissionUiPolicy.Allowed(_permission, ReadPermissionModuleKey, EnumPermissionVerb.View);
    #endregion

    #region - Notifications -
    private void RaiseAll()
    {
        NotifyOfPropertyChange(nameof(Items));
        NotifyOfPropertyChange(nameof(CountText));
        NotifyOfPropertyChange(nameof(IsEmpty));
        NotifyOfPropertyChange(nameof(EmptyStateText));
        NotifyOfPropertyChange(nameof(CanAdd));
        NotifyOfPropertyChange(nameof(AddBlockedReason));
        NotifyOfPropertyChange(nameof(CanDelete));
        NotifyOfPropertyChange(nameof(DeleteBlockedReason));
        NotifyOfPropertyChange(nameof(CanReload));
        NotifyOfPropertyChange(nameof(CanEdit));
        NotifyOfPropertyChange(nameof(CanDeletePermission));
        NotifyOfPropertyChange(nameof(CanViewPermission));
        NotifyOfPropertyChange(nameof(IsUnsupported));
        NotifyOfPropertyChange(nameof(CanUndoReorder));
        NotifyOfPropertyChange(nameof(DetailCanApply));
        NotifyOfPropertyChange(nameof(IsContentTouched));
        NotifyOfPropertyChange(nameof(DraftContentCountText));
        NotifyOfPropertyChange(nameof(DraftValidationError));
        NotifyOfPropertyChange(nameof(StatusText));
        NotifyOfPropertyChange(nameof(DraftValidationMessage));
        NotifyOfPropertyChange(nameof(EmptyStateHint));
        NotifyOfPropertyChange(nameof(CanMoveSelected));
        NotifyOfPropertyChange(nameof(MoveBlockedReason));
        NotifyOfPropertyChange(nameof(MoveUpToolTip));
        NotifyOfPropertyChange(nameof(MoveDownToolTip));
        NotifyOfPropertyChange(nameof(RowEditText));
        NotifyOfPropertyChange(nameof(RowDeleteToolTip));
        NotifyOfPropertyChange(nameof(DetailFooterText));
        NotifyOfPropertyChange(nameof(IsDetailEmpty));
        NotifyOfPropertyChange(nameof(IsDetailFormVisible));
        NotifyOfPropertyChange(nameof(DetailEmptyHint));
    }
    #endregion

    #region - Attributes -
    public Task Close() => TryCloseAsync();

    /// <summary>
    /// 적용하지 않은 문구가 있으면 닫지 않는다 — 바닥 막대가 흔들리며 "적용하거나 되돌린 뒤 이동하세요" 라고 말한다
    /// (저장 안 한 변경을 말없이 버리지 않는다. 선례: 이벤트 매핑 워크벤치). 읽기 전용이면 붙잡지 않는다 —
    /// 저장할 길이 없는데 닫기까지 막으면 창을 닫을 방법이 사라진다.
    /// </summary>
    public override Task<bool> CanCloseAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(!Detail.IsDirty || Detail.IsReadOnly || Detail.Guard.TryNavigate(ConsoleNavigation.SelectRow));
    public string PanelTitle => "조치보고 문구";

    private readonly IPermissionService _permission;
    private readonly IActionReportTemplateApiService _api;
    private ActionReportTemplateItem? _pendingDeleteItem;
    #endregion
}
