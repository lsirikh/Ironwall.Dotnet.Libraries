using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Providers;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Db2.Models;
using Ironwall.Dotnet.Libraries.Db2.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using System;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Db2.Tests;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/14/2025 5:08:49 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class UnitTest
{
    // ── 공용 객체 ─────────────────────────────
    private readonly DbServiceForMonitor _svc;
    private readonly CancellationTokenSource _cts = new();
    private readonly AccountProvider _accProvider = new();
    private readonly LoginProvider _logProvider = new();

    // 테스트 전용 DB 설정 (localhost 기준)
    private readonly DbSetupModel _setup = new()
    {
        IpDbServer = "127.0.0.1",
        PortDbServer = 3306,
        DbDatabase = "monitordb",
        UidDbServer = "root",
        PasswordDbServer = "root"
    };

    public UnitTest()
    {
        // 필요한 최소 서비스만 수동 생성
        var logSvc = new LogService();
        var ea = new EventAggregator();

        _svc = new DbServiceForMonitor(
                    logSvc,
                    ea,
                    _accProvider,
                    _logProvider,
                    _setup);
    }

    // ── IAsyncLifetime 구현 ──────────────────
    [Fact(DisplayName = "Initialize DB Service")]
    public async Task InitializeAsync()
    {
        // Connect + 스키마 + 캐시 초기화
        await _svc.Connect(_cts.Token);
        await _svc.BuildSchemeAsync(_cts.Token);
        await _svc.FetchInstanceAsync(_cts.Token);
        Assert.True(_svc.IsConnected);
    }


    [Fact(DisplayName = "Dispose DB Service")]
    public async Task DisposeAsync()
    {
        await InitializeAsync();

        await _svc.StopService(_cts.Token);
        _cts.Cancel();
        _cts.Dispose();

        Assert.False(_svc.IsConnected);
    }

    // ─────────────────────────────────────────
    [Fact(DisplayName = "Account CRUD End-to-End")]
    public async Task Account_Crud_EndToEnd()
    {
        await InitializeAsync();

        // Insert
        var acc = new AccountModel
        {
            Username = $"sensorway",
            Password = "sensorway1",
            Name = "tester",
            Level = EnumLevelType.USER,
            Birth = DateTime.Parse( new DateOnly(1990, 1, 3).ToString()),
            Phone = "01012345678",
            Used = EnumUsedType.USED,
        };

        var fetchAcc = await _svc.InsertAccountAsync(acc, _cts.Token);
        acc.Id = fetchAcc!.Id;
        Assert.True(fetchAcc!.Id > 0);

        // Fetch by Id
        var fetched = await _svc.FetchAccountAsync(acc.Id, _cts.Token);
        Assert.NotNull(fetched);
        Assert.Equal(acc.Username, fetched!.Username);

        // Fetch by Username and Password
        fetched = await _svc.FetchAccountAsync(acc.Username, acc.Password, _cts.Token);
        Assert.NotNull(fetched);
        Assert.Equal(acc.Username, fetched!.Username);

        // Update
        fetched.Name = "tester-updated";
        fetched.Birth = DateTime.Parse(new DateOnly(1990, 1, 3).ToString());
        await _svc.UpdateAccountAsync(fetched, _cts.Token);

        var updated = await _svc.FetchAccountAsync(acc.Id, _cts.Token);
        Assert.Equal("tester-updated", updated!.Name);

        // Delete
        await _svc.DeleteAccountAsync(acc, _cts.Token);
        var deleted = await _svc.FetchAccountAsync(acc.Id, _cts.Token);
        Assert.Null(deleted);

        await _svc.StopService(_cts.Token);
    }

    // ─────────────────────────────────────────
    [Fact(DisplayName = "Login CRUD + Latest")]
    public async Task Login_Crud_And_Latest()
    {
        await InitializeAsync();

        // Insert
        var log = new LoginModel
        {
            Username = $"login_{Guid.NewGuid():N}",
            IsIdSaved = true
        };

        log.Id = await _svc.InsertLoginAsync(log, _cts.Token);
        Assert.True(log.Id > 0);

        // Fetch latest
        var latest = await _svc.FetchLoginsLatestAsync(_cts.Token);
        Assert.NotNull(latest);
        Assert.Equal(log.Id, latest!.Id);

        // Update
        latest.IsIdSaved = false;
        await _svc.UpdateLoginAsync(latest, _cts.Token);

        var fetched = await _svc.FetchLoginByIdAsync(latest.Id, _cts.Token);
        Assert.False(fetched!.IsIdSaved);

        // Delete
        await _svc.DeleteLoginAsync(latest.Id, _cts.Token);
        var afterDel = await _svc.FetchLoginByIdAsync(latest.Id, _cts.Token);
        Assert.Null(afterDel);

        await _svc.StopService(_cts.Token);
    }

    // ─────────────────────────────────────────
    [Fact(DisplayName = "Fetch Collection")]
    public async Task Fetch_Collections()
    {
        await InitializeAsync();

        var accList = await _svc.FetchAccountsAsync(_cts.Token);
        Assert.NotNull(accList);

        var logList = await _svc.FetchLoginsAsync(_cts.Token);
        Assert.NotNull(logList);

        await _svc.StopService(_cts.Token);
    }
}