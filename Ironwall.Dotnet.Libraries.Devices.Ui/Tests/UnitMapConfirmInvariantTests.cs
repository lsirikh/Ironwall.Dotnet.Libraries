using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// unit-relationship-map RISK-02 (FR-29 · FR-32 · FR-33) — "확인 전 서버 0" 불변식을 뷰모델 구현 <b>전에</b> 잠근다.
/// 위치를 옮기려다 노드 위에 떨어뜨려 상위가 바뀌는 오조작(PRD 리스크 "높음")의 안전망이다.
/// 시나리오: SIM-F124 · F125 · F126 · SIM-G424~G469(오버레이 중 입력 차단) · 조정자 필수 항목 1(오버레이 · M 모드 동안 재적재 보류).
/// </summary>
public class UnitMapConfirmInvariantTests
{
    private static UnitMapDropRequest Drop(UnitMapFixture f, string moving, string? hover, bool ctrl = false, double dx = 30, double dy = 10)
        => new(f.IdOf(moving), dx, dy, hover is null ? null : f.IdOf(hover), ctrl);

    [Fact]
    public async Task should_only_open_confirm_when_dropped_on_higher_echelon()
    {
        // Arrange
        var kit = await MapKit.OpenAsync();

        // Act
        kit.Vm.BeginDrag(kit.F.IdOf("8중대"));
        kit.Vm.CompleteDrag(Drop(kit.F, "8중대", "3대대"));
        await kit.Vm.WhenIdleAsync();

        // Assert — 확인 오버레이만, 편제 · 배치 호출 0
        Assert.NotNull(kit.Vm.PendingConfirm);
        Assert.Equal(UnitMapConfirmKind.Reparent, kit.Vm.PendingConfirm!.Kind);
        Assert.Equal(kit.F.IdOf("3대대"), kit.Vm.PendingConfirm.TargetId);
        Assert.Equal(0, kit.Commands.WriteCalls);
        Assert.Empty(kit.Api.Writes);
    }

    [Fact]
    public async Task should_only_open_confirm_when_dropped_on_same_echelon()
    {
        var kit = await MapKit.OpenAsync();

        kit.Vm.CompleteDrag(Drop(kit.F, "6중대", "9중대"));
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(UnitMapConfirmKind.Adjoin, kit.Vm.PendingConfirm!.Kind);
        Assert.Equal(0, kit.Commands.WriteCalls);
        Assert.Empty(kit.Api.Writes);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("esc")]
    [InlineData("close")]
    public async Task should_call_server_zero_times_when_confirm_is_cancelled(string how)
    {
        var kit = await MapKit.OpenAsync();
        kit.Vm.CompleteDrag(Drop(kit.F, "8중대", "3대대"));

        switch (how)
        {
            case "cancel": kit.Vm.CancelConfirm(); break;
            case "esc": Assert.True(kit.Vm.HandleKey(UnitMapKeyCommand.Escape, shift: false)); break;
            default: kit.Vm.CancelAll(); break;       // 창 닫기(SIM-F126)
        }
        await kit.Vm.WhenIdleAsync();

        Assert.Null(kit.Vm.PendingConfirm);
        Assert.Equal(0, kit.Commands.WriteCalls);
        Assert.Empty(kit.Api.Writes);
    }

    [Fact]
    public async Task should_keep_overlay_and_call_nothing_when_window_deactivated()
    {
        // SIM-F125 — 창 비활성화는 오버레이를 유지한다(돌아와 계속). 그동안 호출 0.
        var kit = await MapKit.OpenAsync();
        kit.Vm.CompleteDrag(Drop(kit.F, "8중대", "3대대"));

        kit.Vm.OnWindowDeactivated();
        await kit.Vm.WhenIdleAsync();

        Assert.NotNull(kit.Vm.PendingConfirm);
        Assert.Equal(0, kit.Commands.WriteCalls);
        Assert.Empty(kit.Api.Writes);
    }

