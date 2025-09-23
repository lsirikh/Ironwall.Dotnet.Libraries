using Caliburn.Micro;
using Dapper;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Db.Models;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using MySql.Data.MySqlClient;
using System;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Db.Tests;

/// <summary>
/// GMapDbSymbolService GeometrySymbol 전용 Fixture
/// </summary>
public sealed class GMapDbGeometrySymbolFixture : GMapBaseSymbolFixture
{
    #region - Properties -
    #endregion

    #region - Constants -
    
    #endregion

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
        foreach (var t in _tables)
            await conn.ExecuteAsync($"DROP TABLE IF EXISTS `{t}`;");
    }
    #endregion

    #region - Seed Data Methods -
    /// <summary>
    /// GeometrySymbol 시드 데이터 생성
    /// </summary>
    [Fact(DisplayName = "GeometrySymbol DB Insert Service")]
    public async Task SeedGeometrySymbolsAsync()
    {
        var random = new Random();
        var shapeTypes = Enum.GetValues<EnumShapeType>();
        var operationStates = Enum.GetValues<EnumOperationState>();

        for (int i = 1; i <= SymbolCount; i++)
        {
            var geometrySymbol = new GeometricSymbolModel
            {
                Pid = 5000 + i,
                Title = $"기하심볼_{i:00}",
                OperationState = operationStates[random.Next(operationStates.Length)],
                Latitude = 37.4 + random.NextDouble() * 0.4, // 서울 남쪽
                Longitude = 126.8 + random.NextDouble() * 0.4,
                Altitude = random.Next(0, 500),
                Bearing = random.NextDouble() * 360,
                Width = 25 + random.NextDouble() * 25,
                Height = 25 + random.NextDouble() * 25,
                Category = EnumMarkerCategory.GEOMETRICS, // 기하 심볼은 BASIC_SHAPES
                ShowShape = random.Next(2) == 0,
                ShowTitle = random.Next(2) == 0,
                FillColor = Enum.GetValues<EnumColorType>()[random.Next(Enum.GetValues<EnumColorType>().Length)],
                StrokeColor = Enum.GetValues<EnumColorType>()[random.Next(Enum.GetValues<EnumColorType>().Length)],
                StrokeThickness = 1.0 + random.NextDouble() * 3.0,  // 1.0 ~ 4.0
                // GeometrySymbol 전용 속성
                ShapeType = shapeTypes[random.Next(shapeTypes.Length)],
                Opacity = 0.5 + random.NextDouble() * 0.5 // 0.5 ~ 1.0
            };

            int id = await Svc.InsertGeometrySymbolAsync(geometrySymbol);
            InsertedSymbolIds.Add(id);
        }
    }

    /// <summary>
    /// 특정 Shape 타입 GeometrySymbol 시드 데이터 생성
    /// </summary>
    /// <param name="shapeType">생성할 Shape 타입</param>
    /// <param name="count">생성 개수</param>
    /// <returns>생성된 GeometrySymbol ID 목록</returns>
    public async Task<List<int>> SeedGeometrySymbolsByShapeTypeAsync(EnumShapeType shapeType, int count = 15)
    {
        var random = new Random();
        var createdIds = new List<int>();

        for (int i = 1; i <= count; i++)
        {
            var geometrySymbol = new GeometricSymbolModel
            {
                Pid = 6000 + i,
                Title = $"{shapeType}_심볼_{i:00}",
                OperationState = EnumOperationState.ACTIVE,
                Latitude = 37.55 + random.NextDouble() * 0.05,
                Longitude = 126.95 + random.NextDouble() * 0.05,
                Altitude = 0,
                Bearing = i * 45, // 45도씩 회전
                Width = 30,
                Height = 30,
                Category = EnumMarkerCategory.GEOMETRICS,
                ShowShape = true,
                ShowTitle = true,
                FillColor = Enum.GetValues<EnumColorType>()[random.Next(Enum.GetValues<EnumColorType>().Length)],
                StrokeColor = Enum.GetValues<EnumColorType>()[random.Next(Enum.GetValues<EnumColorType>().Length)],
                StrokeThickness = 1.0 + random.NextDouble() * 3.0,  // 1.0 ~ 4.0
                // GeometrySymbol 전용 속성
                ShapeType = shapeType,
                Opacity = 0.8
            };

            int id = await Svc.InsertGeometrySymbolAsync(geometrySymbol);
            InsertedSymbolIds.Add(id);
            createdIds.Add(id);
        }

        return createdIds;
    }
    #endregion
}

