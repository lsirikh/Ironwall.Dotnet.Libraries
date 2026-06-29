using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/// <summary>
/// 카메라 "특정 위치 확인" 회전요청 NATS 발행 서비스 인터페이스.
/// <para>GIS→nvr_manager 로 타겟 좌표를 PUB(fire-and-forget) 발행. 실제 회전은 서버/NVR이 수행.</para>
/// </summary>
public interface ICameraAimControlService
{
    /// <summary>회전요청 body를 발행한다. 발행 실패는 예외를 던지지 않고 내부 로깅(UI 비차단).</summary>
    Task PublishAimAsync(CameraAimLocationBodyDto body, CancellationToken ct = default);
}
