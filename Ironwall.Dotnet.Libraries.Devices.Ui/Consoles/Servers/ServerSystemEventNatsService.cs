using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Systems;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
/****************************************************************************
   Purpose      : SYSTEM_EVENT · SYNC_SYSTEM_EVENT 자가 필터 수신(G52, 브로커 §6.5 · §9.15) → 서버 콘솔 시스템 이벤트 목록.
                  · SYSTEM_EVENT       본문 = REST 항목 그대로(7.0+ is_acknowledged · 6.3 acknowledged 둘 다 읽는다) → 한 줄 추가
                  · SYNC_SYSTEM_EVENT  {action, resource_id} → UPDATED 는 콘솔이 다시 읽고, DELETED 는 뺀다
                  봉투 id 로 중복을 거른다. 수신 예외는 여기서 막는다(GIS 본체 보호).
   Created By   : Claude
   Created On   : 2026-10-21
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public sealed class ServerSystemEventNatsService : IService
{
    public ServerSystemEventNatsService(ILogService? log, INatsService nats, IEventAggregator eventAggregator, ITokenStorageService? tokenStorage = null)
    {
        _log = log;
        _nats = nats;
        _eventAggregator = eventAggregator;
        _tokenStorage = tokenStorage;
    }

    public Task ExecuteAsync(CancellationToken token = default) => StartService(token);

    public Task StartService(CancellationToken token = default)
    {
        _nats.NatsSubscribeEventAsync -= OnNatsAsync;   // 멱등
        _nats.NatsSubscribeEventAsync += OnNatsAsync;
        _log?.Info($"{nameof(ServerSystemEventNatsService)} started — SYSTEM_EVENT · SYNC_SYSTEM_EVENT 구독 등록");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token = default)
    {
        _nats.NatsSubscribeEventAsync -= OnNatsAsync;
        return Task.CompletedTask;
    }

    private async Task OnNatsAsync(MessageArgsModel e)
    {
        if (_tokenStorage is { IsAuthenticated: false }) return;   // 로그인 게이팅
        try
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return;
            var token = JToken.Parse(e.Data);
            var envelopes = token is JArray array ? (IEnumerable<JToken>)array : new[] { token };
            foreach (var item in envelopes)
            {
                if (item is not JObject envelope) continue;
                if (ToMessage(envelope) is not { } message) continue;
                if (!Remember(envelope.Value<string>("id"))) continue;   // 같은 봉투 두 번째는 버린다
                await _eventAggregator.PublishOnUIThreadAsync(message).ConfigureAwait(false);
            }
        }
        catch (JsonException) { /* 다른 수신자 몫이거나 깨진 본문 — 조용히 넘긴다 */ }
        catch (Exception ex)
        {
            _log?.Error($"{nameof(ServerSystemEventNatsService)} 처리 오류: {ex.Message}");
        }
    }

    /// <summary>봉투 → 콘솔 메시지. 이 서비스 몫(대문자 cmd · RSP 아님)이 아니면 null.</summary>
    internal static ServerSystemEventMessage? ToMessage(JObject envelope)
    {
        var cmd = envelope.Value<string>("cmd");
        if (cmd is not (CMD_EVENT or CMD_SYNC)) return null;
        if (string.Equals(envelope.Value<string>("m_type"), "RSP", StringComparison.Ordinal)) return null;
        if (envelope["body"] is not JObject body) return null;

        if (cmd == CMD_EVENT)
        {
            var dto = body.ToObject<SystemEventDto>();
            return dto is { Id: > 0 } ? new ServerSystemEventMessage(dto.Id, dto, false) : null;
        }

        var id = body.Value<int?>("resource_id") ?? 0;
        if (id <= 0) return null;
        var action = body.Value<string>("action");
        if (string.Equals(action, "DELETED", StringComparison.OrdinalIgnoreCase)) return new ServerSystemEventMessage(id, null, true);
        if (string.Equals(action, "UPDATED", StringComparison.OrdinalIgnoreCase)) return new ServerSystemEventMessage(id, null, false);
        return null;
    }

    private bool Remember(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return true;
        lock (_seen)
        {
            if (!_seen.Add(id)) return false;
            _order.Enqueue(id);
            while (_order.Count > 512) _seen.Remove(_order.Dequeue());
            return true;
        }
    }

    public const string CMD_EVENT = "SYSTEM_EVENT";
    public const string CMD_SYNC = "SYNC_SYSTEM_EVENT";

    private readonly ILogService? _log;
    private readonly INatsService _nats;
    private readonly IEventAggregator _eventAggregator;
    private readonly ITokenStorageService? _tokenStorage;
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private readonly Queue<string> _order = new();
}
