using Caliburn.Micro;
using Dapper;
using global::Ironwall.Dotnet.Libraries.Base.Services;
using global::Ironwall.Dotnet.Libraries.Enums;
using global::Ironwall.Dotnet.Libraries.GMaps.Db.Models;
using global::Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using global::Ironwall.Dotnet.Libraries.GMaps.Providers;
using global::Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using global::Ironwall.Dotnet.Monitoring.Models.Symbols;
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
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/19/2025 2:37:00 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// GMapDbSymbolService LineSymbol 전용 Fixture
/// </summary>
public sealed class GMapDbLineSymbolFixture : IAsyncLifetime
{
    #region - Properties -
    /// <summary>Symbol DB 서비스 인스턴스</summary>
    public IGMapDbSymbolService Svc { get; private set; } = null!;

    /// <summary>Symbol 데이터 제공자</summary>
    public SymbolProvider SymbolProvider = new();
    public GeometricSymbolProvider GeometrySymbolProvider { get; private set; } = null!;
    public PidsSymbolProvider PidsSymbolProvider { get; private set; } = null!;
    public MilitarySymbolProvider MilitarySymbolProvider { get; private set; } = null!;
    public LineSymbolProvider LineSymbolProvider { get; private set; } = null!;
    public InfraSymbolProvider InfraSymbolProvider { get; private set; } = null!;


    /// <summary>취소 토큰 소스</summary>
    internal CancellationTokenSource Cts { get; } = new();

    /// <summary>테스트용 LineSymbol 생성 개수</summary>
    public int LineSymbolCount = 10;

    /// <summary>삽입된 LineSymbol ID 목록</summary>
    public List<int> InsertedLineSymbolIds = new List<int>();
    #endregion

    #region - Constants -
    /// <summary>테스트용 테이블 목록</summary>
    private static readonly string[] _tables = { "LinePoints", "LineSymbols", "PidsSymbols", "GeometrySymbols", "MilitarySymbols", "InfraSymbols", "Symbols" };

    /// <summary>DB 설정</summary>
    private readonly GMapDbSetupModel _setup = new()
    {
        IpDbServer = "127.0.0.1",
        PortDbServer = 3306,
        DbDatabase = "monitor_db",
        UidDbServer = "root",
        PasswordDbServer = "root"
    };
    #endregion

    #region - IAsyncLifetime Implementation -
    /// <summary>
    /// 테스트 픽스처 초기화 - DB 서비스 시작
    /// </summary>
    [Fact(DisplayName = "Initialize LineSymbol DB Service")]
    public async Task InitializeAsync()
    {
        var log = new LogService();
        var ea = new EventAggregator();
        GeometrySymbolProvider = new GeometricSymbolProvider(log, SymbolProvider);
        PidsSymbolProvider = new PidsSymbolProvider(log, SymbolProvider);
        MilitarySymbolProvider = new MilitarySymbolProvider(log, SymbolProvider);
        LineSymbolProvider = new LineSymbolProvider(log, SymbolProvider);
        InfraSymbolProvider = new InfraSymbolProvider(log, SymbolProvider);
        Svc = new GMapDbSymbolService(log, ea, SymbolProvider, GeometrySymbolProvider, PidsSymbolProvider, MilitarySymbolProvider, LineSymbolProvider, InfraSymbolProvider, _setup);

        await DropTablesAsync();               // 깨끗한 DB 확보
        await Svc.StartService(Cts.Token);     // Connect + BuildScheme + FetchInstance

        Assert.True(Svc.IsConnected);
    }

    /// <summary>
    /// 테스트 픽스처 정리 - DB 서비스 중지
    /// </summary>
    [Fact(DisplayName = "Dispose LineSymbol DB Service")]
    public async Task DisposeAsync()
    {
        await Svc.StopService(Cts.Token);
        await DropTablesAsync();

        if (!Cts.IsCancellationRequested)
            Cts.Cancel();

        Assert.False(Svc.IsConnected);
    }
    #endregion

    #region - Private Methods -
    /// <summary>
    /// DB 내부 테이블 삭제 (CASCADE 순서 고려)
    /// </summary>
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

