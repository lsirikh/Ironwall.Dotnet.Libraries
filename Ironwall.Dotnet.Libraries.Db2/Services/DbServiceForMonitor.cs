using Caliburn.Micro;
using Dapper;
using Ironwall.Dotnet.Framework.Helpers;
using Ironwall.Dotnet.Libraries.Accounts.Providers;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Db2.Models;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using log4net;
using Microsoft.SqlServer.Server;
using MySql.Data.MySqlClient;
using MySqlX.XDevAPI.Common;
using System;
using System.Data;
using System.Security.Principal;
using System.Text;

namespace Ironwall.Dotnet.Libraries.Db2.Services;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/7/2025 8:27:53 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
internal class DbServiceForMonitor : TaskService, IDbServiceForMonitor
{

    #region - Ctors -
    public DbServiceForMonitor(ILogService log
                            , IEventAggregator eventAggregator
                            , AccountProvider accountProvider
                            , LoginProvider loginProvider
                            , DbSetupModel setupModel)
    {
        _log = log;
        _eventAggregator = eventAggregator;
        _setup = setupModel;
        _accountProvider = accountProvider;
        _loginProvider = loginProvider;
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    protected override async Task RunTask(CancellationToken token = default)
    {
        await StartService(token);
    }
    protected override async Task ExitTask(CancellationToken token = default)
    {
        await StopService(token);
    }

    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    public async Task StartService(CancellationToken token = default)
    {
        try
        {
            await Connect(token);
            await BuildSchemeAsync(token);
            await FetchInstanceAsync(token);

        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task StopService(CancellationToken token = default)
    {
        try
        {
            await Disconnect(token);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task Connect(CancellationToken token = default)
    {
        try
        {
            var tempConnString =
                $"Server={_setup.IpDbServer};" +
                $"Port={_setup.PortDbServer};" +
                $"User={_setup.UidDbServer};" +
                $"Password={_setup.PasswordDbServer};" +
                $"SslMode=None;";

            using (var tempConn = new MySqlConnection(tempConnString))
            {
                await tempConn.OpenAsync(token);

                // DB가 있는지 확인: SHOW DATABASES LIKE '{_setup.DbDatabase}'
                var checkSql = $"SHOW DATABASES LIKE '{_setup.DbDatabase}'";
                var result = await tempConn.ExecuteScalarAsync<string>(checkSql);

                if (string.IsNullOrEmpty(result))
                {
                    // DB가 없다면 CREATE
                    var createSql = $"CREATE DATABASE `{_setup.DbDatabase}`;";
                    await tempConn.ExecuteAsync(createSql);
                    _log?.Info($"DB({_setup.DbDatabase})가 존재하지 않아 새로 생성했습니다.");
                }

                if (_eventAggregator != null)
                    await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = "DbServiceForMonitor", Message = "DB테이블을 호출했습니다." });
            }

            var connectionString =
                $"Server={_setup.IpDbServer};" +
                $"Port={_setup.PortDbServer};" +
                $"Database={_setup.DbDatabase};" +
                $"User={_setup.UidDbServer};" +
                $"Password={_setup.PasswordDbServer};" +
                $"SslMode=None;";

            // MySqlConnection 인스턴스 생성
            _conn = new MySqlConnection(connectionString);

            // DB 오픈
            await _conn.OpenAsync(token);
            var msg = $"DB 연결 성공: {_setup.IpDbServer}:{_setup.PortDbServer}/{_setup.DbDatabase}";
            _log?.Info(msg);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = "DbServiceForGym", Message = msg });
        }
        catch (Exception ex)
        {
            _log?.Error($"Raised {nameof(Exception)} : {ex}");
        }
    }

    public async Task Disconnect(CancellationToken token = default)
    {
        try
        {
            if (_conn != null && _conn.State == ConnectionState.Open)
            {
                await _conn.CloseAsync();
                _conn.Dispose();
                _log?.Info("MySQL/MariaDB 연결이 정상적으로 종료되었습니다.");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"Raised {nameof(Exception)} : {ex}");
        }

    }

    public async Task BuildSchemeAsync(CancellationToken token = default)
    {
        try
        {
            if (_conn == null || _conn.State != ConnectionState.Open)
                throw new Exception("DB 연결이 이루어지지 않았습니다.");

            /////Section 테이블
            //var createSection = @$"CREATE TABLE IF NOT EXISTS `Section` (
            //                                id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
            //                                namearea TEXT,
            //                                typedevice INTEGER,
            //                                namedevice TEXT,
            //                                idcontroller INTEGER,    
            //                                idsensor INTEGER,
            //                                typeshape INTEGER,
            //                                x1 REAL,
            //                                y1 REAL,
            //                                x2 REAL,
            //                                y2 REAL,
            //                                width REAL,
            //                                height REAL,
            //                                angle REAL,
            //                                map INTEGER,
            //                                used INTEGER,
            //                                visibility INTEGER,
            //                                time_created DATETIME NOT NULL DEFAULT (DATETIME('NOW', 'LOCALTIME'))
            //                               )";

            /////Area Group 테이블
            //var createAreaGroup = @$"CREATE TABLE IF NOT EXISTS `AreaGroup` (
            //                                id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
            //                                namearea TEXT,
            //                                typedevice INTEGER,
            //                                namedevice TEXT,
            //                                idcontroller INTEGER,    
            //                                idsensor INTEGER,
            //                                typeshape INTEGER,
            //                                x1 REAL,
            //                                y1 REAL,
            //                                x2 REAL,
            //                                y2 REAL,
            //                                width REAL,
            //                                height REAL,
            //                                angle REAL,
            //                                map INTEGER,
            //                                used INTEGER,
            //                                visibility INTEGER,
            //                                time_created DATETIME NOT NULL DEFAULT (DATETIME('NOW', 'LOCALTIME'))
            //                               )";

            /////Siren 테이블
            //var createSirens = @$"CREATE TABLE IF NOT EXISTS `Sirens` (
            //                                id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
            //                                namearea TEXT,
            //                                typedevice INTEGER,
            //                                idcontroller INTEGER,
            //                                idsensorBgn INTEGER,
            //                                idsensorEnd INTEGER,
            //                                idcontrollercontact INTEGER,
            //                                idsensorcontact INTEGER,
            //                                used INTEGER,
            //                                visibility INTEGER,
            //                                time_created DATETIME DEFAULT CURRENT_TIMESTAMP NOT NULL
            //                           )";

            /////사이렌 레이아웃 이미지
            //var createSirenLayout = @$"CREATE TABLE IF NOT EXISTS `SirenLayout` (
            //                                id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
            //                                namearea TEXT,
            //                                typedevice INTEGER,
            //                                namedevice TEXT,
            //                                idcontroller INTEGER,    
            //                                idsensor INTEGER,
            //                                typeshape INTEGER,
            //                                x1 REAL,
            //                                y1 REAL,
            //                                x2 REAL,
            //                                y2 REAL,
            //                                width REAL,
            //                                height REAL,
            //                                angle REAL,
            //                                used INTEGER,
            //                                visibility INTEGER,
            //                                time_created DATETIME NOT NULL DEFAULT (DATETIME('NOW', 'LOCALTIME')),
            //                                map INTEGER
            //                               )";

            /////And조건 테이블
            //var createSensorFusion = @$"CREATE TABLE IF NOT EXISTS `SensorFusion` (
            //                                id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
            //                                namearea TEXT,
            //                                typedevice INTEGER,
            //                                idcontroller INTEGER,
            //                                idsensorBgn INTEGER,
            //                                idsensorEnd INTEGER,
            //                                idcontrollercontact INTEGER,
            //                                idsensorcontact INTEGER,
            //                                used INTEGER,
            //                                visibility INTEGER,
            //                                time_created DATETIME NOT NULL DEFAULT (DATETIME('NOW', 'LOCALTIME')),
            //                                map INTEGER
            //                               )";


            /////Group Label 테이블
            //var createAreaGroupLabel = @$"CREATE TABLE IF NOT EXISTS `AreaGroupLabel` (
            //                                id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
            //                                namearea TEXT,
            //                                typedevice INTEGER,
            //                                namedevice TEXT,
            //                                idcontroller INTEGER,    
            //                                idsensor INTEGER,
            //                                typeshape INTEGER,
            //                                x1 REAL,
            //                                y1 REAL,
            //                                x2 REAL,
            //                                y2 REAL,
            //                                width REAL,
            //                                height REAL,
            //                                angle REAL,
            //                                map INTEGER,
            //                                used INTEGER,
            //                                visibility INTEGER,
            //                                time_created DATETIME NOT NULL DEFAULT (DATETIME('NOW', 'LOCALTIME'))
            //                               )";

            /////Camera Label 테이블
            //var createCameraLabel = @$"CREATE TABLE IF NOT EXISTS `CameraLabel` (
            //                                id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
            //                                namearea TEXT,
            //                                typedevice INTEGER,
            //                                namedevice TEXT,
            //                                idcontroller INTEGER,    
            //                                idsensor INTEGER,
            //                                typeshape INTEGER,
            //                                x1 REAL,
            //                                y1 REAL,
            //                                x2 REAL,
            //                                y2 REAL,
            //                                width REAL,
            //                                height REAL,
            //                                angle REAL,
            //                                map INTEGER,
            //                                used INTEGER,
            //                                visibility INTEGER,
            //                                time_created DATETIME NOT NULL DEFAULT (DATETIME('NOW', 'LOCALTIME'))
            //                               )";

            /////Camera 테이블
            //var createCameras = @$"CREATE TABLE IF NOT EXISTS `Cameras` (
            //                                id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
            //                                namearea TEXT,
            //                                typedevice INTEGER,
            //                                namedevice TEXT,
            //                                idcontroller INTEGER,    
            //                                idsensor INTEGER,
            //                                typeshape INTEGER,
            //                                x1 REAL,
            //                                y1 REAL,
            //                                x2 REAL,
            //                                y2 REAL,
            //                                width REAL,
            //                                height REAL,
            //                                angle REAL,
            //                                map INTEGER,
            //                                used INTEGER,
            //                                visibility INTEGER,
            //                                time_created DATETIME NOT NULL DEFAULT (DATETIME('NOW', 'LOCALTIME'))
            //                               )";

            /////Sensor 테이블
            //var createSensors = @$"CREATE TABLE IF NOT EXISTS `Sensors` (
            //                                id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
            //                                namearea TEXT,
            //                                typedevice INTEGER,
            //                                namedevice TEXT,
            //                                idcontroller INTEGER,    
            //                                idsensor INTEGER,
            //                                typeshape INTEGER,
            //                                x1 REAL,
            //                                y1 REAL,
            //                                x2 REAL,
            //                                y2 REAL,
            //                                width REAL,
            //                                height REAL,
            //                                angle REAL,
            //                                map INTEGER,
            //                                used INTEGER,
            //                                visibility INTEGER,
            //                                time_created DATETIME NOT NULL DEFAULT (DATETIME('NOW', 'LOCALTIME'))
            //                               )";

            /////Controller 테이블
            //var createControllers = @$"CREATE TABLE IF NOT EXISTS `Controllers` (
            //                                id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
            //                                namearea TEXT,
            //                                typedevice INTEGER,
            //                                namedevice TEXT,
            //                                idcontroller INTEGER,    
            //                                idsensor INTEGER,
            //                                typeshape INTEGER,
            //                                x1 REAL,
            //                                y1 REAL,
            //                                x2 REAL,
            //                                y2 REAL,
            //                                width REAL,
            //                                height REAL,
            //                                angle REAL,
            //                                map INTEGER,
            //                                used INTEGER,
            //                                visibility INTEGER,
            //                                time_created DATETIME NOT NULL DEFAULT (DATETIME('NOW', 'LOCALTIME'))
            //                               )";

            ///// 맵 테이블
            //var createMaps = @$"CREATE TABLE IF NOT EXISTS `Maps` (
            //                                id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
            //                                mapname TEXT NOT NULL,
            //                                mapnumber INTEGER,
            //                                filename TEXT,
            //                                filetype TEXT,
            //                                url TEXT,
            //                                width REAL,
            //                                height REAL,
            //                                used INTEGER,
            //                                visibility INTEGER,
            //                                time_created DATETIME NOT NULL DEFAULT (DATETIME('NOW', 'LOCALTIME'))
            //                               )";


            // Accounts 테이블
            var createAccountsTable = @"CREATE TABLE IF NOT EXISTS `Accounts` (
                                            `Id`              INT AUTO_INCREMENT PRIMARY KEY,
                                            `Username`        VARCHAR(100)  NOT NULL UNIQUE,
                                            `Password`        VARCHAR(255)  NOT NULL,
                                            `Name`            VARCHAR(100)  NOT NULL,
                                            `Level`           VARCHAR(20),
                                            `Used`            VARCHAR(20),
                                            `EmployeeNumber`  VARCHAR(50),
                                            `Birth`           DATE,
                                            `Phone`           VARCHAR(20),
                                            `Address`         VARCHAR(255),
                                            `Email`           VARCHAR(100),
                                            `Image`           VARCHAR(255),
                                            `Position`        VARCHAR(100),
                                            `Department`      VARCHAR(100),
                                            `Company`         VARCHAR(100),
                                            `CreatedAt`       DATETIME       DEFAULT CURRENT_TIMESTAMP,
                                            `UpdatedAt`       DATETIME       DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
                                        );
                                    ";

            // Logins 테이블
            var createLoginsTable = @"CREATE TABLE IF NOT EXISTS `Logins` (
                                            `Id`          INT AUTO_INCREMENT PRIMARY KEY,
                                            `Username`    VARCHAR(100) NOT NULL,
                                            `IsIdSaved`   TINYINT(1)   NOT NULL,
                                            `CreatedAt`   DATETIME     DEFAULT CURRENT_TIMESTAMP,
                                            `UpdatedAt`   DATETIME     DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
                                        );
                                    ";


            await _conn.ExecuteAsync(createAccountsTable);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = this.GetType().Name.ToString(), Message = "Accounts DB테이블을 생성합니다..." });
            await _conn.ExecuteAsync(createLoginsTable);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = this.GetType().Name.ToString(), Message = "Logins DB테이블을 생성합니다..." });

            _log?.Info("테이블 생성/확인 완료.");
        }
        catch (Exception ex)
        {
            _log?.Error($"Raised {nameof(Exception)} : {ex}");
        }
    }

    public async Task FetchInstanceAsync(CancellationToken token = default)
    {
        try
        {
            if (_conn == null || _conn.State != ConnectionState.Open)
                throw new Exception("DB 연결이 이루어지지 않았습니다.");

            // 예시로 UserTable에 몇 명이 있는지 SELECT
            //var countSql = @"SELECT COUNT(*) FROM `UserTable`;";
            //var userCount = await _conn.ExecuteScalarAsync<int>(countSql);
            //_log?.Info($"UserTable 내 총 사용자 수: {userCount} 명");

            var users = await FetchAccountsAsync(token);
            _accountProvider.Clear();
            foreach (var item in users!.OfType<AccountModel>())
            {
                _accountProvider.Add(item);
            }
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = this.GetType().Name.ToString(), Message = "UserProvider의 정보를 모두 불러왔습니다..." });

            var logins = await FetchLoginsAsync(token);
            _loginProvider.Clear();
            foreach (var item in logins!.OfType<LoginModel>())
            {
                _loginProvider.Add(item);
            }
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = this.GetType().Name.ToString(), Message = "LoginProvider의 정보를 모두 불러왔습니다..." });

        }
        catch (Exception ex)
        {
            _log?.Error($"Raised {nameof(Exception)} : {ex}");
        }
    }


    public async Task<List<IAccountModel>?> FetchAccountsAsync(CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");



            var sql = @"SELECT * FROM Accounts ORDER BY Id;";
            var rows = await _conn.QueryAsync<AccountModelSQL>(sql);

            var result = new List<IAccountModel>();
            foreach (var row in rows)
            {
                var usedParsed = ParseEnum<EnumUsedType>(row.Used);
                var levelParsed = ParseEnum<EnumLevelType>(row.Level);

                // Account 정보
                var account = new AccountModel
                {
                    Id = row.Id,
                    Username = row.Username,      // 또는 UserId 매핑
                    Password = row.Password,

                    // ── 열거형 변환 ─────────────────────────────
                    Used = usedParsed,
                    Level = levelParsed,

                    // ── 기본 프로필 ─────────────────────────────
                    Name = row.Name,
                    EmployeeNumber = row.EmployeeNumber,
                    Birth = row.Birth,        
                    Phone = row.Phone,
                    Address = row.Address,
                    EMail = row.EMail,
                    Image = row.Image,

                    // ── 조직 정보 ──────────────────────────────
                    Position = row.Position,
                    Department = row.Department,
                    Company = row.Company,
                };
                result.Add(account);
            }
            _log?.Info($"FetchAccountsAsync 완료 - Accounts의 갯수({result.Count})");
            return result;
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchAccountsAsync Error: {ex}");
            throw;
        }
    }

    public async Task<IAccountModel?> FetchAccountAsync(int id, CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            var sql = @"SELECT * FROM Accounts WHERE Id=@Id;";
            var row = await _conn.QueryFirstOrDefaultAsync<AccountModelSQL>(sql, new { Id = id });
            if (row == null) return null;

            var usedParsed = ParseEnum<EnumUsedType>(row.Used);
            var levelParsed = ParseEnum<EnumLevelType>(row.Level);
            var account = new AccountModel
            {
                Id = row.Id,
                Username = row.Username,      // 또는 UserId 매핑
                Password = row.Password,

                // ── 열거형 변환 ─────────────────────────────
                Used = usedParsed,
                Level = levelParsed,

                // ── 기본 프로필 ─────────────────────────────
                Name = row.Name,
                EmployeeNumber = row.EmployeeNumber,
                Birth = row.Birth,          // DateTime? (null 허용)
                Phone = row.Phone,
                Address = row.Address,
                EMail = row.EMail,
                Image = row.Image,

                // ── 조직 정보 ──────────────────────────────
                Position = row.Position,
                Department = row.Department,
                Company = row.Company,
            };

            _log?.Info($"FetchAccountAsync 완료 - Id={account.Id}");
            return account;
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchAccountAsync Error: {ex}");
            throw;
        }
    }

    public async Task<IAccountModel?> FetchAccountAsync(string username, string password, CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            // 사용자명만으로 row 조회
            const string sql = @"
                                SELECT Id, Username, Password, Name, Level, Used,
                                        EmployeeNumber, Birth, Phone, Address, Email,
                                        Image, Position, Department, Company
                                    FROM Accounts
                                    WHERE Username = @Username;";
            var row = await _conn.QueryFirstOrDefaultAsync<AccountModelSQL>(sql, new { Username = username});
            if (row == null) throw new Exception("아이디가 존재하지 않습니다.");

            // 해시 검증
            if (!PasswordHelper.VerifyPassword(row.Password, password))
                throw new Exception("비밀번호가 일치하지 않습니다."); 

            var usedParsed = ParseEnum<EnumUsedType>(row.Used);
            var levelParsed = ParseEnum<EnumLevelType>(row.Level);
            var account = new AccountModel
            {
                Id = row.Id,
                Username = row.Username,      // 또는 UserId 매핑
                Password = row.Password,

                // ── 열거형 변환 ─────────────────────────────
                Used = usedParsed,
                Level = levelParsed,

                // ── 기본 프로필 ─────────────────────────────
                Name = row.Name,
                EmployeeNumber = row.EmployeeNumber,
                Birth = row.Birth,          // DateTime? (null 허용)
                Phone = row.Phone,
                Address = row.Address,
                EMail = row.EMail,
                Image = row.Image,

                // ── 조직 정보 ──────────────────────────────
                Position = row.Position,
                Department = row.Department,
                Company = row.Company,
            };

            _log?.Info($"FetchAccountAsync 완료 - Id={account.Id}");
            return account;
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchAccountAsync Error: {ex}");
            throw;
        }
    }


    public async Task<IAccountModel?> InsertAccountAsync(IAccountModel acc, CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            var sql = @"
            INSERT INTO Accounts
                (Username, Password, Name, Level, Used, EmployeeNumber, Birth,
                 Phone, Address, Email, Image, Position, Department, Company)
            VALUES
                (@Username, @PasswordHash, @Name, @Level, @Used, @EmployeeNumber, @Birth,
                 @Phone, @Address, @Email, @Image, @Position, @Department, @Company);
            SELECT LAST_INSERT_ID();";

            // 비밀번호 해시
            string passwordHash = PasswordHelper.HashPassword(acc.Password);

            var newId = await _conn.ExecuteScalarAsync<int>(sql,
            new
            {
                Username = acc.Username,
                passwordHash,                            // 해시 저장
                Name = acc.Name,
                Level = acc.Level.ToString(),
                Used = acc.Used.ToString(),
                EmployeeNumber = acc.EmployeeNumber,
                Birth = acc.Birth,
                Phone = acc.Phone,
                Address = acc.Address,
                Email = acc.EMail,
                Image = acc.Image,
                Position = acc.Position,
                Department = acc.Department,
                Company = acc.Company
            });
            acc.Id = newId;
            _log?.Info($"InsertAccountAsync 완료 - NewId={newId}, Name={acc.Username}");
            var fetchAcc = await FetchAccountAsync(acc.Id);
            return fetchAcc;
        }
        catch (Exception ex)
        {
            _log?.Error($"InsertAccountAsync Error: {ex}");
            throw;
        }
    }

    public async Task<IAccountModel?> UpdateAccountAsync(IAccountModel acc, CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            var sql = @"
            UPDATE Accounts SET
                Name=@Name, Level=@Level, Used=@Used, EmployeeNumber=@EmployeeNumber, Birth=@Birth, Phone=@Phone,
                Address=@Address, Email=@Email, Image=@Image, Position=@Position, Department=@Department, Company=@Company
            WHERE Id=@Id;";
            await _conn.ExecuteAsync(sql, new
            {
                acc.Id,
                acc.Name,
                Level = acc.Level.ToString(),
                Used = acc.Used.ToString(),
                acc.EmployeeNumber,
                acc.Birth,
                acc.Phone,
                acc.Address,
                acc.EMail,
                acc.Image,
                acc.Position,
                acc.Department,
                acc.Company
            });

            _log?.Info($"UpdateAccountAsync 완료 - Id={acc.Id}");

            var fetchAcc = await FetchAccountAsync(acc.Id);

            return fetchAcc;
        }
        catch (Exception ex)
        {
            _log?.Error($"UpdateAccountAsync Error: {ex}");
            throw;
        }
    }

    public async Task<IAccountModel?> UpdateAccountPassAsync(IAccountModel acc, CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            var sql = @"
            UPDATE Accounts SET Password=@PasswordHash
                            WHERE Id=@Id;";

            // 비밀번호 해시
            string passwordHash = PasswordHelper.HashPassword(acc.Password);
            await _conn.ExecuteAsync(sql, new
            {
                Id = acc.Id,
                PasswordHash = passwordHash,

            });

            _log?.Info($"UpdateAccountPassAsync 완료 - Id={acc.Id}");

            var fetchAcc = await FetchAccountAsync(acc.Id);

            return fetchAcc;
        }
        catch (Exception ex)
        {
            _log?.Error($"UpdateAccountPassAsync Error: {ex}");
            throw;
        }
    }

    public async Task<bool> DeleteAccountAsync(IAccountModel acc, CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            //var fetchAcc = await _conn.ExecuteAsync("SELECT * FROM Accounts WHERE Id=@Id;", new { Id = acc.Id });

            var ret = await _conn.ExecuteAsync("DELETE FROM Accounts WHERE Id=@Id;", new { Id = acc.Id });
            if (!(ret > 0)) throw new Exception($"사용자 정보(아이디:{acc.Id}) 삭세를 실패했습니다.");
            return true;
        }
        catch (Exception ex)
        {
            _log?.Error($"DeleteAccountAsync Error: {ex}");
            throw;
        }
    }

    public async Task<bool> IsUsernameTakenAsync(string username, CancellationToken token = default)
    {
        if (_conn?.State != ConnectionState.Open)
            throw new Exception("DB not connected.");

        const string sql = @"
        SELECT 1
        FROM   Accounts
        WHERE  Username = @Username
        LIMIT  1;";                     // MySQL·SQLite 공통 LIMIT 문법

        var found = await _conn.ExecuteScalarAsync<int?>(
                        new CommandDefinition(sql, new { Username = username }, cancellationToken: token));

        return found.HasValue;              // null → 미존재, 1 → 존재
    }

    // ─────────────────────── SELECT ALL ────────────────────────────
    public async Task<List<ILoginModel>?> FetchLoginsAsync(CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            var sql = @"SELECT * FROM Logins ORDER BY Id;";
            var rows = await _conn.QueryAsync<LoginModel>(sql);
            var list = rows.OfType<ILoginModel>().ToList();
            _log?.Info($"FetchLoginsAsync 완료 - LoginModels의 갯수({list.Count})");
            return list;
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchLoginsAsync Error: {ex}");
            throw;
        }
    }

    // ─────────────────────── SELECT ONE ────────────────────────────
    public async Task<ILoginModel?> FetchLoginByIdAsync(int id, CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            var sql = @"SELECT * FROM Logins WHERE Id=@Id;";
            var row = await _conn.QueryFirstOrDefaultAsync<LoginModel>(sql, new { Id = id });
            if (row == null) return null;
            _log?.Info($"FetchLoginByIdAsync 완료 - Id={row.Id}");
            return row;
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchLoginByIdAsync Error: {ex}");
            throw;
        }
    }

    // ─────────────────────── SELECT LATEST ONE ─────────────────────────
    public async Task<ILoginModel?> FetchLoginsLatestAsync(CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            const string sql = @"
            SELECT  Id,
                    Username,
                    IsIdSaved
            FROM    Logins
            ORDER BY CreatedAt DESC           -- 가장 최근 순
            LIMIT 1;                          -- 한 건만";

            var loginModel = await _conn.QueryFirstOrDefaultAsync<LoginModel>(sql);

            _log?.Info("FetchLoginsLatestAsync 완료 - LoginModel");
            return loginModel;
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchLoginsLatestAsync Error: {ex}");
            throw;
        }
    }


    // ─────────────────────── INSERT ────────────────────────────────
    public async Task<int> InsertLoginAsync(ILoginModel login, CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            const string sql = @"
            INSERT INTO Logins (Username, IsIdSaved)
            VALUES (@Username, @IsIdSaved);
            SELECT LAST_INSERT_ID();";

            var newId = await _conn.ExecuteScalarAsync<int>(sql, login);
            login.Id = newId;
            _log?.Info($"InsertLoginAsync 완료 - NewId={newId}, Username={login.Username}");
            return newId;
        }
        catch (Exception ex)
        {
            _log?.Error($"InsertLoginAsync Error: {ex}");
            throw;
        }
    }

    // ─────────────────────── UPDATE ────────────────────────────────
    public async Task UpdateLoginAsync(ILoginModel login, CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            const string sql = @"
            UPDATE Logins
            SET    Username    = @Username,
                   IsIdSaved = @IsIdSaved
            WHERE  Id = @Id;";

            await _conn.ExecuteAsync(sql, login);
            _log?.Info($"UpdateLoginAsync 완료 - Id={login.Id}");
        }
        catch (Exception ex)
        {
            _log?.Error($"UpdateLoginAsync Error: {ex}");
            throw;
        }
    }

    // ─────────────────────── DELETE ────────────────────────────────
    public async Task DeleteLoginAsync(int id, CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            await _conn.ExecuteAsync("DELETE FROM Logins WHERE Id=@Id;", new { Id = id });
            _log?.Info($"DeleteLoginAsync 완료 - Id={id}");
        }
        catch (Exception ex)
        {
            _log?.Error($"DeleteLoginAsync Error: {ex}");
            throw;
        }
    }

    private TEnum ParseEnum<TEnum>(string? rawValue) where TEnum : struct
    {
        if (string.IsNullOrEmpty(rawValue))
            return default; // Enum의 기본값(0) 혹은 예외 발생시킬 수도 있음

        // 대소문자 무시하려면 ignoreCase=true
        if (Enum.TryParse<TEnum>(rawValue, ignoreCase: false, out var parsed))
        {
            return parsed;
        }

        // 파싱 실패 시
        _log?.Warning($"[ParseEnum] '{rawValue}' is not valid for {typeof(TEnum).Name}. Using default.");
        return default;
    }
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public bool IsConnected =>
            _conn != null && _conn.State == ConnectionState.Open;
    #endregion
    #region - Attributes -
    private ILogService? _log;
    private IEventAggregator? _eventAggregator;
    private DbSetupModel _setup;
    private LoginProvider _loginProvider;
    private AccountProvider _accountProvider;
    private MySqlConnection? _conn;
    #endregion

}

/// <summary>
/// DB ↔ Domain 매핑용 POCO (Level·Used를 문자열로 보관)
/// </summary>
internal sealed class AccountModelSQL
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;   // e.g. "Admin", "User"
    public string Used { get; set; } = "true";            // "True", "False"
    public string Password { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public DateTime? Birth { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? EMail { get; set; }
    public string? Image { get; set; }
    public string? Position { get; set; }
    public string? Department { get; set; }
    public string? Company { get; set; }
}