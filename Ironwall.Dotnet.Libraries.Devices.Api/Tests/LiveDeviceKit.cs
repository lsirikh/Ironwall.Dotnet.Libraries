using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Tests;

/// <summary>
/// 라이브 장비 시험이 <b>스스로 만드는</b> 선행 장비 — 고정 id(제어기 1 · 센서 1 · 카메라 1 · 함체 3)에 기대지 않는다.
/// </summary>
/// <remarks>
/// <para><b>왜</b>: 서버 8.0 은 장비 7종이 <c>devices.id</c> 번호표 하나를 같이 쓰고(서버 <c>models/device.py</c>),
/// 샘플 시드는 <c>INIT_SAMPLE_DATA</c> 게이트 뒤에 있다(<c>utils/init_db.py:876</c>). 그래서 "id 1 은 제어기" 같은 가정은
/// 서버 상태마다 404 로 깨지고, 그 id 가 실제로 있으면 <b>남의 장비를 고치거나 지운다</b>.</para>
/// <para>본문은 8.0.9 생성 스키마의 필수 키를 채운다 — 제어기 <c>connection.ip_address</c>(<c>schemas/device.py:536-557</c>),
/// 센서 <c>controller_id</c>·<c>type_sensor</c>(:593-613), 카메라 <c>type_camera</c>·<c>connection.protocol</c>(:648-672).
/// 이름은 접두어 <c>LIVE-</c> + 무작위라 재실행 · 병렬 실행이 서로 엉키지 않는다.</para>
/// <para>지우기는 호출부의 <c>finally</c> 가 한다 — 제어기를 지우면 소속 센서가, 카메라를 지우면 프리셋이 함께 지워진다.</para>
/// </remarks>
internal static class LiveDeviceKit
{
    /// <summary>시험 장비 이름 — 접두어 + 용도 + 무작위(서버 name 상한 200자 안).</summary>
    public static string Tag(string what) => $"LIVE-{what}-{Guid.NewGuid():N}".Substring(0, 30);

    public static async Task<ControllerDeviceDto> NewControllerAsync(IDeviceApiService svc, string what)
    {
        var r = await svc.CreateControllerAsync(new ControllerDeviceDto
        {
            NumberDevice = 1,
            NameDevice = Tag(what),
            TypeDevice = "Controller",
            Status = "ACTIVATED",
            IsEnable = true,
            IpAddress = "192.168.250.1",
            IpPort = 9001,
        });
        Assert.True(r.Success, $"선행 제어기 생성 실패: {r.Error?.Code} {r.Error?.Message ?? r.Message} {r.Error?.Details}");
        Assert.True(r.Data!.Id > 0);
        return r.Data;
    }

    public static async Task<SensorDeviceDto> NewSensorAsync(IDeviceApiService svc, int controllerId, string what, string type = "Fence")
    {
        var r = await svc.CreateSensorAsync(new SensorDeviceDto
        {
            ControllerId = controllerId,
            NumberDevice = 1,
            NameDevice = Tag(what),
            TypeDevice = type,
            Status = "ACTIVATED",
            IsEnable = true,
        });
        Assert.True(r.Success, $"선행 센서 생성 실패: {r.Error?.Code} {r.Error?.Message ?? r.Message} {r.Error?.Details}");
        Assert.True(r.Data!.Id > 0);
        return r.Data;
    }

    public static async Task<CameraDeviceDto> NewCameraAsync(IDeviceApiService svc, string what, string category = "PTZ")
    {
        var r = await svc.CreateCameraAsync(new CameraDeviceDto
        {
            NumberDevice = 1,
            NameDevice = Tag(what),
            TypeDevice = "IpCamera",
            Status = "ACTIVATED",
            IsEnable = true,
            IpAddress = "192.168.250.2",
            IpPort = 554,
            UserName = "admin",
            UserPassword = "admin123",
            Mode = "ONVIF",
            Category = category,
        });
        Assert.True(r.Success, $"선행 카메라 생성 실패: {r.Error?.Code} {r.Error?.Message ?? r.Message} {r.Error?.Details}");
        Assert.True(r.Data!.Id > 0);
        return r.Data;
    }

    public static async Task<EnclosureDeviceDto> NewEnclosureAsync(IDeviceApiService svc, string what)
    {
        var r = await svc.CreateEnclosureAsync(new EnclosureDeviceDto
        {
            NumberDevice = 1,
            NameDevice = Tag(what),
            TypeDevice = "Enclosure",
            Status = "ACTIVATED",
            IsEnable = true,
        });
        Assert.True(r.Success, $"선행 함체 생성 실패: {r.Error?.Code} {r.Error?.Message ?? r.Message} {r.Error?.Details}");
        Assert.True(r.Data!.Id > 0);
        return r.Data;
    }
}
