using System.ComponentModel;

namespace Ironwall.Dotnet.Libraries.Enums;
/****************************************************************************
   Purpose      : AI 추적 타겟 분류 (TRACKING_STATUS targets[].label 매핑)
   Created By   : GHLee
   Created On   : 2026-06-25
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 추적 타겟 종류 — 심볼 아이콘 선택용.
/// <para>와이어 label(Gop_Message_Broker §8.3.7)은 자유 문자열(person, car, vehicle, animal 등)이며,
/// <c>car</c>·<c>vehicle</c>은 둘 다 <see cref="Vehicle"/>로 매핑한다. 미매핑 값은 <see cref="Unknown"/>.</para>
/// </summary>
public enum EnumTargetType
{
    [Description("사람")]
    Person,

    [Description("차량 (car·vehicle)")]
    Vehicle,

    [Description("동물")]
    Animal,

    [Description("미상")]
    Unknown,
}