    [Fact]
    public async Task should_move_exactly_once_when_confirmed()
    {
        var kit = await MapKit.OpenAsync();
        kit.Vm.CompleteDrag(Drop(kit.F, "8중대", "3대대"));

        // Enter 를 두 번 빨리 눌러도 한 번
        var first = kit.Vm.ConfirmAsync();
        var second = kit.Vm.ConfirmAsync();
        await Task.WhenAll(first, second);
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(new[] { $"Move:{kit.F.IdOf("8중대")}->{kit.F.IdOf("3대대")}" }, kit.Commands.Writes);
        Assert.Empty(kit.Api.Writes);
        Assert.Null(kit.Vm.PendingConfirm);
    }

    [Theory]
    [InlineData("3대대")]      // 상위 판정 자리
    [InlineData("9중대")]      // 인접 판정 자리
    [InlineData("11소초")]     // 막힘 자리
    [InlineData(null)]
    public async Task should_write_position_once_and_no_command_when_ctrl_drop(string? hover)
    {
        var kit = await MapKit.OpenAsync();

        kit.Vm.CompleteDrag(Drop(kit.F, "8중대", hover, ctrl: true));
        await kit.Vm.WhenIdleAsync();

        var write = Assert.Single(kit.Api.Writes);
        Assert.Equal(new[] { kit.F.IdOf("8중대") }, write.Change.Set.Keys);
        Assert.Equal(0, kit.Commands.WriteCalls);
        Assert.Null(kit.Vm.PendingConfirm);
    }

    [Fact]
    public async Task should_ignore_second_drop_and_other_input_while_confirm_overlay_is_open()
    {
        var kit = await MapKit.OpenAsync();
        kit.Vm.CompleteDrag(Drop(kit.F, "8중대", "3대대"));
        var pending = kit.Vm.PendingConfirm;

        kit.Vm.CompleteDrag(Drop(kit.F, "6중대", null));                 // 위치 드롭
        kit.Vm.CompleteDrag(Drop(kit.F, "6중대", "9중대"));              // 인접 드롭
        Assert.True(kit.Vm.HandleKey(UnitMapKeyCommand.MoveMode, false)); // 삼킨다
        Assert.True(kit.Vm.HandleKey(UnitMapKeyCommand.Undo, false));
        Assert.True(kit.Vm.HandleKey(UnitMapKeyCommand.ParentUp, false));
        await kit.Vm.UndoAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.Same(pending, kit.Vm.PendingConfirm);
        Assert.False(kit.Vm.IsMoveMode);
        Assert.Equal(0, kit.Commands.WriteCalls);
        Assert.Empty(kit.Api.Writes);
    }

    [Fact]
    public async Task should_defer_reload_while_overlay_or_move_mode_and_announce_release()
    {
        // 조정자 필수 항목 1 — 오버레이 · M 모드 동안 콘솔의 재적재를 막는다
        var kit = await MapKit.OpenAsync();
        var changes = 0;
        kit.Vm.DefersReloadChanged += (_, _) => changes++;
        Assert.False(kit.Vm.DefersReload);

        kit.Vm.CompleteDrag(Drop(kit.F, "8중대", "3대대"));
        Assert.True(kit.Vm.DefersReload);
        kit.Vm.CancelConfirm();
        Assert.False(kit.Vm.DefersReload);

        kit.Vm.RequestSelect(kit.F.IdOf("8중대"));
        Assert.True(kit.Vm.HandleKey(UnitMapKeyCommand.MoveMode, false));
        Assert.True(kit.Vm.IsMoveMode);
        Assert.True(kit.Vm.DefersReload);
        Assert.True(kit.Vm.HandleKey(UnitMapKeyCommand.Escape, false));
        Assert.False(kit.Vm.DefersReload);

        kit.Vm.BeginDrag(kit.F.IdOf("8중대"));
        Assert.True(kit.Vm.DefersReload);
        kit.Vm.CancelDrag(kit.F.IdOf("8중대"));
        Assert.False(kit.Vm.DefersReload);

        Assert.Equal(6, changes);   // 켜짐 · 꺼짐 × 3
        Assert.Equal(0, kit.Commands.WriteCalls);
        Assert.Empty(kit.Api.Writes);
    }

