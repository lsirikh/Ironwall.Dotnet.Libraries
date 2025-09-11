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
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/18/2025 8:18:42 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// GMapDbSymbolService 전용 Fixture – DB 한 번만 세팅 & 해제
/// </summary>
public sealed class GMapDbSymbolFixture : IAsyncLifetime
{
    #region - Properties -
    /// <summary>Symbol DB 서비스 인스턴스</summary>
    public IGMapDbSymbolService Svc { get; private set; } = null!;

    /// <summary>Symbol 데이터 제공자</summary>
    public SymbolProvider SymbolProvider = new();
    public GeometricSymbolProvider GeometrySymbolProvider { get; private set; } = null!;
    public PidsSymbolProvider PidsSymbolProvider { get; private set; } = null!;
    public MilitarySymbolProvider MilitarySymbolProvider { get; private set; } = null!;

    /// <summary>취소 토큰 소스</summary>
    internal CancellationTokenSource Cts { get; } = new();

    /// <summary>테스트용 Symbol 생성 개수</summary>
    public int SymbolCount = 20;

    /// <summary>삽입된 Symbol ID 목록</summary>
    public List<int> InsertedSymbolIds = new List<int>();
    #endregion

    #region - Constants -
    /// <summary>테스트용 Symbol 테이블 목록</summary>
    private static readonly string[] _tables = { "PidsSymbols", "GeometrySymbols", "Symbols", "MilitarySymbols" };
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
    /// <returns>Task</returns>
    [Fact(DisplayName = "Initialize Symbol DB Service")]
    public async Task InitializeAsync()
    {
        var log = new LogService();
        var ea = new EventAggregator();
        GeometrySymbolProvider = new GeometricSymbolProvider(log, SymbolProvider);
        PidsSymbolProvider = new PidsSymbolProvider(log, SymbolProvider);
        MilitarySymbolProvider = new MilitarySymbolProvider(log, SymbolProvider);
        Svc = new GMapDbSymbolService(log, ea, SymbolProvider, GeometrySymbolProvider, PidsSymbolProvider, MilitarySymbolProvider, _setup);

        await DropTablesAsync();               // 깨끗한 DB 확보
        await Svc.StartService(Cts.Token);     // Connect + BuildScheme + FetchInstance

        Assert.True(Svc.IsConnected);
    }

    /// <summary>
    /// 테스트 픽스처 정리 - DB 서비스 중지
    /// </summary>
    /// <returns>Task</returns>
    [Fact(DisplayName = "Dispose Symbol DB Service")]
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
    /// DB 내부 테이블만 삭제
    /// </summary>
    /// <returns>Task</returns>
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

        // 1. 자식 테이블(GeometrySymbols) 먼저 삭제
        await conn.ExecuteAsync("DROP TABLE IF EXISTS `GeometrySymbols`;");

