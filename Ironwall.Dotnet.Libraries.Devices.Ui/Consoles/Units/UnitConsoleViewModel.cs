using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;

/****************************************************************************
   Purpose      : 부대 콘솔 — 편제 트리 · 상세 · 미배치 장비 (N-11)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 부대 편제 콘솔. 정본 목업: <c>docs/design/event-mapping-unit-console-storyboard.html</c> 화면 G~K ·
/// <c>event-mapping-unit-console-wireframe.html</c> §2-2 · §3 · §4-3 · §7.
/// </summary>
/// <remarks>
/// <para><b>서버 호출 횟수가 드래그 허용의 기준</b>이다(드래그 와이어프레임 L432) —
/// 상위 바꾸기·인접은 호출 <b>1회</b>라 드롭 즉시 보내고 되돌리기를 준다,
/// 장비 배치는 장비 수만큼 <b>N회</b>라 Draft 트레이에 쌓고 [적용] 때 한꺼번에 보낸다.</para>
/// <para><b>형제 순서 드래그는 없다</b> — 서버 계약에 순서 필드가 아예 없어 끌어도 저장되지 않는다
/// (스토리보드 L359-361 · 드래그 와이어프레임 L446).</para>
/// </remarks>
public sealed class UnitConsoleViewModel : Screen
{
    public const string RAIL_TREE = "tree";
    public const string RAIL_ADJACENCY = "adjacency";
    public const string RAIL_DEVICES = "devices";

    #region - Ctors -
    public UnitConsoleViewModel(
        IUnitGraphApi units,
        IUnitDeviceApi devices,
        ILogService? log = null,
        Func<string?>? myUnitCode = null,
        Func<bool>? canEdit = null,
        Func<bool>? canDelete = null)
    {
        _units = units ?? throw new ArgumentNullException(nameof(units));
        _devices = devices ?? throw new ArgumentNullException(nameof(devices));
        _log = log;
        _myUnitCode = myUnitCode ?? (() => null);
        _canEdit = canEdit ?? DevicePermissionGate.CanEdit;
        _canDelete = canDelete ?? DevicePermissionGate.CanDelete;

        Detail = new ConsoleDetailPresenter { TypeName = "부대" };
        Form = new UnitDetailFormViewModel(Detail);
        Tray = new DraftTrayViewModel();
        Drop = new UnitDropHandler(() => Tree, () => SelectedRow?.Id ?? 0, () => _canEdit(), () => IsBusy, OnDropped);

        RailEntries.Add(new ConsoleRailEntry(RAIL_TREE, "편제 트리") { ShowCount = true });
        RailEntries.Add(new ConsoleRailEntry(RAIL_ADJACENCY, "인접 관계도") { ShowCount = true });
        RailEntries.Add(new ConsoleRailEntry(RAIL_DEVICES, "미배치 장비") { ShowCount = true });
        _selectedRail = RailEntries[0];

        EchelonFilters.Add(new UnitEchelonFilterViewModel(null, "전체") { IsSelected = true });
        foreach (var echelon in Enum.GetValues<EnumUnitEchelon>())
            EchelonFilters.Add(new UnitEchelonFilterViewModel(echelon, UnitDropRules.EchelonText(echelon)));

        Detail.Tracker.Changed += (_, _) => RaiseDetail();
        Tray.PropertyChanged += (_, _) => RaiseTray();
    }
    #endregion

    #region - Kernel pieces -
    public ConsoleDetailPresenter Detail { get; }
    public UnitDetailFormViewModel Form { get; }
    public DraftTrayViewModel Tray { get; }
    public UnitDropHandler Drop { get; }

    public BindableCollection<ConsoleRailEntry> RailEntries { get; } = new();
    public BindableCollection<UnitEchelonFilterViewModel> EchelonFilters { get; } = new();
    public BindableCollection<UnitNodeRowViewModel> Rows { get; } = new();
    public BindableCollection<UnitDeviceRowViewModel> DeviceRows { get; } = new();
    #endregion

    #region - View state -
    /// <summary>서버가 준 편제 전체. 화면에 보이는 행(<see cref="Rows"/>)은 이것의 접힘·필터 투영이다.</summary>
    public UnitTreeModel Tree { get; private set; } = UnitTreeModel.Empty;

    public ConsoleRailEntry SelectedRail
    {
        get => _selectedRail;
        set
        {
            if (ReferenceEquals(_selectedRail, value) || value is null) return;
            // 레일 전환은 미적용 변경을 버릴 수 있다 — 커널 관문을 먼저 지난다.
            if (!Detail.Guard.TryNavigate(ConsoleNavigation.SwitchRail)) { NotifyOfPropertyChange(); return; }
            _selectedRail = value;
            NotifyOfPropertyChange();
            RaiseViewFlags();
            Project();
        }
    }

    public bool IsTreeView => _selectedRail.Key == RAIL_TREE;
    public bool IsDeviceView => _selectedRail.Key == RAIL_DEVICES;

    /// <summary>인접 관계도는 <b>이 노드의 범위 밖</b>이다(봉투: "관계도는 뒤"). 자리만 두고 준비 중으로 보인다.</summary>
    public bool IsAdjacencyView => _selectedRail.Key == RAIL_ADJACENCY;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value ?? string.Empty;
            NotifyOfPropertyChange();
            Project();
        }
    }

    public UnitNodeRowViewModel? SelectedRow
    {
        get => _selectedRow;
        private set { _selectedRow = value; NotifyOfPropertyChange(); RaiseCommands(); }
    }

    /// <summary>미배치 목록에서 지금 고른 장비들 — 끌기의 버튼 폴백이 쓴다.</summary>
    public IReadOnlyList<UnitDeviceRowViewModel> SelectedDevices
    {
        get => _selectedDevices;
        private set { _selectedDevices = value; NotifyOfPropertyChange(); RaiseCommands(); }
    }

    public bool IsBusy { get => _isBusy; private set { _isBusy = value; NotifyOfPropertyChange(); RaiseCommands(); } }

    public string StatusText { get => _statusText; private set { _statusText = value; NotifyOfPropertyChange(); } }

    public string ListStatusText => IsDeviceView
        ? $"미배치 장비 {DeviceRows.Count}"
        : $"부대 {Tree.Count} · 인접 쌍 {AdjacencyPairCount}";

    public string RailFooterText => string.IsNullOrEmpty(MyUnitCode)
        ? "형제 순서는 서버에 저장할 자리가 없어 코드 순으로 고정합니다."
        : $"내 부대 · {MyUnitCode}";

    public string? MyUnitCode => _myUnitCode();

    public int AdjacencyPairCount { get; private set; }

    /// <summary>삭제가 409 로 막혔을 때 무엇이 매달려 있는지(스토리보드 화면 J).</summary>
    public UnitDeleteBlock? DeleteBlock { get => _deleteBlock; private set { _deleteBlock = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(IsDeleteBlocked)); } }
    public bool IsDeleteBlocked => _deleteBlock is { Items.Count: > 0 };

    /// <summary>방금 한 이동 — 되돌리기는 <b>마지막 1회</b>만(드래그 와이어프레임 L546).</summary>
    public bool CanUndoMove => _lastMove is not null && !IsBusy;
    #endregion

    #region - Commands state -
    public bool IsAvailable => _units.IsAvailable;
    public bool CanEditUnits => _canEdit();
    public bool CanAdd => IsAvailable && CanEditUnits && !IsBusy && !IsDeviceView;
    public string AddBlockedReason => !IsAvailable ? "이 서버 판본에는 부대 편제가 없습니다." : "부대를 등록할 권한이 없습니다(units:edit).";
    public bool CanDeleteUnit => IsAvailable && _canDelete() && SelectedRow is not null && !Form.IsCreating && !IsBusy;
    public string DeleteBlockedReason => SelectedRow is null ? "지울 부대를 먼저 고르세요." : "부대를 지울 권한이 없습니다(units:delete).";
    public bool CanReload => IsAvailable && !IsBusy;
    public bool CanMoveSelected => IsAvailable && CanEditUnits && SelectedRow is not null && !IsBusy;
    public bool CanAssignSelectedDevices => IsAvailable && CanEditUnits && SelectedDevices.Count > 0 && SelectedRow is not null && !IsBusy;
    public bool IsDetailRequested => Detail.IsDetailRequested;
    #endregion

    #region - Lifecycle -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        if (_loadedOnce) return;
        _loadedOnce = true;
        await ReloadAsync(cancellationToken);
    }

    /// <summary>편제 전체를 한 번에 읽는다 — <c>/graph</c> 는 페이지네이션이 없다(와이어프레임 L314).</summary>
    /// <param name="quiet">
    /// 쓰기 뒤의 재조회다 — 상태 띠의 <b>방금 한 일</b>을 "편제 N개를 읽었습니다" 로 덮지 않는다.
    /// 덮으면 이동 · 인접 · 배치의 결과(특히 실패 사유)가 한 순간에 사라진다.
    /// </param>
    public async Task ReloadAsync(CancellationToken token = default, bool quiet = false)
    {
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.Refresh)) return;
        if (!IsAvailable)
        {
            StatusText = "이 서버 판본에는 부대 편제가 없습니다 — 서버 8.0 이상에서만 보입니다.";
            return;
        }

        IsBusy = true;
        try
        {
            var response = await _units.GetGraphAsync(token).ConfigureAwait(true);
            if (!response.Success)
            {
                StatusText = $"편제를 읽지 못했습니다 — {Reason(response.Error?.Message, response.Message)}";
                return;
            }

            Tree = UnitTreeBuilder.Build(response.Data);
            AdjacencyPairCount = response.Data?.Edges?.AdjacencyPairs.Count() ?? 0;

            if (_devices.IsAvailable)
            {
                var loaded = await _devices.LoadAllAsync(token).ConfigureAwait(true);
                _allDevices = loaded.Items;
                if (loaded.HasFailure) _log?.Warning($"[UnitConsole] 장비 일부를 읽지 못했습니다 — {string.Join(" · ", loaded.Failures)}");
            }

            // 되돌리기는 재조회를 넘어 살아남는다 — 반대 방향 PATCH 한 번이라 화면을 다시 읽어도 여전히 유효하다.
            DeleteBlock = null;
            RestoreSelection();
            Project();
            if (!quiet) StatusText = $"편제 {Tree.Count}개를 읽었습니다.";
        }
        catch (OperationCanceledException) { StatusText = "읽기를 취소했습니다."; }
        catch (Exception ex)
        {
            _log?.Error($"[UnitConsole] reload: {ex.Message}");
            StatusText = "편제를 읽지 못했습니다 — 서버에 닿지 못했습니다.";
        }
        finally
        {
            IsBusy = false;
            RaiseCommands();
        }
    }
    #endregion

    #region - Projection -
    /// <summary>접힘 · 제대 칩 · 검색을 반영해 보이는 행을 다시 만든다. <b>한 번에 한 번만</b> 다시 만든다.</summary>
    public void Project()
    {
        var echelon = EchelonFilters.FirstOrDefault(f => f.IsSelected)?.Echelon;
        var needle = _searchText.Trim();

        var keep = new List<UnitNodeRowViewModel>();
        var collapsed = new HashSet<int>();

        foreach (var node in Tree.Ordered)
        {
            // 접힌 부모 밑은 통째로 건너뛴다.
            if (node.ParentId is int parentId && collapsed.Contains(parentId)) { collapsed.Add(node.Id); continue; }

            var row = RowOf(node);
            if (!row.IsExpanded) collapsed.Add(node.Id);

            if (echelon is EnumUnitEchelon wanted && node.Echelon != wanted) continue;
            if (needle.Length > 0
                && node.Name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0
                && node.Code.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0) continue;

            row.DeviceCount = _allDevices.Count(d => d.UnitId == node.Id);
            keep.Add(row);
        }

        Sync(Rows, keep);

        var unassigned = _allDevices.Where(IsUnassigned).ToList();
        var deviceRows = unassigned.Select(item => DeviceRowOf(item)).ToList();
        Sync(DeviceRows, deviceRows);

        RailEntries[0].Count = Tree.Count;
        RailEntries[1].Count = AdjacencyPairCount;
        RailEntries[2].Count = DeviceRows.Count;

        NotifyOfPropertyChange(nameof(ListStatusText));
        NotifyOfPropertyChange(nameof(RailFooterText));
    }

    /// <summary>
    /// 미배치의 정의 — <b>소속이 비었거나(<c>unit_id</c> 없음) 편제에 없는 부대를 가리키는</b> 장비.
    /// </summary>
    /// <remarks>
    /// ⚠ 서버에 "부대 없는 장비" 질의가 없다(8.0.1 필터축은 <c>unit_id</c>·<c>include_descendants</c> 뿐).
    /// 게다가 <c>unit_id</c> 를 생략하고 등록하면 서버가 <b>기본 부대로 자동 귀속</b>시키므로
    /// 실제 운영에서 이 목록은 대개 비어 있다 — 그것이 정상이고, 그때는 빈 상태 안내를 낸다.
    /// </remarks>
    private bool IsUnassigned(UnitDeviceItem item)
        => item.UnitId is not int unitId || unitId <= 0 || Tree.Find(unitId) is null;

    private UnitNodeRowViewModel RowOf(UnitTreeNode node)
    {
        if (_rowCache.TryGetValue(node.Id, out var existing) && ReferenceEquals(existing.Node, node)) return existing;

        var isMine = !string.IsNullOrEmpty(MyUnitCode) && string.Equals(node.Code, MyUnitCode, StringComparison.Ordinal);
        var row = new UnitNodeRowViewModel(node, isMine);
        if (existing is not null) row.IsExpanded = existing.IsExpanded;
        _rowCache[node.Id] = row;
        return row;
    }

    private UnitDeviceRowViewModel DeviceRowOf(UnitDeviceItem item)
    {
        var unitText = item.UnitId is int unitId && Tree.Find(unitId) is { } node ? node.Name : "소속 없음";
        if (_deviceRowCache.TryGetValue(item.Id, out var existing) && existing.Item == item) return existing;

        var row = new UnitDeviceRowViewModel(item, unitText);
        if (existing is not null) row.PendingUnitName = existing.PendingUnitName;
        _deviceRowCache[item.Id] = row;
        return row;
    }

    /// <summary>선택을 죽이지 않고 목록을 맞춘다 — <c>Clear()+Add()</c> 는 쓰지 않는다.</summary>
    private static void Sync<T>(BindableCollection<T> target, IReadOnlyList<T> wanted)
    {
        for (var i = 0; i < wanted.Count; i++)
        {
            var index = target.IndexOf(wanted[i]);
            if (index < 0) target.Insert(i, wanted[i]);
            else if (index != i) target.Move(index, i);
        }
        while (target.Count > wanted.Count) target.RemoveAt(target.Count - 1);
    }
    #endregion

    #region - Selection -
    public async Task SelectRowAsync(UnitNodeRowViewModel? row, CancellationToken token = default)
    {
        if (ReferenceEquals(row, SelectedRow)) return;
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.SelectRow)) return;

        SelectedRow = row;
        DeleteBlock = null;

        if (row is null)
        {
            Form.Clear();
            Detail.Reset();
            return;
        }

        Detail.IsCreating = false;
        Detail.SelectedCount = 1;
        Detail.SingleTitle = row.Name;
        Detail.SingleNumber = row.Code;
        Detail.IsReadOnly = !CanEditUnits;
        Detail.Tracker.Clear();

        await LoadDetailAsync(row, token).ConfigureAwait(true);
        RaiseDetail();
    }

    private async Task LoadDetailAsync(UnitNodeRowViewModel row, CancellationToken token)
    {
        var deviceCount = _allDevices.Count(d => d.UnitId == row.Id);

        if (!IsAvailable)
        {
            Form.Clear();
            return;
        }

        try
        {
            var response = await _units.GetDetailAsync(row.Id, token).ConfigureAwait(true);
            if (response.Success && response.Data is { } detail)
            {
                Form.Load(detail, Tree, deviceCount);
                Form.RefreshChildEchelonWarning(Tree);
                return;
            }
            StatusText = $"'{row.Name}' 상세를 읽지 못했습니다 — {Reason(response.Error?.Message, response.Message)}";
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Warning($"[UnitConsole] detail {row.Id}: {ex.Message}");
            StatusText = $"'{row.Name}' 상세를 읽지 못했습니다 — 서버에 닿지 못했습니다.";
        }

        // 상세를 못 받았어도 트리가 아는 만큼은 채운다 — 빈 칸으로 두면 편집이 원본 없이 시작된다.
        var fallback = new UnitDetailDto
        {
            Id = row.Id,
            Code = row.Code,
            Name = row.Name,
            EchelonRaw = row.Node.EchelonRaw,
            ParentId = row.Node.ParentId,
            IsEnable = row.IsEnable,
            AdjacentUnitIds = row.Node.AdjacentIds.ToList(),
        };
        Form.Load(fallback, Tree, deviceCount);
    }

    public void SetSelectedDevices(IEnumerable<UnitDeviceRowViewModel>? rows)
        => SelectedDevices = rows?.ToList() ?? (IReadOnlyList<UnitDeviceRowViewModel>)Array.Empty<UnitDeviceRowViewModel>();

    private void RestoreSelection()
    {
        var wanted = SelectedRow?.Id ?? 0;
        _rowCache.Clear();
        _deviceRowCache.Clear();
        SelectedRow = null;
        if (wanted > 0 && Tree.Find(wanted) is { } node) SelectedRow = RowOf(node);
    }
    #endregion

    #region - 등록 · 적용 · 되돌리기 -
    public void BeginCreate()
    {
        if (!CanAdd) return;
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.BeginCreate)) return;

        SelectedRow = null;
        DeleteBlock = null;
        Detail.Reset();
        Detail.IsCreating = true;
        Detail.IsReadOnly = false;
        Detail.CreateBanner = "부대 코드는 등록 뒤 절대 바꿀 수 없습니다 — NATS subject 의 두 번째 토큰이라 바꾸면 구독자가 메시지를 잃습니다.";
        Form.BeginCreate(Tree, null);
        RaiseDetail();
    }

    /// <summary>상세 칸의 [적용] · [등록].</summary>
    public async Task ApplyAsync(CancellationToken token = default)
    {
        if (IsBusy || !Detail.CanApply) return;

        Form.ErrorText = null;
        if (Form.IsCreating) await CreateAsync(token).ConfigureAwait(true);
        else await SaveAsync(token).ConfigureAwait(true);
    }

    public void Revert()
    {
        Form.ErrorText = null;
        if (Form.IsCreating)
        {
            Detail.Reset();
            Form.Clear();
            StatusText = "등록을 취소했습니다 — 서버 호출 0.";
        }
        else if (SelectedRow is { } row)
        {
            var detail = Form.Original as UnitDetailDto;
            if (detail is not null) Form.Load(detail, Tree, Form.DeviceCount);
            Detail.Tracker.Clear();
            StatusText = $"'{row.Name}' 의 변경을 되돌렸습니다 — 서버 호출 0.";
        }
        RaiseDetail();
    }

    private async Task CreateAsync(CancellationToken token)
    {
        var values = Form.CreateValues;
        var parent = values.ParentId is int parentId ? Tree.Find(parentId) : null;
        var codes = Tree.Ordered.Select(n => n.Code).ToList();

        if (!UnitRequestBuilder.TryValidateCreate(values, codes, parent, out var error))
        {
            Form.ErrorText = error;
            return;
        }

        IsBusy = true;
        try
        {
            var response = await _units.CreateAsync(UnitRequestBuilder.Create(values), token).ConfigureAwait(true);
            if (!response.Success)
            {
                Form.ErrorText = Reason(response.Error?.Message, response.Message);
                return;
            }

            Detail.Settle($"'{values.Name}' 을 등록했습니다.");
            StatusText = $"'{values.Name}'({values.Code}) 을 등록했습니다.";
            _pendingSelectId = response.Data?.Id ?? 0;
            IsBusy = false;
            await ReloadAsync(token, quiet: true).ConfigureAwait(true);
            await SelectByIdAsync(_pendingSelectId, token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) { Form.ErrorText = "등록을 취소했습니다."; }
        catch (Exception ex)
        {
            _log?.Error($"[UnitConsole] create: {ex.Message}");
            Form.ErrorText = "등록하지 못했습니다 — 서버에 닿지 못했습니다.";
        }
        finally { IsBusy = false; RaiseCommands(); }
    }

    private async Task SaveAsync(CancellationToken token)
    {
        if (SelectedRow is not { } row) return;
        if (!UnitRequestBuilder.TryValidateEdit(Form.EditValues, out var error)) { Form.ErrorText = error; return; }

        var dto = UnitRequestBuilder.Edit(Form.Original, Form.EditValues);
        if (dto is null) { Detail.Settle("바뀐 것이 없습니다 — 보내지 않았습니다."); RaiseDetail(); return; }

        IsBusy = true;
        try
        {
            var response = await _units.PatchAsync(row.Id, dto, token).ConfigureAwait(true);
            if (!response.Success) { Form.ErrorText = Reason(response.Error?.Message, response.Message); return; }

            Detail.Settle($"'{Form.Name}' 을 저장했습니다.");
            StatusText = $"'{Form.Name}' 을 저장했습니다.";
            IsBusy = false;
            await ReloadAsync(token, quiet: true).ConfigureAwait(true);
            await SelectByIdAsync(row.Id, token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) { Form.ErrorText = "저장을 취소했습니다."; }
        catch (Exception ex)
        {
            _log?.Error($"[UnitConsole] save {row.Id}: {ex.Message}");
            Form.ErrorText = "저장하지 못했습니다 — 서버에 닿지 못했습니다.";
        }
        finally { IsBusy = false; RaiseCommands(); }
    }
    #endregion

    #region - 트리 이동 (호출 1회 · 즉시) -
    /// <summary>
    /// 상위 부대 바꾸기. <b>드롭 즉시 한 번</b> 보내고, 실패하면 편제를 다시 읽어 제자리로 되돌린다.
    /// </summary>
    public async Task<bool> MoveAsync(int movingId, int? targetParentId, CancellationToken token = default)
    {
        var verdict = UnitDropRules.CanMove(Tree, movingId, targetParentId);
        if (!verdict.IsAllowed) { StatusText = verdict.Reason!; return false; }
        if (!CanEditUnits) { StatusText = "부대를 바꿀 권한이 없습니다(units:edit)."; return false; }
        if (IsBusy) { StatusText = "앞선 작업이 아직 끝나지 않았습니다."; return false; }

        var moving = Tree.Find(movingId)!;
        var previousParentId = moving.ParentId;
        var targetName = targetParentId is int id ? Tree.Find(id)?.Name ?? $"#{id}" : "최상위";

        IsBusy = true;
        try
        {
            var response = await _units.PatchAsync(movingId, UnitRequestBuilder.Move(targetParentId), token).ConfigureAwait(true);
            if (!response.Success)
            {
                StatusText = $"'{moving.Name}' 을 옮기지 못했습니다 — {Reason(response.Error?.Message, response.Message)}";
                IsBusy = false;
                await ReloadAsync(token, quiet: true).ConfigureAwait(true);   // 실패 복구는 재조회다(화면과 서버를 다시 맞춘다)
                return false;
            }

            _lastMove = new UnitMoveUndo(movingId, moving.Name, previousParentId);
            StatusText = $"'{moving.Name}' 을 '{targetName}' 으로 옮겼습니다.";
            IsBusy = false;
            await ReloadAsync(token, quiet: true).ConfigureAwait(true);
            await SelectByIdAsync(movingId, token).ConfigureAwait(true);
            return true;
        }
        catch (OperationCanceledException) { StatusText = "이동을 취소했습니다."; return false; }
        catch (Exception ex)
        {
            _log?.Error($"[UnitConsole] move {movingId} → {targetParentId}: {ex.Message}");
            StatusText = $"'{moving.Name}' 을 옮기지 못했습니다 — 서버에 닿지 못했습니다.";
            return false;
        }
        finally { IsBusy = false; RaiseCommands(); }
    }

    /// <summary>마지막 이동 1회를 되돌린다 — 반대 방향 PATCH 한 번.</summary>
    public async Task UndoMoveAsync(CancellationToken token = default)
    {
        if (_lastMove is not { } undo) return;
        _lastMove = null;
        NotifyOfPropertyChange(nameof(CanUndoMove));

        var ok = await MoveAsync(undo.UnitId, undo.PreviousParentId, token).ConfigureAwait(true);
        if (ok)
        {
            _lastMove = null;
            StatusText = $"'{undo.UnitName}' 의 이동을 되돌렸습니다.";
            NotifyOfPropertyChange(nameof(CanUndoMove));
        }
    }

    /// <summary>키보드 폴백 — Alt+↑ 는 한 단계 위로(부모의 부모, 없으면 최상위).</summary>
    public Task MoveSelectedUpAsync(CancellationToken token = default)
    {
        if (SelectedRow is not { } row) return Task.CompletedTask;
        var node = Tree.Find(row.Id);
        if (node?.ParentId is not int parentId) { StatusText = $"'{row.Name}' 은 이미 최상위 부대입니다."; return Task.CompletedTask; }

        var grandParentId = Tree.Find(parentId)?.ParentId;
        return MoveAsync(row.Id, grandParentId, token);
    }

    /// <summary>키보드 폴백 — Alt+↓ 는 바로 위 형제 밑으로(상위 제대일 때만).</summary>
    public Task MoveSelectedDownAsync(CancellationToken token = default)
    {
        if (SelectedRow is not { } row) return Task.CompletedTask;

        var index = Rows.IndexOf(row);
        for (var i = index - 1; i >= 0; i--)
        {
            var candidate = Rows[i];
            if (UnitDropRules.CanMove(Tree, row.Id, candidate.Id).IsAllowed) return MoveAsync(row.Id, candidate.Id, token);
        }

        StatusText = $"'{row.Name}' 을 받을 수 있는 상위 부대가 바로 위에 없습니다.";
        return Task.CompletedTask;
    }
    #endregion

    #region - 인접 (호출 1회 · 전체 집합 교체) -
    /// <summary>
    /// 인접을 더하거나 뺀다. 서버가 그 부대의 인접을 <b>전삭제 후 재생성</b>하므로
    /// 현재 전체 + 변경분을 함께 보낸다. 보내기 직전에 <b>다시 읽어</b> 다른 세션의 변경을 덮지 않는다.
    /// </summary>
    public async Task ChangeAdjacencyAsync(int? add, int? remove, CancellationToken token = default)
    {
        if (SelectedRow is not { } row) return;
        if (!CanEditUnits) { StatusText = "인접을 바꿀 권한이 없습니다(units:edit)."; return; }
        if (IsBusy) return;

        if (add is int addId)
        {
            var verdict = UnitDropRules.CanAdjoin(Tree, addId, row.Id);
            if (!verdict.IsAllowed) { StatusText = verdict.Reason!; return; }
        }

        IsBusy = true;
        try
        {
            // 저장 직전 재조회 — 동시 편집에서 나중 저장이 앞선 변경을 지우는 것을 막는다(스토리보드 L368).
            var fresh = await _units.GetDetailAsync(row.Id, token).ConfigureAwait(true);
            if (!fresh.Success || fresh.Data is null)
            {
                StatusText = $"인접을 바꾸기 전에 '{row.Name}' 을 다시 읽지 못했습니다 — 아무것도 보내지 않았습니다.";
                return;
            }

            var current = fresh.Data.AdjacentUnitIds ?? new List<int>();
            var known = Form.AdjacentIds;
            if (!current.OrderBy(x => x).SequenceEqual(known.OrderBy(x => x)))
                StatusText = $"'{row.Name}' 의 인접이 그 사이 바뀌었습니다 — 서버의 최신 목록에 이어서 보냅니다.";

            var merged = UnitDropRules.MergeAdjacency(current, row.Id, add, remove);
            var response = await _units.PatchAsync(row.Id, UnitRequestBuilder.Adjacency(merged, row.Id), token).ConfigureAwait(true);
            if (!response.Success)
            {
                StatusText = $"인접을 바꾸지 못했습니다 — {Reason(response.Error?.Message, response.Message)}";
                return;
            }

            var other = add ?? remove;
            var otherName = other is int id ? Tree.Find(id)?.Name ?? $"#{id}" : string.Empty;
            StatusText = add is not null
                ? $"'{row.Name}' 과 '{otherName}' 을 인접으로 이었습니다 — 양방향으로 연결됩니다."
                : $"'{row.Name}' 과 '{otherName}' 의 인접을 끊었습니다.";

            IsBusy = false;
            await ReloadAsync(token, quiet: true).ConfigureAwait(true);
            await SelectByIdAsync(row.Id, token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) { StatusText = "인접 변경을 취소했습니다."; }
        catch (Exception ex)
        {
            _log?.Error($"[UnitConsole] adjacency {row.Id}: {ex.Message}");
            StatusText = "인접을 바꾸지 못했습니다 — 서버에 닿지 못했습니다.";
        }
        finally { IsBusy = false; RaiseCommands(); }
    }
    #endregion

    #region - 삭제 · 운용 중지 -
    public async Task DeleteAsync(CancellationToken token = default)
    {
        if (SelectedRow is not { } row || !CanDeleteUnit) return;

        IsBusy = true;
        try
        {
            var response = await _units.DeleteAsync(row.Id, token).ConfigureAwait(true);
            if (response.Success)
            {
                StatusText = $"'{row.Name}' 을 지웠습니다.";
                DeleteBlock = null;
                SelectedRow = null;
                Form.Clear();
                Detail.Reset();
                IsBusy = false;
                await ReloadAsync(token, quiet: true).ConfigureAwait(true);
                return;
            }

            DeleteBlock = UnitRequestBuilder.ParseDeleteConflict(response.Error);
            StatusText = IsDeleteBlocked
                ? $"'{row.Name}' 에 매달린 것이 있어 지울 수 없습니다 — 운용 중지를 쓰십시오."
                : $"'{row.Name}' 을 지우지 못했습니다 — {Reason(response.Error?.Message, response.Message)}";
        }
        catch (OperationCanceledException) { StatusText = "삭제를 취소했습니다."; }
        catch (Exception ex)
        {
            _log?.Error($"[UnitConsole] delete {row.Id}: {ex.Message}");
            StatusText = "지우지 못했습니다 — 서버에 닿지 못했습니다.";
        }
        finally { IsBusy = false; RaiseCommands(); }
    }

    /// <summary>퇴역은 삭제가 아니라 <c>is_enable=false</c> 다 — 이력과 소속이 보존된다(스토리보드 L386).</summary>
    public async Task DisableAsync(CancellationToken token = default)
    {
        if (SelectedRow is not { } row || !CanEditUnits || IsBusy) return;

        IsBusy = true;
        try
        {
            var response = await _units.PatchAsync(row.Id, new UnitUpdateDto { IsEnable = false }, token).ConfigureAwait(true);
            StatusText = response.Success
                ? $"'{row.Name}' 을 운용 중지했습니다 — 이력과 소속은 그대로입니다."
                : $"운용 중지에 실패했습니다 — {Reason(response.Error?.Message, response.Message)}";
            if (!response.Success) return;

            DeleteBlock = null;
            IsBusy = false;
            await ReloadAsync(token, quiet: true).ConfigureAwait(true);
            await SelectByIdAsync(row.Id, token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) { StatusText = "운용 중지를 취소했습니다."; }
        catch (Exception ex)
        {
            _log?.Error($"[UnitConsole] disable {row.Id}: {ex.Message}");
            StatusText = "운용 중지에 실패했습니다 — 서버에 닿지 못했습니다.";
        }
        finally { IsBusy = false; RaiseCommands(); }
    }

    public void DismissDeleteBlock() => DeleteBlock = null;
    #endregion

    #region - 미배치 장비 → 부대 (호출 N회 · Draft) -
    /// <summary>
    /// 장비들을 부대에 붙이는 Draft 를 쌓는다. 장비 <b>한 대마다 다시 받기 1 + PATCH 1</b> 이라
    /// 드롭 즉시 보내지 않는다(드래그 와이어프레임 L413).
    /// </summary>
    public void QueueAssign(int targetUnitId, IReadOnlyList<UnitDeviceRowViewModel> devices)
    {
        if (Tree.Find(targetUnitId) is not { } target) return;
        if (Tray.IsApplying) { StatusText = "적용 중에는 더 쌓을 수 없습니다."; return; }

        var verdict = UnitDropRules.CanAssignDevices(Tree, targetUnitId, devices.Select(d => d.Item.UnitId).ToList());
        if (!verdict.IsAllowed) { StatusText = verdict.Reason!; return; }

        var capped = devices.Take(MAX_ASSIGN_PER_APPLY).ToList();
        foreach (var row in capped)
        {
            row.PendingUnitName = target.Name;
            var item = row.Item;
            Tray.Add(new DraftEntry(
                targetKey: $"device:{item.Id}",
                callKind: "unit-assign",
                description: $"{item.Name} → {target.Name}",
                apply: ct => AssignOneAsync(item, targetUnitId, ct)));
        }

        var dropped = devices.Count - capped.Count;
        StatusText = dropped > 0
            ? $"'{target.Name}' 에 {capped.Count}대를 쌓았습니다 — 한 번에 {MAX_ASSIGN_PER_APPLY}대까지만 보냅니다({dropped}대는 제외)."
            : $"'{target.Name}' 에 {capped.Count}대를 쌓았습니다 — [적용] 때 {capped.Count}번 보냅니다.";
        RaiseTray();
    }

    private async Task<DraftOutcome> AssignOneAsync(UnitDeviceItem item, int unitId, CancellationToken token)
    {
        // 앞이 실패했으면 그 뒤는 보내지 않는다 — 폭발반경을 실패 지점에서 끊는다.
        if (_assignStopped) { _assignSkipped.Add(item.Name); return DraftOutcome.Skipped; }

        var result = await _devices.AssignAsync(item, unitId, token).ConfigureAwait(true);
        if (result.IsSuccess) return DraftOutcome.Applied;

        _assignStopped = true;
        _assignFailure = result.Message;
        return DraftOutcome.Failed;
    }

    public async Task ApplyAssignsAsync(CancellationToken token = default)
    {
        if (!Tray.CanApply) return;

        _assignStopped = false;
        _assignFailure = null;
        _assignSkipped.Clear();

        IsBusy = true;
        try
        {
            var summary = await Tray.ApplyAsync(token).ConfigureAwait(true);
            var line = summary.ToMessage();
            if (_assignFailure is not null) line += $" · 첫 실패에서 멈췄습니다 — {_assignFailure}";
            if (_assignSkipped.Count > 0) line += $" · 보내지 않은 {_assignSkipped.Count}대는 그대로 남습니다";
            StatusText = line;

            IsBusy = false;
            await ReloadAsync(token, quiet: true).ConfigureAwait(true);   // 성공분 반영은 재조회로 확정한다
        }
        catch (OperationCanceledException) { StatusText = "배치를 취소했습니다."; }
        catch (Exception ex)
        {
            _log?.Error($"[UnitConsole] apply assigns: {ex.Message}");
            StatusText = "배치에 실패했습니다 — 서버에 닿지 못했습니다.";
        }
        finally { IsBusy = false; RaiseTray(); RaiseCommands(); }
    }

    public void RevertAssigns()
    {
        Tray.Revert();
        foreach (var row in DeviceRows) row.PendingUnitName = null;
        StatusText = "쌓아 둔 배치를 버렸습니다 — 서버 호출 0.";
        RaiseTray();
    }

    /// <summary>끌기의 버튼 폴백 — 고른 장비를 지금 고른 부대에 쌓는다.</summary>
    public void QueueAssignSelected()
    {
        if (SelectedRow is not { } row) { StatusText = "놓을 부대를 트리에서 먼저 고르십시오."; return; }
        QueueAssign(row.Id, SelectedDevices);
    }
    #endregion

    #region - Drop plumbing -
    private void OnDropped(UnitDropRequest request)
    {
        // 드롭은 UI 스레드에서 오지만 이어지는 전송은 작업 스레드에서 끝난다 — 화면을 만지는 일은 UI 로 되돌린다.
        Execute.BeginOnUIThread(async () =>
        {
            try
            {
                switch (request.ZoneKey)
                {
                    case UnitDropRules.ZONE_ROOT:
                        await MoveAsync(request.Units[0].Id, null).ConfigureAwait(true);
                        break;

                    case UnitDropRules.ZONE_PARENT when request.Devices.Count > 0:
                        QueueAssign(request.TargetUnitId, request.Devices);
                        break;

                    case UnitDropRules.ZONE_PARENT:
                        await MoveAsync(request.Units[0].Id, request.TargetUnitId).ConfigureAwait(true);
                        break;

                    case UnitDropRules.ZONE_ADJACENCY:
                        await ChangeAdjacencyAsync(request.Units[0].Id, null).ConfigureAwait(true);
                        break;
                }
            }
            catch (Exception ex)
            {
                _log?.Error($"[UnitConsole] drop {request.ZoneKey}: {ex.Message}");
                StatusText = "놓은 것을 처리하지 못했습니다.";
            }
        });
    }
    #endregion

    #region - Helpers -
    public async Task SelectByIdAsync(int unitId, CancellationToken token = default)
    {
        if (unitId <= 0) return;
        var node = Tree.Find(unitId);
        if (node is null) return;
        await SelectRowAsync(RowOf(node), token).ConfigureAwait(true);
    }

    public void SelectEchelon(UnitEchelonFilterViewModel? filter)
    {
        if (filter is null) return;
        foreach (var f in EchelonFilters) f.IsSelected = ReferenceEquals(f, filter);
        Project();
    }

    public void ToggleExpand(UnitNodeRowViewModel? row)
    {
        if (row is null || !row.HasChildren) return;
        row.IsExpanded = !row.IsExpanded;
        Project();
    }

    private static string Reason(string? errorMessage, string? message)
        => string.IsNullOrWhiteSpace(errorMessage)
         ? (string.IsNullOrWhiteSpace(message) ? "서버가 거절했습니다." : message!)
         : errorMessage!;

    private void RaiseDetail()
    {
        NotifyOfPropertyChange(nameof(IsDetailRequested));
        RaiseCommands();
    }

    private void RaiseTray()
    {
        NotifyOfPropertyChange(nameof(Tray));
        RaiseCommands();
    }

    private void RaiseViewFlags()
    {
        NotifyOfPropertyChange(nameof(IsTreeView));
        NotifyOfPropertyChange(nameof(IsDeviceView));
        NotifyOfPropertyChange(nameof(IsAdjacencyView));
        NotifyOfPropertyChange(nameof(ListStatusText));
    }

    private void RaiseCommands()
    {
        NotifyOfPropertyChange(nameof(CanAdd));
        NotifyOfPropertyChange(nameof(CanDeleteUnit));
        NotifyOfPropertyChange(nameof(CanReload));
        NotifyOfPropertyChange(nameof(CanMoveSelected));
        NotifyOfPropertyChange(nameof(CanAssignSelectedDevices));
        NotifyOfPropertyChange(nameof(CanUndoMove));
        NotifyOfPropertyChange(nameof(CanEditUnits));
        NotifyOfPropertyChange(nameof(IsAvailable));
        NotifyOfPropertyChange(nameof(ListStatusText));
    }
    #endregion

    #region - Attributes -
    /// <summary>한 번의 [적용] 에서 보낼 수 있는 최대 장비 수 — N회 호출의 폭발반경을 눈에 보이게 묶는다.</summary>
    public const int MAX_ASSIGN_PER_APPLY = 50;

    private sealed record UnitMoveUndo(int UnitId, string UnitName, int? PreviousParentId);

    private readonly IUnitGraphApi _units;
    private readonly IUnitDeviceApi _devices;
    private readonly ILogService? _log;
    private readonly Func<string?> _myUnitCode;
    private readonly Func<bool> _canEdit;
    private readonly Func<bool> _canDelete;

    private readonly Dictionary<int, UnitNodeRowViewModel> _rowCache = new();
    private readonly Dictionary<int, UnitDeviceRowViewModel> _deviceRowCache = new();
    private readonly List<string> _assignSkipped = new();

    private IReadOnlyList<UnitDeviceItem> _allDevices = Array.Empty<UnitDeviceItem>();
    private IReadOnlyList<UnitDeviceRowViewModel> _selectedDevices = Array.Empty<UnitDeviceRowViewModel>();
    private ConsoleRailEntry _selectedRail;
    private UnitNodeRowViewModel? _selectedRow;
    private UnitMoveUndo? _lastMove;
    private UnitDeleteBlock? _deleteBlock;
    private string _searchText = string.Empty;
    private string _statusText = string.Empty;
    private bool _isBusy;
    private bool _loadedOnce;
    private bool _assignStopped;
    private string? _assignFailure;
    private int _pendingSelectId;
    #endregion
}
