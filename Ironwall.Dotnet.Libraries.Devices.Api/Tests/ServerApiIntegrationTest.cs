using Autofac;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Modules;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Tests;
/****************************************************************************
   Purpose      : Server API Integration Tests
   Created By   : Claude
   Created On   : 2026-02-24
   Department   : SW Team
   Company      : Sensorway Co., Ltd.

   Description  : ServerCategory + ServerInstance + ServerMetrics + EnclosureMetrics 통합 테스트
                  xUnit IAsyncLifetime Fixture 패턴 사용
                  GOP API 서버 (localhost:8000) 연동 필수
****************************************************************************/

#region - Fixture & Collection -

public sealed class ServerApiFixture : IAsyncLifetime
{
    internal IServerApiService ServerService { get; private set; } = null!;
    internal IDeviceApiService DeviceService { get; private set; } = null!;
    internal ILogService Log { get; } = new LogService();
    private IContainer? _container;

    private readonly ApiSetupModel _setup = new()
    {
        Url = "http://localhost:8000/api",
        Username = "admin",
        Password = "admin123",
        Timeout = 30
    };

    public async Task InitializeAsync()
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule(new DeviceApiModule(Log, _setup, "ServerIntTest"));
        _container = builder.Build();
        ServerService = _container.ResolveNamed<IServerApiService>("ServerIntTest");
        DeviceService = _container.ResolveNamed<IDeviceApiService>("ServerIntTest");
        await ServerService.ExecuteAsync(CancellationToken.None);
        await DeviceService.ExecuteAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await ServerService.StopAsync(CancellationToken.None);
        await DeviceService.StopAsync(CancellationToken.None);
        _container?.Dispose();
    }
}

[CollectionDefinition(nameof(ServerApiCollection))]
public sealed class ServerApiCollection : ICollectionFixture<ServerApiFixture> { }

#endregion

#region - Category API Tests (S01~S03) -

[Collection(nameof(ServerApiCollection))]
[Trait("Category", "Integration")]
public class CategoryApiTests
{
    private readonly IServerApiService _svc;
    private readonly ITestOutputHelper _output;

    public CategoryApiTests(ServerApiFixture fixture, ITestOutputHelper output)
    {
        _svc = fixture.ServerService;
        _output = output;
    }

    [Fact]
    public async Task S01_Category_CRUD_Lifecycle()
    {
        // [1] CREATE — type_server는 서버 enum: ETC 사용
        var createDto = new CategoryDto
        {
            TypeServer = "ETC",
            Name = "MIG_테스트카테고리",
            Description = "마이그레이션 통합 테스트용",
            SortOrder = 99
        };
        var createResp = await _svc.CreateCategoryAsync(createDto);
        _output.WriteLine($"Create: Success={createResp.Success}, Id={createResp.Data?.Id}");
        Assert.True(createResp.Success, $"Category create failed: {createResp.Message}");
        Assert.True(createResp.Data!.Id > 0);
        Assert.Equal("ETC", createResp.Data.TypeServer);
        var createdId = createResp.Data.Id;

        try
        {
            // [2] READ
            var getResp = await _svc.GetCategoryByIdAsync(createdId);
            Assert.True(getResp.Success);
            Assert.NotNull(getResp.Data);
            Assert.Equal("ETC", getResp.Data!.TypeServer);
            Assert.Equal("MIG_테스트카테고리", getResp.Data.Name);

            // [3] PATCH — 기존 데이터 복사 후 수정
            var patchDto = new CategoryDto
            {
                TypeServer = getResp.Data.TypeServer,
                Name = getResp.Data.Name,
                Description = "패치된 설명",
                SortOrder = getResp.Data.SortOrder
            };
            var patchResp = await _svc.PatchCategoryAsync(createdId, patchDto);
            _output.WriteLine($"Patch: Success={patchResp.Success}");
            Assert.True(patchResp.Success);

            var verifyPatch = await _svc.GetCategoryByIdAsync(createdId);
            Assert.Equal("패치된 설명", verifyPatch.Data?.Description);

            // [4] PUT — 전체 교체
            var putDto = new CategoryDto
            {
                TypeServer = "ETC",
                Name = "MIG_교체_카테고리",
                Description = "교체된 설명",
                SortOrder = 50
            };
            var putResp = await _svc.UpdateCategoryAsync(createdId, putDto);
            _output.WriteLine($"Put: Success={putResp.Success}");
            Assert.True(putResp.Success);

            var verifyPut = await _svc.GetCategoryByIdAsync(createdId);
            Assert.Equal("MIG_교체_카테고리", verifyPut.Data?.Name);
        }
        finally
        {
            // [5] DELETE + verify
            var delResp = await _svc.DeleteCategoryAsync(createdId);
            _output.WriteLine($"Delete: Success={delResp.Success}");

            var verifyDel = await _svc.GetCategoryByIdAsync(createdId);
            Assert.True(!verifyDel.Success || verifyDel.Data == null);
        }
    }

