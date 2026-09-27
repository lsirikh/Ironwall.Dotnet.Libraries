using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Servers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
/****************************************************************************
   Purpose      : 장비 → 서버 배정 드롭 처리 (N-12)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>되돌리기에 필요한 것 — 어느 장비가 <b>어디에</b> 붙어 있었는가.</summary>
/// <param name="ServerId">방금 붙인 서버.</param>
/// <param name="ServerName">그 서버 이름(문구용).</param>
/// <param name="Moved">(장비, 이전 서버 Id) — 이전이 없으면 <c>null</c>. <b>서버에서 확인한 이전 값</b>만 담는다.</param>
/// <param name="UnknownPrevious">배정 전 서버를 확인하지 못해 되돌리기에서 뺀 대수 — 모르는 값을 "없음" 으로 되돌리면 실제 배정을 지운다.</param>
public sealed record ServerAssignUndo(int ServerId, string ServerName, IReadOnlyList<(IBaseDeviceModel Device, int? PreviousServerId)> Moved,
    int UnknownPrevious = 0)
{
    public int Count => Moved.Count;
}

/// <summary>배정 · 되돌리기 한 번의 결과.</summary>
public sealed record ServerAssignResult(string Line, ServerAssignUndo? Undo, IReadOnlyList<int> ChangedDeviceIds);

/// <summary>배정 전에 사람에게 묻는 창.</summary>
public interface IServerConsoleDialogs
{
    Task<bool> ConfirmAsync(string title, string message);

    /// <summary>지표 이력 창 — 임계 배지를 그리지 않는다.</summary>
    Task ShowMetricHistoryAsync(int serverId, string serverName);
}

/// <summary>
/// 서버 행 드롭존의 처리기 — <b>1회 호출은 즉시, N회는 Draft 트레이</b>(콘솔 공통 규칙 · 드래그 와이어프레임 L432).
/// </summary>
/// <remarks>
/// <para><b>폭발반경</b> — 서버에 "여러 장비를 한 서버에" 를 한 번에 보내는 경로가 없어 호출이 장비 수만큼 번진다.
/// 그래서 한 대는 그대로 보내고(되돌리기 제공), 두 대 이상은 <see cref="DraftTrayViewModel"/> 에 쌓아
/// [적용] 한 번에 진행률·부분 실패를 보인다 — 장비 콘솔(N-02)·계정(N-06)과 같은 몸짓이다.</para>
/// <para><b>해제는 축 계약에서만</b> 된다 — <c>server_id: null</c> 이 관계 해제다
/// (서버 <c>app/schemas/device.py:568·683·740</c>). 6.3 에는 그 입구가 없어 되돌리기가 반쪽이 되고,
/// 화면이 그 사실을 문장으로 말한다.</para>
/// <para><see cref="CanDrop"/> 는 끄는 동안 매 프레임 불린다 — 서버를 부르지 않고 순수 판정만 한다.</para>
/// </remarks>
public sealed class ServerAssignHandler : IDragDropHandler
{
    private readonly IServerConsoleService _service;
    private readonly Func<IEnumerable<IBaseDeviceModel>> _allDevices;
    private readonly DraftTrayViewModel _tray;
    private readonly IServerConsoleDialogs? _dialogs;
    private readonly ILogService? _log;

    public ServerAssignHandler(
        IServerConsoleService service,
        Func<IEnumerable<IBaseDeviceModel>> allDevices,
        DraftTrayViewModel tray,
        IServerConsoleDialogs? dialogs = null,
        ILogService? log = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _allDevices = allDevices ?? throw new ArgumentNullException(nameof(allDevices));
        _tray = tray ?? throw new ArgumentNullException(nameof(tray));
        _dialogs = dialogs;
        _log = log;
    }

    /// <summary>끝났다(성공이든 아니든).</summary>
    public event Action<ServerAssignResult>? Completed;

    /// <summary>끄는 동안 어떤 행이 왜 막혔는지 알린다 — 행마다 다른 사유를 툴팁에 띄운다.</summary>
    public event Action<IReadOnlyList<IBaseDeviceModel>>? DragProbeRequested;

    public bool IsBusy { get; private set; }

    #region - IDragDropHandler -
    public bool CanDrop(DragPayload payload, DropTarget target)
    {
        if (IsBusy || payload is null || target?.ZoneKey != ServerDropRules.ZoneKey) return false;
        if (target.ZoneData is not IServerAssignTarget server) return false;

        var devices = ModelsOf(payload.Items);
        DragProbeRequested?.Invoke(devices);      // 행마다 사유를 채운다(고정 문구를 쓰지 않는다)
        return ServerDropRules.Plan(server.Id, server.Type, devices, _service.Contract, CurrentServerIdOf).CanSend;
    }

    public async void Drop(DragPayload payload, DropTarget target)
    {
        if (target?.ZoneData is not IServerAssignTarget server) return;
        try { await AssignAsync(server, ModelsOf(payload.Items)).ConfigureAwait(true); }
        catch (Exception ex) { _log?.Error($"[ServerAssign] {ex.Message}"); }
    }
    #endregion

    /// <summary>
    /// 배정. 키보드 · 버튼 폴백("고른 장비를 이 서버에 배정")도 <b>같은 이 경로</b>를 부른다.
    /// </summary>
    public async Task<string> AssignAsync(IServerAssignTarget server, IReadOnlyList<IBaseDeviceModel> devices, CancellationToken token = default)
    {
        if (server is null) return Finish("배정할 서버가 없습니다.", null);

        var plan = ServerDropRules.Plan(server.Id, server.Type, devices, _service.Contract, CurrentServerIdOf);
        if (!plan.CanSend) return Finish(plan.BlockReason ?? "배정할 장비가 없습니다.", null);
        if (IsBusy) return Finish("앞선 배정이 아직 끝나지 않았습니다. 잠시 후 다시 시도하세요.", null);

        // N 회로 번지는 배정은 즉시 보내지 않는다 — Draft 트레이에 쌓고 [적용] 한 번에 보낸다.
        if (plan.IsMultiCall) return Finish(Queue(server, plan), null);

        IsBusy = true;
        try
        {
            var device = plan.Devices[0];
            // 되돌리기의 기준은 서버가 말하는 지금 값이다 — 모델에는 스피커 말고는 서버 축이 없어,
            // 예전에는 카메라 · 제어기의 이전 서버가 늘 "없음" 이었고 되돌리기가 실제 배정을 지웠다.
            var previous = await PreviousAsync(device, token).ConfigureAwait(true);
            var result = await SendAsync(device, server.Id, token).ConfigureAwait(true);

            if (!result.IsSuccess)
            {
                // 서버 원문은 화면에 붙이지 않는다 — 로그로 보낸다(U-18 공통 규칙).
                _log?.Warning($"[ServerAssign] server={server.Id} device={device.Id} 거절: {result.Message}");
                return Finish($"'{server.Name}'에 배정하지 못했습니다. 잠시 후 다시 시도하세요.", null);
            }

            Reflect(device, server);
            var undo = previous.Known ? new ServerAssignUndo(server.Id, server.Name, new[] { (device, previous.ServerId) }) : null;
            var line = ServerDropRules.ResultLine(server.Name, plan, 1, 0);
            if (!previous.Known) line += " " + UnknownPreviousNote;
            return Finish(line, undo, new[] { device.Id });
        }
        catch (OperationCanceledException) { return Finish("배정을 취소했습니다.", null); }
        catch (Exception ex)
        {
            _log?.Error($"[ServerAssign] server={server.Id}: {ex.Message}");
            return Finish($"'{server.Name}'에 배정하지 못했습니다. 서버 연결을 확인하세요.", null);
        }
        finally { IsBusy = false; }
    }

    /// <summary>여러 대 — 트레이에 쌓는다. 서버 호출은 <b>0</b> 이다.</summary>
    private string Queue(IServerAssignTarget server, ServerAssignPlan plan)
    {
        var moved = new List<(IBaseDeviceModel Device, int? PreviousServerId)>();
        var unknown = new List<int>();

        foreach (var device in plan.Devices)
        {
            var name = string.IsNullOrWhiteSpace(device.DeviceName) ? $"장비 {device.Id}" : device.DeviceName!;

            _tray.Add(new DraftEntry(
                targetKey: $"server:{server.Id}:{device.Id}",
                callKind: "PATCH server_id",
                description: $"{name} → '{server.Name}'",
                apply: async ct =>
                {
                    // 보내기 직전에 읽는다 — 담아 둔 사이에 다른 창이 옮겼을 수 있다.
                    var previous = await PreviousAsync(device, ct).ConfigureAwait(true);
                    var result = await SendAsync(device, server.Id, ct).ConfigureAwait(true);
                    if (!result.IsSuccess) return DraftOutcome.Failed;

                    Reflect(device, server);
                    if (previous.Known) moved.Add((device, previous.ServerId));
                    else unknown.Add(device.Id);
                    return DraftOutcome.Applied;
                }));
        }

        _lastQueued = (server.Id, server.Name, moved, unknown);
        return $"'{server.Name}'에 배정할 {plan.WriteCount}대를 대기 목록에 담았습니다 — [적용]을 누르면 저장됩니다.";
    }

    /// <summary>트레이를 적용한 뒤 그 결과를 알린다(되돌리기는 실제로 나간 것만 대상이다).</summary>
    public async Task<string> ApplyTrayAsync(CancellationToken token = default)
    {
        if (!_tray.HasEntries) return Finish("대기 중인 배정이 없습니다.", null);

        var summary = await _tray.ApplyAsync(token).ConfigureAwait(true);
        var queued = _lastQueued;
        _lastQueued = null;

        var undo = queued is { } q && q.Moved.Count > 0
            ? new ServerAssignUndo(q.ServerId, q.ServerName, q.Moved.ToList(), q.Unknown.Count)
            : null;
        var line = summary.ToMessage();
        if (queued is { } u && u.Unknown.Count > 0) line += " " + UnknownPreviousNote;
        var changed = (queued?.Moved.Select(m => m.Device.Id) ?? Enumerable.Empty<int>())
            .Concat(queued?.Unknown ?? Enumerable.Empty<int>())
            .ToList();
        return Finish(line, undo, changed);
    }

    /// <summary>트레이를 버린다 — 서버 호출 0.</summary>
    public string RevertTray()
    {
        var count = _tray.Count;
        _tray.Revert();
        _lastQueued = null;
        return Finish($"대기 목록을 비웠습니다({count}건). 저장하지 않았습니다.", null);
    }

    /// <summary>방금 배정한 것을 되돌린다. 축 계약에서는 이전이 없던 장비를 <b>해제</b>로 되돌린다.</summary>
    public async Task<string> UndoAsync(ServerAssignUndo undo, CancellationToken token = default)
    {
        if (undo is null || undo.Moved.Count == 0) return Finish("되돌릴 배정이 없습니다.", null);
        if (IsBusy) return Finish("앞선 배정이 아직 끝나지 않았습니다. 잠시 후 다시 시도하세요.", undo);

        var canDetach = _service.IsAxisEra;
        var restorable = undo.Moved.Where(m => m.PreviousServerId is > 0 || canDetach).ToList();
        var stuck = undo.Moved.Count - restorable.Count;

        if (restorable.Count == 0)
            return Finish($"되돌릴 수 없습니다. 현재 서버에서는 배정 해제를 지원하지 않아 "
                        + $"배정 전에 서버가 없던 {stuck}대를 되돌릴 수 없습니다.", null);

        IsBusy = true;
        try
        {
            var restored = new List<int>();
            foreach (var (device, previousServerId) in restorable)
            {
                token.ThrowIfCancellationRequested();
                var result = await SendAsync(device, previousServerId, token).ConfigureAwait(true);
                if (!result.IsSuccess) break;

                restored.Add(device.Id);
                Detach(device, previousServerId);
            }

            var parts = new List<string> { $"{restored.Count}대를 되돌렸습니다" };
            if (stuck > 0) parts.Add($"배정 전에 서버가 없던 {stuck}대는 현재 서버에서 해제할 수 없습니다");
            if (restored.Count < restorable.Count) parts.Add($"{restorable.Count - restored.Count}대는 되돌리지 못했습니다");
            if (undo.UnknownPrevious > 0) parts.Add($"배정 전 서버를 확인하지 못한 {undo.UnknownPrevious}대는 그대로 두었습니다");
            return Finish(string.Join(" · ", parts) + ".", null, restored);
        }
        catch (OperationCanceledException) { return Finish("되돌리기를 취소했습니다.", undo); }
        catch (Exception ex)
        {
            _log?.Error($"[ServerAssign] undo server={undo.ServerId}: {ex.Message}");
            return Finish("되돌리지 못했습니다. 서버 연결을 확인하세요.", undo);
        }
        finally { IsBusy = false; }
    }

    /// <summary>배정 전 서버를 읽지 못했을 때 결과 줄에 덧붙이는 말.</summary>
    public const string UnknownPreviousNote = "배정 전 서버를 확인하지 못해 이 배정은 되돌릴 수 없습니다.";

    /// <summary>
    /// 장비의 지금 서버 — 서버에서 읽어 둔 값이 먼저이고, 없으면 모델(스피커만 서버 축이 있다).
    /// 끄는 동안 매 프레임 불려도 되게 서버를 부르지 않는다.
    /// </summary>
    public int? CurrentServerIdOf(IBaseDeviceModel device)
    {
        if (device is null) return null;
        lock (_knownGate)
        {
            if (_known.TryGetValue(device.Id, out var serverId)) return serverId;
        }
        return ServerDropRules.ServerIdOf(device);
    }

    /// <summary>배정 후보 전부의 지금 서버를 서버에서 다시 읽는다 — 칩에 실제 서버 이름을 보이려고. 실패하면 모델 값을 쓴다.</summary>
    public async Task RefreshServerMapAsync(CancellationToken token = default)
    {
        IReadOnlyDictionary<int, int?>? map;
        try { map = await _service.GetDeviceServerMapAsync(token).ConfigureAwait(true); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Warning($"[ServerAssign] 장비의 관리 서버를 읽지 못했습니다 — 모델 값을 씁니다: {ex.Message}");
            return;
        }

        lock (_knownGate)
        {
            _known.Clear();
            if (map is null) return;
            foreach (var pair in map) _known[pair.Key] = pair.Value;
        }
    }

    /// <summary>
    /// 배정 직전 그 장비의 서버 — ① 서버에서 방금 읽은 값 ② 모델에 서버 축이 있는 스피커의 값 ③ 목록을 적재할 때 서버에서 읽어 둔 값.
    /// 셋 다 없으면 <b>모른다</b>(되돌리기에서 뺀다).
    /// </summary>
    private async Task<DeviceServerLookup> PreviousAsync(IBaseDeviceModel device, CancellationToken token)
    {
        try
        {
            var read = await _service.GetDeviceServerAsync(DeviceAxesMapper.CategoryOf(device), device.Id, token).ConfigureAwait(true);
            if (read.Known)
            {
                Remember(device.Id, read.ServerId);
                return read;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _log?.Warning($"[ServerAssign] 장비 {device.Id} 의 지금 서버를 읽지 못했습니다: {ex.Message}"); }

        if (device is ISpeakerDeviceModel) return new DeviceServerLookup(true, ServerDropRules.ServerIdOf(device));
        lock (_knownGate)
        {
            if (_known.TryGetValue(device.Id, out var cached)) return new DeviceServerLookup(true, cached);
        }
        return DeviceServerLookup.Unknown;
    }

    private void Remember(int deviceId, int? serverId)
    {
        lock (_knownGate) _known[deviceId] = serverId;
    }

    /// <summary>끌어 온 행(뷰모델 또는 모델)에서 장비 모델을 꺼낸다.</summary>
    public static IReadOnlyList<IBaseDeviceModel> ModelsOf(IEnumerable<object>? rows)
        => rows?.Select(row => row as IBaseDeviceModel ?? row?.GetType().GetProperty("Model")?.GetValue(row) as IBaseDeviceModel)
                .Where(model => model is not null)
                .Select(model => model!)
                .ToList()
           ?? new List<IBaseDeviceModel>();

    private Task<ServerWriteResult> SendAsync(IBaseDeviceModel device, int? serverId, CancellationToken token)
        => _service.AssignDeviceAsync(DeviceAxesMapper.CategoryOf(device), device.Id, serverId, token);

    /// <summary>서버가 받아 준 것만 로컬에 반영한다(스피커만 모델에 서버 축이 있다 — 나머지는 읽어 둔 사전에 적는다).</summary>
    private void Reflect(IBaseDeviceModel? model, IServerAssignTarget server)
    {
        if (model is null) return;
        Remember(model.Id, server.Id);
        if (model is ISpeakerDeviceModel speaker) speaker.Server = server.AsModel();
    }

    private void Detach(IBaseDeviceModel? model, int? previousServerId)
    {
        if (model is null) return;
        Remember(model.Id, previousServerId is > 0 ? previousServerId : null);
        if (model is not ISpeakerDeviceModel speaker) return;
        speaker.Server = previousServerId is > 0 ? new ServerModel { Id = previousServerId.Value } : null;
    }

    private string Finish(string line, ServerAssignUndo? undo, IReadOnlyList<int>? changed = null)
    {
        Completed?.Invoke(new ServerAssignResult(line, undo, changed ?? Array.Empty<int>()));
        return line;
    }

    private (int ServerId, string ServerName, List<(IBaseDeviceModel Device, int? PreviousServerId)> Moved, List<int> Unknown)? _lastQueued;

    /// <summary>서버에서 읽은 "장비 → 지금 서버". 끄는 동안(UI) · 적재(await 뒤) 양쪽에서 닿아 잠근다.</summary>
    private readonly Dictionary<int, int?> _known = new();
    private readonly object _knownGate = new();
}
