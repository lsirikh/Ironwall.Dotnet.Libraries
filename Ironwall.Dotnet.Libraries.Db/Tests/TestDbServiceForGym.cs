using Caliburn.Micro;
using Dotnet.Gym.Message.Accounts;
using Dotnet.Gym.Message.Contacts;
using Dotnet.Gym.Message.Enums;
using Dotnet.Gym.Message.Providers;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Db.Models;
using Ironwall.Dotnet.Libraries.Db.Services;
using System;
using System.IO;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Db.Tests;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 2/3/2025 2:37:16 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class TestDbServiceForGym
{
    private const string TEST_DB_SERVER = "192.168.202.180";  // 실제 환경에 맞게 변경
    private const int TEST_DB_PORT = 3306;             // MariaDB 기본 포트
    private const string TEST_DB_NAME = "gymdb";   // 테스트용 DB
    private const string TEST_DB_USER = "root";        // 예시
    private const string TEST_DB_PASS = "root";            // 예시
    private DbSetupModel _setupModel;
    private UserProvider _users;
    private EmsMessageProvider _emsMessages;

    // DbServiceForGym 인스턴스
    private DbServiceForGym _service;

    // 생성자(xUnit의 [Fact]/[Theory] 메서드마다 새로 생성)
    public TestDbServiceForGym()
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
    }

    [Fact]
    public async Task Test01_BuildSchemeAsync()
    {
        await _service.Connect();
        await _service.BuildSchemeAsync();
        await _service.FetchInstanceAsync();
    }

    [Fact]
    public async Task Test02_InsertUser_And_Fetch()
    {
        await _service.StartService();

        // 1) 새 User 생성
        var newUser = new UserModel
        {
            UserName = "홍길동",
            MobilePhone = "010-1234-5678",
            Age = 30,
            Gender = EnumGenderType.MALE,
            RegisterDate = DateTime.Now,
            IsActive = EnumTrueFalse.True,
            ActivePeriod = null // 1:1 관계지만 아직 ActivePeriod 없이 생성
        };

        // Insert
        var newId = await _service.InsertUserAsync(newUser);
        Assert.True(newId > 0, "새로운 User Id가 0보다 커야 함.");

        // 2) FetchUsers
        var allUsers = await _service.FetchUsersAsync();
        Assert.NotNull(allUsers);
        Assert.True(allUsers.Count > 0);

        // 3) FetchUserById
        var fetchedOne = await _service.FetchUserByIdAsync(newId);
        Assert.NotNull(fetchedOne);
        Assert.Equal("홍길동", fetchedOne.UserName);

        // 종료
        await _service.StopService();
    }

    [Fact]
    public async Task Test03_ActiveUser_CRUD()
    {
        await _service.StartService();

        
        // 1) User를 하나 더 생성
        var newUser = new UserModel
        {
            UserName = "김사용",
            MobilePhone = $"010-9876-5545",
            Age = 25,
            Gender = EnumGenderType.FEMALE,
            RegisterDate = DateTime.Now,
            IsActive = EnumTrueFalse.True,
            ActivePeriod = null
        };
        var newUserId = await _service.InsertUserAsync(newUser);
        Assert.True(newUserId > 0);

        // 2) ActivePeriod 생성
        var au = new ActivePeriod
        {
            Id = newUserId,  // 1:1 -> User Id와 동일
            StartDate = DateTime.Now.Date,
            EndDate = DateTime.Now.Date.AddMonths(1)
        };
        await _service.InsertActiveUserAsync(au);

        // FetchUserById -> ActiveUser도 함께 조회되는지 확인
        var fetchedUser = await _service.FetchUserByIdAsync(newUserId);
        Assert.NotNull(fetchedUser);
        Assert.NotNull(fetchedUser.ActivePeriod);
        Assert.Equal(newUserId, fetchedUser.ActivePeriod.Id);

        // 3) Update ActivePeriod
        fetchedUser.ActivePeriod.EndDate = fetchedUser.ActivePeriod.EndDate?.AddMonths(1);
        await _service.UpdateActiveUserAsync(fetchedUser.ActivePeriod);

        // 다시 조회해서 갱신 여부 확인
        var updatedUser = await _service.FetchUserByIdAsync(newUserId);
        var newFinish = updatedUser.ActivePeriod.EndDate;
        Assert.Equal(2, (newFinish?.Year - DateTime.Now.Year) * 12 + (newFinish?.Month - DateTime.Now.Month)); // 단순 비교

        // 4) DeleteActiveUser
        await _service.DeleteActiveUserAsync(newUserId);

        var afterDeleteAU = await _service.FetchUserByIdAsync(newUserId);
        Assert.NotNull(afterDeleteAU);
        Assert.Null(afterDeleteAU.ActivePeriod);

        // 5) 마지막으로 User 삭제
        await _service.DeleteUserAsync(newUserId);

        var afterDeleteUser = await _service.FetchUserByIdAsync(newUserId);
        Assert.Null(afterDeleteUser);

        await _service.StopService();
    }

    [Fact]
    public async Task Test05_ActiveUser_CRUD()
    {
        await _service.StartService();

        var rand = new Random();
        //$"0101111{(10000 + i):D5}"
        for (int i = 0; i < 100; i++)
        {

            // 1) User를 하나 더 생성
            var newUser = new UserModel
            {
                UserName = $"김사용{i}",
                MobilePhone = $"010-9876-{(10000 + i):D5}",
                Age = 25,
                Gender = EnumGenderType.FEMALE,
                RegisterDate = (DateTime.Now - TimeSpan.FromDays((double)rand.Next(0, 100))),
                IsActive = EnumTrueFalse.True,
                ActivePeriod = null
            };
            var newUserId = await _service.InsertUserAsync(newUser);
            Assert.True(newUserId > 0);


            // 2) ActivePeriod 생성
            var au = new ActivePeriod
            {
                Id = newUserId,  // 1:1 -> User Id와 동일
                StartDate = DateTime.Now.Date - TimeSpan.FromDays((double)rand.Next(0, 100)),
                EndDate = DateTime.Now.Date.AddMonths(rand.Next(0, 10))
            };
            await _service.InsertActiveUserAsync(au);
            // FetchUserById -> ActiveUser도 함께 조회되는지 확인
            var fetchedUser = await _service.FetchUserByIdAsync(newUserId);
            Assert.NotNull(fetchedUser);
            Assert.NotNull(fetchedUser.ActivePeriod);
            Assert.Equal(newUserId, fetchedUser.ActivePeriod.Id);
        }
        await _service.StopService();
    }

    [Fact]
    public async Task Test04_UpdateUser()
    {
        await _service.StartService();

        // 1) Insert
        var newUser = new UserModel
        {
            UserName = "이초기",
            MobilePhone = "010-1111-2222",
            Age = 20,
            Gender = EnumGenderType.MALE,
            RegisterDate = DateTime.Now,
            IsActive = EnumTrueFalse.True,
        };
        var newUserId = await _service.InsertUserAsync(newUser);
        Assert.True(newUserId > 0);

        // 2) ActivePeriod 생성
        var au = new ActivePeriod
        {
            Id = newUserId,  // 1:1 -> User Id와 동일
            StartDate = DateTime.Now.Date,
            EndDate = DateTime.Now.Date.AddMonths(1)
        };
        await _service.InsertActiveUserAsync(au);

        // FetchUserById -> ActiveUser도 함께 조회되는지 확인
        var fetchedUser = await _service.FetchUserByIdAsync(newUserId);
        Assert.NotNull(fetchedUser);
        Assert.NotNull(fetchedUser.ActivePeriod);
        Assert.Equal(newUserId, fetchedUser.ActivePeriod.Id);

        // 3) Update
        newUser.Id = newUserId;
        newUser.UserName = "이수정";
        newUser.IsActive = EnumTrueFalse.True;

        await _service.UpdateUserAsync(newUser);

        // 3) Fetch
        var fetched = await _service.FetchUserByIdAsync(newUserId);
        Assert.Equal("이수정", fetched.UserName);
        Assert.Equal(EnumTrueFalse.True, fetched.IsActive);

        // 4) 마지막으로 User 삭제 Cascade 확인
        await _service.DeleteUserAsync(newUserId);

        var afterDeleteUser = await _service.FetchUserByIdAsync(newUserId);
        Assert.Null(afterDeleteUser);

        // 종료
        await _service.StopService();
    }

    [Fact]
    public async Task Test05_EmsMessage_CRUD()
    {
        // 1) Start
        await _service.StartService();

        // 2) 먼저 EmsMessage를 생성하기 위해 User를 하나 Insert
        var user = new UserModel
        {
            UserName = "사용자-EmsTest",
            MobilePhone = "010-9999-8888",
            Age = 28,
            Gender = EnumGenderType.FEMALE,
            RegisterDate = DateTime.Now,
            IsActive = EnumTrueFalse.True,
        };
        var userId = await _service.InsertUserAsync(user);
        Assert.True(userId > 0, "User Insert 실패");

        // 3) 샘플 이미지 읽어서 ImageModel 만들어보기
        string path1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tests", "sample1.jpg");
        byte[] bytes1 = File.ReadAllBytes(path1);
        string base64_1 = Convert.ToBase64String(bytes1);

        string path2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tests", "sample2.jpg");
        byte[] bytes2 = File.ReadAllBytes(path2);
        string base64_2 = Convert.ToBase64String(bytes2);

        var sampleImage1 = new ImageModel
        {
            Base64Image = base64_1,
            FileName = "sample1.jpg",
            ContentType = "image/jpeg"
        };

        var sampleImage2 = new ImageModel
        {
            Base64Image = base64_2,
            FileName = "sample2.jpg",
            ContentType = "image/jpeg"
        };

        // 4) EmsMessage 생성
        var newMessage = new EmsMessageModel
        {
            UserId = userId,
            NoticeType = EnumNoticeType.LockerEndNotice,  // 문자열로 DB 저장
            MsgType = EnumMsgType.MMS,                   // 문자열로 DB 저장
            Sender = "GymAdmin",
            Receiver = "010-5555-6666",
            Message = "사물함 이용기간이 곧 만료됩니다.",
            Title = "Locker Notice",
            Destination = "Push",
            Reservation = DateTime.Now.AddMinutes(30),
            SendTime = DateTime.Now,
            AttachedImages = new List<ImageModel>
            {
                sampleImage1,
                sampleImage2
            }
        };

        // 4) Insert EmsMessage
        var emsId = await _service.InsertEmsMessageAsync(newMessage);
        Assert.True(emsId > 0, "EmsMessage Insert 실패");

        // 5) FetchEmsMessagesAsync → 방금 Insert한 메시지 찾기
        var allMsgs = await _service.FetchEmsMessagesAsync();
        Assert.NotNull(allMsgs);
        Assert.True(allMsgs.Count > 0, "EmsMessage Fetch 결과가 비어 있음");

        // 가져온 목록에서 emsId에 해당하는 메시지 찾기
        var fetchedMsg = allMsgs.Find(x => x.Id == emsId);
        Assert.NotNull(fetchedMsg);
        Assert.Equal(EnumNoticeType.LockerEndNotice, fetchedMsg.NoticeType);
        Assert.Equal(EnumMsgType.MMS, fetchedMsg.MsgType);
        Assert.Equal("GymAdmin", fetchedMsg.Sender);
        Assert.Equal(2, fetchedMsg.AttachedImages?.Count);

        // 6) EmsMessage 삭제 (Cascade로 Image도 삭제)
        await _service.DeleteEmsMessageAsync(emsId);

        // 다시 Fetch하여 삭제 확인
        var afterDelete = await _service.FetchEmsMessagesAsync();
        var deletedMsg = afterDelete.Find(x => x.Id == emsId);
        Assert.Null(deletedMsg);  // 이미 삭제됨

        // 7) 마지막으로 테스트용 User 삭제
        await _service.DeleteUserAsync(userId);

        // 종료
        await _service.StopService();
    }

}

