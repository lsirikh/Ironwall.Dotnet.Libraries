using Newtonsoft.Json;

namespace Ironwall.Dotnet.Monitoring.Models.Fences;

/// <summary>
/// 한 갈래의 번호 대역 — 시작 · 끝(둘 다 포함). 번호 = 시작 + (그 갈래 안 위치 순서 − 1)(fence-wiring-editor FR-10).
/// </summary>
public sealed record NumberBand(
    [property: JsonProperty("category")] FenceSensorCategory Category,
    [property: JsonProperty("start")] int Start,
    [property: JsonProperty("end")] int End)
{
    /// <summary>담을 수 있는 번호 수(끝 &lt; 시작이면 0).</summary>
    [JsonIgnore]
    public int Size => End >= Start ? End - Start + 1 : 0;

    /// <summary>두 대역이 겹치는가.</summary>
    public bool Overlaps(NumberBand other) => other is not null && Start <= other.End && other.Start <= End;

    /// <summary>"스마트 1~99".</summary>
    [JsonIgnore]
    public string Text => $"{NumberingMath.CategoryText(Category)} {Start}~{End}";
}
