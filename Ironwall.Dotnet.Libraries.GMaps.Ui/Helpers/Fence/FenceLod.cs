using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence
{
    /// <summary>철망 표현 단계 — 3중 게이트(Symbol3D 플래그 AND 그룹 Render3D AND Lod==Full3D)의 마지막 항.</summary>
    public enum FenceLodLevel
    {
        /// <summary>현행 2D 폴리라인 그대로(변경 0)</summary>
        Line2D = 0,
        /// <summary>기둥 점 + 굵은 그림자 라인(철망 격자는 노이즈)</summary>
        PostsAndThickLine = 1,
        /// <summary>기둥 + 철망 + 센서 노드 3D</summary>
        Full3D = 2,
    }

    /// <summary>
    /// LOD 선택 순수 함수 (PRD FR-06, 분석 C-10). 줌 숫자가 아니라 <b>간격 1개가 차지하는 화면 픽셀</b>로 판정한다 —
    /// z18·위도 37.5° 에서 0.474 m/px 라 3m 간격은 6.3px(스토리보드 §A-3 의 12px 은 0.25 m/px 가정 오류), 디지털 줌은 RenderTransform 배율로 곱해진다.
    /// </summary>
    public static class FenceLod
    {
        /// <summary>타일 줌·위도에서의 지도 픽셀당 미터(Web Mercator). LineDrawingService.GetMetersPerPixel 과 같은 공식.</summary>
        public static double MetersPerPixel(double latitudeDeg, double tileZoom)
            => 156543.03392 * Math.Cos(latitudeDeg * Math.PI / 180.0) / Math.Pow(2.0, tileZoom);

        /// <summary>간격 1개의 화면 픽셀 = spacingM · digitalScale / mpp. 입력이 비정상이면 NaN.</summary>
        /// <remarks>
        /// 방향 의존(map-tilt-25d PRD FR-14, G7 범위 — v1 보정 없음): 지도 카드 틸트 φ 가 활성이면 뷰 변환이 ScaleTransform(s, s·cosφ) 이라
        /// 남북 방향 런의 화면 간격은 s·cosφ 배(동서 런은 s 배로 무영향)인데, 이 함수는 digitalZoomScale(s) 만 곱한다.
        /// 따라서 남북 런은 실제보다 큰 px 로 판정되어 Full3D(≥LodFullPx)/Posts(≥LodPostsPx) 경계가 방향에 따라 달라진다
        /// (φ=35° 면 cosφ≈0.819 — 예: 판정 10.0px 인 남북 런의 실제 화면 간격 ≈8.2px). 보정이 필요해지면 tiltCos 인자를 후속으로 추가한다.
        /// </remarks>
        public static double PxPerSpacing(double spacingM, double digitalZoomScale, double metersPerPixel)
        {
            if (!(spacingM > 0) || !(digitalZoomScale > 0) || !(metersPerPixel > 0)) return double.NaN;
            return spacingM * digitalZoomScale / metersPerPixel;
        }

        public static FenceLodLevel Select(double pxPerSpacing)
        {
            if (double.IsNaN(pxPerSpacing) || double.IsInfinity(pxPerSpacing)) return FenceLodLevel.Line2D;
            if (pxPerSpacing >= FenceDefaults.LodFullPx) return FenceLodLevel.Full3D;
            if (pxPerSpacing >= FenceDefaults.LodPostsPx) return FenceLodLevel.PostsAndThickLine;
            return FenceLodLevel.Line2D;
        }

        /// <summary>편의: (위도, 타일줌, 디지털 배율, 간격) → LOD</summary>
        public static FenceLodLevel Select(double latitudeDeg, double tileZoom, double digitalZoomScale, double spacingM)
            => Select(PxPerSpacing(spacingM, digitalZoomScale, MetersPerPixel(latitudeDeg, tileZoom)));

        /// <summary>3중 게이트 — 전역 3D 플래그 AND 그룹 Render3D AND Full3D.</summary>
        public static bool Show3D(bool symbol3DEnabled, bool groupRender3D, FenceLodLevel lod)
            => symbol3DEnabled && groupRender3D && lod == FenceLodLevel.Full3D;
    }
}