/// <summary>
/// xUnit 컬렉션 정의 - GeometrySymbol
/// </summary>
[CollectionDefinition(nameof(GMapDbGeometrySymbolCollection))]
public sealed class GMapDbGeometrySymbolCollection : ICollectionFixture<GMapDbGeometrySymbolFixture> { }

/*======================================================================
 *  GeometrySymbol CRUD 기본 테스트
 *====================================================================*/
[Collection(nameof(GMapDbGeometrySymbolCollection))]
public class GMapDbGeometrySymbol_BasicCrudTests
{
    private readonly GMapDbGeometrySymbolFixture _fx;

    /// <summary>
    /// 테스트 클래스 생성자
    /// </summary>
    /// <param name="fx">픽스처</param>
    public GMapDbGeometrySymbol_BasicCrudTests(GMapDbGeometrySymbolFixture fx) => _fx = fx;

    /// <summary>
    /// GeometrySymbol 삽입 및 조회 테스트
    /// </summary>
    [Fact(DisplayName = "GeometrySymbols – Insert & Fetch")]
    public async Task Insert_And_Fetch_GeometrySymbols()
    {
        await _fx.SeedGeometrySymbolsByShapeTypeAsync(EnumShapeType.Circle);

        /* 1) FetchGeometrySymbolsAsync → 전체 개수 일치 */
        var all = await _fx.Svc.FetchGeometrySymbolsByShapeTypeAsync(EnumShapeType.Circle);
        Assert.NotNull(all);
        Assert.True(all!.Count >= _fx.SymbolCount);

        /* 2) 각각 FetchGeometrySymbolAsync로 필드 검증 */
        foreach (var id in _fx.InsertedSymbolIds)
        {
            var one = await _fx.Svc.FetchGeometrySymbolAsync(id);
            Assert.NotNull(one);
            Assert.Equal(id, one!.Id);
            Assert.True(one.Pid > 0);
            Assert.NotEmpty(one.Title);
            Assert.True(Enum.IsDefined(typeof(EnumOperationState), one.OperationState));
            Assert.Equal(EnumMarkerCategory.GEOMETRICS, one.Category);

            // GeometrySymbol 전용 속성 검증
            Assert.True(Enum.IsDefined(typeof(EnumShapeType), one.ShapeType));
            Assert.True(one.Opacity >= 0.0 && one.Opacity <= 1.0);

            // 위치 범위 검증
            Assert.True(one.Latitude >= -90 && one.Latitude <= 90);
            Assert.True(one.Longitude >= -180 && one.Longitude <= 180);
            Assert.True(one.Width > 0);
            Assert.True(one.Height > 0);
        }
    }

