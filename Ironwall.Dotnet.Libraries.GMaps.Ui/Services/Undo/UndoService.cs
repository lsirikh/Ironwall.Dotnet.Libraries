using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo;

/****************************************************************************
   Purpose      : IUndoService 구현 — 맵별 undo/redo 2스택, 깊이 50, 직렬화 실행, 배치/억제.
   Note         : Map_Edit_Undo_Redo FR-01/07/12. 스레드 안전(lock + SemaphoreSlim). 예외는 로그 후 흡수.
   Created On   : 2026-07-03 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class UndoService : IUndoService
{
    private const int MAX_DEPTH = 50;

    private sealed class MapHistory
    {
        public readonly LinkedList<IUndoableCommand> Undo = new();   // Last=top
        public readonly LinkedList<IUndoableCommand> Redo = new();
    }

    private readonly object _gate = new();
    private readonly Dictionary<int, MapHistory> _byMap = new();
    private readonly SemaphoreSlim _runLock = new(1, 1);   // Undo/Redo 직렬화(NFR-06)
    private readonly ILogService? _log;

    private int _suspendDepth;
    private BatchScope? _activeBatch;

    public UndoService(ILogService? log = null) => _log = log;

    public int CurrentMapId { get; set; }

    public bool IsRecordingSuspended => Volatile.Read(ref _suspendDepth) > 0;

    public event EventHandler? StateChanged;

    private MapHistory Current()
    {
        if (!_byMap.TryGetValue(CurrentMapId, out var h)) { h = new MapHistory(); _byMap[CurrentMapId] = h; }
        return h;
    }

    public bool CanUndo { get { lock (_gate) return Current().Undo.Count > 0; } }
    public bool CanRedo { get { lock (_gate) return Current().Redo.Count > 0; } }
    public string? NextUndoDescription { get { lock (_gate) return Current().Undo.Last?.Value.Description; } }
    public string? NextRedoDescription { get { lock (_gate) return Current().Redo.Last?.Value.Description; } }

    public void Push(IUndoableCommand command)
    {
        if (command == null) return;
        if (IsRecordingSuspended) return;

        // 배치 중이면 매크로에 수집(스택엔 Dispose 시 1건으로)
        var batch = _activeBatch;
        if (batch != null) { batch.Add(command); return; }

        lock (_gate)
        {
            command.ScopeMapId = CurrentMapId;
            var h = Current();
            h.Undo.AddLast(command);
            h.Redo.Clear();
            while (h.Undo.Count > MAX_DEPTH) h.Undo.RemoveFirst();
        }
        RaiseChanged();
    }

    public async Task<bool> UndoAsync(CancellationToken ct = default)
    {
        IUndoableCommand? cmd;
        lock (_gate) { cmd = Current().Undo.Last?.Value; }
        if (cmd == null) return false;

        // NOTE: no ConfigureAwait(false) on the replay path — undo/redo replay is UI-affine
        // (커맨드가 WPF 마커/맵을 동기 변형). UI 스레드(AsyncRelayCommand 람다)에서 진입하므로
        // 캡처된 Dispatcher 컨텍스트로 되돌아와 마커 변형이 UI 스레드에서 실행됨. 세마포어 경합 없이도
        // MacroCommand 자식 2번째부터 스레드풀로 새던 그룹이동 무음실패(THREAD-01/02) 차단.
        await _runLock.WaitAsync(ct);
        using var _ = SuspendRecording();
        try
        {
            await cmd.UndoAsync(ct);
            lock (_gate)
            {
                var h = Current();
                if (h.Undo.Last?.Value == cmd) { h.Undo.RemoveLast(); h.Redo.AddLast(cmd); }
            }
            RaiseChanged();
            return true;
        }
        catch (Exception ex) { _log?.Error($"Undo 실패 '{cmd.Description}': {ex.Message}"); return false; }
        finally { _runLock.Release(); }
    }

    public async Task<bool> RedoAsync(CancellationToken ct = default)
    {
        IUndoableCommand? cmd;
        lock (_gate) { cmd = Current().Redo.Last?.Value; }
        if (cmd == null) return false;

        await _runLock.WaitAsync(ct);   // UI-affine replay — see UndoAsync note (THREAD-01/02)
        using var _ = SuspendRecording();
        try
        {
            await cmd.ExecuteAsync(ct);
            lock (_gate)
            {
                var h = Current();
                if (h.Redo.Last?.Value == cmd) { h.Redo.RemoveLast(); h.Undo.AddLast(cmd); }
            }
            RaiseChanged();
            return true;
        }
        catch (Exception ex) { _log?.Error($"Redo 실패 '{cmd.Description}': {ex.Message}"); return false; }
        finally { _runLock.Release(); }
    }

    public void Clear(int? mapId = null)
    {
        lock (_gate)
        {
            if (mapId is < 0) _byMap.Clear();
            else if (_byMap.TryGetValue(mapId ?? CurrentMapId, out var h)) { h.Undo.Clear(); h.Redo.Clear(); }
        }
        RaiseChanged();
    }

    public IDisposable BeginBatch(string description)
    {
        // 단일 레벨 배치(중첩은 기존 배치에 합류)
        if (_activeBatch != null) return new NoOpScope();
        var scope = new BatchScope(this, description);
        _activeBatch = scope;
        return scope;
    }

    public IDisposable SuspendRecording()
    {
        Interlocked.Increment(ref _suspendDepth);
        return new SuspendScope(this);
    }

    private void RaiseChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    // ── 내부 스코프 ──
    private sealed class SuspendScope : IDisposable
    {
        private readonly UndoService _s; private bool _done;
        public SuspendScope(UndoService s) => _s = s;
        public void Dispose() { if (_done) return; _done = true; Interlocked.Decrement(ref _s._suspendDepth); }
    }

    private sealed class NoOpScope : IDisposable { public void Dispose() { } }

    private sealed class BatchScope : IDisposable
    {
        private readonly UndoService _s;
        private readonly string _desc;
        private readonly List<IUndoableCommand> _children = new();
        private bool _done;
        public BatchScope(UndoService s, string desc) { _s = s; _desc = desc; }
        public void Add(IUndoableCommand c) => _children.Add(c);
        public void Dispose()
        {
            if (_done) return; _done = true;
            _s._activeBatch = null;
            if (_children.Count == 0) return;
            var macro = _children.Count == 1 ? _children[0] : new MacroCommand(_desc, _children, _s._log);
            _s.Push(macro);
        }
    }
}
