using System;
using System.Collections.Generic;
using System.Globalization;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.GMaps.Models;
/****************************************************************************
   Purpose      : 지도 2.5D 기울이기(틸트) 설정 영속 — map-tilt-25d PRD FR-07(§0 G1/G2/G5).
                  회전(MapRotation)과 분리된 kill-switch(기본 OFF) + 사용자 각도 + 줌 게이트 파라미터를
                  AppSettings.MapTilt 에 저장. 키 부재 시 기본값(SIM-P003), 범위 밖 값은 Normalize 가
                  클램프 + 경고 로그(SIM-P002). 저장: MapSettingsHelper.SaveMapTiltAsync, 복원: MapViewModel(홈 줌 이후).
   Note         : 판정(TiltMath.Decide)은 이 모델의 MinZoom/HysteresisSteps/MaxAngleDeg 를 입력으로 받는다.
                  GMaps 모델 프로젝트는 GMaps.Ui 를 참조할 수 없어 0.5 스냅은 ZoomLadder.Snap 과 동형 수식을 둔다.
   Created On   : 2026-09-08 · Sensorway Co., Ltd.
 ****************************************************************************/
public class MapTiltModel
{
    /// <summary>기본 각(§0 G2).</summary>
    public const double DefaultAngleDeg = 20.0;
    /// <summary>게이트 진입 실효줌 기본(§0 ②).</summary>
    public const double DefaultMinZoom = 18.0;
    /// <summary>히스테리시스 기본 스텝(0.5 줌 단위, §0 ②).</summary>
    public const int DefaultHysteresisSteps = 1;
    /// <summary>최대 틸트 각 상한(§0 G1) — MaxAngleDeg 설정값도 이 값을 넘지 못한다.</summary>
    public const double HardMaxAngleDeg = 35.0;
    /// <summary>히스테리시스 스텝 상한(이탈 임계 = MinZoom − 0.5·h, 4 스텝 = 2 줌).</summary>
    public const int MaxHysteresisSteps = 4;
    /// <summary>MinZoom 상한 여유 = 0.5 × DigitalZoomSteps(3, MapZoomControl 기본) — 소프트 밴드 최상단 "Max.5++".</summary>
    public const double MinZoomSoftBandCap = 1.5;

    /// <summary>틸트 기능 kill-switch(§0 G5, 기본 OFF). 부팅 시 이 값으로 복원.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>사용자 각도(°) — 게이트 통과 시 적용 φ = clamp(AngleDeg, 0, MaxAngleDeg).</summary>
    public double AngleDeg { get; set; } = DefaultAngleDeg;

    /// <summary>게이트 진입 실효줌(0.5 그리드). 이탈은 MinZoom − 0.5·HysteresisSteps.</summary>
    public double MinZoom { get; set; } = DefaultMinZoom;

    /// <summary>히스테리시스 스텝(0.5 줌 단위).</summary>
    public int HysteresisSteps { get; set; } = DefaultHysteresisSteps;

    /// <summary>각도 상한(°) — HardMaxAngleDeg(35) 를 넘지 못한다.</summary>
    public double MaxAngleDeg { get; set; } = HardMaxAngleDeg;

