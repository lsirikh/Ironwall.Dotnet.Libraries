using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

/****************************************************************************
   Purpose      : 디바이스 위치(좌표/방위) 저장 seam — 맵(GMaps.Ui) 등 상위가
                  심볼 현재 위치를 디바이스 Model에 반영하고 서버 API로 저장할 때
                  사용하는 추상화. 구현은 디바이스 API/매퍼를 보유한 Devices.Ui에 둔다
                  (GMaps.Ui가 Devices.Ui/Devices.Api에 직접 결합하지 않도록).
   Created By   : GHLee
   Created On   : 2026-06-29
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 디바이스 모델의 위치(위도/경도) + 방위(Heading)를 갱신하고 서버에 저장하는 게이트웨이.
/// <para>구현은 디바이스 타입별 전체 DTO 매핑 + Update(PUT)로 저장하여 다른 필드 유실을 방지한다.</para>
/// </summary>
public interface IDeviceLocationGateway
{
    /// <summary>
    /// <paramref name="device"/>에 좌표/방위를 반영하고 서버에 저장한다.
    /// </summary>
    /// <param name="device">대상 디바이스 모델(타입별로 적절한 API로 분기).</param>
    /// <param name="latitude">새 위도(WGS84).</param>
    /// <param name="longitude">새 경도(WGS84).</param>
    /// <param name="heading">새 방위(0~360°). null이면 방위 미변경.</param>
    /// <returns>저장 성공 시 true. 실패 시 false(모델은 원래 값으로 롤백).</returns>
    Task<bool> ApplyLocationAsync(IBaseDeviceModel device, double latitude, double longitude, double? heading, CancellationToken ct = default);
}
