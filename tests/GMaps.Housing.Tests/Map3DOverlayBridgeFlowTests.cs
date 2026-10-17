using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
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
/// 지도 3D 규약 v2 — 다리 흐름: 아이콘 · 이미지 픽셀이 스냅샷보다 먼저 한 번만, 스냅샷 images[] · 추적 upsert, 지도 이미지 음수 id · 제거.
/// 창을 띄우지 않는다(지도 컨트롤 · 마커 Shape 를 부모 없이 배치만).
/// </summary>
public class Map3DOverlayBridgeFlowTests
{
    private static List<JObject> Parse(List<string> posts) => posts.Select(JObject.Parse).ToList();

    private static (GMapCustomControl Map, GMapGeometricMarker Geo, GMapCustomImage Image, GMapTrackingMarker Track) Scene()
    {
        var log = Mock.Of<ILogService>();
        var map = new GMapCustomControl();
        var geo = new GMapGeometricMarker(log, new GeometricSymbolModel { Id = 1, Title = "도형", Width = 30, Height = 30, Visible = true });
        HousingTests.Layout((FrameworkElement)geo.Shape, 30, 30);
        map.Markers.Add(geo);

        var image = new GMapCustomImage(log, new ImageModel(0, "가림", 127.0, 37.2, 127.1, 37.1) { Visibility = true, Opacity = 0.8 });
        image.Img = BitmapSource.Create(64, 32, 96, 96, PixelFormats.Bgra32, null, new byte[64 * 32 * 4], 64 * 4);
        map.CustomImages.Add(image);

        var track = new GMapTrackingMarker(3, "t-1", new PointLatLng(37.15, 127.05)) { ZIndex = 2000 };
        track.Update(EnumColorType.Red, EnumTargetType.Person, 45, showArrow: true, speedMps: 1.2);
        var trail = new GMapTrailMarker(3, "t-1", EnumColorType.Red);
        trail.AddPoint(new PointLatLng(37.149, 127.049), 30);
        trail.AddPoint(new PointLatLng(37.15, 127.05), 30);
        map.Markers.Add(track);
        map.Markers.Add(trail);
        return (map, geo, image, track);
    }

    [Fact]
    public void should_send_pixels_before_snapshot_once_when_3d_starts() => HousingTests.Sta(() =>
    {
        var (map, _, image, _) = Scene();
        var posts = new List<string>();
        var bridge = new Map3DOverlayBridge(posts.Add, null);

        bridge.Start(map);

        var msgs = Parse(posts);
        var types = msgs.Select(m => m.Value<string>("type")).ToList();
        int snapAt = types.IndexOf("overlay.snapshot");
        Assert.True(types.IndexOf("overlay.icon") is >= 0 and var iconAt && iconAt < snapAt);
        Assert.True(types.IndexOf("overlay.imageData") is >= 0 and var dataAt && dataAt < snapAt);
        Assert.Equal(1, types.Count(t => t == "overlay.imageData"));

        var snap = msgs[snapAt];
        var point = (JObject)snap["points"]!.Single();
        Assert.Equal("geometric", point.Value<string>("kind"));
        Assert.Equal(msgs[types.IndexOf("overlay.icon")].Value<string>("key"), point["icon"]!.Value<string>("key"));
        Assert.Equal(30, point["icon"]!.Value<double>("wPx"));

        var img = (JObject)snap["images"]!.Single();
        Assert.Equal("image", img.Value<string>("kind"));
        Assert.Equal(-1, img.Value<int>("id"));
        Assert.True(img.Value<bool>("mask"));
        Assert.Equal(0.8, img.Value<double>("opacity"), 6);
        Assert.Equal(msgs[types.IndexOf("overlay.imageData")].Value<string>("key"), img.Value<string>("imageKey"));
        Assert.Same(image, bridge.FindCustomImage(-1));

        var up = msgs[types.IndexOf("track.upsert")];
        Assert.True(types.IndexOf("track.upsert") > snapAt);
        Assert.Equal("3:t-1", up.Value<string>("id"));
        Assert.Equal(45, up.Value<double>("bearing"));
        Assert.Equal("#E74C3C", up.Value<string>("color"));
        Assert.Equal(2, up["trail"]!.Count());
        Assert.Equal("t-1", up.Value<string>("label"));

        // 다시 스냅샷(resync) — 픽셀 캐시는 재사용하되 3D 쪽 캐시를 모르니 다시 먼저 보낸다
        posts.Clear();
        bridge.SendSnapshot();
        var again = Parse(posts).Select(m => m.Value<string>("type")).ToList();
        Assert.True(again.IndexOf("overlay.icon") < again.IndexOf("overlay.snapshot"));
        Assert.True(again.IndexOf("overlay.imageData") < again.IndexOf("overlay.snapshot"));
        bridge.Stop();
    });

    [Fact]
    public void should_remove_image_and_track_when_removed_from_map() => HousingTests.Sta(() =>
    {
        var (map, _, image, track) = Scene();
        var posts = new List<string>();
        var bridge = new Map3DOverlayBridge(posts.Add, null);
        bridge.Start(map);
        posts.Clear();

        map.CustomImages.Remove(image);
        map.Markers.Remove(track);

        var msgs = Parse(posts);
        Assert.Contains(msgs, m => m.Value<string>("type") == "overlay.remove" && m.Value<string>("kind") == "image" && m.Value<int>("id") == -1);
        Assert.Contains(msgs, m => m.Value<string>("type") == "track.remove" && m.Value<string>("id") == "3:t-1");
        Assert.Null(bridge.FindCustomImage(-1));
        bridge.Stop();
    });

    [Fact]
    public void should_add_new_image_with_next_negative_id_when_image_added_during_3d() => HousingTests.Sta(() =>
    {
        var (map, _, _, _) = Scene();
        var posts = new List<string>();
        var bridge = new Map3DOverlayBridge(posts.Add, null);
        bridge.Start(map);
        posts.Clear();

        var second = new GMapCustomImage(Mock.Of<ILogService>(), new ImageModel(0, "가림 2", 127.2, 37.3, 127.3, 37.2) { Visibility = true });
        second.Img = BitmapSource.Create(8, 8, 96, 96, PixelFormats.Bgra32, null, Enumerable.Repeat((byte)255, 8 * 8 * 4).ToArray(), 8 * 4);
        map.CustomImages.Add(second);

        var msgs = Parse(posts);
        Assert.Equal("overlay.imageData", msgs[0].Value<string>("type"));   // 새 픽셀이 add 보다 먼저
        var add = msgs.Single(m => m.Value<string>("type") == "overlay.add");
        Assert.Equal(-2, add["item"]!.Value<int>("id"));
        Assert.Equal(msgs[0].Value<string>("key"), add["item"]!.Value<string>("imageKey"));
        bridge.Stop();
    });
}
