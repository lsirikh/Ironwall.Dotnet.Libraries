using System;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Nats.Services;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Brokers;

/****************************************************************************
   Purpose      : GIS→매니저 제어 REQ 공통 실행기 (GIS.md v1.5.2 §6.4/§6.9).
                  WINDY 선례(NatsDomainService)의 REQ+RSP 3분기 패턴을
                  라이브러리 발행 서비스(Broadcast/Aim/Lamp)용으로 공통화했다.
                  - m_type=REQ, from=GIS 봉투 생성 → RequestAsync(기본 5s)
                  - 응답 3분기: null(무응답) / success=false(거부) / 성공
                  - §6.9 서버 message 패턴 → 한글 안내문 매핑(UserMessage)
   Created By   : GHLee
   Created On   : 2026-07-30
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class BrokerRequestClient : IBrokerRequestClient
{
    /// <summary>기본 RSP 대기 시간 — 현장 RSP 지연 이력으로 5초 (WINDY 선례와 동일)</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private const string FROM_GIS = "GIS";

    #region - Ctors -
    public BrokerRequestClient(INatsService natsService, ILogService? log = null)
    {
        _natsService = natsService ?? throw new ArgumentNullException(nameof(natsService));
        _log = log;
    }
    #endregion

    #region - Implementation of Interface -
    public async Task<BrokerRequestResult> RequestAsync<TBody>(
        string subject,
        string command,
        TBody body,
        TimeSpan? timeout = null,
        CancellationToken ct = default) where TBody : class
    {
        if (string.IsNullOrWhiteSpace(subject) || body is null)
            return BrokerRequestResult.Fail(EnumBrokerFailure.Invalid, "요청 정보가 유효하지 않습니다.");

        try
        {
            ct.ThrowIfCancellationRequested();

            var msg = body.ToBrokerRequest(command, FROM_GIS);   // m_type=REQ, id=GUID
            var json = msg.ToJson();

            var reply = await _natsService
                .RequestAsync(subject, json, timeout ?? DefaultTimeout)
                .ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            // ① 무응답 — 타임아웃/무구독자(no-responders). 세부 사유는 NatsService가 로그로 구분.
            if (reply is null)
            {
                _log?.Warning($"[BrokerReq] {command} 무응답 — subject={subject}, req_id={msg.Id}");
                return BrokerRequestResult.Fail(
                    EnumBrokerFailure.NoResponse,
                    "대상 서비스 응답 없음 — 서비스 기동/설정을 확인하세요.",
                    reqId: msg.Id);
            }

            // ② RSP 봉투 파싱 (§6.9 — 실패 응답은 body=null이므로 object로 관용 파싱)
            Messages.Defines.Brokers.BrokerResponse<object>? rsp;
            try
            {
                rsp = BrokerMessageHelper.FromJsonResponse<object>(reply);
            }
            catch (Newtonsoft.Json.JsonException)
            {
                rsp = null;   // 비JSON/봉투 불일치 → ParseError로 분류
            }
            if (rsp is null)
            {
                _log?.Warning($"[BrokerReq] {command} RSP 해석 실패 — subject={subject}, raw={Truncate(reply)}");
                return BrokerRequestResult.Fail(
                    EnumBrokerFailure.ParseError,
                    "서버 응답을 해석하지 못했습니다.",
                    reqId: msg.Id);
            }

            // ③ 서버 거부
            if (!rsp.Success)
            {
                _log?.Warning($"[BrokerReq] {command} 거부 — message='{rsp.Message}', req_id={rsp.RequestId}");
                return BrokerRequestResult.Fail(
                    EnumBrokerFailure.Rejected,
                    MapServerMessage(rsp.Message),
                    serverMessage: rsp.Message ?? string.Empty,
                    reqId: rsp.RequestId);
            }

            _log?.Info($"[BrokerReq] {command} 성공 — subject={subject}, message='{rsp.Message}'");
            return BrokerRequestResult.Ok(rsp.Message ?? string.Empty, rsp.RequestId);
        }
        catch (OperationCanceledException)
        {
            // 호출 측 취소(ESC 등) — 정상 흐름
            _log?.Info($"[BrokerReq] {command} 취소(cancelled) — subject={subject}");
            return BrokerRequestResult.Fail(EnumBrokerFailure.Cancelled, "요청이 취소되었습니다.");
        }
        catch (Exception ex)
        {
            // 직렬화/전송 예외 — UI 비차단(예외 격리)
            _log?.Error($"[BrokerReq] {command} 실패 — subject={subject}: {ex.Message}");
            return BrokerRequestResult.Fail(EnumBrokerFailure.NoResponse, "요청 전송에 실패했습니다.");
        }
    }
    #endregion

    #region - Processes -
    /// <summary>
    /// §6.9 서버 에러 message 패턴 → 사용자 안내 한글 문구.
    /// 알려진 패턴 외에는 서버 원문을 그대로 노출한다.
    /// </summary>
    public static string MapServerMessage(string? serverMessage)
    {
        if (string.IsNullOrWhiteSpace(serverMessage))
            return "서버가 요청을 거부했습니다.";

        if (serverMessage.StartsWith("Unknown command", StringComparison.OrdinalIgnoreCase))
            return "서버가 명령을 인식하지 못했습니다. (버전 불일치 가능)";
        if (serverMessage.StartsWith("Handler error", StringComparison.OrdinalIgnoreCase))
            return "서버 처리 중 오류가 발생했습니다.";
        if (serverMessage.StartsWith("Camera not found", StringComparison.OrdinalIgnoreCase))
            return "카메라 장비를 찾을 수 없습니다.";
        if (serverMessage.StartsWith("Lamp not found", StringComparison.OrdinalIgnoreCase))
            return "경광등 장비를 찾을 수 없습니다.";
        if (serverMessage.StartsWith("NVR connection failed", StringComparison.OrdinalIgnoreCase))
            return "NVR 서버 연결에 실패했습니다.";
        if (serverMessage.StartsWith("Timeout waiting", StringComparison.OrdinalIgnoreCase))
            return "서버 응답 대기 시간을 초과했습니다.";

        return serverMessage;   // 미분류 — 서버 원문 노출
    }

    private static string Truncate(string s) => s.Length <= 200 ? s : s[..200] + "…";
    #endregion

    #region - Attributes -
    private readonly INatsService _natsService;
    private readonly ILogService? _log;
    #endregion
}
