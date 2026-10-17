using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.RtspMessage.Defines;
/****************************************************************************
   Purpose      : Generic Body를 지원하는 요청 메시지 기본 클래스
   Created By   : GHLee
   Created On   : 10/30/2025 3:00:00 PM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// Generic Body를 지원하는 RequestMessage
/// </summary>
/// <typeparam name="TBody">Body 타입</typeparam>
public class RequestMessage<TBody> : BaseMessage<TBody> where TBody : class
{
    public RequestMessage()
    {
        MType = EnumMessageType.REQ;
    }
}

/// <summary>
/// Non-Generic RequestMessage (하위 호환성을 위해 유지)
/// </summary>
public class RequestMessage : BaseMessage
{
    public RequestMessage()
    {
        MType = EnumMessageType.REQ;
    }
}