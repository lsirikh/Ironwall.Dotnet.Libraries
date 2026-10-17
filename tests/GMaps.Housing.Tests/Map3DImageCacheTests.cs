using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapImages;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// 2D → 3D 전환 빠르게(2026-10-17) — 오버레이 이미지 인코딩 디스크 캐시(JPEG/PNG · 2048) 와 다리 Pause/Resume(바뀐 것만 이어 보내기).
/// 창을 띄우지 않는다. 캐시 폴더는 시험마다 임시 폴더(끝나면 지운다).
/// </summary>
public class Map3DImageCacheTests
{
    private static BitmapSource Solid(int w, int h, byte alpha)
    {
        var px = new byte[w * h * 4];
        for (int i = 0; i < px.Length; i += 4) { px[i] = 40; px[i + 1] = 120; px[i + 2] = 200; px[i + 3] = alpha; }
        return BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, w * 4);
    }

    /// <summary>그림을 PNG 파일로 쓰고 앱과 같은 방식(BitmapImage.UriSource)으로 읽는다.</summary>
    private static BitmapImage FromFile(string path, BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using (var stream = File.Create(path)) encoder.Save(stream);
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new System.Uri(path, System.UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;   // 같은 경로를 다시 쓰는 시험 — WPF URI 캐시를 거치지 않게(GMapCustomImage 와 같다)
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static BitmapFrame Decode(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
    }

    private static void WithTempDir(System.Action<string> action)
    {
        string dir = Path.Combine(Path.GetTempPath(), "map3d-cache-test-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try { action(dir); }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void should_encode_jpeg_fitted_to_2048_when_image_opaque() => HousingTests.Sta(() =>
    {
        var (bytes, mime) = Map3DImageCache.Encode(Solid(4000, 1000, 255));

        Assert.Equal("image/jpeg", mime);
        Assert.Equal(new byte[] { 0xFF, 0xD8 }, bytes[..2]);
        var frame = Decode(bytes);
        Assert.Equal(2048, frame.PixelWidth);
        Assert.Equal(512, frame.PixelHeight);
    });

    [Fact]
    public void should_encode_png_when_image_has_transparency() => HousingTests.Sta(() =>
    {
        var (bytes, mime) = Map3DImageCache.Encode(Solid(64, 32, 128));

        Assert.Equal("image/png", mime);
        Assert.Equal(64, Decode(bytes).PixelWidth);
    });

    [Fact]
    public void should_read_disk_cache_instead_of_encoding_when_same_file_again() => HousingTests.Sta(() => WithTempDir(dir =>
    {
        string cacheDir = Path.Combine(dir, "Ironwall", "Gis", "map3d-cache");   // 없으면 만든다
        var image = FromFile(Path.Combine(dir, "overlay.png"), Solid(300, 200, 255));

        var first = Map3DImageCache.GetOrEncode(image, cacheDir);
        string cached = Assert.Single(Directory.GetFiles(cacheDir));
        Assert.EndsWith(".jpg", cached);
        Assert.Equal(first.Bytes, File.ReadAllBytes(cached));

        File.WriteAllBytes(cached, new byte[] { 1, 2, 3 });   // 캐시에서 꺼내는지 확인용 표식
        var second = Map3DImageCache.GetOrEncode(image, cacheDir);

        Assert.Equal(new byte[] { 1, 2, 3 }, second.Bytes);
        Assert.Equal("image/jpeg", second.Mime);
    }));

    [Fact]
    public void should_reencode_when_source_file_changes() => HousingTests.Sta(() => WithTempDir(dir =>
    {
        string cacheDir = Path.Combine(dir, "cache");
        string path = Path.Combine(dir, "overlay.png");
        Map3DImageCache.GetOrEncode(FromFile(path, Solid(30, 20, 255)), cacheDir);

        var changed = FromFile(path, Solid(30, 20, 100));   // 같은 경로 · 내용(크기) 바뀜
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddMinutes(1));
        var result = Map3DImageCache.GetOrEncode(changed, cacheDir);

        Assert.Equal("image/png", result.Mime);
        Assert.Equal(2, Directory.GetFiles(cacheDir).Length);
    }));

    [Fact]
    public void should_not_write_cache_when_image_has_no_source_file() => HousingTests.Sta(() => WithTempDir(dir =>
    {
        string cacheDir = Path.Combine(dir, "cache");

        var (_, mime) = Map3DImageCache.GetOrEncode(Solid(16, 16, 255), cacheDir);

        Assert.Equal("image/jpeg", mime);
        Assert.False(Directory.Exists(cacheDir));
    }));

    // ── 다리 Pause / Resume ──

    [Fact]
    public void should_send_only_changes_with_continued_rev_when_resumed_after_pause() => HousingTests.Sta(() =>
    {
        var log = Mock.Of<ILogService>();
        var map = new GMapCustomControl();
        var model = new GeometricSymbolModel { Id = 1, Title = "도형", Width = 30, Height = 30, Visible = true };
        var geo = new GMapGeometricMarker(log, model);
        HousingTests.Layout((FrameworkElement)geo.Shape, 30, 30);
        map.Markers.Add(geo);
        var image = new GMapCustomImage(log, new ImageModel(0, "가림", 127.0, 37.2, 127.1, 37.1) { Visibility = true });
        image.Img = Solid(8, 8, 255);
        map.CustomImages.Add(image);

        var posts = new List<string>();
        var bridge = new Map3DOverlayBridge(posts.Add, null);
        bridge.Start(map);
        long snapshotRev = JObject.Parse(posts.Single(p => p.Contains("overlay.snapshot"))).Value<long>("rev");

        bridge.Pause();
        posts.Clear();
        model.Title = "새 이름";              // 접혀 있는 동안 바뀜
        map.CustomImages.Remove(image);       // 접혀 있는 동안 빠짐
        Assert.Empty(posts);                  // 2D 동안은 아무것도 보내지 않는다
        Assert.False(bridge.IsActive);

        bridge.Resume(map);

        var msgs = posts.Select(JObject.Parse).ToList();
        var types = msgs.Select(m => m.Value<string>("type")).ToList();
        Assert.DoesNotContain("overlay.snapshot", types);
        Assert.DoesNotContain("overlay.icon", types);        // 3D 가 이미 가진 픽셀은 다시 보내지 않는다
        Assert.DoesNotContain("overlay.imageData", types);
        var patch = msgs.Single(m => m.Value<string>("type") == "overlay.patch");
        Assert.Equal("새 이름", patch["set"]!.Value<string>("title"));
        var remove = msgs.Single(m => m.Value<string>("type") == "overlay.remove");
        Assert.Equal("image", remove.Value<string>("kind"));
        Assert.All(msgs.Where(m => m["rev"] != null), m => Assert.True(m.Value<long>("rev") > snapshotRev));
        Assert.True(bridge.IsActive);

        posts.Clear();
        bridge.Resume(map);                   // 이미 붙어 있으면 아무것도 하지 않는다
        Assert.Empty(posts);
        bridge.Stop();
    });

    [Fact]
    public void should_send_full_snapshot_when_resumed_without_records() => HousingTests.Sta(() =>
    {
        var map = new GMapCustomControl();
        var posts = new List<string>();
        var bridge = new Map3DOverlayBridge(posts.Add, null);
        bridge.Start(map);
        bridge.Pause();
        bridge.Stop();                        // 페이지가 상태를 잃었다(overlay.resync · 새 장면)
        posts.Clear();

        bridge.Resume(map);

        Assert.Contains(posts, p => JObject.Parse(p).Value<string>("type") == "overlay.snapshot");
        bridge.Stop();
    });
}