        // 2. 부모 테이블(Symbols) 나중에 삭제
        await conn.ExecuteAsync("DROP TABLE IF EXISTS `Symbols`;");
    }
    #endregion

    #region - Seed Data Methods -
    /// <summary>
    /// Symbol 시드 데이터 생성
    /// </summary>
    /// <returns>Task</returns>
    [Fact(DisplayName = "Symbol DB Insert Service")]
    public async Task SeedSymbolsAsync()
    {
        var random = new Random();
        var categories = Enum.GetValues<EnumMarkerCategory>();
        var operationStates = Enum.GetValues<EnumOperationState>();

        for (int i = 1; i <= SymbolCount; i++)
        {
            var symbol = new SymbolModel
            {
                Pid = 1000 + i,
                Title = $"테스트_심볼_{i:00}",
                TitleSize = 13,
                OperationState = operationStates[random.Next(operationStates.Length)],
                Latitude = 37.0 + random.NextDouble() * 0.6, // 서울 근처
                Longitude = 126.0 + random.NextDouble() * 1.0, // 서울 근처
                Altitude = random.Next(0, 1000),
                Zoom = random.Next(11, 19),
                Bearing = random.NextDouble() * 360,
                Width = 20 + random.NextDouble() * 40,
                Height = 20 + random.NextDouble() * 40,
                Category = categories[random.Next(categories.Length)],
                ShowShape = random.Next(2) == 0,
                ShowTitle = random.Next(2) == 0,
                FillColor = Enum.GetValues<EnumColorType>()[random.Next(Enum.GetValues<EnumColorType>().Length)],
                StrokeColor = Enum.GetValues<EnumColorType>()[random.Next(Enum.GetValues<EnumColorType>().Length)],
                StrokeThickness = 1.0 + random.NextDouble() * 3.0  // 1.0 ~ 4.0
            };

            int id = await Svc.InsertSymbolAsync(symbol);
            InsertedSymbolIds.Add(id);
        }
    }

    /// <summary>
    /// 특정 카테고리 Symbol 시드 데이터 생성
    /// </summary>
    /// <param name="category">생성할 카테고리</param>
    /// <param name="count">생성 개수</param>
    /// <returns>생성된 Symbol ID 목록</returns>
    public async Task<List<int>> SeedSymbolsByCategoryAsync(EnumMarkerCategory category, int count = 5)
    {
        var random = new Random();
        var createdIds = new List<int>();

        for (int i = 1; i <= count; i++)
        {
            var symbol = new SymbolModel
            {
                Pid = 2000 + i,
                Title = $"{category}_심볼_{i:00}",
                TitleSize = 13,
                OperationState = EnumOperationState.ACTIVE,
                Latitude = 37.5 + random.NextDouble() * 0.1,
                Longitude = 126.9 + random.NextDouble() * 0.1,
                Altitude = 0,
                Zoom = 17,
                Bearing = 0,
                Width = 30,
                Height = 30,
                Category = category,
                ShowShape = true,
                ShowTitle = true,
                FillColor = Enum.GetValues<EnumColorType>()[random.Next(Enum.GetValues<EnumColorType>().Length)],
                StrokeColor = Enum.GetValues<EnumColorType>()[random.Next(Enum.GetValues<EnumColorType>().Length)],
                StrokeThickness = 1.0 + random.NextDouble() * 3.0  // 1.0 ~ 4.0
            };

            int id = await Svc.InsertSymbolAsync(symbol);
            createdIds.Add(id);
        }

        return createdIds;
    }
    #endregion
}

/// <summary>
/// xUnit 컬렉션 정의
/// </summary>
[CollectionDefinition(nameof(GMapDbSymbolCollection))]
public sealed class GMapDbSymbolCollection : ICollectionFixture<GMapDbSymbolFixture> { }

/*======================================================================
 *  Symbol CRUD 기본 테스트
 *====================================================================*/
[Collection(nameof(GMapDbSymbolCollection))]
public class GMapDbSymbol_BasicCrudTests
{
    private readonly GMapDbSymbolFixture _fx;

    /// <summary>
    /// 테스트 클래스 생성자
    /// </summary>
    /// <param name="fx">픽스처</param>
    public GMapDbSymbol_BasicCrudTests(GMapDbSymbolFixture fx) => _fx = fx;

