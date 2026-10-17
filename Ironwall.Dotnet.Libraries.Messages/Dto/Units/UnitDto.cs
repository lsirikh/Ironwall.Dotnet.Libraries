using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Units;
/****************************************************************************
   Purpose      : 부대 단건(full) 프로필 DTO — GOP API 8.0 §11-A
   Created By   : Claude
   Created On   : 2026-09-18
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 부대 <b>단건(full)</b> 프로필 = 목록 + 설명 · 인접 · 시각.
/// <c>POST</c>·<c>PATCH</c>·<c>PUT</c> 의 응답 <c>data</c> 가 이 모양이다(배포 스웨거 <c>UnitResponse</c>).
/// </summary>
public class UnitDto : UnitListDto
{
    /// <summary>설명. 없으면 <c>null</c>.</summary>
    [JsonProperty("description", Order = 10, NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    /// <summary>
    /// 인접 부대 id — 서버가 <b>오름차순</b>으로 준다. 인접은 <b>같은 제대끼리만</b>이고
    /// 형제 자동 파생이 아니라 <b>직접 지정</b>이다(대대 경계를 넘는 인접 중대를 표현하려고).
    /// </summary>
    /// <remarks>
    /// ⚠ <c>PUT</c> 으로 이 부대를 교체할 때 <b>이 값을 그대로 실어 보내야 한다</b> —
    /// 생략하면 서버가 인접을 <b>전부 지운다</b>. <c>UnitReplaceDto.FromCurrent</c> 가 그 일을 한다.
    /// </remarks>
    [JsonProperty("adjacent_unit_ids", Order = 11)]
    public List<int> AdjacentUnitIds { get; set; } = new();

    /// <summary>
    /// 생성 일시 — 서버가 <b>offset 이 붙은 ISO 8601</b> 문자열로 준다
    /// (라이브 실측: <c>2026-09-18T08:14:18.631873+09:00</c>).
    /// </summary>
    /// <remarks>
    /// 문자열로 받는다 — 레포 관용구(<c>BaseDto.CreatedAt</c>)와 같고, 마이크로초 6자리·offset 을
    /// 왕복에서 잃지 않는다. 타입이 필요하면 <see cref="CreatedAtOffset"/> 를 쓴다.
    /// </remarks>
    [JsonProperty("created_at", Order = 98, NullValueHandling = NullValueHandling.Ignore)]
    public string? CreatedAt { get; set; }

    /// <summary>수정 일시 — offset 붙은 ISO 8601 문자열.</summary>
    [JsonProperty("updated_at", Order = 99, NullValueHandling = NullValueHandling.Ignore)]
    public string? UpdatedAt { get; set; }

    /// <summary><see cref="CreatedAt"/> 를 타입으로 해석한 값(실패하면 <c>null</c>).</summary>
    [JsonIgnore]
    public DateTimeOffset? CreatedAtOffset => TryParseOffset(CreatedAt);

    /// <summary><see cref="UpdatedAt"/> 를 타입으로 해석한 값(실패하면 <c>null</c>).</summary>
    [JsonIgnore]
    public DateTimeOffset? UpdatedAtOffset => TryParseOffset(UpdatedAt);

    private static DateTimeOffset? TryParseOffset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTimeOffset.TryParse(value,
                                       CultureInfo.InvariantCulture,
                                       DateTimeStyles.None,
                                       out var parsed)
             ? parsed
             : null;
    }
}
