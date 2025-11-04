using Caliburn.Micro;
using Dapper;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Db.Models;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using MySql.Data.MySqlClient;
using System;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Db.Tests;

/// <summary>
/// GMapDbSymbolService PidsGroupSymbol 전용 Fixture
/// </summary>
public sealed class GMapDbPidsGroupSymbolFixture : GMapBaseSymbolFixture
{
    #region - Private Methods -
    /// <summary>
    /// DB 내부 테이블 삭제 (CASCADE 순서 고려)
    /// </summary>
    protected override async Task DropTablesAsync()
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

        // Foreign Key 때문에 순서 중요: 자식 → 부모
        await conn.ExecuteAsync("DROP TABLE IF EXISTS `PidsGroupPoints`;");
        foreach (var t in _tables)
            await conn.ExecuteAsync($"DROP TABLE IF EXISTS `{t}`;");
    }
    #endregion

    #region - Seed Data Methods -
    /// <summary>
    /// PidsGroupSymbol 시드 데이터 생성
    /// </summary>
    [Fact(DisplayName = "PidsGroupSymbol DB Insert Service")]
    public async Task SeedPidsGroupSymbolsAsync()
    {
        var random = new Random();
        var eventStatuses = Enum.GetValues<EnumEventStatus>();
        var linePatterns = Enum.GetValues<EnumLinePattern>();
        var colorTypes = Enum.GetValues<EnumColorType>();

        for (int i = 1; i <= SymbolCount; i++)
        {
            // 경계선 포인트 생성 (사각형 또는 다각형)
            var points = GeneratePolygonPoints(
                baseLatitude: 37.5 + random.NextDouble() * 0.1,
                baseLongitude: 126.9 + random.NextDouble() * 0.1,
                radius: 0.001 + random.NextDouble() * 0.003,
                sides: random.Next(3, 8)
            );

            var pidsGroupSymbol = new PidsGroupSymbolModel
            {
                Pid = 30000 + i,
                Title = $"PIDS_그룹_{i:00}",
                TitleSize = 10 + random.Next(5),
                OperationState = EnumOperationState.ACTIVE,
                Latitude = 37.5 + random.NextDouble() * 0.2,
                Longitude = 126.9 + random.NextDouble() * 0.2,
                Altitude = random.Next(0, 100),
                Zoom = 15 + random.Next(5),
                Bearing = random.NextDouble() * 360,
                Width = 100 + random.NextDouble() * 100,
                Height = 100 + random.NextDouble() * 100,
                Category = EnumMarkerCategory.AREA_BOUNDARY,
                ShowShape = true,
                ShowTitle = random.Next(2) == 0,
                FillColor = colorTypes[random.Next(colorTypes.Length)],
                StrokeColor = colorTypes[random.Next(colorTypes.Length)],
                StrokeThickness = 2.0 + random.NextDouble() * 2.0,
                // PidsGroupSymbol 전용 속성
                LinkedDeviceGroup = 40000 + i,
                EventStatus = eventStatuses[random.Next(eventStatuses.Length)],
                LineOpacity = 0.5 + random.NextDouble() * 0.5,
                IsClosedPath = true, // 그룹은 대부분 닫힌 경로
                ShowArrowHead = false,
                LinePattern = linePatterns[random.Next(linePatterns.Length)],
                LinePoints = points
            };

            int id = await Svc.InsertPidsGroupSymbolAsync(pidsGroupSymbol);
            InsertedSymbolIds.Add(id);
        }
    }

    /// <summary>
    /// 특정 EventStatus PidsGroupSymbol 시드 데이터 생성
    /// </summary>
    public async Task<List<int>> SeedPidsGroupSymbolsByEventStatusAsync(EnumEventStatus eventStatus, int count = 5)
    {
        var random = new Random();
        var createdIds = new List<int>();

        for (int i = 1; i <= count; i++)
        {
            var points = GenerateRectanglePoints(
                centerLat: 37.55,
                centerLng: 126.95,
                width: 0.005,
                height: 0.003
            );

            var pidsGroupSymbol = new PidsGroupSymbolModel
            {
                Pid = 31000 + i,
                Title = $"그룹_{eventStatus}_{i:00}",
                OperationState = EnumOperationState.ACTIVE,
                Latitude = 37.55,
                Longitude = 126.95,
                Category = EnumMarkerCategory.AREA_BOUNDARY,
                LinkedDeviceGroup = 41000 + i,
                EventStatus = eventStatus,
                LineOpacity = 0.8,
                IsClosedPath = true,
                ShowArrowHead = false,
                LinePattern = EnumLinePattern.Solid,
                LinePoints = points
            };

            int id = await Svc.InsertPidsGroupSymbolAsync(pidsGroupSymbol);
            InsertedSymbolIds.Add(id);
            createdIds.Add(id);
        }

        return createdIds;
    }

    /// <summary>
    /// 다각형 포인트 생성 헬퍼 메서드
    /// </summary>
    private List<GeoPoint> GeneratePolygonPoints(double baseLatitude, double baseLongitude, double radius, int sides)
    {
        var points = new List<GeoPoint>();
        for (int i = 0; i < sides; i++)
        {
            var angle = 2 * Math.PI * i / sides;
            var lat = baseLatitude + radius * Math.Sin(angle);
            var lng = baseLongitude + radius * Math.Cos(angle);
            points.Add(new GeoPoint(lat, lng, 0));
        }
        return points;
    }

    /// <summary>
    /// 사각형 포인트 생성 헬퍼 메서드
    /// </summary>
    private List<GeoPoint> GenerateRectanglePoints(double centerLat, double centerLng, double width, double height)
    {
        return new List<GeoPoint>
        {
            new GeoPoint(centerLat - height/2, centerLng - width/2, 0),
            new GeoPoint(centerLat - height/2, centerLng + width/2, 0),
            new GeoPoint(centerLat + height/2, centerLng + width/2, 0),
            new GeoPoint(centerLat + height/2, centerLng - width/2, 0)
        };
    }
    #endregion
}

