using System;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// GMapCustomControl 버그 수정 순수 로직 단위 테스트
///
/// 검증 대상:
///   FP-4 — ResizeBoundsWithRatio expand 판정 (지배적 축 방식)
///   FP-5 — _dragStartPoint 갱신 누적 delta 억제 (상태 시뮬레이션)
///   FP-6 — ResizeBoundsFree (long)Math.Round vs (long) 직접 캐스트 차이
///
/// 테스트 전략:
///   GMapCustomControl 은 WPF UIElement 상속이므로 직접 인스턴스화 불가.
///   FP-4/5/6 의 핵심 판정 식은 순수 산술이므로
///   동일한 식을 static 헬퍼로 추출하여 WPF 의존 없이 검증한다.
/// </summary>
public class ResizeBoundsLogicTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // 헬퍼: GMapCustomControl.cs 에서 추출한 동일 식
    // ─────────────────────────────────────────────────────────────────────────

    public enum ResizeHandle
    {
        None,
        TopLeft, TopCenter, TopRight,
        MiddleLeft, MiddleRight,
        BottomLeft, BottomCenter, BottomRight,
        Move
    }

    /// <summary>
    /// [FP-4] 수정 후 expand 판정 — 지배적 축 방식
    /// GMapCustomControl.ResizeBoundsWithRatio 에서 동일 식을 사용한다.
    /// </summary>
    private static bool ComputeExpandDominantAxis(double deltaX, double deltaY, ResizeHandle corner)
    {
        return corner switch
        {
            ResizeHandle.TopLeft =>
                Math.Abs(deltaX) >= Math.Abs(deltaY) ? deltaX < 0 : deltaY < 0,
            ResizeHandle.TopRight =>
                Math.Abs(deltaX) >= Math.Abs(deltaY) ? deltaX > 0 : deltaY < 0,
            ResizeHandle.BottomLeft =>
                Math.Abs(deltaX) >= Math.Abs(deltaY) ? deltaX < 0 : deltaY > 0,
            ResizeHandle.BottomRight =>
                Math.Abs(deltaX) >= Math.Abs(deltaY) ? deltaX > 0 : deltaY > 0,
            _ => false
        };
    }

    /// <summary>
    /// [FP-4] 버그가 있던 OR 조건 expand 판정 (회귀 비교용)
    /// </summary>
    private static bool ComputeExpandOr(double deltaX, double deltaY, ResizeHandle corner)
    {
        return corner switch
        {
            ResizeHandle.TopLeft => deltaX < 0 || deltaY < 0,
            ResizeHandle.TopRight => deltaX > 0 || deltaY < 0,
            ResizeHandle.BottomLeft => deltaX < 0 || deltaY > 0,
            ResizeHandle.BottomRight => deltaX > 0 || deltaY > 0,
            _ => false
        };
    }

    /// <summary>
    /// [FP-6] 수정 후: Math.Round 반올림
    /// </summary>
    private static long ApplyDeltaRound(double delta) => (long)Math.Round(delta);

    /// <summary>
    /// [FP-6] 버그가 있던 직접 캐스트 (회귀 비교용)
    /// </summary>
    private static long ApplyDeltaCast(double delta) => (long)delta;

    // ─────────────────────────────────────────────────────────────────────────
    // FP-4: expand 판정 — X축 지배 케이스 (|deltaX| > |deltaY|)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>TC-07 대응: TopLeft 핸들, X지배, deltaX > 0 (오른쪽=축소) → expand=false</summary>
    [Fact]
    public void should_return_shrink_when_TopLeft_and_dominant_X_moves_right()
    {
        // PRD FP-4 반례: deltaX=+15, deltaY=-3 → OR조건 버그 시 expand=true 였음
        double deltaX = 15;
        double deltaY = -3;

        bool actual = ComputeExpandDominantAxis(deltaX, deltaY, ResizeHandle.TopLeft);

        Assert.False(actual); // 지배적 축=X, deltaX > 0 = 오른쪽 = 축소
    }

    /// <summary>TC-07 대응: OR 조건 버그는 위 케이스에서 expand=true(오류)를 반환함을 확인</summary>
    [Fact]
    public void should_demonstrate_or_bug_returns_expand_when_dominant_axis_says_shrink()
    {
        double deltaX = 15;
        double deltaY = -3;

        bool buggy = ComputeExpandOr(deltaX, deltaY, ResizeHandle.TopLeft);
        bool fixed_ = ComputeExpandDominantAxis(deltaX, deltaY, ResizeHandle.TopLeft);

        Assert.True(buggy);   // OR 조건 버그: expand 오판
        Assert.False(fixed_); // 지배적 축 방식: 올바른 shrink
    }

    /// <summary>TC-08 대응: TopLeft 핸들, Y지배, deltaY < 0 (위=확대) → expand=true</summary>
    [Fact]
    public void should_return_expand_when_TopLeft_and_dominant_Y_moves_up()
    {
        double deltaX = 3;
        double deltaY = -20;

        bool actual = ComputeExpandDominantAxis(deltaX, deltaY, ResizeHandle.TopLeft);

        Assert.True(actual); // 지배적 축=Y, deltaY < 0 = 위 = 확대
    }

    /// <summary>TC-09 대응: BottomRight 핸들, X지배, deltaX < 0 (왼쪽=축소) → expand=false</summary>
    [Fact]
    public void should_return_shrink_when_BottomRight_and_dominant_X_moves_left()
    {
        // PRD FP-4 반례: deltaX=-20, deltaY=+4 → OR조건 버그 시 expand=true
        double deltaX = -20;
        double deltaY = 4;

        bool actual = ComputeExpandDominantAxis(deltaX, deltaY, ResizeHandle.BottomRight);

        Assert.False(actual); // 지배적 축=X, deltaX < 0 = 왼쪽 = 축소
    }

    /// <summary>TC-09 대응: BottomRight OR 버그 확인</summary>
    [Fact]
    public void should_demonstrate_or_bug_for_BottomRight_dominant_X_left()
    {
        double deltaX = -20;
        double deltaY = 4;

        bool buggy = ComputeExpandOr(deltaX, deltaY, ResizeHandle.BottomRight);
        bool fixed_ = ComputeExpandDominantAxis(deltaX, deltaY, ResizeHandle.BottomRight);

        Assert.True(buggy);   // OR 버그: deltaY=+4 > 0 이므로 expand 오판
        Assert.False(fixed_); // 수정: 지배적 축 X, deltaX < 0 = 축소
    }

    /// <summary>TC-10 대응: BottomRight 핸들, X지배, deltaX > 0 (오른쪽=확대) → expand=true</summary>
    [Fact]
    public void should_return_expand_when_BottomRight_and_dominant_X_moves_right()
    {
        double deltaX = 20;
        double deltaY = -3;

        bool actual = ComputeExpandDominantAxis(deltaX, deltaY, ResizeHandle.BottomRight);

        Assert.True(actual); // 지배적 축=X, deltaX > 0 = 오른쪽 = 확대
    }

    /// <summary>TC-11 대응: TopRight 핸들, X지배, deltaX > 0 → expand=true</summary>
    [Fact]
    public void should_return_expand_when_TopRight_and_dominant_X_moves_right()
    {
        double deltaX = 15;
        double deltaY = 3;

        bool actual = ComputeExpandDominantAxis(deltaX, deltaY, ResizeHandle.TopRight);

        Assert.True(actual);
    }

    /// <summary>TC-11 대응: TopRight 핸들, X지배, deltaX < 0 (왼쪽=축소) → expand=false</summary>
    [Fact]
    public void should_return_shrink_when_TopRight_and_dominant_X_moves_left()
    {
        double deltaX = -15;
        double deltaY = 3;

        bool actual = ComputeExpandDominantAxis(deltaX, deltaY, ResizeHandle.TopRight);

        Assert.False(actual);
    }

    /// <summary>TC-11 대응: TopRight 핸들, Y지배, deltaY < 0 (위=확대) → expand=true</summary>
    [Fact]
    public void should_return_expand_when_TopRight_and_dominant_Y_moves_up()
    {
        double deltaX = 2;
        double deltaY = -20;

        bool actual = ComputeExpandDominantAxis(deltaX, deltaY, ResizeHandle.TopRight);

        Assert.True(actual);
    }

    /// <summary>TC-11 대응: BottomLeft 핸들, X지배, deltaX < 0 (왼쪽=확대) → expand=true</summary>
    [Fact]
    public void should_return_expand_when_BottomLeft_and_dominant_X_moves_left()
    {
        double deltaX = -15;
        double deltaY = 3;

        bool actual = ComputeExpandDominantAxis(deltaX, deltaY, ResizeHandle.BottomLeft);

        Assert.True(actual);
    }

    /// <summary>TC-11 대응: BottomLeft 핸들, Y지배, deltaY > 0 (아래=확대) → expand=true</summary>
    [Fact]
    public void should_return_expand_when_BottomLeft_and_dominant_Y_moves_down()
    {
        double deltaX = 2;
        double deltaY = 20;

        bool actual = ComputeExpandDominantAxis(deltaX, deltaY, ResizeHandle.BottomLeft);

        Assert.True(actual);
    }

    /// <summary>동점 케이스: |deltaX| == |deltaY|, >= 조건으로 X축 우선 처리</summary>
    [Fact]
    public void should_prefer_X_axis_when_deltaX_and_deltaY_magnitudes_are_equal()
    {
        // TopLeft: |deltaX|==|deltaY| → >= 조건으로 X축 선택 → deltaX=+10=오른쪽=축소
        double deltaX = 10;
        double deltaY = -10;

        bool actual = ComputeExpandDominantAxis(deltaX, deltaY, ResizeHandle.TopLeft);

        // X축이 선택되고 deltaX > 0 이므로 TopLeft 에서는 shrink
        Assert.False(actual);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FP-5: _dragStartPoint 무조건 갱신 — 누적 delta 억제 시뮬레이션
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-12 대응: bounds 유효성 실패 프레임에서 _dragStartPoint 갱신이 빠지면
    /// 다음 프레임에서 delta가 누적되어 큰 값이 발생하는 것을 수치로 검증.
    /// </summary>
    [Fact]
    public void should_prevent_delta_accumulation_when_dragStartPoint_always_updated()
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        // 시뮬레이션: 매 프레임 currentPos가 10px씩 증가
        // Frame 1: 조건 실패(bounds 무효) → _dragStartPoint 갱신 여부가 핵심
        double dragStartPoint = 0;  // 초기 _dragStartPoint.X

        double frame1Pos = 10;
        double frame2Pos = 20;

        // ── Act (버그 시나리오: 조건 실패 시 갱신 안 함) ──────────────────────
        // Frame 1: bounds 유효성 실패 → 갱신 없음 (버그)
        double delta1 = frame1Pos - dragStartPoint; // = 10
        bool frame1Valid = false;                   // 최소 크기 미달 시뮬레이션
        if (frame1Valid)
            dragStartPoint = frame1Pos;             // 갱신 안 됨 → dragStartPoint = 0 유지

        // Frame 2: 이번엔 유효 → delta가 누적됨
        double delta2_buggy = frame2Pos - dragStartPoint; // = 20 (누적!)

        // ── Act (수정 후: 항상 갱신) ─────────────────────────────────────────
        double dragStartPoint_fixed = 0;

        delta1 = frame1Pos - dragStartPoint_fixed;  // = 10
        // 수정: bounds 결과와 무관하게 항상 갱신
        dragStartPoint_fixed = frame1Pos;            // = 10

        double delta2_fixed = frame2Pos - dragStartPoint_fixed; // = 10 (정상)

        // ── Assert ───────────────────────────────────────────────────────────
        Assert.Equal(20.0, delta2_buggy); // 버그: 2배 누적
        Assert.Equal(10.0, delta2_fixed); // 수정: 정상 delta
        Assert.True(delta2_buggy > delta2_fixed, "버그 시 delta가 더 크게 누적되어야 함");
    }

    /// <summary>
    /// TC-12 변형: N 프레임 동안 조건 실패 후 한 프레임 성공 시 delta 폭발 검증
    /// </summary>
    [Fact]
    public void should_prevent_explosion_after_multiple_failed_frames()
    {
        const int FAILED_FRAMES = 5;
        const double MOVE_PER_FRAME = 10.0;

        // ── 버그 시나리오: 5프레임 동안 _dragStartPoint 갱신 없음 ────────────
        double dragStart_buggy = 0;
        double currentPos = 0;

        for (int i = 0; i < FAILED_FRAMES; i++)
        {
            currentPos += MOVE_PER_FRAME;
            // bounds 유효성 실패 → 갱신 안 함
        }
        double delta_buggy = currentPos - dragStart_buggy; // = 50

        // ── 수정 후: 매 프레임 갱신 ──────────────────────────────────────────
        double dragStart_fixed = 0;
        currentPos = 0;

        for (int i = 0; i < FAILED_FRAMES; i++)
        {
            currentPos += MOVE_PER_FRAME;
            dragStart_fixed = currentPos; // 항상 갱신
        }
        currentPos += MOVE_PER_FRAME; // 6번째 프레임 (유효)
        double delta_fixed = currentPos - dragStart_fixed; // = 10

        // ── Assert ───────────────────────────────────────────────────────────
        Assert.Equal(FAILED_FRAMES * MOVE_PER_FRAME, delta_buggy); // 50px 누적
        Assert.Equal(MOVE_PER_FRAME, delta_fixed);                  // 10px 정상
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FP-6: Math.Round vs (long) 직접 캐스트
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-13 대응: sub-pixel delta(0.7) 에서 Math.Round=1, 직접 캐스트=0 차이 검증
    /// </summary>
    [Fact]
    public void should_return_1_when_delta_is_0_7_using_round()
    {
        double delta = 0.7;

        long round = ApplyDeltaRound(delta);
        long cast = ApplyDeltaCast(delta);

        Assert.Equal(1L, round); // Math.Round(0.7) = 1 → 이동 있음
        Assert.Equal(0L, cast);  // (long)0.7 = 0 → stutter 발생
    }

    /// <summary>TC-13: 0.3 이하에서는 Math.Round도 0 (올림 임계 이하)</summary>
    [Fact]
    public void should_return_0_when_delta_is_0_3_using_round()
    {
        double delta = 0.3;

        long round = ApplyDeltaRound(delta);

        Assert.Equal(0L, round);
    }

    /// <summary>TC-13: 정확히 1.0은 양쪽 모두 1 (경계 확인)</summary>
    [Fact]
    public void should_return_1_when_delta_is_exactly_1_using_both_methods()
    {
        double delta = 1.0;

        Assert.Equal(1L, ApplyDeltaRound(delta));
        Assert.Equal(1L, ApplyDeltaCast(delta));
    }

    /// <summary>음수 delta -0.7: Math.Round=-1, 직접 캐스트=0 (음수 방향 stutter)</summary>
    [Fact]
    public void should_return_negative_1_when_delta_is_negative_0_7_using_round()
    {
        double delta = -0.7;

        long round = ApplyDeltaRound(delta);
        long cast = ApplyDeltaCast(delta);

        Assert.Equal(-1L, round);
        Assert.Equal(0L, cast);
    }

    /// <summary>1.5 케이스: Math.Round(ToEven)=2, 직접 캐스트=1 (은행가 반올림 경계)</summary>
    [Fact]
    public void should_return_2_when_delta_is_1_5_using_round_ToEven()
    {
        double delta = 1.5;

        long round = ApplyDeltaRound(delta); // ToEven: 짝수인 2로 반올림

        // PRD §3 FP-6 주석: "픽셀 delta가 정확히 0.5인 경우는 극히 드물므로 기본 모드로 충분"
        // 1.5 → Math.Round(ToEven) = 2 (짝수 선택)
        Assert.Equal(2L, round);
    }

    /// <summary>0.5 케이스: Math.Round(ToEven)=0 vs AwayFromZero=1 — 기본 모드 동작 확인</summary>
    [Fact]
    public void should_return_0_for_0_5_with_ToEven_rounding_mode()
    {
        double delta = 0.5;

        long roundToEven = ApplyDeltaRound(delta);                               // ToEven → 0
        long roundAwayFromZero = (long)Math.Round(delta, MidpointRounding.AwayFromZero); // → 1

        Assert.Equal(0L, roundToEven);    // 은행가 반올림: 짝수 0 선택
        Assert.Equal(1L, roundAwayFromZero);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FP-4 추가: None 핸들은 항상 false
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void should_return_false_for_unknown_handle()
    {
        bool actual = ComputeExpandDominantAxis(100, 100, ResizeHandle.None);
        Assert.False(actual);
    }

    [Fact]
    public void should_return_false_for_move_handle()
    {
        bool actual = ComputeExpandDominantAxis(100, 100, ResizeHandle.Move);
        Assert.False(actual);
    }
}
