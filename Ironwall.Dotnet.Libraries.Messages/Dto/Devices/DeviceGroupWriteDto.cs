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
/// <para><c>unit_id</c> 는 싣지 않는다 — 생략하면 등록은 기본 부대, 수정(PUT·PATCH)은 현재 부대를 유지한다.</para>
/// </remarks>
public sealed class DeviceGroupWriteDto
{
    [JsonProperty("name", Order = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>그룹 설명. <c>null</c> 도 그대로 보낸다 — 편집 칸을 비운 것은 "설명 없음"이다.</summary>
    [JsonProperty("description", Order = 2)]
    public string? Description { get; set; }

    /// <summary>응답/화면 DTO 에서 쓰기 가능한 두 칸만 옮긴다.</summary>
    public static DeviceGroupWriteDto From(DeviceGroupDto dto)
    {
        if (dto == null) throw new System.ArgumentNullException(nameof(dto));
        return new DeviceGroupWriteDto { Name = dto.Name ?? string.Empty, Description = dto.Description };
    }
}
