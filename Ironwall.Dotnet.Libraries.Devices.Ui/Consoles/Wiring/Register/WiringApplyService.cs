using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Dialogs;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;

/// <summary>한 줄의 저장 결과 — 실패한 줄만 남기고 사유 한 줄(WS L450).</summary>
public sealed record WiringRowResult(int Key, int Number, string Display, bool IsCreate, bool Ok, string Message, int? NewId = null);

/// <summary>그룹 호출 한 건의 결과 — 그룹 하나 · 방향 하나(W2).</summary>
/// <param name="DeviceIds">서버가 이 호출로 그 그룹 소속을 맞춰 준 장비 id — 성공한 호출에서만 채운다(보드가 이 줄들만 그룹 기준선을 옮긴다).</param>
public sealed record WiringGroupResult(int GroupId, bool Add, int DeviceCount, bool Ok, string Message, IReadOnlyList<int>? DeviceIds = null);

/// <summary>저장 한 번의 결과.</summary>
public sealed record WiringApplyResult(
    bool IsSuccess,
    bool IsConflict,
    bool IsBlocked,
    string Message,
    IReadOnlyList<WiringRowResult> Rows,
    IReadOnlyList<WiringGroupResult>? GroupCalls = null)
{
    /// <summary>그룹 호출 결과(없으면 빈 목록).</summary>
    public IReadOnlyList<WiringGroupResult> Groups { get; } = GroupCalls ?? Array.Empty<WiringGroupResult>();

    public int SentCount => Rows.Count;
    public int OkCount => Rows.Count(r => r.Ok);
    public int FailedCount => Rows.Count(r => !r.Ok);

    /// <summary>성공한 줄의 키 — 보드가 이 줄만 새 기준으로 삼는다.</summary>
    public IReadOnlyList<int> OkKeys => Rows.Where(r => r.Ok).Select(r => r.Key).ToList();

    internal static WiringApplyResult Stop(string message, bool conflict = false, bool blocked = false)
        => new(false, conflict, blocked, message, Array.Empty<WiringRowResult>());
}

/// <summary>저장 진행률(WS L450 "저장 진행률").</summary>
public sealed record WiringProgress(int Done, int Total, string Current);

/// <summary>
/// 제어기 한 대의 센서 변경을 서버에 보낸다 — <b>만든 줄은 POST 1회 · 고친 줄은 PATCH 1회</b>(WS L475, L528).
/// </summary>
/// <remarks>
/// <para>순서: ① 계약 가드(6.3 이면 한 번도 부르지 않는다) ② <b>쓸 줄을 전부 다시 받아</b> 그 사이 바뀌지 않았는지
/// 확인(WS L477 — 바뀌었으면 <b>한 건도 보내지 않는다</b>) ③ 보내기 ④ 부분 실패 보고.</para>
/// <para><b>PATCH 본문은 받은 값으로 되채운다</b> — 축 모드에서 조건 없이 나가는 키(<c>number_device</c> ·
/// <c>name_device</c> · <c>status</c> · <c>is_enable</c>)를 비워 두면 PATCH 가 그것을 지운다(RFC 7396).
/// <c>hardware_spec</c> 은 <c>spec</c> 한 칸만 싣고 <c>components</c> 는 <b>절대 싣지 않는다</b> —
/// 서버가 부품 배열을 통째로 바꾸고 빠진 부품은 경고 없이 지운다.</para>
/// </remarks>
public sealed class WiringApplyService
{
    private readonly ISensorWriteGateway _gateway;
    private readonly IDeviceProviderService? _providerService;
    private readonly ILogService? _log;
    private readonly DeviceQueryPolicy _policy;
    private readonly IEventAggregator? _eventAggregator;

