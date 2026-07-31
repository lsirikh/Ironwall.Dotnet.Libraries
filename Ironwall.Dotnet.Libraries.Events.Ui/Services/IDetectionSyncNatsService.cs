namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;

/// <summary>
/// NATS SYNC_DETECTION(all.sync.detection) 구독 서비스 계약 — PTZ 회전 후 썸네일 갱신.
/// DetectionNatsSyncService(cmd=="DETECT", 최초 탐지)와 독립. cmd=="SYNC_DETECTION"만 처리.
/// </summary>
public interface IDetectionSyncNatsService
{
    Task StartService(CancellationToken token = default);
    Task StopAsync(CancellationToken token = default);
}
