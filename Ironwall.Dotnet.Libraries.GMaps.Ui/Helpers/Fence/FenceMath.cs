using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence
{
    /// <summary>
    /// 철망 런 2.5D 투영 수학 (PRD FR-05, 분석 C-1). 하우징과 같은 화면 고정 35° 피치 카메라를 쓰되,
    /// 35° 피치는 지면의 북쪽 거리를 sin35(≈0.574)배로 단축하므로 라인 정합을 위해 지오메트리 Z 를 <see cref="GroundStretch"/>(=1/sin35)로 선-신장한다.
    /// 그 결과 지면 (x,z) → 화면 (cx+x, cy−z) 항등, 높이 y → −y·cos35.
    /// Symbols3D/HousingMath.Pitch(35) 와 같은 값이어야 한다 — Housing.Tests 에서 동일성 단언(HousingFencePitchTests.should_share_pitch_between_housing_and_fence).
    /// 지도 카드 틸트(ScaleY=cosφ)를 조상 RenderTransform 으로 승계(V-02 헤드리스 확정 2026-09-08) — 링:지도지면 상대비 0.574 불변,
    /// 철망은 2D 폴리라인과 같은 Canvas 라 정합 유지. 이 클래스는 틸트 φ 를 모르며 역보정하지 않는다(map-tilt-25d PRD FR-14 정책 (i)).
    /// </summary>
    public static class FenceMath
    {
        public const double PitchDeg = 35.0;
        public static readonly double SinPitch = Math.Sin(PitchDeg * Math.PI / 180.0);
        public static readonly double CosPitch = Math.Cos(PitchDeg * Math.PI / 180.0);
        /// <summary>루트 ScaleTransform3D(-1, 1, GroundStretch) 에 쓰는 Z 배율(X 는 −1 거울상 보정 — FenceRunVisual/FenceMirrorTests).</summary>
        public static readonly double GroundStretch = 1.0 / SinPitch;

        /// <summary>직교 카메라 거리 하한(px). 프레임이 작을 때의 종전 고정값.</summary>
        public const double MinCameraDistance = 4096;
        /// <summary>직교 카메라 거리 상한(px) — 소프트웨어 래스터라이저(RenderTargetBitmap·RDP Tier0) 실측: D=8192 정상, D≥16384 면 장면 전체가 렌더되지 않는다(2026-09-08 검증). 그 아래로 고정.</summary>
        public const double MaxCameraDistance = 8192;
        /// <summary>근평면 거리 — 종전 1 은 프레임 높이 ≈5.7k px 부터 남쪽 구간을 잘랐다(C18). 카메라 거리가 프레임에 비례하므로 0.1 로 두어도 안전.</summary>
        public const double CameraNearPlane = 0.1;

        /// <summary>
        /// 프레임 높이(px)·철망 높이(px)에 비례하는 카메라 거리(C18). 최소 장면 깊이 = D − h·sin35 − H·cos35/(2·sin35) 가
        /// 근평면보다 커야 하므로 D = 0.75·H + h + 16 이면 잔여 깊이 ≥ 0.036·H + 0.43·h + 16 &gt; 0. <see cref="MaxCameraDistance"/> 로 상한.
        /// </summary>
        public static double CameraDistance(double frameHeightPx, double fenceHeightPx)
        {
            double h = double.IsFinite(frameHeightPx) ? Math.Max(0, frameHeightPx) : 0;
            double f = double.IsFinite(fenceHeightPx) ? Math.Max(0, fenceHeightPx) : 0;
            return Math.Clamp(h * 0.75 + f + 16, MinCameraDistance, MaxCameraDistance);
        }

        /// <summary>
        /// 장면에서 카메라에 가장 가까운 점(남단 Z=−H → z'=−H/(2·sin35), 꼭대기 y=철망 높이)의 깊이.
        /// 카메라 P=(0, D·sin, −D·cos)·Look=(0, −sin, cos) 에서 깊이 = D − y·sin35 + z'·cos35. 이 값이 <see cref="CameraNearPlane"/> 미만이면 남쪽 구간이 잘린다.
        /// </summary>
        public static double MinSceneDepth(double cameraDistance, double frameHeightPx, double fenceHeightPx)
            => cameraDistance - fenceHeightPx * SinPitch - frameHeightPx * CosPitch / (2.0 * SinPitch);

        /// <summary>모델 좌표(x=동 px, y=높이 px, z=북 px) → 화면 좌표. Z 신장이 적용된 카메라 결과와 일치해야 한다.</summary>
        public static (double X, double Y) Project(double x, double y, double z, double cx, double cy)
            => (cx + x, cy - z - y * CosPitch);

        /// <summary>철망 높이(m) → <b>실척</b> 화면 픽셀 높이(타일줌 px). 디지털 줌은 RenderTransform 이 승계하므로 곱하지 않는다.</summary>
        public static double HeightPx(double fenceHeightM, double metersPerPixel)
            => metersPerPixel > 0 ? fenceHeightM / metersPerPixel : 0;

        /// <summary>
        /// 높이 과장 배율 — <paramref name="referenceHeightM"/> 가 최소 <paramref name="minVisualPx"/> px 로 보이게 하는 배율.
        /// 개별 철망 높이와 <b>무관</b>한 값이라(줌·위도에만 의존) 이걸 곱한 높이 px 는 실제 높이(m)에 정비례한다.
        /// 충분히 확대해 기준 실척이 하한을 넘으면 1(과장 없음 — 그때부터는 순수 실척).
        /// </summary>
        public static double HeightExaggeration(double metersPerPixel, double referenceHeightM, double minVisualPx)
        {
            double referencePx = HeightPx(referenceHeightM, metersPerPixel);
            if (!(referencePx > 0) || !(minVisualPx > 0) || !double.IsFinite(minVisualPx)) return 1.0;
            return Math.Max(1.0, minVisualPx / referencePx);
        }

        /// <summary>
        /// 화면에 그릴 철망 높이(px) = 실척 × <see cref="HeightExaggeration"/>.
        /// 종전 <c>Math.Max(실척, 하한)</c> 은 슬라이더 전 범위의 실척이 하한 아래라 높이를 하나의 값으로 평탄화했고,
        /// 그 결과 프레임 해시가 변하지 않아 <c>DecideFrame</c> 이 Skip 을 돌려 <b>높이 조절이 심볼에 반영되지 않았다</b>
        /// (사용자 보고 2026-09-08). 비율 과장은 단조증가라 높이를 올리면 반드시 높아진다.
        /// </summary>
        public static double VisualHeightPx(double fenceHeightM, double metersPerPixel, double referenceHeightM, double minVisualPx)
            => HeightPx(fenceHeightM, metersPerPixel) * HeightExaggeration(metersPerPixel, referenceHeightM, minVisualPx);

        /// <summary>
        /// 속성창 슬라이더 값 정규화 — 눈금 격자로 양자화한 뒤 [min, max] 로 클램프한다.
        /// WPF <c>IsSnapToTickEnabled</c> 는 스냅 값을 <c>Minimum + n×TickFrequency</c> 로 <b>double 누산</b>해 구하므로
        /// 3.0000000000000004 · 2.9999999999999996 같은 잔차가 남는다. 라벨은 <c>{0:0.0}</c> 이라 가려지지만 모델·Undo 엔트리·
        /// DB·프레임 해시에는 그대로 들어가고 다른 표면에서 노출된다(사용자 보고 2026-09-08). 저장 경로 입구에서 한 번에 정리한다.
        /// <paramref name="step"/> 이 0 이하/비유한이면 양자화 없이 잔차 정리와 클램프만 한다.
        /// </summary>
        public static double Quantize(double value, double min, double max, double step)
        {
            double v = double.IsFinite(value) ? value : min;
            if (step > 0 && double.IsFinite(step)) v = Math.Round(v / step, MidpointRounding.AwayFromZero) * step;
            v = Math.Round(v, 3);                     // n×step 자체의 이진 잔차 제거(예: 15×0.1 = 1.5000000000000002)
            return Math.Clamp(v, min, max);
        }

        /// <summary>구간(패널) 하나의 삼각형 수 추정(보수적 상한) — 기둥 Box 12 + 레일 24(실구현은 Tube sides=4 ×2 = 16) + 패널 쿼드 2(양면 재질) + 노드 Box 12(센서 모드).</summary>
        public static int TrianglesPerSegment(bool sensorMode) => 12 + 24 + 2 + (sensorMode ? 12 : 0);

        /// <summary>
        /// 예산 초과 여부 — 1km/3m(333 구간) 기준 <see cref="FenceDefaults.TriangleBudgetPerKm"/>.
        /// <paramref name="totalLengthM"/> 은 반드시 <b>미터</b>다 — px 프레임 레이아웃(FenceRunVisual.Layout)의 TotalLengthM 은 px 이므로
        /// 호출자가 metersPerPixel 을 곱해 넘겨야 한다(C17: px 를 m 로 넘기면 예산이 1/mpp 배 부풀어 Full3D 에서 절대 초과하지 않는다).
        /// </summary>
        public static bool ExceedsBudget(int segmentCount, bool sensorMode, double totalLengthM)
        {
            double km = Math.Max(totalLengthM / 1000.0, 1e-9);
            return segmentCount * TrianglesPerSegment(sensorMode) > FenceDefaults.TriangleBudgetPerKm * Math.Max(1.0, km);
        }
    }
}
