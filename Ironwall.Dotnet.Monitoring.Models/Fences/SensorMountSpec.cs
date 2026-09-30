using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Fences;

/// <summary>
/// 센서 한 대의 설치 자리(fence-wiring-editor FR-07) — 어느 망(또는 기둥) · 어느 자리 · 높이 조정 · 보는 쪽.
/// </summary>
/// <param name="Panel">
/// 기둥 자리(<see cref="FenceMountSpot.PostTop"/> · <see cref="FenceMountSpot.PostMiddle"/>)면 <b>기둥 번호</b>(0…망 수 — 망 i 의 왼쪽 기둥이 i),
/// 그 밖이면 <b>망 번호</b>(0…망 수−1).
/// </param>
/// <param name="Spot">자리.</param>
/// <param name="HeightOffsetM">자리 높이에서 더 올리거나(+) 내린(−) 거리(m).</param>
/// <param name="FacesBack">
/// 보는 쪽이 뒤(펜스 내부)인가 — 저장 문서에만 싣는다(지도가 읽을 몫). 결선 창에서는 서버의 <c>spec.wiring.facing</c> 이 정본이라
/// 편집 중에는 늘 <c>false</c> 로 둔다.
/// </param>
public sealed record SensorMountSpec(
    [property: JsonProperty("panel")] int Panel,
    [property: JsonProperty("spot")] FenceMountSpot Spot,
    [property: JsonProperty("height_offset_m")] double HeightOffsetM = 0,
    [property: JsonProperty("faces_back")] bool FacesBack = false)
{
    public const double MIN_OFFSET_M = -3.0;
    public const double MAX_OFFSET_M = 3.0;

    /// <summary>기둥에 다는 자리인가(<see cref="Panel"/> 이 기둥 번호).</summary>
    [JsonIgnore]
    public bool IsPostSpot => IsPost(Spot);

    /// <summary>기둥에 다는 자리인가.</summary>
    public static bool IsPost(FenceMountSpot spot) => spot is FenceMountSpot.PostTop or FenceMountSpot.PostMiddle;

    /// <summary>담에 다는 자리인가.</summary>
    public static bool IsWall(FenceMountSpot spot) => spot is FenceMountSpot.WallTop or FenceMountSpot.WallFace;

    /// <summary>높이 조정을 범위 안으로(0.05m 단위).</summary>
    public static double ClampOffset(double metres)
        => double.IsFinite(metres) ? Math.Round(Math.Clamp(metres, MIN_OFFSET_M, MAX_OFFSET_M) * 20) / 20 : 0;

    /// <summary>자리 이름(한글).</summary>
    public static string SpotText(FenceMountSpot spot) => spot switch
    {
        FenceMountSpot.PostMiddle => "기둥 중간",
        FenceMountSpot.PanelCenter => "망 가운데",
        FenceMountSpot.WallTop => "담 위",
        FenceMountSpot.WallFace => "담 앞면",
        _ => "기둥 위",
    };
}
