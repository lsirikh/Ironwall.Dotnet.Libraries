using System;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Providers.Ptz;

/****************************************************************************
   Purpose      : 영상 위 드래그 → PTZ 상대 이동 환산(순수 수학) — camera-popup drag PTZ
   Created By   : Claude Code
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>드래그 한 번을 카메라에 보내는 방식(우선순위 순).</summary>
public enum PtzDragMoveKind
{
    /// <summary>보낼 방법이 없다(좌표 공간 없음).</summary>
    None = 0,
    /// <summary>RelativeMove · 화각 상대 공간(TranslationSpaceFov) — ±1 = 화면 가장자리. 줌을 카메라가 스스로 반영한다.</summary>
    RelativeFov = 1,
    /// <summary>RelativeMove · 일반 상대 공간 — 기억해 둔 줌으로 화각을 어림해 이동량을 정한다.</summary>
    RelativeGeneric = 2,
    /// <summary>AbsoluteMove — 기억 · 추적한 위치 + 이동량.</summary>
    Absolute = 3,
    /// <summary>ContinuousMove 를 드래그 길이만큼의 시간 뒤 정지(옛 방식 — 위치도 줌도 모를 때의 마지막 수단).</summary>
    ContinuousPulse = 4,
}

/// <summary>
/// 렌즈 · 구동부 어림값(ONVIF 일반 공간은 도 단위를 알려주지 않는다). 광각 수평 화각 · 광학 줌 배율 ·
/// 정규화 좌표 −1..1 이 덮는 팬 · 틸트 각도. 값이 실제와 달라도 방향은 맞고 크기만 비례해 어긋난다.
/// </summary>
public sealed record PtzLensModel(double WideHorizontalFovDeg, double ZoomRatio, double PanSpanDeg, double TiltSpanDeg)
{
    /// <summary>일반 PTZ 돔 기준: 광각 60° · 30배 · 팬 360° · 틸트 180°(틸트 범위가 더 좁은 카메라에서는 덜 가는 쪽 — 안전한 쪽).</summary>
    public static readonly PtzLensModel Default = new(60d, 30d, 360d, 180d);
}

/// <summary>
/// 드래그 환산에 쓰는 카메라 좌표 공간 요약(GetNode 캐시에서 뽑는다 — 드래그마다 카메라에 묻지 않는다).
/// </summary>
public sealed record PtzDragSpaces(
    bool HasRelativeFov, double FovXMin, double FovXMax, double FovYMin, double FovYMax,
    bool HasRelativeGeneric, double RelXMin, double RelXMax, double RelYMin, double RelYMax,
    bool HasAbsolute, double AbsXMin, double AbsXMax, double AbsYMin, double AbsYMax,
    double ZoomMin, double ZoomMax,
    bool HasContinuous);

/// <summary>기억 · 추적한 카메라 위치. <see cref="PanTiltKnown"/> · <see cref="ZoomKnown"/> 이 false 면 그 값은 믿지 않는다.</summary>
public readonly record struct PtzKnownPosition(double Pan, double Tilt, double Zoom, bool PanTiltKnown, bool ZoomKnown)
{
    public static readonly PtzKnownPosition Unknown = new(0, 0, 0, false, false);
}

/// <summary>드래그 환산 결과. <see cref="Kind"/> 에 따라 Pan/Tilt 의 뜻이 다르다(상대 이동량 · 절대 위치 · 속도).</summary>
public readonly record struct PtzDragPlan(PtzDragMoveKind Kind, double Pan, double Tilt, int PulseMs = 0);

/// <summary>
/// 드래그 벡터(영상 상자 크기에 대한 비율: 오른쪽 +, 아래 +) → 카메라 이동. WPF · ONVIF 인스턴스 비의존.
/// <para>계약: 드래그 방향으로 카메라가 돈다(오른쪽으로 끌면 오른쪽을 본다 · 위로 끌면 위를 본다). 크기는
/// "화면 중심에서 그 벡터만큼 떨어진 점이 중심으로 온다" — 줌이 깊을수록 같은 드래그가 더 적게 움직인다.</para>
/// </summary>
public static class PtzDragMath
{
    public const double DefaultAspect = 16d / 9d;

    // 옛 시간 제한 연속 이동(드래그 길이 비례) 상수 — 위치 · 줌을 모를 때의 대체 수단으로만 남긴다.
    public const int PulseMaxMs = 700;
    public const int PulseMinMs = 120;
    public const double PulseSpeed = 0.6;
    private const double PulseSaturation = 0.65;   // 영상 짧은 변의 65% 를 끌면 최대

    /// <summary>방식 고르기 — 화각 상대 → 일반 상대(줌을 알 때) → 절대(위치를 알 때) → 시간 제한 연속 → 일반 상대(줌 모름 = 광각 가정).</summary>
    /// <param name="hasAbsoluteWithPosition">절대 공간이 있고 팬 · 틸트 위치를 안다.</param>
    /// <param name="zoomKnown">줌(= 지금 화각)을 안다.</param>
    public static PtzDragMoveKind SelectKind(bool hasRelativeFov, bool hasRelativeGeneric, bool hasAbsoluteWithPosition, bool zoomKnown, bool hasContinuous)
    {
        if (hasRelativeFov) return PtzDragMoveKind.RelativeFov;
        if (hasRelativeGeneric && zoomKnown) return PtzDragMoveKind.RelativeGeneric;
        if (hasAbsoluteWithPosition && zoomKnown) return PtzDragMoveKind.Absolute;
        if (hasContinuous) return PtzDragMoveKind.ContinuousPulse;
        return hasRelativeGeneric ? PtzDragMoveKind.RelativeGeneric : PtzDragMoveKind.None;
    }

    /// <summary>
    /// 드래그 한 번의 계획(순수): 방식 고르기 + 이동량 환산 + 한계 자르기. 벡터가 0 이면 <see cref="PtzDragMoveKind.None"/>.
    /// </summary>
    public static PtzDragPlan Plan(PtzDragSpaces spaces, PtzKnownPosition position, double viewX, double viewY, double viewAspect, PtzLensModel? lens = null)
    {
        lens ??= PtzLensModel.Default;
        double fx = SanitizeFraction(viewX), fy = SanitizeFraction(viewY);
        if (Math.Abs(fx) < 1e-6 && Math.Abs(fy) < 1e-6) return new PtzDragPlan(PtzDragMoveKind.None, 0, 0);
        bool panWraps = lens.PanSpanDeg >= 360d;
        var kind = SelectKind(spaces.HasRelativeFov, spaces.HasRelativeGeneric, spaces.HasAbsolute && position.PanTiltKnown,
            position.ZoomKnown, spaces.HasContinuous);
        switch (kind)
        {
            case PtzDragMoveKind.RelativeFov:
            {
                var (pan, tilt) = FovTranslation(fx, fy, spaces.FovXMin, spaces.FovXMax, spaces.FovYMin, spaces.FovYMax);
                return new PtzDragPlan(kind, pan, tilt);
            }
            case PtzDragMoveKind.RelativeGeneric:
            case PtzDragMoveKind.Absolute:
            {
                double zoom = position.ZoomKnown ? NormalizeZoom(position.Zoom, spaces.ZoomMin, spaces.ZoomMax) : 0d;
                double xSpan = spaces.HasAbsolute ? Math.Abs(spaces.AbsXMax - spaces.AbsXMin) : 2d;
                double ySpan = spaces.HasAbsolute ? Math.Abs(spaces.AbsYMax - spaces.AbsYMin) : 2d;
                if (kind == PtzDragMoveKind.Absolute)
                {
                    var (dPan, dTilt) = GenericTranslation(fx, fy, viewAspect, zoom, lens, xSpan, ySpan,
                        double.NegativeInfinity, double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity);
                    var (toPan, toTilt) = AbsoluteTarget(position.Pan, position.Tilt, dPan, dTilt,
                        spaces.AbsXMin, spaces.AbsXMax, spaces.AbsYMin, spaces.AbsYMax, panWraps);
                    return new PtzDragPlan(kind, toPan, toTilt);
                }
                var (pan, tilt) = GenericTranslation(fx, fy, viewAspect, zoom, lens, xSpan, ySpan,
                    spaces.RelXMin, spaces.RelXMax, spaces.RelYMin, spaces.RelYMax);
                if (position.PanTiltKnown && spaces.HasAbsolute)
                    (pan, tilt) = ClampToTravel(position.Pan, position.Tilt, pan, tilt,
                        spaces.AbsXMin, spaces.AbsXMax, spaces.AbsYMin, spaces.AbsYMax, panWraps);
                return new PtzDragPlan(kind, pan, tilt);
            }
            case PtzDragMoveKind.ContinuousPulse:
                return ContinuousPulse(fx, fy, viewAspect);
            default:
                return new PtzDragPlan(PtzDragMoveKind.None, 0, 0);
        }
    }

    /// <summary>유한하지 않은 값은 0, 나머지는 −1..1 로(드래그는 상자 밖으로 나가도 한 화면까지만).</summary>
    public static double SanitizeFraction(double v) => double.IsFinite(v) ? Math.Clamp(v, -1d, 1d) : 0d;

    public static double SanitizeAspect(double aspect) => double.IsFinite(aspect) && aspect > 0.1 && aspect < 10 ? aspect : DefaultAspect;

    /// <summary>정규화 줌(0 = 광각, 1 = 최대 망원) → 수평 화각(도). 초점거리가 줌에 선형이라고 본다.</summary>
    public static double HorizontalFovDeg(double zoomNormalized, PtzLensModel lens)
    {
        double z = double.IsFinite(zoomNormalized) ? Math.Clamp(zoomNormalized, 0d, 1d) : 0d;
        double ratio = 1d + z * (Math.Max(1d, lens.ZoomRatio) - 1d);
        double wideHalf = DegToRad(Math.Clamp(lens.WideHorizontalFovDeg, 1d, 170d) / 2d);
        return RadToDeg(2d * Math.Atan(Math.Tan(wideHalf) / ratio));
    }

    /// <summary>수평 화각 + 가로세로비 → 수직 화각(도).</summary>
    public static double VerticalFovDeg(double horizontalFovDeg, double aspect)
        => RadToDeg(2d * Math.Atan(Math.Tan(DegToRad(horizontalFovDeg / 2d)) / SanitizeAspect(aspect)));

    /// <summary>절대 좌표 값 → 정규화 줌(0..1). 범위가 뒤집혔거나 0 폭이면 0.</summary>
    public static double NormalizeZoom(double zoom, double zoomMin, double zoomMax)
    {
        if (!(zoomMax > zoomMin) || !double.IsFinite(zoom)) return 0d;
        return Math.Clamp((zoom - zoomMin) / (zoomMax - zoomMin), 0d, 1d);
    }

    /// <summary>
    /// 화각 상대 공간 이동량: ±1 = 화면 가장자리(중심에서 반 너비). 드래그 비율 f 는 상자 전체 기준이므로 2f.
    /// 화면 아래(+)로 끌면 틸트 내림(−).
    /// </summary>
    public static (double Pan, double Tilt) FovTranslation(double viewX, double viewY,
        double xMin, double xMax, double yMin, double yMax)
    {
        double pan = PtzCoordinateMath.Clamp(2d * SanitizeFraction(viewX), xMin, xMax);
        double tilt = PtzCoordinateMath.Clamp(-2d * SanitizeFraction(viewY), yMin, yMax);
        return (pan, tilt);
    }

    /// <summary>
    /// 일반 상대 공간 이동량: 드래그 비율 × 지금 화각(도) ÷ 좌표 1 단위가 덮는 각도. 절대 공간 폭(기본 2 = −1..1)이
    /// <see cref="PtzLensModel.PanSpanDeg"/> · <see cref="PtzLensModel.TiltSpanDeg"/> 를 덮는다고 본다. 결과는 상대 공간 범위로 자른다.
    /// </summary>
    public static (double Pan, double Tilt) GenericTranslation(double viewX, double viewY, double viewAspect, double zoomNormalized,
        PtzLensModel lens, double absXSpan, double absYSpan, double relXMin, double relXMax, double relYMin, double relYMax)
    {
        double hfov = HorizontalFovDeg(zoomNormalized, lens);
        double vfov = VerticalFovDeg(hfov, viewAspect);
        double xSpan = absXSpan > 0 ? absXSpan : 2d;
        double ySpan = absYSpan > 0 ? absYSpan : 2d;
        double pan = SanitizeFraction(viewX) * hfov / Math.Max(1d, lens.PanSpanDeg) * xSpan;
        double tilt = -SanitizeFraction(viewY) * vfov / Math.Max(1d, lens.TiltSpanDeg) * ySpan;
        return (PtzCoordinateMath.Clamp(pan, relXMin, relXMax), PtzCoordinateMath.Clamp(tilt, relYMin, relYMax));
    }

    /// <summary>
    /// 남은 구동 범위로 이동량을 자른다(한계에 닿은 축이 범위 밖으로 나가지 않게). 팬이 한 바퀴 도는 카메라(<paramref name="panWraps"/>)는
    /// 팬을 자르지 않는다 — 끝을 넘어 반대쪽으로 이어진다.
    /// </summary>
    public static (double Pan, double Tilt) ClampToTravel(double currentPan, double currentTilt, double deltaPan, double deltaTilt,
        double xMin, double xMax, double yMin, double yMax, bool panWraps)
    {
        double pan = panWraps ? deltaPan : PtzCoordinateMath.Clamp(currentPan + deltaPan, xMin, xMax) - PtzCoordinateMath.Clamp(currentPan, xMin, xMax);
        double tilt = PtzCoordinateMath.Clamp(currentTilt + deltaTilt, yMin, yMax) - PtzCoordinateMath.Clamp(currentTilt, yMin, yMax);
        return (pan, tilt);
    }

    /// <summary>절대 목표 = 지금 위치 + 이동량. 팬이 한 바퀴 도는 카메라는 범위를 넘으면 반대쪽으로 감고, 아니면 자른다. 틸트는 항상 자른다.</summary>
    public static (double Pan, double Tilt) AbsoluteTarget(double currentPan, double currentTilt, double deltaPan, double deltaTilt,
        double xMin, double xMax, double yMin, double yMax, bool panWraps)
    {
        if (xMin > xMax) (xMin, xMax) = (xMax, xMin);
        double pan = currentPan + deltaPan;
        double width = xMax - xMin;
        if (panWraps && width > 0)
        {
            while (pan > xMax) pan -= width;
            while (pan < xMin) pan += width;
        }
        else
        {
            pan = PtzCoordinateMath.Clamp(pan, xMin, xMax);
        }
        return (pan, PtzCoordinateMath.Clamp(currentTilt + deltaTilt, yMin, yMax));
    }

    /// <summary>
    /// 시간 제한 연속 이동(옛 방식): 방향 = 드래그 방향, 속도 · 시간 = 드래그 길이 비례(짧은 변의 65% 에서 최대).
    /// 아주 짧은 드래그도 <see cref="PulseMinMs"/> 는 움직인다.
    /// </summary>
    public static PtzDragPlan ContinuousPulse(double viewX, double viewY, double viewAspect, double speed = PulseSpeed)
    {
        double aspect = SanitizeAspect(viewAspect);
        double px = SanitizeFraction(viewX) * aspect;   // 세로 = 1 로 둔 길이
        double py = SanitizeFraction(viewY);
        double length = Math.Sqrt((px * px) + (py * py));
        if (length < 1e-6) return new PtzDragPlan(PtzDragMoveKind.None, 0, 0);
        double magnitude = Math.Min(1d, length / (PulseSaturation * Math.Min(aspect, 1d)));
        double velocity = Math.Clamp(speed, 0.1, 1d) * (0.3 + (0.7 * magnitude));
        int ms = Math.Max(PulseMinMs, (int)(magnitude * PulseMaxMs));
        return new PtzDragPlan(PtzDragMoveKind.ContinuousPulse, px / length * velocity, -(py / length) * velocity, ms);
    }

    private static double DegToRad(double deg) => deg * Math.PI / 180d;
    private static double RadToDeg(double rad) => rad * 180d / Math.PI;
}
