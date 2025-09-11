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
/// GMapDbSymbolService MilitarySymbol 전용 Fixture
/// </summary>
public sealed class GMapDbMilitarySymbolFixture : IAsyncLifetime
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

    /// <summary>테스트용 MilitarySymbol 생성 개수</summary>
    public int MilitarySymbolCount = 15;

    /// <summary>삽입된 MilitarySymbol ID 목록</summary>
    public List<int> InsertedMilitarySymbolIds = new List<int>();
    #endregion

    #region - Constants -
    /// <summary>테스트용 테이블 목록</summary>
    private static readonly string[] _tables = { "PidsSymbols", "GeometrySymbols", "MilitarySymbols", "Symbols" };

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
    [Fact(DisplayName = "Initialize MilitarySymbol DB Service")]
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
    [Fact(DisplayName = "Dispose MilitarySymbol DB Service")]
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
    /// MilitarySymbol 시드 데이터 생성
    /// </summary>
    [Fact(DisplayName = "MilitarySymbol DB Insert Service")]
    public async Task SeedMilitarySymbolsAsync()
    {
        var random = new Random();
        var affiliations = Enum.GetValues<EnumMilitaryAffiliation>();
        var battleDimensions = Enum.GetValues<EnumMilitaryBattleDimension>();
        var standardIdentities = Enum.GetValues<EnumMilitaryStandardIdentity>();
        var unitTypes = Enum.GetValues<EnumMilitaryUnitType>();
        var unitSizes = Enum.GetValues<EnumMilitaryUnitSize>();
        var operationStates = Enum.GetValues<EnumOperationState>();
        var colorTypes = Enum.GetValues<EnumColorType>();

        for (int i = 1; i <= MilitarySymbolCount; i++)
        {
            var militarySymbol = new MilitarySymbolModel
            {
                Pid = 30000 + i,
                Title = $"MILITARY_{i:00}",
                OperationState = operationStates[random.Next(operationStates.Length)],
                Latitude = 37.4 + random.NextDouble() * 0.4, // 서울 남쪽
                Longitude = 126.8 + random.NextDouble() * 0.4,
                Altitude = random.Next(0, 500),
                Bearing = random.NextDouble() * 360,
                Width = 40 + random.NextDouble() * 20,
                Height = 40 + random.NextDouble() * 20,
                Category = EnumMarkerCategory.MILITARY_SYMBOLS,
                ShowShape = random.Next(2) == 0,
                ShowTitle = random.Next(2) == 0,
                FillColor = colorTypes[random.Next(colorTypes.Length)],
                StrokeColor = colorTypes[random.Next(colorTypes.Length)],
                StrokeThickness = 1.0 + random.NextDouble() * 3.0,  // 1.0 ~ 4.0

                // MilitarySymbol 전용 속성
                Affiliation = affiliations[random.Next(affiliations.Length)],
                BattleDimension = battleDimensions[random.Next(battleDimensions.Length)],
                StandardIdentity = standardIdentities[random.Next(standardIdentities.Length)],
                UnitType = unitTypes[random.Next(unitTypes.Length)],
                UnitSize = unitSizes[random.Next(unitSizes.Length)],
                UnitDesignator = $"Unit-{i:00}",
                HigherFormation = $"Formation-{(i % 5) + 1}",
                CallSign = $"Call-{i:00}",
                CountryCode = i % 3 == 0 ? "KOR" : (i % 3 == 1 ? "USA" : "CHN")
            };

            int id = await Svc.InsertMilitarySymbolAsync(militarySymbol);
            InsertedMilitarySymbolIds.Add(id);
        }
    }

    /// <summary>
    /// 특정 소속 구분별 MilitarySymbol 시드 데이터 생성
    /// </summary>
    /// <param name="affiliation">생성할 소속 구분</param>
    /// <param name="count">생성 개수</param>
    /// <returns>생성된 MilitarySymbol ID 목록</returns>
    public async Task<List<int>> SeedMilitarySymbolsByAffiliationAsync(EnumMilitaryAffiliation affiliation, int count = 15)
    {
        var random = new Random();
        var createdIds = new List<int>();

        for (int i = 1; i <= count; i++)
        {
            var militarySymbol = new MilitarySymbolModel
            {
                Pid = 31000 + i,
                Title = $"{affiliation}_부대_{i:00}",
                OperationState = EnumOperationState.ACTIVE,
                Latitude = 37.55 + random.NextDouble() * 0.05,
                Longitude = 126.95 + random.NextDouble() * 0.05,
                Altitude = 0,
                Bearing = i * 45, // 45도씩 회전
                Width = 50,
                Height = 50,
                Category = EnumMarkerCategory.MILITARY_SYMBOLS,
                ShowShape = true,
                ShowTitle = true,
                FillColor = EnumColorType.Blue,
                StrokeColor = EnumColorType.White,
                StrokeThickness = 2.0,

                // MilitarySymbol 전용 속성
                Affiliation = affiliation,
                BattleDimension = EnumMilitaryBattleDimension.Land,
                StandardIdentity = EnumMilitaryStandardIdentity.Present,
                UnitType = EnumMilitaryUnitType.Infantry,
                UnitSize = EnumMilitaryUnitSize.Company,
                UnitDesignator = $"{affiliation}-{i:00}",
                HigherFormation = $"{affiliation} Brigade",
                CallSign = $"{affiliation}-{i:00}",
                CountryCode = "KOR"
            };

            int id = await Svc.InsertMilitarySymbolAsync(militarySymbol);
            InsertedMilitarySymbolIds.Add(id);
            createdIds.Add(id);
        }

        return createdIds;
    }

    /// <summary>
    /// 특정 부대 종류별 MilitarySymbol 시드 데이터 생성
    /// </summary>
    /// <param name="unitType">생성할 부대 종류</param>
    /// <param name="count">생성 개수</param>
    /// <returns>생성된 MilitarySymbol ID 목록</returns>
    public async Task<List<int>> SeedMilitarySymbolsByUnitTypeAsync(EnumMilitaryUnitType unitType, int count = 10)
    {
        var random = new Random();
        var createdIds = new List<int>();

        for (int i = 1; i <= count; i++)
        {
            var militarySymbol = new MilitarySymbolModel
            {
                Pid = 32000 + i,
                Title = $"{unitType}_부대_{i:00}",
                OperationState = EnumOperationState.ACTIVE,
                Latitude = 37.50 + random.NextDouble() * 0.10,
                Longitude = 126.90 + random.NextDouble() * 0.10,
                Altitude = 0,
                Bearing = 0,
                Width = 45,
                Height = 45,
                Category = EnumMarkerCategory.MILITARY_SYMBOLS,
                ShowShape = true,
                ShowTitle = true,
                FillColor = EnumColorType.Green,
                StrokeColor = EnumColorType.Black,
                StrokeThickness = 1.5,

                // MilitarySymbol 전용 속성
                Affiliation = EnumMilitaryAffiliation.Friend,
                BattleDimension = EnumMilitaryBattleDimension.Land,
                StandardIdentity = EnumMilitaryStandardIdentity.Present,
                UnitType = unitType,
                UnitSize = EnumMilitaryUnitSize.Company,
                UnitDesignator = $"{unitType}-{i:00}",
                HigherFormation = $"{unitType} Regiment",
                CallSign = $"{unitType.ToString().Substring(0, 3)}-{i:00}",
                CountryCode = "KOR"
            };

            int id = await Svc.InsertMilitarySymbolAsync(militarySymbol);
            InsertedMilitarySymbolIds.Add(id);
            createdIds.Add(id);
        }

        return createdIds;
    }
    #endregion
}