    [Fact]
    public async Task should_call_server_zero_times_while_dragging()
    {
        // NFR-04 — 끄는 동안(판정 · 머묾) 포트 호출 0, 놓을 때 1회
        var kit = await MapKit.OpenAsync();
        var reads = kit.Api.ReadCount;
        var moving = kit.F.IdOf("8중대");

        kit.Vm.BeginDrag(moving);
        foreach (var node in kit.F.Tree.Ordered) kit.Vm.Classify(moving, node.Id, ctrl: false);
        kit.Vm.Classify(moving, null, ctrl: false);

        Assert.Equal(reads, kit.Api.ReadCount);
        Assert.Empty(kit.Api.Writes);
        Assert.Equal(0, kit.Commands.WriteCalls);
        kit.Vm.CancelDrag(moving);
    }

    #region - 캔버스 오버레이 계약(레인 C UnitMapCanvas.Overlays — IUnitMapOverlayCommands) -
    [Fact]
    public async Task should_expose_confirm_prompt_and_disable_ok_with_reason_while_console_busy()
    {
        var kit = await MapKit.OpenAsync();
        kit.Vm.CompleteDrag(Drop(kit.F, "8중대", "3대대"));

        var prompt = kit.Vm.ConfirmPrompt!;
        Assert.Equal("상위 부대 바꾸기", prompt.Title);
        Assert.Equal("옮기기", prompt.OkText);
        Assert.True(prompt.CanConfirm);

        kit.Bridge.IsBusy = true;
        Assert.False(kit.Vm.ConfirmPrompt!.CanConfirm);
        Assert.Equal(UnitMapViewModel.ConsoleBusyStatus, kit.Vm.ConfirmPrompt.BusyText);
    }

    [Fact]
    public async Task should_call_server_zero_times_when_overlay_command_cancels()
    {
        var kit = await MapKit.OpenAsync();
        IUnitMapOverlayCommands overlay = kit.Vm;
        kit.Vm.CompleteDrag(Drop(kit.F, "8중대", "3대대"));

        overlay.Confirm(false);
        await kit.Vm.WhenIdleAsync();

        Assert.Null(kit.Vm.ConfirmPrompt);
        Assert.Equal(0, kit.Commands.WriteCalls);

        kit.Vm.CompleteDrag(Drop(kit.F, "8중대", "3대대"));
        overlay.Confirm(true);
        await kit.Vm.WhenIdleAsync();
        Assert.Equal(1, kit.Commands.WriteCalls);
    }

    [Fact]
    public async Task should_expose_bar_with_undo_and_move_mode_text_for_canvas()
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        IUnitMapOverlayCommands overlay = kit.Vm;
        kit.Vm.CompleteDrag(Drop(kit.F, "6중대", null));
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(new UnitMapBar("‘6중대’ 위치를 옮겼습니다 — 모든 운영자에게 보입니다.", false, true), kit.Vm.Bar);
        overlay.Undo();
        await kit.Vm.WhenIdleAsync();
        Assert.Equal(2, api.Writes.Count);
        Assert.False(kit.Vm.Bar!.CanUndo);

        kit.Vm.RequestSelect(kit.F.IdOf("6중대"));
        kit.Vm.HandleKey(UnitMapKeyCommand.MoveMode, false);
        Assert.Contains("위치 이동", kit.Vm.MoveModeText);
        kit.Vm.HandleKey(UnitMapKeyCommand.Escape, false);
        Assert.Null(kit.Vm.MoveModeText);
    }
    #endregion
}

#region - 관계도 뷰모델 시험 도구 -
/// <summary>관계도 뷰모델 시험 한 벌 — 편제 200 · 가짜 콘솔 · 가짜 배치 서버 · 손으로 흘리는 시간.</summary>
internal sealed class MapKit
{
    public UnitMapFixture F { get; }
    public FakeMapCommands Commands { get; }
    public FakeUnitLayoutApi Api { get; }
    public GatedLayoutApi Gate { get; }
    public ManualDelay Delay { get; }
    public FakeMapSurface Surface { get; } = new();
    public List<object> Published { get; } = new();
    public FakeConsoleBridge Bridge { get; } = new();
    public ConsolePrefsProbe Prefs { get; } = new();
    public MutableClock Clock { get; } = new();
    public UnitMapViewModel Vm { get; }
    public IReadOnlyList<UnitDeviceItem> Devices { get; }

