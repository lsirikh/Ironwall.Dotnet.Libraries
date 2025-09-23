using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Db.Models;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using System;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Db.Tests;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/23/2025 8:12:17 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/


public abstract class GMapBaseSymbolFixture : IAsyncLifetime
{
    /// <summary>Symbol DB 서비스 인스턴스</summary>
    public IGMapDbSymbolService Svc { get; private set; } = null!;

    /// <summary>Symbol 데이터 제공자</summary>
    public SymbolProvider SymbolProvider = new();
    public GeometricSymbolProvider GeometrySymbolProvider { get; private set; } = null!;
    public PidsSymbolProvider PidsSymbolProvider { get; private set; } = null!;
    public MilitarySymbolProvider MilitarySymbolProvider { get; private set; } = null!;
    public LineSymbolProvider LineSymbolProvider { get; private set; } = null!;
    public InfraSymbolProvider InfraSymbolProvider { get; private set; } = null!;
    public PidsGroupSymbolProvider PidsGroupSymbolProvider { get; private set; } = null!;


    /// <summary>취소 토큰 소스</summary>
    public CancellationTokenSource Cts { get; } = new();

    /// <summary>테스트용 Symbol 생성 개수</summary>
    public int SymbolCount = 20;

    /// <summary>삽입된 Symbol ID 목록</summary>
    public List<int> InsertedSymbolIds = new List<int>();

    #region - Constants -
    /// <summary>테스트용 Symbol 테이블 목록</summary>
    protected static readonly string[] _tables = { "LinePoints", "LineSymbols", "PidsSymbols", "PidsGroupSymbols", "GeometrySymbols", "MilitarySymbols", "InfraSymbols", "Symbols"  };
    /// <summary>DB 설정</summary>
    protected readonly GMapDbSetupModel _setup = new()
    {
        IpDbServer = "127.0.0.1",
        PortDbServer = 3306,
        DbDatabase = "monitor_db",
        UidDbServer = "root",
        PasswordDbServer = "root"
    };


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
        LineSymbolProvider = new LineSymbolProvider(log, SymbolProvider);
        InfraSymbolProvider = new InfraSymbolProvider(log, SymbolProvider);
        PidsGroupSymbolProvider = new PidsGroupSymbolProvider(log, SymbolProvider);
        Svc = new GMapDbSymbolService(log, ea, SymbolProvider, GeometrySymbolProvider, PidsSymbolProvider, MilitarySymbolProvider, LineSymbolProvider, InfraSymbolProvider, PidsGroupSymbolProvider, _setup);
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

    protected abstract Task DropTablesAsync();

}
