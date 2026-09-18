using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Models;
/****************************************************************************
   Purpose      : GET /api/devices/by-component 응답 행 DTO (A-devices D-3 · D-30)
   Created By   : GHLee
   Created On   : 9/18/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 서버 8.0 <c>ComponentStateRow</c> — <c>GET /api/devices/by-component</c> 의 항목.
/// <b>장비 참조 + 그 부품 한 개의 관측</b>이다.
/// </summary>
/// <remarks>
/// <para><b>왜 이 통로가 필요한가</b> — 7.0 에서 <c>enclosures?door_status=</c>·<c>gates?gate_status=</c> 가
/// 제거되면서(문 위치가 스칼라에서 <c>device_status.components</c> 로 이관) 문 상태를 <b>일괄로</b>
/// 조회할 방법이 사라졌다. 장비를 <c>?view=full</c> 로 전건 받아 <c>device_status</c> 를 파는 우회는
/// 페이지 수만큼 왕복이 늘고 <c>view=basic</c> 기본값에 걸리면 <b>키째 없어</b> 조용히 "설정 안 됨"이 된다.
/// 이 엔드포인트는 그 한 가지 질문(<b>"지금 열려 있는 문이 어디인가"</b>)에 1회 왕복으로 답한다.</para>
///
/// <para><b>DS-1 — 한 장비에 같은 유형 부품이 둘이면 두 행이다.</b> 즉 <see cref="Id"/> 는
/// 응답 안에서 유일하지 않다. <c>id</c> 로 사전을 만들면 뒤 행이 앞 행을 덮어쓴다 —
/// 키는 <c>(id, component)</c> 복합으로 잡는다.</para>
///
/// <para><b>이름·종류축이 없다</b>(헌장 P2-6 "참조에는 종류축이 없다"). 서버가 의도적으로 빼는 것이라
/// 표시명은 <b>장비 캐시가 정본</b>이고 이 응답으로 이름을 갱신하면 낡은 이름이 굳는다.</para>
///
/// <para><b>DTO 위치</b> — 이 프로젝트가 소유한다(<c>Messages</c> 가 아니다). 장비 도메인의 다른 DTO 와
/// 달리 <c>by-component</c> 는 <b>장비 표현이 아니라 조회 결과 투영</b>이고, 브로커 메시지로도
/// 오가지 않는다. <c>Messages</c> 에 같은 이름이 생기면 이 파일을 지우고 그쪽으로 이관한다.</para>
/// </remarks>
public class ComponentStateRowDto
{
    /// <summary>장비 id(필수). ⚠ 응답 안에서 <b>유일하지 않다</b> — DS-1 참조.</summary>
    [JsonProperty("id", Order = 1)]
    public int Id { get; set; }

    /// <summary>
    /// 장비 카테고리(필수) — <c>camera</c>·<c>controller</c>·<c>enclosure</c>·<c>gate</c>·
    /// <c>lamp</c>·<c>sensor</c>·<c>speaker</c>(<b>단수</b>).
    /// </summary>
    /// <remarks>
    /// 경로 세그먼트(<c>enclosures</c>)와 <b>단·복수가 다르다</b> — 이 값을 URL 에 그대로 끼우면 404 다.
    /// 변환은 <see cref="Helpers.DeviceTypePaths.FromCategory"/> 가 한다.
    /// </remarks>
    [JsonProperty("category_device", Order = 2)]
    public string CategoryDevice { get; set; } = string.Empty;

    /// <summary>부품 <c>key</c>(필수) — 장비가 <c>hardware_spec.components[].key</c> 로 선언한 식별자.</summary>
    /// <remarks>대소문자·공백을 <b>그대로</b> 비교하는 자유 문자열이다(닫힌 어휘가 아니다).</remarks>
    [JsonProperty("component", Order = 3)]
    public string Component { get; set; } = string.Empty;

    /// <summary>
    /// 부품 유형 — <c>DOOR_SENSOR</c>·<c>DOOR_ACTUATOR</c> 등. <b>형상 선언이 근거</b>라
    /// 미선언 옛 데이터는 <c>null</c> 이다.
    /// </summary>
    [JsonProperty("component_type", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
    public string? ComponentType { get; set; }

    /// <summary>
    /// 상태 어휘(대문자) — 예 <c>OPEN</c>·<c>CLOSED</c>·<c>RUNNING</c>.
    /// <b>상태 축이 없는 유형이면 키째 없다</b>(계측·수동 유형) — <c>null</c> 은 "닫힘"이 아니라 "축 없음"이다.
    /// </summary>
    [JsonProperty("state", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    public string? State { get; set; }

    /// <summary>건강 값(대문자) — <c>OK</c>·<c>DEGRADED</c>·<c>FAULT</c>·<c>UNKNOWN</c>.</summary>
    [JsonProperty("health", Order = 6, NullValueHandling = NullValueHandling.Ignore)]
    public string? Health { get; set; }

    /// <summary>
    /// 관측 시각 — 표시 tz · 마이크로초 6자리(D19). <b><c>null</c> 일 수 있다</b> —
    /// 옛 스칼라에서 이관된 행에는 시각이 없고 서버가 지어내지 않는다.
    /// </summary>
    [JsonProperty("observed_at", Order = 7, NullValueHandling = NullValueHandling.Ignore)]
    public string? ObservedAt { get; set; }

    /// <summary>DS-1 대응 복합 키 — <c>id</c> 만으로는 행이 유일하지 않다.</summary>
    [JsonIgnore]
    public string RowKey => $"{Id}/{Component}";
}
