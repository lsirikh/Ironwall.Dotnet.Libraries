using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// 장비 그룹 <b>쓰기</b> 본문 — <c>POST /devices/groups</c> · <c>PUT</c> · <c>PATCH /devices/groups/{id}</c>.
/// </summary>
/// <remarks>
/// <para>서버 스키마(<c>DeviceGroupCreate</c> · <c>DeviceGroupUpdate</c>)는 <c>extra="forbid"</c> 이고 받는 키는
/// <c>name</c> · <c>description</c> · <c>unit_id</c> 뿐이다. 응답 DTO(<see cref="DeviceGroupDto"/>)를 그대로 보내면
/// <c>id</c> · <c>device_count</c> · <c>created_at</c>(BaseDto 기본값 = 지금)이 함께 나가 <b>422 UNKNOWN_FIELD</b> 로
/// 등록도 수정도 한 건도 되지 않았다(라이브 실측 2026-09-24, 서버 8.0.2).</para>
/// <para><c>unit_id</c> 는 <b>값이 있을 때만</b> 싣는다(<see cref="UnitId"/>). 생략하면 등록은 서버 기본 부대(<c>unit001</c>),
/// 수정(PUT·PATCH)은 현재 부대를 유지한다. 등록은 호출부(장비 콘솔)가 이 클라이언트의 부대를 채운다 — 비워 두면
/// 그룹이 기본 부대로 가서, 기본 부대의 예하가 아닌 부대의 장비는 그 그룹에 넣을 수 없다
/// (서버 <c>assert_members_in_unit_scope</c> 422, 라이브 하네스 dl.2 2026-09-26). 8.0 미만에서는
/// <c>DeviceApiService</c> 가 지운다(7.0 스키마에 없는 키라 실리면 422).</para>
/// </remarks>
public sealed class DeviceGroupWriteDto
{
    [JsonProperty("name", Order = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>그룹 설명. <c>null</c> 도 그대로 보낸다 — 편집 칸을 비운 것은 "설명 없음"이다.</summary>
    [JsonProperty("description", Order = 2)]
    public string? Description { get; set; }

    /// <summary>소속 부대 id(v8.0). <c>null</c> 이면 키를 싣지 않는다 — "지워라"가 아니라 "건드리지 않음"이다.</summary>
    [JsonProperty("unit_id", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public int? UnitId { get; set; }

    /// <summary>응답/화면 DTO 에서 쓰기 가능한 칸(이름 · 설명 · 소속 부대)만 옮긴다.</summary>
    public static DeviceGroupWriteDto From(DeviceGroupDto dto)
    {
        if (dto == null) throw new System.ArgumentNullException(nameof(dto));
        return new DeviceGroupWriteDto { Name = dto.Name ?? string.Empty, Description = dto.Description, UnitId = dto.UnitId };
    }
}
