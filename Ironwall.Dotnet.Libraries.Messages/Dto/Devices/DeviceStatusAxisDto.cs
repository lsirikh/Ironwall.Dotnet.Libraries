using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
/****************************************************************************
   Purpose      : 서버 7.0+ device_status(관측 축) 응답 DTO — 문 위치의 유일한 자리
   Created By   : GHLee
   Created On   : 9/18/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 서버 7.0 <c>DeviceStatusAxisOut</c> — <c>devices.device_status</c> 의 <b>응답 모양</b>.
/// <b>읽기 전용</b>이다.
/// </summary>
/// <remarks>
/// <para><b>절대 요청 본문에 실지 않는다</b> — 장비 본문의 <c>device_status</c> 는 <c>REMOVED_FIELD</c> 가 아니라
/// <c>422 OBSERVED_FIELD</c> 이고 <c>moved_to</c> 조차 실리지 않는다(명세 §5 머리, 헌장 P4-3).
/// 보고는 매니저가 <c>PATCH /api/devices/{종류}/{id}/component-status</c>(<c>devices:control</c>) 로 한다.
/// 그래서 이 DTO 를 참조하는 프로퍼티는 전부 <c>ShouldSerialize…() =&gt; false</c> 다.</para>
/// <para><b>키가 아예 없을 수 있다</b> — 목록 기본 프로필은 <c>view=basic</c> 이고 그때
/// <c>device_status</c> 는 <b>키째 오지 않는다</b>(A-devices D-25). 즉 <c>null</c> 은
/// "부품이 없다"가 아니라 <b>"섹션을 안 받았다"</b> 다. 두 경우를 값으로 구분할 수 없으니
/// 읽는 쪽은 <c>null</c> 을 "모름"으로 다뤄야 한다.</para>
/// </remarks>
public class DeviceStatusAxisDto
{
    [JsonProperty("schema", Order = 1, NullValueHandling = NullValueHandling.Ignore)]
    public int? Schema { get; set; }

    /// <summary>부품 <c>key</c> → 현재 상태.</summary>
    [JsonProperty("components", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, ComponentStatusDto>? Components { get; set; }

    /// <summary>주어진 부품 key 의 상태를 찾는다(대소문자 무시). 없으면 <c>null</c>.</summary>
    public ComponentStatusDto? Find(string? key)
    {
        if (Components == null || string.IsNullOrWhiteSpace(key)) return null;
        if (Components.TryGetValue(key, out var hit)) return hit;
        foreach (var pair in Components)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)) return pair.Value;
        }
        return null;
    }
}

/// <summary>
/// 서버 7.0 <c>ComponentStatusOut</c> — <c>device_status.components{}</c> 의 값. "지금 어떤가".
/// </summary>
/// <remarks>
/// <para><b><see cref="ObservedAt"/> 은 <c>null</c> 일 수 있다</b>(요청 모델과 딱 이 한 가지가 다르다).
/// 옛 스칼라(<c>door_status</c>·<c>gate_status</c>)에서 이관된 행에는 관측 시각이 없고
/// <b>서버가 지어내지 않는다</b>. 같은 값 재보고도 시각을 갱신하지 않으므로(ST-3)
/// <b>보고한 주체 자신도</b> 이 <c>null</c> 을 만난다(서버 실측 2026-09-13: 부품 162개 중 8개).</para>
/// <para><see cref="State"/> 도 <c>null</c> 일 수 있다 — 계측·수동 유형(온도·습도·UPS·렌즈 등)은
/// <b>건강만</b> 보고한다. 즉 <c>State == null</c> 은 "꺼짐"이 아니라 "이 부품엔 동작 상태 축이 없다"다.</para>
/// </remarks>
public class ComponentStatusDto
{
    /// <summary>관측 시각 — <b>null 가능</b>(위 remarks).</summary>
    [JsonProperty("observed_at", Order = 1, NullValueHandling = NullValueHandling.Ignore)]
    public string? ObservedAt { get; set; }

    /// <summary>동작 상태 — 카탈로그 <c>component_type.definition.states</c> 중 하나(대문자). 유형에 상태 축이 없으면 <c>null</c>.</summary>
    [JsonProperty("state", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    public string? State { get; set; }

    /// <summary>건강 상태 — <see cref="ComponentHealthNames"/>. 항목마다 필수라 응답에는 늘 있다.</summary>
    [JsonProperty("health", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public string? Health { get; set; }

    /// <summary>고장 사유 — 카탈로그 <c>component_fault</c> 어휘. <c>health</c> 가 FAULT·DEGRADED 일 때만 있다.</summary>
    [JsonProperty("fault_reason", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
    public string? FaultReason { get; set; }
}