    /// <summary>
    /// GeometrySymbol 업데이트 테스트
    /// </summary>
    [Fact(DisplayName = "GeometrySymbols – Update")]
    public async Task Update_GeometrySymbol_Works()
    {
        await _fx.SeedGeometrySymbolsByShapeTypeAsync(EnumShapeType.Circle);
        await _fx.Svc.FetchGeometrySymbolsByShapeTypeAsync(EnumShapeType.Circle);
        var geometrySymbol = await _fx.Svc.FetchGeometrySymbolAsync(_fx.InsertedSymbolIds.First());

        /* 수정 */
        geometrySymbol!.Title = "업데이트된_기하심볼";
        geometrySymbol.OperationState = EnumOperationState.FAULT;
        geometrySymbol.Latitude = 35.654321;
        geometrySymbol.Longitude = 129.123456;
        geometrySymbol.Bearing = 270.0;
        geometrySymbol.Width = 45;
        geometrySymbol.Height = 45;
        geometrySymbol.ShowShape = false;
        geometrySymbol.ShowTitle = true;
        geometrySymbol.FillColor = EnumColorType.Yellow;
        geometrySymbol.StrokeColor = EnumColorType.Black;
        geometrySymbol.StrokeThickness = 2.5;
        // GeometrySymbol 전용 속성 수정
        geometrySymbol.ShapeType = EnumShapeType.Square;
        geometrySymbol.Opacity = 0.3;

        var updated = await _fx.Svc.UpdateGeometrySymbolAsync(geometrySymbol);

        Assert.NotNull(updated);
        Assert.Equal(geometrySymbol.Id, updated!.Id);
        Assert.Equal("업데이트된_기하심볼", updated.Title);
        Assert.Equal(EnumOperationState.FAULT, updated.OperationState);
        Assert.Equal(35.654321, updated.Latitude);
        Assert.Equal(129.123456, updated.Longitude);
        Assert.Equal(270.0, updated.Bearing);
        Assert.Equal(45, updated.Width);
        Assert.Equal(45, updated.Height);
        Assert.False(updated.ShowShape);
        Assert.True(updated.ShowTitle);

        // GeometrySymbol 전용 속성 검증
        Assert.Equal(EnumShapeType.Square, updated.ShapeType);
        Assert.Equal(0.3, updated.Opacity, 2); // 소수점 2자리까지 비교
    }

    /// <summary>
    /// GeometrySymbol 삭제 테스트 (CASCADE 확인)
    /// </summary>
    [Fact(DisplayName = "GeometrySymbols – Delete (CASCADE)")]
    public async Task Delete_GeometrySymbol_Works()
    {
        await _fx.SeedGeometrySymbolsAsync();
        var geometrySymbol = await _fx.Svc.FetchGeometrySymbolAsync(_fx.InsertedSymbolIds.First());

        /* 삭제 */
        bool ok = await _fx.Svc.DeleteGeometrySymbolAsync(geometrySymbol!);
        Assert.True(ok);

        /* 실제로 사라졌는지 확인 (JOIN 쿼리로 확인) */
        var fetched = await _fx.Svc.FetchGeometrySymbolAsync(geometrySymbol!.Id);
        Assert.Null(fetched);

        /* 기본 Symbol도 같이 삭제되었는지 확인 */
        var baseSymbol = await _fx.Svc.FetchSymbolAsync(geometrySymbol.Id);
        Assert.Null(baseSymbol);
    }
}

/*======================================================================
 *  GeometrySymbol 특화 기능 테스트 (ShapeType 기반)
 *====================================================================*/
[Collection(nameof(GMapDbGeometrySymbolCollection))]
public class GMapDbGeometrySymbol_SpecializedTests
{
    private readonly GMapDbGeometrySymbolFixture _fx;

    /// <summary>
    /// 테스트 클래스 생성자
    /// </summary>
    /// <param name="fx">픽스처</param>
    public GMapDbGeometrySymbol_SpecializedTests(GMapDbGeometrySymbolFixture fx) => _fx = fx;

    /// <summary>
    /// ShapeType별 GeometrySymbol 조회 테스트
    /// </summary>
    [Fact(DisplayName = "GeometrySymbols – Fetch By ShapeType")]
    public async Task Fetch_GeometrySymbols_By_ShapeType_Works()
    {
        // 특정 ShapeType으로 GeometrySymbol 생성
        var testShapeType = EnumShapeType.Triangle;
        var triangleSymbolIds = await _fx.SeedGeometrySymbolsByShapeTypeAsync(testShapeType, 4);

        /* ShapeType별 조회 */
        var symbolsByShapeType = await _fx.Svc.FetchGeometrySymbolsByShapeTypeAsync(testShapeType);

        Assert.NotNull(symbolsByShapeType);
        Assert.True(symbolsByShapeType!.Count >= 4);

        // 모든 GeometrySymbol이 해당 ShapeType인지 확인
        Assert.All(symbolsByShapeType, s => Assert.Equal(testShapeType, s.ShapeType));

        // 생성한 GeometrySymbol이 모두 포함되어 있는지 확인
        foreach (var createdId in triangleSymbolIds)
        {
            Assert.Contains(symbolsByShapeType, s => s.Id == createdId);
        }
    }

