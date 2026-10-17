using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using System;

namespace Ironwall.Dotnet.Libraries.Messages.Helpers;

/// <summary>
/// 장비 판별자(<c>category_device</c>)와 옛 종류(<c>type_device</c>) 사이의 해석 — <b>단일 정본</b>.
/// </summary>
/// <remarks>
/// <para>서버는 v7.0 에서 <c>type_device</c> 를 없애고 판별자 + 카테고리별 종류축으로 갈랐다.
/// 운영 6.3.2 는 여전히 <c>type_device</c> 전문을 주고, 7.0+ 는 <c>category_device</c> 만 준다.
/// 클라의 <see cref="EnumDeviceType"/> 은 지도 심볼·3D 하우징·문 개폐·이벤트 큐 키가 소비하는
/// <b>파생값</b>으로 유지한다(device-console-v8 AD-1) — 그 파생을 한 곳에서만 한다.</para>
/// <para>원래 <c>Events.Ui/Helpers/DtoToModelHelper</c> 의 private 함수였다(FR-21, 2026-09-18).
/// Devices.Ui 가 같은 규칙을 다시 쓰지 않도록 DTO 와 같은 어셈블리로 <b>이관</b>했다(복사 아님).</para>
/// </remarks>
public static class DeviceTypeResolver
{
    /// <summary>
    /// <c>device</c> 참조/전문에서 옛 종류를 복원한다. 확정 못 하면 <c>null</c>(=모른다).
    /// </summary>
    public static EnumDeviceType? Resolve(BaseDeviceDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return Resolve(dto.TypeDevice, dto.CategoryDevice);
    }

    /// <summary>
    /// ① <c>type_device</c>(6.3 전문) → ② <c>category_device</c>(7.0+ 참조) 순으로 본다.
    /// </summary>
    /// <remarks>
    /// 종류가 카테고리보다 좁으므로 전문이 읽히면 전문이 이긴다 — 6.3 의 결과는 이관 전과 같다.
    /// 어느 쪽도 못 읽으면 <b>추측하지 않는다</b>. 미지 폴백이 '센서'로 굳는 것이 FR-21 의 버그였다.
    /// </remarks>
    public static EnumDeviceType? Resolve(string? typeDevice, string? categoryDevice)
    {
        // ① 관용 파싱(대소문자·공백 무시). NONE 과 미정의 정수는 미복원 취급.
        if (TryParseDefined(typeDevice, out EnumDeviceType typed) && typed != EnumDeviceType.NONE)
            return typed;

        // ② 카테고리로 복원할 수 있는 것만 복원한다.
        return FromCategory(ParseCategory(categoryDevice));
    }

    /// <summary>
    /// 카테고리 → 옛 종류. <b>1:1 로 확정되는 6개만</b> 옮기고 나머지는 <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <c>sensor</c> 는 <b>의도적으로 매핑하지 않는다</b> — 그 안에 Fence·Multi·PIR·SmartSensor… 가
    /// 전부 들어 있어 어느 하나를 고르면 <b>틀린 종류를 단정</b>하게 된다.
    /// <c>gate</c> 는 7.0+ 가 늘린 값이다(6.3 어휘엔 없음) — 관용 수용이라 양쪽 무해.
    /// </remarks>
    public static EnumDeviceType? FromCategory(EnumDeviceCategory category) => category switch
    {
        EnumDeviceCategory.Controller => EnumDeviceType.Controller,
        EnumDeviceCategory.Camera     => EnumDeviceType.IpCamera,
        EnumDeviceCategory.Speaker    => EnumDeviceType.IpSpeaker,
        EnumDeviceCategory.Enclosure  => EnumDeviceType.Enclosure,
        EnumDeviceCategory.Lamp       => EnumDeviceType.Lamp,
        EnumDeviceCategory.Gate       => EnumDeviceType.Gate,
        _ => null   // Sensor(종류 미상) · None(미지 어휘) · Etc
    };

    /// <summary>서버 판별자 문자열(소문자 7값) → <see cref="EnumDeviceCategory"/>. 미지·공백은 <see cref="EnumDeviceCategory.None"/>.</summary>
    public static EnumDeviceCategory ParseCategory(string? categoryDevice)
        => TryParseDefined(categoryDevice, out EnumDeviceCategory category) ? category : EnumDeviceCategory.None;

    /// <summary>
    /// 옛 종류 → 판별자(역방향). 6.3 응답에는 <c>category_device</c> 가 없으므로 캐시 키·탭 분류를
    /// 세대와 무관하게 같은 축으로 맞출 때 쓴다.
    /// </summary>
    /// <remarks>
    /// 분류는 <c>Monitoring.Models/Helpers/DeviceModelConverter</c> 의 모델 배정과 같다 —
    /// <c>IoController</c> 는 제어기가 아니라 <b>센서 계열</b>이고, 6.3 센서 표에만 나오는
    /// <c>Cable</c>·<c>Fence_Group</c> 도 센서다(v7.0 종류축으로는 승계되지 않는 값이지만 소속은 센서).
    /// </remarks>
    public static EnumDeviceCategory CategoryOf(EnumDeviceType type) => type switch
    {
        EnumDeviceType.NONE       => EnumDeviceCategory.None,
        EnumDeviceType.Controller => EnumDeviceCategory.Controller,
        EnumDeviceType.IpCamera   => EnumDeviceCategory.Camera,
        EnumDeviceType.IpSpeaker  => EnumDeviceCategory.Speaker,
        EnumDeviceType.Enclosure  => EnumDeviceCategory.Enclosure,
        EnumDeviceType.Lamp       => EnumDeviceCategory.Lamp,
        EnumDeviceType.Gate       => EnumDeviceCategory.Gate,
        _ => Enum.IsDefined(type) ? EnumDeviceCategory.Sensor : EnumDeviceCategory.None
    };

    /// <summary>
    /// 판별자를 정한다 — <b>판별자 문자열이 읽히면 그것이 정본</b>(경로가 정한 값),
    /// 없거나 미지면 옛 종류에서 유도한다.
    /// </summary>
    public static EnumDeviceCategory ResolveCategory(string? categoryDevice, string? typeDevice)
    {
        var category = ParseCategory(categoryDevice);
        if (category != EnumDeviceCategory.None)
            return category;

        return TryParseDefined(typeDevice, out EnumDeviceType typed) ? CategoryOf(typed) : EnumDeviceCategory.None;
    }

    // Enum.TryParse 는 "999" 같은 미정의 정수 문자열도 성공으로 돌려준다 → IsDefined 로 한 번 더 거른다.
    private static bool TryParseDefined<TEnum>(string? text, out TEnum value) where TEnum : struct, Enum
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        return Enum.TryParse(text.Trim(), ignoreCase: true, out value) && Enum.IsDefined(value);
    }
}
