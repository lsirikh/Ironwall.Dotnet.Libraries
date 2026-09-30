using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Monitoring.Models.Fences;

/// <summary>
/// 제어기 한 대의 번호 대역 묶음(fence-wiring-editor FR-10 · §1-A) — 프리셋 3개 또는 직접 설정. <b>강제 규칙이 아니라 참고 프리셋</b>이다
/// (민간 시설은 다를 수 있다). 값 비교(<see cref="Equals(NumberBandSet?)"/>)는 대역 목록 내용으로 한다.
/// </summary>
/// <param name="Preset">프리셋 이름 — <see cref="PRESET_TIER2"/> · <see cref="PRESET_TIER3"/> · <see cref="PRESET_TIER4"/> · <see cref="PRESET_CUSTOM"/>.</param>
/// <param name="Bands">갈래별 대역(갈래당 하나).</param>
public sealed record NumberBandSet(
    [property: JsonProperty("preset")] string Preset,
    [property: JsonProperty("bands")] IReadOnlyList<NumberBand> Bands)
{
    public const string PRESET_TIER2 = "tier2";
    public const string PRESET_TIER3 = "tier3";
    public const string PRESET_TIER4 = "tier4";
    public const string PRESET_CUSTOM = "custom";

    /// <summary>중요시설 2차 — 펜스센서 1~ · 복합센서 180~(초기 시스템).</summary>
    public static NumberBandSet Tier2 { get; } = new(PRESET_TIER2, new[]
    {
        new NumberBand(FenceSensorCategory.Fence, 1, 179),
        new NumberBand(FenceSensorCategory.Multi, 180, NumberingMath.MAX_NUMBER),
    });

    /// <summary>중요시설 3차 — 스마트센서(단독) 1~ · 복합센서 180~.</summary>
    public static NumberBandSet Tier3 { get; } = new(PRESET_TIER3, new[]
    {
        new NumberBand(FenceSensorCategory.Smart, 1, 179),
        new NumberBand(FenceSensorCategory.Multi, 180, NumberingMath.MAX_NUMBER),
    });

    /// <summary>중요시설 4차 — 스마트센서 1~99 · 펜스센서 101~199(판망 = 스마트, 윤형 = 펜스).</summary>
    public static NumberBandSet Tier4 { get; } = new(PRESET_TIER4, new[]
    {
        new NumberBand(FenceSensorCategory.Smart, 1, 99),
        new NumberBand(FenceSensorCategory.Fence, 101, 199),
    });

    /// <summary>프리셋 3개(단추 차례).</summary>
    public static IReadOnlyList<NumberBandSet> Presets { get; } = new[] { Tier2, Tier3, Tier4 };

    /// <summary>프리셋 이름(한글).</summary>
    [JsonIgnore]
    public string PresetText => Preset switch
    {
        PRESET_TIER2 => "중요시설 2차",
        PRESET_TIER3 => "중요시설 3차",
        PRESET_TIER4 => "중요시설 4차",
        _ => "직접 설정",
    };

    /// <summary>"중요시설 4차 · 스마트 1~99 · 펜스 101~199".</summary>
    [JsonIgnore]
    public string Text => string.Join(" · ", new[] { PresetText }.Concat(Bands.Select(b => b.Text)));

    /// <summary>그 갈래의 대역(없으면 <c>null</c> — 번호를 건드리지 않는다).</summary>
    public NumberBand? BandOf(FenceSensorCategory category) => Bands?.FirstOrDefault(b => b.Category == category);

    /// <summary>대역 하나를 바꾸거나 더한(시작 · 끝) <b>직접 설정</b> 사본. <paramref name="band"/> 가 <c>null</c> 이면 그 갈래를 뺀다.</summary>
    public NumberBandSet With(FenceSensorCategory category, NumberBand? band)
    {
        var list = (Bands ?? Array.Empty<NumberBand>()).Where(b => b.Category != category).ToList();
        if (band is not null) list.Add(band with { Category = category });
        return new NumberBandSet(PRESET_CUSTOM, list.OrderBy(b => b.Start).ToList());
    }

    public bool Equals(NumberBandSet? other)
        => other is not null
           && string.Equals(Preset, other.Preset, StringComparison.Ordinal)
           && (Bands ?? Array.Empty<NumberBand>()).SequenceEqual(other.Bands ?? Array.Empty<NumberBand>());

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Preset, StringComparer.Ordinal);
        foreach (var band in Bands ?? Array.Empty<NumberBand>()) hash.Add(band);
        return hash.ToHashCode();
    }
}
