using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Graph;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;

/****************************************************************************
   Purpose      : 부대 관계도 뷰모델 — 선택 다리 · 드롭 파이프라인 · 확인 오버레이 · 실패 복구 · 되돌리기 (FR-02 · FR-31~35)
   Created By   : Claude
   Created On   : 2026-09-28
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>확인 오버레이의 종류.</summary>
public enum UnitMapConfirmKind
{
    /// <summary>상위 바꾸기(드롭 · <c>Alt+↑/↓</c>).</summary>
    Reparent,

    /// <summary>인접 연결(같은 제대 위 드롭).</summary>
    Adjoin,

    /// <summary>툴바 [배치 초기화](전체 — FR-09).</summary>
    ResetLayout,
}

/// <summary>
/// 확인 대기 중인 조작 — <b>확정 전에는 서버 호출 0</b>(RISK-02). 캔버스 안 오버레이(<c>Units.Map.Confirm</c>)가 그린다.
/// </summary>
/// <param name="Kind">종류.</param>
/// <param name="MovingId">끈 부대(초기화면 <c>null</c>).</param>
/// <param name="TargetId">놓은 부대(초기화면 <c>null</c>).</param>
/// <param name="OldParentId">끈 부대의 지금 상위 — 되돌리기 · 상세 새로 고침 판정에 쓴다.</param>
/// <param name="Text">제목 · 본문 · 확정 단추 문구.</param>
public sealed record UnitMapPendingConfirm(UnitMapConfirmKind Kind, int? MovingId, int? TargetId, int? OldParentId, UnitMapConfirmText Text);

/// <summary>뷰모델이 옮겨 달라는 키보드 포커스(조정자 필수 항목 3 — 캔버스가 포커스를 가진다).</summary>
public enum UnitMapFocusTarget
{
    /// <summary>캔버스 — 오버레이 · M 모드 · 막대가 닫힌 뒤 <c>Ctrl+Z</c> · 화살표가 계속 되게.</summary>
    Canvas,

    /// <summary>상세 칸 첫 입력 — <c>Enter</c>(FR-36).</summary>
    DetailFirstField,
}

/// <summary>
/// 부대 콘솔과의 좁은 다리 — 콘솔 VM 이 구현한다(Phase 4 결선). 없으면(<c>null</c>) 관계도는 상세가 깨끗하고 콘솔이 한가하다고 본다.
/// </summary>
/// <remarks>
/// 관계도 확정이 상세의 미적용 편집을 말없이 지우지 않고(ISSUE-20 · 조정자 필수 항목 5), 콘솔이 바쁠 때 확정이 흔적 없이
/// 사라지지 않으며(#23 · #49), 확인 중 미뤄 둔 편제 변경을 확정 직전에 다시 판정하게(#25) 한다. UI 스레드 전용.
/// </remarks>
public interface IUnitMapConsoleBridge
{
    /// <summary>선택 부대(<see cref="IUnitMapCommands.SelectedUnitId"/>)의 상세에 적용하지 않은 편집이 있다.</summary>
    bool IsDetailDirty { get; }

    /// <summary>상세를 서버 값으로 다시 읽는다(편제 쓰기가 그 부대를 바꿨을 때 — 선택은 그대로).</summary>
    Task RefreshDetailAsync(CancellationToken token = default);

    /// <summary>관계도가 재적재를 미루게 한 동안 들어온 편제 알림이 있다 — 확정 직전 새 편제로 다시 판정해야 한다.</summary>
    bool HasDeferredReload { get; }

    /// <summary>콘솔이 다른 작업(재조회 · 앞선 쓰기) 중이다 — 확인 오버레이의 [확정]을 끈다.</summary>
    bool IsBusy { get; }

    /// <summary><see cref="IsBusy"/> 가 바뀌었다.</summary>
    event EventHandler? BusyChanged;

    /// <summary>마지막 편제 쓰기가 실패한 상태별 사유(<see cref="UnitMapText.OrgFailureReason"/>) — 없으면 <c>null</c>(일반 사유).</summary>
    string? LastWriteFailureReason { get; }
}

/// <summary>관계도 뷰모델의 선택 사항 — 시험은 시간 · 설정 · 메시지를 여기로 바꿔 끼운다.</summary>
public sealed class UnitMapViewModelOptions
{
    /// <summary>지도 연동 메시지(<see cref="MapLocateRequest"/>) · 배치 알림 구독. 없으면 지도 연동 · 실시간 반영이 꺼진다.</summary>
    public IEventAggregator? Events { get; init; }

    /// <summary>합침 · 디바운스 창의 지연(기본 <see cref="Task.Delay(TimeSpan, CancellationToken)"/>). 시험은 손으로 흘린다.</summary>
    public Func<TimeSpan, CancellationToken, Task>? Delay { get; init; }

    /// <summary>부대 콘솔의 개인 표시 설정 항목(뷰 · 레이어만 — 공유 배치는 쓰지 않는다, NFR-14).</summary>
    public ConsolePrefEntry? Prefs { get; init; }

    /// <summary>개인 표시 설정 저장.</summary>
    public System.Action? SavePrefs { get; init; }

    /// <summary>부대 콘솔 다리.</summary>
    public IUnitMapConsoleBridge? Console { get; init; }

    /// <summary>이 앱이 붙은 부대(★ · <c>Home</c> · 첫 화면).</summary>
    public int? MyUnitId { get; init; }

    /// <summary>내 부대를 편제가 바뀔 때마다 다시 찾는 함수(콘솔은 부대 <b>코드</b>만 알아 편제를 읽은 뒤에야 id 가 정해진다). 있으면 <see cref="MyUnitId"/> 보다 먼저.</summary>
    public Func<int?>? MyUnitIdProvider { get; init; }

    /// <summary>전역 구독이 꺼져 실시간 반영이 없는가(" · 실시간 반영 꺼짐" 꼬리표 — ISSUE-32). 없으면 켜져 있다고 본다.</summary>
    public Func<bool>? IsLiveOff { get; init; }

    /// <summary>시계 — 캔버스의 휠 합침 · 알림 합침에 넘긴다(<c>IClock</c> — 시험은 가짜).</summary>
    public Ironwall.Dotnet.Libraries.Base.Services.IClock? Clock { get; init; }

    /// <summary>이 운영자 이름 — 배치 "마지막 변경"이 나면 "나"로 적는다.</summary>
    public string? CurrentOperatorName { get; init; }

    /// <summary>화살표 선택 이동 뒤 콘솔 선택(상세 GET)까지 기다리는 창 — 자동 반복 키의 GET 폭주 방지(필수 항목 2).</summary>
    public TimeSpan SelectionDebounce { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>배치 알림 합침 창(FR-53).</summary>
    public TimeSpan NoticeCoalesce { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// 기록 — 대기열 작업의 예기치 못한 예외 원문은 막대가 아니라 여기로 간다(REVIEW-01 L-6). 없으면 남기지 않는다.
    /// </summary>
    public Ironwall.Dotnet.Libraries.Base.Services.ILogService? Log { get; init; }
}

/// <summary>
/// 부대 관계도 뷰모델 — 캔버스(<see cref="IUnitMapInteraction"/>)와 부대 콘솔(<see cref="IUnitMapCommands"/>) 사이.
/// </summary>
/// <remarks>
/// <para><b>확인 전 서버 0</b>(RISK-02): 상위 · 인접 드롭은 <see cref="PendingConfirm"/> 만 만든다. 서버는 <see cref="ConfirmAsync"/> 에서만 부른다.
/// 오버레이가 떠 있는 동안 다른 드롭 · 키 · 되돌리기는 무시되고 콘솔의 재적재는 <see cref="DefersReload"/> 로 미뤄진다(콘솔 전체 모달 — #49).</para>
/// <para><b>쓰기는 한 번에 하나</b>(NFR-12): 서버 쓰기 · 배치 재조회는 UI 스레드의 단일 대기열(<see cref="Serialize"/>)을 탄다 —
/// 두 번째 확정은 앞선 응답 뒤에 나가고, <c>If-Match</c> 는 나가는 순간의 버전이다.</para>
/// <para><b>위치는 낙관적</b>: 먼저 그리고(대기 중 변경) 응답으로 확정 · 실패면 서버 배치를 다시 읽어 그대로 그린다(FR-34).</para>
/// <para><b>되돌리기</b>는 레인 B 의 <see cref="UnitMapUndoStack"/> 한 줄(시간순). 막대 [되돌리기]는 막대가 말하는 바로 그 항목일 때만 켜진다(#22).</para>
/// <para><b>스레드</b>: UI 스레드 전용. 메시지 구독은 UI 스레드로 받는다(<c>SubscribeOnUIThread</c>).</para>
/// </remarks>
public sealed partial class UnitMapViewModel : PropertyChangedBase, IUnitMapInteraction, IUnitMapReloadGate,
    IUnitMapOverlayCommands, IUnitMapBarAction, IHandle<UnitLayoutChangedMessage>, IHandle<MapLocateResult>, IDisposable
{
    /// <summary>서버가 받는 Δ 한계(S-1 ⑤ · 시나리오 ISSUE-40).</summary>
    public const double MaxDelta = GraphViewport.MaxDelta;

    /// <summary>콘솔이 바쁠 때 [확정]을 누르면 보이는 사유(#23).</summary>
    public const string ConsoleBusyStatus = UnitMapText.ConsoleBusyStatus;

    private readonly IUnitMapCommands _commands;
    private readonly IUnitLayoutApi _api;
    private readonly UnitMapViewModelOptions _options;
    private readonly UnitMapUndoStack _undo = new();

    /// <summary>상위 바꾸기 직후의 조상 사슬(부대 + 조상 값 비교 — #21). 되돌리기 항목과 함께 산다.</summary>
    private readonly ConditionalWeakTable<UnitMapReparentUndo, ReparentFacts> _reparentFacts = new();

    private UnitTreeModel _tree = UnitTreeModel.Empty;
    private IReadOnlyList<UnitDeviceItem> _devices = Array.Empty<UnitDeviceItem>();
    private Dictionary<int, (int Count, int Errors)> _deviceStats = new();
    private int _treeGeneration;
    private UnitMapScene _scene = UnitMapScene.Empty;

    private IUnitMapSurface? _surface;
    private int? _selected;
    private UnitMapPendingConfirm? _pending;
    private int? _draggingUnit;
    private int _queued;
    private Task _tail = Task.CompletedTask;
    private string? _barText;
    private bool _barIsError;
    private string? _statusText;
    private bool _defersReload;
    private bool _disposed;

    private sealed record ReparentFacts(IReadOnlyList<int> ChainAfterMove, bool HadDelta);

    private readonly Ironwall.Dotnet.Libraries.Base.Services.IClock _clock;
    private string? _barAction;

    /// <summary>창이 닫혔다 — 아직 시작하지 않은 대기열 작업 · 연기 감시 · 콘솔 한가 기다림을 거둔다(REVIEW-01 L-7).</summary>
    private readonly CancellationTokenSource _closing = new();

    /// <summary>
    /// 대기열에서 차례를 받은 편제 쓰기가 콘솔이 한가해지기를 기다리는 한도(REVIEW-01 MEDIUM-4). 넘으면 보내지 않고 막대로 말한다.
    /// </summary>
    public static readonly TimeSpan ConsoleIdleWait = TimeSpan.FromSeconds(10);

    /// <summary>내 부대 id — 콘솔이 준 함수가 있으면 그것(편제를 읽은 뒤 정해진다).</summary>
    private int? MyUnitId => _options.MyUnitIdProvider?.Invoke() ?? _options.MyUnitId;

    public UnitMapViewModel(IUnitMapCommands commands, IUnitLayoutApi layoutApi, UnitMapViewModelOptions? options = null)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _api = layoutApi ?? throw new ArgumentNullException(nameof(layoutApi));
        _options = options ?? new UnitMapViewModelOptions();

        _clock = _options.Clock ?? new Ironwall.Dotnet.Libraries.Base.Services.SystemClock();
        _noticeTrigger = new CoalescingTrigger(OnNoticeSettledAsync, _options.NoticeCoalesce, _options.Delay);
        _viewSaveTrigger = new CoalescingTrigger(OnViewIdleAsync, ViewSaveIdle, _options.Delay);
        _selectTrigger = new CoalescingTrigger(OnSelectionSettledAsync, _options.SelectionDebounce, _options.Delay);
        _layers = ReadLayersPref();

        _selected = _commands.SelectedUnitId;
        _commands.SelectedUnitChanged += OnConsoleSelectionChanged;
        if (_options.Console is { } bridge) bridge.BusyChanged += OnConsoleBusyChanged;
        _options.Events?.SubscribeOnUIThread(this);
    }

    #region - 상태 -
    /// <summary>편제(콘솔이 읽은 것).</summary>
    public UnitTreeModel Tree => _tree;

    /// <summary>캔버스가 그리는 한 장 — Δ 가 입혀진 위치(대기 중 낙관 변경 · M 모드 미리보기 포함).</summary>
    public UnitMapScene Scene
    {
        get => _scene;
        private set { _scene = value; NotifyOfPropertyChange(); }
    }

    /// <summary>관계도의 선택 표시(트리와 같은 선택 — FR-02). 화살표로 옮기는 동안에는 콘솔 확정보다 먼저 움직인다.</summary>
    public int? SelectedUnitId
    {
        get => _selected;
        private set
        {
            if (_selected == value) return;
            _selected = value;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(CanLocateOnMap));
            NotifyOfPropertyChange(nameof(LocateDisabledReason));
            NotifyResetState();
        }
    }

    /// <summary>확인 대기 중인 조작(없으면 <c>null</c>).</summary>
    public UnitMapPendingConfirm? PendingConfirm => _pending;

    /// <summary>확인 오버레이가 떠 있다 — 콘솔 전체가 모달이다(#49: 트리 · 상세 · 막대 · 레일 · 배경 재조회 막음).</summary>
    public bool IsConfirming => _pending is not null;

    /// <summary>오버레이의 [확정]을 누를 수 있다 — 콘솔이 바쁘면 끈다(#23 — 눌러도 사라지지 않게).</summary>
    public bool CanConfirm => _pending is not null && _options.Console?.IsBusy != true;

    /// <summary>노드를 끄는 중(데드존을 넘은 뒤).</summary>
    public bool IsDragging => _draggingUnit is not null;

    /// <summary>서버 쓰기 · 배치 재조회가 대기열에 있다.</summary>
    public bool IsWriting => _queued > 0;

    /// <summary>캔버스 아래 막대 문구(<c>Units.Map.UndoText</c>). 다음 조작까지 남는다(타이머 없음).</summary>
    public string? BarText => _barText;

    /// <summary>막대가 실패 · 충돌 모양(왼쪽 세로 막대).</summary>
    public bool BarIsError => _barIsError;

    /// <summary>되돌릴 것이 있고 지금 되돌릴 수 있다(<c>Ctrl+Z</c> — 가장 최근 항목).</summary>
    public bool CanUndo => _undo.Count > 0 && _pending is null && !_moveMode && _draggingUnit is null;

    /// <summary>막대의 [되돌리기] — 막대가 말하는 조작이 곧 되돌릴 항목일 때만(#22 — 문구와 동작이 어긋나지 않게).</summary>
    public bool BarCanUndo => CanUndo && _undo.Bar is { } bar && ReferenceEquals(bar, _undo.Peek()) && _barText is not null;

    /// <summary>캔버스 확인 오버레이(<c>Units.Map.Confirm</c>) — 없으면 <c>null</c>. 콘솔이 바쁘면 [확정] 을 끄고 까닭을 싣는다(#23).</summary>
    public UnitMapConfirmPrompt? ConfirmPrompt
        => _pending is { } p ? new UnitMapConfirmPrompt(p.Text.Title, p.Text.Lines, p.Text.OkText, CanConfirm, CanConfirm ? null : ConsoleBusyStatus) : null;

    /// <summary>캔버스 아래 되돌리기 막대 — 없으면 <c>null</c>.</summary>
    public UnitMapBar? Bar => _barText is { } text ? new UnitMapBar(text, _barIsError, BarCanUndo, _barAction) : null;

    /// <summary>캔버스 시계(휠 합침 — <c>UnitMapCanvas.Clock</c>).</summary>
    public Ironwall.Dotnet.Libraries.Base.Services.IClock? Clock => _options.Clock;

    /// <summary>한 줄 상태(막힘 사유 · 검색 · 지도 회신).</summary>
    public string? StatusText
    {
        get => _statusText;
        private set { _statusText = value; NotifyOfPropertyChange(); }
    }

    /// <summary>캔버스(또는 상세)로 포커스를 옮겨 달라.</summary>
    public event EventHandler<UnitMapFocusTarget>? FocusRequested;

    /// <summary>지금 콘솔이 편제 · 배치를 다시 읽으면 안 된다 — 확인 오버레이 · M 모드 · 끌기.</summary>
    public bool DefersReload => _defersReload;

    /// <summary><see cref="DefersReload"/> 가 바뀌었다.</summary>
    public event EventHandler? DefersReloadChanged;

    private UnitMapBusy Busy
        => (_draggingUnit is not null ? UnitMapBusy.Dragging : UnitMapBusy.None)
         | (_moveMode ? UnitMapBusy.MoveMode : UnitMapBusy.None)
         | (_pending is not null ? UnitMapBusy.Confirming : UnitMapBusy.None)
         | (_queued > 0 ? UnitMapBusy.WriteInFlight : UnitMapBusy.None);

    private UnitMapDropPolicy Policy => new(_commands.CanView, _commands.CanEdit, LayoutState);
    #endregion

    #region - 데이터 -
    /// <summary>
    /// 콘솔이 편제 · 장비를 (다시) 읽었다. 확인 대기 · M 모드가 사라진 부대를 가리키면 거둔다.
    /// </summary>
    public void SetData(UnitTreeModel tree, IReadOnlyList<UnitDeviceItem> devices)
    {
        _tree = tree ?? UnitTreeModel.Empty;
        _devices = devices ?? Array.Empty<UnitDeviceItem>();
        _treeGeneration++;
        _deviceStats = _devices.Where(d => d?.UnitId is int)
                               .GroupBy(d => d.UnitId!.Value)
                               .ToDictionary(g => g.Key, g => (g.Count(), g.Count(d => d.Status == UnitDeviceStatus.Error)));

        if (_pending is { } p && (p.MovingId is int m && _tree.Find(m) is null || p.TargetId is int t && _tree.Find(t) is null))
        {
            ClosePending(focusCanvas: false);
            StatusText = "확인하던 부대가 편제에서 사라져 취소했습니다.";
        }
        if (_moveMode && (_moveUnit is not int mu || _tree.Find(mu) is null))
        {
            ExitMoveMode(focusCanvas: false);
            StatusText = UnitMapText.MoveModeUnitDeleted;         // FR-37 경계 표 — 서버 0
        }
        if (_selected is int s && _tree.Find(s) is null) SelectedUnitId = _commands.SelectedUnitId;

        // 빈 상태 문구는 "지금" 편제의 사실이다 — 캔버스가 편제보다 먼저 붙으면 빈 편제로 "표시할 부대가 없습니다" 를 적고,
        // 부대가 도착해 그려진 뒤에도 그 문구가 남아 있었다(실앱 2 부대 화면 · 2026-09-28).
        if (_tree.Count > 0 && StatusText == UnitMapText.EmptyMapStatus) StatusText = null;

        RebuildScene();
        NotifyOfPropertyChange(nameof(Tree));
        NotifyOfPropertyChange(nameof(CanLocateOnMap));
        NotifyOfPropertyChange(nameof(LocateDisabledReason));
        TryRestoreView();
    }

    private string NameOf(int? unitId) => unitId is int id ? _tree.Find(id)?.Name ?? $"#{id}" : string.Empty;

    /// <summary>화면에 입힐 Δ — 서버(또는 세션) 문서 + 대기 중 낙관 변경 + M 모드 미리보기.</summary>
    private Dictionary<int, Vector> DisplayDeltas()
    {
        if (!_assessment.AppliesDeltas) return new Dictionary<int, Vector>();

        var snapshot = _snapshot;
        foreach (var change in _optimistic) snapshot = change.ApplyTo(snapshot);
        var deltas = new Dictionary<int, Vector>(snapshot.Deltas);
        if (_moveMode && _moveUnit is int unit)
            deltas[unit] = ClampDelta((deltas.TryGetValue(unit, out var own) ? own : default) + _moveOffset);
        return deltas;
    }

    private Vector? DisplayDeltaOf(int unitId) => DisplayDeltas().TryGetValue(unitId, out var delta) ? delta : null;

    private void RebuildScene()
    {
        var deltas = DisplayDeltas();
        var layout = UnitMapLayout.Compute(_tree, deltas);
        var facts = new Dictionary<int, UnitMapNodeFacts>(_tree.Count);
        foreach (var node in _tree.Ordered)
        {
            var (count, errors) = _deviceStats.TryGetValue(node.Id, out var stat) ? stat : (0, 0);
            facts[node.Id] = new UnitMapNodeFacts(
                node.Id,
                count,
                errors,
                IsMine: MyUnitId == node.Id,
                IsMoved: deltas.ContainsKey(node.Id),
                IsDimmed: _highlight is EnumUnitEchelon e && node.Echelon != e);
        }
        Scene = new UnitMapScene(_tree, layout.Positions, facts, _layers);
        NotifyResetState();     // 옮긴 부대가 바뀌면 [이 부대 배치 초기화]도 바뀐다
    }

    private static Vector ClampDelta(Vector delta) => GraphViewport.ClampDelta(delta);
    #endregion

    #region - 선택 다리 (FR-02) -
    /// <summary>노드 클릭 · UIA <c>Select()</c> · 오른쪽 클릭(조정자 결정 — 선택만) — 가드를 거쳐 곧바로 고른다.</summary>
    public void RequestSelect(int unitId)
    {
        if (_pending is not null || _tree.Find(unitId) is null) return;
        if (_moveMode && _moveUnit != unitId) ExitMoveMode(focusCanvas: false);   // FR-37 — 다른 노드 클릭 = M 취소(서버 0) 후 선택
        _pendingSelect = null;
        _selectTrigger.Cancel();
        SelectedUnitId = _commands.TrySelect(unitId) ? unitId : _commands.SelectedUnitId;
    }

    /// <summary>빈 곳 클릭 — 콘솔에 "선택 없음" 경로가 없어 선택을 그대로 둔다(상세 가드도 흔들리지 않는다).</summary>
    public void RequestClearSelection() { }

    private void OnConsoleSelectionChanged(object? sender, EventArgs e)
    {
        if (_pendingSelect is not null) return;     // 화살표로 옮기는 중 — 확정은 디바운스 끝
        SelectedUnitId = _commands.SelectedUnitId;
    }

    private void OnConsoleBusyChanged(object? sender, EventArgs e)
    {
        NotifyOfPropertyChange(nameof(CanConfirm));
        NotifyOfPropertyChange(nameof(ConfirmPrompt));
        if (_options.Console?.IsBusy != true && StatusText == ConsoleBusyStatus) StatusText = null;
    }
    #endregion

    #region - 끌기 (IUnitMapInteraction) -
    public void AttachSurface(IUnitMapSurface? surface)
    {
        if (ReferenceEquals(_surface, surface)) return;
        if (_surface is not null) _surface.ViewChanged -= OnSurfaceViewChanged;
        _surface = surface;
        if (_surface is not null) _surface.ViewChanged += OnSurfaceViewChanged;
        _viewRestored = false;
        TryRestoreView();
    }

    /// <summary>끄는 동안 대상 표시 판정 — 드롭 판정기 + 더러운 상세 막힘. 서버를 부르지 않는다(NFR-04).</summary>
    public UnitMapDropDecision Classify(int movingUnitId, int? hoverUnitId, bool ctrl)
    {
        var decision = UnitMapDropClassifier.Classify(_tree, movingUnitId, hoverUnitId, ctrl, Policy);
        if (decision.Kind is UnitMapDropKind.Reparent or UnitMapDropKind.Adjoin
            && DetailBlocks(AffectedUnits(decision.Kind, movingUnitId, decision.TargetId)))
            return UnitMapDropDecision.Blocked(UnitMapText.DirtyDetailBlocked, decision.TargetId);
        return decision;
    }

    public void BeginDrag(int unitId)
    {
        if (_pending is not null || _moveMode) return;
        _draggingUnit = unitId;
        NotifyOfPropertyChange(nameof(IsDragging));
        UpdateBusy();
    }

    /// <summary>놓았다 — 위치면 먼저 그리고 PATCH 1회, 상위 · 인접이면 확인 오버레이만(서버 0).</summary>
    public void CompleteDrag(UnitMapDropRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        EndDrag();
        if (_pending is not null || _moveMode) return;     // 오버레이 · M 모드 중 두 번째 드롭은 없는 것으로

        var decision = Classify(request.UnitId, request.HoverUnitId, request.Ctrl);
        switch (decision.Kind)
        {
            case UnitMapDropKind.Position:
                var offset = new Vector(request.WorldDx, request.WorldDy);
                if (offset.X == 0 && offset.Y == 0) return;
                _ = WritePositionAsync(request.UnitId, (DisplayDeltaOf(request.UnitId) ?? default) + offset);
                break;
            case UnitMapDropKind.Reparent:
                OpenPending(UnitMapConfirmKind.Reparent, request.UnitId, decision.TargetId!.Value,
                            UnitMapText.ConfirmReparent(_tree, request.UnitId, decision.TargetId.Value, showParentPath: !_layers.Hierarchy));
                break;
            case UnitMapDropKind.Adjoin:
                OpenPending(UnitMapConfirmKind.Adjoin, request.UnitId, decision.TargetId!.Value,
                            UnitMapText.ConfirmAdjoin(_tree, request.UnitId, decision.TargetId.Value));
                break;
            default:
                StatusText = KoreanParticles.Resolve(decision.Reason ?? string.Empty);
                break;
        }
    }

    /// <summary><c>Esc</c> · 캡처 상실 · 창 비활성화 · 캔버스 밖 놓기(조정자 결정) — 원위치 · 서버 0.</summary>
    public void CancelDrag(int unitId) => EndDrag();

    private void EndDrag()
    {
        if (_draggingUnit is null) return;
        _draggingUnit = null;
        NotifyOfPropertyChange(nameof(IsDragging));
        UpdateBusy();
    }
    #endregion

    #region - 확인 오버레이 (FR-32) -
    private void OpenPending(UnitMapConfirmKind kind, int? movingId, int? targetId, UnitMapConfirmText text)
    {
        var oldParent = movingId is int m ? _tree.Find(m)?.ParentId : null;
        _pending = new UnitMapPendingConfirm(kind, movingId, targetId, oldParent, text);
        NotifyPending();
    }

    /// <summary>
    /// [옮기기] · [연결] · [초기화] · <c>Enter</c> — 서버를 부르는 유일한 곳. 두 번 눌러도 한 번.
    /// 콘솔이 바쁘면 오버레이를 그대로 두고 까닭을 말한다(#23).
    /// </summary>
    public Task ConfirmAsync()
    {
        if (_pending is not { } pending) return Task.CompletedTask;
        if (!CanConfirm)
        {
            StatusText = ConsoleBusyStatus;
            return Task.CompletedTask;
        }
        ClosePending(focusCanvas: true);

        var subject = NameOf(pending.MovingId);
        return pending.Kind switch
        {
            UnitMapConfirmKind.Reparent => Serialize(() => ExecuteReparentAsync(pending), subject),
            UnitMapConfirmKind.Adjoin => Serialize(() => ExecuteAdjoinAsync(pending), subject),
            _ => ResetLayoutConfirmedAsync(),
        };
    }

    /// <summary>[취소] · <c>Esc</c> — 서버 0.</summary>
    public void CancelConfirm()
    {
        if (_pending is null) return;
        ClosePending(focusCanvas: true);
    }

    private void ClosePending(bool focusCanvas)
    {
        _pending = null;
        NotifyPending();
        if (focusCanvas) RequestFocus(UnitMapFocusTarget.Canvas);
    }

    private void NotifyPending()
    {
        NotifyOfPropertyChange(nameof(PendingConfirm));
        NotifyOfPropertyChange(nameof(IsConfirming));
        NotifyOfPropertyChange(nameof(CanConfirm));
        NotifyOfPropertyChange(nameof(ConfirmPrompt));
        NotifyOfPropertyChange(nameof(BarCanUndo));
        NotifyOfPropertyChange(nameof(Bar));
        UpdateBusy();
    }

    /// <summary>
    /// 확정 직전 재판정(#25) — 확인 중 미뤄 둔 편제 알림이 있으면 새 편제를 먼저 읽고, 판정이 달라졌으면 보내지 않는다.
    /// </summary>
    private async Task<bool> RevalidateAsync(UnitMapPendingConfirm pending)
    {
        if (_options.Console?.HasDeferredReload == true)
            await _commands.ReloadAsync(quiet: true).ConfigureAwait(true);

        var expected = pending.Kind == UnitMapConfirmKind.Reparent ? UnitMapDropKind.Reparent : UnitMapDropKind.Adjoin;
        var decision = UnitMapDropClassifier.Classify(_tree, pending.MovingId!.Value, pending.TargetId, false, Policy);
        var stillSameParent = pending.Kind != UnitMapConfirmKind.Reparent || _tree.Find(pending.MovingId.Value)?.ParentId == pending.OldParentId;
        if (decision.Kind == expected && decision.TargetId == pending.TargetId && stillSameParent) return true;

        var reason = decision.Kind == UnitMapDropKind.Blocked ? KoreanParticles.Resolve(decision.Reason ?? string.Empty) : "다른 곳에서 이미 바뀌었습니다.";
        ShowBar($"편제가 바뀌어 {Quote(NameOf(pending.MovingId))}에 대한 확정을 보내지 않았습니다 — {reason}", isError: true);
        return false;
    }

    private async Task ExecuteReparentAsync(UnitMapPendingConfirm pending)
    {
        if (!await WhenConsoleIdleAsync(NameOf(pending.MovingId)).ConfigureAwait(true)) return;
        if (!await RevalidateAsync(pending).ConfigureAwait(true)) return;
        var (movingId, targetId, oldParentId) = (pending.MovingId!.Value, pending.TargetId!.Value, pending.OldParentId);
        var name = NameOf(movingId);
        var targetName = NameOf(targetId);
        var chainAfter = new[] { movingId }.Concat(UnitMapLayoutSync.TouchedUnits(_tree, targetId)).ToList();
        var hadDelta = DisplayDeltaOf(movingId) is not null;

        if (await _commands.MoveAsync(movingId, targetId).ConfigureAwait(true))
        {
            var entry = new UnitMapReparentUndo(movingId, oldParentId, targetId);
            _reparentFacts.AddOrUpdate(entry, new ReparentFacts(chainAfter, hadDelta));
            _undo.Push(entry);
            ShowBar(UnitMapText.ReparentedBar(name, targetName), isError: false);
            await CleanupAfterReparentAsync(movingId).ConfigureAwait(true);
            await RefreshDetailIfAffectedAsync(movingId, targetId, oldParentId).ConfigureAwait(true);
            return;
        }
        await ReportOrgWriteFailedAsync(reason => UnitMapText.WriteFailedBar(name, reason)).ConfigureAwait(true);
    }

    private async Task ExecuteAdjoinAsync(UnitMapPendingConfirm pending)
    {
        if (!await WhenConsoleIdleAsync(NameOf(pending.MovingId)).ConfigureAwait(true)) return;
        if (!await RevalidateAsync(pending).ConfigureAwait(true)) return;
        var (unitId, otherId) = (pending.MovingId!.Value, pending.TargetId!.Value);
        var name = NameOf(unitId);
        var otherName = NameOf(otherId);
        if (await _commands.ChangeAdjacencyAsync(unitId, otherId, null).ConfigureAwait(true))
        {
            _undo.Push(new UnitMapAdjacencyUndo(unitId, otherId, Added: true));
            if (_layers.Adjacency) ShowBar(UnitMapText.AdjoinedBar(name, otherName), isError: false);
            else ShowBar(UnitMapText.AdjoinedBar(name, otherName) + UnitMapText.AdjacencyHiddenNote, isError: false, action: UnitMapText.ShowAdjacencyAction);   // ISSUE-55
            await RefreshDetailIfAffectedAsync(unitId, otherId).ConfigureAwait(true);
            return;
        }
        await ReportOrgWriteFailedAsync(reason => UnitMapText.AdjacencyWriteFailedBar(name, otherName, reason)).ConfigureAwait(true);
    }

    /// <summary>
    /// 콘솔이 한가해질 때까지(REVIEW-01 MEDIUM-4) — 대기열에서 차례를 받은 편제 쓰기 · 되돌리기가 바쁜 콘솔에 가서 "앞선 작업" 으로
    /// 튕기거나, 재조회가 미뤄진 옛 편제로 재판정하지 않게. <see cref="ConsoleIdleWait"/> 를 넘기면 보내지 않고 막대로 말한다.
    /// </summary>
    /// <remarks>
    /// 한가해진 알림(<c>BusyChanged</c>)은 콘솔 작업의 <c>finally</c> 한가운데서 온다 — 거기서 곧바로 이어 쓰면 재진입이다.
    /// 그래서 연속을 비동기로 미룬다(UI 스레드면 디스패처 뒤로). 창이 닫히면(<see cref="_closing"/>) 조용히 그만둔다.
    /// </remarks>
    /// <returns>보내도 되면 <c>true</c>.</returns>
    private async Task<bool> WhenConsoleIdleAsync(string subject)
    {
        if (_options.Console is not { } bridge || !bridge.IsBusy) return !_closing.IsCancellationRequested;

        var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(_closing.Token);
        var timeout = SafeDelay(ConsoleIdleWait, stop.Token);        // 먼저 건다 — 구독이 붙은 뒤의 시간은 모두 센다
        void OnBusyChanged(object? sender, EventArgs e) { if (!bridge.IsBusy) idle.TrySetResult(); }
        bridge.BusyChanged += OnBusyChanged;
        try
        {
            if (bridge.IsBusy) await Task.WhenAny(idle.Task, timeout).ConfigureAwait(true);
        }
        finally
        {
            bridge.BusyChanged -= OnBusyChanged;
            stop.Cancel();
        }

        if (_closing.IsCancellationRequested) return false;
        if (!bridge.IsBusy) return true;
        ShowBar(UnitMapText.ConsoleStillBusyBar(subject), isError: true);
        return false;
    }

    /// <summary>주입된 지연(시험은 손으로 흘린다) — 취소는 예외가 아니라 끝난 작업으로 돌려준다.</summary>
    private async Task SafeDelay(TimeSpan span, CancellationToken token)
    {
        try { await (_options.Delay ?? Task.Delay)(span, token).ConfigureAwait(true); }
        catch (OperationCanceledException) { /* 거뒀다 */ }
    }
    #endregion

    #region - 상세 다리 (필수 항목 5 · #20) -
    private IEnumerable<int> AffectedUnits(UnitMapDropKind kind, int movingId, int? targetId)
    {
        yield return movingId;
        if (targetId is int t) yield return t;
        if (kind == UnitMapDropKind.Reparent && _tree.Find(movingId)?.ParentId is int oldParent) yield return oldParent;
    }

    private bool DetailBlocks(IEnumerable<int> affected)
        => _options.Console is { IsDetailDirty: true } && _commands.SelectedUnitId is int selected && affected.Contains(selected);

    /// <summary>선택이 영향 부대(끈 · 대상 · 옛 상위 · 새 상위)면 상세만 다시 읽는다 — 선택은 그대로(강제 재선택 없음 — #20).</summary>
    private async Task RefreshDetailIfAffectedAsync(params int?[] affected)
    {
        if (_options.Console is not { } bridge || _commands.SelectedUnitId is not int selected) return;
        if (affected.Any(id => id == selected)) await bridge.RefreshDetailAsync().ConfigureAwait(true);
    }
    #endregion

    #region - 되돌리기 (FR-35 · #21 · #22) -
    /// <summary>막대 [되돌리기] · <c>Ctrl+Z</c> — 가장 최근 조작 하나(한 줄 시간순). 대기열을 타므로 연타해도 차례대로.</summary>
    public Task UndoAsync()
    {
        if (!CanUndo) return Task.CompletedTask;
        return Serialize(async () =>
        {
            if (_undo.Peek() is not { } entry) return;
            await ExecuteUndoAsync(entry).ConfigureAwait(true);
            NotifyOfPropertyChange(nameof(CanUndo));
            NotifyOfPropertyChange(nameof(BarCanUndo));
        });
    }

    private Task ExecuteUndoAsync(UnitMapUndoEntry entry) => entry switch
    {
        UnitMapPositionUndo position => UndoPositionAsync(position),
        UnitMapLayoutResetUndo reset => UndoResetAsync(reset),
        UnitMapReparentUndo move => UndoReparentAsync(move),
        UnitMapAdjacencyUndo adjacency => UndoAdjacencyAsync(adjacency),
        _ => Task.CompletedTask,
    };

    /// <summary>
    /// 상위 되돌리기 — 먼저 편제를 다시 읽고(#21), 그 부대의 상위 사슬(부대 + 조상, 값)이 내가 옮긴 그대로일 때만 반대로 보낸다.
    /// 반대 이동은 <b>언제나 이 표가 직접</b> 보낸다 — 옛 상위가 없었으면(최상위) 최상위로(<c>MoveAsync(id, null)</c>).
    /// 트리 툴바의 공유 <c>_lastMove</c> 는 쓰지 않는다(REVIEW-01 HIGH-1 — 그 칸은 하나뿐이라 다른 이동이 덮으면 엉뚱한 부대를 옮기고,
    /// 비었으면 옮기지 않은 채 "성공" 했다).
    /// </summary>
    private async Task UndoReparentAsync(UnitMapReparentUndo entry)
    {
        var name = NameOf(entry.UnitId);                                           // 다시 읽기 전 이름 — 사라졌어도 막대가 이름으로 말한다
        if (!await WhenConsoleIdleAsync(name).ConfigureAwait(true)) return;      // 표는 그대로 — 다시 시도할 수 있게
        await _commands.ReloadAsync(quiet: true).ConfigureAwait(true);

        var node = _tree.Find(entry.UnitId);
        if (node is null) { Drop(entry, UnitMapText.UndoUnitGoneBar(name)); return; }

        var facts = _reparentFacts.TryGetValue(entry, out var f) ? f : null;
        var chainNow = UnitMapLayoutSync.TouchedUnits(_tree, entry.UnitId);
        var unchanged = node.ParentId == entry.ToParentId && (facts is null || chainNow.SequenceEqual(facts.ChainAfterMove));
        if (!unchanged) { Drop(entry, UnitMapText.ReparentUndoRefusedBar(name)); return; }

        var ok = await _commands.MoveAsync(entry.UnitId, entry.FromParentId).ConfigureAwait(true);
        if (ok)
        {
            _undo.CompleteUndo(entry, succeeded: true);
            ShowBar(facts?.HadDelta == true ? UnitMapText.ReparentUndoneWithLayoutBar(name) : UnitMapText.ReparentUndoneBar(name), isError: false);
            await RefreshDetailIfAffectedAsync(entry.UnitId, entry.FromParentId, entry.ToParentId).ConfigureAwait(true);
            return;
        }
        await ReportOrgWriteFailedAsync(reason => UnitMapText.WriteFailedBar(name, reason)).ConfigureAwait(true);
    }

    /// <summary>인접 되돌리기 — 먼저 편제를 다시 읽고, 이미 반영돼 있으면 보내지 않는다(SIM-F120).</summary>
    private async Task UndoAdjacencyAsync(UnitMapAdjacencyUndo entry)
    {
        var name = NameOf(entry.UnitId);                                           // 다시 읽기 전 이름(사라진 부대도 이름으로)
        var otherName = NameOf(entry.OtherId);
        if (!await WhenConsoleIdleAsync(name).ConfigureAwait(true)) return;
        await _commands.ReloadAsync(quiet: true).ConfigureAwait(true);

        var node = _tree.Find(entry.UnitId);
        if (node is null || _tree.Find(entry.OtherId) is null) { Drop(entry, UnitMapText.UndoUnitGoneBar(node is null ? name : otherName)); return; }

        var present = node.AdjacentIds.Contains(entry.OtherId);
        if (present != entry.Added) { Drop(entry, UnitMapText.AdjacencyAlreadyBar(name, otherName, entry.Added)); return; }

        var ok = await _commands.ChangeAdjacencyAsync(entry.UnitId,
                                                      entry.Added ? null : entry.OtherId,
                                                      entry.Added ? entry.OtherId : null).ConfigureAwait(true);
        if (ok)
        {
            _undo.CompleteUndo(entry, succeeded: true);
            ShowBar(UnitMapText.AdjacencyUndoneBar(name, otherName), isError: false);
            await RefreshDetailIfAffectedAsync(entry.UnitId, entry.OtherId).ConfigureAwait(true);
            return;
        }
        await ReportOrgWriteFailedAsync(reason => UnitMapText.AdjacencyWriteFailedBar(name, otherName, reason)).ConfigureAwait(true);
    }

    /// <summary>
    /// 편제 쓰기 실패 복구(FR-34) — 콘솔이 조용히 다시 읽어 제자리로 돌리고, 막대는 오류 모양으로 사유 한 줄.
    /// 문구는 한 일에 맞춘다(이동 · 인접 — REVIEW-01 L-2). 사유는 콘솔이 이번 쓰기에서 정한 것(바쁨 포함 — MEDIUM-4).
    /// </summary>
    private async Task ReportOrgWriteFailedAsync(Func<string, string> bar)
    {
        var reason = _options.Console?.LastWriteFailureReason ?? UnitMapText.OrgWriteFailedReason;
        await _commands.ReloadAsync(quiet: true).ConfigureAwait(true);
        ShowBar(bar(reason), isError: true);
    }

    /// <summary>되돌릴 수 없는 항목을 표에서 빼고 까닭을 알린다(남의 변경을 되돌리기로 지우지 않는다).</summary>
    private void Drop(UnitMapUndoEntry entry, string bar)
    {
        _undo.CompleteUndo(entry, succeeded: true);
        ShowBar(bar, isError: true);
    }

    /// <summary>막대 ✕ — 표는 그대로, 문구만 닫고 캔버스로 포커스.</summary>
    public void DismissBar()
    {
        _undo.Dismiss();
        _barText = null;
        _barIsError = false;
        _barAction = null;
        NotifyBar();
        RequestFocus(UnitMapFocusTarget.Canvas);
    }

    private void ShowBar(string text, bool isError, string? action = null)
    {
        _barText = text;
        _barIsError = isError;
        _barAction = action;
        NotifyBar();
    }

    /// <summary>막대 오른쪽의 추가 단추 글(예 "인접선 켜기" — ISSUE-55). 없으면 <c>null</c>.</summary>
    public string? BarActionText => _barAction;

    /// <summary>막대 추가 단추 — 인접선을 켠다(자동으로 켜지 않는다: 결정).</summary>
    public void RunBarAction()
    {
        if (_barAction == UnitMapText.ShowAdjacencyAction) SetLayers(_layers with { Adjacency = true });
        _barAction = null;
        NotifyBar();
        RequestFocus(UnitMapFocusTarget.Canvas);
    }

    private void NotifyBar()
    {
        NotifyOfPropertyChange(nameof(BarText));
        NotifyOfPropertyChange(nameof(BarIsError));
        NotifyOfPropertyChange(nameof(CanUndo));
        NotifyOfPropertyChange(nameof(BarCanUndo));
        NotifyOfPropertyChange(nameof(Bar));
        NotifyOfPropertyChange(nameof(BarActionText));
    }

    private static string Quote(string name) => $"‘{name}’";
    #endregion

    #region - 콘솔이 알리는 편제 변경 (#22 · FR-08) -
    /// <summary>
    /// 트리 레일(관계도 밖)에서 상위 · 인접을 바꿨다 — 관계도 되돌리기 표의 편제 항목을 무효로 한다(#22 · TEST-63 ②):
    /// 막대가 말하는 조작 뒤에 다른 편제 변경이 끼었으므로, 막대 [되돌리기]가 그 사이 바뀐 편제를 되돌리지 않게(보수적으로 뺀다).
    /// 관계도 되돌리기는 트리의 <c>_lastMove</c> 를 쓰지 않는다(REVIEW-01 HIGH-1).
    /// </summary>
    public void OnConsoleStructureChanged()
    {
        var removed = false;
        foreach (var entry in _undo.Entries.Where(e => !e.IsLayout).ToList())
        {
            _undo.CompleteUndo(entry, succeeded: true);
            removed = true;
        }
        if (removed) NotifyBar();
    }

    /// <summary>
    /// 상위 바꾸기 뒤 그 부대의 배치(Δ) 정리(FR-08) — 트리에서 옮긴 경우 콘솔이 부른다(관계도에서 옮긴 경우는 이 VM 이 이미 한다).
    /// <b>언제나 대기열을 탄다</b>(한 번에 하나 — NFR-12): 앞선 배치 쓰기가 응답을 기다리는 중이면 그 뒤에 읽고 쓴다(REVIEW-01 MEDIUM-3 —
    /// 종전엔 "대기열 깊이 &gt; 0" 이면 곧바로 돌아 앞선 쓰기와 겹쳤다).
    /// </summary>
    /// <param name="insideQueue">
    /// 부르는 쪽이 <b>이미 대기열 작업 안</b>이다 — 그때만 곧바로 한다(대기열 안에서 대기열 끝을 기다리면 자기 자신을 기다린다).
    /// 깊이로 짐작하지 않고 부르는 쪽이 밝힌다. 지금은 대기열 안의 호출부가 없다(관계도 자신의 정리는 <c>ExecuteReparentAsync</c> 가 직접 한다).
    /// </param>
    public Task CleanupAfterParentChangeAsync(int unitId, bool insideQueue = false)
        => insideQueue ? CleanupAfterReparentAsync(unitId) : Serialize(() => CleanupAfterReparentAsync(unitId), NameOf(unitId));
    #endregion

    #region - 보여 주기 (FR-46) -
    private int? _pendingReveal;

    /// <summary>
    /// 지도 · 다른 창에서 온 "이 부대를 보여 달라" — 선택 표시 + 가운데(배율 유지). 캔버스가 아직 없으면 붙을 때 한다.
    /// 콘솔 선택은 콘솔이 이미 바꿨다(<c>TryRevealAsync</c>).
    /// </summary>
    public void Reveal(int unitId)
    {
        if (_tree.Find(unitId) is null) return;
        SelectedUnitId = unitId;
        if (_surface is { } surface && _viewRestored) surface.CenterOn(unitId);
        else _pendingReveal = unitId;
    }
    #endregion

    #region - 캔버스 오버레이 명령 (IUnitMapOverlayCommands) -
    /// <summary>확인 오버레이 [확정] · [취소] · Enter · Esc.</summary>
    public void Confirm(bool accept)
    {
        if (accept) _ = ConfirmAsync();
        else CancelConfirm();
    }

    /// <summary>막대 [되돌리기](<c>Units.Map.Undo</c>) — 막대가 말하는 항목일 때만(#22).</summary>
    public void Undo()
    {
        if (BarCanUndo) _ = UndoAsync();
    }

    /// <summary>[다시 시도](<c>Units.Map.LayoutRetry</c>).</summary>
    public void RetryLayout()
    {
        if (CanRetryLayout) _ = RetryLayoutAsync();
    }
    #endregion

    #region - 창 수명 -
    /// <summary>창 비활성화 — 끄는 중이면 취소(서버 0). 확인 오버레이는 남긴다(돌아와 계속 — SIM-F125).</summary>
    public void OnWindowDeactivated()
    {
        if (_draggingUnit is int unit) CancelDrag(unit);
    }

    /// <summary>창 닫기 — 오버레이 · M 모드 · 끌기를 모두 서버 0 으로 거두고 대기 중 합침을 끊는다(SIM-F126).</summary>
    public void CancelAll()
    {
        if (_draggingUnit is int unit) CancelDrag(unit);
        if (_pending is not null) ClosePending(focusCanvas: false);
        if (_moveMode) ExitMoveMode(focusCanvas: false);
        _noticeTrigger.Cancel();
        _selectTrigger.Cancel();
        _pendingSelect = null;
        _firstNoticeAt = null;
    }

    /// <summary>
    /// 닫힌다 — 오버레이 · M 모드 · 끌기를 거두고, <b>아직 시작하지 않은</b> 대기열 작업 · 연기 감시 · 콘솔 한가 기다림을 멈춘다(REVIEW-01 L-7).
    /// 이미 나간 요청 하나는 돌아오기만 하고 이어지는 쓰기는 없다 — 닫기 전에 그 결과를 볼지는 콘솔의 닫기 가드가 묻는다
    /// (<see cref="WaitForWritesAsync"/>).
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelDeferWatch();
        _closing.Cancel();
        CancelAll();
        SaveView();
        _commands.SelectedUnitChanged -= OnConsoleSelectionChanged;
        if (_surface is not null) _surface.ViewChanged -= OnSurfaceViewChanged;
        _viewSaveTrigger.Cancel();
        if (_options.Console is { } bridge) bridge.BusyChanged -= OnConsoleBusyChanged;
        _options.Events?.Unsubscribe(this);
    }
    #endregion

    #region - 대기열 · 상태 알림 -
    /// <summary>서버 쓰기 · 배치 재조회를 UI 스레드의 단일 대기열에 싣는다(NFR-12 — 한 번에 하나, 도착 순서대로).</summary>
    /// <param name="subject">예기치 못한 실패 때 막대가 말할 대상(부대 이름) — 없으면 일반 문구.</param>
    private Task Serialize(Func<Task> work, string? subject = null)
    {
        _queued++;
        NotifyOfPropertyChange(nameof(IsWriting));
        var task = RunAfterAsync(_tail, work, subject);
        _tail = task;
        return task;
    }

    private async Task RunAfterAsync(Task previous, Func<Task> work, string? subject)
    {
        try { await previous.ConfigureAwait(true); }
        catch { /* 앞 작업의 실패는 그쪽 막대가 알렸다 */ }

        try
        {
            // 닫힌 뒤 차례가 온 작업은 보내지 않는다(REVIEW-01 L-7) — 아무도 결과를 보지 못한다.
            if (!_closing.IsCancellationRequested) await work().ConfigureAwait(true);
        }
        catch (OperationCanceledException) { /* 닫는 중 */ }
        catch (Exception ex)
        {
            // 예외 원문(영문 · 내부 정보)은 화면에 싣지 않는다 — 기록으로(REVIEW-01 L-6 · U-18 공통 규칙).
            _options.Log?.Error($"[UnitMap] 대기열 작업 실패({subject ?? "-"}): {ex}");
            ShowBar(UnitMapText.UnexpectedFailureBar(subject), isError: true);
        }
        finally
        {
            _queued--;
            NotifyOfPropertyChange(nameof(IsWriting));
            UpdateBusy();
        }
    }

    /// <summary>
    /// 대기열이 비기를 <paramref name="wait"/> 만큼만 기다린다 — 창 닫기 가드가 "쓰는 중에 말없이 닫기" 를 막을 때 쓴다(REVIEW-01 L-7).
    /// </summary>
    /// <returns>비었으면 <c>true</c>(더 쓰는 것이 없다).</returns>
    public async Task<bool> WaitForWritesAsync(TimeSpan wait)
    {
        if (!IsWriting) return true;
        using var stop = new CancellationTokenSource();
        await Task.WhenAny(WhenIdleAsync(), SafeDelay(wait, stop.Token)).ConfigureAwait(true);
        stop.Cancel();
        return !IsWriting;
    }

    /// <summary>대기열이 빌 때까지(시험 · 닫기 전 정리).</summary>
    public async Task WhenIdleAsync()
    {
        Task tail;
        do
        {
            tail = _tail;
            try { await tail.ConfigureAwait(true); } catch { /* 막대가 알렸다 */ }
        } while (!ReferenceEquals(tail, _tail));
    }

    private void RequestFocus(UnitMapFocusTarget target) => FocusRequested?.Invoke(this, target);

    /// <summary>손 · 화면 상태가 바뀌었다 — 재적재 문을 갱신하고, 풀렸으면 미룬 배치 알림을 다시 판정한다.</summary>
    private void UpdateBusy()
    {
        var defers = _pending is not null || _moveMode || _draggingUnit is not null;
        if (defers != _defersReload)
        {
            _defersReload = defers;
            NotifyOfPropertyChange(nameof(DefersReload));
            DefersReloadChanged?.Invoke(this, EventArgs.Empty);
        }
        NotifyOfPropertyChange(nameof(CanUndo));
        NotifyOfPropertyChange(nameof(BarCanUndo));
        NotifyOfPropertyChange(nameof(Bar));
        if (Busy == UnitMapBusy.None) ReplayDeferredNotice();
    }
    #endregion
}