    /// <summary>
    /// Symbol 삽입 및 조회 테스트
    /// </summary>
    /// <returns>Task</returns>
    [Fact(DisplayName = "Symbols – Insert & Fetch")]
    public async Task Insert_And_Fetch_Symbols()
    {
        await _fx.SeedSymbolsByCategoryAsync(EnumMarkerCategory.BASIC_SHAPES, _fx.SymbolCount);

        /* 1) FetchSymbolsAsync → 전체 개수 일치 */
        var all = await _fx.Svc.FetchSymbolsAsync();
        Assert.NotNull(all);
        Assert.True(all!.Count >= _fx.SymbolCount);

        foreach (var item in all)
        {
            _fx.InsertedSymbolIds.Add(item.Id);
        }

        /* 2) 각각 FetchSymbolAsync로 필드 검증 */
        foreach (var id in _fx.InsertedSymbolIds)
        {
            var one = await _fx.Svc.FetchSymbolAsync(id);
            Assert.NotNull(one);
            Assert.Equal(id, one!.Id);
            Assert.True(one.Pid > 0);
            Assert.NotEmpty(one.Title);
            Assert.True(Enum.IsDefined(typeof(EnumOperationState), one.OperationState));
            Assert.True(Enum.IsDefined(typeof(EnumMarkerCategory), one.Category));

            // 위치 범위 검증
            Assert.True(one.Latitude >= -90 && one.Latitude <= 90);
            Assert.True(one.Longitude >= -180 && one.Longitude <= 180);
            Assert.True(one.Longitude >= -180 && one.Longitude <= 180);
            Assert.True(one.Zoom >= 11 && one.Zoom <= 20);
            Assert.True(one.Width > 0);
            Assert.True(one.Height > 0);

            // 추가: 색상 관련 속성 검증
            Assert.True(Enum.IsDefined(typeof(EnumColorType), one.FillColor));
            Assert.True(Enum.IsDefined(typeof(EnumColorType), one.StrokeColor));
            Assert.True(one.StrokeThickness > 0);
            Assert.True(one.StrokeThickness <= 10); // 합리적인 범위
        }
    }

    /// <summary>
    /// Symbol 업데이트 테스트
    /// </summary>
    /// <returns>Task</returns>
    [Fact(DisplayName = "Symbols – Update")]
    public async Task Update_Symbol_Works()
    {
        await _fx.SeedSymbolsAsync();
        var symbol = await _fx.Svc.FetchSymbolAsync(_fx.InsertedSymbolIds.First());

        /* 수정 */
        symbol!.Title = "UPDATED_SYMBOL_TITLE";
        symbol.OperationState = EnumOperationState.FAULT;
        symbol.Latitude = 35.123456;
        symbol.Longitude = 129.987654;
        symbol.Bearing = 180.5;
        symbol.Width = 50;
        symbol.Height = 60;
        symbol.Category = EnumMarkerCategory.MILITARY_SYMBOLS;
        symbol.ShowShape = false;
        symbol.ShowTitle = true;
        symbol.FillColor = EnumColorType.Yellow;
        symbol.StrokeColor = EnumColorType.Black;
        symbol.StrokeThickness = 2.5;

        var updated = await _fx.Svc.UpdateSymbolAsync(symbol);

        Assert.NotNull(updated);
        Assert.Equal(symbol.Id, updated!.Id);
        Assert.Equal("UPDATED_SYMBOL_TITLE", updated.Title);
        Assert.Equal(EnumOperationState.FAULT, updated.OperationState);
        Assert.Equal(35.123456, updated.Latitude);
        Assert.Equal(129.987654, updated.Longitude);
        Assert.Equal(180.5, updated.Bearing);
        Assert.Equal(50, updated.Width);
        Assert.Equal(60, updated.Height);
        Assert.Equal(EnumMarkerCategory.MILITARY_SYMBOLS, updated.Category);
        Assert.False(updated.ShowShape);
        Assert.True(updated.ShowTitle);
        Assert.Equal(EnumColorType.Yellow, updated.FillColor);
        Assert.Equal(EnumColorType.Black, updated.StrokeColor);
        Assert.Equal(2.5, updated.StrokeThickness);
    }

    /// <summary>
    /// Symbol 삭제 테스트
    /// </summary>
    /// <returns>Task</returns>
    [Fact(DisplayName = "Symbols – Delete")]
    public async Task Delete_Symbol_Works()
    {
        await _fx.SeedSymbolsAsync();
        var symbol = await _fx.Svc.FetchSymbolAsync(_fx.InsertedSymbolIds.First());

        /* 삭제 */
        bool ok = await _fx.Svc.DeleteSymbolAsync(symbol!);
        Assert.True(ok);

        /* 실제로 사라졌는지 확인 */
        var fetched = await _fx.Svc.FetchSymbolAsync(symbol!.Id);
        Assert.Null(fetched);
    }
}

