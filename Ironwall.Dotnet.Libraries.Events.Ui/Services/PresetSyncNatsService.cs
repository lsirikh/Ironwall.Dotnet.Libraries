using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/****************************************************************************
   Purpose      : SYNC_PRESET 자가필터 수신 서비스(맵핑 결함 G9).
                  서버가 카메라 프리셋을 추가 · 수정 · 삭제(감시금지구역 변경 포함)하면 all.sync.preset 으로 알린다.
                  종전엔 cmd 이름만 알고 받는 곳이 없어, 이벤트 창 매핑 캐시가 옛 프리셋 번호 · 이름 · 금지 여부로 창을 열었다.
                  처리는 CameraPresetsChangedMessage 발행 하나 — 받는 쪽이 캐시를 버리고 다시 읽는다.
   Created By   : Claude
   Created On   : 2026-10-04
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>프리셋 SYNC 수신 서비스(자가필터).</summary>
public interface IPresetSyncNatsService : IService
{
    /// <summary>구독을 건다(멱등).</summary>
    Task StartService(CancellationToken token = default);
}

/// <summary>
/// <see cref="IPresetSyncNatsService"/> 구현 — <see cref="EventSuppressionSyncNatsService"/> 와 같은 모양(값싼 선필터 → 봉투마다 → cmd 판정).
/// 같은 봉투가 전역 · 부대 subject 로 두 번 와도 받는 쪽은 캐시를 비울 뿐이라 해가 없다.
/// </summary>
public class PresetSyncNatsService : IPresetSyncNatsService
{
    #region - Ctors -
    public PresetSyncNatsService(
        ILogService? log,
        INatsService natsService,
        IEventAggregator events,
        ITokenStorageService? tokenStorage = null)
    {
        _log = log;
        _natsService = natsService;
        _events = events;
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
        _log?.Info($"{nameof(PresetSyncNatsService)} started — {CmdName} 구독 등록");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token = default)
    {
        _natsService.NatsSubscribeEventAsync -= OnNatsAsync;
        _log?.Info($"{nameof(PresetSyncNatsService)} stopped");
        return Task.CompletedTask;
    }
    #endregion

    #region - Processes -
    /// <summary>모든 NATS 메시지에 불린다 — 문자열 선필터를 지난 것만 파싱한다.</summary>
    internal Task OnNatsAsync(MessageArgsModel e)
    {
        // 로그인 게이팅 — 미인증 상태에서 재조회를 유발하지 않는다(로그인 뒤 첫 조회가 어차피 새로 읽는다).
        if (_tokenStorage is { IsAuthenticated: false }) return Task.CompletedTask;

        var data = e.Data;
        if (string.IsNullOrWhiteSpace(data)) return Task.CompletedTask;
        if (data.IndexOf(CmdName, StringComparison.Ordinal) < 0) return Task.CompletedTask;

        foreach (var envelope in NatsEnvelopeItems.Parse(data, _log, CmdName))
            ProcessEnvelope(envelope);
        return Task.CompletedTask;
    }

    private void ProcessEnvelope(JObject jObj)
    {
        try
        {
            if (!NatsEnvelopeItems.IsNotice(jObj, CmdName)) return;

            var body = jObj["body"] as JObject;
            var action = body?.Value<string>("action") ?? string.Empty;
            var resourceId = body?.Value<int?>("resource_id") ?? 0;
            var cameraId = body?.Value<int?>("camera_id") ?? 0;

            _log?.Info($"[{nameof(PresetSyncNatsService)}] {CmdName} action={action} preset={resourceId} camera={cameraId}");
            _ = PublishAsync(new CameraPresetsChangedMessage(action, resourceId, cameraId));
        }
        catch (Exception ex)
        {
            // 항목 하나의 해석 실패로 구독이 죽으면 안 된다 — 다음 SYNC_EVENT_MAPPING · 재시작이 복구한다.
            _log?.Warning($"[{nameof(PresetSyncNatsService)}] 파싱 실패(무시): {ex.Message}");
        }
    }

    private async Task PublishAsync(CameraPresetsChangedMessage message)
    {
        try { await _events.PublishOnCurrentThreadAsync(message).ConfigureAwait(false); }
        catch (Exception ex) { _log?.Warning($"[{nameof(PresetSyncNatsService)}] 알림 발행 실패: {ex.Message}"); }
    }
    #endregion

    #region - Attributes -
    private const string CmdName = "SYNC_PRESET";

    private readonly ILogService? _log;
    private readonly INatsService _natsService;
    private readonly IEventAggregator _events;
    private readonly ITokenStorageService? _tokenStorage;
    #endregion
}