/// <summary>
/// xUnit 컬렉션 정의 - PidsGroupSymbol
/// </summary>
[CollectionDefinition(nameof(GMapDbPidsGroupSymbolCollection))]
public sealed class GMapDbPidsGroupSymbolCollection : ICollectionFixture<GMapDbPidsGroupSymbolFixture> { }

/*======================================================================
 *  PidsGroupSymbol CRUD 기본 테스트
 *====================================================================*/
[Collection(nameof(GMapDbPidsGroupSymbolCollection))]
public class GMapDbPidsGroupSymbol_BasicCrudTests
{
    private readonly GMapDbPidsGroupSymbolFixture _fx;

    public GMapDbPidsGroupSymbol_BasicCrudTests(GMapDbPidsGroupSymbolFixture fx) => _fx = fx;

    /// <summary>
    /// PidsGroupSymbol 삽입 및 조회 테스트
    /// </summary>
    [Fact(DisplayName = "PidsGroupSymbols – Insert & Fetch")]
    public async Task Insert_And_Fetch_PidsGroupSymbols()
    {
        await _fx.SeedPidsGroupSymbolsAsync();

        /* 1) FetchPidsGroupSymbolsAsync → 전체 개수 일치 */
        var all = await _fx.Svc.FetchPidsGroupSymbolsAsync();
        Assert.NotNull(all);
        Assert.True(all!.Count >= _fx.SymbolCount);

        /* 2) 각각 FetchPidsGroupSymbolAsync로 필드 검증 */
        foreach (var id in _fx.InsertedSymbolIds)
        {
            var one = await _fx.Svc.FetchPidsGroupSymbolAsync(id);
            Assert.NotNull(one);
            Assert.Equal(id, one!.Id);
            Assert.True(one.Pid > 0);
            Assert.NotEmpty(one.Title);
            Assert.Equal(EnumMarkerCategory.AREA_BOUNDARY, one.Category);

            // PidsGroupSymbol 전용 속성 검증
            Assert.True(one.LinkedDeviceGroup > 0);
            Assert.True(Enum.IsDefined(typeof(EnumEventStatus), one.EventStatus));
            Assert.True(one.LineOpacity >= 0.0 && one.LineOpacity <= 1.0);
            Assert.True(Enum.IsDefined(typeof(EnumLinePattern), one.LinePattern));

            // LinePoints 검증
            Assert.NotNull(one.LinePoints);
            Assert.True(one.LinePoints.Count >= 3); // 최소 3개 포인트
        }
    }

