using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Devices;

/****************************************************************************
   Purpose      : 장비 소속 부대 바꾸기 본문 조립 — 받은 값을 되돌리지 않는다 (N-11 FR-12)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 장비 한 대의 <c>unit_id</c> 만 바꾸는 <b>좁은 PATCH 본문</b>을 만든다.
/// </summary>
/// <remarks>
/// <para><b>왜 받은 DTO 를 그대로 되보내지 않는가</b> — 축 계약에서 <c>connection</c> 은 평면 필드에서
/// <b>재조립</b>되므로 그대로 되보내면 <c>type</c> 이 <c>IP_DIRECT</c> 로 초기화되고
/// <c>channel</c>·<c>parent_device_id</c> 가 소실된다. <c>hardware_spec</c> 을 실으면
/// <c>components[]</c> 가 통째 교체된다(8.0.1 실측 — <c>ComponentApplyService</c> 주석).
/// 그래서 <b>새 DTO</b> 를 만들고, 축 모드에서 <b>조건 없이 직렬화되는 키만</b> 받은 값으로 채운다.</para>
/// <para><b>왜 그 키들을 채워야 하는가</b> — <c>PATCH</c> 는 RFC 7396 병합이라 <c>null</c> 은
/// <b>그 키의 삭제</b>다. 스피커·경광등의 <c>description</c> 은 조건이 하나도 없어 안 채우면
/// <c>"description": null</c> 로 나가 서버에서 지워진다. 이 판정을 사람 눈에 맡기지 않도록
/// <c>UnitAssignRequestTests.should_never_null_or_reset_a_fetched_value_in_the_assign_body</c> 가
/// 일곱 카테고리를 전수로 잠근다.</para>
/// <para><b>여기서는 <c>UnitScopeGate.StampAsync</c> 를 부르지 않는다</b> — 그 관문은 언제나
/// "이 클라이언트의 부대"를 찍는다. 이 경로는 <b>고른 부대</b>로 옮기는 것이 목적이므로
/// <see cref="Build"/> 가 정한 값이 그대로 나가야 한다.</para>
/// </remarks>
public static class UnitAssignRequestBuilder
{
    /// <summary>받은 DTO 에서 <paramref name="unitId"/> 만 바꾼 좁은 본문을 만든다.</summary>
    /// <param name="fetched">방금 단건 조회로 받은 DTO.</param>
    /// <param name="unitId">옮겨 갈 부대 id.</param>
    public static BaseDeviceDto Build(BaseDeviceDto fetched, int unitId)
    {
        ArgumentNullException.ThrowIfNull(fetched);
        if (unitId <= 0) throw new ArgumentOutOfRangeException(nameof(unitId), unitId, "부대 id 는 1 이상이어야 합니다.");

        BaseDeviceDto dto = fetched switch
        {
            ControllerDeviceDto origin => Controller(origin),
            SensorDeviceDto => new SensorDeviceDto(),
            CameraDeviceDto origin => Camera(origin),
            SpeakerDeviceDto origin => Speaker(origin),
            EnclosureDeviceDto origin => Enclosure(origin),
            LampDeviceDto origin => Lamp(origin),
            GateDeviceDto origin => Gate(origin),
            _ => throw new ArgumentException($"소속 부대를 바꿀 수 없는 장비입니다: {fetched.GetType().Name}", nameof(fetched)),
        };

        CopyCommon(fetched, dto);
        dto.UnitId = unitId;
        return dto;
    }

    #region - 카테고리마다 "조건 없이 나가는" 축만 채운다 -
    private static ControllerDeviceDto Controller(ControllerDeviceDto origin) => new()
    {
        // connection 은 축 모드에서 무조건 나간다 — 평면 두 칸이 그 축의 유일한 재료다.
        IpAddress = origin.IpAddress,
        IpPort = origin.IpPort,
    };

    private static CameraDeviceDto Camera(CameraDeviceDto origin) => new()
    {
        // connection 에 protocol 이 필수다 — 비우면 "NONE" 으로 덮인다.
        Mode = origin.Mode,
        IpAddress = origin.IpAddress,
        IpPort = origin.IpPort,
        UserName = origin.UserName,
        UserPassword = origin.UserPassword,
        Urls = origin.Urls,
        // device_config 는 카메라만 조건 없이 나간다 — 모드 묶음이 같은 축이다.
        IsRecord = origin.IsRecord,
        DeviceConfigModes = origin.DeviceConfigModes,
    };

    private static SpeakerDeviceDto Speaker(SpeakerDeviceDto origin) => new()
    {
        // speaker_role 기본값이 "NORMAL" 이다 — 비우면 ADMIN 스피커가 NORMAL 로 덮인다.
        SpeakerType = origin.SpeakerType,
        // description 은 조건이 하나도 없다 — 안 채우면 null 로 나가 서버에서 지워진다.
        Description = origin.Description,
    };

    private static EnclosureDeviceDto Enclosure(EnclosureDeviceDto origin) => new()
    {
        HeaterEnabled = origin.HeaterEnabled,
        FanEnabled = origin.FanEnabled,
        ThresholdConfig = origin.ThresholdConfig,
    };

    private static LampDeviceDto Lamp(LampDeviceDto origin) => new()
    {
        Description = origin.Description,
        IpAddress = origin.IpAddress,
        IpPort = origin.IpPort,
        UserName = origin.UserName,
        UserPassword = origin.UserPassword,
    };

    private static GateDeviceDto Gate(GateDeviceDto origin) => new()
    {
        // 통문의 connection 은 link_info·urls 에서 조립된다 — 둘 다 없으면 축이 통째로 빠진다.
        Urls = origin.Urls,
        LinkInfo = origin.LinkInfo,
    };

    /// <summary>축 모드에서 무조건 나가는 공통 키만 받은 값으로. <c>id</c>·<c>geolocation</c>·<c>group_ids</c> 는 건드리지 않는다.</summary>
    private static void CopyCommon(BaseDeviceDto source, BaseDeviceDto target)
    {
        target.NumberDevice = source.NumberDevice;
        target.NameDevice = source.NameDevice;
        target.Status = source.Status;
        target.IsEnable = source.IsEnable;
        // 종류축(type_device)은 값이 없으면 나가지 않는다 — 기본 생성자가 넣은 값을 지워 덮어쓰기를 막는다.
        target.TypeDevice = string.Empty;
        target.UseAxisWrite = true;
    }
    #endregion
}