    public CapturingLog Log { get; } = new();

    private MapKit(FakeUnitLayoutApi api, int? myUnitId, bool canEdit, bool canView, UnitMapFixture? fixture, int? clientLayoutVersion = null,
                   IUnitLayoutApi? port = null)
    {
        F = fixture ?? UnitMapTestData.Standard200();
        Commands = new FakeMapCommands(F) { CanEdit = canEdit, CanView = canView };
        Api = api;
        ClientLayoutVersion = clientLayoutVersion ?? UnitMapLayout.LayoutVersion;
        api.ClientLayoutVersion = ClientLayoutVersion;          // 어댑터가 싣는 판 = 뷰모델이 견주는 판(같아야 한다)
        Gate = new GatedLayoutApi(port ?? api);                  // port = 같은 가짜 서버의 다른 운영자 창구(두 운영자 시험)
        Delay = new ManualDelay();
        Devices = UnitMapTestData.Devices(F.Tree).Items;
        Vm = new UnitMapViewModel(Commands, Gate, new UnitMapViewModelOptions
        {
            Events = new CapturingEventAggregator(Published),
            Delay = Delay.Run,
            Console = Bridge,
            MyUnitId = myUnitId,
            CurrentOperatorName = "나운영",
            Prefs = Prefs.Entry,
            SavePrefs = Prefs.Save,
            Clock = Clock,
            Log = Log,
            ClientLayoutVersion = ClientLayoutVersion,
        });
        Vm.SetData(F.Tree, Devices);
    }

    /// <summary>이 시험의 클라 자동 배치 판(기본 = 알고리즘 판). 판 올림 시험은 새 판 클라를 흉내 낸다.</summary>
    public int ClientLayoutVersion { get; }

    /// <param name="port">같은 가짜 서버의 다른 클라이언트 창구(<see cref="FakeUnitLayoutApi.ForClient"/>) — 두 운영자 시험. 없으면 기본 창구 "main".</param>
    public static MapKit Create(FakeUnitLayoutApi? api = null, int? myUnitId = null, bool canEdit = true, bool canView = true, UnitMapFixture? fixture = null,
                                int? clientLayoutVersion = null, IUnitLayoutApi? port = null)
        => new(api ?? new FakeUnitLayoutApi(), myUnitId, canEdit, canView, fixture, clientLayoutVersion, port);

    public static async Task<MapKit> OpenAsync(FakeUnitLayoutApi? api = null, int? myUnitId = null, bool canEdit = true, bool canView = true, UnitMapFixture? fixture = null,
                                               int? clientLayoutVersion = null, IUnitLayoutApi? port = null)
    {
        var kit = Create(api, myUnitId, canEdit, canView, fixture, clientLayoutVersion, port);
        await kit.Vm.OpenAsync();
        await kit.Vm.WhenIdleAsync();
        return kit;
    }

    public int Id(string name) => F.IdOf(name);
}

/// <summary>
/// 가짜 부대 콘솔 — 편제 쓰기 · 선택을 기록한다. 진짜 콘솔처럼 <b>상위(부모)를 기억</b>한다(<see cref="CurrentTree"/> · <see cref="ParentOf"/>) —
/// 되돌리기 시험이 "어느 부대가 어디로 갔는가" 를 호출 문자열이 아니라 결과 편제로 본다(REVIEW-01 MEDIUM-T1).
/// </summary>
/// <remarks>
/// REVIEW-01 전에는 이 가짜가 <c>UndoMoveAsync</c> 를 호출 기록만 하고 늘 성공을 돌려줘, 진짜 콘솔의 <b>하나뿐인 공유 <c>_lastMove</c></b>
/// (다른 이동이 덮으면 엉뚱한 부대를 옮기고, 비었으면 옮기지 않고 "성공")를 가렸다. 빨간 시험 단계에서 그 공유 칸을 그대로 흉내 내
/// 결함을 드러낸 뒤, 계약에서 <c>UndoMoveAsync</c> 를 빼고(관계도 되돌리기 = 정확한 반대 <see cref="MoveAsync"/>) 여기서도 뺐다.
/// </remarks>
internal sealed class FakeMapCommands : IUnitMapCommands
{
    private readonly UnitMapFixture _fixture;
    private readonly Dictionary<int, int?> _parents;