    /// <summary>
    /// PidsGroupSymbol 업데이트 테스트
    /// </summary>
    [Fact(DisplayName = "PidsGroupSymbols – Update")]
    public async Task Update_PidsGroupSymbol_Works()
    {
        await _fx.SeedPidsGroupSymbolsByEventStatusAsync(EnumEventStatus.Normal);
        var pidsGroupSymbol = await _fx.Svc.FetchPidsGroupSymbolAsync(_fx.InsertedSymbolIds.First());

        /* 수정 */
        pidsGroupSymbol!.Title = "업데이트된_그룹";
        pidsGroupSymbol.LinkedDeviceGroup = 99999;
        pidsGroupSymbol.EventStatus = EnumEventStatus.Detecting;
        pidsGroupSymbol.LineOpacity = 0.3;
        pidsGroupSymbol.IsClosedPath = false;
        pidsGroupSymbol.ShowArrowHead = true;
        pidsGroupSymbol.LinePattern = EnumLinePattern.Dashed;

        // 포인트 변경
        var newPoints = new List<GeoPoint>
        {
            new GeoPoint(37.6, 127.0, 0),
            new GeoPoint(37.6, 127.01, 0),
            new GeoPoint(37.61, 127.01, 0),
            new GeoPoint(37.61, 127.0, 0),
            new GeoPoint(37.605, 127.005, 10) // 5각형으로 변경
        };
        pidsGroupSymbol.LinePoints = newPoints;

        var updated = await _fx.Svc.UpdatePidsGroupSymbolAsync(pidsGroupSymbol);

        Assert.NotNull(updated);
        Assert.Equal("업데이트된_그룹", updated!.Title);
        Assert.Equal(99999, updated.LinkedDeviceGroup);
        Assert.Equal(EnumEventStatus.Detecting, updated.EventStatus);
        Assert.Equal(0.3, updated.LineOpacity, 2);
        Assert.False(updated.IsClosedPath);
        Assert.True(updated.ShowArrowHead);
        Assert.Equal(EnumLinePattern.Dashed, updated.LinePattern);

        // 포인트 업데이트 검증
        Assert.NotNull(updated.LinePoints);
        Assert.Equal(5, updated.LinePoints.Count);
        Assert.Equal(37.605, updated.LinePoints[4].Latitude, 3);
        Assert.Equal(127.005, updated.LinePoints[4].Longitude, 3);
        Assert.Equal(10, updated.LinePoints[4].Altitude);
    }

    /// <summary>
    /// PidsGroupSymbol 삭제 테스트 (CASCADE 확인)
    /// </summary>
    [Fact(DisplayName = "PidsGroupSymbols – Delete (CASCADE)")]
    public async Task Delete_PidsGroupSymbol_Works()
    {
        await _fx.SeedPidsGroupSymbolsByEventStatusAsync(EnumEventStatus.Fault);
        var pidsGroupSymbol = await _fx.Svc.FetchPidsGroupSymbolAsync(_fx.InsertedSymbolIds.First());

        /* 삭제 */
        bool ok = await _fx.Svc.DeletePidsGroupSymbolAsync(pidsGroupSymbol!);
        Assert.True(ok);

        /* 실제로 사라졌는지 확인 */
        var fetched = await _fx.Svc.FetchPidsGroupSymbolAsync(pidsGroupSymbol!.Id);
        Assert.Null(fetched);

        /* 기본 Symbol도 같이 삭제되었는지 확인 */
        var baseSymbol = await _fx.Svc.FetchSymbolAsync(pidsGroupSymbol.Id);
        Assert.Null(baseSymbol);
    }

