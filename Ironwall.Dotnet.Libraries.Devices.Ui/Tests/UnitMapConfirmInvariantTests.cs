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
    public UnitMapViewModel Vm { get; }
    public IReadOnlyList<UnitDeviceItem> Devices { get; }

    private MapKit(FakeUnitLayoutApi api, int? myUnitId, bool canEdit, bool canView)
    {
        F = UnitMapTestData.Standard200();
        Commands = new FakeMapCommands(F) { CanEdit = canEdit, CanView = canView };
        Api = api;
        Gate = new GatedLayoutApi(api);
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
        });
        Vm.SetData(F.Tree, Devices);
    }

    public static MapKit Create(FakeUnitLayoutApi? api = null, int? myUnitId = null, bool canEdit = true, bool canView = true)
        => new(api ?? new FakeUnitLayoutApi(), myUnitId, canEdit, canView);

    public static async Task<MapKit> OpenAsync(FakeUnitLayoutApi? api = null, int? myUnitId = null, bool canEdit = true, bool canView = true)
    {
        var kit = Create(api, myUnitId, canEdit, canView);
        await kit.Vm.OpenAsync();
        await kit.Vm.WhenIdleAsync();
        return kit;
    }

    public int Id(string name) => F.IdOf(name);
}

/// <summary>가짜 부대 콘솔 — 편제 쓰기 · 선택을 기록한다.</summary>
internal sealed class FakeMapCommands : IUnitMapCommands
{
    private readonly UnitMapFixture _fixture;

    public FakeMapCommands(UnitMapFixture fixture) => _fixture = fixture;

    public int? SelectedUnitId { get; set; }
    public event EventHandler? SelectedUnitChanged;
    public bool CanView { get; set; } = true;
    public bool CanEdit { get; set; } = true;

    /// <summary>네비게이션 가드가 허락하는가.</summary>
    public bool AllowSelect { get; set; } = true;
    public bool MoveResult { get; set; } = true;
    public bool UndoMoveResult { get; set; } = true;
    public bool AdjacencyResult { get; set; } = true;

    /// <summary>편제 쓰기 응답을 붙잡는다(한 번에 하나 시험).</summary>
    public TaskCompletionSource<bool>? WriteGate { get; set; }

    public List<string> Calls { get; } = new();
    public IEnumerable<string> Writes => Calls.Where(IsWrite);
    public int WriteCalls => Calls.Count(IsWrite);
    public int SelectCalls => Calls.Count(c => c.StartsWith("Select:", StringComparison.Ordinal));
    public int ReloadCalls => Calls.Count(c => c.StartsWith("Reload", StringComparison.Ordinal));

    private static bool IsWrite(string c) => c.StartsWith("Move", StringComparison.Ordinal) || c.StartsWith("UndoMove", StringComparison.Ordinal) || c.StartsWith("Adj", StringComparison.Ordinal);

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

    public async Task<bool> MoveAsync(int movingId, int targetId, CancellationToken token = default)
    {
        Calls.Add($"Move:{movingId}->{targetId}");
        if (WriteGate is { } gate) await gate.Task.ConfigureAwait(false);
        if (MoveResult) OnMoveSucceeded?.Invoke();
        return MoveResult;
    }

    public async Task<bool> UndoMoveAsync(CancellationToken token = default)
    {
        Calls.Add("UndoMove");
        if (WriteGate is { } gate) await gate.Task.ConfigureAwait(false);
        return UndoMoveResult;
    }

    public async Task<bool> ChangeAdjacencyAsync(int unitId, int? add, int? remove, CancellationToken token = default)
    {
        Calls.Add($"Adj:{unitId}+{add?.ToString() ?? "-"}-{remove?.ToString() ?? "-"}");
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

    public bool IsDetailDirty { get; set; }
    public int DetailRefreshes { get; private set; }
    public bool HasDeferredReload { get; set; }
    public event EventHandler? BusyChanged;

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; BusyChanged?.Invoke(this, EventArgs.Empty); }
    }

    public Task RefreshDetailAsync(CancellationToken token = default)
    {
        DetailRefreshes++;
        return Task.CompletedTask;
    }
}

/// <summary>개인 표시 설정(메모리) — 저장 횟수와 <c>Extra</c> 키를 본다. 디스크 0.</summary>
internal sealed class ConsolePrefsProbe
{
    public Ironwall.Dotnet.Libraries.Utils.Consoles.ConsolePrefEntry Entry { get; } = new();
    public int Saves { get; private set; }
    public void Save() => Saves++;
}
#endregion
