using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Newtonsoft.Json.Linq;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/****************************************************************************
   Purpose      : SYNC_EVENT_SUPPRESSION 자가필터 수신 서비스.
                  회차 경계(매일 08:00/21:00)마다 서버가 1건 발행하는데
                  status 는 active 로 불변이고 body.suppressing 만 토글되므로
                  status 만 보던 코드는 이 전이를 놓친다.
                  ⚠ suppressing 은 "마지막으로 통지된" 값이라 최대 5분 stale 가능 —
                  권위는 GET /active 폴링이고 이 서비스는 폴링을 앞당기는 가속 신호다.
   Created By   : GHLee
   Created On   : 2026-09-08
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>억제 스케줄 SYNC 수신 서비스(자가필터). 처리는 폴링 가속 하나뿐이다.</summary>
public interface IEventSuppressionSyncNatsService : IService
{
    /// <summary>구독을 건다(멱등).</summary>
    Task StartService(CancellationToken token = default);
}

/// <summary>
/// <see cref="IEventSuppressionSyncNatsService"/> 구현.
/// <para>정본 패턴 = <see cref="DetectionNatsSyncService"/> — 메인 라우터는 명시 no-op 이고
/// 실제 처리는 라이브러리 자가필터 서비스가 한다.</para>
/// </summary>
public class EventSuppressionSyncNatsService : IEventSuppressionSyncNatsService, IService
{
    #region - Ctors -
    public EventSuppressionSyncNatsService(
        ILogService? log,
        INatsService natsService,
        ISuppressionActiveMonitor monitor,
        ITokenStorageService? tokenStorage = null)
    {
        _log = log;
        _natsService = natsService;
        _monitor = monitor;
        _tokenStorage = tokenStorage;
    }
    #endregion

    #region - IService -
    public Task ExecuteAsync(CancellationToken token = default) => StartService(token);

    public Task StartService(CancellationToken token = default)
    {
        // 멱등: 빌드콜백/ExecuteAsync 중복 호출돼도 단일 구독 유지.
        _natsService.NatsSubscribeEventAsync -= OnNatsAsync;
        _natsService.NatsSubscribeEventAsync += OnNatsAsync;
        _log?.Info($"{nameof(EventSuppressionSyncNatsService)} started — SYNC_EVENT_SUPPRESSION 구독 등록");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token = default)
    {
        // ⚠ 이 해제가 없으면 구독이 누수된다 — DI 에서 .As<IService>() 를 빠뜨리면
        //    StopAsync 가 영원히 안 불려 같은 결과가 된다(EventUiModule 의 사문화 선례).
        _natsService.NatsSubscribeEventAsync -= OnNatsAsync;
        _log?.Info($"{nameof(EventSuppressionSyncNatsService)} stopped");
        return Task.CompletedTask;
    }
    #endregion

    #region - Processes -
    /// <summary>
    /// 핸들러는 <b>모든</b> NATS 메시지에 호출된다 — 1Hz 추적 트래픽 위에 얹히므로
    /// 값싼 문자열 선필터를 먼저 통과시킨 뒤에만 JSON 을 파싱한다.
    /// </summary>
    private Task OnNatsAsync(MessageArgsModel e)
    {
        // 로그인 게이팅 — 미인증 상태에서 서버 폴링을 유발하지 않는다.
        if (_tokenStorage is { IsAuthenticated: false }) return Task.CompletedTask;

        var data = e.Data;
        if (string.IsNullOrWhiteSpace(data)) return Task.CompletedTask;

        // 1차 선필터(파싱 없음) — 대다수 메시지를 여기서 버린다.
        if (data.IndexOf(CmdName, StringComparison.Ordinal) < 0) return Task.CompletedTask;

        try
        {
            var jObj = JObject.Parse(data);
            if (!string.Equals(jObj.Value<string>("cmd"), CmdName, StringComparison.Ordinal))
                return Task.CompletedTask;   // 본문에 문자열만 우연히 섞인 경우 차단

            var body = jObj["body"];
            var action = body?.Value<string>("action");
            var resourceId = body?.Value<int?>("resource_id");
            // ⚠ 선택 필드 — 구버전 서버는 안 준다. 값 자체를 상태로 신뢰하지 않는다.
            var suppressing = body?.Value<bool?>("suppressing");

            _log?.Info($"[{nameof(EventSuppressionSyncNatsService)}] {CmdName} " +
                       $"action={action} id={resourceId} suppressing={(suppressing?.ToString() ?? "n/a")}");

            // 유일한 처리 — 권위(GET /active)를 앞당긴다.
            _monitor.RequestImmediatePoll($"{CmdName}/{action ?? "?"}");
        }
        catch (Exception ex)
        {
            // 파싱 실패로 구독이 죽으면 안 된다 — 다음 폴링 주기가 복구한다.
            _log?.Warning($"[{nameof(EventSuppressionSyncNatsService)}] 파싱 실패(무시): {ex.Message}");
        }
        return Task.CompletedTask;
    }
    #endregion

    #region - Attributes -
    private const string CmdName = "SYNC_EVENT_SUPPRESSION";

    private readonly ILogService? _log;
    private readonly INatsService _natsService;
    private readonly ISuppressionActiveMonitor _monitor;
    private readonly ITokenStorageService? _tokenStorage;
    #endregion
}
