namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence
{
    /// <summary>
    /// 3D 철망·통문 상수 SSOT (PRD pidsgroup-3d-fence-gate T-A07). WPF/GMap.NET 무의존 — tests/GMaps.Ui.Tests 에 소스 링크된다.
    /// 값의 근거: D1(슬라이더 1.0~10.0·0.5·기본 3.0), 스토리보드 §A-4(통문 스냅 1.5m), 분석 C-10(LOD 는 간격 픽셀 기준), drag-first-ux(데드존 8 DIU).
    /// </summary>
    public static class FenceDefaults
    {
        /// <summary>전역 기본 기둥 간격(m). 그룹 값이 NULL 이면 appsettings <c>Symbol3D.FencePostSpacingM</c>, 그것도 없으면 이 값.</summary>
        public const double PostSpacingM = 3.0;
        public const double PostSpacingMinM = 1.0;
        public const double PostSpacingMaxM = 10.0;
        public const double PostSpacingStepM = 0.5;

        /// <summary>철망 높이(m) 기본/범위 — 속성창 슬라이더 1.5~4.0(0.1)</summary>
        public const double FenceHeightM = 2.4;
        public const double FenceHeightMinM = 1.5;
        public const double FenceHeightMaxM = 4.0;
        public const double FenceHeightStepM = 0.1;
        /// <summary>
        /// 3D 철망 화면 높이 하한(px) — 실척(2.4 m ≈ 5 px @z18)으로는 높이 차이가 안 보인다(사용자 지적 2026-09-08).
        /// <b>기준 높이</b>(<see cref="FenceHeightM"/>)가 이 픽셀 이상으로 보이도록 <see cref="FenceMath.HeightExaggeration"/> 배율을
        /// 계산해 <b>모든 높이에 똑같이</b> 곱한다 — 높이 px 는 실제 높이(m)에 정비례한다.
        /// (종전엔 <c>Math.Max(실척, 12)</c> 로 잘라, 슬라이더 범위 1.5~4.0 m 의 실척이 전부 12 px 미만이라 어떤 높이를 골라도
        ///  12 px 로 평탄화되어 높이 조절이 화면에 반영되지 않았다.)
        /// </summary>
        public const double MinVisualHeightPx = 12.0;

        /// <summary>통문 폭(m) 기본/범위</summary>
        public const double GateWidthM = 4.0;
        public const double GateWidthMinM = 1.0;
        public const double GateWidthMaxM = 8.0;
        public const double GateWidthStepM = 0.5;

        /// <summary>통문 중심이 변에서 이 거리(m) 이내면 변 위로 스냅해 패널을 절개한다.</summary>
        public const double GateSnapM = 1.5;

        /// <summary>코너 기둥을 1.5× 굵게 하는 내각 임계(도).</summary>
        public const double ThickCornerAngleDeg = 100.0;

        /// <summary>LOD 임계 — 화면에서 간격 1개가 차지하는 픽셀. ≥ Full 이면 3D 철망, ≥ Posts 면 기둥점+굵은 라인, 그 외 현행 2D.</summary>
        public const double LodFullPx = 10.0;
        public const double LodPostsPx = 4.0;

        /// <summary>드래그 드로잉: 단순화 허용 오차(지도 px)·최소 점 간격(m)·캡처 드래그 데드존(DIU, 사내 규칙 8.0 고정)</summary>
        public const double SimplifyEpsilonPx = 2.0;
        public const double MinVertexSpacingM = 1.0;
        public const double DragDeadZoneDiu = 8.0;

        /// <summary>문짝 열림 각(도) — 통문 양개 −80°, 함체 도어 −75°. 애니메이션 400ms.</summary>
        public const double GateOpenAngleDeg = -80.0;
        public const double EnclosureOpenAngleDeg = -75.0;
        public const int DoorAnimationMs = 400;

        /// <summary>속성창 슬라이더 지연 커밋(ms) — 드래그 중 라벨만 갱신, 커밋(DB·Undo)은 1회.</summary>
        public const int SliderCommitDelayMs = 150;

        /// <summary>333 구간(1km/3m) 기준 삼각형 예산 — 초과 시 진단 경고.</summary>
        public const int TriangleBudgetPerKm = 20000;
    }
}