    /// <summary>
    /// LinkedDeviceGroup으로 삭제 테스트
    /// </summary>
    [Fact(DisplayName = "PidsGroupSymbols – Delete By DeviceGroup")]
    public async Task Delete_By_DeviceGroup_Works()
    {
        var ids = await _fx.SeedPidsGroupSymbolsByEventStatusAsync(EnumEventStatus.Connection, 1);
        var pidsGroupSymbol = await _fx.Svc.FetchPidsGroupSymbolAsync(ids.First());

        /* LinkedDeviceGroup으로 삭제 */
        bool deleted = await _fx.Svc.DeletePidsGroupSymbolByDeviceGroupAsync(pidsGroupSymbol!.LinkedDeviceGroup);
        Assert.True(deleted);

        /* 삭제 확인 */
        var deletedSymbol = await _fx.Svc.FetchPidsGroupSymbolAsync(pidsGroupSymbol.Id);
        Assert.Null(deletedSymbol);
    }
}

/*======================================================================
 *  PidsGroupSymbol 특화 기능 테스트 (EventStatus, LinePoints 등)
 *====================================================================*/
[Collection(nameof(GMapDbPidsGroupSymbolCollection))]
public class GMapDbPidsGroupSymbol_SpecializedTests
{
    private readonly GMapDbPidsGroupSymbolFixture _fx;

    public GMapDbPidsGroupSymbol_SpecializedTests(GMapDbPidsGroupSymbolFixture fx) => _fx = fx;

    /// <summary>
    /// 포인트 순서 보장 테스트
    /// </summary>
    [Fact(DisplayName = "PidsGroupSymbols – Point Order Preservation")]
    public async Task Point_Order_Preservation_Test()
    {
        // 특정 순서의 포인트로 PidsGroupSymbol 생성
        var orderedPoints = new List<GeoPoint>
        {
            new GeoPoint(37.5, 127.0, 100),
            new GeoPoint(37.51, 127.01, 200),
            new GeoPoint(37.52, 127.02, 300),
            new GeoPoint(37.53, 127.03, 400),
            new GeoPoint(37.54, 127.04, 500)
        };

        var pidsGroupSymbol = new PidsGroupSymbolModel
        {
            Pid = 35000,
            Title = "포인트순서테스트",
            Category = EnumMarkerCategory.AREA_BOUNDARY,
            LinkedDeviceGroup = 45000,
            EventStatus = EnumEventStatus.Normal,
            LineOpacity = 0.8,
            IsClosedPath = false,
            LinePattern = EnumLinePattern.Solid,
            LinePoints = orderedPoints
        };

        int id = await _fx.Svc.InsertPidsGroupSymbolAsync(pidsGroupSymbol);

        /* 저장된 포인트 순서 검증 */
        var fetched = await _fx.Svc.FetchPidsGroupSymbolAsync(id);
        Assert.NotNull(fetched);
        Assert.NotNull(fetched!.LinePoints);
        Assert.Equal(5, fetched.LinePoints.Count);

        // 순서가 보장되는지 확인
        for (int i = 0; i < orderedPoints.Count; i++)
        {
            Assert.Equal(orderedPoints[i].Latitude, fetched.LinePoints[i].Latitude, 6);
            Assert.Equal(orderedPoints[i].Longitude, fetched.LinePoints[i].Longitude, 6);
            Assert.Equal(orderedPoints[i].Altitude, fetched.LinePoints[i].Altitude, 1);
        }
    }

    