    public FakeMapCommands(UnitMapFixture fixture)
    {
        _fixture = fixture;
        _parents = fixture.Graph.Nodes.ToDictionary(n => n.Id, n => n.ParentId);
    }

    /// <summary>지금 서버 편제(성공한 이동이 반영된) — 콘솔 재조회 흉내에 쓴다.</summary>
    public UnitTreeModel CurrentTree()
    {
        var nodes = _fixture.Graph.Nodes
            .Select(n => UnitMapTestData.Node(n.Id, n.Code, n.Name, n.EchelonRaw, _parents.TryGetValue(n.Id, out var p) ? p : n.ParentId, n.IsEnable))
            .ToList();
        return UnitMapTestData.Tree(UnitMapTestData.Graph(nodes, adjacency: _fixture.Graph.Edges.AdjacencyPairs.ToList()));
    }

    /// <summary>그 부대의 지금 상위.</summary>
    public int? ParentOf(int unitId) => _parents.TryGetValue(unitId, out var p) ? p : null;

    public int? SelectedUnitId { get; set; }
    public event EventHandler? SelectedUnitChanged;
    public bool CanView { get; set; } = true;
    public bool CanEdit { get; set; } = true;

    /// <summary>네비게이션 가드가 허락하는가.</summary>
    public bool AllowSelect { get; set; } = true;
    public bool MoveResult { get; set; } = true;
    public bool AdjacencyResult { get; set; } = true;

    /// <summary>편제 쓰기 응답을 붙잡는다(한 번에 하나 시험).</summary>
    public TaskCompletionSource<bool>? WriteGate { get; set; }

    public List<string> Calls { get; } = new();
    public IEnumerable<string> Writes => Calls.Where(IsWrite);
    public int WriteCalls => Calls.Count(IsWrite);
    public int SelectCalls => Calls.Count(c => c.StartsWith("Select:", StringComparison.Ordinal));
    public int ReloadCalls => Calls.Count(c => c.StartsWith("Reload", StringComparison.Ordinal));

    private static bool IsWrite(string c) => c.StartsWith("Move", StringComparison.Ordinal) || c.StartsWith("Adj", StringComparison.Ordinal);

    public void RaiseSelected(int? id) { SelectedUnitId = id; SelectedUnitChanged?.Invoke(this, EventArgs.Empty); }

    public bool TrySelect(int unitId)
    {
        Calls.Add($"Select:{unitId}");
        if (!AllowSelect) return false;
        RaiseSelected(unitId);
        return true;
    }

    /// <summary>상위 변경이 성공했을 때 할 일(서버가 같은 트랜잭션에서 배치 행을 지우는 것 등을 흉내).</summary>
    public System.Action? OnMoveSucceeded { get; set; }

    /// <summary>편제 쓰기에서 던질 예외(대기열의 예기치 못한 실패 흉내).</summary>
    public Exception? ThrowOnWrite { get; set; }

    /// <summary>콘솔이 바쁜가(시험이 다리의 값을 넘긴다) — 바쁜 콘솔에 온 쓰기를 센다(진짜 콘솔은 거절한다).</summary>
    public Func<bool>? IsConsoleBusy { get; set; }
    public int WritesWhileBusy { get; private set; }

    /// <summary>첫 편제 쓰기가 왔다.</summary>
    public TaskCompletionSource FirstWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private void NoteWrite()
    {
        if (IsConsoleBusy?.Invoke() == true) WritesWhileBusy++;
        FirstWrite.TrySetResult();
    }

    public async Task<bool> MoveAsync(int movingId, int? targetId, CancellationToken token = default)
    {
        Calls.Add($"Move:{movingId}->{targetId?.ToString() ?? "root"}");
        NoteWrite();
        if (WriteGate is { } gate) await gate.Task.ConfigureAwait(false);
        if (ThrowOnWrite is { } ex) throw ex;
        if (MoveResult) ApplyMove(movingId, targetId);
        return MoveResult;
    }

    private void ApplyMove(int movingId, int? targetId)
    {
        _parents[movingId] = targetId;
        OnMoveSucceeded?.Invoke();
    }

