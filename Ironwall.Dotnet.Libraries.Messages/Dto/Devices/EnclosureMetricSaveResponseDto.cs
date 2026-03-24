using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// 함체 메트릭 저장 응답 DTO (§5.5.9)
/// <para>threshold_exceeded가 data와 동일 레벨(top-level)에 위치하는 특수 구조</para>
/// </summary>
public class EnclosureMetricSaveResponseDto
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("message")]
    public string? Message { get; set; }

    [JsonProperty("data")]
    public EnclosureMetricDto? Data { get; set; }

    [JsonProperty("threshold_exceeded")]
    public List<ThresholdExceededItemDto>? ThresholdExceeded { get; set; }
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

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;
}