/// <summary>
/// xUnit 컬렉션 정의 - MilitarySymbol
/// </summary>
[CollectionDefinition(nameof(GMapDbMilitarySymbolCollection))]
public sealed class GMapDbMilitarySymbolCollection : ICollectionFixture<GMapDbMilitarySymbolFixture> { }

/*======================================================================
 *  MilitarySymbol CRUD 기본 테스트
 *====================================================================*/
[Collection(nameof(GMapDbMilitarySymbolCollection))]
public class GMapDbMilitarySymbol_BasicCrudTests
{
    private readonly GMapDbMilitarySymbolFixture _fx;

    /// <summary>
    /// 테스트 클래스 생성자
    /// </summary>
    /// <param name="fx">픽스처</param>
    public GMapDbMilitarySymbol_BasicCrudTests(GMapDbMilitarySymbolFixture fx) => _fx = fx;

    /// <summary>
    /// MilitarySymbol 삽입 및 조회 테스트
    /// </summary>
    [Fact(DisplayName = "MilitarySymbols – Insert & Fetch")]
    public async Task Insert_And_Fetch_MilitarySymbols()
    {
        await _fx.SeedMilitarySymbolsByAffiliationAsync(EnumMilitaryAffiliation.Friend);

        /* 1) FetchMilitarySymbolsAsync → 전체 개수 일치 */
        var all = await _fx.Svc.FetchMilitarySymbolsAsync();
        Assert.NotNull(all);
        Assert.True(all!.Count >= _fx.MilitarySymbolCount);

        /* 2) 각각 FetchMilitarySymbolAsync로 필드 검증 */
        foreach (var id in _fx.InsertedMilitarySymbolIds)
        {
            var one = await _fx.Svc.FetchMilitarySymbolAsync(id);
            Assert.NotNull(one);
            Assert.Equal(id, one!.Id);
            Assert.True(one.Pid > 0);
            Assert.NotEmpty(one.Title);
            Assert.True(Enum.IsDefined(typeof(EnumOperationState), one.OperationState));
            Assert.Equal(EnumMarkerCategory.MILITARY_SYMBOLS, one.Category);

            // MilitarySymbol 전용 속성 검증
            Assert.True(Enum.IsDefined(typeof(EnumMilitaryAffiliation), one.Affiliation));
            Assert.True(Enum.IsDefined(typeof(EnumMilitaryBattleDimension), one.BattleDimension));
            Assert.True(Enum.IsDefined(typeof(EnumMilitaryStandardIdentity), one.StandardIdentity));
            Assert.True(Enum.IsDefined(typeof(EnumMilitaryUnitType), one.UnitType));
            Assert.True(Enum.IsDefined(typeof(EnumMilitaryUnitSize), one.UnitSize));

            // 위치 범위 검증
            Assert.True(one.Latitude >= -90 && one.Latitude <= 90);
            Assert.True(one.Longitude >= -180 && one.Longitude <= 180);
            Assert.True(one.Width > 0);
            Assert.True(one.Height > 0);
        }
    }

