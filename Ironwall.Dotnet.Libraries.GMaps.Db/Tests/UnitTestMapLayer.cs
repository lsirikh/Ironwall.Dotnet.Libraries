using Caliburn.Micro;
using Dapper;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Db.Models;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using MySql.Data.MySqlClient;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Db.Tests;

public sealed class MapLayerTestFixture : IAsyncLifetime
{
    public IGMapDbService Svc { get; private set; } = null!;
    public MapProvider MapProvider = new();
    public CustomMapProvider CustomMapProvider = null!;
    public DefinedMapProvider DefinedMapProvider = null!;
    internal CancellationTokenSource Cts { get; } = new();

    private static readonly string[] _tables =
    {
        "MapLayers", "MapRois", "GeoControlPoints", "CustomMaps", "DefinedMaps", "Maps"
    };

    private readonly GMapDbSetupModel _setup = new()
    {
        IpDbServer = "127.0.0.1", PortDbServer = 3306,
        DbDatabase = "monitor_db", UidDbServer = "root", PasswordDbServer = "root"
    };

    public async Task InitializeAsync()
    {
        var log = new LogService();
        var ea = new EventAggregator();
        CustomMapProvider = new CustomMapProvider(log, MapProvider);
        DefinedMapProvider = new DefinedMapProvider(log, MapProvider);
        Svc = new GMapDbService(log, ea, MapProvider, CustomMapProvider, DefinedMapProvider, _setup);
        await DropTablesAsync();
        await Svc.StartService(Cts.Token);
        Assert.True(Svc.IsConnected);
    }

    public async Task DisposeAsync()
    {
        await Svc.StopService(Cts.Token);
        await DropTablesAsync();
        if (!Cts.IsCancellationRequested) Cts.Cancel();
    }

    private async Task DropTablesAsync()
    {
        var csb = new MySqlConnectionStringBuilder
        {
            Server = _setup.IpDbServer, Port = (uint)_setup.PortDbServer,
            UserID = _setup.UidDbServer, Password = _setup.PasswordDbServer,
            Database = _setup.DbDatabase, SslMode = MySqlSslMode.Disabled
        };
        await using var conn = new MySqlConnection(csb.ToString());
        await conn.OpenAsync();
        foreach (var t in _tables)
            await conn.ExecuteAsync($"DROP TABLE IF EXISTS `{t}`;");
    }
}

[CollectionDefinition(nameof(MapLayerTestCollection))]
public class MapLayerTestCollection : ICollectionFixture<MapLayerTestFixture> { }

[Collection(nameof(MapLayerTestCollection))]
public class UnitTestMapLayer
{
    private readonly MapLayerTestFixture _fx;
    public UnitTestMapLayer(MapLayerTestFixture fx) => _fx = fx;

    [Fact(DisplayName = "MapLayer - Insert & Fetch")]
    public async Task Insert_And_Fetch_MapLayer()
    {
        var layer = new MapLayerModel
        {
            Name = "TestOverlay", LayerType = "OverlayMap",
            IsVisible = true, Opacity = 0.8, ZOrder = 10
        };

        int id = await _fx.Svc.InsertMapLayerAsync(layer);
        var list = await _fx.Svc.FetchMapLayersAsync();

        Assert.True(id > 0);
        Assert.NotNull(list);
        var fetched = list!.First(l => l.Id == id);
        Assert.Equal("TestOverlay", fetched.Name);
        Assert.Equal("OverlayMap", fetched.LayerType);
        Assert.Equal(0.8, fetched.Opacity, 1);

        await _fx.Svc.DeleteMapLayerAsync(id);
    }

    [Fact(DisplayName = "MapLayer - Update Visibility")]
    public async Task Update_Layer_Visibility()
    {
        var layer = new MapLayerModel { Name = "VisTest", LayerType = "Symbol", Category = "PidsCamera", IsVisible = true };
        int id = await _fx.Svc.InsertMapLayerAsync(layer);

        bool updated = await _fx.Svc.UpdateMapLayerVisibilityAsync(id, false);
        var list = await _fx.Svc.FetchMapLayersAsync();
        var fetched = list!.First(l => l.Id == id);

        Assert.True(updated);
        Assert.False(fetched.IsVisible);

        await _fx.Svc.DeleteMapLayerAsync(id);
    }

    [Fact(DisplayName = "MapLayer - Update Opacity")]
    public async Task Update_Layer_Opacity()
    {
        var layer = new MapLayerModel { Name = "OpacityTest", LayerType = "OverlayImage", Opacity = 1.0 };
        int id = await _fx.Svc.InsertMapLayerAsync(layer);

        bool updated = await _fx.Svc.UpdateMapLayerOpacityAsync(id, 0.5);
        var list = await _fx.Svc.FetchMapLayersAsync();
        var fetched = list!.First(l => l.Id == id);

        Assert.True(updated);
        Assert.Equal(0.5, fetched.Opacity, 1);

        await _fx.Svc.DeleteMapLayerAsync(id);
    }

    [Fact(DisplayName = "MapLayer - Delete")]
    public async Task Delete_MapLayer()
    {
        var layer = new MapLayerModel { Name = "DeleteTest", LayerType = "Symbol" };
        int id = await _fx.Svc.InsertMapLayerAsync(layer);

        bool deleted = await _fx.Svc.DeleteMapLayerAsync(id);
        var list = await _fx.Svc.FetchMapLayersAsync();

        Assert.True(deleted);
        Assert.DoesNotContain(list!, l => l.Id == id);
    }

    [Fact(DisplayName = "MapLayer - Seed Default Symbol Layers")]
    public async Task Seed_Default_Symbol_Layers()
    {
        await _fx.Svc.SeedDefaultSymbolLayersAsync();
        var list = await _fx.Svc.FetchMapLayersAsync();
        var symbols = list!.Where(l => l.LayerType == "Symbol").ToList();

        Assert.Equal(11, symbols.Count);
        Assert.Contains(symbols, s => s.Category == "PidsCamera");
        Assert.Contains(symbols, s => s.Category == "Military");
        Assert.Contains(symbols, s => s.Category == "Geometric");

        // 중복 호출 시 증가 안 함
        await _fx.Svc.SeedDefaultSymbolLayersAsync();
        var list2 = await _fx.Svc.FetchMapLayersAsync();
        Assert.Equal(11, list2!.Count(l => l.LayerType == "Symbol"));

        // Cleanup
        foreach (var s in symbols)
            await _fx.Svc.DeleteMapLayerAsync(s.Id);
    }
}