    public async Task<bool> ChangeAdjacencyAsync(int unitId, int? add, int? remove, CancellationToken token = default)
    {
        Calls.Add($"Adj:{unitId}+{add?.ToString() ?? "-"}-{remove?.ToString() ?? "-"}");
        NoteWrite();
        if (WriteGate is { } gate) await gate.Task.ConfigureAwait(false);
        return AdjacencyResult;
    }

    /// <summary>재조회 때 할 일(콘솔이 새 편제를 관계도에 넘기는 것을 흉내).</summary>
    public System.Action? OnReload { get; set; }

    public Task ReloadAsync(bool quiet, CancellationToken token = default)
    {
        Calls.Add(quiet ? "Reload:quiet" : "Reload");
        OnReload?.Invoke();
        return Task.CompletedTask;
    }
}

/// <summary>가짜 캔버스 면 — 뷰 이동만 기록한다.</summary>
internal sealed class FakeMapSurface : IUnitMapSurface
{
    public double Scale { get; set; } = 0.5;
    public UnitMapLevel Level { get; set; } = UnitMapLevel.L1;
    public Point CenterWorld { get; set; } = new(0, 0);
    public event EventHandler? ViewChanged;
    public List<string> Calls { get; } = new();
    public Func<int, bool> InView { get; set; } = _ => true;

    public void RaiseViewChanged() => ViewChanged?.Invoke(this, EventArgs.Empty);
    public void CenterOn(int unitId, double? scale = null) => Calls.Add(scale is double s ? $"CenterOn:{unitId}@{s:0.00}" : $"CenterOn:{unitId}");
    public void SetView(double scale, Point centerWorld) => Calls.Add($"SetView:{scale:0.00}@{centerWorld.X:0},{centerWorld.Y:0}");
    public void Fit() => Calls.Add("Fit");
    public bool IsInView(int unitId) => InView(unitId);
}

/// <summary>손으로 흘리는 지연 — 합침 · 디바운스 창을 시험이 닫는다(<c>Task.Delay</c> 없음).</summary>
internal sealed class ManualDelay
{
    private readonly List<TaskCompletionSource> _pending = new();

    public int Pending => _pending.Count(t => !t.Task.IsCompleted);

    public Task Run(TimeSpan window, CancellationToken token)
    {
        var tcs = new TaskCompletionSource();
        token.Register(() => tcs.TrySetCanceled());
        _pending.Add(tcs);
        return tcs.Task;
    }

    /// <summary>열린 창을 모두 닫는다(그 뒤에 새로 열린 창은 남는다).</summary>
    public void ElapseAll()
    {
        foreach (var tcs in _pending.ToList()) tcs.TrySetResult();
        _pending.RemoveAll(t => t.Task.IsCompleted);
    }
}

/// <summary>배치 포트 앞에 끼워 쓰기 · 읽기 응답을 붙잡는다(한 번에 하나 · 첫 그림 시험).</summary>
internal sealed class GatedLayoutApi : IUnitLayoutApi
{
    private readonly IUnitLayoutApi _inner;
    private readonly Queue<TaskCompletionSource> _writeHolds = new();
    private TaskCompletionSource? _readHold;

    public GatedLayoutApi(IUnitLayoutApi inner) => _inner = inner;

    private readonly object _gate = new();
    private readonly List<(int Count, TaskCompletionSource Signal)> _startWaiters = new();
    private int _startedWrites;

    public bool HoldWrites { get; set; }
    public bool HoldReads { get; set; }
    public int StartedWrites { get { lock (_gate) return _startedWrites; } }
    public int HeldWrites { get { lock (_gate) return _writeHolds.Count; } }

