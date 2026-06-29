using System;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Services;

/****************************************************************************
   Purpose      : IDeviceLocationGateway 구현 — 장비의 좌표(+방위)만 서버에 부분 저장.
                  geolocation JSONB만 PATCH(전체 교체)하므로 이름/IP/비번/hardware_spec 등
                  다른 필드는 전송하지 않아 보존된다(H1: 전체 DTO PUT의 자격증명/스펙 유실 회피).
                  geolocation은 전체 교체이므로 보존 하위필드(location/altitude)를 포함해 보낸다.
                  실제 회전/저장 성공 시에만 in-memory 모델을 갱신(롤백 불요).
   Created By   : GHLee
   Created On   : 2026-06-29
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class DeviceLocationGateway : IDeviceLocationGateway
{
    #region - Ctors -
    public DeviceLocationGateway(IDeviceApiService apiService, ILogService? log = null)
    {
        _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
        _log = log;
    }
    #endregion

    #region - Implementation of Interface -
    public async Task<bool> ApplyLocationAsync(IBaseDeviceModel device, double latitude, double longitude, double? heading, CancellationToken ct = default)
    {
        if (device is null || device.Id <= 0)
        {
            _log?.Warning("[DeviceLocation] 저장 취소 — 유효하지 않은 디바이스");
            return false;
        }
        ct.ThrowIfCancellationRequested();

        string? kind = DeviceKindPath(device);
        if (kind == null)
        {
            _log?.Warning($"[DeviceLocation] 미지원 디바이스 타입: {device.GetType().Name}");
            return false;
        }

        // geolocation JSONB는 PATCH 시 전체 교체 → 보존할 하위필드(location/altitude)도 채워 보낸다.
        var geo = new GeolocationDto
        {
            Location = device.Location,
            Latitude = latitude,
            Longitude = longitude,
            Altitude = device.Altitude,
            Heading = heading ?? device.Heading
        };

        try
        {
            var r = await _apiService.PatchGeolocationAsync(kind, device.Id, geo, ct).ConfigureAwait(false);
            if (r.Success)
            {
                // 성공 시에만 in-memory 모델 반영(실패 시 모델 무변경 → 롤백 불요)
                device.Latitude = latitude;
                device.Longitude = longitude;
                if (heading.HasValue) device.Heading = heading.Value;
                _log?.Info($"[DeviceLocation] 저장 — {kind}/{device.Id} ({latitude:F6},{longitude:F6}) heading={geo.Heading?.ToString("F0") ?? "-"}");
                return true;
            }

            _log?.Warning($"[DeviceLocation] 저장 실패 응답 — {kind}/{device.Id}: {r.Error?.Details}");
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log?.Error($"[DeviceLocation] 저장 실패 — id={device.Id}: {ex.Message}");
            return false;
        }
    }
    #endregion

    #region - Processes -
    /// <summary>디바이스 모델 → API 엔드포인트 경로 세그먼트.</summary>
    private static string? DeviceKindPath(IBaseDeviceModel device) => device switch
    {
        CameraDeviceModel => "cameras",
        SensorDeviceModel => "sensors",
        ControllerDeviceModel => "controllers",
        SpeakerDeviceModel => "speakers",
        EnclosureDeviceModel => "enclosures",
        LampDeviceModel => "lamps",
        _ => null
    };
    #endregion

    #region - Attributes -
    private readonly IDeviceApiService _apiService;
    private readonly ILogService? _log;
    #endregion
}
