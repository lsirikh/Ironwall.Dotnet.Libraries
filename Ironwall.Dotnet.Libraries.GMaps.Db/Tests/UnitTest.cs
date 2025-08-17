using Caliburn.Micro;
using Dapper;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Db.Models;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using MySql.Data.MySqlClient;
using System;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Db.Tests;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/25/2025 2:31:26 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// GMapDbService 전용 Fixture – DB 한 번만 세팅 & 해제
/// </summary>
public sealed class GMapDbFixture : IAsyncLifetime
{
    public IGMapDbService Svc { get; private set; } = null!;
    public MapProvider MapProvider = new();
    public CustomMapProvider CustomMapProvider = null!;
    public DefinedMapProvider DefinedMapProvider = null!;
    internal CancellationTokenSource Cts { get; } = new();

    public int CustomMapCount = 10;
    public int DefinedMapCount = 15;
    public int ControlPointCount = 5; // 각 CustomMap당 기준점 수

    public List<int> InsertedCustomMapIds = new List<int>();
    public List<int> InsertedDefinedMapIds = new List<int>();
    public List<int> InsertedControlPointIds = new List<int>();

    /* 테스트용 지도 테이블 목록 */
    private static readonly string[] _mapTables =
    {
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

    // ────────── IAsyncLifetime ──────────
    [Fact(DisplayName = "Initialize GMap DB Service")]
    public async Task InitializeAsync()
    {
        var log = new LogService();
        var ea = new EventAggregator();

        CustomMapProvider = new CustomMapProvider(log, MapProvider);
        DefinedMapProvider = new DefinedMapProvider(log, MapProvider);
        Svc = new GMapDbService(
            log,
            ea,
            MapProvider,
            CustomMapProvider,
            DefinedMapProvider,
            _setup);

        await DropTablesAsync();               // 깨끗한 DB 확보

        await Svc.StartService(Cts.Token);     // Connect + BuildScheme + FetchInstance
        Assert.True(Svc.IsConnected);
    }

    /* ───────── DB 내부 테이블만 삭제 ────── */
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

        // 외래키 제약조건 때문에 순서가 중요
        foreach (var t in _mapTables)
            await conn.ExecuteAsync($"DROP TABLE IF EXISTS `{t}`;");
    }

    [Fact(DisplayName = "Dispose GMap DB Service")]
    public async Task DisposeAsync()
    {
        await Svc.StopService(Cts.Token);
        await DropTablesAsync();

        if (!Cts.IsCancellationRequested)
            Cts.Cancel();

        Assert.False(Svc.IsConnected);
    }

    /*---------------------------------------------------------------------
      *  A. CustomMap 시드 데이터 생성
      *-------------------------------------------------------------------*/
    [Fact(DisplayName = "Custom Map DB Insert Service")]
    public async Task SeedCustomMapsAsync()
    {
        var random = new Random();

        for (int i = 1; i <= CustomMapCount; i++)
        {
            var customMap = new CustomMapModel
            {
                Name = $"커스텀지도_{i:00}",
                Description = $"테스트용 커스텀 지도 {i}번",
                Category = EnumMapCategory.Standard,
                DataType = EnumMapData.Raster,
                CoordinateSystem = "WGS84",
                EpsgCode = "EPSG:4326",
                MinLatitude = 37.0 + random.NextDouble() * 0.1,
                MaxLatitude = 37.1 + random.NextDouble() * 0.1,
                MinLongitude = 126.0 + random.NextDouble() * 0.1,
                MaxLongitude = 126.1 + random.NextDouble() * 0.1,
                MinZoomLevel = 0,
                MaxZoomLevel = 18,
                TileSize = 256,
                Status = EnumMapStatus.Active,
                CreatedBy = "testuser",
                SourceImagePath = $"/test/images/map_{i}.tif",
                TilesDirectoryPath = $"/test/tiles/map_{i}",
                OriginalWidth = 2048 + random.Next(0, 1024),
                OriginalHeight = 1536 + random.Next(0, 1024),
                OriginalFileSize = 1024 * 1024 * (10 + random.Next(0, 50)), // 10-60MB
                TotalTileCount = 256 + random.Next(0, 512),
                TilesDirectorySize = 1024 * 1024 * (5 + random.Next(0, 25)), // 5-30MB
                PixelResolutionX = 0.000001 * (1 + random.NextDouble()),
                PixelResolutionY = 0.000001 * (1 + random.NextDouble()),
                ResolutionUnit = "degrees",
                GeoReferenceMethod = EnumGeoReference.ManualControlPoints,
                ControlPointCount = ControlPointCount,
                ProcessedAt = DateTime.Now.AddDays(-random.Next(1, 30)),
                ProcessingTimeMinutes = 5 + random.Next(0, 55),
                QualityScore = 0.7 + random.NextDouble() * 0.3,
                ControlPoints = new List<IGeoControlPointModel>()
            };

            // 기준점 생성
            for (int j = 1; j <= ControlPointCount; j++)
            {
                var controlPoint = new GeoControlPointModel
                {
                    PixelX = random.Next(0, customMap.OriginalWidth),
                    PixelY = random.Next(0, customMap.OriginalHeight),
                    Latitude = customMap.MinLatitude.Value + random.NextDouble() *
                              (customMap.MaxLatitude.Value - customMap.MinLatitude.Value),
                    Longitude = customMap.MinLongitude.Value + random.NextDouble() *
                               (customMap.MaxLongitude.Value - customMap.MinLongitude.Value),
                    AccuracyMeters = 1.0 + random.NextDouble() * 5.0,
                    Description = $"기준점_{j}"
                };
                customMap.ControlPoints.Add(controlPoint);
            }

            int id = await Svc.InsertCustomMapAsync(customMap);
            InsertedCustomMapIds.Add(id);
        }
    }