    [Fact]
    public async Task S02_GetCategories_Pagination()
    {
        var resp = await _svc.GetCategoriesAsync(page: 1, limit: 5);
        _output.WriteLine($"Pagination: Success={resp.Success}, Total={resp.Pagination?.Total}");
        Assert.True(resp.Success);
        Assert.NotNull(resp.Pagination);
        Assert.True(resp.Pagination!.Total >= 0);
    }

    [Fact]
    public async Task S03_GetCategory_NotFound()
    {
        var resp = await _svc.GetCategoryByIdAsync(999999);
        _output.WriteLine($"NotFound: Success={resp.Success}");
        Assert.True(!resp.Success || resp.Data == null);
    }
}

#endregion

#region - Server Instance API Tests (S04~S08) -

[Collection(nameof(ServerApiCollection))]
[Trait("Category", "Integration")]
public class ServerInstanceApiTests
{
    private readonly IServerApiService _svc;
    private readonly ITestOutputHelper _output;

    public ServerInstanceApiTests(ServerApiFixture fixture, ITestOutputHelper output)
    {
        _svc = fixture.ServerService;
        _output = output;
    }

    [Fact]
    public async Task S04_Server_CRUD_Lifecycle()
    {
        // 선행: Category 생성 — type_server enum: MONITORING
        var catDto = new CategoryDto
        {
            TypeServer = "MONITORING",
            Name = "MIG_서버테스트_카테고리",
            Description = "서버 CRUD 테스트용"
        };
        var catResp = await _svc.CreateCategoryAsync(catDto);
        Assert.True(catResp.Success, $"Category create failed: {catResp.Message}");
        var categoryId = catResp.Data!.Id;

        try
        {
            // [1] CREATE — status: NORMAL | WARNING | ERROR
            var createDto = new ServerDto
            {
                CategoryId = categoryId,
                Name = "MIG_테스트서버",
                Status = "NORMAL",
                IpAddress = "10.99.99.50",
                Port = 8080
            };
            var createResp = await _svc.CreateServerAsync(createDto);
            _output.WriteLine($"Create: Success={createResp.Success}, Id={createResp.Data?.Id}, Msg={createResp.Message}");
            Assert.True(createResp.Success, $"Server create failed: {createResp.Message}");
            Assert.True(createResp.Data!.Id > 0);
            var serverId = createResp.Data.Id;

            try
            {
                // [2] READ
                var getResp = await _svc.GetServerByIdAsync(serverId);
                Assert.True(getResp.Success);
                Assert.Equal("MIG_테스트서버", getResp.Data?.Name);
                Assert.Equal("10.99.99.50", getResp.Data?.IpAddress);

                // [3] PATCH — 기존 데이터 복사 후 수정
                var patchBase = getResp.Data!;
                patchBase.Name = "MIG_수정_서버";
                var patchResp = await _svc.PatchServerAsync(serverId, patchBase);
                _output.WriteLine($"Patch: Success={patchResp.Success}");
                Assert.True(patchResp.Success);

                var verifyPatch = await _svc.GetServerByIdAsync(serverId);
                Assert.Equal("MIG_수정_서버", verifyPatch.Data?.Name);

                // [4] PUT — 전체 교체
                var putDto = new ServerDto
                {
                    CategoryId = categoryId,
                    Name = "MIG_교체_서버",
                    Status = "ERROR",
                    IpAddress = "10.99.99.51",
                    Port = 9090
                };
                var putResp = await _svc.UpdateServerAsync(serverId, putDto);
                _output.WriteLine($"Put: Success={putResp.Success}");
                Assert.True(putResp.Success);

                var verifyPut = await _svc.GetServerByIdAsync(serverId);
                Assert.Equal("MIG_교체_서버", verifyPut.Data?.Name);
                Assert.Equal("10.99.99.51", verifyPut.Data?.IpAddress);
            }
            finally
            {
                // [5] DELETE Server
                await _svc.DeleteServerAsync(serverId);
                var verifyDel = await _svc.GetServerByIdAsync(serverId);
                Assert.True(!verifyDel.Success || verifyDel.Data == null);
            }
        }
        finally
        {
            // 후행: Category 삭제
            await _svc.DeleteCategoryAsync(categoryId);
        }
    }

