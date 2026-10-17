using Dapper;
using Ironwall.Dotnet.Libraries.GMaps.Db.Models;
using MySql.Data.MySqlClient;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Db.Tests;
/****************************************************************************
   Purpose      : 통합테스트 전용 격리 DB 헬퍼
   Created By   : GHLee
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 통합테스트 전용 격리 DB.
/// 테스트 픽스처는 InitializeAsync/DisposeAsync에서 <c>DROP TABLE</c> 을 수행하므로,
/// 운영 DB(monitor_db)를 가리키면 실행 즉시 운영 지도/심볼/이벤트가 전멸한다(2026-07-03 실제 사고).
/// → 반드시 별도 스키마(<see cref="Name"/>)를 사용하고, 없으면 자동 생성한다.
/// </summary>
internal static class GMapTestDb
{
    /// <summary>격리 테스트 DB명 — 운영 monitor_db 와 분리. 절대 운영 DB명으로 바꾸지 말 것.</summary>
    public const string Name = "monitor_test_db";

    /// <summary>
    /// 테스트 DB가 없으면 생성한다. 각 픽스처 <c>InitializeAsync</c> 시작 시 1회 호출.
    /// Database 를 지정하지 않고 서버에 접속해 <c>CREATE DATABASE IF NOT EXISTS</c> 를 실행하므로,
    /// 이후 <c>DbDatabase = Name</c> 으로의 연결(DropTables/StartService)이 항상 성공한다.
    /// </summary>
    public static async Task EnsureAsync(GMapDbSetupModel setup)
    {
        var csb = new MySqlConnectionStringBuilder
        {
            Server = setup.IpDbServer,
            Port = (uint)setup.PortDbServer,
            UserID = setup.UidDbServer,
            Password = setup.PasswordDbServer,
            SslMode = MySqlSslMode.Disabled
            // Database 미지정 — 서버에 접속해 DB 자체를 생성
        };

        await using var conn = new MySqlConnection(csb.ToString());
        await conn.OpenAsync();
        await conn.ExecuteAsync($"CREATE DATABASE IF NOT EXISTS `{Name}`;");
    }
}