    /*---------------------------------------------------------------------
     *  B. DefinedMap 시드 데이터 생성 (실제 지도 Provider 기반)
     *-------------------------------------------------------------------*/

    [Fact(DisplayName = "Defined Map DB Insert Service")]
    public async Task SeedDefinedMapsAsync()
    {
        var predefinedMaps = new List<DefinedMapModel>
        {
            // ═══════════════════ Google Maps ═══════════════════
            new DefinedMapModel
            {
                Name = "Google 지도",
                Description = "Google 기본 지도 (도로, 지명 표시)",
                Category = EnumMapCategory.Standard,
                DataType = EnumMapData.Raster,
                CoordinateSystem = "WGS84",
                MinZoomLevel = 0,
                MaxZoomLevel = 20,
                TileSize = 256,
                Status = EnumMapStatus.Active,
                CreatedBy = "System",
                GMapProviderName = "GoogleMapProvider",
                ProviderGuid = "GOOGLE-MAP-001",
                Vendor = EnumMapVendor.Google,
                Style = EnumMapStyle.Normal,
                RequiresApiKey = true,
                ApiKey = "test_bing_api_key",
                ServiceUrl = "https://maps.googleapis.com/maps/api/",
                DailyRequestLimit = 25000,
                LicenseInfo = "Google Maps Platform 라이선스"
            },

            new DefinedMapModel
            {
                Name = "Google 위성지도",
                Description = "Google 위성 이미지",
                Category = EnumMapCategory.Satellite,
                DataType = EnumMapData.Raster,
                CoordinateSystem = "WGS84",
                MinZoomLevel = 0,
                MaxZoomLevel = 20,
                TileSize = 256,
                Status = EnumMapStatus.Active,
                CreatedBy = "System",
                GMapProviderName = "GoogleSatelliteMapProvider",
                ProviderGuid = "GOOGLE-SATELLITE-001",
                Vendor = EnumMapVendor.Google,
                Style = EnumMapStyle.Satellite,
                RequiresApiKey = true,
                ApiKey = "test_bing_api_key",
                ServiceUrl = "https://maps.googleapis.com/maps/api/",
                DailyRequestLimit = 25000,
                LicenseInfo = "Google Maps Platform 라이선스"
            },

            new DefinedMapModel
            {
                Name = "Google 하이브리드",
                Description = "Google 위성지도 + 도로/지명 오버레이",
                Category = EnumMapCategory.Hybrid,
                DataType = EnumMapData.Raster,
                CoordinateSystem = "WGS84",
                MinZoomLevel = 0,
                MaxZoomLevel = 20,
                TileSize = 256,
                Status = EnumMapStatus.Active,
                CreatedBy = "System",
                GMapProviderName = "GoogleHybridMapProvider",
                ProviderGuid = "GOOGLE-HYBRID-001",
                Vendor = EnumMapVendor.Google,
                Style = EnumMapStyle.Hybrid,
                RequiresApiKey = true,
                ApiKey = "test_bing_api_key",
                ServiceUrl = "https://maps.googleapis.com/maps/api/",
                DailyRequestLimit = 25000,
                LicenseInfo = "Google Maps Platform 라이선스"
            },

            new DefinedMapModel
            {
                Name = "Google 지형지도",
                Description = "Google 지형 정보 (고도, 산맥 등)",
                Category = EnumMapCategory.Terrain,
                DataType = EnumMapData.Raster,
                CoordinateSystem = "WGS84",
                MinZoomLevel = 0,
                MaxZoomLevel = 15, // Terrain은 보통 줌 제한이 있음
                TileSize = 256,
                Status = EnumMapStatus.Active,
                CreatedBy = "System",
                GMapProviderName = "GoogleTerrainMapProvider",
                ProviderGuid = "GOOGLE-TERRAIN-001",
                Vendor = EnumMapVendor.Google,
                Style = EnumMapStyle.Terrain,
                RequiresApiKey = true,
                ApiKey = "test_bing_api_key",
                ServiceUrl = "https://maps.googleapis.com/maps/api/",
                DailyRequestLimit = 25000,
                LicenseInfo = "Google Maps Platform 라이선스"
            },

            // ═══════════════════ Microsoft Bing Maps ═══════════════════
            new DefinedMapModel
            {
                Name = "Bing 지도",
                Description = "Microsoft Bing 기본 지도",
                Category = EnumMapCategory.Standard,
                DataType = EnumMapData.Raster,
                CoordinateSystem = "WGS84",
                MinZoomLevel = 1,
                MaxZoomLevel = 21,
                TileSize = 256,
                Status = EnumMapStatus.Active,
                CreatedBy = "System",
                GMapProviderName = "BingMapProvider",
                ProviderGuid = "BING-MAP-001",
                Vendor = EnumMapVendor.Microsoft,
                Style = EnumMapStyle.Roads,
                RequiresApiKey = true,
                ApiKey = "test_bing_api_key",
                ServiceUrl = "https://dev.virtualearth.net/REST/v1/",
                DailyRequestLimit = 125000,
                LicenseInfo = "Microsoft Bing Maps 라이선스"
            },

            new DefinedMapModel
            {
                Name = "Bing 위성지도",
                Description = "Microsoft Bing 위성 이미지",
                Category = EnumMapCategory.Satellite,
                DataType = EnumMapData.Raster,
                CoordinateSystem = "WGS84",
                MinZoomLevel = 1,
                MaxZoomLevel = 21,
                TileSize = 256,
                Status = EnumMapStatus.Active,
                CreatedBy = "System",
                GMapProviderName = "BingSatelliteMapProvider",
                ProviderGuid = "BING-SATELLITE-001",
                Vendor = EnumMapVendor.Microsoft,
                Style = EnumMapStyle.Satellite,
                RequiresApiKey = true,
                ApiKey = "test_bing_api_key",
                ServiceUrl = "https://dev.virtualearth.net/REST/v1/",
                DailyRequestLimit = 125000,
                LicenseInfo = "Microsoft Bing Maps 라이선스"
            },

            new DefinedMapModel
            {
                Name = "Bing 하이브리드",
                Description = "Microsoft Bing 위성지도 + 도로/지명",
                Category = EnumMapCategory.Hybrid,
                DataType = EnumMapData.Raster,
                CoordinateSystem = "WGS84",
                MinZoomLevel = 1,
                MaxZoomLevel = 21,
                TileSize = 256,
                Status = EnumMapStatus.Active,
                CreatedBy = "System",
                GMapProviderName = "BingHybridMapProvider",
                ProviderGuid = "BING-HYBRID-001",
                Vendor = EnumMapVendor.Microsoft,
                Style = EnumMapStyle.Hybrid,
                RequiresApiKey = true,
                ApiKey = "test_bing_api_key",
                ServiceUrl = "https://dev.virtualearth.net/REST/v1/",
                DailyRequestLimit = 125000,
                LicenseInfo = "Microsoft Bing Maps 라이선스"
            },

            // ═══════════════════ OpenStreetMap (무료) ═══════════════════
            new DefinedMapModel
            {
                Name = "OpenStreetMap",
                Description = "오픈소스 지도 서비스 (무료)",
                Category = EnumMapCategory.Standard,
                DataType = EnumMapData.Raster,
                CoordinateSystem = "WGS84",
                MinZoomLevel = 0,
                MaxZoomLevel = 19,
                TileSize = 256,
                Status = EnumMapStatus.Active,
                CreatedBy = "System",
                GMapProviderName = "OpenStreetMapProvider",
                ProviderGuid = "OSM-STANDARD-001",
                Vendor = EnumMapVendor.OpenStreetMap,
                Style = EnumMapStyle.Normal,
                RequiresApiKey = false,
                ApiKey = null,
                ServiceUrl = "https://tile.openstreetmap.org/",
                DailyRequestLimit = null, // 무제한이지만 Fair Use Policy 적용
                LicenseInfo = "Open Database License (ODbL)"
            }
        };

        foreach (var definedMap in predefinedMaps)
        {
            int id = await Svc.InsertDefinedMapAsync(definedMap);
            InsertedDefinedMapIds.Add(id);
        }
    }
}

