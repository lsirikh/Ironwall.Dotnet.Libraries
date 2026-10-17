using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 요청 본문 만들기 — 빈 DTO 에서 짓지 않는다(RFC 7396 사고 방지)
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 보드 행 → 서버 요청 본문. <b>순수 함수</b>다.
/// </summary>
/// <remarks>
/// <para>🔴 <b>빈 DTO 에서 PATCH 본문을 만들지 않는다.</b> RFC 7396 에서 <c>null</c> 은 "지워라" 이고,
/// 서버는 PATCH 를 <c>exclude_unset</c> 으로 병합한다. 빈 객체를 채워 보내면
/// <b>건드리지도 않은 칸이 사라진다</b>. 그래서 여기서는 <b>바뀐 키만</b> 세운다 —
/// 바뀌지 않은 키는 대입조차 하지 않아 <c>ShouldSerialize*</c> 가 본문에서 뺀다.</para>
/// <para>생성 본문은 반대로 <b>전 필드를 명시</b>한다. 기본값에 기대면 서버 기본값이 바뀌었을 때
/// 화면이 보여 주던 값과 저장된 값이 갈린다.</para>
/// </remarks>
public static class MappingRequestBuilder
{
    #region - 매핑 본체 -
    /// <summary>매핑 생성 본문. 빈 문자열은 키째 뺀다(서버가 <c>EMPTY_STRING</c> 422).</summary>
    public static EventMappingCreateDto CreateMapping(string? name, int? groupId, string? category, string? description, bool status)
        => new()
        {
            NameEvent = EventMappingRules.NormalizeText(name) ?? string.Empty,
            DeviceGroupId = groupId,
            CategoryEventMapping = EventMappingRules.IsKnownCategory(category) ? category! : EventMappingRules.CATEGORY_NONE,
            Description = EventMappingRules.NormalizeText(description),
            Status = status,
        };

    /// <summary>
    /// 매핑 부분 수정 본문 — <paramref name="original"/> 과 <b>다른 키만</b> 싣는다.
    /// </summary>
    /// <remarks>
    /// 설명을 지우는 것과 안 건드리는 것을 갈라야 하므로, 편집 결과가 빈 값이고 원본에 값이 있었으면
    /// <c>description: null</c> 을 <b>명시적으로</b> 싣는다. 그 외에는 키를 내지 않는다.
    /// </remarks>
    public static EventMappingUpdateDto PatchMapping(
        EventMappingReadDto original, string? name, int? groupId, string? category, string? description, bool status)
    {
        var body = new EventMappingUpdateDto();

        var newName = EventMappingRules.NormalizeText(name);
        if (newName is not null && !string.Equals(newName, original.NameEvent, StringComparison.Ordinal))
            body.NameEvent = newName;

        if (EventMappingRules.IsKnownCategory(category)
            && !string.Equals(category, original.CategoryEventMapping, StringComparison.Ordinal))
            body.CategoryEventMapping = category;

        if (status != original.Status) body.Status = status;

        if (groupId != original.DeviceGroupId) body.DeviceGroupId = groupId;   // null 대입 = 그룹 해제

        var newDesc = EventMappingRules.NormalizeText(description);
        var oldDesc = EventMappingRules.NormalizeText(original.Description);
        if (!string.Equals(newDesc, oldDesc, StringComparison.Ordinal)) body.Description = newDesc;

        return body;
    }
    #endregion

    #region - 배선 생성(벌크 items[]) -
    /// <summary>카메라 배선 생성 항목.</summary>
    public static MappingCameraCreateDto CreateCamera(MappingBoardRow row, int? priority)
        => new()
        {
            CameraId = row.DeviceId ?? 0,
            TargetPresetId = row.TargetPresetId,
            HomePresetId = row.HomePresetId,
            DelayTime = row.DelayTime,
            IsEnable = row.IsEnable,
            Priority = priority,
        };

    /// <summary>스피커 배선 생성 항목.</summary>
    public static MappingSpeakerCreateDto CreateSpeaker(MappingBoardRow row, int? priority)
        => new()
        {
            SpeakerId = row.DeviceId ?? 0,
            FileGroupId = row.FileGroupId,
            RepeatCount = Math.Max(EventMappingRules.REPEAT_COUNT_MIN, row.RepeatCount),
            IsEnable = row.IsEnable,
            Priority = priority,
        };

    /// <summary>경광등 배선 생성 항목 — <c>priority</c> 는 non-null <c>ge=1</c> 이다.</summary>
    public static MappingLampCreateDto CreateLamp(MappingBoardRow row, int? priority)
        => new()
        {
            LampId = row.DeviceId ?? 0,
            Color = row.Color,
            BuzzerTime = Math.Max(EventMappingRules.BUZZER_TIME_MIN, row.BuzzerTime),
            BuzzerSound = row.BuzzerSound,
            LightMode = row.LightMode,
            IsEnable = row.IsEnable,
            Priority = Math.Max(EventMappingRules.LAMP_PRIORITY_MIN, priority ?? EventMappingRules.LAMP_PRIORITY_MIN),
        };

