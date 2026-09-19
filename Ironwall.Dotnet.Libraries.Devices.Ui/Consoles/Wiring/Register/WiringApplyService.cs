using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;

/// <summary>한 줄의 저장 결과 — 실패한 줄만 남기고 사유 한 줄(WS L450).</summary>
public sealed record WiringRowResult(int Key, int Number, string Display, bool IsCreate, bool Ok, string Message, int? NewId = null);

/// <summary>저장 한 번의 결과.</summary>
public sealed record WiringApplyResult(
    bool IsSuccess,
    bool IsConflict,
    bool IsBlocked,
    string Message,
    IReadOnlyList<WiringRowResult> Rows)
{
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

    public WiringApplyService(ISensorWriteGateway gateway,
                              IDeviceProviderService? providerService = null,
                              ILogService? log = null,
                              DeviceQueryPolicy? policy = null)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _providerService = providerService;
        _log = log;
        _policy = policy ?? DeviceQueryPolicy.Resolve();
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
                    return WiringApplyResult.Stop($"{row.Display} 을(를) 다시 받지 못해 아무것도 보내지 않았습니다 — {Text(response, "다시 받기 실패")}");

                var server = response.Data;
                var serverPlacement = WiringSpec.Read(server.HardwareSpec?.Spec);
                if (!WiringSpec.SamePlacement(serverPlacement, row.BaselinePlacement))
                    return WiringApplyResult.Stop(
                        $"다른 사람이 {row.Display} 의 자리를 바꿨습니다({Describe(row.BaselinePlacement)} → {Describe(serverPlacement)}) — 아무것도 보내지 않았습니다. 창을 닫고 다시 열어 확인하십시오.",
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

            var ok = results.Count(r => r.Ok);
            var failed = results.Count - ok;

            if (ok > 0 && _providerService is not null)
            {
                try { await _providerService.FetchAllDevicesAsync(token).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { _log?.Warning($"[{nameof(WiringApplyService)}] 저장 뒤 재조회 실패: {ex.Message}"); }
            }

            var message = failed == 0
                ? $"저장했습니다 — 센서 {ok}대(대당 1회) · 실패 0"
                : $"센서 {ok}대를 저장하고 {failed}대는 실패했습니다 — 실패한 줄만 남겨 두었습니다.";

            return new WiringApplyResult(failed == 0, false, false, message, results);
        }
        catch (OperationCanceledException)
        {
            return WiringApplyResult.Stop("저장이 취소되었습니다.");
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(WiringApplyService)}] {ex.Message}");
            return WiringApplyResult.Stop(ex.Message);
        }
    }

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
            dto.HardwareSpec = new HardwareSpecDto { UseAxisWrite = true, Spec = WiringSpec.Apply(null, placement) };

        await UnitScopeGate.StampAsync(dto, nameof(WiringApplyService), _log, token).ConfigureAwait(false);
        var response = await _gateway.CreateAsync(dto, token).ConfigureAwait(false);

        return response.Success
            ? new WiringRowResult(row.Key, row.Facts.Number, row.Display, true, true, "만들었습니다", response.Data?.Id)
            : new WiringRowResult(row.Key, row.Facts.Number, row.Display, true, false, Text(response, "만들지 못했습니다"));
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
                Spec = WiringSpec.Apply(server.HardwareSpec?.Spec, placement),
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