    /// <summary>
    /// 모든 ShapeType 테스트
    /// </summary>
    [Fact(DisplayName = "GeometrySymbols – All ShapeTypes")]
    public async Task All_ShapeTypes_Work()
    {
        var shapeTypes = Enum.GetValues<EnumShapeType>();
        var createdSymbolIds = new Dictionary<EnumShapeType, List<int>>();

        /* 각 ShapeType별로 GeometrySymbol 생성 */
        foreach (var shapeType in shapeTypes)
        {
            var ids = await _fx.SeedGeometrySymbolsByShapeTypeAsync(shapeType, 2);
            createdSymbolIds[shapeType] = ids;
        }

        /* 각 ShapeType별로 조회 및 검증 */
        foreach (var shapeType in shapeTypes)
        {
            var symbols = await _fx.Svc.FetchGeometrySymbolsByShapeTypeAsync(shapeType);

            Assert.NotNull(symbols);
            Assert.True(symbols!.Count >= 2);
            Assert.All(symbols, s => Assert.Equal(shapeType, s.ShapeType));

            // 생성한 ID들이 모두 포함되어 있는지 확인
            var expectedIds = createdSymbolIds[shapeType];
            foreach (var expectedId in expectedIds)
            {
                Assert.Contains(symbols, s => s.Id == expectedId);
            }
        }
    }

    /// <summary>
    /// 투명도(Opacity) 경계값 테스트
    /// </summary>
    [Fact(DisplayName = "GeometrySymbols – Opacity Boundary Test")]
    public async Task Opacity_Boundary_Test()
    {
        // 경계값 테스트: 0.0, 0.5, 1.0
        var opacityValues = new[] { 0.0, 0.5, 1.0 };
        var createdIds = new List<int>();

        for (int i = 0; i < opacityValues.Length; i++)
        {
            var geometrySymbol = new GeometricSymbolModel
            {
                Pid = 7000 + i,
                Title = $"투명도테스트_{opacityValues[i]:F1}",
                OperationState = EnumOperationState.ACTIVE,
                Latitude = 37.5,
                Longitude = 126.9,
                Altitude = 0,
                Bearing = 0,
                Width = 30,
                Height = 30,
                Category = EnumMarkerCategory.GEOMETRICS,
                ShowShape = true,
                ShowTitle = false,
                FillColor = EnumColorType.Yellow,
                StrokeColor = EnumColorType.Black,
                StrokeThickness = 2.5,
                ShapeType = EnumShapeType.Circle,
                Opacity = opacityValues[i]
            };

            int id = await _fx.Svc.InsertGeometrySymbolAsync(geometrySymbol);
            createdIds.Add(id);
        }

        /* 생성된 GeometrySymbol들의 투명도 검증 */
        for (int i = 0; i < createdIds.Count; i++)
        {
            var fetched = await _fx.Svc.FetchGeometrySymbolAsync(createdIds[i]);
            Assert.NotNull(fetched);
            Assert.Equal(opacityValues[i], fetched!.Opacity, 2); // 소수점 2자리까지 비교
        }
    }