    [Fact]
    public async Task S05_GetServers_Pagination()
    {
        var resp = await _svc.GetServersAsync(page: 1, limit: 5);
        _output.WriteLine($"Pagination: Success={resp.Success}, Total={resp.Pagination?.Total}");
        Assert.True(resp.Success);
        Assert.NotNull(resp.Pagination);
    }

    [Fact]
    public async Task S06_GetServers_CategoryFilter()
    {
        // 기존 데이터에서 첫 번째 카테고리 찾기
        var cats = await _svc.GetCategoriesAsync(page: 1, limit: 1);
        if (cats.Data == null || cats.Data.Count == 0)
        {
            _output.WriteLine("No categories found, skipping category filter test");
            return;
        }
        var catId = cats.Data[0].Id;

        var resp = await _svc.GetServersAsync(categoryId: catId, page: 1, limit: 100);
        _output.WriteLine($"CategoryFilter: Success={resp.Success}, Count={resp.Data?.Count}");
        Assert.True(resp.Success);
    }

    [Fact]
    public async Task S07_GetServers_StatusFilter()
    {
        var resp = await _svc.GetServersAsync(status: "NORMAL", page: 1, limit: 100);
        _output.WriteLine($"StatusFilter: Success={resp.Success}, Count={resp.Data?.Count}");
        Assert.True(resp.Success);
    }

    [Fact]
    public async Task S08_GetServer_NotFound()
    {
        var resp = await _svc.GetServerByIdAsync(999999);
        _output.WriteLine($"NotFound: Success={resp.Success}");
        Assert.True(!resp.Success || resp.Data == null);
    }
}

#endregion

#region - Server Metrics API Tests (S09~S11) -

[Collection(nameof(ServerApiCollection))]
[Trait("Category", "Integration")]
public class ServerMetricsApiTests
{
    private readonly IServerApiService _svc;
    private readonly ITestOutputHelper _output;

    public ServerMetricsApiTests(ServerApiFixture fixture, ITestOutputHelper output)
    {
        _svc = fixture.ServerService;
        _output = output;
    }

    [Fact]
    public async Task S09_ServerMetrics_Lifecycle()
    {
        // 선행: Category + Server 생성
        var catResp = await _svc.CreateCategoryAsync(new CategoryDto
        {
            TypeServer = "LOG",
            Name = "MIG_메트릭테스트_카테고리"
        });
        Assert.True(catResp.Success, $"Category create failed: {catResp.Message}");
        var categoryId = catResp.Data!.Id;

        try
        {
            var srvResp = await _svc.CreateServerAsync(new ServerDto
            {
                CategoryId = categoryId,
                Name = "MIG_메트릭테스트_서버",
                Status = "NORMAL",
                IpAddress = "10.99.99.60",
                Port = 8080
            });
            Assert.True(srvResp.Success, $"Server create failed: {srvResp.Message}");
            var serverId = srvResp.Data!.Id;

            try
            {
                // [1] CREATE Metric
                var metricDto = new ServerMetricDto
                {
                    CpuUsage = 45.5,
                    RamUsage = 60.2,
                    DiskUsage = 30.1,
                    NetworkInMbps = 10.5,
                    NetworkOutMbps = 20.3
                };
                var createResp = await _svc.CreateServerMetricAsync(serverId, metricDto);
                _output.WriteLine($"CreateMetric: Success={createResp.Success}, Id={createResp.Data?.Id}");
                Assert.True(createResp.Success, $"Metric create failed: {createResp.Message}");

                // [2] GET History
                var historyResp = await _svc.GetServerMetricsAsync(serverId);
                _output.WriteLine($"History: Success={historyResp.Success}, Count={historyResp.Data?.Count}");
                Assert.True(historyResp.Success);
                Assert.NotNull(historyResp.Data);
                Assert.True(historyResp.Data!.Count > 0);

                // [3] GET Latest
                var latestResp = await _svc.GetServerMetricLatestAsync(serverId);
                _output.WriteLine($"Latest: Success={latestResp.Success}");
                Assert.True(latestResp.Success);
                Assert.NotNull(latestResp.Data?.LatestMetrics);

                // [4] DELETE — 서버는 before_date 기준으로 "old" 메트릭만 삭제하므로
                //     최근 생성된 메트릭은 deleted_count=0일 수 있음 (서버 정책)
                var delResp = await _svc.DeleteServerMetricsAsync(serverId);
                _output.WriteLine($"DeleteMetrics: Success={delResp.Success}, Deleted={delResp.Data?.DeletedCount}");
                Assert.True(delResp.Success);
            }
            finally
            {
                await _svc.DeleteServerAsync(serverId);
            }
        }
        finally
        {
            await _svc.DeleteCategoryAsync(categoryId);
        }
    }

