using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// 함체 메트릭 저장 응답 DTO (§5.5.9)
/// <para>임계 초과 판정의 자리가 판본마다 다르다 — 6.3 은 봉투 <b>최상위</b> <c>threshold_exceeded[].type</c>,
/// 7.0+ 는 <c>data.threshold_exceeded[]</c> 의 <c>direction</c> · <c>severity</c>. 읽을 때는 <see cref="EffectiveThresholdExceeded"/> 를 쓴다.</para>
/// </summary>
public class EnclosureMetricSaveResponseDto
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("message")]
    public string? Message { get; set; }

    [JsonProperty("data")]
    public EnclosureMetricDto? Data { get; set; }

    /// <summary>6.3 자리 — 봉투 최상위 <c>threshold_exceeded</c>. 7.0+ 에서는 없다(<c>null</c>).</summary>
    [JsonProperty("threshold_exceeded")]
    public List<ThresholdExceededItemDto>? ThresholdExceeded { get; set; }

    /// <summary>거부하지 않은 경고 — 봉투 최상위 <c>warnings[]</c>(7.0+, 예: <c>OBSERVED_AT_DEFAULTED</c>).</summary>
    [JsonProperty("warnings", NullValueHandling = NullValueHandling.Ignore)]
    public List<Ironwall.Dotnet.Libraries.Messages.Dto.Bases.ResponseWarningDto>? Warnings { get; set; }

    /// <summary>
    /// 실제 판정 결과 — <c>data.threshold_exceeded</c>(7.0+)를 먼저, 없으면 최상위(6.3)를 읽는다. 둘 다 없으면 빈 목록.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<ThresholdExceededItemDto> EffectiveThresholdExceeded
        => (IReadOnlyList<ThresholdExceededItemDto>?)Data?.ThresholdExceeded
           ?? ThresholdExceeded
           ?? (IReadOnlyList<ThresholdExceededItemDto>)Array.Empty<ThresholdExceededItemDto>();
}

/// <summary>
/// 임계값 초과 항목 DTO
/// </summary>
public class ThresholdExceededItemDto
{
    [JsonProperty("field")]
    public string Field { get; set; } = string.Empty;

    [JsonProperty("value")]
    public double Value { get; set; }

    [JsonProperty("threshold")]
    public double Threshold { get; set; }

    /// <summary>6.3 어휘(<c>HIGH</c>·<c>LOW</c>) — 7.0+ 에는 없다. 읽을 때는 <see cref="EffectiveDirection"/>.</summary>
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>7.0+ 초과 방향 — <c>HIGH</c>|<c>LOW</c>.</summary>
    [JsonProperty("direction", NullValueHandling = NullValueHandling.Ignore)]
    public string? Direction { get; set; }

    /// <summary>7.0+ 그 초과가 만드는 운영 이벤트의 심각도 원문. 6.3 에는 없다(<c>null</c>).</summary>
    [JsonProperty("severity", NullValueHandling = NullValueHandling.Ignore)]
    public string? Severity { get; set; }

    /// <summary>방향 — <see cref="Direction"/>(7.0+) 우선, 없으면 6.3 <see cref="Type"/>. 둘 다 비면 <c>null</c>.</summary>
    [JsonIgnore]
    public string? EffectiveDirection
        => !string.IsNullOrWhiteSpace(Direction) ? Direction!.Trim().ToUpperInvariant()
         : !string.IsNullOrWhiteSpace(Type) ? Type.Trim().ToUpperInvariant()
         : null;
}
