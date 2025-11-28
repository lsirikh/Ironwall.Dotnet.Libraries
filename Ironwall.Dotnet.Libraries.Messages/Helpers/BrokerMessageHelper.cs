using Ironwall.Dotnet.Libraries.Messages.Defines.Brokers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
        DateFormatHandling = DateFormatHandling.IsoDateFormat,
        DateParseHandling = DateParseHandling.None  // ISO 날짜 문자열을 DateTime으로 변환하지 않음

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

    #region - Broker 메시지 파싱 (data가 escaped string인 경우) -
    /// <summary>
    /// Broker 메시지에서 단일 Event DTO 추출
    /// <para>data가 escaped JSON string인 경우 2차 파싱</para>
    /// </summary>
    /// <typeparam name="TDto">변환할 DTO 타입</typeparam>
    /// <param name="json">전체 Broker 메시지 JSON</param>
    /// <returns>파싱된 DTO 또는 null</returns>
    public static TDto? ParseSingleEventFromBrokerMessage<TDto>(string json) where TDto : class
    {
        try
        {
            var brokerMsg = JObject.Parse(json);
            var dataToken = brokerMsg["data"];

            if (dataToken == null || dataToken.Type == JTokenType.Null)
                return null;

            // data가 string인 경우 (escaped JSON) → 직접 문자열 사용
            // data가 객체인 경우 → JSON으로 변환
            string dataJson = dataToken.Type == JTokenType.String
                ? dataToken.ToString()
                : dataToken.ToString(Formatting.None);

            return JsonConvert.DeserializeObject<TDto>(dataJson, _jsonSettings);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Broker 메시지에서 Event DTO 목록 추출 (배열/단일 모두 지원)
    /// <para>data가 escaped JSON string인 경우 2차 파싱</para>
    /// </summary>
    /// <typeparam name="TDto">변환할 DTO 타입</typeparam>
    /// <param name="json">전체 Broker 메시지 JSON</param>
    /// <returns>파싱된 DTO 목록 (파싱 실패 시 빈 리스트)</returns>
    public static List<TDto> ParseEventsFromBrokerMessage<TDto>(string json) where TDto : class
    {
        var result = new List<TDto>();

        try
        {
            var brokerMsg = JObject.Parse(json);
            var dataToken = brokerMsg["data"];

            if (dataToken == null || dataToken.Type == JTokenType.Null)
                return result;

            // data가 string인 경우 (escaped JSON) → 직접 문자열 사용
            // data가 객체인 경우 → JSON으로 변환
            string dataJson = dataToken.Type == JTokenType.String
                ? dataToken.ToString()
                : dataToken.ToString(Formatting.None);

            var innerToken = JToken.Parse(dataJson);

            if (innerToken is JArray arr)
            {
                foreach (var item in arr)
                {
                    var dto = item.ToObject<TDto>();
                    if (dto != null)
                        result.Add(dto);
                }
            }
            else if (innerToken is JObject obj)
            {
                var dto = obj.ToObject<TDto>();
                if (dto != null)
                    result.Add(dto);
            }
        }
        catch (JsonException)
        {
            // 파싱 실패 시 빈 리스트 반환
        }

        return result;
    }
    #endregion
}