    /// <summary>
    /// 범위 밖 값을 제자리에서 클램프하고 경고 문자열을 반환(+ <paramref name="log"/> 에 Warning) — SIM-P002/P003.
    /// 순서: MaxAngleDeg → AngleDeg(MaxAngleDeg 의존) → MinZoom(0.5 스냅 → [minZoom, maxZoom+1.5]) → HysteresisSteps.
    /// NaN/∞ 는 기본값으로 대체. 반환 목록이 비면 파일 값이 그대로 유효했다는 뜻.
    /// </summary>
    /// <param name="minZoom">맵 MinZoom(타일 줌 하한).</param>
    /// <param name="maxZoom">맵 MaxZoom(타일 줌 상한) — 실효줌 상한은 maxZoom + 1.5.</param>
    /// <param name="log">경고 로그 대상(없으면 반환 목록만).</param>
    public IReadOnlyList<string> Normalize(int minZoom, int maxZoom, ILogService? log = default)
    {
        var warnings = new List<string>();

        // 1) MaxAngleDeg ∈ [0, 35]
        if (!IsFinite(MaxAngleDeg))
        {
            warnings.Add(Message("MaxAngleDeg", MaxAngleDeg, HardMaxAngleDeg, "유한값 아님 → 기본값"));
            MaxAngleDeg = HardMaxAngleDeg;
        }
        else if (MaxAngleDeg < 0.0 || MaxAngleDeg > HardMaxAngleDeg)
        {
            double clamped = Math.Clamp(MaxAngleDeg, 0.0, HardMaxAngleDeg);
            warnings.Add(Message("MaxAngleDeg", MaxAngleDeg, clamped, Range(0.0, HardMaxAngleDeg)));
            MaxAngleDeg = clamped;
        }

        // 2) AngleDeg ∈ [0, MaxAngleDeg]
        if (!IsFinite(AngleDeg))
        {
            double fallback = Math.Min(DefaultAngleDeg, MaxAngleDeg);
            warnings.Add(Message("AngleDeg", AngleDeg, fallback, "유한값 아님 → 기본값"));
            AngleDeg = fallback;
        }
        else if (AngleDeg < 0.0 || AngleDeg > MaxAngleDeg)
        {
            double clamped = Math.Clamp(AngleDeg, 0.0, MaxAngleDeg);
            warnings.Add(Message("AngleDeg", AngleDeg, clamped, Range(0.0, MaxAngleDeg)));
            AngleDeg = clamped;
        }

        // 3) MinZoom: 0.5 스냅 → [minZoom, maxZoom + 1.5]
        double lo = minZoom;
        double hi = Math.Max(lo, maxZoom + MinZoomSoftBandCap);
        if (!IsFinite(MinZoom))
        {
            double fallback = Math.Clamp(DefaultMinZoom, lo, hi);
            warnings.Add(Message("MinZoom", MinZoom, fallback, "유한값 아님 → 기본값"));
            MinZoom = fallback;
        }
        else
        {
            double snapped = SnapHalf(MinZoom);
            if (snapped < lo || snapped > hi)
            {
                double clamped = Math.Clamp(snapped, lo, hi);
                warnings.Add(Message("MinZoom", MinZoom, clamped, Range(lo, hi)));
                MinZoom = clamped;
            }
            else
            {
                MinZoom = snapped;   // 0.5 그리드 정규화는 경고 없이 조용히 적용
            }
        }

        // 4) HysteresisSteps ∈ [0, 4]
        if (HysteresisSteps < 0 || HysteresisSteps > MaxHysteresisSteps)
        {
            int clamped = Math.Clamp(HysteresisSteps, 0, MaxHysteresisSteps);
            warnings.Add(Message("HysteresisSteps", HysteresisSteps, clamped, Range(0, MaxHysteresisSteps)));
            HysteresisSteps = clamped;
        }

        foreach (var w in warnings) log?.Warning(w);
        return warnings;
    }

    /// <summary>0.5 그리드 스냅 — ZoomLadder.Snap 동형(AwayFromZero, 은행가 반올림 금지).</summary>
    public static double SnapHalf(double value)
        => Math.Round(value * 2.0, MidpointRounding.AwayFromZero) / 2.0;

    private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

    /// <summary>허용 범위 문구 — InvariantCulture 고정(de-DE "20,5" 금지, ZoomLadder.Label NFR-01 동형).</summary>
    private static string Range(double lo, double hi)
        => string.Format(CultureInfo.InvariantCulture, "허용 {0:0.#}~{1:0.#}", lo, hi);

    private static string Message(string key, double from, double to, string rule)
        => string.Format(CultureInfo.InvariantCulture,
            "[MapTilt] {0} 범위 밖: {1} → {2} 클램프({3})", key, from, to, rule);

    public override string ToString()
        => string.Format(CultureInfo.InvariantCulture,
            "MapTilt[enabled={0}, angle={1:F1}, minZoom={2:F1}, hyst={3}, maxAngle={4:F1}]",
            IsEnabled, AngleDeg, MinZoom, HysteresisSteps, MaxAngleDeg);
}
