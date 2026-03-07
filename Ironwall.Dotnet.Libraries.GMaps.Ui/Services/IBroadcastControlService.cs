namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/// <summary>
/// 스피커 방송 제어 NATS 발행 서비스 인터페이스
/// </summary>
public interface IBroadcastControlService
{
    /// <summary>음원 재생 (BROADCAST_PLAY) 발행</summary>
    Task PublishPlayAsync(int speakerId, int fileGroupId, int repeat);

    /// <summary>TTS 방송 발행</summary>
    Task PublishTtsAsync(int speakerId, string message);

    /// <summary>방송 정지 (BROADCAST_STOP) 발행</summary>
    Task PublishStopAsync(int speakerId);
}
