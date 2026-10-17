using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Models;
/****************************************************************************
   Purpose      : 축 경로(/config · /component-status) 응답 data DTO (A-devices D-30 · D-31)
   Created By   : GHLee
   Created On   : 9/18/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 서버 7.0 <c>DeviceConfigWriteData</c> — <c>GET·PATCH·PUT /api/devices/{종류}/{id}/config</c> 의 <c>data</c>.
/// </summary>
/// <remarks>
/// <para><c>device_config</c> 는 <b>네 키 골격</b>(<c>schema</c>·<c>modes</c>·<c>thresholds</c>·
/// <c>component_overrides</c>)으로 늘 나온다(CF-8 · R11) — 비어 있으면 <c>{}</c> 이지 키가 빠지지 않는다.
/// 즉 <b>여기서는</b> <c>null</c> 과 "빈 설정"이 구분된다(장비 목록의 <c>view=basic</c> 누락과 다른 점).</para>
/// <para>참조는 <c>{id, category_device}</c> 뿐이다 — 이름·종류축은 싣지 않는다(D5 · P2-6).</para>
/// </remarks>
public class DeviceConfigWriteDataDto
{
    /// <summary>장비 id.</summary>
    [JsonProperty("id", Order = 1)]
    public int Id { get; set; }

    /// <summary>장비 카테고리(<b>단수</b>). 경로 세그먼트로 쓰려면 <see cref="Helpers.DeviceTypePaths.FromCategory"/>.</summary>
    [JsonProperty("category_device", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    public string? CategoryDevice { get; set; }

    /// <summary>의도 축 전체 — 네 키 골격(D10).</summary>
    [JsonProperty("device_config", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public DeviceConfigAxisDto? DeviceConfig { get; set; }
}

/// <summary>
/// 서버 7.0 <c>DeviceStatusWriteData</c> — <c>PATCH /api/devices/{종류}/{id}/component-status</c> 의 <c>data</c>.
/// </summary>
/// <remarks>
/// <para><b>응답은 병합된 축 전체</b>다 — 보고하지 않은 형제 부품이 함께 실린다. 그래서 축을
/// <b>요청 모델이 아니라 응답 모델</b>(<see cref="DeviceStatusAxisDto"/> = <c>DeviceStatusAxisOut</c>)로 받는다:
/// 옛 스칼라에서 이관된 문 위치 부품은 <c>observed_at</c> 이 <b>null</b> 이라, 요청 모델처럼
/// 필수로 다루면 <b>자기 성공 응답(200)을 역직렬화하다 터진다</b>(서버 주석 원문).</para>
/// <para><b>요청 검증은 그대로다</b> — <c>observed_at</c> 없는 보고는 여전히 422 다.</para>
/// </remarks>
public class DeviceStatusWriteDataDto
{
    /// <summary>장비 id.</summary>
    [JsonProperty("id", Order = 1)]
    public int Id { get; set; }

    /// <summary>장비 카테고리(<b>단수</b>).</summary>
    [JsonProperty("category_device", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    public string? CategoryDevice { get; set; }

    /// <summary>관측 축 전체 — <c>{schema, components}</c>(D9). 보고하지 않은 부품도 실린다.</summary>
    [JsonProperty("device_status", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public DeviceStatusAxisDto? DeviceStatus { get; set; }
}