/// xUnit 컬렉션
[CollectionDefinition(nameof(GMapDbCollection))]
public sealed class GMapDbCollection : ICollectionFixture<GMapDbFixture> { }

/*======================================================================
 *  CustomMap CRUD 테스트
 *====================================================================*/
[Collection(nameof(GMapDbCollection))]
public class GMapDb_CustomMapCrudTests
{
    private readonly GMapDbFixture _fx;
    public GMapDb_CustomMapCrudTests(GMapDbFixture fx) => _fx = fx;

    /*──────────────────────────── 01. Fetch & Insert ───────────────────────────*/
    [Fact(DisplayName = "CustomMaps – Insert & Fetch")]
    public async Task Insert_And_Fetch_CustomMaps()
    {
        await _fx.SeedCustomMapsAsync();

        /* 1) FetchCustomMapsAsync → 전체 개수 일치 */
        var all = await _fx.Svc.FetchCustomMapsAsync();
        Assert.NotNull(all);
        Assert.True(all!.Count >= _fx.CustomMapCount);

        /* 2) 각각 FetchCustomMapAsync 로 필드 검증 */
        foreach (var id in _fx.InsertedCustomMapIds)
        {
            var one = await _fx.Svc.FetchCustomMapAsync(id);
            Assert.NotNull(one);
            Assert.Equal(id, one!.Id);
            Assert.Equal(EnumMapProvider.Custom, one.ProviderType);
            Assert.NotNull(one.SourceImagePath);
            Assert.NotNull(one.TilesDirectoryPath);
            Assert.True(one.OriginalWidth > 0);
            Assert.True(one.OriginalHeight > 0);
            Assert.Equal(_fx.ControlPointCount, one.ControlPoints?.Count);

            // 기준점 검증
            if (one.ControlPoints?.Any() == true)
            {
                foreach (var cp in one.ControlPoints)
                {
                    Assert.True(cp.PixelX >= 0);
                    Assert.True(cp.PixelY >= 0);
                    Assert.True(cp.Latitude >= -90 && cp.Latitude <= 90);
                    Assert.True(cp.Longitude >= -180 && cp.Longitude <= 180);
                }
            }
        }
    }

