using Autofac;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Modules;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Tests.LiveApi;
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
                  라이브 서버: IRONWALL_LIVE_API_URL(기본 https://localhost:8000/api)
                  + IRONWALL_E2E_CRED_FILE 전용 계정 — 없으면 건너뜀,
                  쓰기 시험은 loopback · 사설망 주소에서만 돈다
                  서버 등록 · 수정은 7.0+ 축 통로(IServerAxisApiService)로 한다 — 평면 ServerDto 쓰기는
                  AXIS_SHAPE_REQUIRED 로 막히는 것을 회귀로 확인한다(ServerTestKit).
                  카테고리 유형은 UNIQUE 라 시험마다 시드에 없는 유형을 따로 쓴다
                  (이 파일: ETC · MONITORING · LOG · BACKUP · PUSH — ServerApiServiceTests 와 겹치지 않는다).
****************************************************************************/

#region - Fixture & Collection -

public sealed class ServerApiFixture : IAsyncLifetime
{
    internal IServerApiService ServerService { get; private set; } = null!;
    internal IDeviceApiService DeviceService { get; private set; } = null!;
    internal ILogService Log { get; } = new LogService();

    public async Task InitializeAsync()
    {
        // 라이브 환경이 없으면 모든 시험이 건너뛴다 — 픽스처도 네트워크에 닿지 않는다.
        if (LiveApiTarget.ReadSkipReason is not null) return;

        // 어셈블리 공용 로그인 세션에서 꺼낸다(초기화 · 로그인은 세션이 이미 했다).
        ServerService = await LiveApiSession.ResolveAsync<IServerApiService>();
        DeviceService = await LiveApiSession.ResolveAsync<IDeviceApiService>();
    }

    // 공용 세션은 어셈블리 내내 쓴다 — 여기서 서비스를 멈추거나 컨테이너를 버리지 않는다.
    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition(nameof(ServerApiCollection))]
public sealed class ServerApiCollection : ICollectionFixture<ServerApiFixture> { }

#endregion

#region - Category API Tests (S01~S03) -

[Collection(nameof(ServerApiCollection))]
[Trait("Category", "Integration")]
[Trait("Scenario", "DEP-202")]
public class CategoryApiTests
{
    private readonly IServerApiService _svc;
    private readonly ITestOutputHelper _output;

    public CategoryApiTests(ServerApiFixture fixture, ITestOutputHelper output)
    {
        _svc = fixture.ServerService;
        _output = output;
    }

    [LiveApiWriteFact]
    public async Task S01_Category_CRUD_Lifecycle()
    {
        // [1] CREATE — 유형은 서버 enum ETC(시드에 없는 유형, 이 시험 전용 — 유형은 UNIQUE 라 시험마다 다르게 쓴다).
        //     7.0+ 은 판별자를 category_server 로 보낸다(ServerApiService.CategoryAxisBody).
        await ServerTestKit.PurgeLeftoverCategoryAsync(_svc, "ETC", ServerTestKit.MigrationPrefix, _output);
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

    [LiveApiFact]
    public async Task S02_GetCategories_Pagination()
    {
        var resp = await _svc.GetCategoriesAsync(page: 1, limit: 5);
        _output.WriteLine($"Pagination: Success={resp.Success}, Total={resp.Pagination?.Total}");
        Assert.True(resp.Success);
        Assert.NotNull(resp.Pagination);
        Assert.True(resp.Pagination!.Total >= 0);
    }

    [LiveApiFact]
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
[Trait("Scenario", "DEP-202")]
public class ServerInstanceApiTests
{
    private readonly IServerApiService _svc;
    private readonly ITestOutputHelper _output;

    public ServerInstanceApiTests(ServerApiFixture fixture, ITestOutputHelper output)
    {
        _svc = fixture.ServerService;
        _output = output;
    }

    [LiveApiWriteFact]
    public async Task S04_Server_CRUD_Lifecycle()
    {
        // 선행: Category 생성 — 서버는 그 유형(category_server)의 카테고리 행이 있어야 등록된다(servers.py:198-208).
        //       유형 MONITORING 은 이 시험 전용(시드에 없음 · 유형 UNIQUE).
        const string type = "MONITORING";
        var categoryId = await ServerTestKit.CreateCategoryAsync(
            _svc, type, "MIG_서버테스트_카테고리", ServerTestKit.MigrationPrefix, _output, "서버 CRUD 테스트용");

        // 7.0+ 서버 쓰기는 축 통로(IServerAxisApiService)로 한다 — 평면 ServerDto 는 네트워크 전에 막힌다(D-22).
        var axis = await ServerTestKit.AxisAsync();
        var isAxisEra = ServerTestKit.IsAxisEra(axis);
        var name = ServerTestKit.UniqueName("MIG_테스트서버");
        var serverId = 0;

        try
        {
            // [0] 회귀 확인 — 축 계약에서 평면 ServerDto 등록은 나가지 않고 AXIS_SHAPE_REQUIRED 다.
            if (isAxisEra)
            {
                var flat = await _svc.CreateServerAsync(new ServerDto
                {
                    CategoryId = categoryId,
                    Name = name,
                    IpAddress = "10.99.99.50",
                    Port = 8080
                });
                Assert.False(flat.Success);
                Assert.Equal("AXIS_SHAPE_REQUIRED", flat.Error?.Code);
            }

            // [1] CREATE — 축 통로. status 는 관측 필드라 등록 본문에 싣지 않는다(7.0+ 은 늘 UNKNOWN 으로 시작, servers.py:523).
            var created = await axis.CreateServerAsync(new ServerWriteIntent
            {
                TypeServer = type,
                CategoryId = categoryId,
                Name = name,
                IpAddress = "10.99.99.50",
                Port = 8080
            });
            _output.WriteLine($"Create: Success={created.IsSuccess}, Id={created.Id}, Msg={created.Message}");
            Assert.True(created.IsSuccess, $"Server create failed: {created.Message}");
            Assert.True(created.Id > 0);
            serverId = created.Id;

            // [2] READ — 레거시 단건 조회도 축 응답(connection)을 평면 필드로 역투영해 읽는다.
            var getResp = await _svc.GetServerByIdAsync(serverId);
            Assert.True(getResp.Success);
            Assert.Equal(name, getResp.Data?.Name);
            Assert.Equal("10.99.99.50", getResp.Data?.IpAddress);
            Assert.Equal(8080, getResp.Data?.Port);
            if (isAxisEra)
            {
                Assert.Equal(type, getResp.Data?.CategoryServer);
                Assert.Equal("UNKNOWN", getResp.Data?.Status);
            }

            // [3] PATCH — 이름만 바꾼다(보낸 키만 바뀐다).
            var patchedName = ServerTestKit.UniqueName("MIG_수정_서버");
            if (isAxisEra)
            {
                var flatPatch = await _svc.PatchServerAsync(serverId, new ServerDto { Name = patchedName });
                Assert.Equal("AXIS_SHAPE_REQUIRED", flatPatch.Error?.Code);
            }
            var patchResp = await axis.PatchServerAsync(serverId, new ServerWriteIntent { Name = patchedName });
            _output.WriteLine($"Patch: Success={patchResp.IsSuccess}, Msg={patchResp.Message}");
            Assert.True(patchResp.IsSuccess, patchResp.Message);

            var verifyPatch = await _svc.GetServerByIdAsync(serverId);
            Assert.Equal(patchedName, verifyPatch.Data?.Name);
            Assert.Equal("10.99.99.50", verifyPatch.Data?.IpAddress);

            // [4] 교체 — 7.0+ 평면 PUT 은 막혀 있다. 이름 · 접속을 한 번에 바꾸고, 상태는 관측 보고 입구로 바꾼다
            //     (PATCH /servers/{id}/status 가 status 의 유일한 입구, servers.py:699-782).
            var replacedName = ServerTestKit.UniqueName("MIG_교체_서버");
            if (isAxisEra)
            {
                var flatPut = await _svc.UpdateServerAsync(serverId, new ServerDto
                {
                    CategoryId = categoryId,
                    Name = replacedName,
                    IpAddress = "10.99.99.51",
                    Port = 9090
                });
                Assert.Equal("AXIS_SHAPE_REQUIRED", flatPut.Error?.Code);
            }
            var putResp = await axis.PatchServerAsync(serverId, new ServerWriteIntent
            {
                Name = replacedName,
                IpAddress = "10.99.99.51",
                Port = 9090
            });
            _output.WriteLine($"Put: Success={putResp.IsSuccess}, Msg={putResp.Message}");
            Assert.True(putResp.IsSuccess, putResp.Message);

            if (isAxisEra)
            {
                var report = await _svc.ReportServerStatusAsync(serverId, "ERROR");
                _output.WriteLine($"Status: Success={report.Success}, Status={report.Data?.Status}, Msg={report.Message}");
                Assert.True(report.Success, $"Status report failed: {report.Message} {report.Error?.Message}");
                Assert.Equal("ERROR", report.Data?.Status);
            }

            var verifyPut = await _svc.GetServerByIdAsync(serverId);
            Assert.Equal(replacedName, verifyPut.Data?.Name);
            Assert.Equal("10.99.99.51", verifyPut.Data?.IpAddress);
            Assert.Equal(9090, verifyPut.Data?.Port);
            if (isAxisEra) Assert.Equal("ERROR", verifyPut.Data?.Status);
        }
        finally
        {
            // [5] DELETE Server → 후행: Category 삭제(서버가 남으면 409 라 서버가 먼저다)
            var serverDeleted = serverId <= 0 || (await _svc.DeleteServerAsync(serverId)).Success;
            var verifyDel = serverId > 0 ? await _svc.GetServerByIdAsync(serverId) : null;
            var categoryDeleted = await _svc.DeleteCategoryAsync(categoryId);
            _output.WriteLine($"Cleanup: server={serverDeleted}, category={categoryDeleted.Success} {categoryDeleted.Message}");

            Assert.True(serverDeleted);
            Assert.True(verifyDel is null || !verifyDel.Success || verifyDel.Data == null);
        }
    }

    [LiveApiFact]
    public async Task S05_GetServers_Pagination()
    {
        var resp = await _svc.GetServersAsync(page: 1, limit: 5);
        _output.WriteLine($"Pagination: Success={resp.Success}, Total={resp.Pagination?.Total}");
        Assert.True(resp.Success);
        Assert.NotNull(resp.Pagination);
    }

    [LiveApiFact]
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

    [LiveApiFact]
    public async Task S07_GetServers_StatusFilter()
    {
        var resp = await _svc.GetServersAsync(status: "NORMAL", page: 1, limit: 100);
        _output.WriteLine($"StatusFilter: Success={resp.Success}, Count={resp.Data?.Count}");
        Assert.True(resp.Success);
    }

    [LiveApiFact]
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
[Trait("Scenario", "DEP-202")]
public class ServerMetricsApiTests
{
    private readonly IServerApiService _svc;
    private readonly ITestOutputHelper _output;

    public ServerMetricsApiTests(ServerApiFixture fixture, ITestOutputHelper output)
    {
        _svc = fixture.ServerService;
        _output = output;
    }

    [LiveApiWriteFact]
    public async Task S09_ServerMetrics_Lifecycle()
    {
        // 선행: Category + Server 생성 — 서버 등록은 축 통로(7.0+ 평면 ServerDto 는 AXIS_SHAPE_REQUIRED)
        const string type = "LOG";
        var categoryId = await ServerTestKit.CreateCategoryAsync(
            _svc, type, "MIG_메트릭테스트_카테고리", ServerTestKit.MigrationPrefix, _output);
        var serverId = 0;

        try
        {
            var axis = await ServerTestKit.AxisAsync();
            serverId = await ServerTestKit.CreateServerAsync(
                axis, type, categoryId, ServerTestKit.UniqueName("MIG_메트릭테스트_서버"), "10.99.99.60", 8080);

            // [1] CREATE Metric — 7.0+ 본문은 서버가 받는 키만(server_id · created_at · threshold_exceeded 없음)
            var metricDto = new ServerMetricDto
            {
                CpuUsage = 45.5,
                RamUsage = 60.2,
                DiskUsage = 30.1,
                NetworkInMbps = 10.5,
                NetworkOutMbps = 20.3
            };
            var createResp = await _svc.CreateServerMetricAsync(serverId, metricDto);
            _output.WriteLine($"CreateMetric: Success={createResp.Success}, Id={createResp.Data?.Id}, Msg={createResp.Message}");
            Assert.True(createResp.Success, $"Metric create failed: {createResp.Message} {createResp.Error?.Message}");
            Assert.Equal(serverId, createResp.Data!.ServerId);
            Assert.Equal(45.5, createResp.Data.CpuUsage);

            // [2] GET History
            var historyResp = await _svc.GetServerMetricsAsync(serverId);
            _output.WriteLine($"History: Success={historyResp.Success}, Count={historyResp.Data?.Count}");
            Assert.True(historyResp.Success);
            Assert.NotNull(historyResp.Data);
            Assert.True(historyResp.Data!.Count > 0);
            Assert.All(historyResp.Data, m => Assert.Equal(serverId, m.ServerId));

            // [3] GET Latest
            var latestResp = await _svc.GetServerMetricLatestAsync(serverId);
            _output.WriteLine($"Latest: Success={latestResp.Success}");
            Assert.True(latestResp.Success);
            Assert.NotNull(latestResp.Data?.LatestMetrics);
            Assert.Equal(serverId, latestResp.Data!.ServerId);
            Assert.Equal(45.5, latestResp.Data.LatestMetrics!.CpuUsage);

            // [4] DELETE — 서버 계약은 older_than_days(≥1) 하나뿐이다(server_metrics.py:293-336).
            //     방금 기록한 계측은 1일보다 새것이라 0건 삭제. 건수는 message 에만 실려 와 라이브러리가 Data 를 합성한다.
            var delResp = await _svc.DeleteServerMetricsAsync(serverId, olderThanDays: 1);
            _output.WriteLine($"DeleteMetrics: Success={delResp.Success}, Deleted={delResp.Data?.DeletedCount}, Msg={delResp.Message}");
            Assert.True(delResp.Success, $"Metric delete failed: {delResp.Message}");
            Assert.NotNull(delResp.Data);
            Assert.Equal(serverId, delResp.Data!.ServerId);
            Assert.Equal(0, delResp.Data.DeletedCount);

            var afterDelete = await _svc.GetServerMetricsAsync(serverId);
            Assert.True(afterDelete.Success);
            Assert.True(afterDelete.Data!.Count > 0);

            // 회귀 확인 — 서버에 없는 before_date 오버로드는 나가지 않고 거부한다(조용한 30일 삭제 방지).
#pragma warning disable CS0618
            var legacyDelete = await _svc.DeleteServerMetricsAsync(serverId, beforeDate: "2020-01-01");
#pragma warning restore CS0618
            Assert.False(legacyDelete.Success);
            Assert.Equal("INVALID_ARGUMENT", legacyDelete.Error?.Code);
        }
        finally
        {
            await ServerTestKit.DeleteServerThenCategoryAsync(_svc, serverId, categoryId, _output);
        }
    }

    [LiveApiWriteFact]
    public async Task S10_GetServerMetrics_DateFilter()
    {
        // 선행: Category + Server + Metric 생성
        const string type = "BACKUP";
        var categoryId = await ServerTestKit.CreateCategoryAsync(
            _svc, type, "MIG_날짜필터_카테고리", ServerTestKit.MigrationPrefix, _output);
        var serverId = 0;

        try
        {
            var axis = await ServerTestKit.AxisAsync();
            serverId = await ServerTestKit.CreateServerAsync(
                axis, type, categoryId, ServerTestKit.UniqueName("MIG_날짜필터_서버"), "10.99.99.61", 8080);

            var metric = await _svc.CreateServerMetricAsync(serverId, new ServerMetricDto
            {
                CpuUsage = 50.0,
                RamUsage = 70.0
            });
            Assert.True(metric.Success, $"Metric create failed: {metric.Message} {metric.Error?.Message}");

            // 날짜 필터 테스트 — 오늘 범위
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var resp = await _svc.GetServerMetricsAsync(serverId,
                startDate: today, endDate: today, limit: 10);
            _output.WriteLine($"DateFilter: Success={resp.Success}, Count={resp.Data?.Count}");
            Assert.True(resp.Success);

            // 정리: 서버 삭제가 계측을 CASCADE 로 함께 지운다(servers.py:670) — 따로 지우지 않는다.
        }
        finally
        {
            await ServerTestKit.DeleteServerThenCategoryAsync(_svc, serverId, categoryId, _output);
        }
    }

    [LiveApiWriteFact]
    public async Task S11_ServerMetrics_EmptyHistory()
    {
        // 선행: Category + Server 생성 (메트릭 없음)
        const string type = "PUSH";
        var categoryId = await ServerTestKit.CreateCategoryAsync(
            _svc, type, "MIG_빈히스토리_카테고리", ServerTestKit.MigrationPrefix, _output);
        var serverId = 0;

        try
        {
            var axis = await ServerTestKit.AxisAsync();
            serverId = await ServerTestKit.CreateServerAsync(
                axis, type, categoryId, ServerTestKit.UniqueName("MIG_빈히스토리_서버"), "10.99.99.62", 8080);

            var resp = await _svc.GetServerMetricsAsync(serverId);
            _output.WriteLine($"EmptyHistory: Success={resp.Success}, Count={resp.Data?.Count}");
            Assert.True(resp.Success);
            Assert.True(resp.Data == null || resp.Data.Count == 0);

            // 보고받은 적 없는 서버의 최신 계측은 latest_metrics=null 이다(server_metrics.py:275-290).
            var latest = await _svc.GetServerMetricLatestAsync(serverId);
            Assert.True(latest.Success);
            Assert.Equal(serverId, latest.Data?.ServerId);
            Assert.Null(latest.Data?.LatestMetrics);
        }
        finally
        {
            await ServerTestKit.DeleteServerThenCategoryAsync(_svc, serverId, categoryId, _output);
        }
    }
}

#endregion

#region - Enclosure Metrics API Tests (S12~S14) -

[Collection(nameof(ServerApiCollection))]
[Trait("Category", "Integration")]
[Trait("Scenario", "DEP-202")]
public class EnclosureMetricsApiTests
{
    private readonly IDeviceApiService _deviceSvc;
    private readonly ITestOutputHelper _output;

    public EnclosureMetricsApiTests(ServerApiFixture fixture, ITestOutputHelper output)
    {
        _deviceSvc = fixture.DeviceService;
        _output = output;
    }

    [LiveApiWriteFact]
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

    [LiveApiWriteFact]
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

    [LiveApiWriteFact]
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
