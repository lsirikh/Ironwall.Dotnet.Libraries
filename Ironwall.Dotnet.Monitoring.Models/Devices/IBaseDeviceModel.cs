using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Enums;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public interface IBaseDeviceModel : IBaseModel
{
    List<int>? DeviceGroups { get; set; }
    string? DeviceName { get; set; }
    int DeviceNumber { get; set; }
    EnumDeviceType DeviceType { get; set; }
    EnumDeviceStatus Status { get; set; }
    string? Version { get; set; }
    string? Location { get; set; }
    double Latitude { get; set; }
    double Longitude { get; set; }
    bool IsEnable { get; set; }
    double? Heading { get; set; }
    double? Altitude { get; set; }

    // ── v7.0+ 표현 모델 (device-console-v8 FR-03) — 전부 읽기 경로용, 와이어 직렬화에 나가지 않는다 ──

    /// <summary>
    /// 판별자(<c>category_device</c>) — "무엇인가". <b>경로가 정본이고 바뀌지 않는다</b>.
    /// 6.3 응답에는 없으므로 매핑 계층이 DTO 종류(=온 경로)로 채운다. 미상은 <see cref="EnumDeviceCategory.None"/>.
    /// </summary>
    EnumDeviceCategory CategoryDevice { get; set; }

    /// <summary>
    /// 카테고리별 종류축(<c>type_&lt;category&gt;</c>)의 <b>서버 원값</b> — 카탈로그 코드 문자열.
    /// 클라 enum 은 서버 어휘를 못 따라가므로(<c>SPEED_DOME</c>·<c>SmartController</c>) enum 을 거치지 않고 보존한다.
    /// <see cref="DeviceType"/> 은 이 값과 판별자에서 나온 <b>파생값</b>으로 남는다.
    /// </summary>
    string? TypeAxisCode { get; set; }

    /// <summary>소속 부대 id(<c>unit_id</c>, 서버 8.0+). 6.3·7.0 응답이면 <c>null</c>.</summary>
    int? UnitId { get; set; }

    /// <summary>표현 축 묶음. 축을 하나도 받지 못했으면(6.3) <c>null</c> — 빈 묶음을 지어내지 않는다.</summary>
    IDeviceAxesModel? Axes { get; set; }
}