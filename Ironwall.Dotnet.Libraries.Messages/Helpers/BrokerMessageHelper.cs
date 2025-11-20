using Ironwall.Dotnet.Libraries.Messages.Defines.Brokers;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Helpers;

/// <summary>
/// Broker 메시지 생성 및 변환 Helper
/// <para>DTO만으로 BrokerRequest/BrokerResponse를 쉽게 생성합니다.</para>
/// <para>ResponseHelper와 동일한 패턴을 Broker 메시지에 적용합니다.</para>
/// </summary>
public static class BrokerMessageHelper
{
    private static readonly JsonSerializerSettings _jsonSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore,
        DateFormatHandling = DateFormatHandling.IsoDateFormat
    };

    #region - Request 생성 -
    /// <summary>
    /// DTO → BrokerRequest&lt;TDto&gt; 변환 (확장 메서드)
    /// <para>RESTful API의 ToApiResponseAsync와 동일한 패턴</para>
    /// </summary>
    public static BrokerRequest<TDto> ToBrokerRequest<TDto>(
        this TDto dto,
        string command,
        string from) where TDto : class
    {
        return new BrokerRequest<TDto>
        {
            Id = Guid.NewGuid().ToString(),
            TypeMessage = "REQ",
            Command = command,
            From = from,
            Data = dto,
            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        };
    }

    /// <summary>
    /// DTO → BrokerRequest&lt;TDto&gt; 변환 (자동 Command 생성)
    /// <para>DTO 타입 이름에서 Command 자동 추출</para>
    /// <para>예: EventCallDto → EVENT_CALL</para>
    /// </summary>
    public static BrokerRequest<TDto> ToBrokerRequest<TDto>(
        this TDto dto,
        string from) where TDto : class
    {
        var command = typeof(TDto).Name.Replace("Dto", "").ToUpper();
        return dto.ToBrokerRequest(command, from);
    }

    /// <summary>
    /// BrokerRequest 생성 (정적 팩토리 메서드)
    /// </summary>
    public static BrokerRequest<TDto> CreateRequest<TDto>(
        TDto data,
        string command,
        string from) where TDto : class
    {
        return data.ToBrokerRequest(command, from);
    }
    #endregion

    #region - Response 생성 -
    /// <summary>
    /// 성공 응답 생성
    /// </summary>
    public static BrokerResponse<TDto> CreateResponse<TDto>(
        TDto data,
        string requestId,
        string from,
        string command = "",
        string message = "Success") where TDto : class
    {
        return new BrokerResponse<TDto>
        {
            Id = Guid.NewGuid().ToString(),
            TypeMessage = "RSP",
            Command = command,
            From = from,
            Data = data,
            RequestId = requestId,
            Success = true,
            Message = message,
            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        };
    }

    /// <summary>
    /// 에러 응답 생성
    /// </summary>
    public static BrokerResponse<TDto> CreateErrorResponse<TDto>(
        string requestId,
        string from,
        string errorMessage,
        string command = "") where TDto : class
    {
        return new BrokerResponse<TDto>
        {
            Id = Guid.NewGuid().ToString(),
            TypeMessage = "RSP",
            Command = command,
            From = from,
            Data = null,
            RequestId = requestId,
            Success = false,
            Message = errorMessage,
            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        };
    }

    /// <summary>
    /// 원본 요청에 대한 응답 생성 (확장 메서드)
    /// </summary>
    public static BrokerResponse<TResponse> CreateResponseFor<TRequest, TResponse>(
        this BrokerRequest<TRequest> request,
        TResponse responseData,
        string from,
        string message = "Success")
        where TRequest : class
        where TResponse : class
    {
        return new BrokerResponse<TResponse>
        {
            Id = Guid.NewGuid().ToString(),
            TypeMessage = "RSP",
            Command = request.Command,  // 원본 Command 복사
            From = from,
            Data = responseData,
            RequestId = request.Id,
            Success = true,
            Message = message,
            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        };
    }
    #endregion

    #region - JSON 변환 -
    /// <summary>
    /// BrokerRequest → JSON 직렬화
    /// </summary>
    public static string ToJson<TDto>(this BrokerRequest<TDto> request) where TDto : class
    {
        return JsonConvert.SerializeObject(request, _jsonSettings);
    }

    /// <summary>
    /// BrokerResponse → JSON 직렬화
    /// </summary>
    public static string ToJson<TDto>(this BrokerResponse<TDto> response) where TDto : class
    {
        return JsonConvert.SerializeObject(response, _jsonSettings);
    }

    /// <summary>
    /// JSON → BrokerRequest&lt;TDto&gt; 역직렬화
    /// </summary>
    public static BrokerRequest<TDto>? FromJsonRequest<TDto>(string json) where TDto : class
    {
        return JsonConvert.DeserializeObject<BrokerRequest<TDto>>(json, _jsonSettings);
    }

    /// <summary>
    /// JSON → BrokerResponse&lt;TDto&gt; 역직렬화
    /// </summary>
    public static BrokerResponse<TDto>? FromJsonResponse<TDto>(string json) where TDto : class
    {
        return JsonConvert.DeserializeObject<BrokerResponse<TDto>>(json, _jsonSettings);
    }
    #endregion
}