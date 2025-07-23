using Caliburn.Micro;
using Dapper;
using Ironwall.Dotnet.Libraries.Accounts.Db.Models;
using Ironwall.Dotnet.Libraries.Accounts.Db.Services;
using Ironwall.Dotnet.Libraries.Accounts.Providers;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using MySql.Data.MySqlClient;
using System;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Db.Tests;
/****************************************************************************
   Purpose      : (xUnit) DB와 한 번만 연결·해제하기 위한 Fixture.
                  모든 테스트 클래스에서 재사용할 수 있다.  
   Created By   : GHLee                                                
   Created On   : 5/14/2025 5:08:49 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public sealed class DbTestFixture : IAsyncLifetime
{
    // ── 공용 객체 ─────────────────────────────
    public IAccountDbService Svc { get; private set; }
    public CancellationTokenSource Cts = new();
    public AccountProvider AccProvider = new();
    public LoginProvider LogProvider = new();

    /* 테스트에서 사용하는 테이블 이름만 나열 */
    private static readonly string[] tables =
    {
        "Accounts",
        "Logins",
    };

    // 테스트 전용 DB 설정 (localhost 기준)
    private readonly AccountDbSetupModel _setup = new()
    {
        IpDbServer = "127.0.0.1",
        PortDbServer = 3306,
        DbDatabase = "monitor_db",
        UidDbServer = "root",
        PasswordDbServer = "root"
    };

    // ── IAsyncLifetime 구현 ──────────────────
    [Fact(DisplayName = "Initialize DB Service")]
    public async Task InitializeAsync()
    {
        // 필요한 최소 서비스만 수동 생성
        var logger = new LogService();
        var ea = new EventAggregator();
        Svc = new AccountDbService(
                    logger,
                    ea,
                    AccProvider,
                    LogProvider,
                    _setup);
        
        await DropTablesAsync();               // 깨끗한 DB 확보

        await Svc.StartService(Cts.Token);       // Connect + BuildScheme + FetchInstance
        Assert.True(Svc.IsConnected);
    }


    [Fact(DisplayName = "Dispose DB Service")]
    public async Task DisposeAsync()
    {
        await Svc.StopService(Cts.Token);
        if(!Cts.IsCancellationRequested)
            Cts.Cancel();

        Assert.False(Svc.IsConnected);
        await DropDatabaseAsync();

    }

    private async Task DropDatabaseAsync()
    {
        var csb = new MySqlConnectionStringBuilder
        {
            Server = _setup.IpDbServer,
            Port = (uint)_setup.PortDbServer,
            UserID = _setup.UidDbServer,
            Password = _setup.PasswordDbServer,
            SslMode = MySqlSslMode.Disabled,
        };

        await using var conn = new MySqlConnection(csb.ToString());
        await conn.OpenAsync();

        await conn.ExecuteAsync($"DROP DATABASE IF EXISTS `{_setup.DbDatabase}`;");
    }
    private async Task DropTablesAsync()
    {
        /* 1단계: DB 없으면 생성해 두기 (부트스트랩 연결) */
        var bootstrap = new MySqlConnectionStringBuilder
        {
            Server = _setup.IpDbServer,
            Port = (uint)_setup.PortDbServer,
            UserID = _setup.UidDbServer,
            Password = _setup.PasswordDbServer,
            SslMode = MySqlSslMode.Disabled
        };
        await using (var conn = new MySqlConnection(bootstrap.ToString()))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync($@"
            CREATE DATABASE IF NOT EXISTS `{_setup.DbDatabase}`
            CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_520_ci;");
        }

        /* 2단계: DB 포함 연결로 테이블 DROP */
        bootstrap.Database = _setup.DbDatabase;
        await using var dbConn = new MySqlConnection(bootstrap.ToString());
        await dbConn.OpenAsync();

        foreach (var t in tables)
            await dbConn.ExecuteAsync($"DROP TABLE IF EXISTS `{t}`;");
    }

}

/// <summary>
/// 모든 테스트 클래스가 동일한 Fixture 인스턴스를 공유한다.
/// </summary>
[CollectionDefinition(nameof(DbCollection))]
public sealed class DbCollection : ICollectionFixture<DbTestFixture> { }

/// <summary>
/// AccountDbService public 메서드 전수(全數) 테스트.
/// </summary>
[Collection(nameof(DbCollection))]
public class AccountDbServiceTests
{
    private readonly DbTestFixture _fx;

