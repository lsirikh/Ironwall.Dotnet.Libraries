using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
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

    /// <summary>
    /// REST 일괄 초깃값(G24)을 함께 쓰는 생성자 — 장비 API 가 등록돼 있으면 Autofac 이 인자가 더 많은 이쪽을 고른다
    /// (GMapUiModule 의 RegisterType 그대로, 배선 변경 없음).
    /// </summary>
    public EnclosureMetricsNatsSyncService(ILogService? log, INatsService natsService, IDeviceApiService deviceApi)
        : this(log, natsService)
    {
        _deviceApi = deviceApi;
    }
    #endregion

    /// <summary>받은 시각을 정하는 시계 — 시험이 가짜 시계를 끼운다(기본 지금 시각).</summary>
    internal Func<DateTime> Clock { get; set; } = () => DateTime.Now;

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
        // 기동 때 1회 REST 로 빈칸을 채운다(다음 주기 발행까지 최대 10초 '받지 못함' 방지). 기다리지 않는다.
        _ = RefreshFromServerAsync(token);
        return Task.CompletedTask;
    }

    /// <summary>
    /// <c>GET /api/enclosure-metrics</c> 1회로 함체별 최신 계측을 채운다(G24). 기동 때 부르고, 재연결 뒤 다시 부를 수 있다.
    /// <c>latest_metrics: null</c> 은 건너뛰고, 이미 든 값보다 오래된 값은 덮지 않는다. 404(이 경로가 없는 서버)는 조용히 넘긴다.
    /// </summary>
    /// <returns>새로 채운 함체 수.</returns>
    public async Task<int> RefreshFromServerAsync(CancellationToken token = default)
    {
        if (_deviceApi == null) return 0;
        try
        {
            var resp = await _deviceApi.GetAllEnclosureLatestMetricsAsync(token).ConfigureAwait(false);
            if (resp == null || resp.StatusCode == 404) return 0;   // 이 경로가 없는 서버 — NATS 만으로 동작
            if (!resp.Success)
            {
                _log?.Warning($"[함체 계측] 일괄 조회 실패 — 상태 {resp.StatusCode} {resp.Error?.Code}(NATS 만으로 동작)");
                return 0;
            }

            var filled = 0;
            var receivedAt = Clock();
            foreach (var entry in resp.Data ?? new())
            {
                if (FromDto(entry, receivedAt) is not { } reading) continue;   // latest_metrics:null — 계측 없음
                // 기동 직후 NATS 로 이미 더 새 값을 받았으면 덮지 않는다.
                if (_latest.TryGetValue(reading.EnclosureId, out var held) && held.MeasuredAt >= reading.MeasuredAt) continue;
                _latest[reading.EnclosureId] = reading;
                filled++;
                MetricsReceived?.Invoke(reading);
            }
            _log?.Info($"[함체 계측] 일괄 조회로 {filled}대 채움");
            return filled;
        }
        catch (Exception ex)
        {
            _log?.Warning($"[함체 계측] 일괄 조회 오류(NATS 만으로 동작): {ex.Message}");
            return 0;
        }
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

            var reading = EnclosureMetricsPayload.TryParse(e.Data, Clock());
            if (reading == null) return Task.CompletedTask;

            // 새 측정이 없으면 서버가 같은 행(같은 id · observed_at)을 주기마다 다시 보낸다(브로커 §6.6 재발행).
            // 같은 행이면 처음 받은 것을 그대로 두고 알리지도 않는다 — 잰 시각 기준으로 '오래된 값' 이 된다.
            if (reading.MetricId is { } rowId
                && _latest.TryGetValue(reading.EnclosureId, out var held) && held.MetricId == rowId)
                return Task.CompletedTask;

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

    /// <summary>
    /// REST 최신 계측 한 줄 → 계측(브로커 §6.6 과 같은 항목). <c>latest_metrics</c> 가 없으면 null.
    /// 잰 시각은 observed_at → measured_at → created_at(<see cref="EnclosureMetricDto.ObservedAtEffective"/>).
    /// </summary>
    internal static EnclosureMetricsReading? FromDto(EnclosureLatestMetricsDto? entry, DateTime receivedAt)
    {
        if (entry?.LatestMetrics is not { } m) return null;
        var enclosureId = entry.EnclosureId > 0 ? entry.EnclosureId : m.EnclosureId;
        if (enclosureId <= 0) return null;
        return new EnclosureMetricsReading(
            enclosureId,
            m.TemperatureValue,
            m.HumidityValue,
            m.VoltageValue,
            m.CurrentValue,
            m.Vibration,
            m.UpsBatteryLevel,
            m.UpsCharging,
            receivedAt,
            MetricId: m.Id > 0 ? m.Id : null,
            ObservedAt: EnclosureMetricsPayload.ParseTime(m.ObservedAtEffective));
    }
    #endregion

    #region - Attributes -
    private readonly ILogService? _log;
    private readonly INatsService _natsService;
    private readonly IDeviceApiService? _deviceApi;
    private readonly ConcurrentDictionary<int, EnclosureMetricsReading> _latest = new();
    private readonly ConcurrentDictionary<int, EnclosureAlert> _alerts = new();   // 운영 이벤트 id → 경고
    private bool _started;
    #endregion
}
