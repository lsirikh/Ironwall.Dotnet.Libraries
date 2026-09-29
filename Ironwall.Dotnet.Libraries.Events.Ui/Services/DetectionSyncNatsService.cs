using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/****************************************************************************
   Purpose      : NATS SYNC_DETECTION(all.sync.detection) 구독 → PTZ 회전 후 썸네일 갱신.
                  서버(DBApi)가 탐지 UPDATE 시 발행. GIS는 현재 EQM에 등록(활성)된 탐지에 한해
                  GET /events/detections/{id}로 재조회 → detail.thumbnail/frame_* 를 실시간 탐지 카드에 반영.
                  · 최초 탐지(DETECT/INSERT)는 DetectionNatsSyncService가 처리 — 본 서비스와 독립.
                  · action=DELETED는 로그만(범위 결정: UPDATED 썸네일 갱신 전용).
   Created By   : GHLee
   Created On   : 2026-07-31
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class DetectionSyncNatsService : IDetectionSyncNatsService, IService
{
    #region - Ctors -
    public DetectionSyncNatsService(
        ILogService? log,
        INatsService natsService,
        IEventQueueManager eventQueueManager,
        IEventApiService apiService,
        IEventAggregator? eventAggregator = null,
        ITokenStorageService? tokenStorage = null)
    {
        _log = log;
        _natsService = natsService;
        _eventQueueManager = eventQueueManager;
        _apiService = apiService;
        _eventAggregator = eventAggregator;
        _tokenStorage = tokenStorage;
    }
    #endregion

    #region - IService -
    // IService.ExecuteAsync — OnStartup→Start() 가 OrderBy(Metadata["Order"]) 정렬 후 호출. StartService 위임.
    // (EB1) IService 등록으로 OnExit 가 동일 정렬 후 StopAsync 를 호출 → NATS 구독 해제. Order 메타데이터는
    //       Start()/OnExit() 의 OrderBy 때문에 필수(누락 시 KeyNotFoundException).
    public Task ExecuteAsync(CancellationToken token = default) => StartService(token);

    public Task StartService(CancellationToken token = default)
    {
        // 멱등: 빌드콜백/ExecuteAsync 중복 호출돼도 단일 구독 유지 (이중 구독 방지)
        _natsService.NatsSubscribeEventAsync -= OnNatsDetectionSyncAsync;
        _natsService.NatsSubscribeEventAsync += OnNatsDetectionSyncAsync;
        _log?.Info($"{nameof(DetectionSyncNatsService)} started — SYNC_DETECTION 구독 등록");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token = default)
    {
        _natsService.NatsSubscribeEventAsync -= OnNatsDetectionSyncAsync;
        _log?.Info($"{nameof(DetectionSyncNatsService)} stopped");
        return Task.CompletedTask;
    }
    #endregion

    #region - Processes -
    private Task OnNatsDetectionSyncAsync(MessageArgsModel e)
    {
        // 로그인 게이팅(Login_Gated_GIS_Init): 로그인 전 NATS 이벤트 수신 차단.
        // _tokenStorage 미주입(null) 시 게이트 비활성(하위호환).
        if (_tokenStorage is { IsAuthenticated: false }) return Task.CompletedTask;
        // 배열 봉투는 항목마다 — 호스트 라우터 · 다른 이벤트 수신 서비스와 같은 의미(WP-1 ⑰). 종전엔 JObject.Parse 라
        //   배열이 오면 통째로 ERROR 한 줄과 함께 그 안의 SYNC_DETECTION 이 사라졌다(프로브 S13).
        foreach (var envelope in NatsEnvelopeItems.Parse(e.Data, _log, "SYNC_DETECTION"))
            ProcessEnvelope(envelope);
        return Task.CompletedTask;
    }

    private void ProcessEnvelope(JObject jObj)
    {
        try
        {
            // cmd 대문자 토큰 · 응답(m_type=RSP) 제외 — 호스트 라우터와 같은 판정
            if (!NatsEnvelopeItems.IsNotice(jObj, "SYNC_DETECTION")) return;

            if (jObj["body"] is not JObject body) return;

            var action = body.Value<string>("action");
            var resourceId = body.Value<int?>("resource_id") ?? 0;

            // UPDATED만 처리 — DELETED는 로그만(범위 결정: 썸네일 갱신 전용).
            if (!string.Equals(action, "UPDATED", StringComparison.OrdinalIgnoreCase))
            {
                _log?.Info($"SYNC_DETECTION 무시(action={action}, resource_id={resourceId}) — UPDATED만 처리");
                return;
            }

            if (resourceId <= 0)
            {
                _log?.Warning("SYNC_DETECTION: resource_id 없음/유효하지 않음 — 무시");
                return;
            }

            // 게이트: 현재 EQM에 등록(활성)된 **탐지**만 재조회 — 비활성이면 REST 호출 없이 no-op.
            // EventType=Intrusion 판별 필수(탐지/장애 독립 id 시퀀스 → 숫자 EventId 충돌 방어).
            // ("우리 현재 탐지로 EventQueueManager에 등록되어있는 경우"에만 갱신)
            if (_eventQueueManager.FindEntryByEventId(resourceId, EnumEventType.Intrusion) == null)
            {
                _log?.Info($"SYNC_DETECTION: resource_id={resourceId} EQM 미등록(비활성 탐지) — 갱신 스킵");
                return;
            }

            // 순서 역전 방지: 같은 resource_id에 UPDATED가 연속 오면 GET 완료 순서가 뒤바뀌어 stale가 최신을 덮을 수 있음.
            // resource_id별 요청 세대를 단조 증가시키고, 처리 완료 시 최신 세대만 반영(아래 HandleUpdatedAsync).
            var seq = _latestRequest.AddOrUpdate(resourceId, 1L, (_, prev) => prev + 1);

            // GET+발행은 NATS 수신 펌프를 블로킹하지 않도록 분리(fire-and-forget + 내부 예외처리).
            // 네트워크 GET을 인라인 await하면 뒤따르는 DETECT(라이브 알람) 수신이 지연됨.
            // 테스트 동기화 시드: _lastProcessingTask (동일 어셈블리 internal 노출).
            _lastProcessingTask = HandleUpdatedAsync(resourceId, seq);
        }
        catch (Exception ex)
        {
            _log?.Error($"OnNatsDetectionSyncAsync 오류: {ex.Message}");
        }
    }

    /// <summary>UPDATED 재조회 → detail 추출 → UI 스레드로 썸네일 갱신 메시지 발행. 예외는 내부 흡수(fire-and-forget).</summary>
    private async Task HandleUpdatedAsync(int detectionId, long seq)
    {
        try
        {
            var response = await _apiService.GetDetectionEventByIdAsync(detectionId).ConfigureAwait(false);

            // 404(action=DELETED 확정과 경합) 포함 — 예외 없이 로그만. Success=false로 표면화됨.
            if (response == null || !response.Success || response.Data == null)
            {
                _log?.Info($"SYNC_DETECTION 재조회 실패/미존재: id={detectionId}, success={response?.Success}, status={response?.StatusCode}");
                return;
            }

            // 순서 역전 방지: GET 진행 중 더 최신 UPDATED 요청이 시작됐으면 이 응답(오래된 것)은 폐기.
            if (_latestRequest.TryGetValue(detectionId, out var latest) && latest != seq)
            {
                _log?.Info($"SYNC_DETECTION 최신 아님(id={detectionId}, seq={seq}<{latest}) — 발행 스킵(순서 역전 방지)");
                return;
            }

            var detail = response.Data.Detail;
            var thumbnail = detail?.Thumbnail;
            var frameWidth = detail?.FrameWidth;
            var frameHeight = detail?.FrameHeight;

            _log?.Info($"SYNC_DETECTION 재조회 성공: id={detectionId}, thumbnail={(string.IsNullOrEmpty(thumbnail) ? "(없음)" : "갱신")}, frame={frameWidth}x{frameHeight}");

            var ea = _eventAggregator;
            if (ea == null) return;

            var message = new DetectionThumbnailSyncedMessage(detectionId, thumbnail, frameWidth, frameHeight);

            // UI 스레드에서 발행 → EventCardListPanelViewModel 핸들러가 UI 스레드에서 카드 갱신.
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                // 헤드리스(테스트/DB모드) — 현재 스레드 발행 폴백.
                await ea.PublishOnCurrentThreadAsync(message).ConfigureAwait(false);
                return;
            }
            // Background 우선순위: PublishOnUIThreadAsync(Normal=9)가 Input(5) 기아 유발 → Background로 하강.
            // InvokeAsync(Func<Task>)는 DispatcherOperation<Task> → 외부 await는 델리게이트 반환까지만 대기.
            // .Task.Unwrap()으로 내부 발행 Task까지 await(예외 관측 + LastProcessingTask 시드 정확성).
            await dispatcher.InvokeAsync(
                () => ea.PublishOnCurrentThreadAsync(message),
                DispatcherPriority.Background).Task.Unwrap();
        }
        catch (Exception ex)
        {
            _log?.Error($"SYNC_DETECTION HandleUpdatedAsync 오류(id={detectionId}): {ex.Message}");
        }
    }
    #endregion

    #region - Attributes -
    private readonly ILogService? _log;
    private readonly INatsService _natsService;
    private readonly IEventQueueManager _eventQueueManager;
    private readonly IEventApiService _apiService;             // GET /events/detections/{id}
    private readonly IEventAggregator? _eventAggregator;
    private readonly ITokenStorageService? _tokenStorage;      // 로그인 게이팅 — IsAuthenticated 단일 소스

    // 순서 역전 방지: resource_id별 최신 요청 세대. 처리 완료 시 최신 세대만 카드에 반영.
    private readonly ConcurrentDictionary<int, long> _latestRequest = new();

    // 테스트 동기화 시드(동일 어셈블리 internal): 마지막 UPDATED 처리 태스크. 프로덕션 로직 미사용.
    private Task? _lastProcessingTask;
    internal Task? LastProcessingTask => _lastProcessingTask;
    #endregion
}