    /// <summary>
    /// 빈 포인트 리스트 처리 테스트
    /// </summary>
    [Fact(DisplayName = "PidsGroupSymbols – Empty Points List")]
    public async Task Empty_Points_List_Test()
    {
        var pidsGroupSymbol = new PidsGroupSymbolModel
        {
            Pid = 36000,
            Title = "빈포인트테스트",
            Category = EnumMarkerCategory.AREA_BOUNDARY,
            LinkedDeviceGroup = 46000,
            EventStatus = EnumEventStatus.Normal,
            LineOpacity = 0.5,
            IsClosedPath = true,
            LinePattern = EnumLinePattern.Solid,
            LinePoints = new List<GeoPoint>() // 빈 리스트
        };

        int id = await _fx.Svc.InsertPidsGroupSymbolAsync(pidsGroupSymbol);

        /* 빈 포인트 리스트로 저장 후 조회 */
        var fetched = await _fx.Svc.FetchPidsGroupSymbolAsync(id);
        Assert.NotNull(fetched);
        Assert.NotNull(fetched!.LinePoints);
        Assert.Empty(fetched.LinePoints);
    }

    /// <summary>
    /// LinePattern 종류별 테스트
    /// </summary>
    [Fact(DisplayName = "PidsGroupSymbols – All LinePatterns")]
    public async Task All_LinePatterns_Work()
    {
        var linePatterns = Enum.GetValues<EnumLinePattern>();
        var createdIds = new Dictionary<EnumLinePattern, int>();

        foreach (var pattern in linePatterns)
        {
            var points = new List<GeoPoint>
            {
                new GeoPoint(37.5, 127.0, 0),
                new GeoPoint(37.51, 127.01, 0),
                new GeoPoint(37.51, 127.0, 0)
            };

            var pidsGroupSymbol = new PidsGroupSymbolModel
            {
                Pid = 37000 + (int)pattern,
                Title = $"패턴테스트_{pattern}",
                Category = EnumMarkerCategory.AREA_BOUNDARY,
                LinkedDeviceGroup = 47000 + (int)pattern,
                EventStatus = EnumEventStatus.Normal,
                LineOpacity = 0.8,
                IsClosedPath = true,
                LinePattern = pattern,
                LinePoints = points
            };

            int id = await _fx.Svc.InsertPidsGroupSymbolAsync(pidsGroupSymbol);
            createdIds[pattern] = id;
        }

        /* 각 LinePattern 검증 */
        foreach (var pattern in linePatterns)
        {
            var fetched = await _fx.Svc.FetchPidsGroupSymbolAsync(createdIds[pattern]);
            Assert.NotNull(fetched);
            Assert.Equal(pattern, fetched!.LinePattern);
        }
    }

    /// <summary>
    /// LineOpacity 경계값 테스트
    /// </summary>
    [Fact(DisplayName = "PidsGroupSymbols – LineOpacity Boundary Test")]
    public async Task LineOpacity_Boundary_Test()
    {
        var opacityValues = new[] { 0.0, 0.5, 1.0 };
        var createdIds = new List<int>();

        for (int i = 0; i < opacityValues.Length; i++)
        {
            var pidsGroupSymbol = new PidsGroupSymbolModel
            {
                Pid = 38000 + i,
                Title = $"투명도테스트_{opacityValues[i]:F1}",
                Category = EnumMarkerCategory.AREA_BOUNDARY,
                LinkedDeviceGroup = 48000 + i,
                EventStatus = EnumEventStatus.Normal,
                LineOpacity = opacityValues[i],
                IsClosedPath = true,
                LinePattern = EnumLinePattern.Solid,
                LinePoints = new List<GeoPoint>()
            };

            int id = await _fx.Svc.InsertPidsGroupSymbolAsync(pidsGroupSymbol);
            createdIds.Add(id);
        }

        /* 투명도 값 검증 */
        for (int i = 0; i < createdIds.Count; i++)
        {
            var fetched = await _fx.Svc.FetchPidsGroupSymbolAsync(createdIds[i]);
            Assert.NotNull(fetched);
            Assert.Equal(opacityValues[i], fetched!.LineOpacity, 2);
        }
    }
}

