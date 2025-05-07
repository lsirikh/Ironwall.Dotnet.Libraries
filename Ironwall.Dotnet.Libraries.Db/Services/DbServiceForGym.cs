using Autofac.Core;
using Caliburn.Micro;
using Dapper;
using Dotnet.Gym.Message.Accounts;
using Dotnet.Gym.Message.Contacts;
using Dotnet.Gym.Message.Enums;
using Dotnet.Gym.Message.Providers;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Db.Models;
using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Data.Common;
using System.Drawing;
using System.IO;
using System.Windows.Interop;

namespace Ironwall.Dotnet.Libraries.Db.Services;
/****************************************************************************
   Purpose      : MariaDB based DbService                                                         
   Created By   : GHLee                                                
   Created On   : 1/31/2025 12:46:37 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
internal class DbServiceForGym : TaskService, IDbServiceForGym
{
    #region - Ctors -
    public DbServiceForGym(ILogService log
                            , IEventAggregator eventAggregator
                            , DbSetupModel setupModel
                            , UserProvider users
                            , EmsMessageProvider emsMessages)
    {
        _log = log;
        _eventAggregator = eventAggregator;
        _setup = setupModel;
        _userProvider = users;
        _emsProvider = emsMessages;
    }
    #endregion
    #region - Implementation of Interface -
    public async void Dispose()
    {
        await Disconnect();
    }
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
                    await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = "DbServiceForGym", Message = "DB테이블을 호출했습니다." });
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

            // UserModel 테이블
            var createUserTable = @"
                CREATE TABLE IF NOT EXISTS `UserTable` (
                    `Id` INT AUTO_INCREMENT PRIMARY KEY,
                    `UserName` VARCHAR(100) NOT NULL,
                    `MobilePhone` VARCHAR(20),
                    `Age` INT,
                    `Gender` VARCHAR(10),
                    `RegisterDate` DATETIME,
                    `IsActive` VARCHAR(10),
                    `CreatedAt` DATETIME DEFAULT CURRENT_TIMESTAMP,
                    `UpdatedAt` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
                );";

            // ActivePeriod 테이블 (UserTable.Id를 참조)
            var createActiveUserTable = @"
                CREATE TABLE IF NOT EXISTS `ActivePeriod` (
                    `Id` INT NOT NULL PRIMARY KEY,
                    `StartDate` DATETIME,
                    `EndDate` DATETIME,
                    CONSTRAINT FK_ActiveUser_User
                      FOREIGN KEY (`Id`) REFERENCES `UserTable`(`Id`)
                      ON DELETE CASCADE
                      ON UPDATE CASCADE
                );";


            // Locker 테이블 (UserTable.Id를 참조, 1:1 관계)
            var createLockerTable = @"
                CREATE TABLE IF NOT EXISTS `LockerTable` (
                    `Id` INT NOT NULL PRIMARY KEY,
                    `Locker` VARCHAR(20),   -- 락커 번호
                    `ShoeLocker` VARCHAR(20), -- 신발장 번호

                    CONSTRAINT FK_Locker_User
                      FOREIGN KEY (`Id`) REFERENCES `UserTable`(`Id`)
                      ON DELETE CASCADE
                      ON UPDATE CASCADE
                );";

            // EmsMessage 테이블
            var createEmsMessageTable = @"
                CREATE TABLE IF NOT EXISTS `EmsMessage` (
                    `Id` INT AUTO_INCREMENT PRIMARY KEY,
                    `UserId` INT NOT NULL,
                    `NoticeType` VARCHAR(20) NOT NULL,
                    `MsgType` VARCHAR(20) NOT NULL,
                    `Sender` VARCHAR(100),
                    `Receiver` VARCHAR(100),
                    `Message` TEXT,
                    `Title` VARCHAR(200),
                    `Destination` VARCHAR(100),
                    `Reservation` DATETIME,
                    `SendTime` DATETIME,

                    CONSTRAINT FK_EmsMessage_User
                      FOREIGN KEY (`UserId`) REFERENCES `UserTable`(`Id`)
                      ON DELETE CASCADE
                      ON UPDATE CASCADE
                );";


            // Image 테이블
            var createImageTable = @"
                CREATE TABLE IF NOT EXISTS `Image` (
                    `Id` INT AUTO_INCREMENT PRIMARY KEY,
                    `EmsMessageId` INT NOT NULL,
                    `Base64Image` LONGTEXT,
                    `FileName` VARCHAR(255),
                    `ContentType` VARCHAR(100),

                    CONSTRAINT FK_Image_EmsMessage
                      FOREIGN KEY (`EmsMessageId`) REFERENCES `EmsMessage`(`Id`)
                      ON DELETE CASCADE
                      ON UPDATE CASCADE
                );";

            

            await _conn.ExecuteAsync(createUserTable);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = "DbServiceForGym", Message = "UserTable DB테이블을 생성합니다..." });
            await _conn.ExecuteAsync(createActiveUserTable);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = "DbServiceForGym", Message = "ActivePeriod DB테이블을 생성합니다..." });
            await _conn.ExecuteAsync(createLockerTable);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = "DbServiceForGym", Message = "LockerTable DB테이블을 생성합니다..." });
            await _conn.ExecuteAsync(createEmsMessageTable);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = "DbServiceForGym", Message = "EmsMessage DB테이블을 생성합니다..." });
            await _conn.ExecuteAsync(createImageTable);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = "DbServiceForGym", Message = "Image DB테이블을 생성합니다..." });

            _log?.Info("테이블(UserTable, ActivePeriod) 생성/확인 완료.");
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

            var users = await FetchUsersAsync(token);
            _userProvider.Clear();
            foreach (var item in users)
            {
                _userProvider.Add(item);
            }
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = "DbServiceForGym", Message = "UserProvider의 정보를 모두 불러왔습니다..." });

            var ems = await FetchEmsMessagesAsync(token);
            _emsProvider.Clear();
            foreach (var item in ems)
            {
                _emsProvider.Add(item);
            }
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = "DbServiceForGym", Message = "EmsProvider의 정보를 모두 불러왔습니다..." });

        }
        catch (Exception ex)
        {
            _log?.Error($"Raised {nameof(Exception)} : {ex}");
        }
    }

    


    #region - SELECT (Fetch) -


    /// <summary>
    /// 전체 User + (1:1) ActivePeriod 데이터 조회
    /// </summary>
    public async Task<List<UserModel>?> FetchUsersAsync(CancellationToken token = default)
    {
        try
        {
            if (_conn == null || _conn.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            //var sql = @"
            //    SELECT 
            //        U.Id AS U_Id,
            //        U.UserName,
            //        U.MobilePhone,
            //        U.Age,
            //        U.Gender,
            //        U.RegisterDate,
            //        U.IsActive,
            //        U.CreatedAt,
            //        U.UpdatedAt,
            //        A.Id AS A_Id, 
            //        A.StartDate, 
            //        A.EndDate
            //    FROM `UserTable` U
            //    LEFT JOIN `ActivePeriod` A
            //        ON U.Id = A.Id
            //    ORDER BY U.Id;
            //    ";

            var sql = @"
                SELECT 
                    U.Id AS U_Id,
                    U.UserName,
                    U.MobilePhone,
                    U.Age,
                    U.Gender,
                    U.RegisterDate,
                    U.IsActive,
                    U.CreatedAt,
                    U.UpdatedAt,
                    A.Id AS A_Id,        
                    A.StartDate,
                    A.EndDate,
                    L.Id AS L_Id,      
                    L.Locker,
                    L.ShoeLocker
                FROM `UserTable` U
                LEFT JOIN `ActivePeriod` A ON U.Id = A.Id
                LEFT JOIN `LockerTable` L ON U.Id = L.Id
                ORDER BY U.Id;
            ";

            var rows = await _conn.QueryAsync<UserActiveLockerJoin>(sql);

            var result = new List<UserModel>();
            foreach (var row in rows)
            {
                // Enum 파싱
                // row.Gender → EnumGenderType 변환
                var genderParsed = ParseEnum<EnumGenderType>(row.Gender);
                var truefalseParsed = ParseEnum<EnumTrueFalse>(row.IsActive);

                // User 정보
                var user = new UserModel
                {
                    Id = row.U_Id,
                    UserName = row.UserName,
                    MobilePhone = row.MobilePhone,
                    Age = row.Age,
                    Gender = genderParsed,
                    RegisterDate = row.RegisterDate ?? DateTime.MinValue,
                    IsActive = truefalseParsed,
                    CreatedAt = row.CreatedAt ?? DateTime.MinValue,
                    UpdatedAt = row.UpdatedAt ?? DateTime.MinValue,
                    ActivePeriod = row.StartDate.HasValue ? new ActivePeriod
                    {
                        Id = row.A_Id.Value, // User Id와 동일
                        StartDate = row.StartDate.Value,
                        EndDate = row.EndDate.Value
                    } : null,
                    Locker = row.Locker != null ? new LockerModel
                    {
                        Id = row.L_Id.Value, // User Id와 동일
                        Locker = row.Locker,
                        ShoeLocker = row.ShoeLocker
                    } : null
                };

                // ActivePeriod(1:1)이 있으면 세팅
                //if (row.A_Id.HasValue)
                //{
                //    user.ActivePeriod = new ActivePeriod
                //    {
                //        Id = row.A_Id.Value, // User Id와 동일
                //        StartDate = row.StartDate ?? DateTime.MinValue,
                //        EndDate = row.EndDate ?? DateTime.MinValue
                //    };
                //}

                //if (row.B_Id.HasValue)
                //{
                //    user.Locker = new LockerModel
                //    {
                //        Id = row.B_Id.Value, // User Id와 동일
                //        Locker = row.Locker ?? "",
                //        ShoeLocker = row.ShoeLocker ?? ""
                //    };
                //}

                result.Add(user);
            }

            return result;
        }
        catch (Exception ex)
        {
            _log?.Error($"[DbServiceForGym] FetchUsersAsync Error: {ex}");
            return null;
        }
    }

    /// <summary>
    /// 특정 User Id로 1건 조회(+ ActivePeriod, LockerModel)
    /// </summary>
    //public async Task<UserModel?> FetchUserByIdAsync(int userId, CancellationToken token = default)
    //{
    //    try
    //    {
    //        if (_conn == null || _conn.State != ConnectionState.Open)
    //            throw new Exception("DB not connected.");

    //        var sql = @"
    //            SELECT 
    //                U.Id AS U_Id,
    //                U.UserName,
    //                U.MobilePhone,
    //                U.Age,
    //                U.Gender,
    //                U.RegisterDate,
    //                U.IsActive,
    //                U.CreatedAt,
    //                U.UpdatedAt,

    //                A.Id AS A_Id,
    //                A.StartDate,
    //                A.EndDate
    //            FROM `UserTable` U
    //            LEFT JOIN `ActivePeriod` A
    //                ON U.Id = A.Id
    //            WHERE U.Id = @Id;
    //            ";

    //        var row = await _conn.QueryFirstOrDefaultAsync<UserActiveLockerJoin>(sql, new { Id = userId });

    //        if (row == null)
    //            return null;

    //        // Enum 파싱
    //        // row.Gender → EnumGenderType 변환
    //        var genderParsed = ParseEnum<EnumGenderType>(row.Gender);
    //        var truefalseParsed = ParseEnum<EnumTrueFalse>(row.IsActive);

    //        var user = new UserModel
    //        {
    //            Id = row.U_Id,
    //            UserName = row.UserName,
    //            MobilePhone = row.MobilePhone,
    //            Age = row.Age,
    //            Gender = genderParsed,
    //            RegisterDate = row.RegisterDate ?? DateTime.MinValue,
    //            IsActive = truefalseParsed,
    //            CreatedAt = row.CreatedAt ?? DateTime.MinValue,
    //            UpdatedAt = row.UpdatedAt ?? DateTime.MinValue,
    //        };

    //        if (row.A_Id.HasValue)
    //        {
    //            user.ActivePeriod = new ActivePeriod
    //            {
    //                Id = row.A_Id.Value, // = userId
    //                StartDate = row.StartDate ?? DateTime.MinValue,
    //                EndDate = row.EndDate ?? DateTime.MinValue
    //            };
    //        }

    //        return user;
    //    }
    //    catch (Exception ex)
    //    {
    //        _log?.Error($"[DbServiceForGym] FetchUserByIdAsync Error: {ex}");
    //        return null;
    //    }
    //}

    public async Task<UserModel?> FetchUserByIdAsync(int userId, CancellationToken token = default)
    {
        try
        {
            if (_conn == null || _conn.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            var sql = @"
                SELECT 
                    U.Id AS U_Id,
                    U.UserName,
                    U.MobilePhone,
                    U.Age,
                    U.Gender,
                    U.RegisterDate,
                    U.IsActive,
                    U.CreatedAt,
                    U.UpdatedAt,

                    A.StartDate,
                    A.EndDate,

                    L.Locker,
                    L.ShoeLocker
                FROM `UserTable` U
                LEFT JOIN `ActivePeriod` A ON U.Id = A.Id
                LEFT JOIN `LockerTable` L ON U.Id = L.Id
                WHERE U.Id = @Id;
            ";

            var row = await _conn.QueryFirstOrDefaultAsync<UserActiveLockerJoin>(sql, new { Id = userId });

            if (row == null)
                return null;

            // Enum 변환
            var genderParsed = ParseEnum<EnumGenderType>(row.Gender);
            var truefalseParsed = ParseEnum<EnumTrueFalse>(row.IsActive);

            // User 객체 생성
            var user = new UserModel
            {
                Id = row.U_Id,
                UserName = row.UserName,
                MobilePhone = row.MobilePhone,
                Age = row.Age,
                Gender = genderParsed,
                RegisterDate = row.RegisterDate ?? DateTime.MinValue,
                IsActive = truefalseParsed,
                CreatedAt = row.CreatedAt ?? DateTime.MinValue,
                UpdatedAt = row.UpdatedAt ?? DateTime.MinValue,
                ActivePeriod = row.StartDate.HasValue ? new ActivePeriod
                {
                    StartDate = row.StartDate.Value,
                    EndDate = row.EndDate.Value
                } : null,
                Locker = !string.IsNullOrEmpty(row.Locker) ? new LockerModel
                {
                    Locker = row.Locker,
                    ShoeLocker = row.ShoeLocker
                } : null
            };

            return user;
        }
        catch (Exception ex)
        {
            _log?.Error($"[DbServiceForGym] FetchUserByIdAsync Error: {ex}");
            return null;
        }
    }


    /// <summary>
    /// EmsMessage + Attached Images 조회(1:N)
    /// </summary>
    public async Task<List<EmsMessageModel>?> FetchEmsMessagesAsync(CancellationToken token = default)
    {
        try
        {
            if (_conn == null || _conn.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            // EmsMessage + Image 조인
            // 조인 쿼리에서 EmsMessage의 NoticeType, MsgType을 
            // AS 별칭으로 문자열 필드(NoticeTypeStr, MsgTypeStr)에 매핑
            var sql = @"
                SELECT
                  EM.Id AS EmsId,
                  EM.UserId,
                  EM.NoticeType AS NoticeTypeStr,
                  EM.MsgType AS MsgTypeStr,
                  EM.Sender,
                  EM.Receiver,
                  EM.Message,
                  EM.Title,
                  EM.Destination,
                  EM.Reservation,
                  EM.SendTime,

                  I.Id AS ImageId,
                  I.EmsMessageId,
                  I.Base64Image,
                  I.FileName,
                  I.ContentType

                FROM EmsMessage EM
                LEFT JOIN Image I ON EM.Id = I.EmsMessageId
                ORDER BY EM.Id;";

            var lookup = new Dictionary<int, EmsMessageModel>();

            var rows = await _conn.QueryAsync<EmsMessageImageJoin>(sql);

            foreach (var row in rows)
            {
                if (!lookup.TryGetValue(row.EmsId, out var ems))
                {
                    // ★ Enum 파싱 ★
                    // row.NoticeTypeStr, row.MsgTypeStr → EnumNoticeType, EnumMsgType 변환
                    var noticeTypeParsed = ParseEnum<EnumNoticeType>(row.NoticeTypeStr);
                    var msgTypeParsed = ParseEnum<EnumMsgType>(row.MsgTypeStr);

                    ems = new EmsMessageModel
                    {
                        Id = row.EmsId,
                        UserId = row.UserId,
                        NoticeType = noticeTypeParsed,
                        MsgType = msgTypeParsed,
                        Sender = row.Sender ?? "",
                        Receiver = row.Receiver ?? "",
                        Message = row.Message ?? "",
                        Title = row.Title ?? "",
                        Destination = row.Destination ?? "",
                        Reservation = row.Reservation,
                        SendTime = row.SendTime,
                        AttachedImages = new List<ImageModel>()
                    };
                    lookup[row.EmsId] = ems;
                }

                // 이미지가 있으면 추가
                var img = new ImageModel
                {
                    Id = row.ImageId,
                    EmsMessageId = row.EmsMessageId,
                    Base64Image = row.Base64Image ?? "",
                    FileName = row.FileName ?? "",
                    ContentType = row.ContentType ?? ""
                };
                lookup[row.EmsId].AttachedImages?.Add(img);
            }

            return lookup.Values.ToList();
        }
        catch (Exception ex)
        {
            _log?.Error($"[DbServiceForGym] FetchEmsMessagesAsync Error: {ex}");
            return null;
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

    #region - INSERT -
    /// <summary>
    /// User 생성. 필요시 ActiveUser도 생성(1:1)
    /// </summary>
    public async Task<int> InsertUserAsync(IUserModel user, CancellationToken token = default)
    {
        try
        {
            if (_conn == null || _conn.State != ConnectionState.Open)
                throw new Exception("DB 연결이 이루어지지 않았습니다.");

            // 1) UserTable Insert
            var sqlUser = @"
                INSERT INTO `UserTable`
                    (UserName, MobilePhone, Age, Gender, RegisterDate, IsActive)
                VALUES
                    (@UserName, @MobilePhone, @Age, @Gender, @RegisterDate, @IsActive);
                SELECT LAST_INSERT_ID();
                ";

            // user.Gender는 Enum이므로 문자열로 변환
            var newId = await _conn.ExecuteScalarAsync<int>(sqlUser, new
            {
                user.UserName,
                user.MobilePhone,
                user.Age,
                Gender = user.Gender.ToString(),
                user.RegisterDate,
                IsActive = user.IsActive.ToString(),
            });

            user.Id = newId;

            //// 2) ActiveUserModel이 있다면, ActiveUser에 INSERT
            ////    PK=Id를 그대로 사용
            //if (user.ActivePeriod != null)
            //{
            //    user.ActivePeriod.Id = newId; // User의 Id를 ActivePeriod Id에 설정
            //    await InsertActiveUserAsync(user.ActivePeriod, token);
            //}

            // 2) ActivePeriod 삽입
            if (user.ActivePeriod != null)
            {
                user.ActivePeriod.Id = newId;
                await InsertActiveUserAsync(user.ActivePeriod, token);
            }

            // 3) Locker 삽입
            if (user.Locker != null)
            {
                user.Locker.Id = newId;
                await InsertLockerAsync(user.Locker, token);
            }

            _log?.Info($"[DbServiceForGym] InsertUserAsync 완료 - NewId={newId}, Name={user.UserName}");
            return newId;
        }
        catch (Exception ex)
        {
            _log?.Error($"[DbServiceForGym] InsertUserAsync Error: {ex}");
            return 0;
        }
    }

    /// <summary>
    /// ActiveUser만 따로 생성
    /// </summary>
    public async Task InsertActiveUserAsync(IActivePeriod model, CancellationToken token = default)
    {
        try
        {
            if (_conn == null || _conn.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            // Id가 이미 UserTable.Id와 동일해야 함
            var sql = @"
                INSERT INTO `ActivePeriod`
                    (`Id`, `StartDate`, `EndDate`)
                VALUES
                    (@Id, @StartDate, @EndDate);
                ";
            await _conn.ExecuteAsync(sql, model);
            _log?.Info($"[DbServiceForGym] InsertActiveUserAsync 완료 - Id={model.Id}");
        }
        catch (Exception ex)
        {
            _log?.Error($"[DbServiceForGym] InsertActiveUserAsync Error: {ex}");
        }
    }

    /// <summary>
    /// Locker 정보 삽입
    /// </summary>
    public async Task InsertLockerAsync(ILockerModel locker, CancellationToken token = default)
    {
        try
        {
            if (_conn == null || _conn.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            var sql = @"
            INSERT INTO `LockerTable`
                (`Id`, `Locker`, `ShoeLocker`)
            VALUES
                (@Id, @Locker, @ShoeLocker);
        ";
            await _conn.ExecuteAsync(sql, locker);
            _log?.Info($"[DbServiceForGym] InsertLockerAsync 완료 - Id={locker.Id}");
        }
        catch (Exception ex)
        {
            _log?.Error($"[DbServiceForGym] InsertLockerAsync Error: {ex}");
        }
    }

    /// <summary>
    /// EmsMessage + Image 삽입
    /// </summary>
    public async Task<int> InsertEmsMessageAsync(IEmsMessageModel msg, CancellationToken token = default)
    {

        try
        {
            if (_conn == null || _conn.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            // 1) EmsMessage Insert
            var sql = @"
                INSERT INTO `EmsMessage`
                  (UserId, NoticeType, MsgType, Sender, Receiver, `Message`, Title, Destination, Reservation, SendTime)
                VALUES
                  (@UserId, @NoticeType, @MsgType, @Sender, @Receiver, @Message, @Title, @Destination, @Reservation, @SendTime);
                SELECT LAST_INSERT_ID();";

            // EnumNoticeType, EnumMsgType → INT 변환
            var newId = await _conn.ExecuteScalarAsync<int>(sql, new
            {
                msg.UserId,
                NoticeType = msg.NoticeType.ToString(),
                MsgType = msg.MsgType.ToString(),
                msg.Sender,
                msg.Receiver,
                msg.Message,
                msg.Title,
                msg.Destination,
                msg.Reservation,
                msg.SendTime
            });

            // 2) Attached Images Insert
            if (msg.AttachedImages != null && msg.AttachedImages.Count > 0)
            {
                foreach (var img in msg.AttachedImages)
                {
                    var sqlImage = @"
                        INSERT INTO `Image`
                           (EmsMessageId, Base64Image, FileName, ContentType)
                        VALUES
                           (@EmsMessageId, @Base64Image, @FileName, @ContentType);";

                    await _conn.ExecuteAsync(sqlImage, new
                    {
                        EmsMessageId = newId,
                        img.Base64Image,
                        img.FileName,
                        img.ContentType
                    });
                }
            }

            _log?.Info($"[DbServiceForGym] InsertEmsMessageAsync - NewId={newId}");
            return newId;
        }
        catch (Exception ex)
        {
            _log?.Error($"[DbServiceForGym] InsertEmsMessageAsync Error: {ex}");
            return 0;
        }
    }
    #endregion

    #region - UPDATE -
    //public async Task UpdateUserAsync(IUserModel user, CancellationToken token = default)
    //{
    //    try
    //    {
    //        if (_conn == null || _conn.State != ConnectionState.Open)
    //            throw new Exception("DB 연결이 이루어지지 않았습니다.");

    //        var sql = @"
    //            UPDATE `UserTable`
    //            SET
    //                UserName=@UserName,
    //                MobilePhone=@MobilePhone,
    //                Age=@Age,
    //                Gender=@Gender,
    //                RegisterDate=@RegisterDate,
    //                IsActive=@IsActive
    //            WHERE Id=@Id;
    //            ";

    //        await _conn.ExecuteAsync(sql, new
    //        {
    //            user.UserName,
    //            user.MobilePhone,
    //            user.Age,
    //            Gender = user.Gender.ToString(),
    //            user.RegisterDate,
    //            IsActive = user.IsActive.ToString(),
    //            user.Id
    //        });

    //        _log?.Info($"[DbServiceForGym] UpdateUserAsync - Id={user.Id}");

    //        // ActiveUser도 수정해야 한다면
    //        if (user.ActivePeriod != null)
    //        {
    //            user.ActivePeriod.Id = user.Id; // 동일 ID 보장
    //            await UpdateActiveUserAsync(user.ActivePeriod, token);
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        _log?.Error($"[DbServiceForGym] UpdateUserAsync Error: {ex}");
    //    }
    //}

    /// <summary>
    /// 사용자 + ActivePeriod + Locker 업데이트
    /// </summary>
    public async Task UpdateUserAsync(IUserModel user, CancellationToken token = default)
    {
        try
        {
            if (_conn == null || _conn.State != ConnectionState.Open)
                throw new Exception("DB 연결이 이루어지지 않았습니다.");

            var sql = @"
                UPDATE `UserTable`
                SET
                    UserName=@UserName,
                    MobilePhone=@MobilePhone,
                    Age=@Age,
                    Gender=@Gender,
                    RegisterDate=@RegisterDate,
                    IsActive=@IsActive
                WHERE Id=@Id;
            ";

            await _conn.ExecuteAsync(sql, new
            {
                user.UserName,
                user.MobilePhone,
                user.Age,
                Gender = user.Gender.ToString(),
                user.RegisterDate,
                IsActive = user.IsActive.ToString(),
                user.Id
            });

            //ActiveUser도 수정해야 한다면
            if (user.ActivePeriod != null)
            {
                await UpdateActiveUserAsync(user.ActivePeriod, token);
            }

            if (user.Locker != null)
            {
                await UpdateLockerAsync(user.Locker, token);
            }

            _log?.Info($"[DbServiceForGym] UpdateUserAsync - Id={user.Id}");
        }
        catch (Exception ex)
        {
            _log?.Error($"[DbServiceForGym] UpdateUserAsync Error: {ex}");
        }
    }

    public async Task UpdateActiveUserAsync(IActivePeriod model, CancellationToken token = default)
    {
        try
        {
            if (_conn == null || _conn.State != ConnectionState.Open)
                throw new Exception("DB 연결이 이루어지지 않았습니다.");

            var sql = @"
                UPDATE `ActivePeriod`
                SET
                    StartDate=@StartDate,
                    EndDate=@EndDate
                WHERE Id=@Id; 
                ";
            await _conn.ExecuteAsync(sql, model);
            _log?.Info($"[DbServiceForGym] UpdateActiveUserAsync - Id={model.Id}");
        }
        catch (Exception ex)
        {
            _log?.Error($"[DbServiceForGym] UpdateActiveUserAsync Error: {ex}");
        }
    }

    /// <summary>
    /// Locker 정보 업데이트
    /// </summary>
    public async Task UpdateLockerAsync(ILockerModel locker, CancellationToken token = default)
    {
        try
        {
            if (_conn == null || _conn.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            var sql = @"
            UPDATE `LockerTable`
            SET
                Locker=@Locker,
                ShoeLocker=@ShoeLocker
            WHERE Id=@Id;
        ";
            await _conn.ExecuteAsync(sql, locker);
            _log?.Info($"[DbServiceForGym] UpdateLockerAsync - Id={locker.Id}");
        }
        catch (Exception ex)
        {
            _log?.Error($"[DbServiceForGym] UpdateLockerAsync Error: {ex}");
        }
    }
    #endregion

    #region - DELETE -
    /// <summary>
    /// User 삭제 → ActivePeriod 자동 삭제 (ON DELETE CASCADE)
    /// </summary>
    public async Task DeleteUserAsync(int userId, CancellationToken token = default)
    {
        try
        {
            if (_conn == null || _conn.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            var sql = "DELETE FROM `UserTable` WHERE Id=@Id;";
            var ret = await _conn.ExecuteAsync(sql, new { Id = userId });
            if (ret > 0)
                _log?.Info($"[DbServiceForGym] DeleteUserAsync - Id={userId}");
            else
                _log?.Info($"[DbServiceForGym] DeleteUserAsync was not executed...");

        }
        catch (Exception ex)
        {
            _log?.Error($"[DbServiceForGym] DeleteUserAsync Error: {ex}");
        }
    }

    /// <summary>
    /// ActiveUser만 제거(사용자는 남김)
    /// </summary>
    public async Task DeleteActiveUserAsync(int userId, CancellationToken token = default)
    {

        try
        {
            if (_conn == null || _conn.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            var sql = "DELETE FROM `ActivePeriod` WHERE Id=@Id;";
            await _conn.ExecuteAsync(sql, new { Id = userId });
            _log?.Info($"[DbServiceForGym] DeleteActiveUserAsync - Id={userId}");
        }
        catch (Exception ex)
        {
            _log?.Error($"[DbServiceForGym] DeleteActiveUserAsync Error: {ex}");
        }
    }

    /// <summary>
    /// Locker만 제거(사용자는 남김)
    /// </summary>
    public async Task DeleteLockerAsync(int userId, CancellationToken token = default)
    {

        try
        {
            if (_conn == null || _conn.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            var sql = "DELETE FROM `LockerTable` WHERE Id=@Id;";
            await _conn.ExecuteAsync(sql, new { Id = userId });
            _log?.Info($"[DbServiceForGym] DeleteLockerAsync - Id={userId}");
        }
        catch (Exception ex)
        {
            _log?.Error($"[DbServiceForGym] DeleteLockerAsync Error: {ex}");
        }
    }

    /// <summary>
    /// EmsMessage 단건 삭제(이미지 포함)
    /// ON DELETE CASCADE로 Image 레코드도 자동 삭제
    /// </summary>
    public async Task DeleteEmsMessageAsync(int emsId, CancellationToken token = default)
    {
        try
        {
            if (_conn == null || _conn.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            var sql = "DELETE FROM EmsMessage WHERE Id=@Id;";
            await _conn.ExecuteAsync(sql, new { Id = emsId });
            _log?.Info($"[DbServiceForGym] DeleteEmsMessageAsync - Id={emsId}");
        }
        catch (Exception ex)
        {
            _log?.Error($"[DbServiceForGym] DeleteEmsMessageAsync Error: {ex}");
        }
    }

    /// <summary>
    /// 모든 사용자를 삭제합니다. (UserTable, ActivePeriod, 관련 EmsMessage 포함)
    /// </summary>
    public async Task DeleteAllUsersAsync(CancellationToken token = default)
    {
        try
        {
            if (_conn == null || _conn.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            _log?.Info("[DbServiceForGym] 모든 사용자를 삭제하는 작업을 시작합니다.");

            // 1) UserTable의 모든 데이터 삭제 (ActivePeriod 및 EmsMessage는 ON DELETE CASCADE 적용)
            var sql = "DELETE FROM `UserTable`;";
            int affectedRows = await _conn.ExecuteAsync(sql);

            _log?.Info($"[DbServiceForGym] DeleteAllUsersAsync 완료 - 삭제된 사용자 수: {affectedRows}");
        }
        catch (Exception ex)
        {
            _log?.Error($"[DbServiceForGym] DeleteAllUsersAsync Error: {ex}");
        }
    }
    #endregion
    // User + ActivePeriod + Locker 조인을 담기 위한 임시 클래스
    internal class UserActiveLockerJoin
    {
        public int U_Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string MobilePhone { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Gender { get; set; } = string.Empty;
        public DateTime? RegisterDate { get; set; }
        public string IsActive { get; set; } = string.Empty;
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public int? A_Id { get; set; }  // ActivePeriod.Id (== User.Id)
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public int? L_Id { get; set; }  // Locker.Id (== User.Id)
        public string? Locker { get; set; }
        public string? ShoeLocker { get; set; }
    }

    /// <summary>
    /// DTO(익명 클래스) for JOIN
    /// </summary>
    internal class EmsMessageImageJoin
    {
        // EmsMessage
        public int EmsId { get; set; }
        public int UserId { get; set; }

        public string NoticeTypeStr { get; set; } = string.Empty;
        public string MsgTypeStr { get; set; } = string.Empty;

        public string Sender { get; set; } = string.Empty;
        public string Receiver { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;

        public DateTime Reservation { get; set; }
        public DateTime SendTime { get; set; }

        // Image
        public int ImageId { get; set; }
        public int EmsMessageId { get; set; }
        public string Base64Image { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
    }
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    private bool IsConnected =>
            _conn != null && _conn.State == ConnectionState.Open;
    #endregion
    #region - Attributes -
    private ILogService? _log;
    private IEventAggregator? _eventAggregator;
    private DbSetupModel _setup;
    private UserProvider _userProvider;
    private EmsMessageProvider _emsProvider;
    private MySqlConnection? _conn;
    #endregion

}