    /*──────────────────────────── 02. Update ───────────────────────────*/
    [Fact(DisplayName = "CustomMaps – Update")]
    public async Task Update_CustomMap_Works()
    {
        await _fx.SeedCustomMapsAsync();
        var customMap = await _fx.Svc.FetchCustomMapAsync(_fx.InsertedCustomMapIds.First());

        /* 수정 */
        customMap!.Name = "UPDATED_NAME";
        customMap.Description = "UPDATED_DESCRIPTION";
        customMap.Status = EnumMapStatus.Processing;
        customMap.QualityScore = 0.95;

        // 기준점 하나 추가
        customMap.ControlPoints.Add(new GeoControlPointModel
        {
            CustomMapId = customMap.Id,
            PixelX = 100,
            PixelY = 100,
            Latitude = 37.5,
            Longitude = 126.9,
            AccuracyMeters = 2.0,
            Description = "추가된 기준점"
        });
        customMap.ControlPointCount = customMap.ControlPoints.Count;

        var updated = await _fx.Svc.UpdateCustomMapAsync(customMap);

        Assert.NotNull(updated);
        Assert.Equal(customMap.Id, updated!.Id);
        Assert.Equal("UPDATED_NAME", updated.Name);
        Assert.Equal("UPDATED_DESCRIPTION", updated.Description);
        Assert.Equal(EnumMapStatus.Processing, updated.Status);
        Assert.Equal(0.95, updated.QualityScore);
        Assert.Equal(_fx.ControlPointCount + 1, updated.ControlPoints?.Count);
    }