/*======================================================================
 *  PidsGroupSymbol Integration 테스트
 *====================================================================*/
[Collection(nameof(GMapDbPidsGroupSymbolCollection))]
public class GMapDbPidsGroupSymbol_IntegrationTests
{
    private readonly GMapDbPidsGroupSymbolFixture _fx;

    public GMapDbPidsGroupSymbol_IntegrationTests(GMapDbPidsGroupSymbolFixture fx) => _fx = fx;

    /// <summary>
    /// 완전한 PidsGroupSymbol 워크플로우 테스트
    /// </summary>
    [Fact(DisplayName = "Complete PidsGroupSymbol Workflow")]
    public async Task Complete_PidsGroupSymbol_Workflow()
    {
        // 1. 복잡한 다각형 경계를 가진 PidsGroupSymbol 생성
        var complexPoints = new List<GeoPoint>
        {
            new GeoPoint(37.5, 127.0, 0),
            new GeoPoint(37.502, 127.003, 10),
            new GeoPoint(37.505, 127.002, 20),
            new GeoPoint(37.504, 126.998, 15),
            new GeoPoint(37.501, 126.997, 5)
        };

        var pidsGroupSymbol = new PidsGroupSymbolModel
        {
            Pid = 39000,
            Title = "통합테스트_보안구역",
            TitleSize = 14,
            OperationState = EnumOperationState.ACTIVE,
            Latitude = 37.5025,
            Longitude = 127.0,
            Altitude = 50,
            Zoom = 17,
            Bearing = 45.0,
            Width = 200,
            Height = 150,
            Category = EnumMarkerCategory.AREA_BOUNDARY,
            ShowShape = true,
            ShowTitle = true,
            FillColor = EnumColorType.Red,
            StrokeColor = EnumColorType.Yellow,
            StrokeThickness = 3.0,
            // PidsGroupSymbol 전용 속성
            LinkedDeviceGroup = 49000,
            EventStatus = EnumEventStatus.Normal,
            LineOpacity = 0.6,
            IsClosedPath = true,
            ShowArrowHead = false,
            LinePattern = EnumLinePattern.DashDot,
            LinePoints = complexPoints
        };

        int symbolId = await _fx.Svc.InsertPidsGroupSymbolAsync(pidsGroupSymbol);
        Assert.True(symbolId > 0);

        // 2. 생성된 PidsGroupSymbol 검증
        var fetchedSymbol = await _fx.Svc.FetchPidsGroupSymbolAsync(symbolId);
        Assert.NotNull(fetchedSymbol);
        Assert.Equal("통합테스트_보안구역", fetchedSymbol!.Title);
        Assert.Equal(39000, fetchedSymbol.Pid);
        Assert.Equal(49000, fetchedSymbol.LinkedDeviceGroup);
        Assert.Equal(EnumEventStatus.Normal, fetchedSymbol.EventStatus);
        Assert.Equal(0.6, fetchedSymbol.LineOpacity, 2);
        Assert.True(fetchedSymbol.IsClosedPath);
        Assert.Equal(EnumLinePattern.DashDot, fetchedSymbol.LinePattern);
        Assert.Equal(5, fetchedSymbol.LinePoints.Count);

        // 3. 이벤트 발생 시뮬레이션 (Normal → Detecting)
        fetchedSymbol.EventStatus = EnumEventStatus.Detecting;
        fetchedSymbol.Title = "침입감지_구역";
        fetchedSymbol.StrokeColor = EnumColorType.Orange;
        fetchedSymbol.StrokeThickness = 5.0;

        // 경계 확장
        fetchedSymbol.LinePoints.Add(new GeoPoint(37.499, 127.001, 0));

        var updatedSymbol = await _fx.Svc.UpdatePidsGroupSymbolAsync(fetchedSymbol);
        Assert.Equal(EnumEventStatus.Detecting, updatedSymbol!.EventStatus);
        Assert.Equal("침입감지_구역", updatedSymbol.Title);
        Assert.Equal(6, updatedSymbol.LinePoints.Count);


        // 5. 정리 (삭제)
        bool deleted = await _fx.Svc.DeletePidsGroupSymbolAsync(updatedSymbol);
        Assert.True(deleted);

        // 6. 삭제 확인
        var deletedSymbol = await _fx.Svc.FetchPidsGroupSymbolAsync(symbolId);
        Assert.Null(deletedSymbol);
    }

