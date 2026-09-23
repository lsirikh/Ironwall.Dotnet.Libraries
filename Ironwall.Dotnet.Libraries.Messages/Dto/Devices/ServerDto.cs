using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Runtime.Serialization;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// 서버 인스턴스 DTO (§8.3) — <b>평면(6.3) 모양이 쓰기 정본</b>이다.
/// </summary>
/// <remarks>
/// <para>⚠ <b>읽기는 7.0+/8.0 축 응답도 받는다</b>(D-22). 서버의 <c>SERVER_REMOVED_FIELDS</c>
/// (<c>app/schemas/server.py:59-66</c> — <c>category_id→category_server</c> · <c>port→connection.ip_port</c> ·
/// <c>hostname→connection.hostname</c> · <c>threshold_config→server_config.thresholds</c>)는 <b>응답에도 그대로
/// 적용된다</b>(<c>ServerResponse</c>, <c>:759-800</c>). 축 응답을 이 평면 DTO 로 그대로 역직렬화하면
/// <c>IpAddress</c>·<c>Port</c>·<c>Hostname</c>·<c>ThresholdConfig</c>·<c>CategoryId</c> 가 전부 기본값(빈 문자열·0)
/// 으로 <b>조용히</b> 비어 버린다 — <c>success:true</c> 응답인데 서버 내용이 텅 빈 것처럼 보인다
/// (예: <c>Devices.Ui/Services/DeviceProviderService.cs:FetchServersAsync</c> 의 스피커 서버 드롭다운).
/// <see cref="OnDeserializedMethod"/> 가 <c>connection</c>·<c>server_config</c> 축을 받았으면 평면 필드로
/// 역투영해 <b>어느 쪽 응답이든 같은 필드로</b> 읽히게 한다.</para>
/// <para><b>쓰기는 이 역투영을 타지 않는다</b> — 손댄 적 없는 <c>CategoryServer</c>·<c>IsEnable</c>·
/// <c>Connection</c>·<c>ServerConfig</c> 는 <c>null</c> 로 남아 직렬화에서 빠진다(<c>NullValueHandling.Ignore</c>).
/// 축 계약으로의 쓰기는 <c>Devices.Api/Servers/ServerAxisWriter</c> 하나가 맡는다
/// (판본 분기는 통로 한 곳에만 둔다 — N-12).</para>
/// </remarks>
public class ServerDto : BaseDto
{
    [JsonProperty("category_id", Order = 2)]
    public int CategoryId { get; set; }

    /// <summary>7.0+ 판별자 문자열(<c>category_server</c>) — <b>응답 전용</b>. 6.3 응답에는 이 키가 없다(<c>null</c>).</summary>
    [JsonProperty("category_server", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    public string? CategoryServer { get; set; }

    [JsonProperty("name", Order = 3)]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("status", Order = 4)]
    public string Status { get; set; } = "NORMAL";

    /// <summary>운용 의도 — 7.0+ 응답에만 있다(<c>is_enable</c>). 쓰기에는 싣지 않는다.</summary>
    [JsonProperty("is_enable", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsEnable { get; set; }

    [JsonProperty("ip_address", Order = 5)]
    public string IpAddress { get; set; } = string.Empty;

    [JsonProperty("port", Order = 6)]
    public int Port { get; set; }

    [JsonProperty("hostname", Order = 7)]
    public string? Hostname { get; set; }

    [JsonProperty("user_name", Order = 8)]
    public string? UserName { get; set; }

    [JsonProperty("user_password", Order = 9)]
    public string? UserPassword { get; set; }

    [JsonProperty("threshold_config", Order = 10)]
    public JObject? ThresholdConfig { get; set; }

    /// <summary>7.0+ 접속 축 원문(<c>connection</c>) — <b>응답 전용</b>. <see cref="OnDeserializedMethod"/> 가
    /// 평면 필드로 역투영한 뒤에도 원본이 필요한 소비자를 위해 남겨 둔다.</summary>
    [JsonProperty("connection", Order = 11, NullValueHandling = NullValueHandling.Ignore)]
    public JObject? Connection { get; set; }

    /// <summary>7.0+ 의도 축 원문(<c>server_config</c> — <c>thresholds</c>·<c>modes</c>) — <b>응답 전용</b>.</summary>
    [JsonProperty("server_config", Order = 12, NullValueHandling = NullValueHandling.Ignore)]
    public JObject? ServerConfig { get; set; }

    /// <summary>
    /// 소속 부대 id — <b>API 8.0 에서 신설</b>(<c>ServerResponse.unit_id</c>, 8.0.1 실측 · 응답 필수).
    /// 6.3·7.0 서버는 이 키를 주지 않으므로 그 판본에서는 <c>null</c> 이다.
    /// <para>⚠ <b>쓰기 게이트</b>: 값이 있으면 요청 본문에도 실린다.
    /// <c>Contract &gt;= EnumServerContract.V8_0</c> 을 확인한 뒤에만 채워라 —
    /// 6.3·7.0 의 쓰기 스키마에는 이 키가 없고 7.0 이후는 <c>additionalProperties: false</c> 라 즉시 422 다.</para>
    /// <para><see cref="NullValueHandling.Ignore"/> 를 <b>속성 단위로</b> 지정한 이유:
    /// <c>ApiService</c> 의 전역 직렬화 설정에 <c>NullValueHandling</c> 이 없어 그대로 두면
    /// <c>"unit_id": null</c> 이 모든 POST/PUT 본문에 실린다.</para>
    /// </summary>
    [JsonProperty("unit_id", Order = 13, NullValueHandling = NullValueHandling.Ignore)]
    public int? UnitId { get; set; }

    /// <summary>
    /// 축 응답(<c>connection</c>·<c>server_config</c>)을 평면 필드로 역투영한다(읽기 전용 보정, D-22).
    /// 이미 값이 있는 필드(예: 6.3 응답이 채운 <c>IpAddress</c>)는 덮어쓰지 않는다 — 어느 판본이 보낸 값이든
    /// 최초로 채워진 값이 이긴다.
    /// </summary>
    [OnDeserialized]
    private void OnDeserializedMethod(StreamingContext context)
    {
        if (Connection is JObject connection)
        {
            if (string.IsNullOrEmpty(IpAddress))
                IpAddress = connection.Value<string?>("ip_address") ?? IpAddress;
            if (Port == 0)
                Port = connection.Value<int?>("ip_port") ?? Port;
            Hostname ??= connection.Value<string?>("hostname");

            if (connection["credentials"] is JObject credentials)
            {
                UserName ??= credentials.Value<string?>("user_name");
                UserPassword ??= credentials.Value<string?>("user_password");
            }
        }

        if (ServerConfig is JObject config && config["thresholds"] is JObject thresholds)
            ThresholdConfig ??= thresholds;
    }
}