    [Fact]
    public async Task S10_GetServerMetrics_DateFilter()
    {
        // 선행: Category + Server + Metric 생성
        var catResp = await _svc.CreateCategoryAsync(new CategoryDto
        {
            TypeServer = "BACKUP",
            Name = "MIG_날짜필터_카테고리"
        });
        Assert.True(catResp.Success);
        var categoryId = catResp.Data!.Id;

        try
        {
            var srvResp = await _svc.CreateServerAsync(new ServerDto
            {
                CategoryId = categoryId,
                Name = "MIG_날짜필터_서버",
                Status = "NORMAL",
                IpAddress = "10.99.99.61",
                Port = 8080
            });
            Assert.True(srvResp.Success);
            var serverId = srvResp.Data!.Id;

            try
            {
                await _svc.CreateServerMetricAsync(serverId, new ServerMetricDto
                {
                    CpuUsage = 50.0,
                    RamUsage = 70.0
                });

                // 날짜 필터 테스트 — 오늘 범위
                var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
                var resp = await _svc.GetServerMetricsAsync(serverId,
                    startDate: today, endDate: today, limit: 10);
                _output.WriteLine($"DateFilter: Success={resp.Success}, Count={resp.Data?.Count}");
                Assert.True(resp.Success);

                // cleanup
                var cleanupDate = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");
                await _svc.DeleteServerMetricsAsync(serverId, beforeDate: cleanupDate);
            }
            finally
            {
                await _svc.DeleteServerAsync(serverId);
            }
        }
        finally
        {
            await _svc.DeleteCategoryAsync(categoryId);
        }
    }

    [Fact]
    public async Task S11_ServerMetrics_EmptyHistory()
    {
        // 선행: Category + Server 생성 (메트릭 없음)
        var catResp = await _svc.CreateCategoryAsync(new CategoryDto
        {
            TypeServer = "PUSH",
            Name = "MIG_빈히스토리_카테고리"
        });
        Assert.True(catResp.Success);
        var categoryId = catResp.Data!.Id;

        try
        {
            var srvResp = await _svc.CreateServerAsync(new ServerDto
            {
                CategoryId = categoryId,
                Name = "MIG_빈히스토리_서버",
                Status = "NORMAL",
                IpAddress = "10.99.99.62",
                Port = 8080
            });
            Assert.True(srvResp.Success);
            var serverId = srvResp.Data!.Id;

            try
            {
                var resp = await _svc.GetServerMetricsAsync(serverId);
                _output.WriteLine($"EmptyHistory: Success={resp.Success}, Count={resp.Data?.Count}");
                Assert.True(resp.Success);
                Assert.True(resp.Data == null || resp.Data.Count == 0);
            }
            finally
            {
                await _svc.DeleteServerAsync(serverId);
            }
        }
        finally
        {
            await _svc.DeleteCategoryAsync(categoryId);
        }
    }
}

#endregion

#region - Enclosure Metrics API Tests (S12~S14) -

[Collection(nameof(ServerApiCollection))]
[Trait("Category", "Integration")]
public class EnclosureMetricsApiTests
{
    private readonly IDeviceApiService _deviceSvc;
    private readonly ITestOutputHelper _output;

    public EnclosureMetricsApiTests(ServerApiFixture fixture, ITestOutputHelper output)
    {
        _deviceSvc = fixture.DeviceService;
        _output = output;
    }

