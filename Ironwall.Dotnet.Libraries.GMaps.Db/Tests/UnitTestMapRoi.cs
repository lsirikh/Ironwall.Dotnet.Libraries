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
/****************************************************************************
   Purpose      : MapRoi CRUD 단위 테스트
   Created By   : GHLee
   Created On   : 3/17/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public sealed class MapRoiTestFixture : IAsyncLifetime
{
    public IGMapDbService Svc { get; private set; } = null!;
    public MapProvider MapProvider = new();
    public CustomMapProvider CustomMapProvider = null!;
    public DefinedMapProvider DefinedMapProvider = null!;
    internal CancellationTokenSource Cts { get; } = new();

    /// <summary>
    /// 테스트용 Map Id (FK 용)
    /// </summary>
    public int TestMapId { get; private set; }

    private static readonly string[] _tables =
    {
        "MapRois",
        "GeoControlPoints",
        "CustomMaps",
        "DefinedMaps",
        "Maps"
    };

    private readonly GMapDbSetupModel _setup = new()
    {
        IpDbServer = "127.0.0.1",
        PortDbServer = 3306,
        DbDatabase = "monitor_db",
        UidDbServer = "root",
        PasswordDbServer = "root"
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

        // FK용 기본 Map 레코드 생성
        var baseMap = new DefinedMapModel
        {
            Name = "테스트용 기본지도",
            Description = "ROI 테스트용",
            Category = EnumMapCategory.Standard,
            DataType = EnumMapData.Raster,
            CoordinateSystem = "WGS84",
            MinZoomLevel = 0,
            MaxZoomLevel = 18,
            TileSize = 256,
            Status = EnumMapStatus.Active,
            CreatedBy = "testuser",
            GMapProviderName = "OpenStreetMapProvider",
            Vendor = EnumMapVendor.OpenStreetMap,
            Style = EnumMapStyle.Normal,
            RequiresApiKey = false
        };
        TestMapId = await Svc.InsertDefinedMapAsync(baseMap);
        Assert.True(TestMapId > 0);
    }

    public async Task DisposeAsync()
    {
        await Svc.StopService(Cts.Token);
        await DropTablesAsync();

        if (!Cts.IsCancellationRequested)
            Cts.Cancel();
    }

    private async Task DropTablesAsync()
    {
        var csb = new MySqlConnectionStringBuilder
        {
            Server = _setup.IpDbServer,
            Port = (uint)_setup.PortDbServer,
            UserID = _setup.UidDbServer,
            Password = _setup.PasswordDbServer,
            Database = _setup.DbDatabase,
            SslMode = MySqlSslMode.Disabled
        };

        await using var conn = new MySqlConnection(csb.ToString());
        await conn.OpenAsync();

        foreach (var t in _tables)
            await conn.ExecuteAsync($"DROP TABLE IF EXISTS `{t}`;");
    }
}

[CollectionDefinition(nameof(MapRoiTestCollection))]
public class MapRoiTestCollection : ICollectionFixture<MapRoiTestFixture> { }

[Collection(nameof(MapRoiTestCollection))]
public class UnitTestMapRoi
{
    private readonly MapRoiTestFixture _fx;
    public UnitTestMapRoi(MapRoiTestFixture fx) => _fx = fx;

    [Fact(DisplayName = "MapRoi – Insert & Fetch by MapId")]
    public async Task Insert_And_Fetch_MapRoi()
    {
        // Arrange
        var roi = new MapRoiModel
        {
            Title = "초소1구역",
            Latitude = 37.648425,
            Longitude = 126.904284,
            Altitude = 0,
            Zoom = 15,
            MapId = _fx.TestMapId
        };

        // Act
        int id = await _fx.Svc.InsertMapRoiAsync(roi);
        var list = await _fx.Svc.FetchMapRoisAsync(_fx.TestMapId);

        // Assert
        Assert.True(id > 0);
        Assert.NotNull(list);
        Assert.NotEmpty(list);
        var fetched = list!.First(r => r.Id == id);
        Assert.Equal("초소1구역", fetched.Title);
        Assert.Equal(37.648425, fetched.Latitude, 5);
        Assert.Equal(126.904284, fetched.Longitude, 5);
        Assert.Equal(15, fetched.Zoom);

        // Cleanup
        await _fx.Svc.DeleteMapRoiAsync(id);
    }

    [Fact(DisplayName = "MapRoi – Fetch by Id")]
    public async Task Fetch_MapRoi_ById()
    {
        // Arrange
        var roi = new MapRoiModel
        {
            Title = "집중관심구역",
            Latitude = 37.55,
            Longitude = 126.95,
            Altitude = 100.5,
            Zoom = 17,
            MapId = _fx.TestMapId
        };

        int id = await _fx.Svc.InsertMapRoiAsync(roi);

        // Act
        var fetched = await _fx.Svc.FetchMapRoiAsync(id);

        // Assert
        Assert.NotNull(fetched);
        Assert.Equal(id, fetched!.Id);
        Assert.Equal("집중관심구역", fetched.Title);
        Assert.Equal(100.5, fetched.Altitude, 1);
        Assert.Equal(17, fetched.Zoom);

        // Cleanup
        await _fx.Svc.DeleteMapRoiAsync(id);
    }

    [Fact(DisplayName = "MapRoi – Delete")]
    public async Task Delete_MapRoi()
    {
        // Arrange
        var roi = new MapRoiModel
        {
            Title = "삭제테스트",
            Latitude = 37.0,
            Longitude = 127.0,
            Zoom = 10,
            MapId = _fx.TestMapId
        };

        int id = await _fx.Svc.InsertMapRoiAsync(roi);

        // Act
        bool deleted = await _fx.Svc.DeleteMapRoiAsync(id);
        var fetched = await _fx.Svc.FetchMapRoiAsync(id);

        // Assert
        Assert.True(deleted);
        Assert.Null(fetched);
    }

    [Fact(DisplayName = "MapRoi – Insert Multiple & FetchAll")]
    public async Task Insert_Multiple_And_FetchAll()
    {
        // Arrange
        var ids = new List<int>();
        for (int i = 1; i <= 3; i++)
        {
            var roi = new MapRoiModel
            {
                Title = $"관심지역_{i}",
                Latitude = 37.0 + i * 0.01,
                Longitude = 126.9 + i * 0.01,
                Zoom = 14 + i,
                MapId = _fx.TestMapId
            };
            ids.Add(await _fx.Svc.InsertMapRoiAsync(roi));
        }

        // Act
        var list = await _fx.Svc.FetchMapRoisAsync(_fx.TestMapId);

        // Assert
        Assert.NotNull(list);
        Assert.True(list!.Count >= 3);
        foreach (var id in ids)
            Assert.Contains(list, r => r.Id == id);

        // Cleanup
        foreach (var id in ids)
            await _fx.Svc.DeleteMapRoiAsync(id);
    }

    [Fact(DisplayName = "MapRoi – Cascade Delete When Map Deleted")]
    public async Task Delete_Cascade_When_Map_Deleted()
    {
        // Arrange: 별도 Map 생성
        var tempMap = new DefinedMapModel
        {
            Name = "임시지도_Cascade테스트",
            Category = EnumMapCategory.Standard,
            DataType = EnumMapData.Raster,
            MinZoomLevel = 0,
            MaxZoomLevel = 18,
            TileSize = 256,
            Status = EnumMapStatus.Active,
            CreatedBy = "testuser",
            GMapProviderName = "TestProvider",
            Vendor = EnumMapVendor.OpenStreetMap,
            Style = EnumMapStyle.Normal,
            RequiresApiKey = false
        };
        int tempMapId = await _fx.Svc.InsertDefinedMapAsync(tempMap);

        // ROI 2건 삽입
        var roi1Id = await _fx.Svc.InsertMapRoiAsync(new MapRoiModel
        {
            Title = "Cascade_A", Latitude = 37.0, Longitude = 127.0, Zoom = 15, MapId = tempMapId
        });
        var roi2Id = await _fx.Svc.InsertMapRoiAsync(new MapRoiModel
        {
            Title = "Cascade_B", Latitude = 37.1, Longitude = 127.1, Zoom = 16, MapId = tempMapId
        });

        // Act: Map 삭제 → FK CASCADE로 ROI도 삭제되어야 함
        var mapModel = await _fx.Svc.FetchDefinedMapAsync(tempMapId);
        Assert.NotNull(mapModel);
        bool mapDeleted = await _fx.Svc.DeleteDefinedMapAsync(mapModel!);

        // Assert
        Assert.True(mapDeleted);
        var remainingRois = await _fx.Svc.FetchMapRoisAsync(tempMapId);
        Assert.NotNull(remainingRois);
        Assert.Empty(remainingRois!);
    }
}
