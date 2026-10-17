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

    /// <summary>
    /// 마이크 방송 cmd 를 서버가 받아들이는가(FR-23).
    /// <para><b>false 인 동안 UI 는 버튼을 비활성으로 두고 사유를 보여준다</b> — 눌러도 아무 일이 없는 버튼을
    /// 활성으로 두지 않는다. 서버 회신(SETUP-02) 후 true 로 바꾸면 아래 두 메서드가 그대로 동작한다.</para>
    /// </summary>
    bool IsMicCommandSupported { get; }

    /// <summary>마이크 방송 시작 (BROADCAST_MIC_START, REQ). 누르고 있는 동안만 유효 — 토글이 아니다.</summary>
    Task<BrokerRequestResult> PublishMicStartAsync(int speakerId);

    /// <summary>마이크 방송 중지 (BROADCAST_MIC_STOP, REQ). 버튼에서 손을 떼면 즉시 보낸다.</summary>
    Task<BrokerRequestResult> PublishMicStopAsync(int speakerId);
}
