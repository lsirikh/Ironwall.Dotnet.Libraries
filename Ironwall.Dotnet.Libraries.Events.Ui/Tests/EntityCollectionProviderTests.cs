using Ironwall.Dotnet.Libraries.Base.DataProviders;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// EntityCollectionProvider AddRange/RemoveRange 배치 메서드 테스트
/// </summary>
public class EntityCollectionProviderTests
{
    private class TestProvider : EntityCollectionProvider<string> { }

    #region - Phase 2: AddRange/RemoveRange -

    [Fact]
    public void AddRange_ShouldAddAllItems()
    {
        // Arrange
        var provider = new TestProvider();
        var items = new[] { "A", "B", "C", "D", "E" };

        // Act
        provider.AddRange(items);

        // Assert
        Assert.Equal(5, provider.Count);
    }

    [Fact]
    public void AddRange_Empty_ShouldNotThrow()
    {
        // Arrange
        var provider = new TestProvider();

        // Act & Assert
        provider.AddRange(Array.Empty<string>());
        Assert.Equal(0, provider.Count);
    }

    [Fact]
    public void RemoveRange_ShouldRemoveAllItems()
    {
        // Arrange
        var provider = new TestProvider();
        provider.Add("A");
        provider.Add("B");
        provider.Add("C");
        var toRemove = new[] { "A", "C" };

        // Act
        provider.RemoveRange(toRemove);

        // Assert
        Assert.Equal(1, provider.Count);
    }

    #endregion
}