    [Fact]
    public async Task S12_EnclosureMetrics_Lifecycle()
    {
        // 선행: Enclosure 생성
        var encDto = new EnclosureDeviceDto
        {
            NumberDevice = 9908,
            NameDevice = "MIG_함체_메트릭테스트",
            TypeDevice = "Enclosure",
            Status = "ACTIVATED",
            DoorStatus = "CLOSED"
        };
        var encResp = await _deviceSvc.CreateEnclosureAsync(encDto);
        Assert.True(encResp.Success, $"Enclosure create failed: {encResp.Message}");
        var enclosureId = encResp.Data!.Id;

        try
        {
            // [1] CREATE Metric
            var metricDto = new EnclosureMetricDto
            {
                Temperature = "25.5",
                Humidity = "55.0"
            };
            var createResp = await _deviceSvc.CreateEnclosureMetricAsync(enclosureId, metricDto);
            _output.WriteLine($"CreateMetric: Success={createResp.Success}");
            Assert.True(createResp.Success, $"Enclosure metric create failed: {createResp.Message}");

            // [2] GET History
            var historyResp = await _deviceSvc.GetEnclosureMetricsAsync(enclosureId);
            _output.WriteLine($"History: Success={historyResp.Success}, Count={historyResp.Data?.Count}");
            Assert.True(historyResp.Success);
            Assert.NotNull(historyResp.Data);
            Assert.True(historyResp.Data!.Count > 0);

            // [3] GET Latest
            var latestResp = await _deviceSvc.GetEnclosureMetricLatestAsync(enclosureId);
            _output.WriteLine($"Latest: Success={latestResp.Success}");
            Assert.True(latestResp.Success);

            // [4] DELETE
            var delResp = await _deviceSvc.DeleteEnclosureMetricsAsync(enclosureId);
            _output.WriteLine($"DeleteMetrics: Success={delResp.Success}, Deleted={delResp.Data?.DeletedCount}");
            Assert.True(delResp.Success);
            Assert.True(delResp.Data!.DeletedCount > 0);
        }
        finally
        {
            await _deviceSvc.DeleteEnclosureAsync(enclosureId);
        }
    }

    [Fact]
    public async Task S13_GetEnclosureMetrics_TimeFilter()
    {
        // 선행: Enclosure + Metric 생성
        var encResp = await _deviceSvc.CreateEnclosureAsync(new EnclosureDeviceDto
        {
            NumberDevice = 9909,
            NameDevice = "MIG_함체_시간필터",
            TypeDevice = "Enclosure",
            Status = "ACTIVATED",
            DoorStatus = "CLOSED"
        });
        Assert.True(encResp.Success);
        var enclosureId = encResp.Data!.Id;

        try
        {
            await _deviceSvc.CreateEnclosureMetricAsync(enclosureId, new EnclosureMetricDto
            {
                Temperature = "30.0",
                Humidity = "60.0"
            });

            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var resp = await _deviceSvc.GetEnclosureMetricsAsync(enclosureId,
                startTime: today, endTime: today, limit: 10);
            _output.WriteLine($"TimeFilter: Success={resp.Success}, Count={resp.Data?.Count}");
            Assert.True(resp.Success);

            // cleanup
            await _deviceSvc.DeleteEnclosureMetricsAsync(enclosureId);
        }
        finally
        {
            await _deviceSvc.DeleteEnclosureAsync(enclosureId);
        }
    }

    [Fact]
    public async Task S14_EnclosureMetrics_EmptyHistory()
    {
        // 선행: Enclosure 생성 (메트릭 없음)
        var encResp = await _deviceSvc.CreateEnclosureAsync(new EnclosureDeviceDto
        {
            NumberDevice = 9910,
            NameDevice = "MIG_함체_빈히스토리",
            TypeDevice = "Enclosure",
            Status = "ACTIVATED",
            DoorStatus = "CLOSED"
        });
        Assert.True(encResp.Success);
        var enclosureId = encResp.Data!.Id;

        try
        {
            var resp = await _deviceSvc.GetEnclosureMetricsAsync(enclosureId);
            _output.WriteLine($"EmptyHistory: Success={resp.Success}, Count={resp.Data?.Count}");
            Assert.True(resp.Success);
            Assert.True(resp.Data == null || resp.Data.Count == 0);
        }
        finally
        {
            await _deviceSvc.DeleteEnclosureAsync(enclosureId);
        }
    }
}

#endregion
