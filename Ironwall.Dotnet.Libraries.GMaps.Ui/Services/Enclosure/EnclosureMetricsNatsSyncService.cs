using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Enclosure;

/****************************************************************************
   Purpose      : NATS ENCLOSURE_METRICS 수신 → 함체별 최신 계측 보관 (이슈 #7-20)
   Created By   : Claude Code
   Created On   : 2026-10-15
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <c>sensorway.{부대ID}.gis.enclosure-metrics</c> 를 구독해 함체별 최신 계측을 보관하고 알린다(브로커 연동설계 §6.6).
/// <para>메인 솔루션 라우터는 이 cmd 를 조용히 넘기므로 라이브러리가 직접 구독한다(BROADCAST_STATUS 와 같은 방식).</para>
/// </summary>
public class EnclosureMetricsNatsSyncService : IEnclosureMetricsNatsSyncService
{
    #region - Ctors -
    public EnclosureMetricsNatsSyncService(ILogService? log, INatsService natsService)
    {
        _log = log;
        _natsService = natsService;
    }
    #endregion

    public event System.Action<EnclosureMetricsReading>? MetricsReceived;

    public bool TryGetLatest(int enclosureId, out EnclosureMetricsReading reading)
        => _latest.TryGetValue(enclosureId, out reading!);

    #region - IService -
    public Task StartService(CancellationToken token = default)
    {
        if (_started) return Task.CompletedTask;
        _started = true;
        _natsService.NatsSubscribeEventAsync += OnNatsAsync;
        _log?.Info($"{nameof(EnclosureMetricsNatsSyncService)} started — ENCLOSURE_METRICS 구독 등록");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token = default)
    {
        if (!_started) return Task.CompletedTask;
        _started = false;
        _natsService.NatsSubscribeEventAsync -= OnNatsAsync;
        _log?.Info($"{nameof(EnclosureMetricsNatsSyncService)} stopped");
        return Task.CompletedTask;
    }
    #endregion

    #region - Processes -
    private Task OnNatsAsync(MessageArgsModel e)
    {
        try
        {
            if (e.Subject?.Contains(EnclosureMetricsPayload.SubjectSuffix) != true) return Task.CompletedTask;

            var reading = EnclosureMetricsPayload.TryParse(e.Data, DateTime.Now);
            if (reading == null) return Task.CompletedTask;

            // 10초마다 함체마다 오므로 로그는 남기지 않는다(로그 폭증 방지).
            _latest[reading.EnclosureId] = reading;
            MetricsReceived?.Invoke(reading);
        }
        catch (Exception ex)
        {
            _log?.Error($"OnNatsAsync(ENCLOSURE_METRICS) 오류: {ex.Message}");
        }
        return Task.CompletedTask;
    }
    #endregion

    #region - Attributes -
    private readonly ILogService? _log;
    private readonly INatsService _natsService;
    private readonly ConcurrentDictionary<int, EnclosureMetricsReading> _latest = new();
    private bool _started;
    #endregion
}