    /*──────────────────────────── 03. Delete ───────────────────────────*/
    [Fact(DisplayName = "CustomMaps – Delete")]
    public async Task Delete_CustomMap_Works()
    {
        await _fx.SeedCustomMapsAsync();
        var customMap = await _fx.Svc.FetchCustomMapAsync(_fx.InsertedCustomMapIds.First());

        /* 삭제 */
        bool ok = await _fx.Svc.DeleteCustomMapAsync(customMap!);
        Assert.True(ok);

        /* 실제로 사라졌는지 확인 */
        var fetched = await _fx.Svc.FetchCustomMapAsync(customMap!.Id);
        Assert.Null(fetched);
    }
}

/*======================================================================
 *  DefinedMap CRUD 테스트
 *====================================================================*/
[Collection(nameof(GMapDbCollection))]
public class GMapDb_DefinedMapCrudTests
{
    private readonly GMapDbFixture _fx;
    public GMapDb_DefinedMapCrudTests(GMapDbFixture fx) => _fx = fx;

    /*────────────────────────── 01. Insert & Fetch ─────────────────────────*/
    [Fact(DisplayName = "DefinedMaps – Insert & Fetch")]
    public async Task Insert_And_Fetch_DefinedMaps()
    {
        await _fx.SeedDefinedMapsAsync();

        /* ② FetchAll */
        var all = await _fx.Svc.FetchDefinedMapsAsync();
        Assert.NotNull(all);
        Assert.True(all!.Count >= _fx.DefinedMapCount);

        /* ③ FetchSingle & 검증 */
        foreach (var id in _fx.InsertedDefinedMapIds)
        {
            var one = await _fx.Svc.FetchDefinedMapAsync(id);
            Assert.NotNull(one);
            Assert.Equal(id, one!.Id);
            Assert.Equal(EnumMapProvider.Defined, one.ProviderType);
            Assert.NotNull(one.GMapProviderName);
            Assert.True(Enum.IsDefined(typeof(EnumMapVendor), one.Vendor));
            Assert.True(Enum.IsDefined(typeof(EnumMapStyle), one.Style));

            // API 키 검증
            if (one.RequiresApiKey)
            {
                Assert.NotNull(one.ApiKey);
                Assert.StartsWith("test_api_key_", one.ApiKey);
            }
        }
    }

    /*────────────────────────── 02. Update ─────────────────────────*/
    [Fact(DisplayName = "DefinedMaps – Update")]
    public async Task Update_DefinedMap_Works()
    {
        await _fx.SeedDefinedMapsAsync();
        var definedMap = await _fx.Svc.FetchDefinedMapAsync(_fx.InsertedDefinedMapIds.First());

        /* 수정 */
        definedMap!.Name = "UPDATED_DEFINED_MAP";
        definedMap.Description = "UPDATED_DESCRIPTION";
        definedMap.Status = EnumMapStatus.Inactive;
        definedMap.TodayUsageCount = 500;
        definedMap.LastAccessedAt = DateTime.Now;

        var updated = await _fx.Svc.UpdateDefinedMapAsync(definedMap);

        Assert.NotNull(updated);
        Assert.Equal(definedMap.Id, updated!.Id);
        Assert.Equal("UPDATED_DEFINED_MAP", updated.Name);
        Assert.Equal("UPDATED_DESCRIPTION", updated.Description);
        Assert.Equal(EnumMapStatus.Inactive, updated.Status);
        Assert.Equal(500, updated.TodayUsageCount);
    }

