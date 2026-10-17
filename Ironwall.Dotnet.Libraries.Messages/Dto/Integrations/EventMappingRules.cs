using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
/****************************************************************************
   Purpose      : 이벤트 맵핑 계약 규칙(GOP API v8.0.1 §7.2~7.5) — 지역 검증·정규화
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 요청을 <b>보내기 전에</b> 검사·정규화하는 순수 함수 모음.
/// </summary>
/// <remarks>
/// <para><b>왜 클라에 두는가</b> — 서버가 권위이지만 이 규칙들은 전부 422 를 되돌려주는 규칙이고,
/// 그 422 는 사용자에게 "왜 안 되는지 모르겠는 실패" 로 보인다. 네트워크에 나가기 전에 막을 수 있는 것은 여기서 막는다.</para>
/// <para><b>서버가 권위인 것은 건드리지 않는다</b> — 장비·프리셋·그룹의 실재, 중복 여부, RBAC.
/// 여기서는 <b>알고 있는 값만으로 판정 가능한 것</b>만 본다.</para>
/// </remarks>
public static class EventMappingRules
{
    #region - 닫힌 집합 -
    /// <summary>미정의 카테고리(기본값).</summary>
    public const string CATEGORY_NONE = "NONE";

    /// <summary>
    /// <c>category_event_mapping</c> 의 닫힌 <b>9값</b>(<c>app/utils/enums.py:340-360</c>).
    /// </summary>
    /// <remarks>
    /// 레포의 <c>EnumEventCategory</c> 는 <b>8값</b>이라 <c>OPERATION_ONLY</c> 를 표현하지 못한다.
    /// 그 enum 은 이 노드 범위 밖(<c>Ironwall.Dotnet.Libraries.Enums</c>)이므로 고치지 않고,
    /// 와이어 정본을 여기에 문자열로 둔다.
    /// </remarks>
    public static readonly IReadOnlyList<string> CATEGORIES = new[]
    {
        CATEGORY_NONE,
        "FENCE_SENSOR_ONLY",
        "FENCE_SENSOR_WITH_MULTI_SENSOR",
        "MULTI_SENSOR_ONLY",
        "SENSOR_WITH_CAMERA",
        "SENSOR_WITH_AI_CAMERA",
        "AI_CAMERA_ONLY",
        "CAMERA_ONLY",
        "OPERATION_ONLY",
    };

    private static readonly Dictionary<string, string> _categoryLabels = new(StringComparer.Ordinal)
    {
        [CATEGORY_NONE] = "미정의",
        ["FENCE_SENSOR_ONLY"] = "펜스센서 단독",
        ["FENCE_SENSOR_WITH_MULTI_SENSOR"] = "펜스센서 + 멀티센서",
        ["MULTI_SENSOR_ONLY"] = "멀티센서 단독",
        ["SENSOR_WITH_CAMERA"] = "센서 + 카메라",
        ["SENSOR_WITH_AI_CAMERA"] = "센서 + AI 카메라",
        ["AI_CAMERA_ONLY"] = "AI 카메라 단독",
        ["CAMERA_ONLY"] = "카메라 단독",
        ["OPERATION_ONLY"] = "운영 이벤트 전용",
    };

    /// <summary>
    /// 카테고리 와이어 값의 한국어 표기. 모르는 값이면 <b>원값을 그대로</b> 돌려준다 —
    /// 서버가 값을 하나 더 늘렸을 때 화면이 빈칸이 되는 것보다 낫다.
    /// </summary>
    public static string CategoryLabel(string? wire)
    {
        if (string.IsNullOrWhiteSpace(wire)) return _categoryLabels[CATEGORY_NONE];
        return _categoryLabels.TryGetValue(wire, out var label) ? label : wire;
    }

    /// <summary>그 값이 서버가 받는 9값 안에 있는가.</summary>
    public static bool IsKnownCategory(string? wire)
        => !string.IsNullOrWhiteSpace(wire) && _categoryLabels.ContainsKey(wire);
    #endregion

    #region - 길이·범위 -
    /// <summary>이벤트 이름 최소 길이 — 0 이면 422(<c>min_length=1</c>).</summary>
    public const int NAME_MIN_LENGTH = 1;

    /// <summary>이벤트 이름 최대 길이.</summary>
    public const int NAME_MAX_LENGTH = 100;

    /// <summary>설명 최대 길이.</summary>
    public const int DESCRIPTION_MAX_LENGTH = 500;

    /// <summary>대기 시간 최소값(<c>ge=0</c>).</summary>
    public const int DELAY_TIME_MIN = 0;

    /// <summary>반복 횟수 최소값 — <b><c>ge=1</c></b>. 0 을 보내면 422 다.</summary>
    public const int REPEAT_COUNT_MIN = 1;

    /// <summary>부저 지속 기본값(초).</summary>
    public const int BUZZER_TIME_DEFAULT = 5;

    /// <summary>부저 지속 최소값(<c>ge=0</c>).</summary>
    public const int BUZZER_TIME_MIN = 0;

    /// <summary>카메라·스피커 우선순위 최소값(<c>ge=0</c>, nullable).</summary>
    public const int PRIORITY_MIN = 0;

    /// <summary>경광등 우선순위 최소값 — <b><c>ge=1</c>, non-null</b>.</summary>
    public const int LAMP_PRIORITY_MIN = 1;

    /// <summary>벌크 한 번에 실을 수 있는 최대 건수. 넘으면 나눠 보낸다.</summary>
    public const int CHUNK_SIZE = 100;
    #endregion

    #region - 정규화 -
    /// <summary>
    /// 텍스트를 <b>trim</b> 하고, 남은 것이 없으면 <c>null</c> 로 만든다.
    /// </summary>
    /// <remarks>
    /// 서버는 빈 문자열을 <b>422(<c>EMPTY_STRING</c>)</b> 로 거절한다 — 키를 빼는 것과 빈 문자열을 보내는 것은 다르다.
    /// </remarks>
    public static string? NormalizeText(string? value)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    /// <summary>
    /// 목록을 <see cref="CHUNK_SIZE"/> 단위로 자른다. <b>빈 입력이면 조각이 하나도 없다</b> —
    /// 0건 벌크 호출은 422 라 호출 자체를 생략해야 하기 때문이다.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<T>> Chunk<T>(IReadOnlyList<T>? items, int size = CHUNK_SIZE)
    {
        if (items is null || items.Count == 0) return Array.Empty<IReadOnlyList<T>>();
        if (size < 1) throw new ArgumentOutOfRangeException(nameof(size));

        var result = new List<IReadOnlyList<T>>();
        for (var i = 0; i < items.Count; i += size)
            result.Add(items.Skip(i).Take(size).ToList());
        return result;
    }
    #endregion

    #region - 검증 -
    /// <summary>
    /// 매핑 본체가 <b>보낼 수 있는 모양</b>인가. 실패 사유는 사용자에게 그대로 보여도 되는 한국어다.
    /// </summary>
    public static bool TryValidateMapping(string? nameEvent, string? category, string? description, out string? error)
    {
        var name = NormalizeText(nameEvent);
        if (name is null)
        {
            error = "이벤트 이름을 입력하십시오.";
            return false;
        }
        if (name.Length > NAME_MAX_LENGTH)
        {
            error = $"이벤트 이름은 {NAME_MAX_LENGTH}자까지입니다(지금 {name.Length}자).";
            return false;
        }
        if (!IsKnownCategory(category))
        {
            error = "이벤트 카테고리를 목록에서 고르십시오.";
            return false;
        }
        var desc = NormalizeText(description);
        if (desc is not null && desc.Length > DESCRIPTION_MAX_LENGTH)
        {
            error = $"설명은 {DESCRIPTION_MAX_LENGTH}자까지입니다(지금 {desc.Length}자).";
            return false;
        }
        error = null;
        return true;
    }

    /// <summary>카메라 배선 값이 서버 제약을 만족하는가.</summary>
    public static bool TryValidateCamera(int delayTime, int? priority, out string? error)
    {
        if (delayTime < DELAY_TIME_MIN)
        {
            error = "지연 시간은 0초 이상이어야 합니다.";
            return false;
        }
        if (priority is int p && p < PRIORITY_MIN)
        {
            error = "카메라 우선순위는 0 이상이어야 합니다.";
            return false;
        }
        error = null;
        return true;
    }

    /// <summary>스피커 배선 값이 서버 제약을 만족하는가.</summary>
    public static bool TryValidateSpeaker(int repeatCount, int? priority, out string? error)
    {
        if (repeatCount < REPEAT_COUNT_MIN)
        {
            error = "반복 횟수는 1회 이상이어야 합니다.";
            return false;
        }
        if (priority is int p && p < PRIORITY_MIN)
        {
            error = "스피커 우선순위는 0 이상이어야 합니다.";
            return false;
        }
        error = null;
        return true;
    }

    /// <summary>경광등 배선 값이 서버 제약을 만족하는가.</summary>
    public static bool TryValidateLamp(int buzzerTime, int priority, out string? error)
    {
        if (buzzerTime < BUZZER_TIME_MIN)
        {
            error = "부저 시간은 0초 이상이어야 합니다.";
            return false;
        }
        if (priority < LAMP_PRIORITY_MIN)
        {
            error = "경광등 우선순위는 1 이상이어야 합니다.";
            return false;
        }
        error = null;
        return true;
    }

    /// <summary>
    /// 프리셋이 <b>그 카메라 소유</b>인가. 아니면 서버가 422(<c>VALUE_NOT_ALLOWED</c>)를 낸다.
    /// </summary>
    /// <remarks>이 규칙은 서버 문서에 없고 <b>코드에만</b> 있다(<c>event_mapping_cameras.py:84-95</c>).</remarks>
    public static bool IsPresetOwnedBy(int? presetCameraId, int? cameraId)
        => presetCameraId is null || cameraId is null || presetCameraId.Value == cameraId.Value;
    #endregion
}