    public AccountDbServiceTests(DbTestFixture fixture) => _fx = fixture;

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "01. 연결 상태 확인")]
    public void IsConnected_ShouldBeTrue() => Assert.True(_fx.Svc.IsConnected);

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "02. Username 중복 검사")]
    public async Task IsUsernameTakenAsync_Works()
    {
        string uid = $"user_{Guid.NewGuid():N}";

        // 미존재 ➜ false
        bool taken = await _fx.Svc.IsUsernameTakenAsync(uid, _fx.Cts.Token);
        Assert.False(taken);

        // 계정 생성
        var acc = await _fx.Svc.InsertAccountAsync(new AccountModel
        {
            Username = uid,
            Password = "pwd1234!",
            Name = "tester",
            Level = EnumLevelType.USER,
            Used = EnumUsedType.USED
        }, _fx.Cts.Token);

        // 존재 ➜ true
        taken = await _fx.Svc.IsUsernameTakenAsync(uid, _fx.Cts.Token);
        Assert.True(taken);

        // 정리
        await _fx.Svc.DeleteAccountAsync(acc!, _fx.Cts.Token);
    }

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "03. Account Insert / Fetch / UpdatePass / Delete")]
    public async Task Account_All_Methods()
    {
        var acc = await _fx.Svc.InsertAccountAsync(new AccountModel
        {
            Username = $"acc_{Guid.NewGuid():N}",
            Password = "1q2w3e4r!",
            Name = "Lee",
            Level = EnumLevelType.ADMIN,
            Used = EnumUsedType.USED
        }, _fx.Cts.Token);

        Assert.NotNull(acc);
        int id = acc!.Id;

        // FetchAccountAsync(id)
        var byId = await _fx.Svc.FetchAccountAsync(id, _fx.Cts.Token);
        Assert.Equal(acc.Username, byId!.Username);

        // FetchAccountAsync(username, password) – 원본 패스워드 사용
        var byUidPwd = await _fx.Svc.FetchAccountAsync(acc.Username, "1q2w3e4r!", _fx.Cts.Token);
        Assert.Equal(id, byUidPwd!.Id);

        // UpdateAccountPassAsync
        acc.Password = "newPASS_!23";
        var afterPass = await _fx.Svc.UpdateAccountPassAsync(acc, _fx.Cts.Token);

        // 새 비밀번호로 다시 로그인-검증
        var loginWithNew = await _fx.Svc.FetchAccountAsync(acc.Username, "newPASS_!23", _fx.Cts.Token);
        Assert.Equal(id, loginWithNew!.Id);

        // 최종 삭제
        bool deleted = await _fx.Svc.DeleteAccountAsync(acc, _fx.Cts.Token);
        Assert.True(deleted);

        var shouldNull = await _fx.Svc.FetchAccountAsync(id, _fx.Cts.Token);
        Assert.Null(shouldNull);
    }

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "04. Account Update (일반 필드)")]
    public async Task Account_UpdateAsync_Works()
    {
        var acc = await _fx.Svc.InsertAccountAsync(new AccountModel
        {
            Username = $"acc_{Guid.NewGuid():N}",
            Password = "pw1!",
            Name = "Origin",
            Level = EnumLevelType.USER,
            Used = EnumUsedType.USED
        }, _fx.Cts.Token);

        acc!.Name = "Modified";
        acc.Phone = "010-9999-0000";
        acc.Address = "Seoul";

        var after = await _fx.Svc.UpdateAccountAsync(acc, _fx.Cts.Token);
        Assert.Equal("Modified", after!.Name);
        Assert.Equal("010-9999-0000", after.Phone);

        await _fx.Svc.DeleteAccountAsync(acc, _fx.Cts.Token);
    }

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "05. FetchAccountsAsync – Provider 캐시 갱신 여부")]
    public async Task FetchAccountsAsync_Fills_Provider()
    {
        // Insert 두 명
        await _fx.Svc.InsertAccountAsync(new AccountModel
        {
            Username = $"a_{Guid.NewGuid():N}",
            Password = "pw",
            Name = "A",
            Level = EnumLevelType.USER,
            Used = EnumUsedType.USED
        });

        await _fx.Svc.InsertAccountAsync(new AccountModel
        {
            Username = $"b_{Guid.NewGuid():N}",
            Password = "pw",
            Name = "B",
            Level = EnumLevelType.USER,
            Used = EnumUsedType.USED
        });

        await _fx.Svc.FetchInstanceAsync(); // 내부 Provider 리로드

        Assert.Equal(
            await _fx.Svc.FetchAccountsAsync()!.ContinueWith(t => t.Result!.Count),
            _fx.AccProvider.Count);
    }

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "06. Login Insert / Update / Latest / Delete")]
    public async Task Login_All_Methods()
    {
        // Insert
        var login = new LoginModel
        {
            Username = $"login_{Guid.NewGuid():N}",
            IsIdSaved = false
        };
        login.Id = await _fx.Svc.InsertLoginAsync(login, _fx.Cts.Token);

        // FetchLatest – 방금 넣은 것이 최신
        var latest = await _fx.Svc.FetchLoginsLatestAsync(_fx.Cts.Token);
        Assert.Equal(login.Id, latest!.Id);

        // Update
        latest.IsIdSaved = true;
        await _fx.Svc.UpdateLoginAsync(latest, _fx.Cts.Token);

        var afterUpd = await _fx.Svc.FetchLoginByIdAsync(latest.Id, _fx.Cts.Token);
        Assert.True(afterUpd!.IsIdSaved);

        // Delete
        await _fx.Svc.DeleteLoginAsync(latest.Id, _fx.Cts.Token);
        var shouldNull = await _fx.Svc.FetchLoginByIdAsync(latest.Id, _fx.Cts.Token);
        Assert.Null(shouldNull);
    }

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "07. FetchLoginsAsync – Provider 캐시 갱신 여부")]
    public async Task FetchLoginsAsync_Fills_Provider()
    {
        // Insert
        var login = new LoginModel
        {
            Username = $"l_{Guid.NewGuid():N}",
            IsIdSaved = true
        };
        login.Id = await _fx.Svc.InsertLoginAsync(login);

        await _fx.Svc.FetchInstanceAsync(); // Provider 리로드

        Assert.Equal(
            await _fx.Svc.FetchLoginsAsync()!.ContinueWith(t => t.Result!.Count),
            _fx.LogProvider.Count);
    }
}