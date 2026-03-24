using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// EventCardBatchBuffer 배치 버퍼링 + Backpressure + 적응형 간격 테스트
/// </summary>
public class EventCardBatchBufferTests
{
    #region - Phase 3: 배치 버퍼링 (FR-02) -

    [Fact]
    public void EnqueueCard_ShouldAddToQueue()
    {
        // Arrange
        var buffer = new EventCardBatchBuffer<string>();

        // Act
        var result = buffer.Enqueue("card1");

        // Assert
        Assert.True(result);
        Assert.Equal(1, buffer.PendingCount);
    }

    [Fact]
    public void FlushPendingCards_ShouldDrainQueue()
    {
        // Arrange
        var buffer = new EventCardBatchBuffer<string>();
        buffer.Enqueue("A");
        buffer.Enqueue("B");
        buffer.Enqueue("C");

        // Act
        var batch = buffer.DrainQueue();

        // Assert
        Assert.Equal(3, batch.Count);
        Assert.Equal(0, buffer.PendingCount);
    }

    [Fact]
    public void FlushPendingCards_EmptyQueue_ShouldReturnEmpty()
    {
        // Arrange
        var buffer = new EventCardBatchBuffer<string>();

        // Act
        var batch = buffer.DrainQueue();

        // Assert
        Assert.Empty(batch);
    }

    #endregion

    #region - Phase 4: Backpressure (FR-03) -

    [Fact]
    public void EnqueueCard_ShouldRejectWhenQueueFull()
    {
        // Arrange — 상한 5로 설정
        var buffer = new EventCardBatchBuffer<string>(maxPending: 5);
        for (int i = 0; i < 5; i++)
            buffer.Enqueue($"item{i}");

        // Act
        var result = buffer.Enqueue("overflow");

        // Assert
        Assert.False(result);
        Assert.Equal(5, buffer.PendingCount);
    }

    [Fact]
    public void EnqueueCard_ShouldAcceptWhenBelowCap()
    {
        // Arrange
        var buffer = new EventCardBatchBuffer<string>(maxPending: 5);
        buffer.Enqueue("item1");

        // Act
        var result = buffer.Enqueue("item2");

        // Assert
        Assert.True(result);
        Assert.Equal(2, buffer.PendingCount);
    }

    #endregion

    #region - Phase 5: 적응형 배치 간격 (FR-05) -

    [Fact]
    public void AdjustBatchInterval_HighLoad_ShouldReturn500()
    {
        // Arrange
        var buffer = new EventCardBatchBuffer<string>();

        // Act
        var interval = buffer.CalculateInterval(25);

        // Assert
        Assert.Equal(EventCardBatchBuffer<string>.INTERVAL_HIGH, interval);
    }

    [Fact]
    public void AdjustBatchInterval_MediumLoad_ShouldReturn300()
    {
        // Arrange
        var buffer = new EventCardBatchBuffer<string>();

        // Act
        var interval = buffer.CalculateInterval(15);

        // Assert
        Assert.Equal(EventCardBatchBuffer<string>.INTERVAL_MEDIUM, interval);
    }

    [Fact]
    public void AdjustBatchInterval_NormalLoad_ShouldReturn150()
    {
        // Arrange
        var buffer = new EventCardBatchBuffer<string>();

        // Act
        var interval = buffer.CalculateInterval(5);

        // Assert
        Assert.Equal(EventCardBatchBuffer<string>.INTERVAL_NORMAL, interval);
    }

    #endregion

    #region - Phase 6: 표시 상한 (FR-06) -

    private record TestCard(string Name, bool IsReported);

    [Fact]
    public void TrimOldCards_ShouldOnlyRemoveReportedCards()
    {
        // Arrange — 상한 3, 5건 중 3건 보고 완료
        var items = new List<TestCard>
        {
            new("A", true),   // reported
            new("B", false),  // unreported
            new("C", true),   // reported
            new("D", false),  // unreported
            new("E", true),   // reported
        };

        // Act — 초과 2건 제거 대상 선별 (reported만)
        var candidates = EventCardBatchBuffer<TestCard>.GetTrimCandidates(
            items, maxVisible: 3, c => c.IsReported);

        // Assert
        Assert.Equal(2, candidates.Count);
        Assert.All(candidates, c => Assert.True(c.IsReported));
    }

    [Fact]
    public void TrimOldCards_BelowLimit_ShouldDoNothing()
    {
        // Arrange — 상한 10, 3건만 존재
        var items = new List<TestCard>
        {
            new("A", true),
            new("B", false),
            new("C", true),
        };

        // Act
        var candidates = EventCardBatchBuffer<TestCard>.GetTrimCandidates(
            items, maxVisible: 10, c => c.IsReported);

        // Assert
        Assert.Empty(candidates);
    }

    [Fact]
    public void TrimOldCards_AllUnreported_ShouldNotRemoveAny()
    {
        // Arrange — 상한 2, 4건 모두 미보고
        var items = new List<TestCard>
        {
            new("A", false),
            new("B", false),
            new("C", false),
            new("D", false),
        };

        // Act
        var candidates = EventCardBatchBuffer<TestCard>.GetTrimCandidates(
            items, maxVisible: 2, c => c.IsReported);

        // Assert — 미보고 카드는 제거 안 함
        Assert.Empty(candidates);
    }

    #endregion
}
