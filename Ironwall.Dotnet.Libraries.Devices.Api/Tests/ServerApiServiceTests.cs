using Autofac;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Modules;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Tests.LiveApi;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
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
                  라이브 서버: IRONWALL_LIVE_API_URL(기본 https://localhost:8000/api)
                  + IRONWALL_E2E_CRED_FILE 전용 계정 — 없으면 건너뜀,
                  쓰기 시험은 loopback · 사설망 주소에서만 돈다
                  ⚠ 카테고리 유형(category_server)은 EnumServerType 유효값만 허용 (UNIQUE 제약)
                    — 이 파일은 시드에 없는 유형 AI_TRAINING · MEDIA · RECORDING · PLAYBACK · STORAGE · ANALYTICS ·
                      AI_INFERENCE · PIDS_API · WEB · AUTH · GATEWAY 만 쓴다(ServerApiIntegrationTest 와 겹치지 않게 —
                      두 파일은 다른 컬렉션이라 병렬로 돈다). 만드는 자료는 이름이 "LIVE-SRV-" 로 시작한다.
                  ⚠ 7.0+ 서버 등록 · 수정은 축 통로(IServerAxisApiService)로 한다 — 평면 ServerDto 쓰기는
                    네트워크 전에 AXIS_SHAPE_REQUIRED 로 막힌다(설계, D-22). 그 차단도 회귀로 확인한다.
                  ⚠ 시드 서버 id(1 등)에 기대지 않는다 — 시험이 서버를 만들고 finally 에서 지운다.
                  테스트 간 병렬 실행 방지를 위해 [Collection] 사용
****************************************************************************/

[Collection("ServerApiTests")]
[Trait("Scenario", "DEP-203")]
public class ServerApiServiceTests
{
    private readonly ILogService _logService;

    public ServerApiServiceTests()
    {
        _logService = new LogService();
    }

    // 어셈블리 공용 로그인 세션의 서비스를 쓴다 — 시험마다 컨테이너를 만들거나 로그인하지 않는다.
    private static Task<IServerApiService> CreateServiceAsync()
        => LiveApiSession.ResolveAsync<IServerApiService>();

    private const string Prefix = ServerTestKit.ServicePrefix;

    /// <summary>잔재 정리 후 이 시험 전용 유형의 카테고리를 만든다.</summary>
    private static Task<int> CreateOwnCategoryAsync(IServerApiService service, string typeServer, string stem)
        => ServerTestKit.CreateCategoryAsync(service, typeServer, $"{Prefix}{stem}", Prefix);

    #region - Server Category API Tests (§8.2) -

    [LiveApiFact(DisplayName = "A59.1: GetCategoriesAsync — 카테고리 목록 조회")]
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

    [LiveApiWriteFact(DisplayName = "A59.2: CreateCategoryAsync — 카테고리 생성")]
    public async Task CreateCategoryAsync_ShouldReturnCreatedCategory()
    {
        // Arrange
        var service = await CreateServiceAsync();
        await ServerTestKit.PurgeLeftoverCategoryAsync(service, "AI_TRAINING", Prefix);
        var dto = new CategoryDto
        {
            Name = $"{Prefix}테스트_카테고리",
            TypeServer = "AI_TRAINING",
            Description = "TDD 테스트용 카테고리",
            SortOrder = 99
        };

        // Act
        var response = await service.CreateCategoryAsync(dto);

        try
        {
            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success, $"API 호출 실패: {response.Message}");
            Assert.NotNull(response.Data);
            Assert.True(response.Data.Id > 0, "생성된 Category ID가 0보다 커야 함");
            Assert.Equal($"{Prefix}테스트_카테고리", response.Data.Name);
            // 7.0+ 응답의 판별자 category_server 도 TypeServer 로 읽힌다(CategoryDto.CategoryServer).
            Assert.Equal("AI_TRAINING", response.Data.TypeServer);

            _logService.Info($"[Test] Category created: Id={response.Data.Id}, Name={response.Data.Name}");
        }
        finally
        {
            // Cleanup
            if (response?.Data?.Id > 0) await service.DeleteCategoryAsync(response.Data.Id);
        }
    }

    [LiveApiWriteFact(DisplayName = "A59.3: GetCategoryByIdAsync — 카테고리 상세 조회 (서버 목록 포함)")]
    public async Task GetCategoryByIdAsync_ShouldReturnCategoryWithServers()
    {
        // Arrange
        var service = await CreateServiceAsync();
        await ServerTestKit.PurgeLeftoverCategoryAsync(service, "MEDIA", Prefix);
        var createResponse = await service.CreateCategoryAsync(new CategoryDto
        {
            Name = $"{Prefix}상세조회_테스트",
            TypeServer = "MEDIA",
            Description = "GetById 테스트",
            SortOrder = 98
        });
        Assert.True(createResponse.Success, $"선행 Create 실패: {createResponse.Message}");
        var createdId = createResponse.Data!.Id;

        try
        {
            // Act
            var response = await service.GetCategoryByIdAsync(createdId);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success, $"API 호출 실패: {response.Message}");
            Assert.NotNull(response.Data);
            Assert.Equal(createdId, response.Data.Id);
            Assert.Equal($"{Prefix}상세조회_테스트", response.Data.Name);
            Assert.Equal("MEDIA", response.Data.TypeServer);

            _logService.Info($"[Test] Category detail: Id={response.Data.Id}, Servers={response.Data.Servers?.Count ?? 0}");
        }
        finally
        {
            // Cleanup
            await service.DeleteCategoryAsync(createdId);
        }
    }

    [LiveApiWriteFact(DisplayName = "A59.4: PatchCategoryAsync — 카테고리 부분 수정")]
    public async Task PatchCategoryAsync_ShouldUpdatePartialFields()
    {
        // Arrange
        var service = await CreateServiceAsync();
        await ServerTestKit.PurgeLeftoverCategoryAsync(service, "RECORDING", Prefix);
        var createResponse = await service.CreateCategoryAsync(new CategoryDto
        {
            Name = $"{Prefix}패치_테스트",
            TypeServer = "RECORDING",
            Description = "원본 설명",
            SortOrder = 97
        });
        Assert.True(createResponse.Success, $"선행 Create 실패: {createResponse.Message}");
        var createdId = createResponse.Data!.Id;

        try
        {
            // Act — 설명만 보낸다(PATCH 본문은 description 한 키)
            var patchDto = new CategoryDto { Description = "수정된 설명" };
            var response = await service.PatchCategoryAsync(createdId, patchDto);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success, $"API 호출 실패: {response.Message}");

            // GET으로 확인 — 보내지 않은 필드는 그대로
            var getResponse = await service.GetCategoryByIdAsync(createdId);
            Assert.True(getResponse.Success);
            Assert.Equal("수정된 설명", getResponse.Data!.Description);
            Assert.Equal($"{Prefix}패치_테스트", getResponse.Data.Name);
            Assert.Equal(97, getResponse.Data.SortOrder);

            _logService.Info($"[Test] Category patched: Description={getResponse.Data.Description}");
        }
        finally
        {
            // Cleanup
            await service.DeleteCategoryAsync(createdId);
        }
    }

    [LiveApiWriteFact(DisplayName = "A59.5: UpdateCategoryAsync — 카테고리 전체 교체")]
    public async Task UpdateCategoryAsync_ShouldReplaceAllFields()
    {
        // Arrange
        var service = await CreateServiceAsync();
        await ServerTestKit.PurgeLeftoverCategoryAsync(service, "PLAYBACK", Prefix);
        var createResponse = await service.CreateCategoryAsync(new CategoryDto
        {
            Name = $"{Prefix}교체_테스트",
            TypeServer = "PLAYBACK",
            Description = "원본",
            SortOrder = 96
        });
        Assert.True(createResponse.Success, $"선행 Create 실패: {createResponse.Message}");
        var createdId = createResponse.Data!.Id;

        try
        {
            // Act — PUT은 같은 유형 유지 (UNIQUE · 유형 불변)
            var updateDto = new CategoryDto
            {
                Name = $"{Prefix}교체_완료",
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
            Assert.Equal($"{Prefix}교체_완료", getResponse.Data!.Name);
            Assert.Equal("전체 교체됨", getResponse.Data.Description);
            Assert.Equal(50, getResponse.Data.SortOrder);

            _logService.Info($"[Test] Category updated: Name={getResponse.Data.Name}");
        }
        finally
        {
            // Cleanup
            await service.DeleteCategoryAsync(createdId);
        }
    }

    [LiveApiWriteFact(DisplayName = "A59.6: DeleteCategoryAsync — 카테고리 삭제")]
    public async Task DeleteCategoryAsync_ShouldReturnSuccess()
    {
        // Arrange
        var service = await CreateServiceAsync();
        await ServerTestKit.PurgeLeftoverCategoryAsync(service, "STORAGE", Prefix);
        var createResponse = await service.CreateCategoryAsync(new CategoryDto
        {
            Name = $"{Prefix}삭제_테스트",
            TypeServer = "STORAGE",
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

        var verify = await service.GetCategoryByIdAsync(createdId);
        Assert.True(!verify.Success || verify.Data == null);

        _logService.Info($"[Test] Category deleted: Id={createdId}");
    }

    [LiveApiWriteFact(DisplayName = "A59.7: Category CRUD Lifecycle — Create→Read→Patch→Update→Delete")]
    public async Task CategoryCrudLifecycle_CreateReadUpdateDelete()
    {
        var service = await CreateServiceAsync();
        await ServerTestKit.PurgeLeftoverCategoryAsync(service, "ANALYTICS", Prefix);

        // 1. Create
        var createResponse = await service.CreateCategoryAsync(new CategoryDto
        {
            Name = $"{Prefix}라이프사이클_테스트",
            TypeServer = "ANALYTICS",
            Description = "CRUD 전체 흐름",
            SortOrder = 90
        });
        Assert.True(createResponse.Success, $"Create 실패: {createResponse.Message}");
        var id = createResponse.Data!.Id;
        Assert.True(id > 0);
        var deleted = false;

        try
        {
            // 2. Read (GetById)
            var getResponse = await service.GetCategoryByIdAsync(id);
            Assert.True(getResponse.Success, $"GetById 실패: {getResponse.Message}");
            Assert.Equal($"{Prefix}라이프사이클_테스트", getResponse.Data!.Name);

            // 3. Patch
            var patchResponse = await service.PatchCategoryAsync(id, new CategoryDto { Description = "패치됨" });
            Assert.True(patchResponse.Success, $"Patch 실패: {patchResponse.Message}");

            // 4. Update (PUT) — 같은 유형 유지 (UNIQUE · 유형 불변)
            var updateResponse = await service.UpdateCategoryAsync(id, new CategoryDto
            {
                Name = $"{Prefix}라이프사이클_완료",
                TypeServer = "ANALYTICS",
                Description = "PUT 교체됨",
                SortOrder = 91
            });
            Assert.True(updateResponse.Success, $"Update 실패: {updateResponse.Message}");

            // 5. Verify final state
            var verifyResponse = await service.GetCategoryByIdAsync(id);
            Assert.True(verifyResponse.Success);
            Assert.Equal($"{Prefix}라이프사이클_완료", verifyResponse.Data!.Name);
            Assert.Equal("PUT 교체됨", verifyResponse.Data.Description);

            // 6. Delete
            var deleteResponse = await service.DeleteCategoryAsync(id);
            Assert.True(deleteResponse.Success, $"Delete 실패: {deleteResponse.Message}");
            deleted = true;
        }
        finally
        {
            if (!deleted) await service.DeleteCategoryAsync(id);
        }

        _logService.Info("[Test] Category CRUD lifecycle completed successfully");
    }

    #endregion

    #region - Server Instance API Tests (§8.3) -

    [LiveApiFact(DisplayName = "A60.1: GetServersAsync — 서버 목록 조회")]
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

    /// <summary>
    /// 축 계약(7.0+)에서 레거시 평면 쓰기가 네트워크 전에 막히는지 — 설계 회귀 확인(D-22).
    /// </summary>
    private static void AssertFlatWriteBlocked(IServerAxisApiService axis, ApiResponse<ServerDto> flat)
    {
        if (!ServerTestKit.IsAxisEra(axis)) return;
        Assert.False(flat.Success);
        Assert.Equal("AXIS_SHAPE_REQUIRED", flat.Error?.Code);
    }

    [LiveApiWriteFact(DisplayName = "A60.2: CreateServerAsync — 서버 생성")]
    public async Task CreateServerAsync_ShouldReturnCreatedServer()
    {
        // Arrange — 서버는 그 유형의 카테고리 행이 있어야 등록된다(servers.py:198-208)
        const string type = "AI_INFERENCE";
        var service = await CreateServiceAsync();
        var axis = await ServerTestKit.AxisAsync();
        var categoryId = await CreateOwnCategoryAsync(service, type, "서버생성_카테고리");
        var name = ServerTestKit.UniqueName($"{Prefix}테스트_서버");
        var serverId = 0;

        try
        {
            // 레거시 평면 등록은 축 계약에서 막힌다(회귀 확인)
            AssertFlatWriteBlocked(axis, await service.CreateServerAsync(new ServerDto
            {
                CategoryId = categoryId,
                Name = name,
                IpAddress = "10.0.0.99",
                Port = 9999,
                Hostname = "test-server"
            }));

            // Act — 축 통로
            var created = await axis.CreateServerAsync(new ServerWriteIntent
            {
                TypeServer = type,
                CategoryId = categoryId,
                Name = name,
                IpAddress = "10.0.0.99",
                Port = 9999,
                Hostname = "test-server"
            });

            // Assert
            Assert.True(created.IsSuccess, $"API 호출 실패: {created.Message}");
            Assert.True(created.Id > 0, "생성된 Server ID가 0보다 커야 함");
            serverId = created.Id;

            var response = await service.GetServerByIdAsync(serverId);
            Assert.True(response.Success, $"GetById 실패: {response.Message}");
            Assert.NotNull(response.Data);
            Assert.Equal(name, response.Data.Name);
            Assert.Equal("10.0.0.99", response.Data.IpAddress);
            Assert.Equal(9999, response.Data.Port);
            Assert.Equal("test-server", response.Data.Hostname);

            _logService.Info($"[Test] Server created: Id={serverId}, Name={response.Data.Name}");
        }
        finally
        {
            // Cleanup
            await ServerTestKit.DeleteServerThenCategoryAsync(service, serverId, categoryId);
        }
    }

    [LiveApiWriteFact(DisplayName = "A60.3: GetServerByIdAsync — 서버 상세 조회")]
    public async Task GetServerByIdAsync_ShouldReturnSingleServer()
    {
        // Arrange
        const string type = "PIDS_API";
        var service = await CreateServiceAsync();
        var axis = await ServerTestKit.AxisAsync();
        var categoryId = await CreateOwnCategoryAsync(service, type, "상세조회_카테고리");
        var name = ServerTestKit.UniqueName($"{Prefix}상세조회_서버");
        var createdId = 0;

        try
        {
            createdId = await ServerTestKit.CreateServerAsync(axis, type, categoryId, name, "10.0.0.98", 9998, "detail-server");

            // Act
            var response = await service.GetServerByIdAsync(createdId);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success, $"API 호출 실패: {response.Message}");
            Assert.NotNull(response.Data);
            Assert.Equal(createdId, response.Data.Id);
            Assert.Equal(name, response.Data.Name);
            Assert.Equal("10.0.0.98", response.Data.IpAddress);
            Assert.Equal(9998, response.Data.Port);
            if (ServerTestKit.IsAxisEra(axis)) Assert.Equal(type, response.Data.CategoryServer);

            _logService.Info($"[Test] Server detail: Id={response.Data.Id}, Name={response.Data.Name}");
        }
        finally
        {
            // Cleanup
            await ServerTestKit.DeleteServerThenCategoryAsync(service, createdId, categoryId);
        }
    }

    [LiveApiWriteFact(DisplayName = "A60.4: PatchServerAsync — 서버 부분 수정")]
    public async Task PatchServerAsync_ShouldUpdatePartialFields()
    {
        // Arrange
        const string type = "WEB";
        var service = await CreateServiceAsync();
        var axis = await ServerTestKit.AxisAsync();
        var categoryId = await CreateOwnCategoryAsync(service, type, "패치_카테고리");
        var name = ServerTestKit.UniqueName($"{Prefix}패치_서버");
        var createdId = 0;

        try
        {
            createdId = await ServerTestKit.CreateServerAsync(axis, type, categoryId, name, "10.0.0.97", 9997);

            // 레거시 평면 PATCH 는 축 계약에서 막힌다(회귀 확인)
            AssertFlatWriteBlocked(axis, await service.PatchServerAsync(createdId, new ServerDto { Port = 9000 }));

            // Act — 포트만 보낸다(보낸 키만 바뀐다)
            var response = await axis.PatchServerAsync(createdId, new ServerWriteIntent { Port = 9000 });

            // Assert
            Assert.True(response.IsSuccess, $"API 호출 실패: {response.Message}");

            var getResponse = await service.GetServerByIdAsync(createdId);
            Assert.True(getResponse.Success);
            Assert.Equal(9000, getResponse.Data!.Port);
            Assert.Equal("10.0.0.97", getResponse.Data.IpAddress);
            Assert.Equal(name, getResponse.Data.Name);

            // 상태는 관측 필드다 — 7.0+ 는 PATCH /servers/{id}/status(servers:control)가 유일한 입구(servers.py:699-782).
            if (ServerTestKit.IsAxisEra(axis))
            {
                var report = await service.ReportServerStatusAsync(createdId, "WARNING");
                Assert.True(report.Success, $"상태 보고 실패: {report.Message} {report.Error?.Message}");

                var afterReport = await service.GetServerByIdAsync(createdId);
                Assert.True(afterReport.Success);
                Assert.Equal("WARNING", afterReport.Data!.Status);
            }

            _logService.Info($"[Test] Server patched: Port={getResponse.Data.Port}");
        }
        finally
        {
            // Cleanup
            await ServerTestKit.DeleteServerThenCategoryAsync(service, createdId, categoryId);
        }
    }

    [LiveApiWriteFact(DisplayName = "A60.5: UpdateServerAsync — 서버 전체 교체")]
    public async Task UpdateServerAsync_ShouldReplaceAllFields()
    {
        // Arrange
        const string type = "AUTH";
        var service = await CreateServiceAsync();
        var axis = await ServerTestKit.AxisAsync();
        var categoryId = await CreateOwnCategoryAsync(service, type, "교체_카테고리");
        var createdId = 0;

        try
        {
            createdId = await ServerTestKit.CreateServerAsync(
                axis, type, categoryId, ServerTestKit.UniqueName($"{Prefix}교체_서버"), "10.0.0.96", 9996);

            var replacedName = ServerTestKit.UniqueName($"{Prefix}교체_완료_서버");

            // 레거시 평면 PUT 은 축 계약에서 막힌다(회귀 확인)
            AssertFlatWriteBlocked(axis, await service.UpdateServerAsync(createdId, new ServerDto
            {
                CategoryId = categoryId,
                Name = replacedName,
                IpAddress = "10.0.0.196",
                Port = 8888,
                Hostname = "updated-server"
            }));

            // Act — 축 통로로 이름 · 접속 전부를 바꾼다
            var response = await axis.PatchServerAsync(createdId, new ServerWriteIntent
            {
                Name = replacedName,
                IpAddress = "10.0.0.196",
                Port = 8888,
                Hostname = "updated-server"
            });

            // Assert
            Assert.True(response.IsSuccess, $"API 호출 실패: {response.Message}");

            var getResponse = await service.GetServerByIdAsync(createdId);
            Assert.True(getResponse.Success);
            Assert.Equal(replacedName, getResponse.Data!.Name);
            Assert.Equal("10.0.0.196", getResponse.Data.IpAddress);
            Assert.Equal(8888, getResponse.Data.Port);
            Assert.Equal("updated-server", getResponse.Data.Hostname);

            _logService.Info($"[Test] Server updated: Name={getResponse.Data.Name}");
        }
        finally
        {
            // Cleanup
            await ServerTestKit.DeleteServerThenCategoryAsync(service, createdId, categoryId);
        }
    }

    [LiveApiWriteFact(DisplayName = "A60.6: DeleteServerAsync — 서버 삭제")]
    public async Task DeleteServerAsync_ShouldReturnSuccess()
    {
        // Arrange
        const string type = "GATEWAY";
        var service = await CreateServiceAsync();
        var axis = await ServerTestKit.AxisAsync();
        var categoryId = await CreateOwnCategoryAsync(service, type, "삭제_카테고리");
        var createdId = 0;
        var deleted = false;

        try
        {
            createdId = await ServerTestKit.CreateServerAsync(
                axis, type, categoryId, ServerTestKit.UniqueName($"{Prefix}삭제_서버"), "10.0.0.95", 9995);

            // Act
            var response = await service.DeleteServerAsync(createdId);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success, $"API 호출 실패: {response.Message}");
            deleted = true;

            var verify = await service.GetServerByIdAsync(createdId);
            Assert.True(!verify.Success || verify.Data == null);

            _logService.Info($"[Test] Server deleted: Id={createdId}");
        }
        finally
        {
            // Cleanup — 서버가 남았으면 서버부터
            await ServerTestKit.DeleteServerThenCategoryAsync(service, deleted ? 0 : createdId, categoryId);
        }
    }

    [LiveApiWriteFact(DisplayName = "A60.7: Server CRUD Lifecycle — Create→Read→Patch→Update→Delete")]
    public async Task ServerCrudLifecycle_CreateReadUpdateDelete()
    {
        const string type = "AI_INFERENCE";
        var service = await CreateServiceAsync();
        var axis = await ServerTestKit.AxisAsync();
        var isAxisEra = ServerTestKit.IsAxisEra(axis);
        var categoryId = await CreateOwnCategoryAsync(service, type, "라이프사이클_카테고리");
        var name = ServerTestKit.UniqueName($"{Prefix}라이프사이클_서버");
        var id = 0;
        var deleted = false;

        try
        {
            // 1. Create
            id = await ServerTestKit.CreateServerAsync(axis, type, categoryId, name, "10.0.0.94", 9994, "lifecycle-server");
            Assert.True(id > 0);

            // 2. Read (GetById)
            var getResponse = await service.GetServerByIdAsync(id);
            Assert.True(getResponse.Success, $"GetById 실패: {getResponse.Message}");
            Assert.Equal(name, getResponse.Data!.Name);
            if (isAxisEra) Assert.Equal("UNKNOWN", getResponse.Data.Status);   // 보고 전 관측 상태(servers.py:523)

            // 3. Patch — 상태는 관측 보고 입구로(7.0+). 평면 PATCH 는 막힌다.
            AssertFlatWriteBlocked(axis, await service.PatchServerAsync(id, new ServerDto { Status = "ERROR" }));
            if (isAxisEra)
            {
                var patchResponse = await service.ReportServerStatusAsync(id, "ERROR");
                Assert.True(patchResponse.Success, $"Patch 실패: {patchResponse.Message} {patchResponse.Error?.Message}");
                Assert.Equal("ERROR", patchResponse.Data?.Status);
            }

            // 4. Update — 축 통로로 이름 · 접속 교체
            var finalName = ServerTestKit.UniqueName($"{Prefix}라이프사이클_완료");
            var updateResponse = await axis.PatchServerAsync(id, new ServerWriteIntent
            {
                Name = finalName,
                IpAddress = "10.0.0.194",
                Port = 7777
            });
            Assert.True(updateResponse.IsSuccess, $"Update 실패: {updateResponse.Message}");

            // 5. Verify final state
            var verifyResponse = await service.GetServerByIdAsync(id);
            Assert.True(verifyResponse.Success);
            Assert.Equal(finalName, verifyResponse.Data!.Name);
            Assert.Equal(7777, verifyResponse.Data.Port);
            Assert.Equal("10.0.0.194", verifyResponse.Data.IpAddress);
            if (isAxisEra) Assert.Equal("ERROR", verifyResponse.Data.Status);

            // 6. Delete
            var deleteResponse = await service.DeleteServerAsync(id);
            Assert.True(deleteResponse.Success, $"Delete 실패: {deleteResponse.Message}");
            deleted = true;
        }
        finally
        {
            await ServerTestKit.DeleteServerThenCategoryAsync(service, deleted ? 0 : id, categoryId);
        }

        _logService.Info("[Test] Server CRUD lifecycle completed successfully");
    }

    #endregion

    #region - Server Metrics API Tests (§8.6) -

    /// <summary>
    /// 계측 시험용 자기 서버 — 카테고리 + 서버를 만들고 본문을 돌린 뒤 서버(계측 CASCADE) → 카테고리 순으로 지운다.
    /// </summary>
    private static async Task WithOwnServerAsync(string type, string stem, Func<IServerApiService, int, Task> body)
    {
        var service = await CreateServiceAsync();
        var axis = await ServerTestKit.AxisAsync();
        var categoryId = await CreateOwnCategoryAsync(service, type, $"{stem}_카테고리");
        var serverId = 0;
        try
        {
            serverId = await ServerTestKit.CreateServerAsync(
                axis, type, categoryId, ServerTestKit.UniqueName($"{Prefix}{stem}_서버"), "10.0.0.90", 9990);
            await body(service, serverId);
        }
        finally
        {
            await ServerTestKit.DeleteServerThenCategoryAsync(service, serverId, categoryId);
        }
    }

    [LiveApiWriteFact(DisplayName = "A61.1: CreateServerMetricAsync — 메트릭 기록")]
    public Task CreateServerMetricAsync_ShouldRecordMetric()
        => WithOwnServerAsync("PIDS_API", "메트릭기록", async (service, serverId) =>
        {
            // Arrange
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

            // Act — 시험이 만든 서버에 기록한다(시드 서버 id 에 기대지 않는다)
            var response = await service.CreateServerMetricAsync(serverId, dto);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success, $"API 호출 실패: {response.Message} {response.Error?.Message}");
            Assert.NotNull(response.Data);
            Assert.True(response.Data.Id > 0);
            Assert.Equal(serverId, response.Data.ServerId);
            Assert.Equal(55.5, response.Data.CpuUsage);
            Assert.Equal(22.4, response.Data.RamUsedGb);
            Assert.Equal(200, response.Data.ProcessCount);

            _logService.Info($"[Test] Metric created: Id={response.Data.Id}, CPU={response.Data.CpuUsage}");
        });

    [LiveApiWriteFact(DisplayName = "A61.2: GetServerMetricsAsync — 메트릭 이력 조회")]
    public Task GetServerMetricsAsync_ShouldReturnMetricHistory()
        => WithOwnServerAsync("WEB", "메트릭이력", async (service, serverId) =>
        {
            // Arrange — 이력에 나올 계측 1건
            var created = await service.CreateServerMetricAsync(serverId, new ServerMetricDto { CpuUsage = 12.5, ProcessCount = 7 });
            Assert.True(created.Success, $"선행 계측 기록 실패: {created.Message} {created.Error?.Message}");

            // Act
            var response = await service.GetServerMetricsAsync(serverId);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success, $"API 호출 실패: {response.Message}");
            Assert.NotNull(response.Data);
            var row = Assert.Single(response.Data);
            Assert.Equal(serverId, row.ServerId);
            Assert.Equal(12.5, row.CpuUsage);
            Assert.Equal(7, row.ProcessCount);

            _logService.Info($"[Test] Metrics history count: {response.Data.Count}");
        });

    [LiveApiWriteFact(DisplayName = "A61.3: GetServerMetricLatestAsync — 최신 메트릭 조회")]
    public Task GetServerMetricLatestAsync_ShouldReturnLatest()
        => WithOwnServerAsync("AUTH", "최신메트릭", async (service, serverId) =>
        {
            // Arrange — 두 건 기록, 나중 것이 최신
            var first = await service.CreateServerMetricAsync(serverId, new ServerMetricDto { CpuUsage = 10.0 });
            Assert.True(first.Success, $"선행 계측 기록 실패: {first.Message} {first.Error?.Message}");
            var second = await service.CreateServerMetricAsync(serverId, new ServerMetricDto { CpuUsage = 66.6 });
            Assert.True(second.Success, $"선행 계측 기록 실패: {second.Message} {second.Error?.Message}");

            // Act
            var response = await service.GetServerMetricLatestAsync(serverId);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success, $"API 호출 실패: {response.Message}");
            Assert.NotNull(response.Data);
            Assert.Equal(serverId, response.Data.ServerId);
            Assert.NotNull(response.Data.LatestMetrics);
            Assert.Equal(second.Data!.Id, response.Data.LatestMetrics!.Id);
            Assert.Equal(66.6, response.Data.LatestMetrics.CpuUsage);

            _logService.Info($"[Test] Latest metric: CPU={response.Data.LatestMetrics.CpuUsage}");
        });

    [LiveApiWriteFact(DisplayName = "A61.4: DeleteServerMetricsAsync — 메트릭 삭제")]
    public Task DeleteServerMetricsAsync_ShouldReturnDeletedCount()
        => WithOwnServerAsync("GATEWAY", "메트릭삭제", async (service, serverId) =>
        {
            // Arrange — 먼저 메트릭 1건 생성
            var created = await service.CreateServerMetricAsync(serverId, new ServerMetricDto
            {
                CpuUsage = 10.0,
                RamUsage = 20.0,
                ProcessCount = 50
            });
            Assert.True(created.Success, $"선행 계측 기록 실패: {created.Message} {created.Error?.Message}");

            // Act — 보존기간 1일: 방금 기록한 계측은 대상이 아니다(삭제 0건이어도 성공).
            //       서버 계약은 older_than_days(≥1)뿐이다(server_metrics.py:301) — before_date 는 어느 판본에도 없다.
            var response = await service.DeleteServerMetricsAsync(serverId, olderThanDays: 1);

            // Assert — data 는 늘 null 이고 건수는 message 에만 있어 라이브러리가 Data 를 합성한다.
            Assert.NotNull(response);
            Assert.True(response.Success, $"API 호출 실패: {response.Message}");
            Assert.NotNull(response.Data);
            Assert.Equal(serverId, response.Data.ServerId);
            Assert.Equal(0, response.Data.DeletedCount);

            var remaining = await service.GetServerMetricsAsync(serverId);
            Assert.True(remaining.Success);
            Assert.Single(remaining.Data!);

            _logService.Info($"[Test] Metrics deleted: {response.Data.DeletedCount}");
        });

    [LiveApiWriteFact(DisplayName = "A61.5: Server Metrics Lifecycle — Record→Query→Latest→Delete")]
    public Task ServerMetricsLifecycle_RecordQueryDelete()
        => WithOwnServerAsync("AI_INFERENCE", "메트릭수명", async (service, serverId) =>
        {
            // 1. Create metric
            var createResponse = await service.CreateServerMetricAsync(serverId, new ServerMetricDto
            {
                CpuUsage = 88.8,
                RamUsage = 77.7,
                DiskUsage = 66.6,
                ProcessCount = 300
            });
            Assert.True(createResponse.Success, $"Create 실패: {createResponse.Message} {createResponse.Error?.Message}");
            Assert.Equal(serverId, createResponse.Data!.ServerId);

            // 2. Get history
            var historyResponse = await service.GetServerMetricsAsync(serverId);
            Assert.True(historyResponse.Success, $"GetHistory 실패: {historyResponse.Message}");
            Assert.True(historyResponse.Data!.Count >= 1);
            Assert.Contains(historyResponse.Data, m => m.Id == createResponse.Data.Id && m.CpuUsage == 88.8);

            // 3. Get latest
            var latestResponse = await service.GetServerMetricLatestAsync(serverId);
            Assert.True(latestResponse.Success, $"GetLatest 실패: {latestResponse.Message}");
            Assert.NotNull(latestResponse.Data!.LatestMetrics);
            Assert.Equal(createResponse.Data.Id, latestResponse.Data.LatestMetrics!.Id);

            // 4. Delete (보존기간 1일 → 삭제 0건이어도 성공)
            var deleteResponse = await service.DeleteServerMetricsAsync(serverId, olderThanDays: 1);
            Assert.True(deleteResponse.Success, $"Delete 실패: {deleteResponse.Message}");
            Assert.NotNull(deleteResponse.Data);

            _logService.Info("[Test] Server Metrics lifecycle completed successfully");
        });

    #endregion

    #region - Proxy Settings API Tests (§8.8) -

    [LiveApiFact(DisplayName = "5.1: GetProxySettingsAsync — 프록시 설정 조회 (server_id=1)")]
    public async Task GetProxySettingsAsync_ShouldReturnProxySetting()
    {
        // Arrange
        var service = await CreateServiceAsync();
        var axis = await ServerTestKit.AxisAsync();

        if (!ServerTestKit.IsAxisEra(axis))
        {
            // 6.3 계약 — 옛 경로가 정상 동작한다(시드 서버 id=1).
            var legacy = await service.GetProxySettingsAsync(1);
            Assert.True(legacy.Success, $"API 호출 실패: {legacy.Message}");
            Assert.NotNull(legacy.Data);
            Assert.Equal(1, legacy.Data.ServerId);
            Assert.False(string.IsNullOrEmpty(legacy.Data.WindyMode), "WindyMode 값이 비어있음");
            Assert.False(string.IsNullOrEmpty(legacy.Data.OperationMode), "OperationMode 값이 비어있음");
            return;
        }

        // 7.0+ — proxy-settings 는 410 묘비다(servers.py:14 · ServerApiService.GetProxySettingsAsync).
        //        네트워크에 나가기 전에 ENDPOINT_REMOVED 로 막고 대체 경로(server_config.modes)를 안내해야 한다.
        var removed = await service.GetProxySettingsAsync(1);
        Assert.False(removed.Success);
        Assert.Equal("ENDPOINT_REMOVED", removed.Error?.Code);
        Assert.Contains("server_config.modes", removed.Message);

        // 대체 경로 확인 — PROXY 서버의 server_config.modes. 시드 PROXY 서버(PROXY-ab0001)는 modes 를 갖고 시작한다
        // (app/utils/init_server_data.py:91-95). 새로 등록한 PROXY 서버는 modes 를 보내지 않으면 server_config 가
        // {"schema":1} 뿐이라(servers.py:270-277) modes 가 없다 — 그래서 modes 를 가진 PROXY 서버를 찾고,
        // 없으면(쓰기 허용 주소일 때만) 이 시험이 modes 를 실어 등록했다가 지운다. PROXY 카테고리(시드)는 건드리지 않는다.
        var list = await axis.GetServersAsync(unitId: null, includeDescendants: false);
        Assert.True(list.IsSuccess, $"서버 목록 실패: {list.Message}");
        var proxy = list.Servers.FirstOrDefault(s => s.TypeServer == "PROXY" && s.Modes is not null);

        var createdId = 0;
        try
        {
            if (proxy is null)
            {
                Assert.True(LiveApiTarget.WriteSkipReason is null,
                    $"modes 를 가진 PROXY 서버가 없고 쓰기도 허용되지 않아 대체 경로를 확인할 수 없습니다: {LiveApiTarget.WriteSkipReason}");
                Assert.Contains(await axis.GetCategoriesAsync(), c => c.TypeServer == "PROXY");

                var created = await axis.CreateServerAsync(new ServerWriteIntent
                {
                    TypeServer = "PROXY",
                    Name = ServerTestKit.UniqueName($"{Prefix}프록시_서버"),
                    IpAddress = "10.0.0.93",
                    Port = 8100,
                    OperationMode = "NORMAL",
                    WindyMode = "wind1"
                });
                Assert.True(created.IsSuccess, $"PROXY 서버 등록 실패: {created.Message}");
                createdId = created.Id;
            }

            // Act — 단건(view=full · include=server_config)으로 읽는다
            var server = await axis.GetServerAsync(proxy?.Id ?? createdId);

            // Assert
            Assert.NotNull(server);
            Assert.Equal("PROXY", server!.TypeServer);
            Assert.True(server.HasConfigSection);
            Assert.NotNull(server.Modes);
            var operationMode = server.Modes!.Value<string>("operation_mode");
            var windyMode = server.Modes.Value<string>("windy_mode");
            Assert.Contains(operationMode, new[] { "NORMAL", "REGISTER" });
            Assert.Contains(windyMode, new[] { "wind0", "wind1", "wind2", "wind3" });   // 소문자(대문자는 422)
            if (createdId > 0)
            {
                Assert.Equal("NORMAL", operationMode);
                Assert.Equal("wind1", windyMode);
            }

            _logService.Info($"[Test] Proxy modes: ServerId={server.Id}, WindyMode={windyMode}, OperationMode={operationMode}");
        }
        finally
        {
            // 이 시험이 만든 서버만 지운다(PROXY 카테고리는 시드라 남긴다).
            if (createdId > 0) await service.DeleteServerAsync(createdId);
        }
    }

    #endregion
}
