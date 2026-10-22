using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// 엔진 계약(2026-10-22): 3D 손잡이로 고친 심볼을 저장한 뒤 GIS 가 새 값을 overlay.patch 로 반드시 보낸다 — 값이 직전에 보낸 것과 같아도.
/// 3D 를 보고 있지 않으면(Start 전 · Stop 뒤) 보내지 않는다.
/// </summary>
public class Map3DOverlayPatchNowTests
{
    [Fact]
    public void should_post_full_patch_even_when_unchanged_and_nothing_when_not_started() => HousingTests.Sta(() =>
    {
        var map = new GMapCustomControl();
        var geo = new GMapGeometricMarker(Mock.Of<ILogService>(), new GeometricSymbolModel { Id = 5, Title = "도형", Latitude = 37.5, Longitude = 127.1, Visible = true });
        map.Markers.Add(geo);
        var posts = new List<string>();
        var bridge = new Map3DOverlayBridge(posts.Add, null);

        bridge.PatchNow(geo);
        Assert.Empty(posts);                                                             // 3D 를 보고 있지 않다

        bridge.Start(map);
        posts.Clear();
        bridge.PatchNow(geo);                                                            // 바뀐 것 없어도
        var patch = JObject.Parse(Assert.Single(posts));
        Assert.Equal(("overlay.patch", "geometric", 5), (patch.Value<string>("type"), patch.Value<string>("kind"), patch.Value<int>("id")));
        Assert.Equal((37.5, 127.1), (patch["set"]!.Value<double>("lat"), patch["set"]!.Value<double>("lon")));
        Assert.Null(patch["set"]!["kind"]);

        bridge.Stop();
        posts.Clear();
        bridge.PatchNow(geo);
        Assert.Empty(posts);
    });
}
