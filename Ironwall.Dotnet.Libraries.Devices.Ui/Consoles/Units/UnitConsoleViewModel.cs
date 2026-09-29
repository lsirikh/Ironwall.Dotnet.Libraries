using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;

/****************************************************************************
   Purpose      : 부대 콘솔 — 편제 트리 · 부대 관계도 · 상세 (N-11)
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
/// 상위 바꾸기·인접은 호출 <b>1회</b>라 드롭 즉시 보내고 되돌리기를 준다.</para>
/// <para><b>형제 순서 드래그는 없다</b> — 서버 계약에 순서 필드가 아예 없어 끌어도 저장되지 않는다
/// (스토리보드 L359-361 · 드래그 와이어프레임 L446).</para>
/// <para><b>「미배치 장비」 칸은 없다</b>(서버 회신 2026-09-28 Q-1 ⓐ 개념 폐지) — 서버 설계상 무소속 장비가 존재할 수 없다
/// (<c>unit_id</c> 없이 만들면 기본 부대 <c>unit001</c>, PATCH/PUT 의 명시적 <c>null</c> 은 422). 장비는 여전히 전량 읽는다 —
/// 트리 행의 장비 수 · 관계도 배지 · [지도에서 보기]의 원천이다. 옛 설정의 <c>LastRailKey="devices"</c> 는 첫 칸(편제 트리)으로 떨어진다.</para>
/// </remarks>
public sealed class UnitConsoleViewModel : Screen, IHandle<UnitTopologyChangedMessage>, IUnitMapCommands, IUnitMapConsoleBridge
{
    public const string RAIL_TREE = "tree";
    public const string RAIL_ADJACENCY = "adjacency";

    /// <summary>창 제목(<see cref="Screen.DisplayName"/>).</summary>
    public const string WINDOW_TITLE = "부대 편제";

    /// <summary>편집 권한이 없을 때 상태 띠 · 툴팁에 쓰는 말(권한 키 이름은 보이지 않는다 — U-18 D-8 8.4).</summary>
    public const string NO_EDIT_PERMISSION = "부대를 편집할 권한이 없습니다.";

    /// <summary>서버가 부대 편제를 모를 때(판본 번호는 보이지 않는다 — U-18 D-8 8.9).</summary>
    public const string NOT_SUPPORTED = "현재 서버는 부대 편제를 지원하지 않습니다.";

    /// <summary>다른 곳(다른 창 · 다른 운영자)에서 편제가 바뀌었는데 지금은 다시 읽지 못할 때 상태 띠에 보이는 말.</summary>
    public const string EXTERNAL_CHANGE_NOTICE = "다른 곳에서 편제가 바뀌었습니다.";

    /// <summary>그 옆 단추의 글 — 누르면 [갱신]과 같은 길(미적용 관문 포함)로 다시 읽는다.</summary>
    public const string EXTERNAL_CHANGE_ACTION = "다시 읽기";

    /// <summary>개인 표시 설정(<see cref="ConsolePrefs"/>)의 이 콘솔 키 — 셸 <c>ConsoleKey</c> 와 같다.</summary>
    public const string PREFS_KEY = "Units";

    /// <summary>관계도 확인 오버레이가 떠 있는 동안 다른 조작을 막을 때의 말(#49 — 콘솔 전체 모달).</summary>
    public const string CONFIRM_PENDING_NOTICE = "관계도에서 확인을 기다리는 중입니다 — [확인] 또는 [취소]를 먼저 누르세요.";

    /// <summary>고른 부대가 재조회 뒤 편제에 없다(ISSUE-29 — 모든 재조회가 같은 말).</summary>
    public const string SELECTED_UNIT_GONE = "고른 부대가 다른 곳에서 삭제되었습니다.";

    /// <summary>관계도에서 고르려 했는데 상세에 적용하지 않은 변경이 있다(v1.3 FR-02 — 가드가 막고 띠로 알린다).</summary>
    public const string SELECT_BLOCKED_BY_EDIT = "적용하지 않은 변경이 있어 다른 부대를 고르지 않았습니다 — [적용] 또는 [되돌리기] 후 다시 고르세요.";

    /// <summary>합침 창이 계속 다시 열려도 첫 알림에서 이만큼 지나면 곧바로 한 번 읽는다(ISSUE-30).</summary>
    public static readonly TimeSpan EXTERNAL_CHANGE_MAX_WAIT = TimeSpan.FromSeconds(2);

    #region - Ctors -
    public UnitConsoleViewModel(
        IUnitGraphApi units,
        IUnitDeviceApi devices,
        ILogService? log = null,
        Func<string?>? myUnitCode = null,
        Func<bool>? canEdit = null,
        Func<bool>? canDelete = null,
        Func<bool>? canView = null,
        IEventAggregator? events = null,
        Func<bool>? isDragging = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        IUnitLayoutApi? layoutApi = null,
        ConsolePrefEntry? prefs = null,
        System.Action? savePrefs = null,
        IClock? clock = null,
        Func<bool>? isLiveOff = null,
        Func<string?>? operatorName = null)
    {
        _units = units ?? throw new ArgumentNullException(nameof(units));
        _devices = devices ?? throw new ArgumentNullException(nameof(devices));
        _log = log;
        _myUnitCode = myUnitCode ?? (() => null);
        // 서버가 부대 편제를 지키는 모듈 그대로 — units:edit · units:delete · units:view(permission_map.py).
        _canEdit = canEdit ?? UnitPermissionGate.CanEdit;
        _canDelete = canDelete ?? UnitPermissionGate.CanDelete;
        _canView = canView ?? UnitPermissionGate.CanView;
        _events = events;
        _isDragging = isDragging ?? (() => DragSession.IsActive);
        // SYNC_UNIT 는 PUT 한 번에 여러 건이 몰려온다 — 창(500 ms) 안의 알림을 재조회 한 번으로 합친다.
        _externalChange = new CoalescingTrigger(OnExternalChangeSettledAsync, delay: delay,
                                                onError: ex => _log?.Error($"[UnitConsole] 외부 변경 재조회: {ex.Message}"));
        _clock = clock ?? new SystemClock();
        _delay = delay ?? Task.Delay;
        _prefs = prefs;
        _savePrefs = savePrefs;

        // 창 제목 — Caliburn 창 관리자가 DisplayName 을 Title 로 묶는다. 비우면 타입 이름이 뜬다(U-18 D-0 0.2 · D-8 8.1).
        DisplayName = WINDOW_TITLE;

        Detail = new ConsoleDetailPresenter { TypeName = "부대" };
        Form = new UnitDetailFormViewModel(Detail);
        Drop = new UnitDropHandler(() => Tree, () => SelectedRow?.Id ?? 0, () => _canEdit(), () => IsBusy, OnDropped,
                                   reason => StatusText = reason);

        // 아이콘은 이름만 쥔 토큰이다 — 싱글턴이 아닌 창이어도 뷰모델이 시각 요소를 쥐지 않는다(장비 콘솔 선례).
        // 「부대 관계도」 칸(unit-relationship-map FR-01). 키는 옛 RAIL_ADJACENCY 그대로라 옛 설정의 LastRailKey="adjacency" 도 유효하다.
        // 숫자 배지는 달지 않는다 — 편제 트리 칸의 배지가 부대 수라 여기 숫자가 있으면 부대 수로 읽힌다(조정자).
        // 인접 쌍 수는 목록 상태 줄이 "인접 N쌍"으로 말한다.
        // 「미배치 장비」 칸은 없다(서버 회신 Q-1 ⓐ — PRD v1.7). 옛 설정의 LastRailKey="devices" 는 아래 폴백으로 첫 칸에 떨어진다.
        RailEntries.Add(new ConsoleRailEntry(RAIL_TREE, "편제 트리", new ConsoleIconToken("FileTree")) { ShowCount = true });
        RailEntries.Add(new ConsoleRailEntry(RAIL_ADJACENCY, "부대 관계도", new ConsoleIconToken("SitemapOutline")) { ShowCount = false });
        _selectedRail = RailEntries.FirstOrDefault(e => e.Key == prefs?.LastRailKey) ?? RailEntries[0];

        // 관계도 — 콘솔이 소유한다(자식). 선택 · 편제 쓰기는 이 콘솔의 기존 경로 한 벌(D-5)을 IUnitMapCommands 로 쓴다.
        // 실시간 꼬리표 · "나" 는 호스트가 준 값(REVIEW-01 MEDIUM-6 — 종전엔 넘기지 않아 꼬리표가 뜨지 않고 내 변경도 이름으로 보였다).
        // 알림 통로(events)가 없으면 배치 알림을 들을 길이 없다 — 그때는 꺼짐이다.
        Map = new UnitMapViewModel(this, layoutApi ?? new UnitLayoutApiAdapter(null), new UnitMapViewModelOptions
        {
            Events = events,
            Delay = delay,
            Prefs = prefs,
            SavePrefs = savePrefs,
            Console = this,
            MyUnitIdProvider = () => MyUnitId,
            Clock = _clock,
            IsLiveOff = isLiveOff ?? (() => events is null),
            CurrentOperatorName = SafeOperatorName(operatorName),
            Log = log,
        });
        Map.PropertyChanged += OnMapPropertyChanged;
        Map.DefersReloadChanged += OnMapDefersReloadChanged;

        EchelonFilters.Add(new UnitEchelonFilterViewModel(null, "전체") { IsSelected = true });
        foreach (var echelon in Enum.GetValues<EnumUnitEchelon>())
            EchelonFilters.Add(new UnitEchelonFilterViewModel(echelon, UnitDropRules.EchelonText(echelon)));

        Detail.Tracker.Changed += (_, _) => RaiseDetail();
    }
    #endregion

    #region - Kernel pieces -
    /// <summary>
    /// 사람에게 묻는 확인 창(제목, 문장 → 예/아니오). 창을 여는 쪽(<see cref="UnitConsoleLauncher"/>)이 넣는다 — 뷰모델은 창 관리자를 모른다.
    /// 비어 있으면(헤드리스 시험) 묻지 않고 진행한다.
    /// <para>2026-09-27 전수 조사: 삭제 · 운용 중지가 확인 없이 곧바로 서버에 나갔고, 창을 닫으면 적용하지 않은 변경이 말없이 버려졌다.</para>
    /// </summary>
    public Func<string, string, Task<bool>>? Confirm { get; set; }

    private Task<bool> AskAsync(string title, string message) => Confirm is null ? Task.FromResult(true) : Confirm(title, message);

    /// <summary>
    /// 닫기 전 관계도의 쓰기(배치 · 편제)가 끝나기를 기다리는 한도(REVIEW-01 L-7). 넘으면 묻는다 — 말없이 닫으면 결과를 아무도 보지 못한다.
    /// </summary>
    public static readonly TimeSpan CLOSE_WRITE_WAIT = TimeSpan.FromSeconds(3);

    /// <summary>창을 닫아도 되는가 — 관계도가 아직 쓰는 중이면 잠깐 기다렸다 묻고, 적용하지 않은 상세 변경이 있으면 묻는다.</summary>
    public override async Task<bool> CanCloseAsync(CancellationToken cancellationToken = default)
    {
        Map.CancelAll();   // 관계도 확인 오버레이 · M 모드 · 끌기는 서버 0 으로 먼저 거둔다(IMPL-31 — 아래 가드는 그대로)
        if (Map.IsWriting && !await Map.WaitForWritesAsync(CLOSE_WRITE_WAIT).ConfigureAwait(true))
        {
            // 닫으면 아직 시작하지 않은 쓰기는 보내지 않고(관계도 Dispose), 나간 요청의 결과는 이 창에서 볼 수 없다.
            if (!await AskAsync("부대 편제 닫기", "관계도의 저장이 아직 끝나지 않았습니다.\n지금 닫으면 기다리는 변경은 보내지 않고, 보낸 변경의 결과는 확인할 수 없습니다. 닫을까요?").ConfigureAwait(true))
                return false;
        }
        if (!Detail.IsDirty) return true;
        return await AskAsync("부대 편제 닫기", "적용하지 않은 부대 정보 변경이 있습니다.\n닫으면 사라집니다. 버리고 닫을까요?").ConfigureAwait(true);
    }

    public ConsoleDetailPresenter Detail { get; }
    public UnitDetailFormViewModel Form { get; }
    public UnitDropHandler Drop { get; }

    public BindableCollection<ConsoleRailEntry> RailEntries { get; } = new();
    public BindableCollection<UnitEchelonFilterViewModel> EchelonFilters { get; } = new();
    public BindableCollection<UnitNodeRowViewModel> Rows { get; } = new();
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
            if (BlockedByConfirm()) { NotifyOfPropertyChange(); return; }     // #49 — 확인 대기 중 레일 전환 막힘
            // 레일 전환은 미적용 변경을 버릴 수 있다 — 커널 관문을 먼저 지난다.
            if (!Detail.Guard.TryNavigate(ConsoleNavigation.SwitchRail)) { NotifyOfPropertyChange(); return; }
            _selectedRail = value;
            NotifyOfPropertyChange();
            RaiseViewFlags();
            Project();
            if (_prefs is not null)
            {
                _prefs.LastRailKey = value.Key;
                _savePrefs?.Invoke();
            }
        }
    }

    public bool IsTreeView => _selectedRail.Key == RAIL_TREE;

    /// <summary>「부대 관계도」 칸을 보고 있다(unit-relationship-map FR-01).</summary>
    public bool IsAdjacencyView => _selectedRail.Key == RAIL_ADJACENCY;

    /// <summary>관계도 뷰모델(캔버스의 <c>Interaction</c>) — 이 콘솔이 소유한다.</summary>
    public UnitMapViewModel Map { get; }

    /// <summary>관계도 확인 오버레이가 떠 있다 — 콘솔 전체가 모달이다(#49).</summary>
    public bool IsStructureConfirmPending => Map.IsConfirming;

    /// <summary>트리 · 상세 · 툴바를 만질 수 있다(확인 대기 중이 아니다) — 뷰가 IsEnabled 로 묶는다.</summary>
    public bool IsConsoleInteractive => !IsStructureConfirmPending;

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
        private set
        {
            var changed = _selectedRow?.Id != value?.Id;
            _selectedRow = value;
            NotifyOfPropertyChange();
            RaiseCommands();
            if (changed) SelectedUnitChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            var changed = _isBusy != value;
            _isBusy = value;
            NotifyOfPropertyChange();
            RaiseCommands();
            if (changed) BusyChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string StatusText { get => _statusText; private set { _statusText = value; NotifyOfPropertyChange(); } }

    /// <summary>트리에 그릴 행이 없다 — 필터 때문인지 편제가 빈 것인지 글로 가른다.</summary>
    public bool IsTreeEmpty => Rows.Count == 0;

    /// <remarks>인접 쌍 수는 관계도 칸에서만 말한다 — 관계도 칸에는 숫자 배지를 달지 않는다(FR-01).</remarks>
    public string ListStatusText => IsAdjacencyView
        ? $"부대 {Tree.Count} · 인접 {AdjacencyPairCount}쌍"
        : $"부대 {Tree.Count}";

    /// <summary>
    /// 레일 바닥 — 내 부대를 <b>이름</b>으로 말한다(V-39: 종전엔 "내 부대 · unit001" 처럼 코드를 그대로 냈다).
    /// 편제에서 그 코드를 못 찾으면(아직 안 읽었거나 다른 서버) 내 부대를 말하지 않고 정렬 안내를 낸다.
    /// </summary>
    public string RailFooterText => MyUnitName is { Length: > 0 } name
        ? $"내 부대 · {name}"
        : "같은 단계의 부대는 코드 순으로 표시됩니다.";

    /// <summary>편제에서 찾은 내 부대 이름 — 없으면 <c>null</c>.</summary>
    public string? MyUnitName
    {
        get
        {
            var code = MyUnitCode;
            if (string.IsNullOrEmpty(code)) return null;
            var node = Tree.Ordered.FirstOrDefault(n => string.Equals(n.Code, code, StringComparison.Ordinal));
            return string.IsNullOrWhiteSpace(node?.Name) ? null : node!.Name;
        }
    }

    /// <summary>
    /// 머리 부제 — 다른 콘솔처럼 <b>지금 레일 이름</b>(V-38 · V-39). 종전엔 내 부대 코드("unit001")를 부제로 냈다.
    /// </summary>
    public string RailSubtitle => _selectedRail.Label;

    public string? MyUnitCode => _myUnitCode();

    /// <summary>편제에서 찾은 내 부대 id — 관계도 ★ · <c>Home</c> · 첫 화면.</summary>
    public int? MyUnitId
    {
        get
        {
            var code = MyUnitCode;
            if (string.IsNullOrEmpty(code)) return null;
            return Tree.Ordered.FirstOrDefault(n => string.Equals(n.Code, code, StringComparison.Ordinal))?.Id;
        }
    }

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

    public bool IsDetailRequested => Detail.IsDetailRequested;
    #endregion

    #region - Lifecycle -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        _closed = false;
        // 창이 떠 있는 동안만 듣는다 — 열 때마다 새로 만드는 창이라 닫힌 뒤 구독이 남으면 창이 수거되지 않는다.
        _events?.SubscribeOnUIThread(this);
        if (_loadedOnce) return;
        _loadedOnce = true;
        await ReloadAsync(cancellationToken);
        // 배치 문서 1회(지원 판정 겸 — FR-50). 관계도 칸을 열지 않아도 읽어 둔다: 트리에서 옮긴 부대의 배치 정리(FR-08)가 판정을 쓴다.
        // 기다리지 않는다(REVIEW-01 MEDIUM-8) — 창 관리자는 활성화가 끝나야 창을 보이므로, 기다리면 배치 GET 이 느린 동안 창이 뜨지 않는다.
        // 관계도는 자동 배치로 먼저 그리고(NFR-01) 응답이 오면 입힌다. 읽기는 관계도의 단일 대기열을 타고, 실패는 막대 · [다시 시도]가 말한다.
        // 활성화 토큰은 넘기지 않는다 — 활성화가 끝난 뒤에도 사는 읽기다(닫으면 관계도 Dispose 가 거둔다).
        _ = Map.OpenAsync(CancellationToken.None);
    }

    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        if (close)
        {
            _closed = true;
            _events?.Unsubscribe(this);
            _externalChange.Cancel();
            Map.Dispose();     // 개인 뷰 저장 · 합침 중단 · 구독 해제
        }
        await base.OnDeactivateAsync(close, cancellationToken);
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
    public Task ReloadAsync(CancellationToken token = default, bool quiet = false, bool bypassGuard = false)
    {
        // [갱신] 은 확인 대기 중 막는다(#49). 서버가 바꾼 것을 다시 읽는 길(bypassGuard)은 막지 않는다.
        if (!bypassGuard && BlockedByConfirm()) return Task.CompletedTask;
        return ReloadCoreAsync(token, quiet, bypassGuard, includeDevices: true);
    }

    /// <summary>
    /// 편제만 다시 읽는다(장비 전량은 그대로 — ISSUE-28). 다른 곳의 편제 변경 · 편제 쓰기 뒤 · 관계도의 확정 직전 재판정이 쓴다.
    /// 장비는 [갱신] · 창 열기에서만 읽는다(카테고리 7종 · 여러 쪽이라 무겁다).
    /// </summary>
    public Task ReloadGraphAsync(CancellationToken token = default, bool quiet = true)
        => ReloadCoreAsync(token, quiet, bypassGuard: true, includeDevices: false);

    /// <summary>
    /// 띠의 [다시 읽기] — 편제만 다시 읽어 그림을 새로 하고 <b>상세의 미적용 편집은 그대로</b> 둔다(결정 D-2026-09-27-215b6d · ISSUE-29).
    /// </summary>
    public async Task ReadExternalChangeAsync(CancellationToken token = default)
    {
        if (BlockedByConfirm()) return;
        await ReloadGraphAsync(token).ConfigureAwait(true);
    }

    private async Task ReloadCoreAsync(CancellationToken token, bool quiet, bool bypassGuard, bool includeDevices)
    {
        if (!bypassGuard && !Detail.Guard.TryNavigate(ConsoleNavigation.Refresh)) return;
        if (IsBusy) { _reloadPending = true; return; }     // 버리지 않는다 — 앞선 작업이 끝나면 한 번(ISSUE-27)
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
        var lost = false;
        try
        {
            var response = await _units.GetGraphAsync(token).ConfigureAwait(true);
            if (!response.Success)
            {
                StatusText = $"편제를 불러오지 못했습니다. {Reason(response.Error?.Message, response.Message)}";
                return;
            }

            Tree = UnitTreeBuilder.Build(response.Data);
            IsExternallyChanged = false;          // 방금 서버의 지금 편제를 받았다 — "바뀌었다" 안내는 더 이상 참이 아니다
            AdjacencyPairCount = response.Data?.Edges?.AdjacencyPairs.Count() ?? 0;

            if (includeDevices && _devices.IsAvailable)
            {
                var loaded = await _devices.LoadAllAsync(token).ConfigureAwait(true);
                _allDevices = loaded.Items;
                if (loaded.HasFailure) _log?.Warning($"[UnitConsole] 장비 일부를 읽지 못했습니다 — {string.Join(" · ", loaded.Failures)}");
            }

            // 되돌리기는 재조회를 넘어 살아남는다 — 반대 방향 PATCH 한 번이라 화면을 다시 읽어도 여전히 유효하다.
            DeleteBlock = null;
            lost = RestoreSelection();
            Project();
            // 고른 행의 통지는 Project 뒤다 — 목록에 아직 없는 인스턴스를 밀면 ListBox 가 그냥 버린다.
            NotifyOfPropertyChange(nameof(SelectedRow));
            Map.SetData(Tree, _allDevices);
            if (lost)
            {
                // 고른 부대가 사라졌다(ISSUE-29 — 어느 재조회든 이 한 곳) — 없는 부대의 상세를 붙들면 다음 적용이 404 로 간다.
                Form.Clear();
                Detail.Reset();
                RaiseDetail();
                SelectedUnitChanged?.Invoke(this, EventArgs.Empty);
                StatusText = SELECTED_UNIT_GONE;
            }
            else if (!quiet) StatusText = $"부대 {Tree.Count}개를 불러왔습니다.";
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
            if (_reloadPending && !_closed)
            {
                _reloadPending = false;
                _externalChangeTask = _externalChange.Pulse();
            }
        }
    }
    #endregion

    #region - 다른 곳에서 바뀐 편제 (SYNC_UNIT) -
    /// <summary>
    /// 다른 곳에서 편제가 바뀌었는데 <b>지금 다시 읽지 않았다</b> — 적용하지 않은 편집 · 끌기 중이라서.
    /// 상태 띠에 <see cref="ExternalChangeText"/> 와 [<see cref="EXTERNAL_CHANGE_ACTION"/>] 단추가 뜬다.
    /// </summary>
    public bool IsExternallyChanged
    {
        get => _isExternallyChanged;
        private set { if (_isExternallyChanged == value) return; _isExternallyChanged = value; NotifyOfPropertyChange(); }
    }

    public string ExternalChangeText => EXTERNAL_CHANGE_NOTICE;
    public string ExternalChangeActionText => EXTERNAL_CHANGE_ACTION;

    /// <summary>
    /// 서버 <c>SYNC_UNIT</c> — 호스트가 옮겨 온다. 여기서는 신호만 세고 곧바로 돌아간다
    /// (호스트의 NATS 처리 줄이 창의 재조회를 기다리지 않도록). 실제 판단은 창이 끝난 뒤 한 번 한다.
    /// </summary>
    /// <remarks><c>ResourceId</c> 는 보지 않는다 — 인접 알림에서는 판본마다 뜻이 다르다(8.0.3 부터 쌍의 낮은 쪽 부대 id, 그 전 판은 인접 행 id —
    /// 서버 회신 2026-09-28 R-3). 늘 편제 전체를 다시 읽고, 알림의 순서 · 건수에 기대지 않는다(R-6 — subject 가 다르면 순서 미보장).</remarks>
    public Task HandleAsync(UnitTopologyChangedMessage message, CancellationToken cancellationToken)
    {
        if (_closed) return Task.CompletedTask;
        var now = _clock.UtcNow;
        _firstExternalChangeAt ??= now;
        if (now - _firstExternalChangeAt.Value >= EXTERNAL_CHANGE_MAX_WAIT)
        {
            // 최대 대기(ISSUE-30) — 400ms 간격으로 계속 오면 뒤끝 창이 영영 닫히지 않는다. 창을 끊고 지금 한 번.
            _externalChange.Cancel();
            _externalChangeTask = OnExternalChangeSettledAsync(CancellationToken.None);
            return Task.CompletedTask;
        }
        _externalChangeTask = _externalChange.Pulse();
        return Task.CompletedTask;
    }

    /// <summary>가장 최근 신호의 작업 — 시험이 창이 끝나기를 기다릴 때 쓴다.</summary>
    internal Task ExternalChangeTask => _externalChangeTask;

    /// <summary>적용하지 않은 것이 있다 — 다시 읽으면 사라진다(상세 · 등록 폼).</summary>
    private bool HasUnappliedWork => Detail.IsDirty;

    private async Task OnExternalChangeSettledAsync(CancellationToken token)
    {
        if (_closed || !_loadedOnce) return;

        // 끌기 · 다른 작업 · 관계도의 확인 오버레이 · M 모드 · 관계도 끌기 중이면 뒤로 미룬다(#49 · ISSUE-9)
        // — 끝나면 다음 창에서(관계도는 DefersReloadChanged 로도) 다시 읽는다.
        if (_isDragging() || IsBusy)
        {
            IsExternallyChanged = true;
            _externalChangeTask = _externalChange.Pulse();
            return;
        }
        if (Map.DefersReload)
        {
            // 관계도는 끝남을 알린다(DefersReloadChanged) — 창을 다시 열어 두지 않는다(열어 두면 확인 창 내내 합침이 돈다).
            IsExternallyChanged = true;
            return;
        }

        _firstExternalChangeAt = null;

        // 사람이 손댄 것은 덮지 않는다 — 그림도 그대로 두고 알린다(결정 D-2026-09-27-215b6d). 사람이 [다시 읽기]로 고른다.
        if (HasUnappliedWork)
        {
            IsExternallyChanged = true;
            return;
        }

        var selectedId = SelectedRow?.Id;
        var before = Tree;
        // 조용히 · 관문 없이 · 편제만(ISSUE-28) — 방금 한 일의 결과 문장을 지우지 않고, 미적용 관문은 위에서 이미 확인했다.
        await ReloadGraphAsync(token).ConfigureAwait(true);
        if (ReferenceEquals(Tree, before) || Detail.IsCreating || selectedId is not int id) return;   // 못 읽었거나 등록 중
        if (SelectedRow is null) return;      // 사라진 부대는 재조회가 한 곳(RestoreSelection)에서 알렸다
        // 같은 부대라도 다시 읽는다 — 옛 상세를 붙들면 다음 PATCH 가 이미 바뀐 칸을 옛 값으로 되돌린다.
        await SelectByIdAsync(id, token, force: true).ConfigureAwait(true);
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

        // 칸을 순번이 아니라 키로 찾는다 — 칸 구성이 판마다 바뀌었다(U-18 D-8 8.2 · Q-1 「미배치 장비」 폐지).
        SetRailCount(RAIL_TREE, Tree.Count);
        SetRailCount(RAIL_ADJACENCY, AdjacencyPairCount);

        NotifyOfPropertyChange(nameof(ListStatusText));
        NotifyOfPropertyChange(nameof(RailFooterText));
        NotifyOfPropertyChange(nameof(MyUnitName));
        NotifyOfPropertyChange(nameof(IsTreeEmpty));
    }

    private UnitNodeRowViewModel RowOf(UnitTreeNode node)
    {
        if (_rowCache.TryGetValue(node.Id, out var existing) && ReferenceEquals(existing.Node, node)) return existing;

        var isMine = !string.IsNullOrEmpty(MyUnitCode) && string.Equals(node.Code, MyUnitCode, StringComparison.Ordinal);
        // 접힘은 _collapsed 가 정본이다 — 행 인스턴스에 기대면 재조회가 캐시를 비우는 순간 전부 펼쳐진다.
        var row = new UnitNodeRowViewModel(node, isMine) { IsExpanded = !_collapsed.Contains(node.Id) };
        _rowCache[node.Id] = row;
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
    /// <param name="force">
    /// <b>쓰기가 끝난 뒤</b>의 다시 읽기다. 재조회가 행 인스턴스를 새로 만들어도 <b>같은 부대</b>라
    /// 참조 비교 조기 반환에 걸려 상세가 영영 갱신되지 않는다 — 인접 칩이 그대로 남고,
    /// <see cref="UnitDetailFormViewModel.Original"/> 이 저장 전 DTO 를 붙들어 다음 PATCH 가
    /// <b>이미 저장된 칸을 다시 보낸다</b>.
    /// </param>
    public async Task SelectRowAsync(UnitNodeRowViewModel? row, CancellationToken token = default, bool force = false)
    {
        if (BlockedByConfirm()) { NotifyOfPropertyChange(nameof(SelectedRow)); return; }   // #49 — 목록 선택을 되돌린다
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

    /// <summary>
    /// 상세 읽기 — 읽는 중에 또 고르면 새로 보내지 않고 <b>마지막 선택 하나</b>만 이어서 읽는다(화살표 자동 반복 · 빠른 클릭의 GET 폭주 방지 — ISSUE-51).
    /// </summary>
    private Task LoadDetailAsync(UnitNodeRowViewModel row, CancellationToken token)
    {
        _wantedDetail = row;
        if (_detailLoop is { IsCompleted: false } running) return running;
        _detailLoop = DetailLoopAsync(token);
        return _detailLoop;
    }

    private async Task DetailLoopAsync(CancellationToken token)
    {
        while (_wantedDetail is { } row)
        {
            _wantedDetail = null;
            await LoadDetailCoreAsync(row, token).ConfigureAwait(true);
        }
    }

    /// <summary>진행 중인 상세 읽기(관계도 선택 포함)가 끝날 때까지.</summary>
    public async Task WhenDetailSettledAsync()
    {
        await _detailSettle.ConfigureAwait(true);
        if (_detailLoop is { } loop) await loop.ConfigureAwait(true);
    }

    private async Task LoadDetailCoreAsync(UnitNodeRowViewModel row, CancellationToken token)
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
            if (ticket != _detailTicket || _wantedDetail is not null) return;   // 그 사이 다른 부대를 골랐다(뒤에 이어 읽는다)
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

    /// <summary>재조회 뒤 선택을 새 행 인스턴스로 옮긴다. 고른 부대가 편제에서 사라졌으면 <c>true</c>(ISSUE-29 — 모든 재조회가 이 한 곳).</summary>
    private bool RestoreSelection()
    {
        var wanted = SelectedRow?.Id ?? 0;
        _rowCache.Clear();
        // 통지 없이 바꾼다 — Project 가 끝나 목록이 채워진 뒤에 한 번만 알린다(ReloadAsync).
        _selectedRow = wanted > 0 && Tree.Find(wanted) is { } node ? RowOf(node) : null;
        return wanted > 0 && _selectedRow is null;
    }
    #endregion

    #region - 등록 · 적용 · 되돌리기 -
    public void BeginCreate()
    {
        if (!CanAdd || BlockedByConfirm()) return;
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
        if (IsBusy || !Detail.CanApply || BlockedByConfirm()) return;

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
        // 실패 사유는 이번 시도의 것만 — 앞선 실패의 사유가 남아 관계도 막대가 엉뚱한 까닭을 말하지 않게(REVIEW-01 MEDIUM-4).
        if (BlockedByConfirm()) return FailStructureWrite(CONFIRM_PENDING_NOTICE);
        var verdict = UnitDropRules.CanMove(Tree, movingId, targetParentId);
        if (!verdict.IsAllowed) { StatusText = verdict.Reason!; return FailStructureWrite(verdict.Reason!); }
        if (!CanEditUnits) { StatusText = NO_EDIT_PERMISSION; return FailStructureWrite(NO_EDIT_PERMISSION); }
        if (IsBusy) { StatusText = BUSY_NOTICE; return FailStructureWrite(UnitMapText.ConsoleBusyReason); }

        var moving = Tree.Find(movingId)!;
        var previousParentId = moving.ParentId;
        var targetName = targetParentId is int id ? Tree.Find(id)?.Name ?? $"부대 {id}번" : "최상위";
        var fromMap = _structureFromMap;
        var affected = new[] { (int?)movingId, targetParentId, previousParentId };
        var selectedId = SelectedRow?.Id;

        IsBusy = true;
        try
        {
            var response = await _units.PatchAsync(movingId, UnitRequestBuilder.Move(targetParentId), token).ConfigureAwait(true);
            if (!response.Success)
            {
                LastWriteFailureReason = StructureReason(response.Error);
                StatusText = $"'{moving.Name}'을(를) 옮기지 못했습니다. {LastWriteFailureReason}";
                IsBusy = false;
                await ReloadGraphAsync(token).ConfigureAwait(true);   // 실패 복구는 재조회다(화면과 서버를 다시 맞춘다 · 504 면 실제 결과를 본다)
                return false;
            }

            LastWriteFailureReason = null;
            if (!fromMap) _lastMove = new UnitMoveUndo(movingId, moving.Name, previousParentId);
            // 관계도의 이동 · 되돌리기는 트리 [이동 되돌리기]를 채우지 않는다 — 관계도는 자기 표로 반대 이동을 보낸다(REVIEW-01 HIGH-1).
            // 같은 부대를 관계도가 다시 옮겼으면 트리의 되돌리기는 이제 그 이동을 말없이 지우게 되므로 거둔다.
            else if (_lastMove?.UnitId == movingId) _lastMove = null;
            StatusText = $"'{moving.Name}'을(를) '{targetName}'(으)로 옮겼습니다.";
            IsBusy = false;
            await ReloadGraphAsync(token).ConfigureAwait(true);
            // 선택을 강제로 바꾸지 않는다(#20) — 고른 부대가 영향 집합(끈 부대 · 새 상위 · 옛 상위)에 들 때만 상세를 다시 읽는다.
            if (selectedId is int sel && affected.Contains(sel) && SelectedRow?.Id == sel) await RefreshSelectedDetailAsync(token).ConfigureAwait(true);
            if (!fromMap)
            {
                // 트리 레일의 이동 — 관계도 되돌리기 표의 편제 항목은 이제 다른 이동을 가리킨다(#22), 배치 Δ 정리는 여기 한 곳(FR-08).
                Map.OnConsoleStructureChanged();
                await Map.CleanupAfterParentChangeAsync(movingId).ConfigureAwait(true);
            }
            return true;
        }
        catch (OperationCanceledException) { StatusText = "이동을 취소했습니다."; return FailStructureWrite("이동을 취소했습니다."); }
        catch (Exception ex)
        {
            _log?.Error($"[UnitConsole] move {movingId} → {targetParentId}: {ex.Message}");
            StatusText = $"'{moving.Name}'을(를) 옮기지 못했습니다. {UNREACHABLE}";
            return FailStructureWrite(UNREACHABLE);
        }
        finally { IsBusy = false; RaiseCommands(); }
    }

    /// <summary>바쁠 때 상태 띠의 말.</summary>
    private const string BUSY_NOTICE = UnitMapText.ConsoleBusyReason + " 잠시 후 다시 시도하세요.";

    /// <summary>편제 쓰기를 보내지 않았다 — 이번 시도의 사유를 남기고 <c>false</c>.</summary>
    private bool FailStructureWrite(string reason)
    {
        LastWriteFailureReason = reason;
        return false;
    }

    /// <summary>마지막 이동 1회를 되돌린다 — 반대 방향 PATCH 한 번.</summary>
    public async Task UndoMoveAsync(CancellationToken token = default)
    {
        if (_lastMove is not { } undo || BlockedByConfirm()) return;

        // 표를 미리 버리지 않는다 — 되돌리기가 실패하면 되돌릴 방법이 영영 사라진다.
        var ok = await MoveAsync(undo.UnitId, undo.PreviousParentId, token).ConfigureAwait(true);
        _lastMove = ok ? null : undo;
        if (ok) StatusText = $"'{undo.UnitName}'의 이동을 되돌렸습니다.";
        NotifyOfPropertyChange(nameof(CanUndoMove));
    }

    /// <summary>키보드 폴백 — Alt+↑ 는 한 단계 위로(부모의 부모, 없으면 최상위). 판정은 <see cref="UnitMovePlanner"/>(관계도와 같은 함수).</summary>
    public Task MoveSelectedUpAsync(CancellationToken token = default)
    {
        if (SelectedRow is not { } row || BlockedByConfirm()) return Task.CompletedTask;
        var plan = UnitMovePlanner.PlanMoveUp(Tree, row.Id);
        if (!plan.IsAllowed) { StatusText = plan.BlockedReason!; return Task.CompletedTask; }
        return MoveAsync(row.Id, plan.NewParentId, token);
    }

    /// <summary>
    /// 키보드 폴백 — Alt+↓ 는 바로 위 형제 밑으로(상위 제대일 때만). 후보는 <b>편제 전체의 트리 순서</b>에서 찾는다 —
    /// 보이는 행(접힘 · 제대 칩 · 검색)에서 찾으면 관계도와 뜻이 갈린다(ISSUE-34).
    /// </summary>
    public Task MoveSelectedDownAsync(CancellationToken token = default)
    {
        if (SelectedRow is not { } row || BlockedByConfirm()) return Task.CompletedTask;
        var plan = UnitMovePlanner.PlanMoveDown(Tree, row.Id);
        if (!plan.IsAllowed || plan.NewParentId is not int target) { StatusText = plan.BlockedReason!; return Task.CompletedTask; }
        return MoveAsync(row.Id, target, token);
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
        await ChangeAdjacencyAsync(row.Id, add, remove, token).ConfigureAwait(true);
    }

    /// <summary>
    /// <paramref name="sourceId"/> 기준 인접 추가 · 해제(#24) — 관계도에서 <b>선택하지 않은</b> 부대를 끌어 이어도 선택을 몰래 바꾸지 않는다.
    /// 비교 기준은 편제에서 읽은 <b>그 부대</b>의 인접이다(상세 폼이 아니다 — 폼은 선택 부대 것이다).
    /// </summary>
    public async Task<bool> ChangeAdjacencyAsync(int sourceId, int? add, int? remove, CancellationToken token = default)
    {
        if (BlockedByConfirm()) return FailStructureWrite(CONFIRM_PENDING_NOTICE);
        if (Tree.Find(sourceId) is not { } source) return FailStructureWrite("편제에 없는 부대입니다.");
        if (!CanEditUnits) { StatusText = "인접 부대를 바꿀 권한이 없습니다."; return FailStructureWrite(StatusText); }
        if (IsBusy) { StatusText = BUSY_NOTICE; return FailStructureWrite(UnitMapText.ConsoleBusyReason); }

        if (add is int addId)
        {
            var verdict = UnitDropRules.CanAdjoin(Tree, addId, sourceId);
            if (!verdict.IsAllowed) { StatusText = verdict.Reason!; return FailStructureWrite(verdict.Reason!); }
        }

        var fromMap = _structureFromMap;
        var selectedId = SelectedRow?.Id;
        IsBusy = true;
        try
        {
            // 저장 직전 재조회 — 동시 편집에서 나중 저장이 앞선 변경을 지우는 것을 막는다(스토리보드 L368).
            var fresh = await _units.GetDetailAsync(sourceId, token).ConfigureAwait(true);
            if (!fresh.Success || fresh.Data is null)
            {
                LastWriteFailureReason = StructureReason(fresh.Error);
                StatusText = $"'{source.Name}' 정보를 다시 불러오지 못해 인접 부대를 바꾸지 않았습니다. {LastWriteFailureReason}";
                return false;
            }

            var current = fresh.Data.AdjacentUnitIds ?? new List<int>();
            var known = source.AdjacentIds;
            if (!current.OrderBy(x => x).SequenceEqual(known.OrderBy(x => x)))
                StatusText = $"'{source.Name}'의 인접 부대가 그사이 바뀌어 최신 목록에 이어서 저장합니다.";

            var merged = UnitDropRules.MergeAdjacency(current, sourceId, add, remove);
            var response = await _units.PatchAsync(sourceId, UnitRequestBuilder.Adjacency(merged, sourceId), token).ConfigureAwait(true);
            if (!response.Success)
            {
                LastWriteFailureReason = StructureReason(response.Error);
                StatusText = $"인접 부대를 바꾸지 못했습니다. {LastWriteFailureReason}";
                IsBusy = false;
                await ReloadGraphAsync(token).ConfigureAwait(true);
                return false;
            }

            LastWriteFailureReason = null;
            var other = add ?? remove;
            var otherName = other is int id ? Tree.Find(id)?.Name ?? $"부대 {id}번" : string.Empty;
            StatusText = add is not null
                ? $"'{source.Name}'과(와) '{otherName}'을(를) 인접 부대로 이었습니다. 양쪽에 함께 표시됩니다."
                : $"'{source.Name}'과(와) '{otherName}'의 인접 관계를 끊었습니다.";
            // 관계도의 인접 조작 · 되돌리기는 콘솔의 인접 되돌리기를 채우지 않는다(이동과 같은 규칙 — REVIEW-01 HIGH-1).
            // 같은 쌍을 관계도가 바꿨으면 콘솔의 되돌리기는 그 조작을 말없이 지우게 되므로 거둔다.
            if (other is int otherId)
            {
                if (!fromMap) _lastAdjacency = (sourceId, otherId, add is not null);
                else if (_lastAdjacency is { } last && ((last.UnitId, last.OtherId) == (sourceId, otherId) || (last.UnitId, last.OtherId) == (otherId, sourceId)))
                    _lastAdjacency = null;
            }
            NotifyOfPropertyChange(nameof(LastAdjacency));
            NotifyOfPropertyChange(nameof(CanUndoAdjacency));

            IsBusy = false;
            await ReloadGraphAsync(token).ConfigureAwait(true);
            // 선택을 강제로 바꾸지 않는다(#20) — 고른 부대가 두 부대 중 하나일 때만 상세를 다시 읽는다.
            if (selectedId is int sel && (sel == sourceId || sel == other) && SelectedRow?.Id == sel) await RefreshSelectedDetailAsync(token).ConfigureAwait(true);
            if (!fromMap) Map.OnConsoleStructureChanged();
            return true;
        }
        catch (OperationCanceledException) { StatusText = "인접 부대 변경을 취소했습니다."; return FailStructureWrite("인접 부대 변경을 취소했습니다."); }
        catch (Exception ex)
        {
            _log?.Error($"[UnitConsole] adjacency {sourceId}: {ex.Message}");
            StatusText = $"인접 부대를 바꾸지 못했습니다. {UNREACHABLE}";
            return FailStructureWrite(UNREACHABLE);
        }
        finally { IsBusy = false; RaiseCommands(); }
    }

    /// <summary>마지막 인접 조작(부대, 상대, 이었는가) — 되돌리기 1회(#24).</summary>
    public (int UnitId, int OtherId, bool Added)? LastAdjacency => _lastAdjacency;

    public bool CanUndoAdjacency => _lastAdjacency is not null && !IsBusy;

    /// <summary>마지막 인접 조작을 반대로 1회 — 먼저 편제를 다시 읽어 이미 반영돼 있으면 보내지 않는다.</summary>
    public async Task UndoAdjacencyAsync(CancellationToken token = default)
    {
        if (_lastAdjacency is not { } last || BlockedByConfirm()) return;
        await ReloadGraphAsync(token).ConfigureAwait(true);
        var present = Tree.Find(last.UnitId)?.AdjacentIds.Contains(last.OtherId) == true;
        if (present != last.Added)
        {
            _lastAdjacency = null;
            StatusText = "인접 관계가 이미 다른 곳에서 바뀌어 되돌릴 것이 없습니다.";
            NotifyOfPropertyChange(nameof(LastAdjacency));
            return;
        }
        var ok = await ChangeAdjacencyAsync(last.UnitId, last.Added ? null : last.OtherId, last.Added ? last.OtherId : null, token).ConfigureAwait(true);
        if (ok) _lastAdjacency = null;
        NotifyOfPropertyChange(nameof(LastAdjacency));
        NotifyOfPropertyChange(nameof(CanUndoAdjacency));
    }
    #endregion

    #region - 삭제 · 운용 중지 -
    public async Task DeleteAsync(CancellationToken token = default)
    {
        if (SelectedRow is not { } row || !CanDeleteUnit || BlockedByConfirm()) return;
        if (!await AskAsync("부대 삭제", $"'{row.Name}' 부대를 삭제할까요?\n되돌릴 수 없습니다. 이력을 남기려면 삭제 대신 [운용 중지]를 쓰세요.").ConfigureAwait(true)) return;

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
        if (SelectedRow is not { } row || !CanEditUnits || IsBusy || BlockedByConfirm()) return;
        if (!await AskAsync("부대 운용 중지", $"'{row.Name}' 부대를 운용 중지할까요?\n이력과 소속은 그대로 남습니다.").ConfigureAwait(true)) return;

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

    private void RaiseViewFlags()
    {
        NotifyOfPropertyChange(nameof(RailSubtitle));
        NotifyOfPropertyChange(nameof(IsTreeView));
        NotifyOfPropertyChange(nameof(IsAdjacencyView));
        NotifyOfPropertyChange(nameof(ListStatusText));
    }

    private void RaiseCommands()
    {
        NotifyOfPropertyChange(nameof(CanAdd));
        NotifyOfPropertyChange(nameof(CanDeleteUnit));
        NotifyOfPropertyChange(nameof(CanReload));
        NotifyOfPropertyChange(nameof(CanMoveSelected));
        NotifyOfPropertyChange(nameof(CanUndoMove));
        NotifyOfPropertyChange(nameof(CanEditUnits));
        NotifyOfPropertyChange(nameof(CanViewUnits));
        NotifyOfPropertyChange(nameof(IsAvailable));
        NotifyOfPropertyChange(nameof(ListStatusText));
    }
    #endregion

    #region - 관계도 결선 (IUnitMapCommands · IUnitMapConsoleBridge) -
    /// <summary>관계도 확인 대기 중이면 막고 까닭을 말한다(#49).</summary>
    private bool BlockedByConfirm()
    {
        if (!IsStructureConfirmPending) return false;
        StatusText = CONFIRM_PENDING_NOTICE;
        return true;
    }

    /// <summary>편제 쓰기 실패의 상태별 사유(FR-34 · ISSUE-25). 서버 원문은 로그로만.</summary>
    private string StructureReason(ApiError? error)
    {
        if (!string.IsNullOrWhiteSpace(error?.Message)) _log?.Warning($"[UnitConsole] 서버 거절 원문: {error!.Code} {error.Message}");
        return UnitMapText.OrgFailureReason(error?.Code);
    }

    /// <summary>고른 부대의 상세를 서버 값으로 다시 읽는다(선택은 그대로 — 같은 부대).</summary>
    private Task RefreshSelectedDetailAsync(CancellationToken token = default)
        => SelectedRow is { } row ? SelectByIdAsync(row.Id, token, force: true) : Task.CompletedTask;

    private void OnMapPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(UnitMapViewModel.IsConfirming) or nameof(UnitMapViewModel.PendingConfirm))
        {
            NotifyOfPropertyChange(nameof(IsStructureConfirmPending));
            NotifyOfPropertyChange(nameof(IsConsoleInteractive));
            if (!IsStructureConfirmPending && StatusText == CONFIRM_PENDING_NOTICE) StatusText = string.Empty;
        }
    }

    /// <summary>관계도의 손이 비었다 — 미뤄 둔 편제 알림이 있으면 한 번 읽는다(깨끗할 때만 — dirty 면 띠가 이미 떠 있다).</summary>
    private void OnMapDefersReloadChanged(object? sender, EventArgs e)
    {
        if (_closed || Map.DefersReload || !IsExternallyChanged || HasUnappliedWork) return;
        _externalChangeTask = _externalChange.Pulse();
    }

    /// <summary>
    /// 지도 · 다른 창에서 온 "이 부대를 보여 달라"(FR-46 · ISSUE-43) — 상세 가드에 막히면 선택을 두고 까닭을 말한다(말없는 실패 0).
    /// 런처(레인 B)가 결과를 <c>OpenUnitConsoleResult</c> 로 회신한다.
    /// </summary>
    public async Task<OpenUnitConsoleOutcome> TryRevealAsync(int unitId, bool openMap, CancellationToken token = default)
    {
        if (!IsAvailable || Tree.Find(unitId) is not { } node) return OpenUnitConsoleOutcome.Unavailable;
        if (IsStructureConfirmPending) { StatusText = CONFIRM_PENDING_NOTICE; return OpenUnitConsoleOutcome.BlockedByUnsavedEdit; }
        if (SelectedRow?.Id != unitId && !Detail.Guard.TryNavigate(ConsoleNavigation.SelectRow))
        {
            StatusText = UnitMapText.RevealBlockedBand(node.Name);
            return OpenUnitConsoleOutcome.BlockedByUnsavedEdit;
        }

        if (openMap && !IsAdjacencyView)
        {
            SelectedRail = RailEntries.First(e => e.Key == RAIL_ADJACENCY);
            // 레일 전환도 미적용 관문을 지난다 — 막혔으면 관계도를 보이지 못했으니 "보였다" 고 하지 않는다(REVIEW-01 MEDIUM-5).
            if (!IsAdjacencyView)
            {
                StatusText = UnitMapText.RevealBlockedBand(node.Name);
                return OpenUnitConsoleOutcome.BlockedByUnsavedEdit;
            }
        }
        if (SelectedRow?.Id != unitId) await SelectRowAsync(RowOf(node), token, force: true).ConfigureAwait(true);
        Map.Reveal(unitId);
        return OpenUnitConsoleOutcome.Shown;
    }

    int? IUnitMapCommands.SelectedUnitId => SelectedRow?.Id;

    /// <summary>선택이 바뀌었다(트리 · 관계도 · 재조회).</summary>
    public event EventHandler? SelectedUnitChanged;

    bool IUnitMapCommands.CanView => CanViewUnits;
    bool IUnitMapCommands.CanEdit => CanEditUnits;

    /// <summary>관계도에서 고름 — 상세 가드를 지난다(dirty 면 막고 띠로 알린다 — v1.3 FR-02).</summary>
    bool IUnitMapCommands.TrySelect(int unitId)
    {
        if (IsStructureConfirmPending || Tree.Find(unitId) is not { } node) return false;
        if (SelectedRow?.Id == unitId) return true;
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.SelectRow)) { StatusText = SELECT_BLOCKED_BY_EDIT; return false; }
        _detailSettle = SelectRowAsync(RowOf(node), force: true);      // 선택은 곧바로, 상세 읽기는 이어서
        return true;
    }

    /// <summary>
    /// 관계도의 상위 바꾸기 · 되돌리기(<paramref name="targetId"/> <c>null</c> = 최상위로) — 트리 [이동 되돌리기](<c>_lastMove</c>)를 채우지 않는다(REVIEW-01 HIGH-1).
    /// </summary>
    async Task<bool> IUnitMapCommands.MoveAsync(int movingId, int? targetId, CancellationToken token)
    {
        _structureFromMap = true;
        try { return await MoveAsync(movingId, targetId, token).ConfigureAwait(true); }
        finally { _structureFromMap = false; }
    }

    async Task<bool> IUnitMapCommands.ChangeAdjacencyAsync(int unitId, int? add, int? remove, CancellationToken token)
    {
        _structureFromMap = true;
        try { return await ChangeAdjacencyAsync(unitId, add, remove, token).ConfigureAwait(true); }
        finally { _structureFromMap = false; }
    }

    /// <summary>
    /// 관계도의 실패 복구 · 확정 직전 재판정 · 되돌리기 전 읽기 — 편제만. 바쁘면 <b>한가해진 뒤 실제로 읽고</b> 돌아온다(REVIEW-01 MEDIUM-4):
    /// 종전엔 바쁘면 "나중에 한 번" 만 적어 두고 곧바로 돌아와, 관계도가 옛 편제로 재판정했다.
    /// </summary>
    async Task IUnitMapCommands.ReloadAsync(bool quiet, CancellationToken token)
    {
        await WhenNotBusyAsync(token).ConfigureAwait(true);
        await ReloadGraphAsync(token, quiet).ConfigureAwait(true);
    }

    /// <summary>관계도가 콘솔이 한가해지기를 기다리는 한도 — 넘으면 그대로 진행한다(재조회는 바쁘면 뒤로 미뤄진다 — ISSUE-27).</summary>
    public static readonly TimeSpan MAP_IDLE_WAIT = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 콘솔이 한가해질 때까지(상한 <see cref="MAP_IDLE_WAIT"/>). 한가해진 알림은 작업의 <c>finally</c> 한가운데서 오므로 연속을 비동기로 미룬다
    /// (UI 스레드면 디스패처 뒤로 — 재진입 방지).
    /// </summary>
    private async Task WhenNotBusyAsync(CancellationToken token)
    {
        if (!IsBusy) return;
        var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var timeout = SafeDelayAsync(MAP_IDLE_WAIT, stop.Token);
        void OnBusyChanged(object? sender, EventArgs e) { if (!IsBusy) idle.TrySetResult(); }
        BusyChanged += OnBusyChanged;
        try
        {
            if (IsBusy) await Task.WhenAny(idle.Task, timeout).ConfigureAwait(true);
        }
        finally
        {
            BusyChanged -= OnBusyChanged;
            stop.Cancel();
        }
    }

    private async Task SafeDelayAsync(TimeSpan span, CancellationToken token)
    {
        try { await _delay(span, token).ConfigureAwait(true); }
        catch (OperationCanceledException) { /* 거뒀다 */ }
    }

    /// <summary>로그인한 운영자 이름(배치 "마지막 변경 나") — 호스트가 주지 않으면 권한 서비스의 표시 이름(<c>user.name</c>). 모르면 <c>null</c>.</summary>
    private static string? SafeOperatorName(Func<string?>? provider)
    {
        if (provider is not null) return provider();
        return DevicePermissionGate.Resolve()?.Name;       // 미등록(오프라인 · 시험)이면 null
    }

    bool IUnitMapConsoleBridge.IsDetailDirty => Detail.IsDirty;

    Task IUnitMapConsoleBridge.RefreshDetailAsync(CancellationToken token) => RefreshSelectedDetailAsync(token);

    bool IUnitMapConsoleBridge.HasDeferredReload => IsExternallyChanged;

    /// <summary>콘솔이 바쁘다가 한가해졌다(관계도 [확정] 활성).</summary>
    public event EventHandler? BusyChanged;

    /// <summary>마지막 편제 쓰기 실패의 상태별 사유(성공하면 <c>null</c>).</summary>
    public string? LastWriteFailureReason { get; private set; }
    #endregion

    #region - Attributes -
    private sealed record UnitMoveUndo(int UnitId, string UnitName, int? PreviousParentId);

    private readonly IUnitGraphApi _units;
    private readonly IUnitDeviceApi _devices;
    private readonly ILogService? _log;
    private readonly Func<string?> _myUnitCode;
    private readonly Func<bool> _canEdit;
    private readonly Func<bool> _canDelete;
    private readonly Func<bool> _canView;
    private readonly IEventAggregator? _events;
    private readonly Func<bool> _isDragging;
    private readonly CoalescingTrigger _externalChange;
    private readonly IClock _clock;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly ConsolePrefEntry? _prefs;
    private readonly System.Action? _savePrefs;
    private DateTime? _firstExternalChangeAt;
    private bool _reloadPending;
    private bool _structureFromMap;
    private (int UnitId, int OtherId, bool Added)? _lastAdjacency;
    private UnitNodeRowViewModel? _wantedDetail;
    private Task? _detailLoop;
    private Task _detailSettle = Task.CompletedTask;
    private Task _externalChangeTask = Task.CompletedTask;
    private bool _isExternallyChanged;
    private bool _closed;

    private readonly Dictionary<int, UnitNodeRowViewModel> _rowCache = new();
    private readonly HashSet<int> _collapsed = new();

    private IReadOnlyList<UnitDeviceItem> _allDevices = Array.Empty<UnitDeviceItem>();
    private ConsoleRailEntry _selectedRail;
    private UnitNodeRowViewModel? _selectedRow;
    private UnitMoveUndo? _lastMove;
    private UnitDeleteBlock? _deleteBlock;
    private string _searchText = string.Empty;
    private string _statusText = string.Empty;
    private bool _isBusy;
    private bool _loadedOnce;
    private int _pendingSelectId;
    private int _detailTicket;
    private bool _isFiltered;
    #endregion
}
