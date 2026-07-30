using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Brokers;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/// <summary>
/// 스피커 방송 제어 NATS 발행 서비스 인터페이스
/// <para>PLAY/STOP은 v1.5.2 §6.4에 따라 REQ 발행 + RSP(success/req_id/message) 결과를 반환한다.</para>
/// <para>TTS는 비스펙 cmd로 서버 RSP 지원 미확인 — PUB 유지 (PRD OQ-1/V-04).</para>
/// </summary>
public interface IBroadcastControlService
{
    /// <summary>음원 재생 (BROADCAST_PLAY, REQ) — RSP 결과 반환</summary>
    Task<BrokerRequestResult> PublishPlayAsync(int speakerId, int fileGroupId, int repeat);

    /// <summary>TTS 방송 발행 (비스펙 cmd, PUB 유지)</summary>
    Task PublishTtsAsync(int speakerId, string message);

    /// <summary>방송 정지 (BROADCAST_STOP, REQ) — RSP 결과 반환</summary>
    Task<BrokerRequestResult> PublishStopAsync(int speakerId);
}