/*======================================================================
 *  Symbol 특화 기능 테스트 (Pid, Category 기반)
 *====================================================================*/
[Collection(nameof(GMapDbSymbolCollection))]
public class GMapDbSymbol_SpecializedTests
{
    private readonly GMapDbSymbolFixture _fx;

    /// <summary>
    /// 테스트 클래스 생성자
    /// </summary>
    /// <param name="fx">픽스처</param>
    public GMapDbSymbol_SpecializedTests(GMapDbSymbolFixture fx) => _fx = fx;

    /// <summary>
    /// Pid로 Symbol 조회/삭제 테스트
    /// </summary>
    /// <returns>Task</returns>
    [Fact(DisplayName = "Symbols – Fetch & Delete By Pid")]
    public async Task Fetch_And_Delete_By_Pid_Works()
    {
        await _fx.SeedSymbolsAsync();

        // 첫 번째 Symbol의 Pid 가져오기
        var firstSymbol = await _fx.Svc.FetchSymbolAsync(_fx.InsertedSymbolIds.First());
        var targetPid = firstSymbol!.Pid;

        /* 1) Pid로 조회 */
        var fetchedByPid = await _fx.Svc.FetchSymbolByPidAsync(targetPid);
        Assert.NotNull(fetchedByPid);
        Assert.Equal(firstSymbol.Id, fetchedByPid!.Id);
        Assert.Equal(targetPid, fetchedByPid.Pid);

        /* 2) Pid로 삭제 */
        bool deleted = await _fx.Svc.DeleteSymbolByPidAsync(targetPid);
        Assert.True(deleted);

        /* 3) 삭제 확인 */
        var deletedSymbol = await _fx.Svc.FetchSymbolByPidAsync(targetPid);
        Assert.Null(deletedSymbol);
    }

    /// <summary>
    /// 카테고리별 Symbol 조회 테스트
    /// </summary>
    /// <returns>Task</returns>
    [Fact(DisplayName = "Symbols – Fetch By Category")]
    public async Task Fetch_Symbols_By_Category_Works()
    {
        // 특정 카테고리로 Symbol 생성
        var testCategory = EnumMarkerCategory.PIDS_EQUIPMENT;
        var categorySymbolIds = await _fx.SeedSymbolsByCategoryAsync(testCategory, 3);

        /* 카테고리별 조회 */
        var symbolsByCategory = await _fx.Svc.FetchSymbolsByCategoryAsync(testCategory);

        Assert.NotNull(symbolsByCategory);
        Assert.True(symbolsByCategory!.Count >= 3);

        // 모든 Symbol이 해당 카테고리인지 확인
        Assert.All(symbolsByCategory, s => Assert.Equal(testCategory, s.Category));

        // 생성한 Symbol이 모두 포함되어 있는지 확인
        foreach (var createdId in categorySymbolIds)
        {
            Assert.Contains(symbolsByCategory, s => s.Id == createdId);
        }
    }

    /// <summary>
    /// 카테고리별 Symbol 일괄 삭제 테스트
    /// </summary>
    /// <returns>Task</returns>
    [Fact(DisplayName = "Symbols – Delete By Category")]
    public async Task Delete_Symbols_By_Category_Works()
    {
        // 특정 카테고리로 Symbol 생성
        var testCategory = EnumMarkerCategory.VEHICLES;
        var categorySymbolIds = await _fx.SeedSymbolsByCategoryAsync(testCategory, 4);

        /* 카테고리별 일괄 삭제 */
        int deletedCount = await _fx.Svc.DeleteSymbolsByCategoryAsync(testCategory);
        Assert.True(deletedCount >= 4);

        /* 삭제 확인 */
        var remainingSymbols = await _fx.Svc.FetchSymbolsByCategoryAsync(testCategory);
        Assert.NotNull(remainingSymbols);

        // 생성한 Symbol들이 모두 삭제되었는지 확인
        foreach (var deletedId in categorySymbolIds)
        {
            Assert.DoesNotContain(remainingSymbols, s => s.Id == deletedId);
        }
    }

