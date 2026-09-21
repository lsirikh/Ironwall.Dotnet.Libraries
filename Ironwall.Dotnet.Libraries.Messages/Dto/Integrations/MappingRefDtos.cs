using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
/****************************************************************************
   Purpose      : 이벤트 맵핑 하위 응답의 nested 참조 객체 — GOP API v8.0.1 §7.3~7.5
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 하위 배선 응답이 물고 오는 <b>장비 참조</b> — 서버는 <c>{id, category_device}</c> <b>두 키만</b> 준다
/// (<c>app/schemas/event.py:72,87-91</c> <c>DeviceReference</c>).
/// </summary>
/// <remarks>
/// <para>🔴 <b>이름이 없다.</b> v7.0 에서 연동 응답의 <c>camera</c>·<c>speaker</c>·<c>lamp</c> 가 2키로 축소됐다.
/// 화면에 장비 이름을 그리려면 <b>장비 캐시 조인이 선행 조건</b>이다 — 캐시에 없으면 <c>#id</c> 로 적는다.</para>
/// <para>장비가 지워지면 서버는 CASCADE 가 아니라 <b>SET NULL</b> 이라 이 객체 자체가 <c>null</c> 로 온다(고아 행).
/// 그래서 이 참조를 담는 쪽 프로퍼티는 <b>반드시 nullable</b> 이다 — 결함 D2·D7 의 정면 대응.</para>
/// </remarks>
public class MappingDeviceRefDto
{
    /// <summary>장비 id — v8.0 에서 <b>전역 유일</b>하다.</summary>
    [JsonProperty("id", Order = 1)]
    public int Id { get; set; }

    /// <summary>장비 카테고리 문자열(<c>Camera</c>·<c>Speaker</c>·<c>Lamp</c> 등). 서버가 주는 유일한 두 번째 키.</summary>
    [JsonProperty("category_device", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    public string? CategoryDevice { get; set; }
}

/// <summary>
/// 카메라 프리셋 참조(<c>target_preset</c>·<c>home_preset</c>) — <c>app/schemas/integration.py:164-181</c>.
/// </summary>
/// <remarks>
/// <para><see cref="CameraId"/> 가 있는 것이 중요하다 — 프리셋은 <b>그 카메라 소유</b>여야 하고,
/// 아니면 서버가 422(<c>VALUE_NOT_ALLOWED</c>)를 낸다(<c>app/routers/event_mapping_cameras.py:84-95</c>).
/// 화면은 이 값으로 <b>드롭 전에</b> 오배정을 막는다.</para>
/// </remarks>
public class MappingPresetRefDto
{
    /// <summary>프리셋 PK.</summary>
    [JsonProperty("id", Order = 1)]
    public int Id { get; set; }

    /// <summary>이 프리셋을 소유한 카메라 id. 소속 검증의 근거.</summary>
    [JsonProperty("camera_id", Order = 2)]
    public int CameraId { get; set; }

    /// <summary>카메라 이름(서버가 조인해 준다 — 프리셋 응답에는 이름이 있다).</summary>
    [JsonProperty("camera_name", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public string? CameraName { get; set; }

    /// <summary>프리셋 번호.</summary>
    [JsonProperty("preset_index", Order = 4)]
    public int PresetIndex { get; set; }

    /// <summary>프리셋 이름. ⚠ 키는 <c>name</c> 이 아니라 <c>preset_name</c> 이다.</summary>
    [JsonProperty("preset_name", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    public string? PresetName { get; set; }

    /// <summary>투어링 시간(초).</summary>
    [JsonProperty("touring_time", Order = 6)]
    public int TouringTime { get; set; }

    /// <summary>
    /// 감시금지구역 여부. <c>true</c> 면 화면이 🔒 와 "일반 팝업·녹화에서 제외됩니다" 를 붙인다.
    /// </summary>
    [JsonProperty("is_restricted_zone", Order = 7)]
    public bool IsRestrictedZone { get; set; }
}

/// <summary>
/// 음원그룹 참조(<c>file_group</c>) — <c>app/schemas/integration.py:184-194</c>.
/// </summary>
public class MappingFileGroupRefDto
{
    /// <summary>음원그룹 PK — 요청의 <c>file_group_id</c> 가 가리키는 값.</summary>
    [JsonProperty("id", Order = 1)]
    public int Id { get; set; }

    /// <summary>소속 서버 id.</summary>
    [JsonProperty("server_id", Order = 2)]
    public int ServerId { get; set; }

    /// <summary>방송 장비 쪽 그룹 번호(<c>id</c> 와 다른 축이다).</summary>
    [JsonProperty("group_id", Order = 3)]
    public int GroupId { get; set; }

    /// <summary>표시 이름. ⚠ 키는 <c>name</c> 이 아니라 <c>group_name</c> 이다.</summary>
    [JsonProperty("group_name", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
    public string? GroupName { get; set; }

    /// <summary>음원 파일 목록(없으면 키가 빠지거나 <c>null</c>).</summary>
    [JsonProperty("files", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? Files { get; set; }
}

/// <summary>
/// 부모 매핑 참조 — <b>경광등 응답에만</b> 실린다(<c>app/schemas/integration.py:197-205, :491</c>).
/// </summary>
/// <remarks>
/// 🔴 <b>3종이 대칭이 아니다.</b> 카메라·스피커 응답은 스칼라 <c>event_mapping_id: int</c> 인데
/// (<c>integration.py:272</c>·<c>:381</c>) 경광등만 <c>event_mapping{}</c> 객체다(<c>:491</c>).
/// 한 타입으로 뭉개면 경광등의 부모 id 가 0 으로 조용히 읽힌다.
/// </remarks>
public class MappingParentRefDto
{
    /// <summary>부모 매핑 PK.</summary>
    [JsonProperty("id", Order = 1)]
    public int Id { get; set; }

    /// <summary>부모 매핑 이름.</summary>
    [JsonProperty("name_event", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    public string? NameEvent { get; set; }

    /// <summary>부모 매핑 카테고리(와이어 문자열).</summary>
    [JsonProperty("category_event_mapping", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public string? CategoryEventMapping { get; set; }
}