        // Foreign Key 때문에 순서 중요: 자식 → 부모
        foreach (var t in _tables)
            await conn.ExecuteAsync($"DROP TABLE IF EXISTS `{t}`;");
    }
    #endregion

    #region - Seed Data Methods -
    /// <summary>
    /// LineSymbol 시드 데이터 생성
    /// </summary>
    [Fact(DisplayName = "LineSymbol DB Insert Service")]
    public async Task SeedLineSymbolsAsync()
    {
        var random = new Random();
        var linePatterns = Enum.GetValues<EnumLinePattern>();
        var operationStates = Enum.GetValues<EnumOperationState>();
        var colorTypes = Enum.GetValues<EnumColorType>();

        for (int i = 1; i <= LineSymbolCount; i++)
        {
            // 라인 포인트 생성 (3~8개)
            int pointCount = random.Next(3, 9);
            var linePoints = new List<GeoPoint>();

            double baseLat = 37.5 + random.NextDouble() * 0.1;
            double baseLng = 127.0 + random.NextDouble() * 0.1;

            for (int p = 0; p < pointCount; p++)
            {
                linePoints.Add(new GeoPoint(
                    latitude: baseLat + p * 0.01,
                    longitude: baseLng + p * 0.01,
                    altitude: random.Next(0, 100)
                ));
            }

            var lineSymbol = new LineSymbolModel
            {
                Pid = 40000 + i,
                Title = $"LINE_{i:00}",
                TitleSize = 10 + random.NextDouble() * 5,
                OperationState = operationStates[random.Next(operationStates.Length)],
                Latitude = baseLat,  // 중심점
                Longitude = baseLng,  // 중심점
                Altitude = random.Next(0, 200),
                Zoom = 15 + random.NextDouble() * 3,
                Bearing = random.NextDouble() * 360,
                Width = 60,
                Height = 60,
                Category = EnumMarkerCategory.AREA_BOUNDARY,
                ShowShape = true,
                ShowTitle = random.Next(2) == 0,
                FillColor = EnumColorType.Transparent,
                StrokeColor = colorTypes[random.Next(colorTypes.Length)],
                StrokeThickness = 1.0 + random.NextDouble() * 4.0,

                // LineSymbol 전용 속성
                LinePoints = linePoints,
                LineOpacity = 0.5 + random.NextDouble() * 0.5,
                IsClosedPath = random.Next(2) == 0,
                ShowArrowHead = random.Next(2) == 0,
                LinePattern = linePatterns[random.Next(linePatterns.Length)]
            };

            int id = await Svc.InsertLineSymbolAsync(lineSymbol);
            InsertedLineSymbolIds.Add(id);
        }
    }

    /// <summary>
    /// 특정 패턴별 LineSymbol 시드 데이터 생성
    /// </summary>
    /// <param name="linePattern">생성할 라인 패턴</param>
    /// <param name="count">생성 개수</param>
    /// <returns>생성된 LineSymbol ID 목록</returns>
    public async Task<List<int>> SeedLineSymbolsByPatternAsync(EnumLinePattern linePattern, int count = 5)
    {
        var random = new Random();
        var createdIds = new List<int>();

        for (int i = 1; i <= count; i++)
        {
            // 사각형 형태의 포인트 생성
            var linePoints = new List<GeoPoint>
            {
                new GeoPoint(37.5 + i * 0.01, 127.0, 0),
                new GeoPoint(37.5 + i * 0.01, 127.02, 0),
                new GeoPoint(37.52 + i * 0.01, 127.02, 0),
                new GeoPoint(37.52 + i * 0.01, 127.0, 0)
            };

            var lineSymbol = new LineSymbolModel
            {
                Pid = 41000 + i,
                Title = $"{linePattern}_LINE_{i:00}",
                TitleSize = 12,
                OperationState = EnumOperationState.ACTIVE,
                Latitude = 37.51 + i * 0.01,  // 중심점
                Longitude = 127.01,  // 중심점
                Altitude = 0,
                Zoom = 16,
                Bearing = 0,
                Width = 60,
                Height = 60,
                Category = EnumMarkerCategory.AREA_BOUNDARY,
                ShowShape = true,
                ShowTitle = true,
                FillColor = EnumColorType.Transparent,
                StrokeColor = EnumColorType.Red,
                StrokeThickness = 2.0,

                // LineSymbol 전용 속성
                LinePoints = linePoints,
                LineOpacity = 0.8,
                IsClosedPath = true,  // 닫힌 경로
                ShowArrowHead = false,
                LinePattern = linePattern
            };

            int id = await Svc.InsertLineSymbolAsync(lineSymbol);
            InsertedLineSymbolIds.Add(id);
            createdIds.Add(id);
        }

        return createdIds;
    }

    /// <summary>
    /// 복잡한 경로를 가진 LineSymbol 생성 (많은 포인트)
    /// </summary>
    public async Task<int> CreateComplexLineSymbolAsync(int pointCount = 20)
    {
        var linePoints = new List<GeoPoint>();
        var random = new Random();

        double baseLat = 37.55;
        double baseLng = 127.05;

        // 사인 곡선 형태의 포인트 생성
        for (int i = 0; i < pointCount; i++)
        {
            double lng = baseLng + i * 0.001;
            double lat = baseLat + Math.Sin(i * Math.PI / 5) * 0.005;
            linePoints.Add(new GeoPoint(lat, lng, i * 5));
        }

        var lineSymbol = new LineSymbolModel
        {
            Pid = 42000,
            Title = "COMPLEX_ROUTE",
            TitleSize = 14,
            OperationState = EnumOperationState.ACTIVE,
            Latitude = baseLat,
            Longitude = baseLng + (pointCount * 0.001 / 2), // 중간점
            Altitude = 50,
            Zoom = 14,
            Bearing = 45,
            Width = 100,
            Height = 100,
            Category = EnumMarkerCategory.AREA_BOUNDARY,
            ShowShape = true,
            ShowTitle = true,
            FillColor = EnumColorType.Transparent,
            StrokeColor = EnumColorType.Blue,
            StrokeThickness = 3.0,

            // LineSymbol 전용 속성
            LinePoints = linePoints,
            LineOpacity = 0.9,
            IsClosedPath = false,
            ShowArrowHead = true,
            LinePattern = EnumLinePattern.Solid
        };

        int id = await Svc.InsertLineSymbolAsync(lineSymbol);
        InsertedLineSymbolIds.Add(id);
        return id;
    }
    #endregion
}

