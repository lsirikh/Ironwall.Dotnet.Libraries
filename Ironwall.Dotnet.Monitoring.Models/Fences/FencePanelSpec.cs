using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ironwall.Dotnet.Monitoring.Models.Fences;

/// <summary>
/// 펜스 한 칸(망 · 기둥 사이 한 칸) — 모양 · 색 · 높이 · 망 간 거리(fence-wiring-editor FR-01 · FR-03).
/// 제어기 한 대의 펜스는 이 칸들의 <b>순서 있는 목록</b>이다(Ch1(A) 쪽 끝이 첫 칸).
/// </summary>
/// <param name="Style">모양 5종.</param>
/// <param name="Color">색 <c>#RRGGBB</c> — <c>null</c> 이면 모양의 기본색(테마 토큰).</param>
/// <param name="HeightM">높이(m, <see cref="MIN_HEIGHT_M"/>~<see cref="MAX_HEIGHT_M"/>).</param>
/// <param name="SpanM">망 간 거리 = 이 칸의 길이(m, <see cref="MIN_SPAN_M"/>~<see cref="MAX_SPAN_M"/>).</param>
public sealed record FencePanelSpec(
    [property: JsonProperty("style")] EnumFenceStyle Style,
    [property: JsonProperty("color")] string? Color,
    [property: JsonProperty("height_m")] double HeightM,
    [property: JsonProperty("span_m")] double SpanM)
{
    public const double MIN_HEIGHT_M = 0.5;
    public const double MAX_HEIGHT_M = 6.0;
    public const double MIN_SPAN_M = 0.5;
    public const double MAX_SPAN_M = 20.0;
    public const double DEFAULT_SPAN_M = 6.0;

    /// <summary>[망 간 거리] 단추 값(m).</summary>
    public static IReadOnlyList<double> SpanChoices { get; } = new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 };

    /// <summary>모양 목록(견본 단추 차례).</summary>
    public static IReadOnlyList<EnumFenceStyle> Styles { get; } = new[]
    {
        EnumFenceStyle.ChainLink, EnumFenceStyle.ChainLinkRazor, EnumFenceStyle.Brick, EnumFenceStyle.Concrete, EnumFenceStyle.DesignFence,
    };

    /// <summary>담(기둥 없음)인가 — 벽돌 · 시멘트.</summary>
    [JsonIgnore]
    public bool IsWall => IsWallStyle(Style);

    /// <summary>담(기둥 없음) 모양인가.</summary>
    public static bool IsWallStyle(EnumFenceStyle style) => style is EnumFenceStyle.Brick or EnumFenceStyle.Concrete;

    /// <summary>모양별 기본 높이(m) — 철조망 2.4 · 벽돌 2.2 · 디자인 2.0.</summary>
    public static double DefaultHeight(EnumFenceStyle style) => style switch
    {
        EnumFenceStyle.Brick => 2.2,
        EnumFenceStyle.DesignFence => 2.0,
        _ => 2.4,
    };

    /// <summary>모양 이름(한글).</summary>
    public static string StyleText(EnumFenceStyle style) => style switch
    {
        EnumFenceStyle.ChainLinkRazor => "철조망+윤형",
        EnumFenceStyle.Brick => "벽돌담",
        EnumFenceStyle.Concrete => "시멘트담",
        EnumFenceStyle.DesignFence => "디자인펜스",
        _ => "철조망",
    };

    /// <summary>기본 칸 — 모양의 기본 높이 · 기본색.</summary>
    public static FencePanelSpec Default(EnumFenceStyle style = EnumFenceStyle.ChainLink, double spanM = DEFAULT_SPAN_M)
        => new FencePanelSpec(style, null, DefaultHeight(style), spanM).Normalized();

    /// <summary>범위 밖 값을 자르고 색 글자를 <c>#RRGGBB</c> 로 맞춘 사본(NaN · 무한은 기본값).</summary>
    public FencePanelSpec Normalized()
        => this with
        {
            Style = Enum.IsDefined(typeof(EnumFenceStyle), Style) ? Style : EnumFenceStyle.ChainLink,
            HeightM = ClampHeight(HeightM, Style),
            SpanM = ClampSpan(SpanM),
            Color = NormalizeColor(Color),
        };

    /// <summary>높이를 범위 안으로(0.1m 단위로 맞춘다).</summary>
    public static double ClampHeight(double metres, EnumFenceStyle style = EnumFenceStyle.ChainLink)
        => double.IsFinite(metres) ? Math.Round(Math.Clamp(metres, MIN_HEIGHT_M, MAX_HEIGHT_M), 2) : DefaultHeight(style);

    /// <summary>망 간 거리를 범위 안으로(0.01m 단위).</summary>
    public static double ClampSpan(double metres)
        => double.IsFinite(metres) ? Math.Round(Math.Clamp(metres, MIN_SPAN_M, MAX_SPAN_M), 2) : DEFAULT_SPAN_M;

    /// <summary>높이 값이 범위 안인가(입력 검증 — 자르기 전).</summary>
    public static bool IsValidHeight(double metres) => double.IsFinite(metres) && metres >= MIN_HEIGHT_M && metres <= MAX_HEIGHT_M;

    /// <summary>망 간 거리 값이 범위 안인가(입력 검증 — 자르기 전).</summary>
    public static bool IsValidSpan(double metres) => double.IsFinite(metres) && metres >= MIN_SPAN_M && metres <= MAX_SPAN_M;

    /// <summary>
    /// 색 글자 → <c>#RRGGBB</c>(대문자). <c>#RGB</c> · <c>RRGGBB</c> 도 받는다. 비었거나 읽을 수 없으면 <c>null</c>(= 기본색).
    /// </summary>
    public static string? NormalizeColor(string? text)
    {
        var t = text?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        if (t.StartsWith('#')) t = t[1..];
        if (t.Length == 3) t = string.Concat(t[0], t[0], t[1], t[1], t[2], t[2]);
        if (t.Length != 6 || !int.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)) return null;
        return "#" + t.ToUpperInvariant();
    }
}
