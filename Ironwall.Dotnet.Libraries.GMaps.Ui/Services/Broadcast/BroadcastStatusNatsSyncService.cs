using System;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Broadcast;

/****************************************************************************
   Purpose      : NATS BROADCAST_STATUS 수신 → 스피커 심볼 송출 표시 (FR-24)
   Created By   : Claude Code
   Created On   : 2026-09-08
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <c>sensorway.{부대ID}.gis.broadcast-status</c> 를 구독해 스피커의 송출 상태를 전달한다.
///
/// <para><b>왜 필요한가</b>: 지금까지 GIS 는 자기가 보낸 방송만 로컬 타이머로 표시했다. 다른 자리에서 시작한 방송이나
/// 방송서버 마이크 송출은 화면에 전혀 나타나지 않는다. 이 구독이 그 구멍을 메운다.</para>
///
/// <para><b>실발행 미확인</b>(VER-04): 로컬에 BroadcastingManager 컨테이너가 없어 관측하지 못했다.
/// 발행이 없으면 이 서비스는 아무 일도 하지 않으므로(무해) 규격대로 먼저 구현하고, 표시는 로컬 폴백과 병행한다.</para>
///
/// <para>페이로드는 규격 <c>{speaker_id, status: ON|OFF}</c> 를 기본으로 하되,
/// 여러 대를 한 번에 보고하는 <c>speaker_ids[]</c> 형태도 받아들인다 — 한쪽만 지원하면 조용히 표시가 죽는다.</para>
/// </summary>
public class BroadcastStatusNatsSyncService : IBroadcastStatusNatsSyncService
{
    #region - Ctors -
    public BroadcastStatusNatsSyncService(ILogService? log, INatsService natsService)
    {
        _log = log;
        _natsService = natsService;
    }
    #endregion

    public event System.Action<int, bool>? BroadcastStateChanged;

    #region - IService -
    public Task StartService(CancellationToken token = default)
    {
        if (_started) return Task.CompletedTask;
        _started = true;
        _natsService.NatsSubscribeEventAsync += OnNatsAsync;
        _log?.Info($"{nameof(BroadcastStatusNatsSyncService)} started — BROADCAST_STATUS 구독 등록");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token = default)
    {
        if (!_started) return Task.CompletedTask;
        _started = false;
        _natsService.NatsSubscribeEventAsync -= OnNatsAsync;
        _log?.Info($"{nameof(BroadcastStatusNatsSyncService)} stopped");
        return Task.CompletedTask;
    }
    #endregion

    #region - Processes -
    private Task OnNatsAsync(MessageArgsModel e)
    {
        try
        {
            if (e.Subject?.Contains("gis.broadcast-status") != true) return Task.CompletedTask;

            var body = BroadcastStatusPayload.TryReadBody(e.Data);
            if (body == null) return Task.CompletedTask;

            bool? isOn = BroadcastStatusPayload.ParseStatus(body["status"]);
            if (isOn is null)
            {
                _log?.Warning($"BROADCAST_STATUS status 해석 불가: '{body["status"]}'");
                return Task.CompletedTask;
            }

            foreach (var speakerId in BroadcastStatusPayload.ParseSpeakerIds(body))
            {
                _log?.Info($"BROADCAST_STATUS 수신: speaker={speakerId}, 송출={(isOn.Value ? "ON" : "OFF")}");
                BroadcastStateChanged?.Invoke(speakerId, isOn.Value);
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"OnNatsAsync(BROADCAST_STATUS) 오류: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    #endregion

    #region - Attributes -
    private readonly ILogService? _log;
    private readonly INatsService _natsService;
    private bool _started;
    #endregion
}