/// <summary>
/// xUnit 컬렉션 정의 - LineSymbol
/// </summary>
[CollectionDefinition(nameof(GMapDbLineSymbolCollection))]
public sealed class GMapDbLineSymbolCollection : ICollectionFixture<GMapDbLineSymbolFixture> { }

/*======================================================================
 *  LineSymbol CRUD 기본 테스트
 *====================================================================*/
[Collection(nameof(GMapDbLineSymbolCollection))]
public class GMapDbLineSymbol_BasicCrudTests
{
    private readonly GMapDbLineSymbolFixture _fx;

    /// <summary>
    /// 테스트 클래스 생성자
    /// </summary>
    /// <param name="fx">픽스처</param>
    public GMapDbLineSymbol_BasicCrudTests(GMapDbLineSymbolFixture fx) => _fx = fx;

    /// <summary>
    /// LineSymbol 삽입 및 조회 테스트
    /// </summary>
    [Fact(DisplayName = "LineSymbols – Insert & Fetch")]
    public async Task Insert_And_Fetch_LineSymbols()
    {
        await _fx.SeedLineSymbolsByPatternAsync(EnumLinePattern.Solid);

        /* 1) FetchLineSymbolsAsync → 전체 개수 일치 */
        var all = await _fx.Svc.FetchLineSymbolsAsync();
        Assert.NotNull(all);
        Assert.True(all!.Count >= 5);

        /* 2) 각각 FetchLineSymbolAsync로 필드 검증 */
        foreach (var id in _fx.InsertedLineSymbolIds)
        {
            var one = await _fx.Svc.FetchLineSymbolAsync(id);
            Assert.NotNull(one);
            Assert.Equal(id, one!.Id);
            Assert.True(one.Pid > 0);
            Assert.NotEmpty(one.Title);
            Assert.True(Enum.IsDefined(typeof(EnumOperationState), one.OperationState));
            Assert.Equal(EnumMarkerCategory.AREA_BOUNDARY, one.Category);

            // LineSymbol 전용 속성 검증
            Assert.NotNull(one.LinePoints);
            Assert.True(one.LinePoints.Count >= 3); // 최소 3개 포인트
            Assert.True(one.LineOpacity > 0 && one.LineOpacity <= 1.0);
            Assert.True(Enum.IsDefined(typeof(EnumLinePattern), one.LinePattern));

            // 포인트 순서 검증 (SequenceOrder가 유지되는지)
            for (int i = 0; i < one.LinePoints.Count; i++)
            {
                var point = one.LinePoints[i];
                Assert.True(point.Latitude >= -90 && point.Latitude <= 90);
                Assert.True(point.Longitude >= -180 && point.Longitude <= 180);
            }

            // 위치 범위 검증
            Assert.True(one.Latitude >= -90 && one.Latitude <= 90);
            Assert.True(one.Longitude >= -180 && one.Longitude <= 180);
            Assert.True(one.Width > 0);
            Assert.True(one.Height > 0);
        }
    }

