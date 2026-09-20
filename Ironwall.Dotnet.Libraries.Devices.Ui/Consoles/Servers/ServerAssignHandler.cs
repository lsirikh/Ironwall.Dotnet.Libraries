using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Monitoring.Models.Devices;
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
/// <param name="Moved">(장비 Id, 이전 서버 Id) — 이전이 없으면 <c>null</c>.</param>
public sealed record ServerAssignUndo(int ServerId, string ServerName, IReadOnlyList<(int DeviceId, int? PreviousServerId)> Moved)
{
    /// <summary>되돌릴 수 있는 것 — 이전 서버가 있던 것만. <c>server_id</c> 해제 입구가 서버에 없다.</summary>
    public IReadOnlyList<(int DeviceId, int PreviousServerId)> Restorable
        => Moved.Where(m => m.PreviousServerId is > 0).Select(m => (m.DeviceId, m.PreviousServerId!.Value)).ToList();

    public int Unrestorable => Moved.Count - Restorable.Count;
}

/// <summary>배정 · 되돌리기 한 번의 결과.</summary>
/// <param name="Line">상태 띠에 남길 한 줄.</param>
/// <param name="Undo">되돌릴 수 있으면 그 정보.</param>
/// <param name="ChangedDeviceIds">소속이 바뀐 장비 — 화면은 이 행들을 다시 그린다.</param>
public sealed record ServerAssignResult(string Line, ServerAssignUndo? Undo, IReadOnlyList<int> ChangedDeviceIds);

/// <summary>배정 전에 사람에게 묻는 창.</summary>
public interface IServerConsoleDialogs
{
    Task<bool> ConfirmAsync(string title, string message);

    /// <summary>지표 이력 창 — 임계 배지를 그리지 않는다.</summary>
    Task ShowMetricHistoryAsync(int serverId, string serverName);
}

/// <summary>
/// 서버 행 드롭존의 처리기 — 장비 한 대당 <b>쓰기 1회</b>, 두 건 이상이면 <b>먼저 묻는다</b>.
/// </summary>
/// <remarks>
/// <para><b>배치 입구가 없다.</b> 서버에 "여러 스피커를 한 서버에" 를 한 번에 보내는 경로가 없어
/// 호출이 장비 수만큼 번진다. 그래서 ① 확인 문구에 <b>호출 횟수를 적고</b> ② <b>첫 실패에서 멈추고</b>
/// ③ 성공한 것만 반영한 뒤 ④ <b>되돌리기</b>를 낸다(드래그 규칙 §서버 쓰기 · 와이어프레임 L415).</para>
/// <para><b>되돌리기의 한계</b> — <c>SpeakerCreate/Update.server_id</c> 는 <b>해제를 지원하지 않는다</b>
/// (<c>null</c> 이면 키째 생략된다). 이전 서버가 있던 장비만 되돌릴 수 있고, 서버가 없던 장비는
/// 되돌릴 수 없다는 사실을 <b>문장으로</b> 말한다 — 조용히 절반만 되돌리지 않는다.</para>
/// <para><see cref="CanDrop"/> 는 끄는 동안 매 프레임 불린다 — 서버를 부르지 않고 순수 판정만 한다.</para>
/// </remarks>
public sealed class ServerAssignHandler : IDragDropHandler
{
    private readonly IServerConsoleService _service;
    private readonly Func<IEnumerable<IBaseDeviceModel>> _allDevices;
    private readonly IServerConsoleDialogs? _dialogs;
    private readonly ILogService? _log;

    public ServerAssignHandler(
        IServerConsoleService service,
        Func<IEnumerable<IBaseDeviceModel>> allDevices,
        IServerConsoleDialogs? dialogs = null,
        ILogService? log = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _allDevices = allDevices ?? throw new ArgumentNullException(nameof(allDevices));
        _dialogs = dialogs;
        _log = log;
    }

    /// <summary>끝났다(성공이든 아니든).</summary>
    public event Action<ServerAssignResult>? Completed;

    public bool IsBusy { get; private set; }

