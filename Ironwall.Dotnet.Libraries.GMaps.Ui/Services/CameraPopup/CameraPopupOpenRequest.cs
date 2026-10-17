using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.CameraPopup;

/****************************************************************************
   Purpose      : 브로커 모드 더블클릭 한 건 — CAMERA_POPUP_OPEN 에 실을 값 (camera-popup-modes T-07 · FR-05/08)
   Created By   : Claude (T-07)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 더블클릭 한 번이 NVR Manager 에 보낼 요청. 설정(<see cref="CameraPopupSettings"/>)의 브로커 묶음 +
/// 카메라 · 관제석 · 요청자를 한데 모은다. <see cref="ToBody"/> 가 명세 필드로 옮긴다.
/// </summary>
/// <param name="CameraId">카메라 장비 ID(REST 정본).</param>
/// <param name="CameraName">토스트에 보일 이름.</param>
/// <param name="TargetClientId">이 관제석 식별자(<c>SetupModel.ClientId</c>).</param>
/// <param name="Monitor">모니터 번호(1부터).</param>
/// <param name="Cell">칸 1~9, 0 = 자동(보내지 않는다).</param>
/// <param name="OnOccupied">칸이 차 있으면 교체/거부.</param>
/// <param name="RequestedBy">요청한 운영자(빈 값이면 보내지 않는다).</param>
/// <param name="TimeoutSeconds">응답 기다림(초) — 설정 "응답 기다림".</param>
public sealed record CameraPopupOpenRequest(
    int CameraId,
    string CameraName,
    string TargetClientId,
    int Monitor,
    int Cell,
    CameraPopupOnOccupied OnOccupied,
    string? RequestedBy,
    int TimeoutSeconds)
{
    /// <summary>설정 한 벌 + 카메라 · 관제석 · 요청자로 요청을 만든다(설정은 정규화한 값을 쓴다).</summary>
    public static CameraPopupOpenRequest From(CameraPopupSettings settings, int cameraId, string? cameraName,
                                              string? clientId, string? requestedBy)
    {
        var s = (settings ?? new CameraPopupSettings()).Normalize();
        var name = string.IsNullOrWhiteSpace(cameraName) ? $"카메라 {cameraId}" : cameraName.Trim();
        return new CameraPopupOpenRequest(cameraId, name, clientId?.Trim() ?? string.Empty, s.BrokerMonitor, s.BrokerCell,
                                          s.BrokerOnOccupied, requestedBy, s.BrokerResponseTimeoutSeconds);
    }

    /// <summary>
    /// 명세 §11.5.9 G-52 body. 칸 자동(0)은 <c>cell</c> 을 생략한다 — 받는 쪽이 기본 자리를 쓴다.
    /// 모니터는 설정이 늘 1 이상이라 보낸다.
    /// </summary>
    public CameraPopupOpenBodyDto ToBody() => new()
    {
        TargetClientId = TargetClientId,
        CameraId = CameraId,
        Monitor = Monitor >= 1 ? Monitor : null,
        Cell = Cell is >= 1 and <= CameraPopupSettings.MaxBrokerCell ? Cell : null,
        OnOccupied = OnOccupied == CameraPopupOnOccupied.Reject
            ? CameraPopupOpenBodyDto.OnOccupiedReject
            : CameraPopupOpenBodyDto.OnOccupiedReplace,
        RequestedBy = string.IsNullOrWhiteSpace(RequestedBy) ? null : RequestedBy,
    };
}
