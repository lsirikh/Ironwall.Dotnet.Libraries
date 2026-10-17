using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

/// <summary>
/// 의도 축(<c>device_config</c>) — 운용자가 "이렇게 동작하라"고 정한 값. 관측(<see cref="IDeviceStatusModel"/>)과 짝이다.
/// </summary>
/// <remarks>
/// 세 묶음 모두 키 구성이 카탈로그(<c>metric_key</c>·<c>device_mode</c>·부품 key)에 달려 있어
/// 닫힌 형으로 옮기지 않고 <b>원본 그대로</b> 둔다 — 모르는 키를 버리지 않기 위해서다.
/// </remarks>
public interface IDeviceConfigModel
{
    int? Schema { get; set; }

    /// <summary>임계치 — <c>{"temperature":{"high":45}}</c> 꼴(함체).</summary>
    JObject? Thresholds { get; set; }

    /// <summary>모드 — <b>카메라만</b> 가진다(<c>is_record</c> 등). 다른 카테고리에 실으면 422.</summary>
    JObject? Modes { get; set; }

    /// <summary>부품 덮어쓰기 — 부품 key 별. 부품을 지울 때는 여기서도 <c>null</c> 로 같이 지워야 한다(서버 회신 2026-09-19).</summary>
    JObject? ComponentOverrides { get; set; }
}