    /// <summary>
    /// MilitarySymbol 업데이트 테스트
    /// </summary>
    [Fact(DisplayName = "MilitarySymbols – Update")]
    public async Task Update_MilitarySymbol_Works()
    {
        await _fx.SeedMilitarySymbolsByAffiliationAsync(EnumMilitaryAffiliation.Friend);
        var militarySymbol = await _fx.Svc.FetchMilitarySymbolAsync(_fx.InsertedMilitarySymbolIds.First());

        /* 수정 */
        militarySymbol!.Title = "업데이트된_군사부대";
        militarySymbol.OperationState = EnumOperationState.FAULT;
        militarySymbol.Latitude = 35.654321;
        militarySymbol.Longitude = 129.123456;
        militarySymbol.Bearing = 270.0;
        militarySymbol.Width = 60;
        militarySymbol.Height = 60;
        militarySymbol.ShowShape = false;
        militarySymbol.ShowTitle = true;
        militarySymbol.FillColor = EnumColorType.Red;
        militarySymbol.StrokeColor = EnumColorType.Black;
        militarySymbol.StrokeThickness = 3.0;

        // MilitarySymbol 전용 속성 수정
        militarySymbol.Affiliation = EnumMilitaryAffiliation.Hostile;
        militarySymbol.BattleDimension = EnumMilitaryBattleDimension.Air;
        militarySymbol.StandardIdentity = EnumMilitaryStandardIdentity.Planned;
        militarySymbol.UnitType = EnumMilitaryUnitType.Artillery;
        militarySymbol.UnitSize = EnumMilitaryUnitSize.Battalion;
        militarySymbol.UnitDesignator = "Updated-01";
        militarySymbol.HigherFormation = "Updated Brigade";
        militarySymbol.CallSign = "Update-1";
        militarySymbol.CountryCode = "USA";

        var updated = await _fx.Svc.UpdateMilitarySymbolAsync(militarySymbol);

        Assert.NotNull(updated);
        Assert.Equal(militarySymbol.Id, updated!.Id);
        Assert.Equal("업데이트된_군사부대", updated.Title);
        Assert.Equal(EnumOperationState.FAULT, updated.OperationState);
        Assert.Equal(35.654321, updated.Latitude);
        Assert.Equal(129.123456, updated.Longitude);
        Assert.Equal(270.0, updated.Bearing);
        Assert.Equal(60, updated.Width);
        Assert.Equal(60, updated.Height);
        Assert.False(updated.ShowShape);
        Assert.True(updated.ShowTitle);

        // MilitarySymbol 전용 속성 검증
        Assert.Equal(EnumMilitaryAffiliation.Hostile, updated.Affiliation);
        Assert.Equal(EnumMilitaryBattleDimension.Air, updated.BattleDimension);
        Assert.Equal(EnumMilitaryStandardIdentity.Planned, updated.StandardIdentity);
        Assert.Equal(EnumMilitaryUnitType.Artillery, updated.UnitType);
        Assert.Equal(EnumMilitaryUnitSize.Battalion, updated.UnitSize);
        Assert.Equal("Updated-01", updated.UnitDesignator);
        Assert.Equal("Updated Brigade", updated.HigherFormation);
        Assert.Equal("Update-1", updated.CallSign);
        Assert.Equal("USA", updated.CountryCode);
    }

