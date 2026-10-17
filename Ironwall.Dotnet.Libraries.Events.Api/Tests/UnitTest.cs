using Autofac;
using Ironwall.Dotnet.Libraries.Messages.Defines.Commons;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Api.Modules;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Tests.LiveApi;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Api.Tests;
/****************************************************************************
   Purpose      : EventApiService Unit Tests
   Created By   : GHLee
   Created On   : 11/11/2025 12:00:00 AM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// EventApiService 단위 테스트
/// <para>라이브 서버: IRONWALL_LIVE_API_URL(기본 https://localhost:8000/api) + IRONWALL_E2E_CRED_FILE 전용 계정 — 없으면 건너뜀,
/// 쓰기 시험은 loopback · 사설망 주소에서만 돈다. 모듈 등록 · 초기화 시험은 네트워크 없이 돈다.</para>
/// <para>2026-10-12: 시험 서버엔 시드 자료가 없다(INIT_SAMPLE_DATA=false). 고정 id(이벤트 1 · 2 · 5, 장비 2)와
/// <c>if (Success)</c> 무검증을 버리고, 시험마다 제어기 · 이벤트를 직접 만들어 단언한 뒤 finally 에서 지운다
/// (<see cref="LiveEventTestData"/>). 조회(By ID) 시험도 자기 이벤트를 만들어야 하므로 쓰기 시험이다.</para>
/// </summary>
[Trait("Scenario", "DEP-207")]
public class EventApiServiceTests
{
    private readonly ILogService _logService;
    private readonly ApiSetupModel _setupModel;
    private IContainer? _container;

    #region - Ctors -
    public EventApiServiceTests()
    {
        _logService = new LogService();

        // 모듈 등록 · 초기화 시험용 접속 설정 — 계정은 싣지 않는다(서버 호출 시험은 공용 로그인 세션을 쓴다).
        _setupModel = LiveApiTarget.CreateSetup();
    }
    #endregion

    #region - Live 시험 자료 도우미 -
    /// <summary>공용 세션의 이벤트 서비스와 이 시험 전용 제어기(이벤트의 device_id).</summary>
    private static async Task<(IEventApiService Service, int DeviceId)> ArrangeAsync()
    {
        var service = await LiveApiSession.ResolveAsync<IEventApiService>();
        var deviceId = await LiveEventTestData.CreateControllerAsync();
        return (service, deviceId);
    }

    private static Task CleanupDeviceAsync(int deviceId)
        => LiveEventTestData.DeleteDeviceAsync(LiveEventTestData.Controllers, deviceId);

    private static async Task<DetectionEventDto> CreateDetectionAsync(IEventApiService service, int deviceId)
    {
        var response = await service.CreateDetectionEventAsync(new DetectionEventDto
        {
            TypeEvent = "Intrusion",
            DeviceId = deviceId,
            Result = "VIBRATION_SENSOR"
        });
        Assert.True(response.Success,
            $"Detection create failed: {response.Error?.Code} - {response.Message}. Details: {response.Error?.Details}");
        Assert.NotNull(response.Data);
        Assert.True(response.Data.Id > 0);
        return response.Data;
    }

    private static async Task<MalfunctionEventDto> CreateMalfunctionAsync(IEventApiService service, int deviceId)
    {
        var response = await service.CreateMalfunctionEventAsync(new MalfunctionEventDto
        {
            TypeEvent = "Fault",
            DeviceId = deviceId,
            Reason = "FAULT_FENCE",
            Detail = new MalfunctionDetailDto { FirstStart = 10, FirstEnd = 10, SecondStart = 15, SecondEnd = 15 }
        });
        Assert.True(response.Success,
            $"Malfunction create failed: {response.Error?.Code} - {response.Message}. Details: {response.Error?.Details}");
        Assert.NotNull(response.Data);
        Assert.True(response.Data.Id > 0);
        return response.Data;
    }

    private static async Task<ConnectionEventDto> CreateConnectionAsync(IEventApiService service, int deviceId)
    {
        var response = await service.CreateConnectionEventAsync(new ConnectionEventDto
        {
            TypeEvent = "Connection",
            DeviceId = deviceId
        });
        Assert.True(response.Success,
            $"Connection create failed: {response.Error?.Code} - {response.Message}. Details: {response.Error?.Details}");
        Assert.NotNull(response.Data);
        Assert.True(response.Data.Id > 0);
        return response.Data;
    }

    private static async Task<ActionEventDto> CreateActionAsync(IEventApiService service, int fromEventId, string content = "Test action content")
    {
        var response = await service.CreateActionEventAsync(new ActionEventCreateDto
        {
            TypeEvent = "Action",
            Content = content,
            User = "admin1",
            FromEventId = fromEventId,
        });
        Assert.True(response.Success,
            $"Action create failed: {response.Error?.Code} - {response.Message}. Details: {response.Error?.Details}");
        Assert.NotNull(response.Data);
        Assert.True(response.Data.Id > 0);
        return response.Data;
    }

    /// <summary>
    /// 삭제 응답 단언 — 서버 DELETE 는 <c>data:null</c> 이라 <c>ApiResponse&lt;bool&gt;.Data</c> 는 늘 <c>false</c> 다.
    /// 성공 여부와 상태 코드(200)로 판정한다.
    /// </summary>
    private static void AssertDeleted(Ironwall.Dotnet.Libraries.Messages.Defines.Apis.ApiResponse<bool> response, string what)
    {
        Assert.True(response.Success, $"{what} delete failed: {response.Error?.Code} - {response.Message}");
        Assert.Equal(200, response.StatusCode);
    }
    #endregion

    #region - Module Registration Tests -
    /// <summary>
    /// Autofac 모듈 등록 테스트
    /// </summary>
    [Fact(DisplayName = "01. Autofac Module Registration")]
    public void Module_Registration_Should_Work()
    {
        // Arrange
        var builder = new ContainerBuilder();

        // Act
        builder.RegisterModule(new EventApiModule(_logService, _setupModel, "EventApi"));
        _container = builder.Build();

        // Assert
        Assert.True(_container.IsRegistered<IEventApiService>());

        var service = _container.ResolveNamed<IEventApiService>("EventApi");
        Assert.NotNull(service);
    }

    /// <summary>
    /// 다중 인스턴스 등록 테스트 (Named 등록)
    /// </summary>
    [Fact(DisplayName = "02. Multiple Named Instances Registration")]
    public void Multiple_Named_Instances_Should_Work()
    {
        // Arrange
        var builder = new ContainerBuilder();
        var setup1 = new ApiSetupModel { Url = LiveApiTarget.Url };
        var setup2 = new ApiSetupModel { Url = LiveApiTarget.Url };

        // Act
        builder.RegisterModule(new EventApiModule(_logService, setup1, "EventApi1"));
        builder.RegisterModule(new EventApiModule(_logService, setup2, "EventApi2"));
        _container = builder.Build();

        // Assert
        var service1 = _container.ResolveNamed<IEventApiService>("EventApi1");
        var service2 = _container.ResolveNamed<IEventApiService>("EventApi2");

        Assert.NotNull(service1);
        Assert.NotNull(service2);
        Assert.NotSame(service1, service2);
    }
    #endregion

    #region - Service Initialization Tests -
    /// <summary>
    /// 서비스 초기화 테스트
    /// </summary>
    [Fact(DisplayName = "03. Service Initialization")]
    public async Task Service_Initialization_Should_Work()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new EventApiModule(_logService, _setupModel, "EventApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IEventApiService>("EventApi");

        // Act
        await service.ExecuteAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(service);
    }
    #endregion

    #region - Detection Event API Tests -
    /// <summary>
    /// Detection Event 목록 조회 API 테스트
    /// </summary>
    [LiveApiFact(DisplayName = "03. Get DetectionEvents")]
    public async Task GetDetectionEvents_Should_Return_List()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IEventApiService>();

        // Act
        var response = await service.GetDetectionEventsAsync(
            startDate: DateTime.Now.AddDays(-3).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            endDate: DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"{response.Error?.Code} - {response.Message}");
        Assert.NotNull(response.Data);
    }

    /// <summary>
    /// Detection Event 단일 조회 API 테스트 — 자기 탐지 이벤트를 만들어 id · 종류 · 결과를 확인한다.
    /// </summary>
    [LiveApiWriteFact(DisplayName = "04. Get DetectionEvent By ID")]
    public async Task GetDetectionEventById_Should_Return_Single()
    {
        var (service, deviceId) = await ArrangeAsync();
        try
        {
            var created = await CreateDetectionAsync(service, deviceId);
            try
            {
                // Act
                var response = await service.GetDetectionEventByIdAsync(created.Id);

                // Assert
                Assert.True(response.Success, $"{response.Error?.Code} - {response.Message}");
                Assert.NotNull(response.Data);
                Assert.Equal(created.Id, response.Data.Id);
                Assert.Equal("Intrusion", response.Data.TypeEvent);
                Assert.Equal("VIBRATION_SENSOR", response.Data.Result);
            }
            finally
            {
                AssertDeleted(await service.DeleteDetectionEventAsync(created.Id), "Detection");
            }
        }
        finally
        {
            await CleanupDeviceAsync(deviceId);
        }
    }

    /// <summary>
    /// Detection Event 생성 API 테스트
    /// </summary>
    [LiveApiWriteFact(DisplayName = "05. Create DetectionEvent")]
    public async Task CreateDetectionEvent_Should_Return_Created()
    {
        var (service, deviceId) = await ArrangeAsync();
        try
        {
            // Act
            var response = await service.CreateDetectionEventAsync(new DetectionEventDto
            {
                TypeEvent = "Intrusion",
                DeviceId = deviceId,
                Result = "VIBRATION_SENSOR"
            });

            // Assert — 201(생성). 202 는 억제 창에 흡수된 것이라 저장되지 않았다(data 없음).
            Assert.True(response.Success,
                $"API Error: {response.Error?.Code} - {response.Message}. Details: {response.Error?.Details}");
            Assert.Equal(201, response.StatusCode);
            Assert.NotNull(response.Data);
            Assert.True(response.Data.Id > 0);
            Assert.Equal("VIBRATION_SENSOR", response.Data.Result);

            AssertDeleted(await service.DeleteDetectionEventAsync(response.Data.Id), "Detection");
        }
        finally
        {
            await CleanupDeviceAsync(deviceId);
        }
    }

    /// <summary>
    /// Detection Event 수정 (부분) API 테스트
    /// <para><c>action_reported</c> 는 요청 필드가 아니다 — 서버가 조치 생성 · 삭제로 재계산하고, 보내면 무시한다
    /// (<c>app/schemas/event.py:181-182 · 474-477</c>). 그래서 바꿀 수 있는 <c>result</c> 로 부분 수정을 확인한다.</para>
    /// </summary>
    [LiveApiWriteFact(DisplayName = "05-1. Patch DetectionEvent")]
    public async Task PatchDetectionEvent_Should_Return_Updated()
    {
        var (service, deviceId) = await ArrangeAsync();
        try
        {
            var created = await CreateDetectionAsync(service, deviceId);
            try
            {
                // Act
                var response = await service.PatchDetectionEventAsync(created.Id, new DetectionEventDto { Result = "PIR_SENSOR" });

                // Assert
                Assert.True(response.Success, $"{response.Error?.Code} - {response.Message}. Details: {response.Error?.Details}");
                Assert.NotNull(response.Data);
                Assert.Equal("PIR_SENSOR", response.Data.Result);

                var reread = await service.GetDetectionEventByIdAsync(created.Id);
                Assert.True(reread.Success);
                Assert.Equal("PIR_SENSOR", reread.Data!.Result);
            }
            finally
            {
                AssertDeleted(await service.DeleteDetectionEventAsync(created.Id), "Detection");
            }
        }
        finally
        {
            await CleanupDeviceAsync(deviceId);
        }
    }

    /// <summary>
    /// Detection Event 수정 (전체) API 테스트
    /// </summary>
    [LiveApiWriteFact(DisplayName = "05-2. Update DetectionEvent")]
    public async Task UpdateDetectionEvent_Should_Return_Updated()
    {
        var (service, deviceId) = await ArrangeAsync();
        try
        {
            var created = await CreateDetectionAsync(service, deviceId);
            try
            {
                // Act
                var response = await service.UpdateDetectionEventAsync(created.Id, new DetectionEventReplaceDto
                {
                    TypeEvent = "Intrusion",
                    Result = "THERMAL_SENSOR"
                });

                // Assert
                Assert.True(response.Success, $"{response.Error?.Code} - {response.Message}. Details: {response.Error?.Details}");
                Assert.NotNull(response.Data);
                Assert.Equal("THERMAL_SENSOR", response.Data.Result);
                Assert.NotNull(response.Data.Device);
            }
            finally
            {
                AssertDeleted(await service.DeleteDetectionEventAsync(created.Id), "Detection");
            }
        }
        finally
        {
            await CleanupDeviceAsync(deviceId);
        }
    }

    /// <summary>
    /// Detection Event 삭제 API 테스트
    /// </summary>
    [LiveApiWriteFact(DisplayName = "05-3. Delete DetectionEvent")]
    public async Task DeleteDetectionEvent_Should_Return_Success()
    {
        var (service, deviceId) = await ArrangeAsync();
        try
        {
            var created = await CreateDetectionAsync(service, deviceId);

            // Act
            var response = await service.DeleteDetectionEventAsync(created.Id);

            // Assert — 지운 뒤 조회는 실패해야 한다
            AssertDeleted(response, "Detection");
            var after = await service.GetDetectionEventByIdAsync(created.Id);
            Assert.False(after.Success, "삭제한 탐지 이벤트가 아직 조회된다");
        }
        finally
        {
            await CleanupDeviceAsync(deviceId);
        }
    }
    #endregion

    #region - Malfunction Event API Tests -
    /// <summary>
    /// Malfunction Event 목록 조회 API 테스트
    /// </summary>
    [LiveApiFact(DisplayName = "06. Get MalfunctionEvents")]
    public async Task GetMalfunctionEvents_Should_Return_List()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IEventApiService>();

        // Act
        var response = await service.GetMalfunctionEventsAsync(
            startDate: DateTime.Now.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            endDate: DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"{response.Error?.Code} - {response.Message}");
        Assert.NotNull(response.Data);
    }

    /// <summary>
    /// Malfunction Event 단일 조회 API 테스트 — 자기 장애 이벤트를 만들어 확인한다.
    /// </summary>
    [LiveApiWriteFact(DisplayName = "07. Get MalfunctionEvent By ID")]
    public async Task GetMalfunctionEventById_Should_Return_Single()
    {
        var (service, deviceId) = await ArrangeAsync();
        try
        {
            var created = await CreateMalfunctionAsync(service, deviceId);
            try
            {
                // Act
                var response = await service.GetMalfunctionEventByIdAsync(created.Id);

                // Assert
                Assert.True(response.Success, $"{response.Error?.Code} - {response.Message}");
                Assert.NotNull(response.Data);
                Assert.Equal(created.Id, response.Data.Id);
                Assert.Equal("Fault", response.Data.TypeEvent);
                Assert.Equal("FAULT_FENCE", response.Data.Reason);
            }
            finally
            {
                AssertDeleted(await service.DeleteMalfunctionEventAsync(created.Id), "Malfunction");
            }
        }
        finally
        {
            await CleanupDeviceAsync(deviceId);
        }
    }

    /// <summary>
    /// Malfunction Event 생성 API 테스트
    /// </summary>
    [LiveApiWriteFact(DisplayName = "08. Create MalfunctionEvent")]
    public async Task CreateMalfunctionEvent_Should_Return_Created()
    {
        var (service, deviceId) = await ArrangeAsync();
        try
        {
            // Act
            var response = await service.CreateMalfunctionEventAsync(new MalfunctionEventDto
            {
                TypeEvent = "Fault",
                DeviceId = deviceId,
                Reason = "FAULT_FENCE",
                Detail = new MalfunctionDetailDto
                {
                    FirstStart = 10,
                    FirstEnd = 10,
                    SecondStart = 15,
                    SecondEnd = 15
                }
            });

            // Assert
            Assert.True(response.Success,
                $"API Error: {response.Error?.Code} - {response.Message}. Details: {response.Error?.Details}");
            Assert.Equal(201, response.StatusCode);
            Assert.NotNull(response.Data);
            Assert.True(response.Data.Id > 0);
            Assert.Equal("FAULT_FENCE", response.Data.Reason);

            AssertDeleted(await service.DeleteMalfunctionEventAsync(response.Data.Id), "Malfunction");
        }
        finally
        {
            await CleanupDeviceAsync(deviceId);
        }
    }

    /// <summary>
    /// Malfunction Event 수정 (부분) API 테스트
    /// <para><c>action_reported</c> 는 서버 관리 값이라 싣지 않는다(event.py:181-182) — <c>reason</c> 만 바꾼다.</para>
    /// </summary>
    [LiveApiWriteFact(DisplayName = "08-1. Patch MalfunctionEvent")]
    public async Task PatchMalfunctionEvent_Should_Return_Updated()
    {
        var (service, deviceId) = await ArrangeAsync();
        try
        {
            var created = await CreateMalfunctionAsync(service, deviceId);
            try
            {
                // Act
                var response = await service.PatchMalfunctionEventAsync(created.Id, new MalfunctionEventDto { Reason = "FAULT_MULTI" });

                // Assert
                Assert.True(response.Success, $"{response.Error?.Code} - {response.Message}. Details: {response.Error?.Details}");
                Assert.NotNull(response.Data);
                Assert.Equal("FAULT_MULTI", response.Data.Reason);
            }
            finally
            {
                AssertDeleted(await service.DeleteMalfunctionEventAsync(created.Id), "Malfunction");
            }
        }
        finally
        {
            await CleanupDeviceAsync(deviceId);
        }
    }

    /// <summary>
    /// Malfunction Event 수정 (전체) API 테스트
    /// </summary>
    [LiveApiWriteFact(DisplayName = "08-2. Update MalfunctionEvent")]
    public async Task UpdateMalfunctionEvent_Should_Return_Updated()
    {
        var (service, deviceId) = await ArrangeAsync();
        try
        {
            var created = await CreateMalfunctionAsync(service, deviceId);
            try
            {
                // Act
                var response = await service.UpdateMalfunctionEventAsync(created.Id, new MalfunctionEventReplaceDto
                {
                    TypeEvent = "Fault",
                    Reason = "FAULT_ETC",
                    Detail = new MalfunctionDetailDto
                    {
                        FirstStart = 2,
                        FirstEnd = 2,
                        SecondStart = 5,
                        SecondEnd = 5
                    }
                });

                // Assert
                Assert.True(response.Success, $"{response.Error?.Code} - {response.Message}. Details: {response.Error?.Details}");
                Assert.NotNull(response.Data);
                Assert.Equal("FAULT_ETC", response.Data.Reason);
                Assert.NotNull(response.Data.Device);
            }
            finally
            {
                AssertDeleted(await service.DeleteMalfunctionEventAsync(created.Id), "Malfunction");
            }
        }
        finally
        {
            await CleanupDeviceAsync(deviceId);
        }
    }

    /// <summary>
    /// Malfunction Event 삭제 API 테스트
    /// </summary>
    [LiveApiWriteFact(DisplayName = "08-3. Delete MalfunctionEvent")]
    public async Task DeleteMalfunctionEvent_Should_Return_Success()
    {
        var (service, deviceId) = await ArrangeAsync();
        try
        {
            var created = await CreateMalfunctionAsync(service, deviceId);

            // Act
            var response = await service.DeleteMalfunctionEventAsync(created.Id);

            // Assert
            AssertDeleted(response, "Malfunction");
            var after = await service.GetMalfunctionEventByIdAsync(created.Id);
            Assert.False(after.Success, "삭제한 장애 이벤트가 아직 조회된다");
        }
        finally
        {
            await CleanupDeviceAsync(deviceId);
        }
    }
    #endregion

    #region - Connection Event API Tests -
    /// <summary>
    /// Connection Event 목록 조회 API 테스트
    /// </summary>
    [LiveApiFact(DisplayName = "09. Get ConnectionEvents")]
    public async Task GetConnectionEvents_Should_Return_List()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IEventApiService>();

        // Act
        var response = await service.GetConnectionEventsAsync(
            startDate: DateTime.Now.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            endDate: DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"{response.Error?.Code} - {response.Message}");
        Assert.NotNull(response.Data);
    }

    /// <summary>
    /// Connection Event 단일 조회 API 테스트 — 자기 연결 이벤트를 만들어 확인한다.
    /// </summary>
    [LiveApiWriteFact(DisplayName = "09-1. Get ConnectionEvent By ID")]
    public async Task GetConnectionEventById_Should_Return_Single()
    {
        var (service, deviceId) = await ArrangeAsync();
        try
        {
            var created = await CreateConnectionAsync(service, deviceId);
            try
            {
                // Act
                var response = await service.GetConnectionEventByIdAsync(created.Id);

                // Assert
                Assert.True(response.Success, $"{response.Error?.Code} - {response.Message}");
                Assert.NotNull(response.Data);
                Assert.Equal(created.Id, response.Data.Id);
                Assert.Equal("Connection", response.Data.TypeEvent);
            }
            finally
            {
                AssertDeleted(await service.DeleteConnectionEventAsync(created.Id), "Connection");
            }
        }
        finally
        {
            await CleanupDeviceAsync(deviceId);
        }
    }

    /// <summary>
    /// Connection Event 생성 API 테스트
    /// </summary>
    [LiveApiWriteFact(DisplayName = "10. Create ConnectionEvent")]
    public async Task CreateConnectionEvent_Should_Return_Created()
    {
        var (service, deviceId) = await ArrangeAsync();
        try
        {
            // Act
            var response = await service.CreateConnectionEventAsync(new ConnectionEventDto
            {
                TypeEvent = "Connection",
                DeviceId = deviceId,
            });

            // Assert
            Assert.True(response.Success,
                $"API Error: {response.Error?.Code} - {response.Message}. Details: {response.Error?.Details}");
            Assert.Equal(201, response.StatusCode);
            Assert.NotNull(response.Data);
            Assert.True(response.Data.Id > 0);

            AssertDeleted(await service.DeleteConnectionEventAsync(response.Data.Id), "Connection");
        }
        finally
        {
            await CleanupDeviceAsync(deviceId);
        }
    }

    /// <summary>
    /// Connection Event 수정 (부분) API 테스트 — 연결 이벤트에서 바꿀 수 있는 키는 <c>type_event</c> 하나다(event.py:717-730).
    /// </summary>
    [LiveApiWriteFact(DisplayName = "10-1. Patch ConnectionEvent")]
    public async Task PatchConnectionEvent_Should_Return_Updated()
    {
        var (service, deviceId) = await ArrangeAsync();
        try
        {
            var created = await CreateConnectionAsync(service, deviceId);
            try
            {
                // Act
                var response = await service.PatchConnectionEventAsync(created.Id, new ConnectionEventDto
                {
                    TypeEvent = "Connection",
                });

                // Assert
                Assert.True(response.Success, $"{response.Error?.Code} - {response.Message}. Details: {response.Error?.Details}");
                Assert.NotNull(response.Data);
                Assert.Equal(created.Id, response.Data.Id);
                Assert.Equal("Connection", response.Data.TypeEvent);
            }
            finally
            {
                AssertDeleted(await service.DeleteConnectionEventAsync(created.Id), "Connection");
            }
        }
        finally
        {
            await CleanupDeviceAsync(deviceId);
        }
    }

    /// <summary>
    /// Connection Event 수정 (전체) API 테스트
    /// </summary>
    [LiveApiWriteFact(DisplayName = "10-2. Update ConnectionEvent")]
    public async Task UpdateConnectionEvent_Should_Return_Updated()
    {
        var (service, deviceId) = await ArrangeAsync();
        try
        {
            var created = await CreateConnectionAsync(service, deviceId);
            try
            {
                // Act
                var response = await service.UpdateConnectionEventAsync(created.Id, new ConnectionEventReplaceDto
                {
                    TypeEvent = "Connection"
                });

                // Assert
                Assert.True(response.Success, $"{response.Error?.Code} - {response.Message}. Details: {response.Error?.Details}");
                Assert.NotNull(response.Data);
                Assert.Equal("Connection", response.Data.TypeEvent);
                Assert.NotNull(response.Data.Device);
            }
            finally
            {
                AssertDeleted(await service.DeleteConnectionEventAsync(created.Id), "Connection");
            }
        }
        finally
        {
            await CleanupDeviceAsync(deviceId);
        }
    }

    /// <summary>
    /// Connection Event 삭제 API 테스트
    /// </summary>
    [LiveApiWriteFact(DisplayName = "10-3. Delete ConnectionEvent")]
    public async Task DeleteConnectionEvent_Should_Return_Success()
    {
        var (service, deviceId) = await ArrangeAsync();
        try
        {
            var created = await CreateConnectionAsync(service, deviceId);

            // Act
            var response = await service.DeleteConnectionEventAsync(created.Id);

            // Assert
            AssertDeleted(response, "Connection");
            var after = await service.GetConnectionEventByIdAsync(created.Id);
            Assert.False(after.Success, "삭제한 연결 이벤트가 아직 조회된다");
        }
        finally
        {
            await CleanupDeviceAsync(deviceId);
        }
    }
    #endregion

    #region - Action Event API Tests -
    /// <summary>
    /// Action Event 목록 조회 API 테스트
    /// </summary>
    [LiveApiFact(DisplayName = "11. Get ActionEvents")]
    public async Task GetActionEvents_Should_Return_List()
    {
        // Arrange
        var service = await LiveApiSession.ResolveAsync<IEventApiService>();

        // Act
        var response = await service.GetActionEventsAsync(
            startDate: DateTime.Now.AddDays(-10).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            endDate: DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"{response.Error?.Code} - {response.Message}");
        Assert.NotNull(response.Data);
    }

    /// <summary>
    /// Action Event 단일 조회 API 테스트 — 자기 탐지 이벤트에 조치를 달아 확인한다.
    /// </summary>
    [LiveApiWriteFact(DisplayName = "11-1. Get ActionEvent By ID")]
    public async Task GetActionEventById_Should_Return_Single()
    {
        var (service, deviceId) = await ArrangeAsync();
        try
        {
            var detection = await CreateDetectionAsync(service, deviceId);
            try
            {
                var action = await CreateActionAsync(service, detection.Id);
                try
                {
                    // Act
                    var response = await service.GetActionEventByIdAsync(action.Id);

                    // Assert
                    Assert.True(response.Success, $"{response.Error?.Code} - {response.Message}");
                    Assert.NotNull(response.Data);
                    Assert.Equal(action.Id, response.Data.Id);
                    Assert.Equal("Action", response.Data.TypeEvent);
                    Assert.Equal("admin1", response.Data.User);
                }
                finally
                {
                    AssertDeleted(await service.DeleteActionEventAsync(action.Id), "Action");
                }
            }
            finally
            {
                // 조치를 먼저 지워야 원본을 지울 수 있다(조치가 달린 원본은 409 — detections.py:634 · 648)
                AssertDeleted(await service.DeleteDetectionEventAsync(detection.Id), "Detection");
            }
        }
        finally
        {
            await CleanupDeviceAsync(deviceId);
        }
    }

    /// <summary>
    /// Action Event 생성 API 테스트
    /// </summary>
    [LiveApiWriteFact(DisplayName = "12. Create ActionEvent")]
    public async Task CreateActionEvent_Should_Return_Created()
    {
        var (service, deviceId) = await ArrangeAsync();
        try
        {
            var detection = await CreateDetectionAsync(service, deviceId);
            try
            {
                // Act
                var response = await service.CreateActionEventAsync(new ActionEventCreateDto
                {
                    TypeEvent = "Action",
                    Content = "Test action content",  // Required by GOP API
                    User = "admin1",  // Required by GOP API
                    FromEventId = detection.Id,  // Required by GOP API (events.id FK)
                });

                // Assert
                Assert.True(response.Success,
                    $"API Error: {response.Error?.Code} - {response.Message}. Details: {response.Error?.Details}");
                Assert.Equal(201, response.StatusCode);
                Assert.NotNull(response.Data);
                Assert.True(response.Data.Id > 0);
                Assert.Equal("Test action content", response.Data.Content);

                // 원본의 조치보고 여부가 서버에서 켜졌는지(조치 생성 = action_reported 재계산)
                var source = await service.GetDetectionEventByIdAsync(detection.Id);
                Assert.True(source.Success);
                Assert.True(string.Equals("true", source.Data!.ActionReported, StringComparison.OrdinalIgnoreCase),
                    $"action_reported='{source.Data.ActionReported}'");

                AssertDeleted(await service.DeleteActionEventAsync(response.Data.Id), "Action");
            }
            finally
            {
                AssertDeleted(await service.DeleteDetectionEventAsync(detection.Id), "Detection");
            }
        }
        finally
        {
            await CleanupDeviceAsync(deviceId);
        }
    }

    /// <summary>
    /// Action Event 수정 (부분) API 테스트
    /// </summary>
    [LiveApiWriteFact(DisplayName = "12-1. Patch ActionEvent")]
    public async Task PatchActionEvent_Should_Return_Updated()
    {
        var (service, deviceId) = await ArrangeAsync();
        try
        {
            var detection = await CreateDetectionAsync(service, deviceId);
            try
            {
                var action = await CreateActionAsync(service, detection.Id);
                try
                {
                    // Act
                    var response = await service.PatchActionEventAsync(action.Id, new ActionEventDto
                    {
                        Content = "침입 탐지 확인 완료 - 오탐지로 판명",
                        User = "operator_kim"
                    });

                    // Assert
                    Assert.True(response.Success, $"{response.Error?.Code} - {response.Message}. Details: {response.Error?.Details}");
                    Assert.NotNull(response.Data);
                    Assert.Equal("operator_kim", response.Data.User);
                    Assert.Equal("침입 탐지 확인 완료 - 오탐지로 판명", response.Data.Content);
                }
                finally
                {
                    AssertDeleted(await service.DeleteActionEventAsync(action.Id), "Action");
                }
            }
            finally
            {
                AssertDeleted(await service.DeleteDetectionEventAsync(detection.Id), "Detection");
            }
        }
        finally
        {
            await CleanupDeviceAsync(deviceId);
        }
    }

    /// <summary>
    /// Action Event 수정 (전체) API 테스트
    /// </summary>
    [LiveApiWriteFact(DisplayName = "12-2. Update ActionEvent")]
    public async Task UpdateActionEvent_Should_Return_Updated()
    {
        var (service, deviceId) = await ArrangeAsync();
        try
        {
            var detection = await CreateDetectionAsync(service, deviceId);
            try
            {
                var action = await CreateActionAsync(service, detection.Id);
                try
                {
                    // Act
                    var response = await service.UpdateActionEventAsync(action.Id, new ActionEventReplaceDto
                    {
                        TypeEvent = "Action",
                        Content = "침입 탐지 재확인 - 실제 침입 확인됨, 경찰 출동 요청",
                        User = "operator_park"
                    });

                    // Assert
                    Assert.True(response.Success, $"{response.Error?.Code} - {response.Message}. Details: {response.Error?.Details}");
                    Assert.NotNull(response.Data);
                    Assert.Equal("operator_park", response.Data.User);
                    Assert.Equal("침입 탐지 재확인 - 실제 침입 확인됨, 경찰 출동 요청", response.Data.Content);
                }
                finally
                {
                    AssertDeleted(await service.DeleteActionEventAsync(action.Id), "Action");
                }
            }
            finally
            {
                AssertDeleted(await service.DeleteDetectionEventAsync(detection.Id), "Detection");
            }
        }
        finally
        {
            await CleanupDeviceAsync(deviceId);
        }
    }

    /// <summary>
    /// Action Event 삭제 API 테스트
    /// </summary>
    [LiveApiWriteFact(DisplayName = "12-3. Delete ActionEvent")]
    public async Task DeleteActionEvent_Should_Return_Success()
    {
        var (service, deviceId) = await ArrangeAsync();
        try
        {
            var detection = await CreateDetectionAsync(service, deviceId);
            try
            {
                var action = await CreateActionAsync(service, detection.Id);

                // Act
                var response = await service.DeleteActionEventAsync(action.Id);

                // Assert — 삭제 응답의 data 는 null 이라 Success · 상태 코드로 판정한다
                AssertDeleted(response, "Action");
                var after = await service.GetActionEventByIdAsync(action.Id);
                Assert.False(after.Success, "삭제한 조치 이벤트가 아직 조회된다");
            }
            finally
            {
                // 조치가 지워졌으므로 원본도 지울 수 있어야 한다
                AssertDeleted(await service.DeleteDetectionEventAsync(detection.Id), "Detection");
            }
        }
        finally
        {
            await CleanupDeviceAsync(deviceId);
        }
    }
    #endregion

    #region - Setup Model Tests -
    /// <summary>
    /// ApiSetupModel (Events.Api partial) 생성 테스트
    /// </summary>
    [Fact(DisplayName = "13. ApiSetupModel Constructor")]
    public void SetupModel_Constructor_Should_Work()
    {
        // Act - Base properties from Ironwall.Dotnet.Libraries.Api.Models.ApiSetupModel
        var setup = new ApiSetupModel
        {
            Url = "http://test.com/api/v1/",
            Username = "testuser",
            Password = "testpass",
            ApiKey = "test-api-key",
            Phone = "010-1234-5678",
            Timeout = 30  // Extended property from Events.Api partial
        };

        // Assert
        Assert.Equal("http://test.com/api/v1/", setup.Url);
        Assert.Equal("testuser", setup.Username);
        Assert.Equal("testpass", setup.Password);
        Assert.Equal("test-api-key", setup.ApiKey);
        Assert.Equal("010-1234-5678", setup.Phone);
        Assert.Equal(30, setup.Timeout);
    }

    /// <summary>
    /// ApiSetupModel Partial 확장 테스트 (Timeout 기본값 확인)
    /// </summary>
    [Fact(DisplayName = "14. ApiSetupModel Partial Extension")]
    public void SetupModel_Partial_Extension_Should_Work()
    {
        // Act
        var setup = new ApiSetupModel
        {
            Url = "http://test.com/api/v1/",
            Timeout = 60
        };

        // Assert
        Assert.Equal("http://test.com/api/v1/", setup.Url);
        Assert.Equal(60, setup.Timeout);
    }
    #endregion
}