    /// <summary>
    /// 잘못된 매개변수 예외 처리 테스트
    /// </summary>
    [Fact(DisplayName = "GeometrySymbols – Invalid Parameters")]
    public async Task Invalid_Parameters_Throw_Exceptions()
    {
        /* 잘못된 ID로 조회 */
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _fx.Svc.FetchGeometrySymbolAsync(-1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _fx.Svc.FetchGeometrySymbolAsync(0));

        /* 잘못된 ID로 삭제 시도 */
        var invalidGeometrySymbol = new GeometricSymbolModel { Id = -1 };
        await Assert.ThrowsAsync<ArgumentException>(
            () => _fx.Svc.DeleteGeometrySymbolAsync(invalidGeometrySymbol));

        var zeroIdGeometrySymbol = new GeometricSymbolModel { Id = 0 };
        await Assert.ThrowsAsync<ArgumentException>(
            () => _fx.Svc.DeleteGeometrySymbolAsync(zeroIdGeometrySymbol));
    }
}

/*======================================================================
 *  GeometrySymbol Integration 테스트 - 복합 시나리오
 *====================================================================*/
[Collection(nameof(GMapDbGeometrySymbolCollection))]
public class GMapDbGeometrySymbol_IntegrationTests
{
    private readonly GMapDbGeometrySymbolFixture _fx;

    /// <summary>
    /// 테스트 클래스 생성자
    /// </summary>
    /// <param name="fx">픽스처</param>
    public GMapDbGeometrySymbol_IntegrationTests(GMapDbGeometrySymbolFixture fx) => _fx = fx;

    /// <summary>
    /// 완전한 GeometrySymbol 워크플로우 테스트
    /// </summary>
    [Fact(DisplayName = "Complete GeometrySymbol Workflow")]
    public async Task Complete_GeometrySymbol_Workflow()
    {
        // 1. GeometrySymbol 생성
        var geometrySymbol = new GeometricSymbolModel
        {
            Pid = 8888,
            Title = "통합테스트_기하심볼",
            OperationState = EnumOperationState.DEACTIVE,
            Latitude = 37.5665,
            Longitude = 126.9780,
            Altitude = 100,
            Bearing = 90.0,
            Width = 40,
            Height = 40,
            Category = EnumMarkerCategory.BASIC_SHAPES,
            ShowShape = true,
            ShowTitle = false,
            FillColor = EnumColorType.Yellow,
            StrokeColor = EnumColorType.Black,
            StrokeThickness = 2.5,
            ShapeType = EnumShapeType.Circle,
            Opacity = 0.7
        };

        int symbolId = await _fx.Svc.InsertGeometrySymbolAsync(geometrySymbol);
        Assert.True(symbolId > 0);

        // 2. 생성된 GeometrySymbol 검증
        var fetchedSymbol = await _fx.Svc.FetchGeometrySymbolAsync(symbolId);
        Assert.NotNull(fetchedSymbol);
        Assert.Equal("통합테스트_기하심볼", fetchedSymbol!.Title);
        Assert.Equal(8888, fetchedSymbol.Pid);
        Assert.Equal(EnumOperationState.DEACTIVE, fetchedSymbol.OperationState);
        Assert.Equal(EnumMarkerCategory.BASIC_SHAPES, fetchedSymbol.Category);
        Assert.Equal(EnumShapeType.Circle, fetchedSymbol.ShapeType);
        Assert.Equal(0.7, fetchedSymbol.Opacity, 2);

        // 3. GeometrySymbol 변경 (Circle → Triangle, 상태 변경)
        fetchedSymbol.OperationState = EnumOperationState.ACTIVE;
        fetchedSymbol.Title = "활성화된_삼각형";
        fetchedSymbol.ShowTitle = true;
        fetchedSymbol.ShapeType = EnumShapeType.Triangle;
        fetchedSymbol.Opacity = 1.0;

        var updatedSymbol = await _fx.Svc.UpdateGeometrySymbolAsync(fetchedSymbol);
        Assert.Equal(EnumOperationState.ACTIVE, updatedSymbol!.OperationState);
        Assert.Equal("활성화된_삼각형", updatedSymbol.Title);
        Assert.True(updatedSymbol.ShowTitle);
        Assert.Equal(EnumShapeType.Triangle, updatedSymbol.ShapeType);
        Assert.Equal(1.0, updatedSymbol.Opacity, 2);

        // 4. ShapeType별 조회 확인
        var triangleSymbols = await _fx.Svc.FetchGeometrySymbolsByShapeTypeAsync(EnumShapeType.Triangle);
        Assert.NotNull(triangleSymbols);
        Assert.Contains(triangleSymbols, s => s.Id == updatedSymbol.Id);

        // 5. 정리 (삭제)
        bool deleted = await _fx.Svc.DeleteGeometrySymbolAsync(updatedSymbol);
        Assert.True(deleted);

        // 6. 삭제 확인 (GeometrySymbol과 기본 Symbol 모두)
        var deletedGeometrySymbol = await _fx.Svc.FetchGeometrySymbolAsync(symbolId);
        Assert.Null(deletedGeometrySymbol);

        var deletedBaseSymbol = await _fx.Svc.FetchSymbolAsync(symbolId);
        Assert.Null(deletedBaseSymbol);
    }

