using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
/****************************************************************************
   Purpose      : 이벤트 맵핑 하위 배선 벌크 등록 / 해제 DTO — GOP API v8.0.1 §7.3.9~7.5.11
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 벌크 등록 요청 본문 — <c>POST …/{mapping_id}/{kind}/bulk</c> 의 <c>{"items":[…]}</c>
/// (<c>app/schemas/integration.py:526-535</c> 외 2종. 3리소스 모두 같은 모양).
/// </summary>
/// <typeparam name="T">
/// <see cref="MappingCameraCreateDto"/> · <see cref="MappingSpeakerCreateDto"/> · <see cref="MappingLampCreateDto"/>
/// </typeparam>
/// <remarks>
/// <c>items</c> 는 <b>1~100건</b>이다. 0건이면 422 라 <b>호출 자체를 생략</b>하고,
/// 100건을 넘으면 <see cref="EventMappingRules.CHUNK_SIZE"/> 로 잘라 여러 번 보낸다.
/// </remarks>
public class MappingBulkCreateRequestDto<T>
{
    /// <summary>등록할 배선 행 배열(1~100).</summary>
    [JsonProperty("items", Order = 1)]
    public List<T> Items { get; set; } = new();
}

/// <summary>
/// 벌크 해제 요청 본문 — <c>DELETE …/{mapping_id}/{kind}</c> 의 <c>{"config_ids":[…]}</c>
/// (<c>app/schemas/integration.py:594-600</c> 외 2종).
/// </summary>
/// <remarks>
/// 🔴 여기 들어가는 것은 <b>배선 행 PK</b>(<c>event_mapping_cameras.id</c>)이지
/// <b>장비 PK 가 아니다</b>. 장비 id 를 넣으면 조용히 <c>not_found_config_ids</c> 로 되돌아온다.
/// </remarks>
public class MappingBulkUnassignRequestDto
{
    /// <summary>해제할 배선 행 PK 배열(1~100, 서버가 중복 제거).</summary>
    [JsonProperty("config_ids", Order = 1)]
    public List<int> ConfigIds { get; set; } = new();
}

/// <summary>
/// 벌크 등록에서 <b>행 단위로 실패</b>한 항목 — <c>{index, item, error}</c>
/// (<c>app/schemas/integration.py:545-556</c>).
/// </summary>
/// <remarks>
/// <c>item</c> 은 <b>보낸 입력 원본의 에코</b>다. 리소스마다 모양이 달라 강타입으로 받지 않고
/// <see cref="JObject"/> 로 둔다 — 화면은 <see cref="Error"/> 만 쓰고 원본은 진단 로그로만 흘린다.
/// </remarks>
public class MappingBulkFailedItemDto
{
    /// <summary>요청 <c>items[]</c> 안의 0-기반 위치.</summary>
    [JsonProperty("index", Order = 1)]
    public int Index { get; set; }

    /// <summary>실패한 입력 행 원본(에코).</summary>
    [JsonProperty("item", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    public JObject? Item { get; set; }

    /// <summary>
    /// 실패 사유 — <b>서버 원문(영문)이다</b>. 사용자에게 그대로 보이면 안 되고,
    /// 화면은 한국어 라벨을 붙이고 원문은 툴팁·로그로 내린다.
    /// </summary>
    [JsonProperty("error", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public string? Error { get; set; }
}

/// <summary>
/// 벌크 등록 응답 <c>data</c> — 3리소스 키 집합이 동일하다
/// (<c>app/schemas/integration.py:559-587</c> · <c>:673-701</c> · <c>:799-827</c>).
/// </summary>
/// <remarks>
/// <para>🔴 <b><c>success:true</c> 만 보면 안 된다.</b> 벌크는 <b>부분 성공</b>이라 실패가 섞여도 200 OK 다.
/// 판정은 <see cref="IsCompleteFor"/> 로 한다.</para>
/// <para><c>skipped_config_ids</c> 는 <b>이미 등록돼 건너뛴 기존 행의 PK</b> 다 — <b>실패가 아니다</b>.
/// 해제 응답에서는 같은 이름이 "다른 매핑 소속이라 건드리지 않음" 을 뜻한다. 의미가 달라서
/// 등록/해제 결과 타입을 <b>나눠 둔다</b>(DF-11).</para>
/// <para><c>not_found_config_ids</c> 는 등록에서 <b>장비 PK</b>, 해제에서 <b>배선 행 PK</b> 다. 역시 축이 다르다.</para>
/// <para>경광등 <c>failed_items</c> 는 서버가 <b>항상 빈 목록</b>으로 둔다(<c>integration.py:794</c>) —
/// "경광등은 실패가 없다" 가 아니라 <b>행 단위 사유를 주지 않는다</b>는 뜻이다.</para>
/// </remarks>
public class MappingBulkCreateResultDto
{
    /// <summary>대상 매핑 PK.</summary>
    [JsonProperty("mapping_id", Order = 1)]
    public int MappingId { get; set; }

    /// <summary>INSERT 에 성공한 <b>배선 행 PK</b> 목록(요청 순서 보존).</summary>
    [JsonProperty("created_ids", Order = 2)]
    public List<int>? CreatedIds { get; set; }

    /// <summary>행 단위 실패 목록. 스피커·경광등 응답에는 비어 있을 수 있다.</summary>
    [JsonProperty("failed_items", Order = 3)]
    public List<MappingBulkFailedItemDto>? FailedItems { get; set; }

    /// <summary>이미 매핑돼 있어 건너뛴 <b>기존 배선 행 PK</b>. 실패가 아니다.</summary>
    [JsonProperty("skipped_config_ids", Order = 4)]
    public List<int>? SkippedConfigIds { get; set; }

    /// <summary>존재하지 않는 <b>장비 PK</b>(등록 경로 기준).</summary>
    [JsonProperty("not_found_config_ids", Order = 5)]
    public List<int>? NotFoundConfigIds { get; set; }

    /// <summary>서버 요약 문구 — <b>화면에 그대로 쓰지 않는다</b>(셈법이 리소스마다 다르다). 로그용.</summary>
    [JsonProperty("message", Order = 6, NullValueHandling = NullValueHandling.Ignore)]
    public string? Message { get; set; }

    /// <summary>성공 처리된 건수 = 새로 만든 것 + 이미 있어 건너뛴 것.</summary>
    [JsonIgnore]
    public int SettledCount => (CreatedIds?.Count ?? 0) + (SkippedConfigIds?.Count ?? 0);

    /// <summary>행 단위 실패 건수(경광등은 서버가 사유를 주지 않아 0 으로 보일 수 있다).</summary>
    [JsonIgnore]
    public int FailedCount => FailedItems?.Count ?? 0;

    /// <summary>존재하지 않는 장비로 거절된 건수.</summary>
    [JsonIgnore]
    public int NotFoundCount => NotFoundConfigIds?.Count ?? 0;

    /// <summary>
    /// 보낸 <paramref name="requestedCount"/> 건이 <b>남김없이</b> 정착했는가.
    /// </summary>
    /// <remarks>
    /// 배열 길이로 <b>클라가 직접 센다</b> — 서버 <c>message</c> 의 "N개 중복" 셈법이
    /// 카메라(요청 항목 수)와 스피커·경광등(<c>skipped</c> 길이)에서 다르기 때문이다.
    /// </remarks>
    public bool IsCompleteFor(int requestedCount)
        => FailedCount == 0 && NotFoundCount == 0 && SettledCount >= requestedCount;
}

/// <summary>
/// 벌크 해제 응답 <c>data</c> — <b>키 이름이 등록과 다르다</b>
/// (<c>app/schemas/integration.py:610-634</c> 외 2종).
/// </summary>
/// <remarks>
/// 지워진 목록의 키는 <c>deleted_ids</c> 가 <b>아니라</b> <c>removed_config_ids</c> 다.
/// 등록 결과 타입을 재사용하면 이 목록이 통째로 <c>null</c> 이 되어 "0건 해제" 로 오독된다(DF-11).
/// </remarks>
public class MappingBulkUnassignResultDto
{
    /// <summary>대상 매핑 PK.</summary>
    [JsonProperty("mapping_id", Order = 1)]
    public int MappingId { get; set; }

    /// <summary>실제로 지워진 배선 행 PK 목록.</summary>
    [JsonProperty("removed_config_ids", Order = 2)]
    public List<int>? RemovedConfigIds { get; set; }

    /// <summary>행은 있으나 <b>다른 매핑 소속</b>이라 건드리지 않은 PK(멱등성 보장).</summary>
    [JsonProperty("skipped_config_ids", Order = 3)]
    public List<int>? SkippedConfigIds { get; set; }

    /// <summary>DB 에 행 자체가 없는 PK(404 가 아니라 분류 응답).</summary>
    [JsonProperty("not_found_config_ids", Order = 4)]
    public List<int>? NotFoundConfigIds { get; set; }

    /// <summary>서버 요약 문구 — 로그용.</summary>
    [JsonProperty("message", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    public string? Message { get; set; }

    /// <summary>실제로 지워진 건수.</summary>
    [JsonIgnore]
    public int RemovedCount => RemovedConfigIds?.Count ?? 0;

    /// <summary>다른 매핑 소속이라 건너뛴 건수.</summary>
    [JsonIgnore]
    public int SkippedCount => SkippedConfigIds?.Count ?? 0;

    /// <summary>행이 없어 건너뛴 건수 — 이미 지워진 것이므로 해제 목적상 <b>성공</b>으로 친다.</summary>
    [JsonIgnore]
    public int NotFoundCount => NotFoundConfigIds?.Count ?? 0;

    /// <summary>
    /// 보낸 <paramref name="requestedCount"/> 건이 <b>더는 이 매핑에 남아 있지 않은가</b>.
    /// </summary>
    /// <remarks>
    /// "행이 없음"(<c>not_found</c>)은 해제 목적상 달성이다. 반면 "다른 매핑 소속"(<c>skipped</c>)은
    /// <b>내가 지우려던 것을 못 지운 것</b>이라 달성이 아니다 — 이 비대칭이 등록 쪽과 반대다.
    /// </remarks>
    public bool IsCompleteFor(int requestedCount)
        => SkippedCount == 0 && RemovedCount + NotFoundCount >= requestedCount;
}
