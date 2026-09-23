using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.ActionReportTemplates;
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
public class ActionReportTemplateConsoleViewModel : BasePanelViewModel, IHandle<CallDeleteActionReportTemplateProcessMessageModel>
{
    public const string ConsoleKey = "ActionReportTemplates";
    public const string RailKey = "templates";
    public const string PermissionModuleKey = "action_report_templates";
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

        Detail = new ConsoleDetailPresenter { TypeName = "문구" };
        Detail.Tracker.MarkIdentity(FieldContent);

        RailEntries = new ObservableCollection<ConsoleRailEntry>
        {
            new ConsoleRailEntry(RailKey, "문구 목록") { ShowCount = true },
        };
        _selectedRail = RailEntries[0];

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

    public string RailFooterText => "고르면 오른쪽에서 고칩니다. 끌어서 순서를 바꿀 수 있습니다.";
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
    public string EmptyStateText => LoadError ?? "등록된 문구가 없습니다. 직접 입력으로 조치보고를 계속할 수 있습니다.";

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
                StatusText = isUndo ? "순서를 되돌렸습니다." : "순서를 바꿨습니다.";
                _undoOrder = isUndo ? null : undoTarget;
            }
            else
            {
                _log?.Warning($"[ActionReportTemplate] Reorder 실패: {res.ErrorText()}");
                StatusText = "순서 변경 중 오류가 발생했습니다. 서버 순서를 다시 불러옵니다.";
                _undoOrder = null;
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"[ActionReportTemplate] Reorder: {ex.Message}");
            StatusText = "순서 변경 중 오류가 발생했습니다. 서버 순서를 다시 불러옵니다.";
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
    public string AddBlockedReason => CanEdit ? string.Empty : "문구 편집 권한이 없습니다.";

    public bool CanDelete => CanDeletePermission && SelectedItem != null;
    public string DeleteBlockedReason => !CanDeletePermission ? "지울 권한이 없습니다." : "지울 문구를 먼저 고르세요.";

    public bool CanReload => !IsBusy;

    public async Task AddAsync()
    {
        if (!CanEdit) return;
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.BeginCreate)) return;
        _selectedItem = null;
        NotifyOfPropertyChange(nameof(SelectedItem));
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
            result = res.Success
                ? new OpenInfoPopupMessageModel { Title = "삭제 완료", Explain = "문구를 삭제했습니다." }
                : new OpenInfoPopupMessageModel { Title = "삭제 실패", Explain = MapError(res.StatusCode, res.ErrorText("삭제하지 못했습니다.")) };
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
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(DraftContentCountText));
            NotifyOfPropertyChange(nameof(DraftValidationError));
            NotifyOfPropertyChange(nameof(IsContentTouched));
            if (!_isLoadingDraft) Detail.Tracker.Touch(FieldContent, original, _draftContent, hasOriginal: !wasCreate);
        }
    }

    private void SetDraftContentQuiet(string value)
    {
        _isLoadingDraft = true;
        try { DraftContent = value; }
        finally { _isLoadingDraft = false; }
    }

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

    /// <summary>커널의 [적용] — 미적용 변경이 있어도 <b>클라 검증을 통과해야</b> 켜진다.</summary>
    public bool DetailCanApply => Detail.CanApply && DraftValidationError == null;

    /// <summary>[적용] · [등록].</summary>
    public async Task ApplyAsync()
    {
        if (!CanEdit) { Detail.Settle("편집 권한이 없습니다."); RaiseAll(); return; }
        if (DraftValidationError != null) { Detail.Settle(DraftValidationError); RaiseAll(); return; }
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
                    Detail.Settle(MapError(res.StatusCode, res.ErrorText("등록하지 못했습니다.")));
                    return;
                }
                IsBusy = false;   // LoadAsync 가 자기 재진입 가드로 스스로 막지 않도록 먼저 내린다.
                await LoadAsync();
                SelectById(res.Data.Id);
                Detail.IsCreating = false;
                Detail.Settle("문구를 등록했습니다.");
            }
            else
            {
                var item = SelectedItem;
                if (item is null) return;
                var res = await _api.UpdateTemplateAsync(item.Id, new ActionReportTemplateUpdateDto { Content = content });
                if (!res.Success || res.Data is null)
                {
                    Detail.Settle(MapError(res.StatusCode, res.ErrorText("적용하지 못했습니다.")));
                    return;
                }
                IsBusy = false;
                await LoadAsync();
                SelectById(item.Id);
                Detail.Settle("적용했습니다.");
            }
        }
        finally { IsBusy = false; RaiseAll(); }
    }

    /// <summary>[되돌리기] · [취소].</summary>
    public void Revert()
    {
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
        => NotifyOfPropertyChange(nameof(DetailCanApply));
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
            }
            else
            {
                // G1 ⓑ — 빈 목록 + 안내. 하드코딩 5종 폴백을 쓰지 않는다(서버에서 지운/수정한 문구가
                // 되살아나 저장될 수 있다 — scenario-analysis §3 G1).
                Board.Load(Enumerable.Empty<ActionReportTemplateDto>());
                _log?.Warning($"[ActionReportTemplate] 조회 실패: {res.ErrorText()}");
                LoadError = "문구 목록을 불러오지 못했습니다. 직접 입력으로 조치보고를 계속할 수 있습니다.";
            }
            if (keepId.HasValue) SelectById(keepId.Value);
        }
        catch (Exception ex)
        {
            _log?.Error($"[ActionReportTemplate] Load: {ex.Message}");
            Board.Load(Enumerable.Empty<ActionReportTemplateDto>());
            LoadError = "문구 목록을 불러오지 못했습니다. 직접 입력으로 조치보고를 계속할 수 있습니다.";
        }
        finally { IsBusy = false; RaiseAll(); }
    }

    /// <summary>서버 detail(영문) → 화면 문구(와이어프레임 §6 표).</summary>
    private static string MapError(int statusCode, string fallback) => statusCode switch
    {
        409 => "이미 등록된 문구입니다.",
        404 => "다른 곳에서 삭제된 문구입니다. 목록을 다시 불러옵니다.",
        422 => "문구는 1~500자여야 합니다.",
        403 => "문구 편집 권한이 없습니다.",
        _ => string.IsNullOrWhiteSpace(fallback) ? "요청을 처리하지 못했습니다." : fallback,
    };
    #endregion

    #region - Permissions -
    private void OnPermissionsChanged() => RefreshPermissions();

    private void RefreshPermissions()
    {
        Detail.IsReadOnly = !CanEdit;
        RaiseAll();
    }

    public bool CanEdit => PermissionUiPolicy.Allowed(_permission, PermissionModuleKey, EnumPermissionVerb.Edit);
    public bool CanDeletePermission => PermissionUiPolicy.Allowed(_permission, PermissionModuleKey, EnumPermissionVerb.Delete);
    public bool CanViewPermission => PermissionUiPolicy.Allowed(_permission, PermissionModuleKey, EnumPermissionVerb.View);
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
        NotifyOfPropertyChange(nameof(CanUndoReorder));
        NotifyOfPropertyChange(nameof(DetailCanApply));
        NotifyOfPropertyChange(nameof(IsContentTouched));
        NotifyOfPropertyChange(nameof(DraftContentCountText));
        NotifyOfPropertyChange(nameof(DraftValidationError));
        NotifyOfPropertyChange(nameof(StatusText));
    }
    #endregion

    #region - Attributes -
    public Task Close() => TryCloseAsync();
    public string PanelTitle => "조치보고 문구";

    private readonly IPermissionService _permission;
    private readonly IActionReportTemplateApiService _api;
    private ActionReportTemplateItem? _pendingDeleteItem;
    #endregion
}