    /*────────────────────────── 03. Delete ─────────────────────────*/
    [Fact(DisplayName = "DefinedMaps – Delete")]
    public async Task Delete_DefinedMap_Works()
    {
        await _fx.SeedDefinedMapsAsync();
        var definedMap = await _fx.Svc.FetchDefinedMapAsync(_fx.InsertedDefinedMapIds.First());

        Assert.True(await _fx.Svc.DeleteDefinedMapAsync(definedMap!));

        var fetched = await _fx.Svc.FetchDefinedMapAsync(definedMap!.Id);
        Assert.Null(fetched);
    }
}

/*======================================================================
 *  GeoControlPoint CRUD 테스트
 *====================================================================*/
[Collection(nameof(GMapDbCollection))]
public class GMapDb_ControlPointCrudTests
{
    private readonly GMapDbFixture _fx;
    public GMapDb_ControlPointCrudTests(GMapDbFixture fx) => _fx = fx;

    /*────────────────────────── 01. Insert & Fetch ─────────────────────────*/
    [Fact(DisplayName = "ControlPoints – Insert & Fetch")]
    public async Task Insert_And_Fetch_ControlPoints()
    {
        await _fx.SeedCustomMapsAsync(); // CustomMap이 있어야 ControlPoint 생성 가능

        var customMapId = _fx.InsertedCustomMapIds.First();

        /* ② FetchControlPoints */
        var controlPoints = await _fx.Svc.FetchControlPointsAsync(customMapId);
        Assert.NotNull(controlPoints);
        Assert.Equal(_fx.ControlPointCount, controlPoints!.Count);

        /* ③ 각 기준점 검증 */
        foreach (var cp in controlPoints)
        {
            Assert.Equal(customMapId, cp.CustomMapId);
            Assert.True(cp.PixelX >= 0);
            Assert.True(cp.PixelY >= 0);
            Assert.True(cp.Latitude >= -90 && cp.Latitude <= 90);
            Assert.True(cp.Longitude >= -180 && cp.Longitude <= 180);
            Assert.True(cp.AccuracyMeters > 0);
        }
    }

    /*────────────────────────── 02. Update ─────────────────────────*/
    [Fact(DisplayName = "ControlPoints – Update")]
    public async Task Update_ControlPoint_Works()
    {
        await _fx.SeedCustomMapsAsync();

        var customMapId = _fx.InsertedCustomMapIds.First();
        var controlPoints = await _fx.Svc.FetchControlPointsAsync(customMapId);
        var controlPoint = controlPoints!.First();

        /* 수정 */
        controlPoint.PixelX = 999;
        controlPoint.PixelY = 888;
        controlPoint.Latitude = 37.123456;
        controlPoint.Longitude = 126.987654;
        controlPoint.AccuracyMeters = 0.5;
        controlPoint.Description = "UPDATED_CONTROL_POINT";

        var updated = await _fx.Svc.UpdateControlPointAsync(controlPoint);

        Assert.NotNull(updated);
        Assert.Equal(controlPoint.Id, updated!.Id);
        Assert.Equal(999, updated.PixelX);
        Assert.Equal(888, updated.PixelY);
        Assert.Equal(37.123456, updated.Latitude);
        Assert.Equal(126.987654, updated.Longitude);
        Assert.Equal(0.5, updated.AccuracyMeters);
        Assert.Equal("UPDATED_CONTROL_POINT", updated.Description);
    }

    /*────────────────────────── 03. Delete ─────────────────────────*/
    [Fact(DisplayName = "ControlPoints – Delete")]
    public async Task Delete_ControlPoint_Works()
    {
        await _fx.SeedCustomMapsAsync();

        var customMapId = _fx.InsertedCustomMapIds.First();
        var controlPoints = await _fx.Svc.FetchControlPointsAsync(customMapId);
        var controlPoint = controlPoints!.First();

        Assert.True(await _fx.Svc.DeleteControlPointAsync(controlPoint));

        // CustomMap을 다시 가져와서 ControlPoint 개수 확인
        var updatedCustomMap = await _fx.Svc.FetchCustomMapAsync(customMapId);
        Assert.Equal(_fx.ControlPointCount - 1, updatedCustomMap!.ControlPoints?.Count);
    }
}

