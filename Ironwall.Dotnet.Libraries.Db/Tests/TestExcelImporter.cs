using Caliburn.Micro;
using DocumentFormat.OpenXml.Spreadsheet;
using Dotnet.Gym.Message.Providers;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Db.Models;
using Ironwall.Dotnet.Libraries.Db.Services;
using Ironwall.Dotnet.Libraries.Db.Utils;
using System;
using System.IO;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Db.Tests;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 3/21/2025 12:12:59 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class TestExcelImporter
{
    private const string TEST_DB_SERVER = "192.168.202.180";  // 실제 환경에 맞게 변경
    private const int TEST_DB_PORT = 3306;             // MariaDB 기본 포트
    private const string TEST_DB_NAME = "GymDB";   // 테스트용 DB
    private const string TEST_DB_USER = "root";        // 예시
    private const string TEST_DB_PASS = "root";            // 예시
    private DbSetupModel _setupModel;
    private UserProvider _users;
    private EmsMessageProvider _emsMessages;

    // DbServiceForGym 인스턴스
    private DbServiceForGym _service;
    private ExcelImporter _excelImporter;

    public TestExcelImporter()
    {
        var logMock = new LogService();  // 아래에 구현한 콘솔 출력 스텁
        var eventMock = new EventAggregator();
        _setupModel = new DbSetupModel
        {
            IpDbServer = TEST_DB_SERVER,
            PortDbServer = TEST_DB_PORT,
            DbDatabase = TEST_DB_NAME,
            UidDbServer = TEST_DB_USER,
            PasswordDbServer = TEST_DB_PASS
        };
        _users = new UserProvider();
        _emsMessages = new EmsMessageProvider();
        // DbServiceForGym 생성
        _service = new DbServiceForGym(logMock, eventMock, _setupModel, _users, _emsMessages);
        _excelImporter = new ExcelImporter(logMock, eventMock, _setupModel, _service);
    }

    [Fact]
    public async Task Test01_ImportExcelToDbAsync()
    {
        await _service.StartService();

        // Excel 파일이 위치한 디렉토리
        var currentDir = AppDomain.CurrentDomain.BaseDirectory;
        var excelDir = Path.Combine(currentDir, "./Excels");

        // 폴더 내 첫 번째 Excel 파일 찾기
        var excelFiles = Directory.GetFiles(excelDir, "*.xlsx");
        if (excelFiles.Length == 0)
        {
            return;
        }

        var memberExcelFile = excelFiles.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Contains("회원", StringComparison.OrdinalIgnoreCase));
        if (memberExcelFile == null) return;

        // 파일이 열려 있는 경우 대비하여 임시 폴더에 복사
        var tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".xlsx");
        File.Copy(memberExcelFile, tempFilePath, overwrite: true);

        var ret = await _excelImporter.ImportExcelToDbAsync(tempFilePath);

        // 파일 사용 후 삭제
        File.Delete(tempFilePath);


    }

}