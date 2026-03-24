using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.Messages.Defines.Brokers;
/****************************************************************************
   Purpose      : Generic Body를 지원하는 응답 메시지
   Created By   : GHLee
   Created On   : 10/30/2025 3:00:00 PM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// Generic Body를 지원하는 BrokerResponse
/// </summary>
/// <typeparam name="TBody">Data 타입</typeparam>
public class BrokerResponse<TBody> : BaseMessage<TBody> where TBody : class
{
    public BrokerResponse()
    {
        TypeMessage = "RSP";
        RequestId = string.Empty;
        Message = string.Empty;
    }

    /// <summary>
    /// 원본 요청 메시지 ID
    /// </summary>
    [JsonProperty(Order = 7, PropertyName = "req_id")]
    public string RequestId { get; set; }

    /// <summary>
    /// 처리 성공 여부
    /// </summary>
    [JsonProperty(Order = 8, PropertyName = "success")]
    public bool Success { get; set; }

    /// <summary>
    /// 응답 메시지
    /// </summary>
    [JsonProperty(Order = 9, PropertyName = "message")]
    public string Message { get; set; }
}

/// <summary>
/// Non-Generic BrokerResponse (Data 없는 단순 응답용)
/// </summary>
public class ResponseMessage : BaseBrokerMessage
{
    public ResponseMessage()
    {
        TypeMessage = "RSP";
        ReqId = string.Empty;
        Message = string.Empty;
    }

    /// <summary>
    /// 원본 요청 메시지 ID
    /// </summary>
    [JsonProperty(Order = 6, PropertyName = "req_id")]
    public string ReqId { get; set; }

    /// <summary>
    /// 처리 성공 여부
    /// </summary>
    [JsonProperty(Order = 7, PropertyName = "success")]
    public bool Success { get; set; }

    /// <summary>
    /// 응답 메시지
    /// </summary>
    [JsonProperty(Order = 8, PropertyName = "message")]
    public string Message { get; set; }
}
