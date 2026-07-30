using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Brokers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/// <summary>
/// 카메라 "특정 위치 확인" 회전요청 NATS 발행 서비스 인터페이스.
/// <para>GIS→nvr_manager로 타겟 좌표를 REQ 발행하고 RSP(success/req_id/message)를 확인한다
/// (v1.5.2 §2.9 — PTZ_AIM_LOCATION의 과거 PUB 예외 폐지). 실제 회전은 서버/NVR이 수행.</para>
/// </summary>
public interface ICameraAimControlService
{
    /// <summary>회전요청 body를 REQ 발행하고 결과를 반환한다. 실패도 예외가 아닌 결과로 반환(UI 비차단).</summary>
    Task<BrokerRequestResult> PublishAimAsync(CameraAimLocationBodyDto body, CancellationToken ct = default);
}
