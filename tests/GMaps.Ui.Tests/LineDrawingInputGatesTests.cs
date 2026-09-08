using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 드래그 드로잉 입력 게이트(<see cref="LineDrawingInputGates"/>·<see cref="PanSuppressGate"/>) 회귀 테스트 — 실코드 소스링크.
/// C11(스트로크 중 휠 차단) · C13(취소 후 팬 억제 상태기계) · C14(클릭 정점 최소 간격).
/// </summary>
public class LineDrawingInputGatesTests
{
    // ── C14: 클릭 정점 최소 간격 ──

    [Fact]
    public void should_accept_click_vertex_when_no_last_vertex()
    {
        // Arrange / Act
        bool accepted = LineDrawingInputGates.ShouldAcceptClickVertex(hasLastVertex: false, distanceToLastM: 0.0, FenceDefaults.MinVertexSpacingM);

        // Assert
        Assert.True(accepted);
    }

    [Fact]
    public void should_reject_click_vertex_when_within_min_spacing()
    {
        // Arrange — 더블클릭(거리 0)·스트로크 끝점 1px 재클릭(z19 ≈ 0.3 m)
        double[] tooClose = { 0.0, 0.3, 0.999 };

        // Act / Assert
        foreach (var d in tooClose)
            Assert.False(LineDrawingInputGates.ShouldAcceptClickVertex(true, d, FenceDefaults.MinVertexSpacingM), $"d={d}");
    }

    [Fact]
    public void should_accept_click_vertex_when_at_or_beyond_min_spacing()
    {
        // Arrange
        double[] farEnough = { FenceDefaults.MinVertexSpacingM, 1.5, 128.0 };

        // Act / Assert
        foreach (var d in farEnough)
            Assert.True(LineDrawingInputGates.ShouldAcceptClickVertex(true, d, FenceDefaults.MinVertexSpacingM), $"d={d}");
    }

    [Fact]
    public void should_accept_click_vertex_when_distance_is_nan()
    {
        // Arrange / Act — 투영 실패는 fail-open
        bool accepted = LineDrawingInputGates.ShouldAcceptClickVertex(true, double.NaN, FenceDefaults.MinVertexSpacingM);

        // Assert
        Assert.True(accepted);
    }

    // ── C11: 스트로크 중 휠 차단 ──

    [Fact]
    public void should_block_wheel_when_stroke_press_active()
        => Assert.True(LineDrawingInputGates.ShouldBlockWheel(linePressActive: true, imageDragActive: false));

    [Fact]
    public void should_block_wheel_when_image_drag_active()
        => Assert.True(LineDrawingInputGates.ShouldBlockWheel(linePressActive: false, imageDragActive: true));

    [Fact]
    public void should_pass_wheel_when_idle()
        => Assert.False(LineDrawingInputGates.ShouldBlockWheel(linePressActive: false, imageDragActive: false));

    // ── C13: 취소 후 팬 억제 상태기계 ──

    [Fact]
    public void should_block_move_after_cancel_when_button_still_pressed()
    {
        // Arrange
        var gate = new PanSuppressGate();
        gate.OnButtonDown();

        // Act — ESC/캡처 소실로 스트로크 취소, 버튼은 계속 눌린 채 이동
        gate.OnStrokeCancelled(leftButtonPressed: true);

        // Assert
        Assert.True(gate.IsSuppressing);
        Assert.True(gate.ShouldBlockMove(leftButtonPressed: true));
        Assert.True(gate.ShouldBlockMove(leftButtonPressed: true));   // 릴리스 전까지 계속 차단
    }

    [Fact]
    public void should_not_suppress_when_button_already_released_at_cancel()
    {
        // Arrange
        var gate = new PanSuppressGate();

        // Act — 정상 릴리스 경로(commit)나 버튼이 이미 풀린 뒤의 취소
        gate.OnStrokeCancelled(leftButtonPressed: false);

        // Assert
        Assert.False(gate.IsSuppressing);
        Assert.False(gate.ShouldBlockMove(leftButtonPressed: false));
    }

    [Fact]
    public void should_release_suppression_and_consume_release_on_button_up()
    {
        // Arrange
        var gate = new PanSuppressGate();
        gate.OnStrokeCancelled(leftButtonPressed: true);

        // Act
        bool consumed = gate.OnButtonUp();

        // Assert
        Assert.True(consumed);
        Assert.False(gate.IsSuppressing);
        Assert.False(gate.ShouldBlockMove(leftButtonPressed: false));
        Assert.False(gate.OnButtonUp());   // 억제가 없던 릴리스는 소비하지 않는다
    }

    [Fact]
    public void should_self_release_when_move_sees_button_released_without_up_event()
    {
        // Arrange — 캡처 소실로 Up 이 다른 요소로 간 경우
        var gate = new PanSuppressGate();
        gate.OnStrokeCancelled(leftButtonPressed: true);

        // Act
        bool blocked = gate.ShouldBlockMove(leftButtonPressed: false);

        // Assert
        Assert.False(blocked);
        Assert.False(gate.IsSuppressing);
    }

    [Fact]
    public void should_reset_suppression_when_new_press_arrives()
    {
        // Arrange
        var gate = new PanSuppressGate();
        gate.OnStrokeCancelled(leftButtonPressed: true);

        // Act — 새 제스처 시작
        gate.OnButtonDown();

        // Assert
        Assert.False(gate.IsSuppressing);
        Assert.False(gate.ShouldBlockMove(leftButtonPressed: true));
    }
}
