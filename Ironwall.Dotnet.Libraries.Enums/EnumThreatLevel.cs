using System.ComponentModel;

namespace Ironwall.Dotnet.Libraries.Enums;
/****************************************************************************
   Purpose      : AI 추적 타겟 위협 등급 (TRACKING_STATUS targets[].threat_level)
   Created By   : GHLee
   Created On   : 2026-06-25
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 위협 등급 — AiAnalysis가 탐지 객체별로 산출.
/// <para>와이어 계약(Gop_Message_Broker §4.1)은 <b>NORMAL / CAUTION / THREAT</b> 3종만 발행한다.</para>
/// <para><see cref="Unknown"/>은 <b>클라이언트 전용 폴백</b>(미매핑·누락 값 → 회색 처리)이며 와이어에는 존재하지 않는다.</para>
/// </summary>
public enum EnumThreatLevel
{
    [Description("일반 — 탐지됐으나 위협 아님")]
    NORMAL,

    [Description("경계 — 주의 필요")]
    CAUTION,

    [Description("위협 — 즉각 대응 필요")]
    THREAT,

    [Description("미상 — 클라이언트 폴백(와이어 없음)")]
    Unknown,
}
