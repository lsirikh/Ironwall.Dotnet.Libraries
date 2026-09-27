using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
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

    /// <summary>시계 — 캔버스의 휠 합침 · 알림 합침에 넘긴다(<c>IClock</c> — 시험은 가짜).</summary>
    public Ironwall.Dotnet.Libraries.Base.Services.IClock? Clock { get; init; }

    /// <summary>이 운영자 이름 — 배치 "마지막 변경"이 나면 "나"로 적는다.</summary>
    public string? CurrentOperatorName { get; init; }

    /// <summary>화살표 선택 이동 뒤 콘솔 선택(상세 GET)까지 기다리는 창 — 자동 반복 키의 GET 폭주 방지(필수 항목 2).</summary>
    public TimeSpan SelectionDebounce { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>배치 알림 합침 창(FR-53).</summary>
    public TimeSpan NoticeCoalesce { get; init; } = TimeSpan.FromMilliseconds(500);
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
    IUnitMapOverlayCommands, IHandle<UnitLayoutChangedMessage>, IHandle<MapLocateResult>, IDisposable
{
    /// <summary>서버가 받는 Δ 한계(S-1 ⑤ · 시나리오 ISSUE-40).</summary>
    public const double MaxDelta = 1_000_000;

    /// <summary>콘솔이 바쁠 때 [확정]을 누르면 보이는 사유(#23).</summary>
    public const string ConsoleBusyStatus = "앞선 작업을 마치는 중입니다 — 끝나면 다시 확정하세요.";

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

    public UnitMapViewModel(IUnitMapCommands commands, IUnitLayoutApi layoutApi, UnitMapViewModelOptions? options = null)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _api = layoutApi ?? throw new ArgumentNullException(nameof(layoutApi));
        _options = options ?? new UnitMapViewModelOptions();

        _noticeTrigger = new CoalescingTrigger(OnNoticeSettledAsync, _options.NoticeCoalesce, _options.Delay);
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

    /// <summary>캔버스 아래 막대 문구(<c>Units.Map.UndoMessage</c>). 다음 조작까지 남는다(타이머 없음).</summary>
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
    public UnitMapBar? Bar => _barText is { } text ? new UnitMapBar(text, _barIsError, BarCanUndo) : null;

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
        if (_moveMode && (_moveUnit is not int mu || _tree.Find(mu) is null)) ExitMoveMode(focusCanvas: false);
        if (_selected is int s && _tree.Find(s) is null) SelectedUnitId = _commands.SelectedUnitId;

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
                IsMine: _options.MyUnitId == node.Id,
                IsMoved: deltas.ContainsKey(node.Id),
                IsDimmed: _highlight is EnumUnitEchelon e && node.Echelon != e);
        }
        Scene = new UnitMapScene(_tree, layout.Positions, facts, _layers);
    }

    private static Vector ClampDelta(Vector delta)
        => new(Math.Clamp(double.IsFinite(delta.X) ? delta.X : 0, -MaxDelta, MaxDelta),
               Math.Clamp(double.IsFinite(delta.Y) ? delta.Y : 0, -MaxDelta, MaxDelta));
    #endregion

    #region - 선택 다리 (FR-02) -
    /// <summary>노드 클릭 · UIA <c>Select()</c> · 오른쪽 클릭(조정자 결정 — 선택만) — 가드를 거쳐 곧바로 고른다.</summary>
    public void RequestSelect(int unitId)
    {
        if (_pending is not null || _tree.Find(unitId) is null) return;
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
        _surface = surface;
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
                            UnitMapText.ConfirmReparent(_tree, request.UnitId, decision.TargetId.Value));
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

        return pending.Kind switch
        {
            UnitMapConfirmKind.Reparent => Serialize(() => ExecuteReparentAsync(pending)),
            UnitMapConfirmKind.Adjoin => Serialize(() => ExecuteAdjoinAsync(pending)),
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
        await ReportOrgWriteFailedAsync(name).ConfigureAwait(true);
    }

    private async Task ExecuteAdjoinAsync(UnitMapPendingConfirm pending)
    {
        if (!await RevalidateAsync(pending).ConfigureAwait(true)) return;
        var (unitId, otherId) = (pending.MovingId!.Value, pending.TargetId!.Value);
        var name = NameOf(unitId);
        var otherName = NameOf(otherId);
        if (await _commands.ChangeAdjacencyAsync(unitId, otherId, null).ConfigureAwait(true))
        {
            _undo.Push(new UnitMapAdjacencyUndo(unitId, otherId, Added: true));
            ShowBar(UnitMapText.AdjoinedBar(name, otherName), isError: false);
            await RefreshDetailIfAffectedAsync(unitId, otherId).ConfigureAwait(true);
            return;
        }
        await ReportOrgWriteFailedAsync(name).ConfigureAwait(true);
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
    /// 트리와 공유하는 <c>_lastMove</c> 가 그 사이 다른 이동을 가리킬 수 있어(#22) 반대 방향을 직접 보낸다.
    /// </summary>
    private async Task UndoReparentAsync(UnitMapReparentUndo entry)
    {
        await _commands.ReloadAsync(quiet: true).ConfigureAwait(true);

        var name = NameOf(entry.UnitId);
        var node = _tree.Find(entry.UnitId);
        if (node is null) { Drop(entry, UnitMapText.UndoUnitGoneBar(name)); return; }

        var facts = _reparentFacts.TryGetValue(entry, out var f) ? f : null;
        var chainNow = UnitMapLayoutSync.TouchedUnits(_tree, entry.UnitId);
        var unchanged = node.ParentId == entry.ToParentId && (facts is null || chainNow.SequenceEqual(facts.ChainAfterMove));
        if (!unchanged) { Drop(entry, UnitMapText.ReparentUndoRefusedBar(name)); return; }

        var ok = entry.FromParentId is int from
            ? await _commands.MoveAsync(entry.UnitId, from).ConfigureAwait(true)
            : await _commands.UndoMoveAsync().ConfigureAwait(true);
        if (ok)
        {
            _undo.CompleteUndo(entry, succeeded: true);
            var bar = UnitMapText.ReparentUndoneBar(name);
            if (facts?.HadDelta == true) bar += " 옮겨 두었던 위치는 되살리지 않습니다 — 자동 배치로 보입니다.";
            ShowBar(bar, isError: false);
            await RefreshDetailIfAffectedAsync(entry.UnitId, entry.FromParentId, entry.ToParentId).ConfigureAwait(true);
            return;
        }
        await ReportOrgWriteFailedAsync(name).ConfigureAwait(true);
    }

    /// <summary>인접 되돌리기 — 먼저 편제를 다시 읽고, 이미 반영돼 있으면 보내지 않는다(SIM-F120).</summary>
    private async Task UndoAdjacencyAsync(UnitMapAdjacencyUndo entry)
    {
        await _commands.ReloadAsync(quiet: true).ConfigureAwait(true);

        var name = NameOf(entry.UnitId);
        var otherName = NameOf(entry.OtherId);
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
        await ReportOrgWriteFailedAsync(name).ConfigureAwait(true);
    }

    /// <summary>편제 쓰기 실패 복구(FR-34) — 콘솔이 조용히 다시 읽어 제자리로 돌리고, 막대는 오류 모양으로 사유 한 줄.</summary>
    private async Task ReportOrgWriteFailedAsync(string unitName)
    {
        await _commands.ReloadAsync(quiet: true).ConfigureAwait(true);
        ShowBar(UnitMapText.WriteFailedBar(unitName, UnitMapText.OrgWriteFailedReason), isError: true);
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
        NotifyBar();
        RequestFocus(UnitMapFocusTarget.Canvas);
    }

    private void ShowBar(string text, bool isError)
    {
        _barText = text;
        _barIsError = isError;
        NotifyBar();
    }

    private void NotifyBar()
    {
        NotifyOfPropertyChange(nameof(BarText));
        NotifyOfPropertyChange(nameof(BarIsError));
        NotifyOfPropertyChange(nameof(CanUndo));
        NotifyOfPropertyChange(nameof(BarCanUndo));
        NotifyOfPropertyChange(nameof(Bar));
    }

    private static string Quote(string name) => $"‘{name}’";
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
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelAll();
        SaveView();
        _commands.SelectedUnitChanged -= OnConsoleSelectionChanged;
        if (_options.Console is { } bridge) bridge.BusyChanged -= OnConsoleBusyChanged;
        _options.Events?.Unsubscribe(this);
    }
    #endregion

    #region - 대기열 · 상태 알림 -
    /// <summary>서버 쓰기 · 배치 재조회를 UI 스레드의 단일 대기열에 싣는다(NFR-12 — 한 번에 하나, 도착 순서대로).</summary>
    private Task Serialize(Func<Task> work)
    {
        _queued++;
        NotifyOfPropertyChange(nameof(IsWriting));
        var task = RunAfterAsync(_tail, work);
        _tail = task;
        return task;
    }

    private async Task RunAfterAsync(Task previous, Func<Task> work)
    {
        try { await previous.ConfigureAwait(true); }
        catch { /* 앞 작업의 실패는 그쪽 막대가 알렸다 */ }

        try
        {
            await work().ConfigureAwait(true);
        }
        catch (OperationCanceledException) { /* 닫는 중 */ }
        catch (Exception ex)
        {
            ShowBar($"작업을 마치지 못했습니다. {ex.Message}", isError: true);
        }
        finally
        {
            _queued--;
            NotifyOfPropertyChange(nameof(IsWriting));
            UpdateBusy();
        }
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
