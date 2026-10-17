using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;
public interface IPidsSymbolModel : IPidsEventCapable
{
    double DetectionAngle { get; set; }
    double DetectionBearing { get; set; }
    double DetectionRange { get; set; }
    EnumDeviceType DeviceType { get; set; }
    string? ModelVariant { get; set; }
    EnumColorType FOVColor { get; set; }
    double FOVOpacity { get; set; }
    int LinkedDeviceId { get; set; }
    bool ShowFOV { get; set; }
    double BaseBearing { get; set; }

    /// <summary>통문 폭(m), NULL=기본 4.0 — Gate 전용(FR-12)</summary>
    double? GateWidthM { get; set; }
    /// <summary>ContactOn=열림 해석 여부(FR-12)</summary>
    bool OpenOnContactOn { get; set; }
    /// <summary>개폐 형태 축 — 런타임 전용, 직렬화 제외(FR-12)</summary>
    EnumDoorState DoorState { get; set; }

    /// <summary>
    /// 연결된 디바이스 객체 (런타임 바인딩용, JSON 직렬화 제외)
    /// <para>설정 시 LinkedDeviceId가 자동 동기화됩니다.</para>
    /// </summary>
    IBaseDeviceModel? LinkedDevice { get; set; }

    /// <summary>
    /// LinkedDeviceId를 기반으로 디바이스 목록에서 LinkedDevice를 바인딩합니다.
    /// <para>Legacy JSON 로드 시 마이그레이션에 사용됩니다.</para>
    /// </summary>
    /// <param name="deviceList">검색할 디바이스 목록</param>
    void BindToDeviceList(IEnumerable<IBaseDeviceModel> deviceList);

    event EventHandler Update;
}
