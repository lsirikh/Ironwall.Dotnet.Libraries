using Ironwall.Dotnet.Libraries.Enums;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Db.Helpers;

/// <summary>
/// DB 행의 문자열 컬럼 → enum 변환. <b>절대 던지지 않는다.</b>
/// <para>
/// 서버 어휘는 클라 enum 보다 앞서 간다(v7.0+ 종류축 <c>SmartController</c>·<c>SPEED_DOME</c>·<c>Sliding</c> 등).
/// <see cref="Enum.Parse{TEnum}(string)"/> 은 그런 값 하나에 목록 전체 조회를 예외로 끝낸다 —
/// 읽기 경로는 미지 어휘를 <see cref="EnumDeviceType.NONE"/> 으로 받아 나머지 행을 살린다.
/// </para>
/// </summary>
public static class DeviceTypeText
{
    /// <summary>장비 타입 문자열 → <see cref="EnumDeviceType"/>. 미지·공백·null·미정의 정수는 <see cref="EnumDeviceType.NONE"/>.</summary>
    public static EnumDeviceType ParseTypeOrNone(string? text)
        => TryParseDefined(text, out EnumDeviceType value) ? value : EnumDeviceType.NONE;

    /// <summary>
    /// 장비 상태 문자열 → <see cref="EnumDeviceStatus"/>. 미지 값은 <see cref="EnumDeviceStatus.DEACTIVATED"/> —
    /// 모르는 상태를 "정상(ACTIVATED)" 으로 보이게 하지 않는다.
    /// </summary>
    public static EnumDeviceStatus ParseStatusOrDeactivated(string? text)
        => TryParseDefined(text, out EnumDeviceStatus value) ? value : EnumDeviceStatus.DEACTIVATED;

    // Enum.TryParse 는 "999" 같은 미정의 정수 문자열도 성공으로 돌려준다 → IsDefined 로 한 번 더 거른다.
    private static bool TryParseDefined<TEnum>(string? text, out TEnum value) where TEnum : struct, Enum
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        return Enum.TryParse(text.Trim(), ignoreCase: true, out value) && Enum.IsDefined(value);
    }
}