    #region - IDragDropHandler -
    public bool CanDrop(DragPayload payload, DropTarget target)
    {
        if (IsBusy || payload is null || target?.ZoneKey != ServerDropRules.ZoneKey) return false;
        if (target.ZoneData is not IServerAssignTarget server) return false;
        return ServerDropRules.Plan(server.Id, server.Type, ModelsOf(payload.Items)).CanSend;
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
        if (server is null) return Finish("대상 서버가 없습니다", null);

        var plan = ServerDropRules.Plan(server.Id, server.Type, devices);
        if (!plan.CanSend) return Finish(plan.BlockReason ?? "보낼 것이 없습니다", null);
        if (IsBusy) return Finish("앞선 배정이 아직 끝나지 않았습니다", null);

        if (plan.NeedsConfirm && _dialogs is not null)
        {
            var accepted = await _dialogs.ConfirmAsync("서버 배정", plan.ConfirmText(server.Name)).ConfigureAwait(true);
            if (!accepted) return Finish("배정을 취소했습니다 — 서버 호출 0회", null);
        }

        IsBusy = true;
        try
        {
            var byId = _allDevices().Where(d => d is not null).ToDictionary(d => d.Id, d => d);
            var moved = new List<(int DeviceId, int? PreviousServerId)>();
            var failure = string.Empty;

            foreach (var deviceId in plan.DeviceIds)
            {
                token.ThrowIfCancellationRequested();

                byId.TryGetValue(deviceId, out var model);
                var previous = ServerDropRules.ServerIdOf(model);

                var result = await _service.AssignSpeakerAsync(deviceId, server.Id, token).ConfigureAwait(true);
                if (!result.IsSuccess)
                {
                    // 첫 실패에서 멈춘다 — 남은 것을 계속 보내면 무엇이 반영됐는지 아무도 모른다.
                    failure = result.Message;
                    break;
                }

                moved.Add((deviceId, previous));
                Reflect(model, server);
            }

            var failed = plan.DeviceIds.Count - moved.Count;
            var line = ServerDropRules.ResultLine(server.Name, plan, moved.Count, failed);
            if (failed > 0 && failure.Length > 0) line += $" — {failure}";

            var undo = moved.Count > 0 ? new ServerAssignUndo(server.Id, server.Name, moved) : null;
            return Finish(line, undo, moved.Select(m => m.DeviceId).ToList());
        }
        catch (OperationCanceledException) { return Finish("배정을 취소했습니다", null); }
        catch (Exception ex)
        {
            _log?.Error($"[ServerAssign] server={server.Id}: {ex.Message}");
            return Finish($"'{server.Name}' 에 배정하지 못했습니다 — 서버에 닿지 못했습니다", null);
        }
        finally { IsBusy = false; }
    }

    /// <summary>방금 배정한 것을 이전 서버로 돌린다. 이전이 없던 장비는 <b>돌릴 수 없다</b>(해제 입구 부재).</summary>
    public async Task<string> UndoAsync(ServerAssignUndo undo, CancellationToken token = default)
    {
        if (undo is null || undo.Moved.Count == 0) return Finish("되돌릴 것이 없습니다", null);
        if (IsBusy) return Finish("앞선 배정이 아직 끝나지 않았습니다", undo);

        var restorable = undo.Restorable;
        if (restorable.Count == 0)
            return Finish($"되돌릴 수 없습니다 — 배정 전 서버가 없던 {undo.Unrestorable}대는 서버에 해제 입구가 없습니다", null);

        IsBusy = true;
        try
        {
            var byId = _allDevices().Where(d => d is not null).ToDictionary(d => d.Id, d => d);
            var restored = new List<int>();

            foreach (var (deviceId, previousServerId) in restorable)
            {
                token.ThrowIfCancellationRequested();
                var result = await _service.AssignSpeakerAsync(deviceId, previousServerId, token).ConfigureAwait(true);
                if (!result.IsSuccess) break;

                restored.Add(deviceId);
                if (byId.TryGetValue(deviceId, out var model)) Detach(model);
            }

            var parts = new List<string> { $"{restored.Count}대를 이전 서버로 되돌렸습니다" };
            if (undo.Unrestorable > 0) parts.Add($"배정 전 서버가 없던 {undo.Unrestorable}대는 되돌릴 수 없습니다(해제 입구 없음)");
            if (restored.Count < restorable.Count) parts.Add($"{restorable.Count - restored.Count}대에서 멈췄습니다");
            return Finish(string.Join(" · ", parts), null, restored);
        }
        catch (OperationCanceledException) { return Finish("되돌리기를 취소했습니다", undo); }
        catch (Exception ex)
        {
            _log?.Error($"[ServerAssign] undo server={undo.ServerId}: {ex.Message}");
            return Finish("되돌리지 못했습니다 — 서버에 닿지 못했습니다", undo);
        }
        finally { IsBusy = false; }
    }

    /// <summary>끌어 온 행(뷰모델 또는 모델)에서 장비 모델을 꺼낸다.</summary>
    public static IReadOnlyList<IBaseDeviceModel> ModelsOf(IEnumerable<object>? rows)
        => rows?.Select(row => row as IBaseDeviceModel ?? row?.GetType().GetProperty("Model")?.GetValue(row) as IBaseDeviceModel)
                .Where(model => model is not null)
                .Select(model => model!)
                .ToList()
           ?? new List<IBaseDeviceModel>();

    /// <summary>서버가 받아 준 것만 로컬에 반영한다.</summary>
    private static void Reflect(IBaseDeviceModel? model, IServerAssignTarget server)
    {
        if (model is not ISpeakerDeviceModel speaker) return;
        speaker.Server = server.AsModel();
    }

    private static void Detach(IBaseDeviceModel? model)
    {
        if (model is ISpeakerDeviceModel speaker) speaker.Server = null;
    }

    private string Finish(string line, ServerAssignUndo? undo, IReadOnlyList<int>? changed = null)
    {
        Completed?.Invoke(new ServerAssignResult(line, undo, changed ?? Array.Empty<int>()));
        return line;
    }
}
