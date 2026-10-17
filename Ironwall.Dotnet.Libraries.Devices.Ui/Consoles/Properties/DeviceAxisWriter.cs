using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Properties;
/****************************************************************************
   Purpose      : 상세 폼의 축 값 편집을 장비마다 좁은 PATCH 한 건으로 보낸다
   Created By   : GHLee
   Created On   : 9/27/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>축 값 편집을 보낸 결과. <see cref="FailedCount"/> 가 0 이면 전부 저장됐다.</summary>
/// <param name="SentCount">보낸 장비 수.</param>
/// <param name="FailedCount">서버가 받지 않았거나 닿지 못한 장비 수.</param>
/// <param name="Message">운영자에게 보일 한 줄(합니다체). 서버 · 예외 원문은 싣지 않는다 — 원문은 로그에만.</param>
public sealed record DeviceAxisWriteResult(int SentCount, int FailedCount, string Message)
{
    public bool IsSuccess => SentCount > 0 && FailedCount == 0;
}

/// <summary>
/// 상세 폼의 접속 · 형상 · 부대 · 설정 칸을 <see cref="IDeviceApiService.PatchDeviceAxesAsync"/> 로 보낸다(장비마다 한 건).
/// </summary>
/// <remarks>
/// <para><b>판본은 서비스가 본다</b> — 축 계약(7.0+)이 아니면 한 줄도 보내지 않는다(호출자가 켠 스위치를 믿지 않는다 —
/// 조립기 적용과 같은 규칙). 6.3 에서는 이 칸들이 폼에 나오지도 않는다.</para>
/// <para><b>부대 보존</b> — 8.0 은 장비 쓰기에서 <c>unit_id</c> 가 빠지면 장비를 기본 부대로 옮길 수 있다(응답에 신호 없음).
/// 그래서 부대를 고치지 않은 편집에도 그 장비의 지금 부대를 싣는다(<see cref="UnitScopeGate"/> — 값이 있으면 보존,
/// 없을 때만 이 단말의 부대).</para>
/// </remarks>
public sealed class DeviceAxisWriter
{
    public DeviceAxisWriter(IDeviceApiService api, DeviceQueryPolicy policy, ILogService? log = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _log = log;
    }

    /// <summary>고른 장비 전부에 같은 축 값 편집을 보낸다.</summary>
    public async Task<DeviceAxisWriteResult> ApplyAsync(
        IReadOnlyList<IBaseDeviceModel> devices,
        IReadOnlyList<(DevicePropertySpec Spec, string Text)> edits,
        CancellationToken token = default)
    {
        if (devices == null) throw new ArgumentNullException(nameof(devices));
        if (edits == null) throw new ArgumentNullException(nameof(edits));
        if (edits.Count == 0) return new DeviceAxisWriteResult(0, 0, "바꾼 칸이 없습니다.");

        if (!_policy.IsAxisContract)
        {
            _log?.Warning($"[{nameof(DeviceAxisWriter)}] 서버 계약 {_policy.Contract} — 축 값 편집을 보내지 않았습니다.");
            return new DeviceAxisWriteResult(0, devices.Count, "현재 서버에서는 이 항목을 저장할 수 없습니다.");
        }

        var labels = string.Join(", ", edits.Select(e => e.Spec.Label).Distinct());
        var sent = 0;
        var failed = 0;
        foreach (var device in devices)
        {
            token.ThrowIfCancellationRequested();
            if (device is null || device.Id <= 0) { failed++; continue; }

            var path = DeviceTypePaths.FromCategory(DeviceAxesMapper.CategoryOf(device).ToString());
            if (path is null) { failed++; continue; }

            var body = DeviceAxisPatchBuilder.Build(edits);
            // JSON null 도 "없음"으로 본다 — 명시적 "unit_id": null 은 8.0+ 에서 422 다(서버 회신 2026-09-28 Q-1, 무소속 장비는 없다).
            if (_policy.Contract >= Ironwall.Dotnet.Libraries.Api.Services.EnumServerContract.V8_0
                && body["unit_id"] is null or { Type: JTokenType.Null })
            {
                // 부대를 고치지 않은 편집 — 그 장비의 지금 부대를 그대로 실어 서버가 기본 부대로 옮기지 않게 한다.
                body.Remove("unit_id");
                var unit = device.UnitId
                    ?? await UnitScopeGate.ResolveForExistingAsync(null, nameof(DeviceAxisWriter), _log, token).ConfigureAwait(false);
                if (unit is { } unitId) body["unit_id"] = unitId;
            }

            try
            {
                var response = await _api.PatchDeviceAxesAsync(path, device.Id, body, token).ConfigureAwait(false);
                if (response?.Success == true) { sent++; continue; }
                failed++;
                _log?.Warning($"[{nameof(DeviceAxisWriter)}] 장비 {device.Id} 저장 거부 — {response?.Error?.Code} {response?.Error?.Message ?? response?.Message}");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                failed++;
                _log?.Error($"[{nameof(DeviceAxisWriter)}] 장비 {device.Id} 저장 실패 — {ex.Message}");
            }
        }

        var message = failed == 0
            ? $"{labels} 값을 저장했습니다."
            : sent == 0
                ? $"{labels} 값을 저장하지 못했습니다. 값을 확인한 뒤 다시 [적용]하세요."
                : $"{sent}대는 저장했고 {failed}대는 저장하지 못했습니다. 값을 확인한 뒤 다시 [적용]하세요.";
        return new DeviceAxisWriteResult(sent, failed, message);
    }

    private readonly IDeviceApiService _api;
    private readonly DeviceQueryPolicy _policy;
    private readonly ILogService? _log;
}
