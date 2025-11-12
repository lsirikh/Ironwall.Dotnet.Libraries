using Autofac;
using Ironwall.Dotnet.Libraries.Api.Messages.Common;
using Ironwall.Dotnet.Libraries.Api.Messages.Events;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Api.Modules;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
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
/// </summary>
public class EventApiServiceTests
{
    private readonly ILogService _logService;
    private readonly ApiSetupModel _setupModel;
    private IContainer? _container;

    #region - Ctors -
    public EventApiServiceTests()
    {
        _logService = new LogService();

        // GOP API 서버 설정 (실제 테스트 시 GOP 서버 URL로 변경 필요)
        _setupModel = new ApiSetupModel
        {
            Url = "http://localhost:8000/api/",  // GOP API 서버 주소
            Username = "admin",
            Password = "admin123",
            ApiKey = "",
            Phone = "",
            Timeout = 30
        };
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
        var setup1 = new ApiSetupModel { Url = "http://localhost:8000/api/" };
        var setup2 = new ApiSetupModel { Url = "http://localhost:8000/api/" };

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
    /// 참고: 실제 GOP 서버가 없으면 실패할 수 있음
    /// </summary>
    [Fact(DisplayName = "03. Get DetectionEvents")]
    public async Task GetDetectionEvents_Should_Return_List()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new EventApiModule(_logService, _setupModel, "EventApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IEventApiService>("EventApi");
        await service.ExecuteAsync(CancellationToken.None);

        // Act
        var response = await service.GetDetectionEventsAsync(
            startDate: DateTime.Now.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            endDate: DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    /// <summary>
    /// Detection Event 단일 조회 API 테스트
    /// </summary>
    [Fact(DisplayName = "04. Get DetectionEvent By ID")]
    public async Task GetDetectionEventById_Should_Return_Single()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new EventApiModule(_logService, _setupModel, "EventApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IEventApiService>("EventApi");
        await service.ExecuteAsync(CancellationToken.None);

        // Act
        var response = await service.GetDetectionEventByIdAsync(1);

        // Assert
        Assert.NotNull(response);
        if (response.Success)
        {
            Assert.NotNull(response.Data);
            Assert.Equal(1, response.Data.Id);
        }
    }

    /// <summary>
    /// Detection Event 생성 API 테스트
    /// </summary>
    [Fact(DisplayName = "05. Create DetectionEvent")]
    public async Task CreateDetectionEvent_Should_Return_Created()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new EventApiModule(_logService, _setupModel, "EventApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IEventApiService>("EventApi");
        await service.ExecuteAsync(CancellationToken.None);

        var dto = new DetectionEventDto
        {
            GroupEvent = "1",
            TypeEvent = "Intrusion",
            Controller = 1,
            Sensor = 2,
            TypeDevice = "Fence",
            Sequence = 1,
            ActionReported = "False",
            Result = "VIBRATION_SENSOR",
            Datetime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        };

        // Act
        var response = await service.CreateDetectionEventAsync(dto);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }
    #endregion

    #region - Malfunction Event API Tests -
    /// <summary>
    /// Malfunction Event 목록 조회 API 테스트
    /// </summary>
    [Fact(DisplayName = "06. Get MalfunctionEvents")]
    public async Task GetMalfunctionEvents_Should_Return_List()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new EventApiModule(_logService, _setupModel, "EventApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IEventApiService>("EventApi");
        await service.ExecuteAsync(CancellationToken.None);

        // Act
        var response = await service.GetMalfunctionEventsAsync(
            startDate: DateTime.Now.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            endDate: DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    /// <summary>
    /// Malfunction Event 단일 조회 API 테스트
    /// </summary>
    [Fact(DisplayName = "07. Get MalfunctionEvent By ID")]
    public async Task GetMalfunctionEventById_Should_Return_Single()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new EventApiModule(_logService, _setupModel, "EventApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IEventApiService>("EventApi");
        await service.ExecuteAsync(CancellationToken.None);

        // Act
        var response = await service.GetMalfunctionEventByIdAsync(1);

        // Assert
        Assert.NotNull(response);
        if (response.Success)
        {
            Assert.NotNull(response.Data);
        }
    }

    /// <summary>
    /// Malfunction Event 생성 API 테스트
    /// </summary>
    [Fact(DisplayName = "08. Create MalfunctionEvent")]
    public async Task CreateMalfunctionEvent_Should_Return_Created()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new EventApiModule(_logService, _setupModel, "EventApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IEventApiService>("EventApi");
        await service.ExecuteAsync(CancellationToken.None);

        var dto = new MalfunctionEventDto
        {
            Controller = 1,
            Sensor = 1,
            GroupEvent = "1",
            TypeEvent = "Fault",
            TypeDevice = "Fence",
            Sequence = 1,
            ActionReported = "False",  // Required by GOP API
            Reason = "FAULT_FENCE",
            FirstStart=10,
            FirstEnd=10,
            SecondStart=15,
            SecondEnd=15,
            Status = "True",
            Datetime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        };

        // Act
        var response = await service.CreateMalfunctionEventAsync(dto);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success,
            $"API Error: {response.Error?.Code} - {response.Message}. Details: {response.Error?.Details}");
        Assert.NotNull(response.Data);
    }
    #endregion

    #region - Connection Event API Tests -
    /// <summary>
    /// Connection Event 목록 조회 API 테스트
    /// </summary>
    [Fact(DisplayName = "09. Get ConnectionEvents")]
    public async Task GetConnectionEvents_Should_Return_List()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new EventApiModule(_logService, _setupModel, "EventApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IEventApiService>("EventApi");
        await service.ExecuteAsync(CancellationToken.None);

        // Act
        var response = await service.GetConnectionEventsAsync(
            startDate: DateTime.Now.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            endDate: DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    /// <summary>
    /// Connection Event 생성 API 테스트
    /// </summary>
    [Fact(DisplayName = "10. Create ConnectionEvent")]
    public async Task CreateConnectionEvent_Should_Return_Created()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new EventApiModule(_logService, _setupModel, "EventApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IEventApiService>("EventApi");
        await service.ExecuteAsync(CancellationToken.None);

        var dto = new ConnectionEventDto
        {
            Controller = 1,
            Sensor = 1,
            GroupEvent = "1",
            TypeEvent = "Connection",
            TypeDevice = "Fence",
            Sequence = 1,
            Datetime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        };

        // Act
        var response = await service.CreateConnectionEventAsync(dto);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }
    #endregion

    #region - Action Event API Tests -
    /// <summary>
    /// Action Event 목록 조회 API 테스트
    /// </summary>
    [Fact(DisplayName = "11. Get ActionEvents")]
    public async Task GetActionEvents_Should_Return_List()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new EventApiModule(_logService, _setupModel, "EventApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IEventApiService>("EventApi");
        await service.ExecuteAsync(CancellationToken.None);

        // Act
        var response = await service.GetActionEventsAsync(
            startDate: DateTime.Now.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            endDate: DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    /// <summary>
    /// Action Event 생성 API 테스트
    /// </summary>
    [Fact(DisplayName = "12. Create ActionEvent")]
    public async Task CreateActionEvent_Should_Return_Created()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterModule(new EventApiModule(_logService, _setupModel, "EventApi"));
        _container = builder.Build();

        var service = _container.ResolveNamed<IEventApiService>("EventApi");
        await service.ExecuteAsync(CancellationToken.None);

        var dto = new ActionEventCreateDto
        {
            TypeEvent = "Action",
            Content = "Test action content",  // Required by GOP API
            User = "admin",  // Required by GOP API
            FromEvent = 1,  // Required by GOP API
            FromEventType = "detection",  // Required by GOP API (must be 'detection', 'malfunction', or 'connection')
            Datetime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        };

        // Act
        var response = await service.CreateActionEventAsync(dto);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success,
            $"API Error: {response.Error?.Code} - {response.Message}. Details: {response.Error?.Details}");
        Assert.NotNull(response.Data);
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
