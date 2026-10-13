using System.Collections.Generic;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Numerics;

/// <summary>
/// 지도 속성창 · 지도 겹창 숫자 칸 명세표(코드판). 표의 원본은 docs/prds/map-property-numeric-precision-prd.md —
/// 칸을 더하거나 범위를 바꾸면 두 곳을 함께 고친다. 칸마다 손으로 반올림하지 않고 여기 spec 의 Coerce/Normalize 만 쓴다.
/// </summary>
public static class MapNumericFields
{
    // ── 공통(GMapPropertyBaseControl) ──
    public static readonly NumericFieldSpec TitleSize = new("제목 크기", 6, 72, 1);
    public static readonly NumericFieldSpec MarkerWidth = new("너비", 0, 10000, 1);
    public static readonly NumericFieldSpec MarkerHeight = new("높이", 0, 10000, 1);
    public static readonly NumericFieldSpec MarkerAltitude = new("설치 높이", 0, 100, 1);
    public static readonly NumericFieldSpec MarkerBearing = new("회전", 0, 360, 1);
    public static readonly NumericFieldSpec TitleMaxWidthPx = new("제목 폭", 40, 800, 1);
    public static readonly NumericFieldSpec MarkerZoom = new("최소 표시 줌", 0, 24, 0.5);

    // ── 타입별 ──
    public static readonly NumericFieldSpec MarkerOpacity = new("도형 투명도", 0, 1, 0.01);
    public static readonly NumericFieldSpec ImageOpacity = new("이미지 투명도", 0, 1, 0.01);
    public static readonly NumericFieldSpec LineOpacity = new("선 투명도", 0, 1, 0.01);
    public static readonly NumericFieldSpec TintStrength = new("색상 강도", 0, 1, 0.01);
    public static readonly NumericFieldSpec FloorCount = new("지상층", 1, 50, 1);
    public static readonly NumericFieldSpec BasementFloorCount = new("지하층", 0, 10, 1);
    public static readonly NumericFieldSpec BuildingArea = new("면적", 0, 1_000_000, 1);
    public static readonly NumericFieldSpec DetectionRange = new("탐지 거리", 10, 1000, 1);
    public static readonly NumericFieldSpec DetectionAngle = new("탐지 각도", 5, 180, 1);
    public static readonly NumericFieldSpec DetectionBearing = new("탐지 방향", -180, 180, 1);
    public static readonly NumericFieldSpec BaseBearing = new("기준 방향", 0, 360, 1);
    public static readonly NumericFieldSpec FOVOpacity = new("FOV 투명도", 0.1, 1, 0.01);
    public static readonly NumericFieldSpec GateWidthM = new("통문 폭", FenceDefaults.GateWidthMinM, FenceDefaults.GateWidthMaxM, FenceDefaults.GateWidthStepM);
    public static readonly NumericFieldSpec PostSpacingM = new("기둥 간격", FenceDefaults.PostSpacingMinM, FenceDefaults.PostSpacingMaxM, FenceDefaults.PostSpacingStepM);
    public static readonly NumericFieldSpec FenceHeightM = new("철망 높이", FenceDefaults.FenceHeightMinM, FenceDefaults.FenceHeightMaxM, FenceDefaults.FenceHeightStepM);

    // ── 지도 겹창 ──
    public static readonly NumericFieldSpec GridSizePx = new("격자 크기", 4, 50, 1);
    public static readonly NumericFieldSpec TrackMinVisibleZoom = new("추적 최소 표시 줌", 0, 20.5, 0.5);
    public static readonly NumericFieldSpec TrackTrailMaxPoints = new("트레일 점 수", 10, 100, 1);
    public static readonly NumericFieldSpec TrackGapThresholdSec = new("끊김 분절(초)", 1, 86400, 1);
    public static readonly NumericFieldSpec TrackDefaultTtlSec = new("기본 수명(초)", 1, 86400, 1);
    public static readonly NumericFieldSpec TrackLostTtlSec = new("소실 수명(초)", 1, 86400, 1);
    public static readonly NumericFieldSpec TrackMaxSpeedMs = new("최대 속도(m/s)", 0, 1000, 0.1);
    public static readonly NumericFieldSpec TrackMaxPlaybackHours = new("최대 재생 시간(시)", 1, 8760, 1);

    /// <summary>시험 · 문서 대조용 전체 목록.</summary>
    public static IReadOnlyList<NumericFieldSpec> All { get; } = new[]
    {
        TitleSize, MarkerWidth, MarkerHeight, MarkerAltitude, MarkerBearing, TitleMaxWidthPx, MarkerZoom,
        MarkerOpacity, ImageOpacity, LineOpacity, TintStrength, FloorCount, BasementFloorCount, BuildingArea,
        DetectionRange, DetectionAngle, DetectionBearing, BaseBearing, FOVOpacity, GateWidthM, PostSpacingM, FenceHeightM,
        GridSizePx, TrackMinVisibleZoom, TrackTrailMaxPoints, TrackGapThresholdSec, TrackDefaultTtlSec, TrackLostTtlSec,
        TrackMaxSpeedMs, TrackMaxPlaybackHours,
    };
}