/*======================================================================
 *  Integration 테스트 - 복합 시나리오
 *====================================================================*/
[Collection(nameof(GMapDbCollection))]
public class GMapDb_IntegrationTests
{
    private readonly GMapDbFixture _fx;
    public GMapDb_IntegrationTests(GMapDbFixture fx) => _fx = fx;

    [Fact(DisplayName = "Complete Workflow - CustomMap with ControlPoints")]
    public async Task Complete_CustomMap_Workflow()
    {
        // 1. CustomMap 생성
        var customMap = new CustomMapModel
        {
            Name = "통합테스트_커스텀지도",
            Description = "통합테스트용",
            Category = EnumMapCategory.Standard,
            Status = EnumMapStatus.Processing,
            SourceImagePath = "/test/integration.tif",
            TilesDirectoryPath = "/test/tiles/integration",
            OriginalWidth = 4096,
            OriginalHeight = 3072,
            GeoReferenceMethod = EnumGeoReference.ManualControlPoints,
            ControlPoints = new List<IGeoControlPointModel>()
        };

        int mapId = await _fx.Svc.InsertCustomMapAsync(customMap);
        Assert.True(mapId > 0);

        // 2. ControlPoint 추가
        var controlPoint = new GeoControlPointModel
        {
            CustomMapId = mapId,
            PixelX = 1000,
            PixelY = 1000,
            Latitude = 37.5665,
            Longitude = 126.9780,
            AccuracyMeters = 1.5,
            Description = "서울시청 기준점"
        };

        int cpId = await _fx.Svc.InsertControlPointAsync(controlPoint);
        Assert.True(cpId > 0);

        // 3. 전체 데이터 검증
        var fetchedMap = await _fx.Svc.FetchCustomMapAsync(mapId);
        Assert.NotNull(fetchedMap);
        Assert.Equal("통합테스트_커스텀지도", fetchedMap!.Name);
        Assert.Single(fetchedMap.ControlPoints!);

        var fetchedCp = fetchedMap.ControlPoints!.First();
        Assert.Equal(1000, fetchedCp.PixelX);
        Assert.Equal(37.5665, fetchedCp.Latitude);

        // 4. 상태 업데이트 (Processing → Active)
        fetchedMap.Status = EnumMapStatus.Active;
        fetchedMap.ProcessedAt = DateTime.Now;
        fetchedMap.QualityScore = 0.92;

        var updatedMap = await _fx.Svc.UpdateCustomMapAsync(fetchedMap);
        Assert.Equal(EnumMapStatus.Active, updatedMap!.Status);
        Assert.NotNull(updatedMap.ProcessedAt);
        Assert.Equal(0.92, updatedMap.QualityScore);

        // 5. 정리 (삭제)
        bool deleted = await _fx.Svc.DeleteCustomMapAsync(updatedMap);
        Assert.True(deleted);
    }

    [Fact(DisplayName = "Provider Integration Test")]
    public async Task Provider_Integration_Test()
    {
        // Provider가 비어있는 상태에서 시작
        _fx.MapProvider.Clear();
        _fx.CustomMapProvider.Clear();
        _fx.DefinedMapProvider.Clear();

        // DB에 데이터 삽입
        await _fx.SeedCustomMapsAsync();
        await _fx.SeedDefinedMapsAsync();

        // FetchInstance로 Provider에 로드
        await _fx.Svc.FetchInstanceAsync();

        // Provider 검증
        Assert.True(_fx.MapProvider.Count >= _fx.CustomMapCount + _fx.DefinedMapCount);
        Assert.True(_fx.CustomMapProvider.Count >= _fx.CustomMapCount);
        Assert.True(_fx.DefinedMapProvider.Count >= _fx.DefinedMapCount);

        // Provider 내 데이터 검증
        var customMaps = _fx.CustomMapProvider.CollectionEntity.ToList();
        var definedMaps = _fx.DefinedMapProvider.CollectionEntity.ToList();

        Assert.All(customMaps, cm => Assert.Equal(EnumMapProvider.Custom, cm.ProviderType));
        Assert.All(definedMaps, dm => Assert.Equal(EnumMapProvider.Defined, dm.ProviderType));
    }
}