    /// <summary>
    /// Provider 통합 테스트 (GeometrySymbol 포함)
    /// </summary>
    [Fact(DisplayName = "Provider Integration Test with GeometrySymbols")]
    public async Task Provider_Integration_Test_With_GeometrySymbols()
    {
        // Provider가 비어있는 상태에서 시작
        _fx.SymbolProvider.Clear();

        // DB에 GeometrySymbol 데이터 삽입
        await _fx.SeedGeometrySymbolsByShapeTypeAsync(EnumShapeType.Circle, _fx.SymbolCount);

        // FetchInstance로 Provider에 로드 (GeometrySymbol 포함)
        await _fx.Svc.FetchInstanceAsync();
        await _fx.Svc.FetchGeometrySymbolsAsync();

        // Provider 검증
        Assert.True(_fx.SymbolProvider.Count >= _fx.SymbolCount);

        // Provider 내 GeometrySymbol 데이터 검증
        var geometrySymbols = _fx.SymbolProvider.CollectionEntity
            .OfType<GeometricSymbolModel>()
            .ToList();

        Assert.True(geometrySymbols.Count >= _fx.SymbolCount);
        Assert.All(geometrySymbols, s =>
        {
            Assert.True(s.Id > 0);
            Assert.True(s.Pid > 0);
            Assert.NotEmpty(s.Title);
            Assert.Equal(EnumMarkerCategory.GEOMETRICS, s.Category);
            Assert.True(Enum.IsDefined(typeof(EnumShapeType), s.ShapeType));
            Assert.True(s.Opacity >= 0.0 && s.Opacity <= 1.0);
        });
    }

    /// <summary>
    /// 트랜잭션 롤백 테스트
    /// </summary>
    [Fact(DisplayName = "Transaction Rollback Test")]
    public async Task Transaction_Rollback_Test()
    {
        // 유효하지 않은 데이터로 삽입 시도 (트랜잭션 실패 유도)
        var invalidGeometrySymbol = new GeometricSymbolModel
        {
            Pid = -1, // 유효하지 않은 Pid
            Title = "", // 빈 제목
            Latitude = 200, // 유효하지 않은 위도
            Longitude = 400, // 유효하지 않은 경도
            Category = EnumMarkerCategory.GEOMETRICS,
            ShapeType = EnumShapeType.Circle,
            Opacity = 0.5
        };

        // 삽입 전 GeometrySymbol 개수 확인
        var beforeCount = (await _fx.Svc.FetchGeometrySymbolsAsync())?.Count ?? 0;

        // 유효하지 않은 데이터 삽입 시도 (예외 발생 예상)
        await Assert.ThrowsAnyAsync<Exception>(
            () => _fx.Svc.InsertGeometrySymbolAsync(invalidGeometrySymbol));

        // 삽입 후 GeometrySymbol 개수가 변하지 않았는지 확인 (롤백 확인)
        var afterCount = (await _fx.Svc.FetchGeometrySymbolsAsync())?.Count ?? 0;
        Assert.Equal(beforeCount, afterCount);
    }

}