    /// <summary>
    /// 잘못된 매개변수 예외 처리 테스트
    /// </summary>
    /// <returns>Task</returns>
    [Fact(DisplayName = "Symbols – Invalid Parameters")]
    public async Task Invalid_Parameters_Throw_Exceptions()
    {
        /* 잘못된 ID로 조회 */
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _fx.Svc.FetchSymbolAsync(-1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _fx.Svc.FetchSymbolAsync(0));

        /* 잘못된 Pid로 조회 */
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _fx.Svc.FetchSymbolByPidAsync(-1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _fx.Svc.FetchSymbolByPidAsync(0));

        /* 잘못된 Pid로 삭제 */
        await Assert.ThrowsAsync<ArgumentException>(
            () => _fx.Svc.DeleteSymbolByPidAsync(-1));
        await Assert.ThrowsAsync<ArgumentException>(
            () => _fx.Svc.DeleteSymbolByPidAsync(0));
    }
}

/*======================================================================
 *  Integration 테스트 - 복합 시나리오
 *====================================================================*/
[Collection(nameof(GMapDbSymbolCollection))]
public class GMapDbSymbol_IntegrationTests
{
    private readonly GMapDbSymbolFixture _fx;

    /// <summary>
    /// 테스트 클래스 생성자
    /// </summary>
    /// <param name="fx">픽스처</param>
    public GMapDbSymbol_IntegrationTests(GMapDbSymbolFixture fx) => _fx = fx;

    /// <summary>
    /// 완전한 Symbol 워크플로우 테스트
    /// </summary>
    /// <returns>Task</returns>
    [Fact(DisplayName = "Complete Symbol Workflow")]
    public async Task Complete_Symbol_Workflow()
    {
        // 1. Symbol 생성
        var symbol = new SymbolModel
        {
            Pid = 9999,
            Title = "통합테스트_심볼",
            OperationState = EnumOperationState.DEACTIVE,
            Latitude = 37.5665,
            Longitude = 126.9780,
            Altitude = 50,
            Bearing = 45.5,
            Width = 25,
            Height = 25,
            Category = EnumMarkerCategory.ANALYSIS,
            ShowShape = true,
            ShowTitle = false,
            FillColor = EnumColorType.Blue,
            StrokeColor = EnumColorType.White,
            StrokeThickness = 1.5
        };

        int symbolId = await _fx.Svc.InsertSymbolAsync(symbol);
        Assert.True(symbolId > 0);

        // 2. 생성된 Symbol 검증
        var fetchedSymbol = await _fx.Svc.FetchSymbolAsync(symbolId);
        Assert.NotNull(fetchedSymbol);
        Assert.Equal("통합테스트_심볼", fetchedSymbol!.Title);
        Assert.Equal(9999, fetchedSymbol.Pid);
        Assert.Equal(EnumOperationState.DEACTIVE, fetchedSymbol.OperationState);
        Assert.Equal(EnumMarkerCategory.ANALYSIS, fetchedSymbol.Category);

        // 3. Symbol 상태 변경 (DEACTIVE → ACTIVE)
        fetchedSymbol.OperationState = EnumOperationState.ACTIVE;
        fetchedSymbol.Title = "활성화된_심볼";
        fetchedSymbol.ShowTitle = true;

        var updatedSymbol = await _fx.Svc.UpdateSymbolAsync(fetchedSymbol);
        Assert.Equal(EnumOperationState.ACTIVE, updatedSymbol!.OperationState);
        Assert.Equal("활성화된_심볼", updatedSymbol.Title);
        Assert.True(updatedSymbol.ShowTitle);

        // 4. Pid로 조회 확인
        var symbolByPid = await _fx.Svc.FetchSymbolByPidAsync(9999);
        Assert.NotNull(symbolByPid);
        Assert.Equal(updatedSymbol.Id, symbolByPid!.Id);

        // 5. 정리 (삭제)
        bool deleted = await _fx.Svc.DeleteSymbolAsync(updatedSymbol);
        Assert.True(deleted);

        // 6. 삭제 확인
        var deletedSymbol = await _fx.Svc.FetchSymbolAsync(symbolId);
        Assert.Null(deletedSymbol);
    }