    /// <summary>종류에 맞는 생성 항목(벌크 <c>items[]</c> 에 그대로 들어간다).</summary>
    public static object Create(MappingBoardRow row, int? priority) => row.Kind switch
    {
        MappingActionKind.Camera => CreateCamera(row, priority),
        MappingActionKind.Speaker => CreateSpeaker(row, priority),
        MappingActionKind.Lamp => CreateLamp(row, priority),
        _ => throw new ArgumentOutOfRangeException(nameof(row)),
    };
    #endregion

    #region - 배선 부분 수정(PATCH) -
    /// <summary>
    /// 카메라 배선 PATCH 본문 — 원본과 다른 키만. <paramref name="priority"/> 가 <c>null</c> 이면 순서 키를 싣지 않는다.
    /// </summary>
    public static MappingCameraUpdateDto PatchCamera(MappingBoardRow row, int? priority)
    {
        var body = new MappingCameraUpdateDto();
        var original = row.OriginalCamera;

        if (row.DeviceId is int deviceId && deviceId > 0 && deviceId != original?.Camera?.Id)
            body.CameraId = deviceId;
        if (original is null || row.DelayTime != original.DelayTime)
            body.DelayTime = row.DelayTime;
        if (original is null || row.IsEnable != original.IsEnable)
            body.IsEnable = row.IsEnable;
        if (row.TargetPresetId != original?.TargetPreset?.Id)
            body.TargetPresetId = row.TargetPresetId;      // null 대입 = 프리셋 해제
        if (row.HomePresetId != original?.HomePreset?.Id)
            body.HomePresetId = row.HomePresetId;
        if (priority is int p) body.Priority = p;

        return body;
    }

    /// <summary>스피커 배선 PATCH 본문.</summary>
    public static MappingSpeakerUpdateDto PatchSpeaker(MappingBoardRow row, int? priority)
    {
        var body = new MappingSpeakerUpdateDto();
        var original = row.OriginalSpeaker;

        if (row.DeviceId is int deviceId && deviceId > 0 && deviceId != original?.Speaker?.Id)
            body.SpeakerId = deviceId;
        if (original is null || row.RepeatCount != original.RepeatCount)
            body.RepeatCount = Math.Max(EventMappingRules.REPEAT_COUNT_MIN, row.RepeatCount);
        if (original is null || row.IsEnable != original.IsEnable)
            body.IsEnable = row.IsEnable;
        if (row.FileGroupId != original?.FileGroup?.Id)
            body.FileGroupId = row.FileGroupId;            // null 대입 = 음원 해제
        if (priority is int p) body.Priority = p;

        return body;
    }

    /// <summary>경광등 배선 PATCH 본문 — 이 축에는 "의미 있는 null" 이 없다.</summary>
    public static MappingLampUpdateDto PatchLamp(MappingBoardRow row, int? priority)
    {
        var body = new MappingLampUpdateDto();
        var original = row.OriginalLamp;

        if (row.DeviceId is int deviceId && deviceId > 0 && deviceId != original?.Lamp?.Id)
            body.LampId = deviceId;
        if (original is null || row.Color != original.Color) body.Color = row.Color;
        if (original is null || row.BuzzerTime != original.BuzzerTime) body.BuzzerTime = row.BuzzerTime;
        if (original is null || row.BuzzerSound != original.BuzzerSound) body.BuzzerSound = row.BuzzerSound;
        if (original is null || row.LightMode != original.LightMode) body.LightMode = row.LightMode;
        if (original is null || row.IsEnable != original.IsEnable) body.IsEnable = row.IsEnable;
        if (priority is int p) body.Priority = Math.Max(EventMappingRules.LAMP_PRIORITY_MIN, p);

        return body;
    }

    /// <summary>종류에 맞는 PATCH 본문. 보낼 키가 하나도 없으면 <c>null</c> — 호출을 생략하라는 뜻이다.</summary>
    public static object? Patch(MappingBoardRow row, int? priority)
    {
        switch (row.Kind)
        {
            case MappingActionKind.Camera:
                var camera = PatchCamera(row, priority);
                return camera.IsEmpty ? null : camera;
            case MappingActionKind.Speaker:
                var speaker = PatchSpeaker(row, priority);
                return speaker.IsEmpty ? null : speaker;
            case MappingActionKind.Lamp:
                var lamp = PatchLamp(row, priority);
                return lamp.IsEmpty ? null : lamp;
            default:
                throw new ArgumentOutOfRangeException(nameof(row));
        }
    }
    #endregion
}
