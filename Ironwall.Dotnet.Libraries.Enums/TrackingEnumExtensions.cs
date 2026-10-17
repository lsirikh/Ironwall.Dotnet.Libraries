using System;

namespace Ironwall.Dotnet.Libraries.Enums;
/****************************************************************************
   Purpose      : 추적 타겟 enum 안전 파싱 + 색상 매핑 (와이어 string → enum)
   Created By   : GHLee
   Created On   : 2026-06-25
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// TRACKING_STATUS 와이어 문자열(threat_level/label)을 enum으로 안전 변환하고,
/// 위협 등급을 마커 색상(<see cref="EnumColorType"/>)으로 매핑한다.
/// <para>모든 파싱은 대소문자 무시 + 미매핑 시 <c>Unknown</c> 폴백(예외 없음).</para>
/// </summary>
public static class TrackingEnumExtensions
{
    /// <summary>
    /// 와이어 threat_level(NORMAL/CAUTION/THREAT, 대소문자 무시)을 <see cref="EnumThreatLevel"/>로 파싱.
    /// null/공백/미매핑 → <see cref="EnumThreatLevel.Unknown"/>.
    /// </summary>
    public static EnumThreatLevel ParseThreatLevel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return EnumThreatLevel.Unknown;

        return Enum.TryParse<EnumThreatLevel>(value.Trim(), ignoreCase: true, out var result)
               && result != EnumThreatLevel.Unknown
            ? result
            : EnumThreatLevel.Unknown;
    }

    /// <summary>
    /// 위협 등급 → 마커 색상. NORMAL=Green · CAUTION=Orange · THREAT=Red · Unknown=Gray.
    /// </summary>
    public static EnumColorType ToColorType(this EnumThreatLevel level) => level switch
    {
        EnumThreatLevel.NORMAL  => EnumColorType.Green,
        EnumThreatLevel.CAUTION => EnumColorType.Orange,
        EnumThreatLevel.THREAT  => EnumColorType.Red,
        _                       => EnumColorType.Gray,
    };

    /// <summary>
    /// 와이어 label(person/car/vehicle/animal 등)을 <see cref="EnumTargetType"/>로 파싱.
    /// <para>복합 label(예 <c>armed_person</c>, <c>person near car</c>)은 토큰을 순서대로 스캔해
    /// <b>첫 번째로 인식되는 타입</b>을 채택한다(엄격 split[0] 대신 — 보안상 무장 인원을 Unknown으로 떨구지 않기 위함, D-RENDER 정련).
    /// <c>car</c>·<c>vehicle</c>·<c>truck</c>은 모두 <see cref="EnumTargetType.Vehicle"/>. 인식 토큰 없으면 <see cref="EnumTargetType.Unknown"/>.</para>
    /// </summary>
    public static EnumTargetType ParseTargetType(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return EnumTargetType.Unknown;

        var tokens = label.Trim().ToLowerInvariant()
            .Split(new[] { ' ', '_', '-', ',' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            switch (token)
            {
                case "person":
                case "people":
                case "human":
                    return EnumTargetType.Person;
                case "car":
                case "vehicle":
                case "truck":
                    return EnumTargetType.Vehicle;
                case "animal":
                    return EnumTargetType.Animal;
            }
        }

        return EnumTargetType.Unknown;
    }
}
