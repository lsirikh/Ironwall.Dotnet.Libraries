using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Broadcast;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/****************************************************************************
   Purpose      : NATS MIC_PAGING(브로커 v2.0.12 §11.2 · subject {부대}.gis.mic-paging) → 스피커 '방송 중' 표시(G38)
   Created By   : Claude
   Created On   : 2026-10-21
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 마이크 페이징 보고 <c>{state: START|STOP, dial, speaker_ids[], mic_device_id, started_at}</c> 를 받아
/// <see cref="BroadcastStatusNatsSyncService"/> 와 <b>같은 표시 경로</b>(<see cref="IBroadcastStatusNatsSyncService.BroadcastStateChanged"/>)로 낸다.
/// <para>지도는 <see cref="IBroadcastStatusNatsSyncService"/> 하나만 구독하므로, 이 서비스가 BROADCAST_STATUS 서비스를 감싸
/// 같은 인터페이스로 등록된다(구독 시작 · 해제도 그대로 위임). 지도 쪽 코드는 바뀌지 않는다.</para>
/// <para>STOP 에 <c>speaker_ids</c> 가 없으면 같은 <c>mic_device_id</c> 의 START 때 켠 스피커를 끈다. 수신 예외는 여기서 막는다.</para>
/// </summary>
public sealed class MicPagingNatsSyncService : IBroadcastStatusNatsSyncService
{
    public MicPagingNatsSyncService(ILogService? log, INatsService natsService, IBroadcastStatusNatsSyncService broadcastStatus)
    {
        _log = log;
        _natsService = natsService;
        _inner = broadcastStatus;
        _inner.BroadcastStateChanged += Raise;
    }

    public event Action<int, bool>? BroadcastStateChanged;

    public async Task StartService(CancellationToken token = default)
    {
        await _inner.StartService(token).ConfigureAwait(false);
        if (_started) return;
        _started = true;
        _natsService.NatsSubscribeEventAsync += OnNatsAsync;
        _log?.Info($"{nameof(MicPagingNatsSyncService)} started — MIC_PAGING 구독 등록");
    }

    public async Task StopAsync(CancellationToken token = default)
    {
        if (_started)
        {
            _started = false;
            _natsService.NatsSubscribeEventAsync -= OnNatsAsync;
        }
        await _inner.StopAsync(token).ConfigureAwait(false);
    }

    private Task OnNatsAsync(MessageArgsModel e)
    {
        try
        {
            if (Read(e.Data) is not { } paging) return Task.CompletedTask;
            foreach (var speakerId in SpeakersFor(paging))
            {
                _log?.Info($"MIC_PAGING 수신: speaker={speakerId}, 방송={(paging.IsOn ? "ON" : "OFF")}, mic={paging.MicDeviceId}, dial={paging.Dial}");
                Raise(speakerId, paging.IsOn);
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"OnNatsAsync(MIC_PAGING) 오류: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    /// <summary>START 면 켠 스피커를 마이크별로 기억하고, STOP 이면 본문 스피커(없으면 기억한 것)를 끈다.</summary>
    private IReadOnlyList<int> SpeakersFor(MicPaging paging)
    {
        lock (_gate)
        {
            if (paging.IsOn)
            {
                if (paging.MicDeviceId is { } mic) _active[mic] = paging.SpeakerIds;
                return paging.SpeakerIds;
            }
            if (paging.MicDeviceId is { } stopMic && _active.Remove(stopMic, out var remembered) && paging.SpeakerIds.Count == 0)
                return remembered;
            return paging.SpeakerIds;
        }
    }

    private void Raise(int speakerId, bool isOn) => BroadcastStateChanged?.Invoke(speakerId, isOn);

    /// <summary>봉투 → 페이징 보고. MIC_PAGING 이 아니거나(대문자 토큰 · 응답 제외) state 를 모르면 null.</summary>
    internal static MicPaging? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        JObject root;
        try { root = JObject.Parse(json); }
        catch { return null; }
        if (!string.Equals(root.Value<string>("cmd"), "MIC_PAGING", StringComparison.Ordinal)) return null;
        if (string.Equals(root.Value<string>("m_type"), "RSP", StringComparison.Ordinal)) return null;
        if (root["body"] is not JObject body) return null;

        bool? isOn = body.Value<string>("state")?.Trim().ToUpperInvariant() switch
        {
            "START" or "STARTED" or "ON" => true,
            "STOP" or "STOPPED" or "OFF" => false,
            _ => null,
        };
        if (isOn is null) return null;

        var speakers = BroadcastStatusPayload.ParseSpeakerIds(body);
        var mic = body["mic_device_id"] is { Type: JTokenType.Integer } m ? m.Value<int>() : (int?)null;
        return new MicPaging(isOn.Value, speakers, mic, body.Value<string>("dial"), body.Value<string>("started_at"));
    }

    internal sealed record MicPaging(bool IsOn, IReadOnlyList<int> SpeakerIds, int? MicDeviceId, string? Dial, string? StartedAt);

    private readonly ILogService? _log;
    private readonly INatsService _natsService;
    private readonly IBroadcastStatusNatsSyncService _inner;
    private readonly Dictionary<int, IReadOnlyList<int>> _active = new();
    private readonly object _gate = new();
    private bool _started;
}
