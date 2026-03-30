using System.Windows.Media.Imaging;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

public class LruTileCacheTests
{
    private static BitmapImage CreateDummyImage()
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = null;
        bmp.StreamSource = new System.IO.MemoryStream(new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG header
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, // 1x1
            0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE,
            0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, 0x54,
            0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00, 0x00,
            0x00, 0x02, 0x00, 0x01, 0xE2, 0x21, 0xBC, 0x33,
            0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
        });
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    [Fact(DisplayName = "LruCache — Put/Get 기본 동작")]
    public void Put_Get_ReturnsImage()
    {
        var cache = new LruTileCache(10);
        var img = CreateDummyImage();

        cache.Put("1_18_100_200", img);

        Assert.Equal(1, cache.Count);
        Assert.Same(img, cache.Get("1_18_100_200"));
    }

    [Fact(DisplayName = "LruCache — 미존재 키 Get → null")]
    public void Get_Missing_ReturnsNull()
    {
        var cache = new LruTileCache(10);
        Assert.Null(cache.Get("nonexistent"));
    }

    [Fact(DisplayName = "LruCache — 최대 크기 초과 시 LRU 제거")]
    public void Eviction_RemovesLeastRecentlyUsed()
    {
        var cache = new LruTileCache(3);
        var img = CreateDummyImage();

        cache.Put("a", img);
        cache.Put("b", img);
        cache.Put("c", img);
        cache.Put("d", img);  // "a" 제거됨

        Assert.Equal(3, cache.Count);
        Assert.Null(cache.Get("a"));       // 제거됨
        Assert.NotNull(cache.Get("b"));    // 유지
        Assert.NotNull(cache.Get("d"));    // 유지
    }

    [Fact(DisplayName = "LruCache — Get 시 MRU로 승격")]
    public void Get_PromotesToMRU()
    {
        var cache = new LruTileCache(3);
        var img = CreateDummyImage();

        cache.Put("a", img);
        cache.Put("b", img);
        cache.Put("c", img);

        cache.Get("a");  // "a"를 MRU로 승격

        cache.Put("d", img);  // "b" 제거 (a가 승격되었으므로 b가 LRU)

        Assert.NotNull(cache.Get("a"));    // 승격되어 유지
        Assert.Null(cache.Get("b"));       // LRU로 제거됨
    }

    [Fact(DisplayName = "LruCache — Invalidate로 특정 맵 캐시 제거")]
    public void Invalidate_RemovesByPrefix()
    {
        var cache = new LruTileCache(100);
        var img = CreateDummyImage();

        cache.Put("5_18_100_200", img);
        cache.Put("5_18_100_201", img);
        cache.Put("8_18_100_200", img);

        cache.Invalidate(5);  // customMapId=5 제거

        Assert.Equal(1, cache.Count);
        Assert.Null(cache.Get("5_18_100_200"));
        Assert.NotNull(cache.Get("8_18_100_200"));
    }

    [Fact(DisplayName = "LruCache — Clear 전체 초기화")]
    public void Clear_RemovesAll()
    {
        var cache = new LruTileCache(100);
        var img = CreateDummyImage();

        cache.Put("a", img);
        cache.Put("b", img);
        cache.Clear();

        Assert.Equal(0, cache.Count);
    }
}
