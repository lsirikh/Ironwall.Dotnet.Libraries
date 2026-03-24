using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.RtspMessage.Defines;
/// <summary>
/// 메시지 타입 열거형
/// </summary>
public enum EnumMessageType
{
    /// <summary>
    /// 요청 메시지
    /// </summary>
    REQ = 1,
    /// <summary>
    /// 응답 메시지
    /// </summary>
    RSP = 2,
}

/// <summary>
/// 명령 타입 열거형
/// </summary>
public enum EnumCommandType
{
    NONE = 0,
    /// <summary>
    /// 이벤트 호출 명령
    /// </summary>
    EVENT_CALL = 1,
    /// <summary>
    /// 상태 조회 명령
    /// </summary>
    STATUS_CHECK = 2,
    /// <summary>
    /// 설정 변경 명령
    /// </summary>
    CONFIG_UPDATE = 3
}

/// <summary>
/// 이벤트 상태 열거형
/// </summary>
public enum EnumEventState
{
    /// <summary>
    /// 이벤트 활성화 (팝업 열기)
    /// </summary>
    ON = 0,
    /// <summary>
    /// 이벤트 비활성화 (팝업 닫기)
    /// </summary>
    OFF = 1
}