    /// <param name="eventAggregator">그룹 호출이 소속을 실제로 바꾸면 <see cref="DeviceGroupMembershipChangedMessage"/> 를 알릴 곳.
    /// 없으면(단위 테스트 · 라이브 하네스) 알리지 않는다 — 지도의 구역선 조회표는 이 알림으로 새 소속을 안다.</param>
    public WiringApplyService(ISensorWriteGateway gateway,
                              IDeviceProviderService? providerService = null,
                              ILogService? log = null,
                              DeviceQueryPolicy? policy = null,
                              IEventAggregator? eventAggregator = null)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _providerService = providerService;
        _log = log;
        _policy = policy ?? DeviceQueryPolicy.Resolve();
        _eventAggregator = eventAggregator;
    }

    /// <summary>
    /// 바뀐 줄을 보낸다. <paramref name="controllerId"/> 는 새로 만드는 센서의 소속이다(제어기 단위 저장 — WS L528).
    /// </summary>
    public async Task<WiringApplyResult> ApplyAsync(int controllerId,
                                                    WiringBoard board,
                                                    IProgress<WiringProgress>? progress = null,
                                                    CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(board);

        if (WiringWriteGuard.IsBlocked(_policy))
        {
            _log?.Warning($"[{nameof(WiringApplyService)}] 서버 계약 {_policy.Contract} — 결선을 보내지 않았습니다.");
            return WiringApplyResult.Stop(WiringWriteGuard.LEGACY_CONTRACT_MESSAGE, blocked: true);
        }

        if (controllerId <= 0)
            return WiringApplyResult.Stop("제어기를 먼저 고르세요 — 어느 제어기의 센서인지 알 수 없습니다.");

        var diff = board.Diff();
        if (diff.IsEmpty) return WiringApplyResult.Stop("바뀐 줄이 없습니다.");

        try
        {
            token.ThrowIfCancellationRequested();

            // ② 쓸 줄을 전부 다시 받는다 — 하나라도 그 사이 바뀌었으면 한 건도 보내지 않는다.
            var fetched = new Dictionary<int, SensorDeviceDto>();
            foreach (var row in diff.ToSend.Where(r => !r.IsNew))
            {
                token.ThrowIfCancellationRequested();
                var response = await _gateway.GetAsync(row.Id, token).ConfigureAwait(false);
                if (!response.Success || response.Data is null)
                {
                    _log?.Warning($"[{nameof(WiringApplyService)}] {row.Display} 재조회 실패: {Text(response, "재조회 실패")}");
                    return WiringApplyResult.Stop($"{row.Display} 정보를 다시 불러오지 못해 저장하지 않았습니다. 잠시 후 다시 시도하세요.");
                }

                var server = response.Data;
                if (DriftOf(row, server) is { } drift)
                    return WiringApplyResult.Stop(
                        $"다른 사용자가 {row.Display}의 {drift} — 저장하지 않았습니다. 창을 닫고 다시 열어 확인하세요.",
                        conflict: true);

                fetched[row.Key] = server;
            }

            // ③ 보내기 — 한 줄에 호출 한 번(표 값과 결선이 같이 바뀌었어도 한 번이다).
            var results = new List<WiringRowResult>();
            var total = diff.ToSend.Count;
            var done = 0;

            foreach (var row in diff.ToSend)
            {
                token.ThrowIfCancellationRequested();
                progress?.Report(new WiringProgress(done, total, row.Display));

                results.Add(row.IsNew
                    ? await CreateAsync(controllerId, row, board, token).ConfigureAwait(false)
                    : await PatchAsync(row, board, fetched[row.Key], token).ConfigureAwait(false));

                done++;
                progress?.Report(new WiringProgress(done, total, row.Display));
            }

            // ③-b 그룹 변화분 — 그룹 하나 · 방향 하나에 호출 한 번(센서 수와 무관). 막 만든 줄은 새 id 로 실린다.
            var newIds = results.Where(r => r.Ok && r.NewId is > 0).ToDictionary(r => r.Key, r => r.NewId!.Value);
            var groupResults = await ApplyGroupsAsync(board, newIds, token).ConfigureAwait(false);

            var ok = results.Count(r => r.Ok);
            var failed = results.Count - ok + groupResults.Count(g => !g.Ok);

            // 그룹만 바꾼 저장도 캐시(다른 창의 소속 표시)를 맞춘다.
            if ((ok > 0 || groupResults.Any(g => g.Ok)) && _providerService is not null)
            {
                try { await _providerService.FetchAllDevicesAsync(token).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { _log?.Warning($"[{nameof(WiringApplyService)}] 저장 뒤 재조회 실패: {ex.Message}"); }
            }

            // 서버가 소속을 맞춰 준 그룹만 알린다 — 재조회 뒤라 캐시가 새 소속을 담고 있다(재조회가 없거나 실패해도 알린다).
            await AnnounceMembershipAsync(groupResults).ConfigureAwait(false);

            var groupNote = groupResults.Count == 0 ? string.Empty : $" · 그룹 변경 {groupResults.Count}건";
            var message = failed == 0
                ? $"저장했습니다 — 센서 {ok}대{groupNote}."
                : $"센서 {ok}대를 저장했고 {failed}건은 저장하지 못했습니다{groupNote}. 저장하지 못한 것만 남겨 두었습니다.";

            return new WiringApplyResult(failed == 0, false, false, message, results, groupResults);
        }
        catch (OperationCanceledException)
        {
            return WiringApplyResult.Stop("저장이 취소되었습니다.");
        }
        catch (Exception ex)
        {
            // 예외 원문은 화면에 붙이지 않는다 — 로그로 보낸다(U-18 공통 규칙).
            _log?.Error($"[{nameof(WiringApplyService)}] {ex.Message}");
            return WiringApplyResult.Stop("저장하지 못했습니다. 서버 연결을 확인하고 다시 시도하세요.");
        }
    }

    #region - Groups (W2) -
    /// <summary>
    /// 소속이 바뀌었다고 알린다 — 지도가 그 그룹들의 구역선만 다시 등록(비면 해제)한다.
    /// 알림 실패는 저장 결과를 바꾸지 않는다. 이 서비스는 작업 스레드에서 끝날 수 있다 — 받는 쪽(지도)이 UI 로 넘긴다.
    /// </summary>
    private async Task AnnounceMembershipAsync(IReadOnlyList<WiringGroupResult> groupResults)
    {
        if (_eventAggregator is null) return;
        var changed = groupResults.Where(g => g.Ok && g.DeviceIds is { Count: > 0 }).Select(g => g.GroupId);
        if (DeviceGroupMembershipChangedMessage.For(changed) is not { } message) return;
        try { await _eventAggregator.PublishOnCurrentThreadAsync(message).ConfigureAwait(false); }
        catch (Exception ex) { _log?.Error($"[{nameof(WiringApplyService)}] 소속 변경 알림 실패 groups={string.Join(",", message.GroupIds)}: {ex.Message}"); }
    }

    /// <summary>
    /// 그룹 변화분을 보낸다 — <b>그룹 하나 · 방향 하나에 배치 호출 한 번</b>. 성공한 그룹만 기준을 옮긴다.
    /// </summary>
    private async Task<IReadOnlyList<WiringGroupResult>> ApplyGroupsAsync(WiringBoard board, IReadOnlyDictionary<int, int> newIds, CancellationToken token)
    {
        var calls = SensorGroupEdit.Plan(board.Rows, row => row.Id > 0 ? row.Id : newIds.TryGetValue(row.Key, out var id) ? id : 0);
        if (calls.Count == 0) return Array.Empty<WiringGroupResult>();

        var results = new List<WiringGroupResult>(calls.Count);
        foreach (var call in calls)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (call.Add)
                {
                    var response = await _gateway.AssignToGroupAsync(call.GroupId, call.DeviceIds, token).ConfigureAwait(false);
                    results.Add(new WiringGroupResult(call.GroupId, true, call.DeviceIds.Count, response.Success,
                        response.Success ? "그룹에 넣었습니다" : Text(response, "그룹에 넣지 못했습니다"),
                        response.Success ? call.DeviceIds : null));
                }
                else
                {
                    // 빼기는 서버가 한 번에 100대까지만 받는다(device_ids max_length=100) — 넘기면 422 로 한 대도 안 빠진다.
                    //   배정 창과 같은 크기로 나눠 보내고, 된 묶음만 성공으로 센다.
                    foreach (var chunk in AssignDelta.ChunkRemovals(call.DeviceIds))
                    {
                        var response = await _gateway.RemoveFromGroupAsync(call.GroupId, chunk, token).ConfigureAwait(false);
                        results.Add(new WiringGroupResult(call.GroupId, false, chunk.Count, response.Success,
                            response.Success ? "그룹에서 뺐습니다" : Text(response, "그룹에서 빼지 못했습니다"),
                            response.Success ? chunk : null));
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _log?.Error($"[{nameof(WiringApplyService)}] 그룹 {call.GroupId} 호출 실패: {ex.Message}");
                results.Add(new WiringGroupResult(call.GroupId, call.Add, call.DeviceIds.Count, false, ex.Message));
            }
        }
        return results;
    }
    #endregion

    #region - Bodies -
    /// <summary>새 센서 — 결선까지 한 본문에 실어 POST 1회.</summary>
    private async Task<WiringRowResult> CreateAsync(int controllerId, WiringSensorRow row, WiringBoard board, CancellationToken token)
    {
        var dto = new SensorDeviceDto
        {
            NumberDevice = row.Facts.Number,
            NameDevice = row.Facts.Name,
            TypeDevice = row.Facts.TypeText ?? string.Empty,
            ControllerId = controllerId,
            IsEnable = true,
            UseAxisWrite = true,
        };

        if (!string.IsNullOrWhiteSpace(row.Facts.Zone))
            dto.Geolocation = new GeolocationDto { Location = row.Facts.Zone };

        var placement = board.PlacementOf(row.Key);
        if (placement is not null)
            dto.HardwareSpec = new HardwareSpecDto { UseAxisWrite = true, Spec = WiringSpec.Apply(null, placement, board.Shape) };

        await UnitScopeGate.StampAsync(dto, nameof(WiringApplyService), _log, token).ConfigureAwait(false);
        var response = await _gateway.CreateAsync(dto, token).ConfigureAwait(false);

        if (!response.Success)
            return new WiringRowResult(row.Key, row.Facts.Number, row.Display, true, false, Text(response, "만들지 못했습니다"));

        var newId = response.Data?.Id ?? 0;
        if (newId <= 0)
        {
            // 만들기는 됐는데 응답에 id 가 없다 — 번호로 되찾는다. 못 찾으면 <b>성공으로 세지 않는다</b>(C7):
            // 그대로 두면 다음 저장이 같은 번호를 한 번 더 만든다.
            newId = await FindByNumberAsync(controllerId, row.Facts.Number, token).ConfigureAwait(false);
            if (newId <= 0)
            {
                _log?.Warning($"[{nameof(WiringApplyService)}] 번호 {row.Facts.Number} 센서를 만들었지만 id 를 확인하지 못했습니다.");
                return new WiringRowResult(row.Key, row.Facts.Number, row.Display, true, false,
                    "저장됨 · 확인 필요 — 저장 결과를 확인하지 못했습니다. [갱신] 뒤 이 줄이 있는지 확인하세요(다시 저장하면 같은 번호가 두 번 만들어질 수 있습니다).");
            }
        }

        return new WiringRowResult(row.Key, row.Facts.Number, row.Display, true, true, "만들었습니다", newId);
    }

    /// <summary>그 제어기의 센서를 다시 받아 번호로 찾는다. 못 찾으면 <c>0</c>.</summary>
    private async Task<int> FindByNumberAsync(int controllerId, int number, CancellationToken token)
    {
        try
        {
            var list = await _gateway.ListByControllerAsync(controllerId, token).ConfigureAwait(false);
            if (list?.Success != true || list.Data is null) return 0;
            var hit = list.Data.Where(d => d is not null && d.NumberDevice == number && d.Id > 0).ToList();
            return hit.Count == 1 ? hit[0].Id : 0;      // 번호가 둘이면 어느 쪽이 우리 것인지 알 수 없다
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Warning($"[{nameof(WiringApplyService)}] id 확인 조회 실패: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// 그 사이 서버 쪽이 바뀌었는가 — <b>이 창이 쓰는 칸 전부</b>를 본다(C10). 안 바뀌었으면 <c>null</c>.
    /// </summary>
    /// <remarks><c>status</c>·<c>updated_at</c> 는 보지 않는다 — 우리가 쓰지 않고 장비가 스스로 바꾼다.</remarks>
    private static string? DriftOf(WiringSensorRow row, SensorDeviceDto server)
    {
        // 기준은 "받아들인 자리"(BaselinePlacement)가 아니라 <b>서버에서 받은 원값</b>(ServerPlacement)이다(F-2b H1) —
        // 불러올 때 겹쳐 팔레트로 뺀 센서는 받아들인 기준이 null 이라, 그것과 비교하면 사람이 다시 놓은 뒤에도 늘 "남이 바꿨다" 였다.
        var serverPlacement = WiringSpec.Read(server.HardwareSpec?.Spec);
        if (!WiringSpec.SamePlacement(serverPlacement, row.ServerPlacement))
            return $"자리를 바꿨습니다({Describe(row.ServerPlacement)} → {Describe(serverPlacement)})";

        if (server.NumberDevice != row.Baseline.Number)
            return $"번호를 바꿨습니다({row.Baseline.Number} → {server.NumberDevice})";

        if (!string.Equals(server.NameDevice ?? string.Empty, row.Baseline.Name, StringComparison.Ordinal))
            return $"이름을 바꿨습니다(\"{row.Baseline.Name}\" → \"{server.NameDevice}\")";

        if (!string.Equals(server.TypeDevice ?? string.Empty, row.Baseline.TypeText, StringComparison.Ordinal))
            return $"종류를 바꿨습니다({row.Baseline.TypeText} → {server.TypeDevice})";

        var serverZone = server.Geolocation?.Location ?? string.Empty;
        if (!string.Equals(serverZone, row.Baseline.Zone, StringComparison.Ordinal))
            return $"구역을 바꿨습니다(\"{row.Baseline.Zone}\" → \"{serverZone}\")";

        return null;
    }

    /// <summary>기존 센서 — 받은 값으로 되채운 뒤 바뀐 칸만 얹어 PATCH 1회.</summary>
    private async Task<WiringRowResult> PatchAsync(WiringSensorRow row, WiringBoard board, SensorDeviceDto server, CancellationToken token)
    {
        var note = string.Empty;

        var dto = new SensorDeviceDto
        {
            // 축 모드에서 조건 없이 나가는 키 — 비우면 PATCH 가 지운다.
            NumberDevice = row.Facts.Number,
            NameDevice = row.Facts.Name,
            Status = server.Status,
            IsEnable = server.IsEnable,
            UseAxisWrite = true,
            // (D-13) 방금 다시 받은 장비의 소속 부대를 보존한다 — UnitScopeGate 가 null 일 때만 이 클라이언트 부대로 채운다.
            UnitId = server.UnitId,
            // 종류는 바뀌었을 때만 싣는다(빈 값이면 type_sensor 가 나가지 않는다).
            TypeDevice = string.Equals(row.Facts.TypeText, row.Baseline.TypeText, StringComparison.Ordinal)
                ? string.Empty
                : row.Facts.TypeText ?? string.Empty,
        };

        // 구역(위치 설명)은 위치 축 안에 있다 — 나머지 좌표를 받은 값 그대로 옮겨 싣는다.
        if (!string.Equals(row.Facts.Zone, row.Baseline.Zone, StringComparison.Ordinal))
        {
            if (server.Geolocation is { } geo)
            {
                dto.Geolocation = new GeolocationDto
                {
                    Location = row.Facts.Zone,
                    Latitude = geo.Latitude,
                    Longitude = geo.Longitude,
                    Altitude = geo.Altitude,
                    Heading = geo.Heading,
                };
            }
            else
            {
                // 좌표가 없는 장비에 구역만 보내면 위도·경도가 0 으로 박힌다 — 그 한 칸만 건너뛰고 나머지는 저장한다.
                note = " (구역은 위치 좌표가 없어 저장하지 않았습니다 — 지도에서 위치를 먼저 잡아 주세요)";
            }
        }

        var placement = board.PlacementOf(row.Key);
        if (!WiringSpec.SamePlacement(placement, row.BaselinePlacement))
        {
            dto.HardwareSpec = new HardwareSpecDto
            {
                UseAxisWrite = true,
                // components 는 싣지 않는다 — 서버가 배열을 통째로 바꾼다(AllowComponentsWrite 기본 false).
                // spec 은 우리 키 하나만 — 병합이라 나머지는 그대로 남고, 자리를 비울 때는 명시적 null 이 지운다.
                // 형식 표지(v 2 · 모양)를 함께 싣는다 — 다음에 열 때 옛 두 선으로 오해하지 않게(F-2b H3).
                Spec = WiringSpec.MergePatch(placement, board.Shape),
            };
        }

        await UnitScopeGate.StampAsync(dto, nameof(WiringApplyService), _log, token).ConfigureAwait(false);
        var response = await _gateway.PatchAsync(row.Id, dto, token).ConfigureAwait(false);

        return response.Success
            ? new WiringRowResult(row.Key, row.Facts.Number, row.Display, false, true, "저장했습니다" + note)
            : new WiringRowResult(row.Key, row.Facts.Number, row.Display, false, false, Text(response, "저장하지 못했습니다"));
    }
    #endregion

    private static string Describe(WiringPlacement? placement) => placement?.Text ?? "미배치";

    private static string Text<T>(ApiResponse<T> response, string fallback)
        => ApiErrorTextHelper.FromFieldErrorsMultiline(response.Error)
           ?? ApiErrorTextHelper.Resolve(response.Error, response.Message, fallback);
}
