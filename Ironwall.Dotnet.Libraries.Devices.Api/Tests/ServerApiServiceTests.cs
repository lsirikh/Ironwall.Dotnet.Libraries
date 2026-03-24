using Autofac;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Modules;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Tests;
/****************************************************************************
   Purpose      : ServerApiService 단위 테스트
   Created By   : Claude
   Created On   : 2026-02-24
   Department   : SW Team
   Company      : Sensorway Co., Ltd.

   Description  : Server Category, Server Instance, Server Metrics API 통합 테스트
                  GOP API 서버 (localhost:8000) 가동 필요
                  ⚠ type_server는 EnumServerType 유효값만 허용 (UNIQUE 제약)
                  테스트 간 병렬 실행 방지를 위해 [Collection] 사용
****************************************************************************/

[Collection("ServerApiTests")]
public class ServerApiServiceTests
{
    private readonly ILogService _logService;
    private readonly ApiSetupModel _setupModel;

    public ServerApiServiceTests()
    {
        _logService = new LogService();
        _setupModel = new ApiSetupModel
        {
            Url = "http://localhost:8000/api",
            Username = "admin",
            Password = "admin123",
            Timeout = 30
        };
    }

    private async Task<IServerApiService> CreateServiceAsync()
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(_logService, _setupModel, "ServerApi"));
        var container = builder.Build();
        var service = container.ResolveNamed<IServerApiService>("ServerApi");
        await service.ExecuteAsync(CancellationToken.None);
        return service;
    }

    #region - Server Category API Tests (§8.2) -

    [Fact(DisplayName = "A59.1: GetCategoriesAsync — 카테고리 목록 조회")]
    public async Task GetCategoriesAsync_ShouldReturnCategoryList()
    {
        // Arrange
        var service = await CreateServiceAsync();

        // Act
        var response = await service.GetCategoriesAsync();

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"API 호출 실패: {response.Message}");
        Assert.NotNull(response.Data);
        Assert.NotNull(response.Pagination);
        Assert.True(response.Pagination.Page >= 1);
        Assert.True(response.Pagination.Limit >= 1);

        _logService.Info($"[Test] Categories count: {response.Data.Count}, Total: {response.Pagination.Total}");
    }

    [Fact(DisplayName = "A59.2: CreateCategoryAsync — 카테고리 생성")]
    public async Task CreateCategoryAsync_ShouldReturnCreatedCategory()
    {
        // Arrange
        var service = await CreateServiceAsync();
        var dto = new CategoryDto
        {
            Name = "테스트_카테고리",
            TypeServer = "ETC",
            Description = "TDD 테스트용 카테고리",
            SortOrder = 99
        };

        // Act
        var response = await service.CreateCategoryAsync(dto);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"API 호출 실패: {response.Message}");
        Assert.NotNull(response.Data);
        Assert.True(response.Data.Id > 0, "생성된 Category ID가 0보다 커야 함");
        Assert.Equal("테스트_카테고리", response.Data.Name);

        _logService.Info($"[Test] Category created: Id={response.Data.Id}, Name={response.Data.Name}");

        // Cleanup
        await service.DeleteCategoryAsync(response.Data.Id);
    }

    [Fact(DisplayName = "A59.3: GetCategoryByIdAsync — 카테고리 상세 조회 (서버 목록 포함)")]
    public async Task GetCategoryByIdAsync_ShouldReturnCategoryWithServers()
    {
        // Arrange
        var service = await CreateServiceAsync();
        var createResponse = await service.CreateCategoryAsync(new CategoryDto
        {
            Name = "상세조회_테스트",
            TypeServer = "MEDIA",
            Description = "GetById 테스트",
            SortOrder = 98
        });
        Assert.True(createResponse.Success, $"선행 Create 실패: {createResponse.Message}");
        var createdId = createResponse.Data!.Id;

        // Act
        var response = await service.GetCategoryByIdAsync(createdId);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"API 호출 실패: {response.Message}");
        Assert.NotNull(response.Data);
        Assert.Equal(createdId, response.Data.Id);
        Assert.Equal("상세조회_테스트", response.Data.Name);

        _logService.Info($"[Test] Category detail: Id={response.Data.Id}, Servers={response.Data.Servers?.Count ?? 0}");

        // Cleanup
        await service.DeleteCategoryAsync(createdId);
    }

    [Fact(DisplayName = "A59.4: PatchCategoryAsync — 카테고리 부분 수정")]
    public async Task PatchCategoryAsync_ShouldUpdatePartialFields()
    {
        // Arrange
        var service = await CreateServiceAsync();
        var createResponse = await service.CreateCategoryAsync(new CategoryDto
        {
            Name = "패치_테스트",
            TypeServer = "RECORDING",
            Description = "원본 설명",
            SortOrder = 97
        });
        Assert.True(createResponse.Success, $"선행 Create 실패: {createResponse.Message}");
        var createdId = createResponse.Data!.Id;

        // Act
        var patchDto = new CategoryDto { Description = "수정된 설명" };
        var response = await service.PatchCategoryAsync(createdId, patchDto);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"API 호출 실패: {response.Message}");

        // GET으로 확인
        var getResponse = await service.GetCategoryByIdAsync(createdId);
        Assert.True(getResponse.Success);
        Assert.Equal("수정된 설명", getResponse.Data!.Description);

        _logService.Info($"[Test] Category patched: Description={getResponse.Data.Description}");

        // Cleanup
        await service.DeleteCategoryAsync(createdId);
    }

    [Fact(DisplayName = "A59.5: UpdateCategoryAsync — 카테고리 전체 교체")]
    public async Task UpdateCategoryAsync_ShouldReplaceAllFields()
    {
        // Arrange
        var service = await CreateServiceAsync();
        var createResponse = await service.CreateCategoryAsync(new CategoryDto
        {
            Name = "교체_테스트",
            TypeServer = "PLAYBACK",
            Description = "원본",
            SortOrder = 96
        });
        Assert.True(createResponse.Success, $"선행 Create 실패: {createResponse.Message}");
        var createdId = createResponse.Data!.Id;

        // Act — PUT은 같은 type_server 유지 (UNIQUE 제약)
        var updateDto = new CategoryDto
        {
            Name = "교체_완료",
            TypeServer = "PLAYBACK",
            Description = "전체 교체됨",
            SortOrder = 50
        };
        var response = await service.UpdateCategoryAsync(createdId, updateDto);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"API 호출 실패: {response.Message}");

        var getResponse = await service.GetCategoryByIdAsync(createdId);
        Assert.True(getResponse.Success);
        Assert.Equal("교체_완료", getResponse.Data!.Name);
        Assert.Equal("전체 교체됨", getResponse.Data.Description);
        Assert.Equal(50, getResponse.Data.SortOrder);

        _logService.Info($"[Test] Category updated: Name={getResponse.Data.Name}");

        // Cleanup
        await service.DeleteCategoryAsync(createdId);
    }

    [Fact(DisplayName = "A59.6: DeleteCategoryAsync — 카테고리 삭제")]
    public async Task DeleteCategoryAsync_ShouldReturnSuccess()
    {
        // Arrange
        var service = await CreateServiceAsync();
        var createResponse = await service.CreateCategoryAsync(new CategoryDto
        {
            Name = "삭제_테스트",
            TypeServer = "LOG",
            Description = "삭제될 카테고리",
            SortOrder = 95
        });
        Assert.True(createResponse.Success, $"선행 Create 실패: {createResponse.Message}");
        var createdId = createResponse.Data!.Id;

        // Act
        var response = await service.DeleteCategoryAsync(createdId);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"API 호출 실패: {response.Message}");

        _logService.Info($"[Test] Category deleted: Id={createdId}");
    }

    [Fact(DisplayName = "A59.7: Category CRUD Lifecycle — Create→Read→Patch→Update→Delete")]
    public async Task CategoryCrudLifecycle_CreateReadUpdateDelete()
    {
        var service = await CreateServiceAsync();

        // 1. Create
        var createResponse = await service.CreateCategoryAsync(new CategoryDto
        {
            Name = "라이프사이클_테스트",
            TypeServer = "BACKUP",
            Description = "CRUD 전체 흐름",
            SortOrder = 90
        });
        Assert.True(createResponse.Success, $"Create 실패: {createResponse.Message}");
        var id = createResponse.Data!.Id;
        Assert.True(id > 0);

        // 2. Read (GetById)
        var getResponse = await service.GetCategoryByIdAsync(id);
        Assert.True(getResponse.Success, $"GetById 실패: {getResponse.Message}");
        Assert.Equal("라이프사이클_테스트", getResponse.Data!.Name);

        // 3. Patch
        var patchResponse = await service.PatchCategoryAsync(id, new CategoryDto { Description = "패치됨" });
        Assert.True(patchResponse.Success, $"Patch 실패: {patchResponse.Message}");

        // 4. Update (PUT) — 같은 type_server 유지 (UNIQUE 제약)
        var updateResponse = await service.UpdateCategoryAsync(id, new CategoryDto
        {
            Name = "라이프사이클_완료",
            TypeServer = "BACKUP",
            Description = "PUT 교체됨",
            SortOrder = 91
        });
        Assert.True(updateResponse.Success, $"Update 실패: {updateResponse.Message}");

        // 5. Verify final state
        var verifyResponse = await service.GetCategoryByIdAsync(id);
        Assert.True(verifyResponse.Success);
        Assert.Equal("라이프사이클_완료", verifyResponse.Data!.Name);
        Assert.Equal("PUT 교체됨", verifyResponse.Data.Description);

        // 6. Delete
        var deleteResponse = await service.DeleteCategoryAsync(id);
        Assert.True(deleteResponse.Success, $"Delete 실패: {deleteResponse.Message}");

        _logService.Info("[Test] Category CRUD lifecycle completed successfully");
    }

    #endregion

    #region - Server Instance API Tests (§8.3) -

    [Fact(DisplayName = "A60.1: GetServersAsync — 서버 목록 조회")]
    public async Task GetServersAsync_ShouldReturnServerList()
    {
        // Arrange
        var service = await CreateServiceAsync();

        // Act
        var response = await service.GetServersAsync();

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"API 호출 실패: {response.Message}");
        Assert.NotNull(response.Data);
        Assert.NotNull(response.Pagination);
        Assert.True(response.Pagination.Page >= 1);
        Assert.True(response.Pagination.Total >= 1);

        _logService.Info($"[Test] Servers count: {response.Data.Count}, Total: {response.Pagination.Total}");
    }

    [Fact(DisplayName = "A60.2: CreateServerAsync — 서버 생성")]
    public async Task CreateServerAsync_ShouldReturnCreatedServer()
    {
        // Arrange
        var service = await CreateServiceAsync();
        var dto = new ServerDto
        {
            CategoryId = 1,  // VMS 카테고리 (시드 데이터)
            Name = "테스트_서버",
            IpAddress = "10.0.0.99",
            Port = 9999,
            Hostname = "test-server"
        };

        // Act
        var response = await service.CreateServerAsync(dto);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"API 호출 실패: {response.Message}");
        Assert.NotNull(response.Data);
        Assert.True(response.Data.Id > 0, "생성된 Server ID가 0보다 커야 함");
        Assert.Equal("테스트_서버", response.Data.Name);

        _logService.Info($"[Test] Server created: Id={response.Data.Id}, Name={response.Data.Name}");

        // Cleanup
        await service.DeleteServerAsync(response.Data.Id);
    }

    [Fact(DisplayName = "A60.3: GetServerByIdAsync — 서버 상세 조회")]
    public async Task GetServerByIdAsync_ShouldReturnSingleServer()
    {
        // Arrange
        var service = await CreateServiceAsync();
        var createResponse = await service.CreateServerAsync(new ServerDto
        {
            CategoryId = 1,
            Name = "상세조회_서버",
            IpAddress = "10.0.0.98",
            Port = 9998,
            Hostname = "detail-server"
        });
        Assert.True(createResponse.Success, $"선행 Create 실패: {createResponse.Message}");
        var createdId = createResponse.Data!.Id;

        // Act
        var response = await service.GetServerByIdAsync(createdId);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"API 호출 실패: {response.Message}");
        Assert.NotNull(response.Data);
        Assert.Equal(createdId, response.Data.Id);
        Assert.Equal("상세조회_서버", response.Data.Name);

        _logService.Info($"[Test] Server detail: Id={response.Data.Id}, Name={response.Data.Name}");

        // Cleanup
        await service.DeleteServerAsync(createdId);
    }

    [Fact(DisplayName = "A60.4: PatchServerAsync — 서버 부분 수정")]
    public async Task PatchServerAsync_ShouldUpdatePartialFields()
    {
        // Arrange
        var service = await CreateServiceAsync();
        var createResponse = await service.CreateServerAsync(new ServerDto
        {
            CategoryId = 1,
            Name = "패치_서버",
            IpAddress = "10.0.0.97",
            Port = 9997,
            Status = "NORMAL"
        });
        Assert.True(createResponse.Success, $"선행 Create 실패: {createResponse.Message}");
        var createdId = createResponse.Data!.Id;

        // Act
        var patchDto = new ServerDto { Status = "WARNING" };
        var response = await service.PatchServerAsync(createdId, patchDto);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"API 호출 실패: {response.Message}");

        var getResponse = await service.GetServerByIdAsync(createdId);
        Assert.True(getResponse.Success);
        Assert.Equal("WARNING", getResponse.Data!.Status);

        _logService.Info($"[Test] Server patched: Status={getResponse.Data.Status}");

        // Cleanup
        await service.DeleteServerAsync(createdId);
    }

    [Fact(DisplayName = "A60.5: UpdateServerAsync — 서버 전체 교체")]
    public async Task UpdateServerAsync_ShouldReplaceAllFields()
    {
        // Arrange
        var service = await CreateServiceAsync();
        var createResponse = await service.CreateServerAsync(new ServerDto
        {
            CategoryId = 1,
            Name = "교체_서버",
            IpAddress = "10.0.0.96",
            Port = 9996
        });
        Assert.True(createResponse.Success, $"선행 Create 실패: {createResponse.Message}");
        var createdId = createResponse.Data!.Id;

        // Act
        var updateDto = new ServerDto
        {
            CategoryId = 1,
            Name = "교체_완료_서버",
            IpAddress = "10.0.0.196",
            Port = 8888,
            Hostname = "updated-server"
        };
        var response = await service.UpdateServerAsync(createdId, updateDto);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"API 호출 실패: {response.Message}");

        var getResponse = await service.GetServerByIdAsync(createdId);
        Assert.True(getResponse.Success);
        Assert.Equal("교체_완료_서버", getResponse.Data!.Name);
        Assert.Equal("10.0.0.196", getResponse.Data.IpAddress);
        Assert.Equal(8888, getResponse.Data.Port);

        _logService.Info($"[Test] Server updated: Name={getResponse.Data.Name}");

        // Cleanup
        await service.DeleteServerAsync(createdId);
    }

    [Fact(DisplayName = "A60.6: DeleteServerAsync — 서버 삭제")]
    public async Task DeleteServerAsync_ShouldReturnSuccess()
    {
        // Arrange
        var service = await CreateServiceAsync();
        var createResponse = await service.CreateServerAsync(new ServerDto
        {
            CategoryId = 1,
            Name = "삭제_서버",
            IpAddress = "10.0.0.95",
            Port = 9995
        });
        Assert.True(createResponse.Success, $"선행 Create 실패: {createResponse.Message}");
        var createdId = createResponse.Data!.Id;

        // Act
        var response = await service.DeleteServerAsync(createdId);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"API 호출 실패: {response.Message}");

        _logService.Info($"[Test] Server deleted: Id={createdId}");
    }

    [Fact(DisplayName = "A60.7: Server CRUD Lifecycle — Create→Read→Patch→Update→Delete")]
    public async Task ServerCrudLifecycle_CreateReadUpdateDelete()
    {
        var service = await CreateServiceAsync();

        // 1. Create
        var createResponse = await service.CreateServerAsync(new ServerDto
        {
            CategoryId = 1,
            Name = "라이프사이클_서버",
            IpAddress = "10.0.0.94",
            Port = 9994,
            Hostname = "lifecycle-server"
        });
        Assert.True(createResponse.Success, $"Create 실패: {createResponse.Message}");
        var id = createResponse.Data!.Id;
        Assert.True(id > 0);

        // 2. Read (GetById)
        var getResponse = await service.GetServerByIdAsync(id);
        Assert.True(getResponse.Success, $"GetById 실패: {getResponse.Message}");
        Assert.Equal("라이프사이클_서버", getResponse.Data!.Name);

        // 3. Patch
        var patchResponse = await service.PatchServerAsync(id, new ServerDto { Status = "ERROR" });
        Assert.True(patchResponse.Success, $"Patch 실패: {patchResponse.Message}");

        // 4. Update (PUT)
        var updateResponse = await service.UpdateServerAsync(id, new ServerDto
        {
            CategoryId = 1,
            Name = "라이프사이클_완료",
            IpAddress = "10.0.0.194",
            Port = 7777,
            Status = "NORMAL"
        });
        Assert.True(updateResponse.Success, $"Update 실패: {updateResponse.Message}");

        // 5. Verify final state
        var verifyResponse = await service.GetServerByIdAsync(id);
        Assert.True(verifyResponse.Success);
        Assert.Equal("라이프사이클_완료", verifyResponse.Data!.Name);
        Assert.Equal(7777, verifyResponse.Data.Port);

        // 6. Delete
        var deleteResponse = await service.DeleteServerAsync(id);
        Assert.True(deleteResponse.Success, $"Delete 실패: {deleteResponse.Message}");

        _logService.Info("[Test] Server CRUD lifecycle completed successfully");
    }

    #endregion

    #region - Server Metrics API Tests (§8.6) -

    [Fact(DisplayName = "A61.1: CreateServerMetricAsync — 메트릭 기록")]
    public async Task CreateServerMetricAsync_ShouldRecordMetric()
    {
        // Arrange
        var service = await CreateServiceAsync();
        var dto = new ServerMetricDto
        {
            CpuUsage = 55.5,
            RamUsage = 70.0,
            RamTotalGb = 32.0,
            RamUsedGb = 22.4,
            DiskUsage = 60.0,
            DiskTotalGb = 500.0,
            DiskUsedGb = 300.0,
            NetworkInMbps = 80.0,
            NetworkOutMbps = 40.0,
            ProcessCount = 200
        };

        // Act — 시드 서버 id=1 (VMS-01) 사용
        var response = await service.CreateServerMetricAsync(1, dto);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"API 호출 실패: {response.Message}");
        Assert.NotNull(response.Data);
        Assert.Equal(1, response.Data.ServerId);
        Assert.Equal(55.5, response.Data.CpuUsage);

        _logService.Info($"[Test] Metric created: Id={response.Data.Id}, CPU={response.Data.CpuUsage}");
    }

    [Fact(DisplayName = "A61.2: GetServerMetricsAsync — 메트릭 이력 조회")]
    public async Task GetServerMetricsAsync_ShouldReturnMetricHistory()
    {
        // Arrange
        var service = await CreateServiceAsync();

        // Act — 시드 서버 id=1
        var response = await service.GetServerMetricsAsync(1);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"API 호출 실패: {response.Message}");
        Assert.NotNull(response.Data);

        _logService.Info($"[Test] Metrics history count: {response.Data.Count}");
    }

    [Fact(DisplayName = "A61.3: GetServerMetricLatestAsync — 최신 메트릭 조회")]
    public async Task GetServerMetricLatestAsync_ShouldReturnLatest()
    {
        // Arrange
        var service = await CreateServiceAsync();

        // Act — 시드 서버 id=1
        var response = await service.GetServerMetricLatestAsync(1);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"API 호출 실패: {response.Message}");
        Assert.NotNull(response.Data);
        Assert.Equal(1, response.Data.ServerId);
        Assert.NotNull(response.Data.LatestMetrics);

        _logService.Info($"[Test] Latest metric: CPU={response.Data.LatestMetrics?.CpuUsage}");
    }

    [Fact(DisplayName = "A61.4: DeleteServerMetricsAsync — 메트릭 삭제")]
    public async Task DeleteServerMetricsAsync_ShouldReturnDeletedCount()
    {
        // Arrange
        var service = await CreateServiceAsync();
        // 먼저 메트릭 1건 생성
        await service.CreateServerMetricAsync(1, new ServerMetricDto
        {
            CpuUsage = 10.0,
            RamUsage = 20.0,
            ProcessCount = 50
        });

        // Act — 과거 날짜로 삭제 (삭제 대상 없어도 성공)
        var response = await service.DeleteServerMetricsAsync(1, "2020-01-01");

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"API 호출 실패: {response.Message}");
        Assert.NotNull(response.Data);
        Assert.True(response.Data.DeletedCount >= 0);

        _logService.Info($"[Test] Metrics deleted: {response.Data.DeletedCount}");
    }

    [Fact(DisplayName = "A61.5: Server Metrics Lifecycle — Record→Query→Latest→Delete")]
    public async Task ServerMetricsLifecycle_RecordQueryDelete()
    {
        var service = await CreateServiceAsync();

        // 1. Create metric
        var createResponse = await service.CreateServerMetricAsync(1, new ServerMetricDto
        {
            CpuUsage = 88.8,
            RamUsage = 77.7,
            DiskUsage = 66.6,
            ProcessCount = 300
        });
        Assert.True(createResponse.Success, $"Create 실패: {createResponse.Message}");
        Assert.Equal(1, createResponse.Data!.ServerId);

        // 2. Get history
        var historyResponse = await service.GetServerMetricsAsync(1);
        Assert.True(historyResponse.Success, $"GetHistory 실패: {historyResponse.Message}");
        Assert.True(historyResponse.Data!.Count >= 1);

        // 3. Get latest
        var latestResponse = await service.GetServerMetricLatestAsync(1);
        Assert.True(latestResponse.Success, $"GetLatest 실패: {latestResponse.Message}");
        Assert.NotNull(latestResponse.Data!.LatestMetrics);

        // 4. Delete (과거 날짜 → 삭제 0건이어도 성공)
        var deleteResponse = await service.DeleteServerMetricsAsync(1, "2020-01-01");
        Assert.True(deleteResponse.Success, $"Delete 실패: {deleteResponse.Message}");

        _logService.Info("[Test] Server Metrics lifecycle completed successfully");
    }

    #endregion

    #region - Proxy Settings API Tests (§8.8) -

    [Fact(DisplayName = "5.1: GetProxySettingsAsync — 프록시 설정 조회 (server_id=1)")]
    public async Task GetProxySettingsAsync_ShouldReturnProxySetting()
    {
        // Arrange
        var service = await CreateServiceAsync();

        // Act — 시드 서버 id=1
        var response = await service.GetProxySettingsAsync(1);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, $"API 호출 실패: {response.Message}");
        Assert.NotNull(response.Data);
        Assert.Equal(1, response.Data.ServerId);
        Assert.False(string.IsNullOrEmpty(response.Data.WindyMode), "WindyMode 값이 비어있음");
        Assert.False(string.IsNullOrEmpty(response.Data.OperationMode), "OperationMode 값이 비어있음");

        _logService.Info($"[Test] ProxySetting: ServerId={response.Data.ServerId}, WindyMode={response.Data.WindyMode}, OperationMode={response.Data.OperationMode}");
    }

    #endregion
}