    /// <summary>
    /// MilitarySymbol 삭제 테스트 (CASCADE 확인)
    /// </summary>
    [Fact(DisplayName = "MilitarySymbols – Delete (CASCADE)")]
    public async Task Delete_MilitarySymbol_Works()
    {
        await _fx.SeedMilitarySymbolsAsync();
        var militarySymbol = await _fx.Svc.FetchMilitarySymbolAsync(_fx.InsertedMilitarySymbolIds.First());

        /* 삭제 */
        bool ok = await _fx.Svc.DeleteMilitarySymbolAsync(militarySymbol!);
        Assert.True(ok);

        /* 실제로 사라졌는지 확인 (JOIN 쿼리로 확인) */
        var fetched = await _fx.Svc.FetchMilitarySymbolAsync(militarySymbol!.Id);
        Assert.Null(fetched);

        /* 기본 Symbol도 같이 삭제되었는지 확인 */
        var baseSymbol = await _fx.Svc.FetchSymbolAsync(militarySymbol.Id);
        Assert.Null(baseSymbol);
    }

    

    /// <summary>
    /// 잘못된 매개변수 예외 처리 테스트
    /// </summary>
    [Fact(DisplayName = "MilitarySymbols – Invalid Parameters")]
    public async Task Invalid_Parameters_Throw_Exceptions()
    {
        /* 잘못된 ID로 조회 */
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _fx.Svc.FetchMilitarySymbolAsync(-1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _fx.Svc.FetchMilitarySymbolAsync(0));

        /* null 모델로 삽입 시도 */
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _fx.Svc.InsertMilitarySymbolAsync(null!));

        /* null 모델로 업데이트 시도 */
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _fx.Svc.UpdateMilitarySymbolAsync(null!));

        /* null 모델로 삭제 시도 */
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _fx.Svc.DeleteMilitarySymbolAsync(null!));
    }
}