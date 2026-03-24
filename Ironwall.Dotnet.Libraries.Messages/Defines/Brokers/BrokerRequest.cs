using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.Messages.Defines.Brokers;
/****************************************************************************
   Purpose      : Generic Body를 지원하는 요청 메시지 기본 클래스
   Created By   : GHLee
   Created On   : 10/30/2025 3:00:00 PM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// Generic Body를 지원하는 BrokerRequest
/// </summary>
/// <typeparam name="TBody">Data 타입</typeparam>
public class BrokerRequest<TBody> : BaseMessage<TBody> where TBody : class
{
    public BrokerRequest()
    {
        TypeMessage = "REQ";
    }
}

/// <summary>
/// Non-Generic BrokerRequest (하위 호환성을 위해 유지)
/// </summary>
public class RequestMessage : BaseBrokerMessage
{
    public RequestMessage()
    {
        TypeMessage = "REQ";
    }
}
