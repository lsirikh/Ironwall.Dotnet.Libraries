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

    /// <summary>창 제목(<see cref="Screen.DisplayName"/>).</summary>
    public const string WINDOW_TITLE = "부대 편제";

    /// <summary>편집 권한이 없을 때 상태 띠 · 툴팁에 쓰는 말(권한 키 이름은 보이지 않는다 — U-18 D-8 8.4).</summary>
    public const string NO_EDIT_PERMISSION = "부대를 편집할 권한이 없습니다.";

    /// <summary>서버가 부대 편제를 모를 때(판본 번호는 보이지 않는다 — U-18 D-8 8.9).</summary>
    public const string NOT_SUPPORTED = "현재 서버는 부대 편제를 지원하지 않습니다.";

    #region - Ctors -
    public UnitConsoleViewModel(
        IUnitGraphApi units,
        IUnitDeviceApi devices,
        ILogService? log = null,
        Func<string?>? myUnitCode = null,
        Func<bool>? canEdit = null,
        Func<bool>? canDelete = null,
        Func<bool>? canView = null,
        Func<bool>? canPlaceDevices = null)
    {
        _units = units ?? throw new ArgumentNullException(nameof(units));
        _devices = devices ?? throw new ArgumentNullException(nameof(devices));
        _log = log;
        _myUnitCode = myUnitCode ?? (() => null);
        // 서버가 부대 편제를 지키는 모듈 그대로 — units:edit · units:delete · units:view(permission_map.py).
        // 장비를 부대에 두는 것만 장비 쓰기(PATCH /api/devices/…)라 devices:edit 다.
        _canEdit = canEdit ?? UnitPermissionGate.CanEdit;
        _canDelete = canDelete ?? UnitPermissionGate.CanDelete;
        _canView = canView ?? UnitPermissionGate.CanView;
        _canPlaceDevices = canPlaceDevices ?? DevicePermissionGate.CanEdit;

        // 창 제목 — Caliburn 창 관리자가 DisplayName 을 Title 로 묶는다. 비우면 타입 이름이 뜬다(U-18 D-0 0.2 · D-8 8.1).
        DisplayName = WINDOW_TITLE;

        Detail = new ConsoleDetailPresenter { TypeName = "부대" };
        Form = new UnitDetailFormViewModel(Detail);
        Tray = new DraftTrayViewModel();
        Drop = new UnitDropHandler(() => Tree, () => SelectedRow?.Id ?? 0, () => _canEdit(), () => IsBusy, OnDropped,
                                   reason => StatusText = reason, () => _canPlaceDevices());

        // 아이콘은 이름만 쥔 토큰이다 — 싱글턴이 아닌 창이어도 뷰모델이 시각 요소를 쥐지 않는다(장비 콘솔 선례).
        // "인접 관계도" 칸은 내지 않는다 — 그림 관계도가 아직 없어 칸을 누르면 "다음 단계" 자리표시만 떴다(U-18 D-8 8.2).
        // 인접 편집은 상세 칸에서 그대로 한다. 관계도가 생기면 RAIL_ADJACENCY 칸을 여기 다시 넣는다.
        RailEntries.Add(new ConsoleRailEntry(RAIL_TREE, "편제 트리", new ConsoleIconToken("FileTree")) { ShowCount = true });
        RailEntries.Add(new ConsoleRailEntry(RAIL_DEVICES, "미배치 장비", new ConsoleIconToken("Devices")) { ShowCount = true });
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

    /// <summary>인접 관계도는 <b>이 노드의 범위 밖</b>이다(봉투: "관계도는 뒤"). 레일 칸을 내지 않아 이 값은 늘 false 다.</summary>
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

    /// <summary>트리에 그릴 행이 없다 — 필터 때문인지 편제가 빈 것인지 글로 가른다.</summary>
    public bool IsTreeEmpty => Rows.Count == 0;

    public bool IsDeviceListEmpty => DeviceRows.Count == 0;

    public string DeviceHeaderText => $"미배치 장비 {DeviceRows.Count}";

    public string ListStatusText => IsDeviceView
        ? $"미배치 장비 {DeviceRows.Count}"
        : $"부대 {Tree.Count} · 인접 쌍 {AdjacencyPairCount}";

    public string RailFooterText => string.IsNullOrEmpty(MyUnitCode)
        ? "같은 단계의 부대는 코드 순으로 표시됩니다."
        : $"내 부대 · {MyUnitCode}";

    public string? MyUnitCode => _myUnitCode();

    public int AdjacencyPairCount { get; private set; }

    /// <summary>제대 칩 · 검색이 걸려 있다 — 목록은 트리가 아니라 평면이다.</summary>
    public bool IsFiltered { get => _isFiltered; private set { _isFiltered = value; NotifyOfPropertyChange(); } }

    /// <summary>삭제가 409 로 막혔을 때 무엇이 매달려 있는지(스토리보드 화면 J).</summary>
    public UnitDeleteBlock? DeleteBlock { get => _deleteBlock; private set { _deleteBlock = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(IsDeleteBlocked)); } }
    public bool IsDeleteBlocked => _deleteBlock is { Items.Count: > 0 };

    /// <summary>방금 한 이동 — 되돌리기는 <b>마지막 1회</b>만(드래그 와이어프레임 L546).</summary>
    public bool CanUndoMove => _lastMove is not null && !IsBusy;
    #endregion

    #region - Commands state -
    public bool IsAvailable => _units.IsAvailable;
    public bool CanEditUnits => _canEdit();
    public bool CanAdd => IsAvailable && CanEditUnits && !IsBusy;
    public string AddBlockedReason => !IsAvailable ? NOT_SUPPORTED : "부대를 등록할 권한이 없습니다.";
    public bool CanDeleteUnit => IsAvailable && _canDelete() && SelectedRow is not null && !Form.IsCreating && !IsBusy;
    public string DeleteBlockedReason => SelectedRow is null ? "지울 부대를 먼저 고르세요." : "부대를 지울 권한이 없습니다.";
    public bool CanViewUnits => _canView();
    public bool CanReload => IsAvailable && CanViewUnits && !IsBusy;
    public bool CanMoveSelected => IsAvailable && CanEditUnits && SelectedRow is not null && !IsBusy;

    /// <summary>장비를 부대에 두는 것은 장비 쓰기다(<c>devices:edit</c>) — <c>units:edit</c> 가 아니다.</summary>
    public bool CanPlaceDevices => IsAvailable && _canPlaceDevices();
    public bool CanAssignSelectedDevices => CanPlaceDevices && SelectedDevices.Count > 0 && AssignTargetId > 0 && !IsBusy;

    /// <summary>장비를 놓을 부대 — 트리 선택을 따르되 콤보로도 고른다(드래그의 버튼 · 키보드 경로).</summary>
    public int? AssignTargetUnitId
    {
        get => _assignTargetUnitId;
        set { _assignTargetUnitId = value; NotifyOfPropertyChange(); RaiseCommands(); }
    }

    /// <summary>실제로 쓰이는 대상 — 콤보가 비어 있으면 트리에서 고른 부대.</summary>
    public int AssignTargetId => _assignTargetUnitId is int id && id > 0 ? id : SelectedRow?.Id ?? 0;

    public string AssignTargetText
        => AssignTargetId <= 0
         ? "놓을 부대를 고르세요"
         : $"'{Tree.Find(AssignTargetId)?.Name ?? $"부대 {AssignTargetId}번"}'에 배치";

    /// <summary>피커가 고를 수 있는 부대 전부.</summary>
    public BindableCollection<UnitOptionViewModel> AssignTargets { get; } = new();
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
    /// <param name="bypassGuard">
    /// <b>서버가 이미 바꾼 것</b>을 다시 읽는 길이다 — 미적용 관문을 건너뛴다.
    /// 관문은 <b>사용자가 손댄 칸</b>을 지키려고 있는 것이지, 서버가 확인해 준 사실을 화면에서 막으라는 뜻이 아니다.
    /// 건너뛰지 않으면 상세가 더럽다는 이유로 이동 · 인접 · 배치 뒤의 재조회가 조용히 취소되어
    /// <b>서버는 바뀌었는데 화면만 옛 상태</b>로 남는다.
    /// </param>
    public async Task ReloadAsync(CancellationToken token = default, bool quiet = false, bool bypassGuard = false)
    {
        if (!bypassGuard && !Detail.Guard.TryNavigate(ConsoleNavigation.Refresh)) return;
        if (IsBusy) return;                 // 스스로 막는다 — 호출부가 IsBusy 를 내려놓고 부르는 길이 여럿이다
        if (!IsAvailable)
        {
            StatusText = NOT_SUPPORTED;
            return;
        }
        if (!CanViewUnits)
        {
            // GET /api/units/graph 는 units:view 다 — 권한이 없으면 부르기 전에 접는다(403 왕복을 만들지 않는다).
            StatusText = "부대 편제를 볼 권한이 없습니다.";
            return;
        }

        IsBusy = true;
        try
        {
            var response = await _units.GetGraphAsync(token).ConfigureAwait(true);
            if (!response.Success)
            {
                StatusText = $"편제를 불러오지 못했습니다. {Reason(response.Error?.Message, response.Message)}";
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
            // 고른 행의 통지는 Project 뒤다 — 목록에 아직 없는 인스턴스를 밀면 ListBox 가 그냥 버린다.
            NotifyOfPropertyChange(nameof(SelectedRow));
            if (!quiet) StatusText = $"부대 {Tree.Count}개를 불러왔습니다.";
        }
        catch (OperationCanceledException) { StatusText = "불러오기를 취소했습니다."; }
        catch (Exception ex)
        {
            _log?.Error($"[UnitConsole] reload: {ex.Message}");
            StatusText = $"편제를 불러오지 못했습니다. {UNREACHABLE}";
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
        // 필터가 걸리면 더는 트리가 아니다 — 부모가 빠진 자식을 원래 깊이로 그리면 허공에 들여쓰기된다.
        IsFiltered = echelon is not null || needle.Length > 0;

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
            row.IsFlat = IsFiltered;
            keep.Add(row);
        }

        Sync(Rows, keep);

        var unassigned = _allDevices.Where(IsUnassigned).ToList();
        var deviceRows = unassigned.Select(item => DeviceRowOf(item)).ToList();
        Sync(DeviceRows, deviceRows);

        SyncAssignTargets();
        // 칸을 순번이 아니라 키로 찾는다 — "인접 관계도" 칸을 숨긴 뒤로 칸 수가 바뀌었다(U-18 D-8 8.2).
        SetRailCount(RAIL_TREE, Tree.Count);
        SetRailCount(RAIL_ADJACENCY, AdjacencyPairCount);
        SetRailCount(RAIL_DEVICES, DeviceRows.Count);

        NotifyOfPropertyChange(nameof(ListStatusText));
        NotifyOfPropertyChange(nameof(RailFooterText));
        NotifyOfPropertyChange(nameof(IsTreeEmpty));
        NotifyOfPropertyChange(nameof(IsDeviceListEmpty));
        NotifyOfPropertyChange(nameof(DeviceHeaderText));
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
        // 접힘은 _collapsed 가 정본이다 — 행 인스턴스에 기대면 재조회가 캐시를 비우는 순간 전부 펼쳐진다.
        var row = new UnitNodeRowViewModel(node, isMine) { IsExpanded = !_collapsed.Contains(node.Id) };
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

    /// <summary>장비를 놓을 부대 후보 — 편제 전체(트리 순서).</summary>
    private void SyncAssignTargets()
    {
        var wanted = Tree.Ordered
                         .Select(n => new UnitOptionViewModel(n.Id, $"{UnitDropRules.EchelonTextOf(n)} · {n.Name}"))
                         .ToList();

        for (var i = 0; i < wanted.Count; i++)
        {
            if (i < AssignTargets.Count && AssignTargets[i].Id == wanted[i].Id) continue;
            if (i < AssignTargets.Count) AssignTargets[i] = wanted[i];
            else AssignTargets.Add(wanted[i]);
        }
        while (AssignTargets.Count > wanted.Count) AssignTargets.RemoveAt(AssignTargets.Count - 1);

        if (_assignTargetUnitId is int id && Tree.Find(id) is null) AssignTargetUnitId = null;
        NotifyOfPropertyChange(nameof(AssignTargetText));
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
    /// <param name="force">
    /// <b>쓰기가 끝난 뒤</b>의 다시 읽기다. 재조회가 행 인스턴스를 새로 만들어도 <b>같은 부대</b>라
    /// 참조 비교 조기 반환에 걸려 상세가 영영 갱신되지 않는다 — 인접 칩이 그대로 남고,
    /// <see cref="UnitDetailFormViewModel.Original"/> 이 저장 전 DTO 를 붙들어 다음 PATCH 가
    /// <b>이미 저장된 칸을 다시 보낸다</b>.
    /// </param>
    public async Task SelectRowAsync(UnitNodeRowViewModel? row, CancellationToken token = default, bool force = false)
    {
        if (!force && ReferenceEquals(row, SelectedRow)) return;
        if (!force && !Detail.Guard.TryNavigate(ConsoleNavigation.SelectRow)) return;

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
        // A 를 고르고 곧바로 B 를 고르면 A 의 답이 뒤에 도착해 B 의 상세를 덮을 수 있다 — 표를 끊어 둔다.
        var ticket = ++_detailTicket;
        var deviceCount = _allDevices.Count(d => d.UnitId == row.Id);

        if (!IsAvailable)
        {
            Form.Clear();
            return;
        }

        try
        {
            var response = await _units.GetDetailAsync(row.Id, token).ConfigureAwait(true);
            if (ticket != _detailTicket) return;                 // 그 사이 다른 부대를 골랐다
            if (response.Success && response.Data is { } detail)
            {
                Form.Load(detail, Tree, deviceCount);
                Form.RefreshChildEchelonWarning(Tree);
                return;
            }
            StatusText = $"'{row.Name}' 상세를 불러오지 못했습니다. {Reason(response.Error?.Message, response.Message)}";
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Warning($"[UnitConsole] detail {row.Id}: {ex.Message}");
            StatusText = $"'{row.Name}' 상세를 불러오지 못했습니다. {UNREACHABLE}";
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
        // 통지 없이 바꾼다 — Project 가 끝나 목록이 채워진 뒤에 한 번만 알린다(ReloadAsync).
        _selectedRow = wanted > 0 && Tree.Find(wanted) is { } node ? RowOf(node) : null;
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
        Detail.CreateBanner = "부대 코드는 등록 후 바꿀 수 없습니다. 신중히 입력하세요.";
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
            StatusText = "등록을 취소했습니다.";
        }
        else if (SelectedRow is { } row)
        {
            var detail = Form.Original as UnitDetailDto;
            if (detail is not null) Form.Load(detail, Tree, Form.DeviceCount);
            Detail.Tracker.Clear();
            StatusText = $"'{row.Name}'의 변경을 되돌렸습니다.";
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
                Form.ErrorText = $"등록하지 못했습니다. {Reason(response.Error?.Message, response.Message)}";
                return;
            }

            Detail.Settle($"'{values.Name}'을(를) 등록했습니다.");
            StatusText = $"'{values.Name}'({values.Code})을(를) 등록했습니다.";
            _pendingSelectId = response.Data?.Id ?? 0;
            IsBusy = false;
            await ReloadAsync(token, quiet: true, bypassGuard: true).ConfigureAwait(true);
            await SelectByIdAsync(_pendingSelectId, token, force: true).ConfigureAwait(true);
        }
        catch (OperationCanceledException) { Form.ErrorText = "등록을 취소했습니다."; }
        catch (Exception ex)
        {
            _log?.Error($"[UnitConsole] create: {ex.Message}");
            Form.ErrorText = $"등록하지 못했습니다. {UNREACHABLE}";
        }
        finally { IsBusy = false; RaiseCommands(); }
    }

    private async Task SaveAsync(CancellationToken token)
    {
        if (SelectedRow is not { } row) return;
        if (!UnitRequestBuilder.TryValidateEdit(Form.EditValues, out var error)) { Form.ErrorText = error; return; }

        var dto = UnitRequestBuilder.Edit(Form.Original, Form.EditValues);
        if (dto is null) { Detail.Settle("바뀐 내용이 없습니다."); RaiseDetail(); return; }

        IsBusy = true;
        try
        {
            var response = await _units.PatchAsync(row.Id, dto, token).ConfigureAwait(true);
            if (!response.Success) { Form.ErrorText = $"저장하지 못했습니다. {Reason(response.Error?.Message, response.Message)}"; return; }

            Detail.Settle($"'{Form.Name}'을(를) 저장했습니다.");
            StatusText = $"'{Form.Name}'을(를) 저장했습니다.";
            IsBusy = false;
            await ReloadAsync(token, quiet: true, bypassGuard: true).ConfigureAwait(true);
            await SelectByIdAsync(row.Id, token, force: true).ConfigureAwait(true);
        }
        catch (OperationCanceledException) { Form.ErrorText = "저장을 취소했습니다."; }
        catch (Exception ex)
        {
            _log?.Error($"[UnitConsole] save {row.Id}: {ex.Message}");
            Form.ErrorText = $"저장하지 못했습니다. {UNREACHABLE}";
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
        if (!CanEditUnits) { StatusText = NO_EDIT_PERMISSION; return false; }
        if (IsBusy) { StatusText = "앞선 작업이 아직 끝나지 않았습니다. 잠시 후 다시 시도하세요."; return false; }

        var moving = Tree.Find(movingId)!;
        var previousParentId = moving.ParentId;
        var targetName = targetParentId is int id ? Tree.Find(id)?.Name ?? $"부대 {id}번" : "최상위";

        IsBusy = true;
        try
        {
            var response = await _units.PatchAsync(movingId, UnitRequestBuilder.Move(targetParentId), token).ConfigureAwait(true);
            if (!response.Success)
            {
                StatusText = $"'{moving.Name}'을(를) 옮기지 못했습니다. {Reason(response.Error?.Message, response.Message)}";
                IsBusy = false;
                await ReloadAsync(token, quiet: true, bypassGuard: true).ConfigureAwait(true);   // 실패 복구는 재조회다(화면과 서버를 다시 맞춘다)
                return false;
            }

            _lastMove = new UnitMoveUndo(movingId, moving.Name, previousParentId);
            StatusText = $"'{moving.Name}'을(를) '{targetName}'(으)로 옮겼습니다.";
            IsBusy = false;
            await ReloadAsync(token, quiet: true, bypassGuard: true).ConfigureAwait(true);
            await SelectByIdAsync(movingId, token, force: true).ConfigureAwait(true);
            return true;
        }
        catch (OperationCanceledException) { StatusText = "이동을 취소했습니다."; return false; }
        catch (Exception ex)
        {
            _log?.Error($"[UnitConsole] move {movingId} → {targetParentId}: {ex.Message}");
            StatusText = $"'{moving.Name}'을(를) 옮기지 못했습니다. {UNREACHABLE}";
            return false;
        }
        finally { IsBusy = false; RaiseCommands(); }
    }

    /// <summary>마지막 이동 1회를 되돌린다 — 반대 방향 PATCH 한 번.</summary>
    public async Task UndoMoveAsync(CancellationToken token = default)
    {
        if (_lastMove is not { } undo) return;

        // 표를 미리 버리지 않는다 — 되돌리기가 실패하면 되돌릴 방법이 영영 사라진다.
        var ok = await MoveAsync(undo.UnitId, undo.PreviousParentId, token).ConfigureAwait(true);
        _lastMove = ok ? null : undo;
        if (ok) StatusText = $"'{undo.UnitName}'의 이동을 되돌렸습니다.";
        NotifyOfPropertyChange(nameof(CanUndoMove));
    }

    /// <summary>키보드 폴백 — Alt+↑ 는 한 단계 위로(부모의 부모, 없으면 최상위).</summary>
    public Task MoveSelectedUpAsync(CancellationToken token = default)
    {
        if (SelectedRow is not { } row) return Task.CompletedTask;
        var node = Tree.Find(row.Id);
        if (node?.ParentId is not int parentId) { StatusText = $"'{row.Name}'은(는) 이미 최상위 부대입니다."; return Task.CompletedTask; }

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

        StatusText = $"'{row.Name}'을(를) 받을 수 있는 상위 부대가 바로 위에 없습니다.";
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
        if (!CanEditUnits) { StatusText = "인접 부대를 바꿀 권한이 없습니다."; return; }
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
                StatusText = $"'{row.Name}' 정보를 다시 불러오지 못해 인접 부대를 바꾸지 않았습니다. 잠시 후 다시 시도하세요.";
                return;
            }

            var current = fresh.Data.AdjacentUnitIds ?? new List<int>();
            var known = Form.AdjacentIds;
            if (!current.OrderBy(x => x).SequenceEqual(known.OrderBy(x => x)))
                StatusText = $"'{row.Name}'의 인접 부대가 그사이 바뀌어 최신 목록에 이어서 저장합니다.";

            var merged = UnitDropRules.MergeAdjacency(current, row.Id, add, remove);
            var response = await _units.PatchAsync(row.Id, UnitRequestBuilder.Adjacency(merged, row.Id), token).ConfigureAwait(true);
            if (!response.Success)
            {
                StatusText = $"인접 부대를 바꾸지 못했습니다. {Reason(response.Error?.Message, response.Message)}";
                return;
            }

            var other = add ?? remove;
            var otherName = other is int id ? Tree.Find(id)?.Name ?? $"부대 {id}번" : string.Empty;
            StatusText = add is not null
                ? $"'{row.Name}'과(와) '{otherName}'을(를) 인접 부대로 이었습니다. 양쪽에 함께 표시됩니다."
                : $"'{row.Name}'과(와) '{otherName}'의 인접 관계를 끊었습니다.";

            IsBusy = false;
            await ReloadAsync(token, quiet: true, bypassGuard: true).ConfigureAwait(true);
            await SelectByIdAsync(row.Id, token, force: true).ConfigureAwait(true);
        }
        catch (OperationCanceledException) { StatusText = "인접 부대 변경을 취소했습니다."; }
        catch (Exception ex)
        {
            _log?.Error($"[UnitConsole] adjacency {row.Id}: {ex.Message}");
            StatusText = $"인접 부대를 바꾸지 못했습니다. {UNREACHABLE}";
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
                StatusText = $"'{row.Name}'을(를) 삭제했습니다.";
                DeleteBlock = null;
                SelectedRow = null;
                Form.Clear();
                Detail.Reset();
                IsBusy = false;
                await ReloadAsync(token, quiet: true, bypassGuard: true).ConfigureAwait(true);
                return;
            }

            DeleteBlock = UnitRequestBuilder.ParseDeleteConflict(response.Error);
            StatusText = IsDeleteBlocked
                ? $"'{row.Name}'에 연결된 항목이 있어 삭제할 수 없습니다. 대신 [운용 중지]를 쓰세요."
                : $"'{row.Name}'을(를) 삭제하지 못했습니다. {Reason(response.Error?.Message, response.Message)}";
        }
        catch (OperationCanceledException) { StatusText = "삭제를 취소했습니다."; }
        catch (Exception ex)
        {
            _log?.Error($"[UnitConsole] delete {row.Id}: {ex.Message}");
            StatusText = $"삭제하지 못했습니다. {UNREACHABLE}";
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
                ? $"'{row.Name}'을(를) 운용 중지했습니다. 이력과 소속은 그대로 남습니다."
                : $"운용 중지하지 못했습니다. {Reason(response.Error?.Message, response.Message)}";
            if (!response.Success) return;

            DeleteBlock = null;
            IsBusy = false;
            await ReloadAsync(token, quiet: true, bypassGuard: true).ConfigureAwait(true);
            await SelectByIdAsync(row.Id, token, force: true).ConfigureAwait(true);
        }
        catch (OperationCanceledException) { StatusText = "운용 중지를 취소했습니다."; }
        catch (Exception ex)
        {
            _log?.Error($"[UnitConsole] disable {row.Id}: {ex.Message}");
            StatusText = $"운용 중지하지 못했습니다. {UNREACHABLE}";
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
        if (Tray.IsApplying) { StatusText = "저장하는 중에는 대기 목록에 더 담을 수 없습니다."; return; }

        if (!CanPlaceDevices) { StatusText = UnitDropHandler.PlaceDeniedReason; return; }

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
            ? $"'{target.Name}'에 배치할 {capped.Count}대를 대기 목록에 담았습니다. 한 번에 {MAX_ASSIGN_PER_APPLY}대까지만 담을 수 있어 {dropped}대는 뺐습니다."
            : $"'{target.Name}'에 배치할 {capped.Count}대를 대기 목록에 담았습니다 — [적용]을 누르면 저장됩니다.";
        RaiseTray();
    }

    private async Task<DraftOutcome> AssignOneAsync(UnitDeviceItem item, int unitId, CancellationToken token)
    {
        // 앞이 실패했으면 그 뒤는 보내지 않는다 — 폭발반경을 실패 지점에서 끊는다.
        // Skipped 가 아니라 Failed 다: 커널 트레이는 Skipped 를 목록에서 <b>지우고</b> Failed 만 남긴다.
        // 지워지면 "남겨 뒀다" 는 안내가 거짓이 되고 실패분만 다시 보내는 길도 사라진다.
        if (_assignStopped) { _assignSkipped.Add(item.Name); return DraftOutcome.Failed; }

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
            if (_assignFailure is not null) line += $" · 처음 실패한 곳에서 멈췄습니다 — {_assignFailure}";
            if (_assignSkipped.Count > 0) line += $" · 저장하지 않은 {_assignSkipped.Count}대는 대기 목록에 남아 있습니다([적용]을 다시 누르면 그것만 저장합니다)";
            StatusText = line;

            IsBusy = false;
            await ReloadAsync(token, quiet: true, bypassGuard: true).ConfigureAwait(true);   // 성공분 반영은 재조회로 확정한다
        }
        catch (OperationCanceledException) { StatusText = "배치를 취소했습니다."; }
        catch (Exception ex)
        {
            _log?.Error($"[UnitConsole] apply assigns: {ex.Message}");
            StatusText = $"배치하지 못했습니다. {UNREACHABLE}";
        }
        finally { IsBusy = false; RaiseTray(); RaiseCommands(); }
    }

    public void RevertAssigns()
    {
        Tray.Revert();
        foreach (var row in DeviceRows) row.PendingUnitName = null;
        StatusText = "대기 목록을 비웠습니다.";
        RaiseTray();
    }

    /// <summary>끌기의 버튼 폴백 — 고른 장비를 지금 고른 부대에 쌓는다.</summary>
    public void QueueAssignSelected()
    {
        var target = AssignTargetId;
        if (target <= 0) { StatusText = "놓을 부대를 트리나 아래 목록에서 고르세요."; return; }
        QueueAssign(target, SelectedDevices);
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
                StatusText = "끌어 놓은 항목을 처리하지 못했습니다. 다시 시도하세요.";
            }
        });
    }
    #endregion

    #region - Helpers -
    public async Task SelectByIdAsync(int unitId, CancellationToken token = default, bool force = false)
    {
        if (unitId <= 0) return;
        var node = Tree.Find(unitId);
        if (node is null) return;
        await SelectRowAsync(RowOf(node), token, force).ConfigureAwait(true);
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
        if (row.IsExpanded) _collapsed.Remove(row.Id);
        else _collapsed.Add(row.Id);
        Project();
    }

    /// <summary>서버에 닿지 못했을 때 붙이는 말.</summary>
    internal const string UNREACHABLE = "서버 연결을 확인하세요.";

    /// <summary>서버가 거절했을 때 붙이는 말 — 서버 원문 대신 이 문장을 보인다.</summary>
    internal const string REJECTED = "잠시 후 다시 시도하세요.";

    /// <summary>
    /// 서버 거절 사유. 원문(영문 코드 · 서버 메시지)은 <b>화면에 붙이지 않고</b> 로그로 보낸다(U-18 공통 규칙) —
    /// 화면에는 운영자가 할 일을 담은 고정 문장만 남는다.
    /// </summary>
    private string Reason(string? errorMessage, string? message)
    {
        var raw = string.IsNullOrWhiteSpace(errorMessage) ? message : errorMessage;
        if (!string.IsNullOrWhiteSpace(raw)) _log?.Warning($"[UnitConsole] 서버 거절 원문: {raw}");
        return REJECTED;
    }

    private void RaiseDetail()
    {
        NotifyOfPropertyChange(nameof(IsDetailRequested));
        RaiseCommands();
    }

    private void SetRailCount(string key, int count)
    {
        var entry = RailEntries.FirstOrDefault(e => e.Key == key);
        if (entry is not null) entry.Count = count;
    }

    private void RaiseTray()
    {
        NotifyOfPropertyChange(nameof(Tray));
        NotifyOfPropertyChange(nameof(TrayMessageText));
        RaiseCommands();
    }

    /// <summary>
    /// 배치 대기 목록의 안내 한 줄. 커널 트레이의 문구("Draft …")는 운영자 말이 아니라 여기서 따로 만든다(U-18 D-8 8.7).
    /// </summary>
    public string TrayMessageText => Tray.IsApplying
        ? $"저장하는 중입니다… ({Tray.ProgressDone}/{Tray.ProgressTotal})"
        : $"배치 대기 {Tray.Count}건 — [적용]을 누르면 저장됩니다.";

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
        NotifyOfPropertyChange(nameof(AssignTargetId));
        NotifyOfPropertyChange(nameof(AssignTargetText));
        NotifyOfPropertyChange(nameof(CanUndoMove));
        NotifyOfPropertyChange(nameof(CanEditUnits));
        NotifyOfPropertyChange(nameof(CanViewUnits));
        NotifyOfPropertyChange(nameof(CanPlaceDevices));
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
    private readonly Func<bool> _canView;
    private readonly Func<bool> _canPlaceDevices;

    private readonly Dictionary<int, UnitNodeRowViewModel> _rowCache = new();
    private readonly Dictionary<int, UnitDeviceRowViewModel> _deviceRowCache = new();
    private readonly List<string> _assignSkipped = new();
    private readonly HashSet<int> _collapsed = new();

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
    private int _detailTicket;
    private int? _assignTargetUnitId;
    private bool _isFiltered;
    #endregion
}
