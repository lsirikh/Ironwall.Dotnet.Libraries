using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Enclosure;

/****************************************************************************
   Purpose      : NATS ENCLOSURE_METRICS 수신 → 함체별 최신 계측 보관 (이슈 #7-20)
                  + 함체 환경 · 부품 판정 경고(OPERATION_EVENT → 걸림, ACTION_REPORT → 풀림 · 이슈 #7 Q-4a)
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

    public event System.Action<int>? AlertsChanged;

    public EnclosureAlertSummary? GetAlert(int enclosureId)
        => EnclosureAlertSummary.Of(_alerts.Values.Where(a => a.EnclosureId == enclosureId));

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
            ApplyAlerts(e.Data);
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

    /// <summary>
    /// 함체 환경 · 부품 판정 경고(이슈 #7 Q-4a) — 운영 이벤트가 걸고, 그 이벤트의 조치보고(카드 [X] 의 자동 조치 포함)가 푼다.
    /// 운영 카드는 이벤트 큐에 들어가지 않으므로(operation-event-card PRD §4) 심볼 색 축이 아니라 따로 든다.
    /// </summary>
    private void ApplyAlerts(string? json)
    {
        // 계측(10초 × 함체 수)은 cmd 글자로 먼저 거른다 — 두 cmd 가 아니면 파싱하지 않는다.
        if (json is null || (json.IndexOf(EnclosureAlertPayload.OperationCommand, StringComparison.Ordinal) < 0
                          && json.IndexOf(EnclosureAlertPayload.ActionReportCommand, StringComparison.Ordinal) < 0)) return;

        foreach (var envelope in EnclosureAlertPayload.Items(json))
        {
            if (EnclosureAlertPayload.TryParseAlert(envelope) is { } alert)
            {
                if (!_alerts.TryAdd(alert.EventId, alert)) continue;   // 같은 봉투 재배달
                _log?.Info($"[함체 경고] 걸림 — 함체 {alert.EnclosureId} · {alert.Reason} · 운영 이벤트 {alert.EventId}");
                AlertsChanged?.Invoke(alert.EnclosureId);
            }
            else if (EnclosureAlertPayload.TryParseReportedOperationId(envelope) is { } eventId
                     && _alerts.TryRemove(eventId, out var released))
            {
                _log?.Info($"[함체 경고] 풀림 — 함체 {released.EnclosureId} · 운영 이벤트 {eventId} 조치됨");
                AlertsChanged?.Invoke(released.EnclosureId);
            }
        }
    }
    #endregion

    #region - Attributes -
    private readonly ILogService? _log;
    private readonly INatsService _natsService;
    private readonly ConcurrentDictionary<int, EnclosureMetricsReading> _latest = new();
    private readonly ConcurrentDictionary<int, EnclosureAlert> _alerts = new();   // 운영 이벤트 id → 경고
    private bool _started;
    #endregion
}