    /// <summary>
    /// LineSymbol 업데이트 테스트 (포인트 포함)
    /// </summary>
    [Fact(DisplayName = "LineSymbols – Update with Points")]
    public async Task Update_LineSymbol_With_Points_Works()
    {
        await _fx.SeedLineSymbolsByPatternAsync(EnumLinePattern.Dashed);
        var lineSymbol = await _fx.Svc.FetchLineSymbolAsync(_fx.InsertedLineSymbolIds.First());

        /* 수정 */
        lineSymbol!.Title = "업데이트된_경로";
        lineSymbol.OperationState = EnumOperationState.FAULT;
        lineSymbol.Latitude = 35.654321;
        lineSymbol.Longitude = 129.123456;
        lineSymbol.Bearing = 90.0;
        lineSymbol.Width = 80;
        lineSymbol.Height = 80;
        lineSymbol.ShowShape = false;
        lineSymbol.ShowTitle = true;
        lineSymbol.StrokeColor = EnumColorType.Yellow;
        lineSymbol.StrokeThickness = 5.0;

        // LineSymbol 전용 속성 수정
        lineSymbol.LineOpacity = 0.5;
        lineSymbol.IsClosedPath = false;
        lineSymbol.ShowArrowHead = true;
        lineSymbol.LinePattern = EnumLinePattern.Dashed;

        // 포인트 변경 (새로운 포인트 세트)
        lineSymbol.LinePoints = new List<GeoPoint>
        {
            new GeoPoint(35.65, 129.12, 10),
            new GeoPoint(35.66, 129.13, 20),
            new GeoPoint(35.67, 129.14, 30),
            new GeoPoint(35.68, 129.15, 40),
            new GeoPoint(35.69, 129.16, 50)
        };

        var updated = await _fx.Svc.UpdateLineSymbolAsync(lineSymbol);

        Assert.NotNull(updated);
        Assert.Equal(lineSymbol.Id, updated!.Id);
        Assert.Equal("업데이트된_경로", updated.Title);
        Assert.Equal(EnumOperationState.FAULT, updated.OperationState);
        Assert.Equal(35.654321, updated.Latitude);
        Assert.Equal(129.123456, updated.Longitude);
        Assert.Equal(90.0, updated.Bearing);
        Assert.Equal(80, updated.Width);
        Assert.Equal(80, updated.Height);
        Assert.False(updated.ShowShape);
        Assert.True(updated.ShowTitle);

        // LineSymbol 전용 속성 검증
        Assert.Equal(0.5, updated.LineOpacity);
        Assert.False(updated.IsClosedPath);
        Assert.True(updated.ShowArrowHead);
        Assert.Equal(EnumLinePattern.Dashed, updated.LinePattern);

        // 포인트 검증
        Assert.Equal(5, updated.LinePoints.Count);
        Assert.Equal(35.65, updated.LinePoints[0].Latitude);
        Assert.Equal(129.12, updated.LinePoints[0].Longitude);
        Assert.Equal(10, updated.LinePoints[0].Altitude);
        Assert.Equal(35.69, updated.LinePoints[4].Latitude);
        Assert.Equal(129.16, updated.LinePoints[4].Longitude);
        Assert.Equal(50, updated.LinePoints[4].Altitude);
    }