    /// <summary>
    /// Provider 통합 테스트 (PidsGroupSymbol 포함)
    /// </summary>
    [Fact(DisplayName = "Provider Integration Test with PidsGroupSymbols")]
    public async Task Provider_Integration_Test_With_PidsGroupSymbols()
    {
        // Provider 초기화
        _fx.PidsGroupSymbolProvider.Clear();

        // DB에 PidsGroupSymbol 데이터 삽입
        await _fx.SeedPidsGroupSymbolsAsync();

        // FetchInstance로 Provider에 로드
        await _fx.Svc.FetchInstanceAsync();

        // Provider 검증
        Assert.True(_fx.PidsGroupSymbolProvider.Count >= _fx.SymbolCount);

        // Provider 내 PidsGroupSymbol 데이터 검증
        var pidsGroupSymbols = _fx.PidsGroupSymbolProvider.CollectionEntity
            .OfType<PidsGroupSymbolModel>()
            .ToList();

        Assert.True(pidsGroupSymbols.Count >= _fx.SymbolCount);
        Assert.All(pidsGroupSymbols, s =>
        {
            Assert.True(s.Id > 0);
            Assert.NotEmpty(s.Title);
            Assert.Equal(EnumMarkerCategory.AREA_BOUNDARY, s.Category);
            Assert.True(s.LinkedDeviceGroup > 0);
            Assert.True(Enum.IsDefined(typeof(EnumEventStatus), s.EventStatus));
            Assert.True(s.LineOpacity >= 0.0 && s.LineOpacity <= 1.0);
            Assert.NotNull(s.LinePoints);
        });
    }

    /// <summary>
    /// 대량 포인트 처리 테스트
    /// </summary>
    [Fact(DisplayName = "Large Points Dataset Test")]
    public async Task Large_Points_Dataset_Test()
    {
        // 100개의 포인트를 가진 복잡한 경계 생성
        var manyPoints = new List<GeoPoint>();
        for (int i = 0; i < 100; i++)
        {
            var angle = 2 * Math.PI * i / 100;
            var lat = 37.5 + 0.01 * Math.Sin(angle);
            var lng = 127.0 + 0.01 * Math.Cos(angle);
            manyPoints.Add(new GeoPoint(lat, lng, i));
        }

        var largeGroupSymbol = new PidsGroupSymbolModel
        {
            Pid = 40000,
            Title = "대량포인트테스트",
            Category = EnumMarkerCategory.AREA_BOUNDARY,
            LinkedDeviceGroup = 50000,
            EventStatus = EnumEventStatus.Normal,
            LineOpacity = 0.7,
            IsClosedPath = true,
            LinePattern = EnumLinePattern.Solid,
            LinePoints = manyPoints
        };

        int id = await _fx.Svc.InsertPidsGroupSymbolAsync(largeGroupSymbol);

        /* 대량 포인트 저장 및 조회 검증 */
        var fetched = await _fx.Svc.FetchPidsGroupSymbolAsync(id);
        Assert.NotNull(fetched);
        Assert.NotNull(fetched!.LinePoints);
        Assert.Equal(100, fetched.LinePoints.Count);

        // 모든 포인트가 올바른 순서로 저장되었는지 확인
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(manyPoints[i].Altitude, fetched.LinePoints[i].Altitude);
        }
    }
}