    /// <summary>쓰기가 <paramref name="count"/> 건 시작될 때까지 — 연속이 다른 스레드에서 이어져도 결정적으로 기다린다(시간 대기 없음, 안전 상한만).</summary>
    public Task WhenWritesStartedAsync(int count)
    {
        lock (_gate)
        {
            if (_startedWrites >= count) return Task.CompletedTask;
            var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _startWaiters.Add((count, signal));
            return signal.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    public async Task<UnitLayoutRead> ReadAsync(CancellationToken token = default)
    {
        if (HoldReads)
        {
            _readHold = new TaskCompletionSource();
            await _readHold.Task.ConfigureAwait(false);
        }
        return await _inner.ReadAsync(token).ConfigureAwait(false);
    }

    public async Task<UnitLayoutWrite> WriteAsync(long ifMatchVersion, UnitLayoutChange change, CancellationToken token = default)
    {
        List<TaskCompletionSource> ready;
        lock (_gate)
        {
            _startedWrites++;
            ready = _startWaiters.Where(w => w.Count <= _startedWrites).Select(w => w.Signal).ToList();
            _startWaiters.RemoveAll(w => w.Count <= _startedWrites);
        }
        foreach (var signal in ready) signal.TrySetResult();
        if (HoldWrites)
        {
            var hold = new TaskCompletionSource();
            lock (_gate) _writeHolds.Enqueue(hold);
            await hold.Task.ConfigureAwait(false);
        }
        return await _inner.WriteAsync(ifMatchVersion, change, token).ConfigureAwait(false);
    }

    public void ReleaseRead() => _readHold?.TrySetResult();

    public void ReleaseNextWrite()
    {
        TaskCompletionSource hold;
        lock (_gate) hold = _writeHolds.Dequeue();
        hold.TrySetResult();
    }
}

/// <summary>가짜 콘솔 다리 — 상세 더러움 · 상세 새로 고침 · 미룬 재적재 · 콘솔 바쁨.</summary>
internal sealed class FakeConsoleBridge : IUnitMapConsoleBridge
{
    private bool _isBusy;
    private EventHandler? _busyChanged;
    private int _subscribers;

    public bool IsDetailDirty { get; set; }
    public int DetailRefreshes { get; private set; }
    public bool HasDeferredReload { get; set; }
    public string? LastWriteFailureReason { get; set; }

    /// <summary>
    /// 관계도 뷰모델은 만들 때 한 번 구독한다 — 그 밖의 구독(대기열이 콘솔이 한가해지기를 기다림)이 붙으면 끝난다.
    /// 시험이 시간 대기 없이 "기다리기 시작했다" 를 안다.
    /// </summary>
    public TaskCompletionSource ExtraBusySubscriber { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public event EventHandler? BusyChanged
    {
        add { _busyChanged += value; if (++_subscribers >= 2) ExtraBusySubscriber.TrySetResult(); }
        remove { _busyChanged -= value; _subscribers--; }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; _busyChanged?.Invoke(this, EventArgs.Empty); }
    }

    public Task RefreshDetailAsync(CancellationToken token = default)
    {
        DetailRefreshes++;
        return Task.CompletedTask;
    }
}

/// <summary>로그를 모은다 — 예외 원문은 화면이 아니라 여기로 가야 한다(REVIEW-01 L-6).</summary>
internal sealed class CapturingLog : Ironwall.Dotnet.Libraries.Base.Services.ILogService
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();

    public void Info(string msg, string memberName = "", string filePath = "", int lineNumber = 0) { }
    public void Warning(string msg, string memberName = "", string filePath = "", int lineNumber = 0) => Warnings.Add(msg);
    public void Error(string msg, string memberName = "", string filePath = "", int lineNumber = 0) => Errors.Add(msg);

#pragma warning disable CS0067 // 시험 가짜 — 발화하지 않는다
    public event EventHandler<Ironwall.Dotnet.Libraries.Base.Services.LogEventArgs>? LogEvent;
#pragma warning restore CS0067
}

/// <summary>손으로 흘리는 시계(<c>IClock</c> — 규칙 I-02).</summary>
internal sealed class MutableClock : Ironwall.Dotnet.Libraries.Base.Services.IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);
    public DateTime Now => UtcNow.ToLocalTime();
    public void Advance(TimeSpan by) => UtcNow += by;
}

/// <summary>개인 표시 설정(메모리) — 저장 횟수와 <c>Extra</c> 키를 본다. 디스크 0.</summary>
internal sealed class ConsolePrefsProbe
{
    public Ironwall.Dotnet.Libraries.Utils.Consoles.ConsolePrefEntry Entry { get; } = new();
    public int Saves { get; private set; }
    public void Save() => Saves++;
}
#endregion
