using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// Speaker(방송 장비) 디바이스 DTO (§5.4)
/// </summary>
public class SpeakerDeviceDto : BaseDeviceDto
{
    public SpeakerDeviceDto()
    {
        TypeDevice = "IpSpeaker";
    }

    /// <summary>
    /// 스피커 타입 (EnumSpeakerType: NORMAL, ADMIN, MONITOR, DEV)
    /// </summary>
    [JsonProperty("speaker_type", Order = 11)]
    public string SpeakerType { get; set; } = "NORMAL";

    /// <summary>
    /// 장비 설명
    /// </summary>
    [JsonProperty("description", Order = 12)]
    public string? Description { get; set; }

    /// <summary>
    /// 방송서버 참조 (read-only, nested) — 응답 역직렬화 전용. 쓰기는 <see cref="ServerId"/> 사용.
    /// </summary>
    [JsonProperty("server", Order = 13)]
    public ServerDto? Server { get; set; }

    /// <summary>
    /// 방송서버 ID (write) — SpeakerCreate/Update.server_id. 해제 미지원이라 null이면 직렬화 생략.
    /// </summary>
    [JsonProperty("server_id", Order = 14, NullValueHandling = NullValueHandling.Ignore)]
    public int? ServerId { get; set; }

    /// <summary>요청 본문에 nested server를 직렬화하지 않음(서버는 server_id를 기대 + 민감정보 누출 방지).</summary>
    public bool ShouldSerializeServer() => false;

    #region - 7.0 축(axis) 쓰기 투영 (FR-09) -
    /// <summary>
    /// 7.0 <c>speaker_role</c>(<c>EnumSpeakerRole</c> = NORMAL·ADMIN·MONITOR·DEV) —
    /// 6.3 <c>speaker_type</c> 의 <b>개명</b>이다. 두 열거값 집합이 완전히 같아 1:1 로 옮긴다.
    /// </summary>
    /// <remarks>
    /// 7.0 은 스피커를 2축으로 쪼갰다 — 역할(<c>speaker_role</c>)과 형상(<c>type_speaker</c>:
    /// Horn·Pillar·Unknown). 6.3 에는 형상 축이 <b>아예 없어</b> 유도할 근거가 없으므로
    /// <c>type_speaker</c> 는 보내지 않는다(서버 기본값 <c>Unknown</c>).
    /// </remarks>
    [JsonProperty("speaker_role", Order = 30, NullValueHandling = NullValueHandling.Ignore)]
    public string? SpeakerRoleAxis
    {
        get => DeviceAxisWrite.NullIfEmpty(SpeakerType);
        set { if (DeviceAxisWrite.NullIfEmpty(value) is { } v) SpeakerType = v; }
    }

    public bool ShouldSerializeSpeakerRoleAxis() => UseAxisWrite;

    /// <summary>
    /// 7.0 <c>type_speaker</c>(<c>EnumSpeakerShape</c> = Horn·Pillar·Unknown) — <b>하우징 형상</b> 축.
    /// </summary>
    /// <remarks>
    /// 6.3 에는 형상 축이 <b>아예 없어</b> 유도할 근거가 없다(서버도 "백필 불가 — DB 에 형상 정보가 없다").
    /// 그래서 값이 없으면 보내지 않고 서버 기본값 <c>Unknown</c> 에 맡긴다. 응답은 필수 키라 항상 채워지고,
    /// 운용자가 입력하면 이 속성으로 실어 보낸다. "현장 미확인"은 <c>null</c> 이 아니라 <c>Unknown</c> 이다.
    /// </remarks>
    [JsonProperty("type_speaker", Order = 31, NullValueHandling = NullValueHandling.Ignore)]
    public string? TypeSpeaker { get; set; }

    public bool ShouldSerializeTypeSpeaker() => UseAxisWrite && TypeSpeaker != null;

    /// <summary>7.0+ <c>hardware_spec</c> — 6.3 쓰기 스키마에 없어 축 모드에서만 전송한다(펌웨어 읽기 경로).</summary>
    [JsonProperty("hardware_spec", Order = 32, NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public HardwareSpecDto? HardwareSpec
    {
        get => HardwareSpecCore;
        set => HardwareSpecCore = value;
    }

    public bool ShouldSerializeHardwareSpec() => ShouldSerializeHardwareSpecCore();

    /// <summary>7.0 에서 <c>speaker_role</c> 로 이관된 키(<c>_legacy.py:99</c>) — 축 모드에선 미전송.</summary>
    public bool ShouldSerializeSpeakerType() => !UseAxisWrite;
    #endregion
}
