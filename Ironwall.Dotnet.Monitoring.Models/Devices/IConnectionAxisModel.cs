using System.Collections.Generic;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

/// <summary>
/// 접속 축(<c>connection</c>) — 장비에 "어떻게 닿는가". 결선 방식은 종류가 아니라 이 축이다.
/// </summary>
public interface IConnectionAxisModel
{
    /// <summary>접속 방식 판별자 — <c>IP_DIRECT · IP_CONVERTER · CONTROLLER_CONTACT · RS485 · ENCLOSURE_CONTACT · SERVER_MANAGED · NONE</c>(서버 엄격 어휘).</summary>
    string? Type { get; set; }
    string? IpAddress { get; set; }
    int? IpPort { get; set; }
    string? UserName { get; set; }
    string? UserPassword { get; set; }

    /// <summary>접점·RS485 로 물린 상위 장비(제어기·함체)의 id.</summary>
    int? ParentDeviceId { get; set; }

    /// <summary>상위 장비의 채널 번호.</summary>
    int? Channel { get; set; }

    /// <summary>프로토콜 — 카메라는 필수(6.3 평면 <c>mode</c> 의 새 자리).</summary>
    string? Protocol { get; set; }

    /// <summary>링크 묶음(<c>connection.urls</c>) — 키는 카테고리마다 다르므로 이름→주소 사전으로 받는다.</summary>
    IDictionary<string, string?> Urls { get; }
}
