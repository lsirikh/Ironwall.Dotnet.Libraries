using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/****************************************************************************
   Purpose      : 지도 카드 틸트(2.5D) 레이아웃 오버스캔·형제 캔버스 좌표 순수 수식(SSOT)
                  — map-tilt-25d PRD FR-04(Height/Δ/Margin) · FR-05(InnerToOuter/OuterToInner) · §3 수식.
   Note         : WPF 무의존(tests/GMaps.Ui.Tests 소스 링크). Thickness/IMultiValueConverter 는
                  TiltOverscanConverter(WPF 어댑터)가 이 클래스에 위임한다.
                  불변식: 컨트롤 중심 = 뷰포트 중심(회전 피벗·디지털줌 피벗·휠 커서 워프·십자선 정합).
                  시뮬 근거: docs/analyses/map-tilt-25d-scenario-analysis.md §3, SIM-C001/C014/C015.
   Created By   : Claude
   Created On   : 2026-09-08
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>MultiBinding ConverterParameter 대상 — "Height" | "Margin".</summary>
public enum TiltOverscanTarget
{
    Height = 0,
    Margin = 1,
}

/// <summary>
/// 오버스캔: <c>Height = H_view / cosφ_layout</c>, <c>Δ = (Height − H_view)/2</c>, <c>Margin = (0, −Δ, 0, −Δ)</c>(상하 대칭).
/// 형제 캔버스: <c>outer = (cx_v + (x−cx)·s, cy_v + (y−cy)·s·cosφ)</c>,
/// (cx,cy)=컨트롤 중심 (W/2, H_ctrl/2), (cx_v,cy_v)=뷰포트 중심 (W/2, H_ctrl/2 − Δ) = (W/2, H_view/2).
/// φ_layout(레이아웃 각)과 φ(렌더 각)는 분리된다 — 조작 중 선확장(G8) 동안 φ_layout=φmax, φ&lt;φmax.
/// </summary>
public static class TiltOverscanMath
{
    /// <summary>배율 항등 판정 허용 오차 — GMapCustomControl.InnerToOuter 의 |s−1|&lt;0.001 가드와 동일.</summary>
    public const double IdentityEpsilon = 0.001;

    /// <summary>각도 0 판정 허용 오차(도) — RotationMath.Epsilon 과 동일값(WPF 의존을 피하려 복제).</summary>
    public const double AngleEpsilon = 1e-4;

    /// <summary>φ=0(평면) 판정. NaN/∞ 는 평면으로 본다.</summary>
    public static bool IsFlat(double phiDeg)
        => double.IsNaN(phiDeg) || double.IsInfinity(phiDeg) || Math.Abs(phiDeg) < AngleEpsilon;

    /// <summary>
    /// cosφ. NaN/∞ → 1. φ≥90° 는 오버스캔이 무한대로 발산하므로 1(항등)로 폴백한다 —
    /// 범위 클램프(G1: 0~MaxAngleDeg)는 판정(TiltMath.Decide)·모델(MapTiltModel) 책임이다.
    /// </summary>
    public static double CosOf(double phiDeg)
    {
        if (IsFlat(phiDeg)) return 1.0;
        double c = Math.Cos(Math.Abs(phiDeg) * Math.PI / 180.0);
        return c > 1e-6 ? c : 1.0;
    }

    /// <summary>오버스캔 높이 <c>H_view / cosφ_layout</c>. φ=0 이면 뷰포트 높이 그대로(해제). 뷰포트 높이가 NaN/≤0 이면 0.</summary>
    public static double Height(double viewportHeight, double phiLayoutDeg)
    {
        if (double.IsNaN(viewportHeight) || double.IsInfinity(viewportHeight) || viewportHeight <= 0) return 0.0;
        if (IsFlat(phiLayoutDeg)) return viewportHeight;
        return viewportHeight / CosOf(phiLayoutDeg);
    }

    /// <summary>상·하 각 여분 <c>Δ = (Height − H_view)/2</c>. φ=0 이면 0. DIU 기준(ActualHeight)이라 DPI 무관(SIM-C014).</summary>
    public static double Delta(double viewportHeight, double phiLayoutDeg)
    {
        double h = Height(viewportHeight, phiLayoutDeg);
        if (h <= 0) return 0.0;
        return (h - viewportHeight) / 2.0;
    }

    /// <summary>음수 마진 <c>(0, −Δ, 0, −Δ)</c> — 상하 대칭이라 컨트롤 중심이 뷰포트 중심에 머문다. φ=0 이면 전부 0.</summary>
    public static (double Left, double Top, double Right, double Bottom) Margin(double viewportHeight, double phiLayoutDeg)
    {
        double d = Delta(viewportHeight, phiLayoutDeg);
        return (0.0, -d, 0.0, -d);
    }

    /// <summary>
    /// inner(컨트롤 로컬/타일) 좌표 → outer(뷰포트/형제 캔버스) 좌표 — FR-05.
    /// <paramref name="controlWidth"/>/<paramref name="controlHeight"/> 는 컨트롤 ActualWidth/ActualHeight(오버스캔 포함),
    /// <paramref name="delta"/> 는 현재 레이아웃의 Δ(φ_layout 기준), <paramref name="phiDeg"/> 는 렌더 φ(ScaleY=cosφ).
    /// 컨트롤 크기가 0 이하이거나 (s≈1 ∧ φ=0 ∧ Δ≈0)이면 항등(현행 가드 승계).
    /// </summary>
    public static (double X, double Y) InnerToOuter((double X, double Y) p, double s, double phiDeg,
        double controlWidth, double controlHeight, double delta)
    {
        if (!(controlWidth > 0) || !(controlHeight > 0)) return p;
        if (IsIdentity(s, phiDeg, delta)) return p;
        double cx = controlWidth / 2.0, cy = controlHeight / 2.0;
        double cyv = cy - delta;
        double k = CosOf(phiDeg);
        return (cx + (p.X - cx) * s, cyv + (p.Y - cy) * s * k);
    }

    /// <summary>outer(뷰포트) 좌표 → inner(컨트롤 로컬) 좌표 — <see cref="InnerToOuter"/> 의 역함수(대칭). s≤0 이면 항등.</summary>
    public static (double X, double Y) OuterToInner((double X, double Y) p, double s, double phiDeg,
        double controlWidth, double controlHeight, double delta)
    {
        if (!(controlWidth > 0) || !(controlHeight > 0) || !(s > 0)) return p;
        if (IsIdentity(s, phiDeg, delta)) return p;
        double cx = controlWidth / 2.0, cy = controlHeight / 2.0;
        double cyv = cy - delta;
        double k = CosOf(phiDeg);
        return (cx + (p.X - cx) / s, cy + (p.Y - cyv) / (s * k));
    }

    private static bool IsIdentity(double s, double phiDeg, double delta)
        => Math.Abs(s - 1.0) < IdentityEpsilon && IsFlat(phiDeg) && Math.Abs(delta) < IdentityEpsilon;

    // ── 컨버터 입력 해석(순수) ─────────────────────────────────────────────

    /// <summary>바인딩 값 → double. double/float/int/long 만 인정, null·UnsetValue·기타 타입은 false.</summary>
    public static bool TryReadDouble(object? value, out double result)
    {
        switch (value)
        {
            case double d: result = d; return !double.IsNaN(d) && !double.IsInfinity(d);
            case float f: result = f; return !float.IsNaN(f) && !float.IsInfinity(f);
            case int i: result = i; return true;
            case long l: result = l; return true;
            default: result = double.NaN; return false;
        }
    }

    /// <summary>ConverterParameter 해석 — "Margin"(대소문자 무관) 또는 <see cref="TiltOverscanTarget.Margin"/> 이면 Margin, 그 외 Height.</summary>
    public static TiltOverscanTarget ParseTarget(object? parameter)
    {
        if (parameter is TiltOverscanTarget t) return t;
        if (parameter is string s && string.Equals(s.Trim(), nameof(TiltOverscanTarget.Margin), StringComparison.OrdinalIgnoreCase))
            return TiltOverscanTarget.Margin;
        return TiltOverscanTarget.Height;
    }

    /// <summary>
    /// 바인딩 값 쌍 → MainMap Height. 값이 없거나(UnsetValue/null) 뷰포트 높이가 무효이거나 φ_layout=0 이면
    /// <see cref="double.NaN"/>(WPF Height=Auto, 즉 오버스캔 <b>해제</b>) — FR-04 "φ=0 이면 Height/Margin 해제".
    /// </summary>
    public static double HeightOrAuto(object? viewportHeightValue, object? phiLayoutValue)
    {
        if (!TryReadDouble(viewportHeightValue, out double h) || h <= 0) return double.NaN;
        if (!TryReadDouble(phiLayoutValue, out double phi) || IsFlat(phi)) return double.NaN;
        return Height(h, phi);
    }

    /// <summary>바인딩 값 쌍 → MainMap Margin. 값이 없거나 φ_layout=0 이면 전부 0(해제).</summary>
    public static (double Left, double Top, double Right, double Bottom) MarginOrZero(object? viewportHeightValue, object? phiLayoutValue)
    {
        if (!TryReadDouble(viewportHeightValue, out double h) || h <= 0) return (0.0, 0.0, 0.0, 0.0);
        if (!TryReadDouble(phiLayoutValue, out double phi) || IsFlat(phi)) return (0.0, 0.0, 0.0, 0.0);
        return Margin(h, phi);
    }
}