    /// <summary>
    /// Provider 통합 테스트
    /// </summary>
    /// <returns>Task</returns>
    [Fact(DisplayName = "Provider Integration Test")]
    public async Task Provider_Integration_Test()
    {
        // Provider가 비어있는 상태에서 시작
        _fx.SymbolProvider.Clear();

        // DB에 Symbol 데이터 삽입
        await _fx.SeedSymbolsByCategoryAsync(EnumMarkerCategory.BASIC_SHAPES, _fx.SymbolCount);

        // FetchInstance로 Provider에 로드
        await _fx.Svc.FetchInstanceAsync();

        // Provider 검증
        Assert.True(_fx.SymbolProvider.Count >= _fx.SymbolCount);

        // Provider 내 데이터 검증
        var symbols = _fx.SymbolProvider.CollectionEntity.ToList();
        Assert.All(symbols, s =>
        {
            Assert.True(s.Id > 0);
            Assert.True(s.Pid > 0);
            Assert.NotEmpty(s.Title);
            Assert.True(Enum.IsDefined(typeof(EnumOperationState), s.OperationState));
            Assert.True(Enum.IsDefined(typeof(EnumMarkerCategory), s.Category));
        });
    }

    /// <summary>
    /// 대량 데이터 처리 성능 테스트
    /// </summary>
    /// <returns>Task</returns>
    [Fact(DisplayName = "Bulk Operations Performance Test")]
    public async Task Bulk_Operations_Performance_Test()
    {
        const int bulkCount = 100;
        var insertedIds = new List<int>();
        var random = new Random();

        // 1. 대량 삽입 성능 측정
        var insertStart = DateTime.Now;

        for (int i = 1; i <= bulkCount; i++)
        {
            var symbol = new SymbolModel
            {
                Pid = 10000 + i,
                Title = $"대량테스트_{i:000}",
                OperationState = EnumOperationState.ACTIVE,
                Latitude = 37.0 + random.NextDouble(),
                Longitude = 126.0 + random.NextDouble(),
                Altitude = 0,
                Bearing = 0,
                Width = 30,
                Height = 30,
                Category = EnumMarkerCategory.BASIC_SHAPES,
                ShowShape = true,
                ShowTitle = false,
                FillColor = EnumColorType.Green,
                StrokeColor = EnumColorType.Red,
                StrokeThickness = 2.0
            };

            int id = await _fx.Svc.InsertSymbolAsync(symbol);
            insertedIds.Add(id);
        }

        var insertDuration = DateTime.Now - insertStart;
        Assert.True(insertDuration.TotalSeconds < 30, "대량 삽입이 30초를 초과했습니다.");

        // 2. 전체 조회 성능 측정
        var fetchStart = DateTime.Now;
        var allSymbols = await _fx.Svc.FetchSymbolsAsync();
        var fetchDuration = DateTime.Now - fetchStart;

        Assert.NotNull(allSymbols);
        Assert.True(allSymbols!.Count >= bulkCount);
        Assert.True(fetchDuration.TotalSeconds < 10, "전체 조회가 10초를 초과했습니다.");

        // 3. 카테고리별 일괄 삭제 성능 측정
        var deleteStart = DateTime.Now;
        int deletedCount = await _fx.Svc.DeleteSymbolsByCategoryAsync(EnumMarkerCategory.BASIC_SHAPES);
        var deleteDuration = DateTime.Now - deleteStart;

        Assert.True(deletedCount >= bulkCount);
        Assert.True(deleteDuration.TotalSeconds < 10, "일괄 삭제가 10초를 초과했습니다.");
    }
}