    /// <summary>
    /// LineSymbol 삭제 테스트 (CASCADE 확인)
    /// </summary>
    [Fact(DisplayName = "LineSymbols – Delete (CASCADE with Points)")]
    public async Task Delete_LineSymbol_Works()
    {
        await _fx.SeedLineSymbolsAsync();
        var lineSymbol = await _fx.Svc.FetchLineSymbolAsync(_fx.InsertedLineSymbolIds.First());

        /* 삭제 */
        bool ok = await _fx.Svc.DeleteLineSymbolAsync(lineSymbol!);
        Assert.True(ok);

        /* 실제로 사라졌는지 확인 (JOIN 쿼리로 확인) */
        var fetched = await _fx.Svc.FetchLineSymbolAsync(lineSymbol!.Id);
        Assert.Null(fetched);

        /* 기본 Symbol도 같이 삭제되었는지 확인 */
        var baseSymbol = await _fx.Svc.FetchSymbolAsync(lineSymbol.Id);
        Assert.Null(baseSymbol);

        // LinePoints도 CASCADE 삭제되었는지 직접 확인
        await using var conn = await _fx.Svc.OpenConnectionAsync();
        var pointCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM LinePoints WHERE LineSymbolId = @Id",
            new { lineSymbol.Id });
        Assert.Equal(0, pointCount);
    }

    /// <summary>
    /// 복잡한 경로 테스트 (많은 포인트)
    /// </summary>
    [Fact(DisplayName = "LineSymbols – Complex Path with Many Points")]
    public async Task Complex_Path_With_Many_Points_Works()
    {
        int id = await _fx.CreateComplexLineSymbolAsync(50);

        var fetched = await _fx.Svc.FetchLineSymbolAsync(id);
        Assert.NotNull(fetched);
        Assert.Equal(50, fetched!.LinePoints.Count);

        // 포인트 순서 유지 확인
        for (int i = 1; i < fetched.LinePoints.Count; i++)
        {
            var prevPoint = fetched.LinePoints[i - 1];
            var currPoint = fetched.LinePoints[i];

            // Longitude가 순차적으로 증가
            Assert.True(currPoint.Longitude >= prevPoint.Longitude);
            // Altitude도 순차적으로 증가 (i * 5)
            Assert.True(currPoint.Altitude >= prevPoint.Altitude);
        }
    }

    /// <summary>
    /// 빈 포인트 리스트 테스트
    /// </summary>
    [Fact(DisplayName = "LineSymbols – Empty Points List")]
    public async Task Insert_LineSymbol_With_Empty_Points()
    {
        var lineSymbol = new LineSymbolModel
        {
            Pid = 43000,
            Title = "EMPTY_LINE",
            OperationState = EnumOperationState.ACTIVE,
            Latitude = 37.5,
            Longitude = 127.0,
            Category = EnumMarkerCategory.AREA_BOUNDARY,
            LinePoints = new List<GeoPoint>(), // 빈 리스트
            LineOpacity = 1.0,
            LinePattern = EnumLinePattern.Solid
        };

        int id = await _fx.Svc.InsertLineSymbolAsync(lineSymbol);
        Assert.True(id > 0);

        var fetched = await _fx.Svc.FetchLineSymbolAsync(id);
        Assert.NotNull(fetched);
        Assert.Empty(fetched!.LinePoints);
    }

    /// <summary>
    /// 잘못된 매개변수 예외 처리 테스트
    /// </summary>
    [Fact(DisplayName = "LineSymbols – Invalid Parameters")]
    public async Task Invalid_Parameters_Throw_Exceptions()
    {
        /* 잘못된 ID로 조회 */
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _fx.Svc.FetchLineSymbolAsync(-1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _fx.Svc.FetchLineSymbolAsync(0));

        /* null 모델로 삽입 시도 */
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _fx.Svc.InsertLineSymbolAsync(null!));

        /* null 모델로 업데이트 시도 */
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _fx.Svc.UpdateLineSymbolAsync(null!));

        /* null 모델로 삭제 시도 */
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _fx.Svc.DeleteLineSymbolAsync(null!));
    }
}