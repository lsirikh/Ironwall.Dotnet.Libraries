using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

/// <summary>
/// 형상 축(<c>hardware_spec</c>) — 장비 제원 + 부품 선언(<c>components[]</c>). <b>장비별 사실만</b> 담는다
/// (유형 공통 사실 — 상태 어휘·명령·제어 가능 여부 — 은 카탈로그 <c>/api/devices/spec</c> 몫).
/// </summary>
public interface IHardwareSpecModel
{
    int? Schema { get; set; }
    string? Manufacturer { get; set; }
    string? Model { get; set; }
    string? Serial { get; set; }
    string? Firmware { get; set; }
    string? HardwareRev { get; set; }
    string? MacAddress { get; set; }
    double? MaxDetectionRange { get; set; }
    string? OnvifVersion { get; set; }

    /// <summary>카테고리별 자유 제원 — 닫힌 스키마가 아니라 원본 그대로 둔다.</summary>
    JObject? Spec { get; set; }

    /// <summary>부품 선언. 빈 목록은 "형상 미입력"이다(절 자체가 미수신이면 축이 <c>null</c>).</summary>
    IList<ComponentDefinitionModel> Components { get; }
}
