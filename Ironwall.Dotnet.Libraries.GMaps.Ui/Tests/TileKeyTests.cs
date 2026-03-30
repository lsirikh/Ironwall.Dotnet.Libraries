using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

public class TileKeyTests
{
    [Fact(DisplayName = "TileKey — 동일 좌표는 같은 키")]
    public void SameCoordinates_AreEqual()
    {
        var a = new TileKey(18, 223480, 101440);
        var b = new TileKey(18, 223480, 101440);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact(DisplayName = "TileKey — 다른 좌표는 다른 키")]
    public void DifferentCoordinates_AreNotEqual()
    {
        var a = new TileKey(18, 223480, 101440);
        var b = new TileKey(18, 223481, 101440);
        var c = new TileKey(17, 223480, 101440);

        Assert.NotEqual(a, b);
        Assert.NotEqual(a, c);
        Assert.True(a != b);
    }

    [Fact(DisplayName = "TileKey — Dictionary 키로 사용 가능")]
    public void CanBeUsedAsDictionaryKey()
    {
        var dict = new Dictionary<TileKey, string>();
        var key = new TileKey(15, 26240, 16383);

        dict[key] = "tile.png";

        Assert.True(dict.ContainsKey(new TileKey(15, 26240, 16383)));
        Assert.Equal("tile.png", dict[new TileKey(15, 26240, 16383)]);
    }

    [Fact(DisplayName = "TileKey — ToString 포맷")]
    public void ToString_ReturnsZoomXY()
    {
        var key = new TileKey(15, 26240, 16383);
        Assert.Equal("15/26240/16383", key.ToString());
    }
}
