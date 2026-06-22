using Autofac;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Api.Modules;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Ironwall.Dotnet.Libraries.Events.Api.Tests;
/****************************************************************************
   Purpose      : Event API Integration Tests
   Created By   : Claude
   Created On   : 2026-02-24
   Department   : SW Team
   Company      : Sensorway Co., Ltd.

   Description  : Event 4종 CRUD + Action 연동 + Detection Log + EventMapping CRUD 통합 테스트
                  xUnit IAsyncLifetime Fixture 패턴 사용
                  GOP API 서버 (localhost:8000) 연동 필수
****************************************************************************/

#region - Fixture & Collection -

public sealed class EventApiFixture : IAsyncLifetime
{
    internal IEventApiService Service { get; private set; } = null!;
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
        builder.RegisterModule(new EventApiModule(Log, _setup, "EventIntTest"));
        _container = builder.Build();
        Service = _container.ResolveNamed<IEventApiService>("EventIntTest");
        await Service.ExecuteAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await Service.StopAsync(CancellationToken.None);
        _container?.Dispose();
    }
}

[CollectionDefinition(nameof(EventApiCollection))]
public sealed class EventApiCollection : ICollectionFixture<EventApiFixture> { }

#endregion

#region - Detection Event Tests (E01~E03) -

[Collection(nameof(EventApiCollection))]
public class DetectionEventApiTests
{
    private readonly IEventApiService _svc;
    private readonly ITestOutputHelper _output;

    public DetectionEventApiTests(EventApiFixture fixture, ITestOutputHelper output)
    {
        _svc = fixture.Service;
        _output = output;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task E01_Detection_CRUD_Lifecycle()
    {
        // ── Create ──
        var createDto = new DetectionEventDto
        {
            TypeEvent = "Intrusion",
            DeviceId = 2,
            Result = "VIBRATION_SENSOR"
        };

        var createResp = await _svc.CreateDetectionEventAsync(createDto);
        Assert.True(createResp.Success,
            $"Create failed: {createResp.Error?.Code} - {createResp.Message}. Details: {createResp.Error?.Details}");
        Assert.NotNull(createResp.Data);
        var id = createResp.Data.Id;
        Assert.True(id > 0);
        _output.WriteLine($"Created Detection ID={id}");

        try
        {
            // ── Get by ID ──
            var getResp = await _svc.GetDetectionEventByIdAsync(id);
            Assert.True(getResp.Success);
            Assert.NotNull(getResp.Data);
            Assert.Equal(id, getResp.Data.Id);
            Assert.Equal("Intrusion", getResp.Data.TypeEvent);

            // ── Patch ──
            var patchDto = new DetectionEventDto { Result = "PIR_SENSOR" };
            var patchResp = await _svc.PatchDetectionEventAsync(id, patchDto);
            Assert.True(patchResp.Success,
                $"Patch failed: {patchResp.Error?.Code} - {patchResp.Message}");
            Assert.Equal("PIR_SENSOR", patchResp.Data!.Result);

            // ── Put ──
            var putDto = new DetectionEventReplaceDto
            {
                TypeEvent = "Intrusion",
                Result = "THERMAL_SENSOR"
            };
            var putResp = await _svc.UpdateDetectionEventAsync(id, putDto);
            Assert.True(putResp.Success,
                $"Put failed: {putResp.Error?.Code} - {putResp.Message}");
            Assert.Equal("THERMAL_SENSOR", putResp.Data!.Result);
        }
        finally
        {
            // ── Delete ──
            var delResp = await _svc.DeleteDetectionEventAsync(id);
            Assert.True(delResp.Success, $"Delete failed: {delResp.Error?.Code}");
            _output.WriteLine($"Deleted Detection ID={id}");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task E02_Detection_GetList_DateFilter()
    {
        var resp = await _svc.GetDetectionEventsAsync(
            startDate: DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            endDate: DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            page: 1, limit: 5);

        Assert.True(resp.Success, $"GetList failed: {resp.Error?.Code} - {resp.Message}");
        Assert.NotNull(resp.Data);
        _output.WriteLine($"Detection list count={resp.Data.Count}, pagination={resp.Pagination?.Total}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task E03_Detection_NotFound()
    {
        var resp = await _svc.GetDetectionEventByIdAsync(999999);
        Assert.False(resp.Success);
        _output.WriteLine($"NotFound: {resp.Error?.Code} - {resp.Message}");
    }
}

#endregion

#region - Malfunction Event Tests (E04~E05) -

[Collection(nameof(EventApiCollection))]
public class MalfunctionEventApiTests
{
    private readonly IEventApiService _svc;
    private readonly ITestOutputHelper _output;

    public MalfunctionEventApiTests(EventApiFixture fixture, ITestOutputHelper output)
    {
        _svc = fixture.Service;
        _output = output;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task E04_Malfunction_CRUD_Lifecycle()
    {
        // ── Create ──
        var createDto = new MalfunctionEventDto
        {
            TypeEvent = "Fault",
            DeviceId = 2,
            Reason = "FAULT_FENCE",
            Detail = new MalfunctionDetailDto
            {
                FirstStart = 10, FirstEnd = 10,
                SecondStart = 15, SecondEnd = 15
            }
        };

        var createResp = await _svc.CreateMalfunctionEventAsync(createDto);
        Assert.True(createResp.Success,
            $"Create failed: {createResp.Error?.Code} - {createResp.Message}. Details: {createResp.Error?.Details}");
        var id = createResp.Data!.Id;
        Assert.True(id > 0);
        _output.WriteLine($"Created Malfunction ID={id}");

        try
        {
            // ── Get by ID ──
            var getResp = await _svc.GetMalfunctionEventByIdAsync(id);
            Assert.True(getResp.Success);
            Assert.Equal(id, getResp.Data!.Id);
            Assert.Equal("Fault", getResp.Data.TypeEvent);

            // ── Patch ──
            var patchDto = new MalfunctionEventDto { Reason = "FAULT_MULTI" };
            var patchResp = await _svc.PatchMalfunctionEventAsync(id, patchDto);
            Assert.True(patchResp.Success,
                $"Patch failed: {patchResp.Error?.Code} - {patchResp.Message}");
            Assert.Equal("FAULT_MULTI", patchResp.Data!.Reason);

            // ── Put ──
            var putDto = new MalfunctionEventReplaceDto
            {
                TypeEvent = "Fault",
                Reason = "FAULT_ETC",
                Detail = new MalfunctionDetailDto
                {
                    FirstStart = 5, FirstEnd = 5,
                    SecondStart = 10, SecondEnd = 10
                }
            };
            var putResp = await _svc.UpdateMalfunctionEventAsync(id, putDto);
            Assert.True(putResp.Success,
                $"Put failed: {putResp.Error?.Code} - {putResp.Message}");
            Assert.Equal("FAULT_ETC", putResp.Data!.Reason);
        }
        finally
        {
            // ── Delete ──
            var delResp = await _svc.DeleteMalfunctionEventAsync(id);
            Assert.True(delResp.Success, $"Delete failed: {delResp.Error?.Code}");
            _output.WriteLine($"Deleted Malfunction ID={id}");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task E05_Malfunction_NotFound()
    {
        var resp = await _svc.GetMalfunctionEventByIdAsync(999999);
        Assert.False(resp.Success);
        _output.WriteLine($"NotFound: {resp.Error?.Code} - {resp.Message}");
    }
}

#endregion

#region - Connection Event Tests (E06~E07) -

[Collection(nameof(EventApiCollection))]
public class ConnectionEventApiTests
{
    private readonly IEventApiService _svc;
    private readonly ITestOutputHelper _output;

    public ConnectionEventApiTests(EventApiFixture fixture, ITestOutputHelper output)
    {
        _svc = fixture.Service;
        _output = output;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task E06_Connection_CRUD_Lifecycle()
    {
        // ── Create ──
        var createDto = new ConnectionEventDto
        {
            TypeEvent = "Connection",
            DeviceId = 2
        };

        var createResp = await _svc.CreateConnectionEventAsync(createDto);
        Assert.True(createResp.Success,
            $"Create failed: {createResp.Error?.Code} - {createResp.Message}. Details: {createResp.Error?.Details}");
        var id = createResp.Data!.Id;
        Assert.True(id > 0);
        _output.WriteLine($"Created Connection ID={id}");

        try
        {
            // ── Get by ID ──
            var getResp = await _svc.GetConnectionEventByIdAsync(id);
            Assert.True(getResp.Success);
            Assert.Equal(id, getResp.Data!.Id);
            Assert.Equal("Connection", getResp.Data.TypeEvent);

            // ── Patch ──
            var patchDto = new ConnectionEventDto { DeviceDescription = "E06 Patched" };
            var patchResp = await _svc.PatchConnectionEventAsync(id, patchDto);
            Assert.True(patchResp.Success,
                $"Patch failed: {patchResp.Error?.Code} - {patchResp.Message}");

            // ── Put ──
            var putDto = new ConnectionEventReplaceDto
            {
                TypeEvent = "Connection"
            };
            var putResp = await _svc.UpdateConnectionEventAsync(id, putDto);
            Assert.True(putResp.Success,
                $"Put failed: {putResp.Error?.Code} - {putResp.Message}");
        }
        finally
        {
            // ── Delete ──
            var delResp = await _svc.DeleteConnectionEventAsync(id);
            Assert.True(delResp.Success, $"Delete failed: {delResp.Error?.Code}");
            _output.WriteLine($"Deleted Connection ID={id}");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task E07_Connection_NotFound()
    {
        var resp = await _svc.GetConnectionEventByIdAsync(999999);
        Assert.False(resp.Success);
        _output.WriteLine($"NotFound: {resp.Error?.Code} - {resp.Message}");
    }
}

#endregion

#region - Action Event Tests (E08~E09) -

[Collection(nameof(EventApiCollection))]
public class ActionEventApiTests
{
    private readonly IEventApiService _svc;
    private readonly ITestOutputHelper _output;

    public ActionEventApiTests(EventApiFixture fixture, ITestOutputHelper output)
    {
        _svc = fixture.Service;
        _output = output;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task E08_Action_CRUD_Lifecycle()
    {
        // ── 먼저 Detection 생성 (Action의 from_event 용) ──
        var detDto = new DetectionEventDto
        {
            TypeEvent = "Intrusion",
            DeviceId = 2,
            Result = "VIBRATION_SENSOR"
        };
        var detResp = await _svc.CreateDetectionEventAsync(detDto);
        Assert.True(detResp.Success, $"Detection create failed: {detResp.Error?.Code}");
        var detId = detResp.Data!.Id;
        _output.WriteLine($"Created source Detection ID={detId}");

        try
        {
            // ── Create Action ──
            var createDto = new ActionEventCreateDto
            {
                TypeEvent = "Action",
                Content = "E08 Integration Test Action",
                User = "test_operator",
                FromEventId = detId
            };
            var createResp = await _svc.CreateActionEventAsync(createDto);
            Assert.True(createResp.Success,
                $"Create failed: {createResp.Error?.Code} - {createResp.Message}. Details: {createResp.Error?.Details}");
            var id = createResp.Data!.Id;
            Assert.True(id > 0);
            _output.WriteLine($"Created Action ID={id}");

            try
            {
                // ── Get by ID ──
                var getResp = await _svc.GetActionEventByIdAsync(id);
                Assert.True(getResp.Success);
                Assert.Equal(id, getResp.Data!.Id);
                Assert.Equal("Action", getResp.Data.TypeEvent);

                // ── Patch ──
                var patchDto = new ActionEventDto
                {
                    Content = "E08 Patched Content",
                    User = "operator_kim"
                };
                var patchResp = await _svc.PatchActionEventAsync(id, patchDto);
                Assert.True(patchResp.Success,
                    $"Patch failed: {patchResp.Error?.Code} - {patchResp.Message}");
                Assert.Equal("operator_kim", patchResp.Data!.User);

                // ── Put ──
                var putDto = new ActionEventReplaceDto
                {
                    TypeEvent = "Action",
                    Content = "E08 Full Update Content",
                    User = "operator_park"
                };
                var putResp = await _svc.UpdateActionEventAsync(id, putDto);
                Assert.True(putResp.Success,
                    $"Put failed: {putResp.Error?.Code} - {putResp.Message}");
                Assert.Equal("operator_park", putResp.Data!.User);
            }
            finally
            {
                // ── Delete Action ──
                var delResp = await _svc.DeleteActionEventAsync(id);
                Assert.True(delResp.Success, $"Delete Action failed: {delResp.Error?.Code}");
                _output.WriteLine($"Deleted Action ID={id}");
            }
        }
        finally
        {
            // ── Delete source Detection ──
            await _svc.DeleteDetectionEventAsync(detId);
            _output.WriteLine($"Deleted source Detection ID={detId}");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task E09_Action_NotFound()
    {
        var resp = await _svc.GetActionEventByIdAsync(999999);
        Assert.False(resp.Success);
        _output.WriteLine($"NotFound: {resp.Error?.Code} - {resp.Message}");
    }
}

#endregion

#region - Detection/Malfunction Action & Detection Log Tests (E10~E12) -

[Collection(nameof(EventApiCollection))]
public class EventRelationApiTests
{
    private readonly IEventApiService _svc;
    private readonly ITestOutputHelper _output;

    public EventRelationApiTests(EventApiFixture fixture, ITestOutputHelper output)
    {
        _svc = fixture.Service;
        _output = output;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task E10_Detection_GetAction()
    {
        // ── Detection 생성 ──
        var detDto = new DetectionEventDto
        {
            TypeEvent = "Intrusion",
            DeviceId = 2,
            Result = "VIBRATION_SENSOR"
        };
        var detResp = await _svc.CreateDetectionEventAsync(detDto);
        Assert.True(detResp.Success);
        var detId = detResp.Data!.Id;

        // ── Action 생성 (from_event = detId) ──
        var actDto = new ActionEventCreateDto
        {
            TypeEvent = "Action",
            Content = "E10 Action for Detection",
            User = "test_operator",
            FromEventId = detId
        };
        var actResp = await _svc.CreateActionEventAsync(actDto);
        Assert.True(actResp.Success);
        var actId = actResp.Data!.Id;
        _output.WriteLine($"Detection={detId}, Action={actId}");

        try
        {
            // ── DetectionActions 조회 (v4.6 1:N — 배열) ──
            var actionResp = await _svc.GetDetectionActionsAsync(detId);
            Assert.True(actionResp.Success,
                $"GetDetectionActions failed: {actionResp.Error?.Code} - {actionResp.Message}");
            Assert.NotNull(actionResp.Data);
            Assert.Contains(actionResp.Data, a => a.Id == actId);
            _output.WriteLine($"DetectionActions count={actionResp.Data.Count}");
        }
        finally
        {
            await _svc.DeleteActionEventAsync(actId);
            await _svc.DeleteDetectionEventAsync(detId);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task E11_Malfunction_GetAction()
    {
        // ── Malfunction 생성 ──
        var malDto = new MalfunctionEventDto
        {
            TypeEvent = "Fault",
            DeviceId = 2,
            Reason = "FAULT_FENCE",
            Detail = new MalfunctionDetailDto
            {
                FirstStart = 10, FirstEnd = 10,
                SecondStart = 15, SecondEnd = 15
            }
        };
        var malResp = await _svc.CreateMalfunctionEventAsync(malDto);
        Assert.True(malResp.Success);
        var malId = malResp.Data!.Id;

        // ── Action 생성 (from_event = malId) ──
        var actDto = new ActionEventCreateDto
        {
            TypeEvent = "Action",
            Content = "E11 Action for Malfunction",
            User = "test_operator",
            FromEventId = malId
        };
        var actResp = await _svc.CreateActionEventAsync(actDto);
        Assert.True(actResp.Success);
        var actId = actResp.Data!.Id;
        _output.WriteLine($"Malfunction={malId}, Action={actId}");

        try
        {
            // ── MalfunctionActions 조회 (v4.6 1:N — 배열) ──
            var actionResp = await _svc.GetMalfunctionActionsAsync(malId);
            Assert.True(actionResp.Success,
                $"GetMalfunctionActions failed: {actionResp.Error?.Code} - {actionResp.Message}");
            Assert.NotNull(actionResp.Data);
            Assert.Contains(actionResp.Data, a => a.Id == actId);
            _output.WriteLine($"MalfunctionActions count={actionResp.Data.Count}");
        }
        finally
        {
            await _svc.DeleteActionEventAsync(actId);
            await _svc.DeleteMalfunctionEventAsync(malId);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task E12_DetectionLog_GetList()
    {
        var resp = await _svc.GetDetectionLogsAsync(
            startDate: DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            endDate: DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            page: 1, limit: 5);

        Assert.True(resp.Success,
            $"GetDetectionLogs failed: {resp.Error?.Code} - {resp.Message}. Details: {resp.Error?.Details}");
        Assert.NotNull(resp.Data);
        _output.WriteLine($"DetectionLog count={resp.Data.Count}, total={resp.Pagination?.Total}");
    }
}

#endregion

#region - Event Mapping Tests (E13~E20) -

[Collection(nameof(EventApiCollection))]
public class EventMappingApiTests
{
    private readonly IEventApiService _svc;
    private readonly ITestOutputHelper _output;

    public EventMappingApiTests(EventApiFixture fixture, ITestOutputHelper output)
    {
        _svc = fixture.Service;
        _output = output;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task E13_EventMapping_CRUD_Lifecycle()
    {
        // ── Create ──
        var createDto = new EventMappingDto
        {
            NameEvent = "E13 Test Mapping",
            DeviceGroupId = 1,
            CategoryEventMapping = "FENCE_SENSOR_ONLY",
            Description = "E13 Integration Test",
            Status = true
        };

        var createResp = await _svc.CreateEventMappingAsync(createDto);
        Assert.True(createResp.Success,
            $"Create failed: {createResp.Error?.Code} - {createResp.Message}. Details: {createResp.Error?.Details}");
        var id = createResp.Data!.Id;
        Assert.True(id > 0);
        _output.WriteLine($"Created EventMapping ID={id}");

        try
        {
            // ── Get by ID ──
            var getResp = await _svc.GetEventMappingByIdAsync(id);
            Assert.True(getResp.Success);
            Assert.Equal(id, getResp.Data!.Id);
            Assert.Equal("E13 Test Mapping", getResp.Data.NameEvent);

            // ── Patch ──
            var patchDto = new EventMappingDto { Description = "E13 Patched" };
            var patchResp = await _svc.PatchEventMappingAsync(id, patchDto);
            Assert.True(patchResp.Success,
                $"Patch failed: {patchResp.Error?.Code} - {patchResp.Message}");

            // ── Put ──
            var putDto = new EventMappingDto
            {
                NameEvent = "E13 Updated Mapping",
                DeviceGroupId = 1,
                CategoryEventMapping = "FENCE_SENSOR_ONLY",
                Description = "E13 Full Update",
                Status = false
            };
            var putResp = await _svc.UpdateEventMappingAsync(id, putDto);
            Assert.True(putResp.Success,
                $"Put failed: {putResp.Error?.Code} - {putResp.Message}");
        }
        finally
        {
            // ── Delete ──
            var delResp = await _svc.DeleteEventMappingAsync(id);
            Assert.True(delResp.Success, $"Delete failed: {delResp.Error?.Code}");
            _output.WriteLine($"Deleted EventMapping ID={id}");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task E14_EventMapping_GetList()
    {
        var resp = await _svc.GetEventMappingsAsync(page: 1, limit: 5);
        Assert.True(resp.Success,
            $"GetList failed: {resp.Error?.Code} - {resp.Message}");
        Assert.NotNull(resp.Data);
        _output.WriteLine($"EventMapping list count={resp.Data.Count}, total={resp.Pagination?.Total}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task E15_EventMapping_NotFound()
    {
        var resp = await _svc.GetEventMappingByIdAsync(999999);
        Assert.False(resp.Success);
        _output.WriteLine($"NotFound: {resp.Error?.Code} - {resp.Message}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task E16_MappingCamera_CRUD_Lifecycle()
    {
        // ── Mapping 먼저 생성 ──
        var mappingDto = new EventMappingDto
        {
            NameEvent = "E16 Camera Mapping",
            DeviceGroupId = 1,
            CategoryEventMapping = "FENCE_SENSOR_ONLY",
            Description = "E16 Camera Test",
            Status = true
        };
        var mappingResp = await _svc.CreateEventMappingAsync(mappingDto);
        Assert.True(mappingResp.Success);
        var mappingId = mappingResp.Data!.Id;
        _output.WriteLine($"Created Mapping ID={mappingId}");

        try
        {
            // ── Create Camera Config ──
            var camDto = new EventMappingCameraDto
            {
                CameraId = 3,
                TargetPresetId = 0,
                HomePresetId = 0,
                DelayTime = 5,
                Priority = 1
            };
            var createResp = await _svc.CreateMappingCameraAsync(mappingId, camDto);
            Assert.True(createResp.Success,
                $"Create Camera failed: {createResp.Error?.Code} - {createResp.Message}. Details: {createResp.Error?.Details}");
            _output.WriteLine($"Created MappingCamera for Mapping={mappingId}");

            // ── Get List ──
            var listResp = await _svc.GetMappingCamerasAsync(mappingId);
            Assert.True(listResp.Success);
            Assert.NotNull(listResp.Data);
            Assert.True(listResp.Data.Count > 0);
            var configId = listResp.Data[0].CameraId;
            _output.WriteLine($"MappingCamera list count={listResp.Data.Count}, first cameraId={configId}");
        }
        finally
        {
            // ── Cleanup: Delete Mapping (cascades to camera configs) ──
            var delResp = await _svc.DeleteEventMappingAsync(mappingId);
            Assert.True(delResp.Success);
            _output.WriteLine($"Deleted Mapping ID={mappingId}");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task E17_MappingSpeaker_CRUD_Lifecycle()
    {
        // ── Mapping 먼저 생성 ──
        var mappingDto = new EventMappingDto
        {
            NameEvent = "E17 Speaker Mapping",
            DeviceGroupId = 1,
            CategoryEventMapping = "FENCE_SENSOR_ONLY",
            Description = "E17 Speaker Test",
            Status = true
        };
        var mappingResp = await _svc.CreateEventMappingAsync(mappingDto);
        Assert.True(mappingResp.Success);
        var mappingId = mappingResp.Data!.Id;
        _output.WriteLine($"Created Mapping ID={mappingId}");

        try
        {
            // ── Create Speaker Config ──
            var spkDto = new EventMappingSpeakerDto
            {
                SpeakerId = 6,
                FileGroupId = 0,
                RepeatCount = 3,
                Priority = 1
            };
            var createResp = await _svc.CreateMappingSpeakerAsync(mappingId, spkDto);
            Assert.True(createResp.Success,
                $"Create Speaker failed: {createResp.Error?.Code} - {createResp.Message}. Details: {createResp.Error?.Details}");
            _output.WriteLine($"Created MappingSpeaker for Mapping={mappingId}");

            // ── Get List ──
            var listResp = await _svc.GetMappingSpeakersAsync(mappingId);
            Assert.True(listResp.Success);
            Assert.NotNull(listResp.Data);
            Assert.True(listResp.Data.Count > 0);
            _output.WriteLine($"MappingSpeaker list count={listResp.Data.Count}");
        }
        finally
        {
            var delResp = await _svc.DeleteEventMappingAsync(mappingId);
            Assert.True(delResp.Success);
            _output.WriteLine($"Deleted Mapping ID={mappingId}");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task E18_MappingLamp_CRUD_Lifecycle()
    {
        // ── Mapping 먼저 생성 ──
        var mappingDto = new EventMappingDto
        {
            NameEvent = "E18 Lamp Mapping",
            DeviceGroupId = 1,
            CategoryEventMapping = "FENCE_SENSOR_ONLY",
            Description = "E18 Lamp Test",
            Status = true
        };
        var mappingResp = await _svc.CreateEventMappingAsync(mappingDto);
        Assert.True(mappingResp.Success);
        var mappingId = mappingResp.Data!.Id;
        _output.WriteLine($"Created Mapping ID={mappingId}");

        try
        {
            // ── Create Lamp Config ──
            var lampDto = new EventMappingLampDto
            {
                EventMappingId = mappingId,
                LampId = 7,
                Color = "Red",
                LightMode = "steady",
                BuzzerSound = "PI-PI-PI",
                BuzzerTime = 5,
                IsEnable = true,
                Priority = 1
            };
            var createResp = await _svc.CreateMappingLampAsync(mappingId, lampDto);
            Assert.True(createResp.Success,
                $"Create Lamp failed: {createResp.Error?.Code} - {createResp.Message}. Details: {createResp.Error?.Details}");
            _output.WriteLine($"Created MappingLamp for Mapping={mappingId}");

            // ── Get List ──
            var listResp = await _svc.GetMappingLampsAsync(mappingId);
            Assert.True(listResp.Success);
            Assert.NotNull(listResp.Data);
            Assert.True(listResp.Data.Count > 0);
            _output.WriteLine($"MappingLamp list count={listResp.Data.Count}");
        }
        finally
        {
            var delResp = await _svc.DeleteEventMappingAsync(mappingId);
            Assert.True(delResp.Success);
            _output.WriteLine($"Deleted Mapping ID={mappingId}");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task E19_EventMapping_WithAllDevices()
    {
        // ── Mapping 생성 ──
        var mappingDto = new EventMappingDto
        {
            NameEvent = "E19 All Devices Mapping",
            DeviceGroupId = 1,
            CategoryEventMapping = "FENCE_SENSOR_ONLY",
            Description = "E19 All Devices Test",
            Status = true
        };
        var mappingResp = await _svc.CreateEventMappingAsync(mappingDto);
        Assert.True(mappingResp.Success);
        var mappingId = mappingResp.Data!.Id;

        try
        {
            // ── Camera 추가 ──
            await _svc.CreateMappingCameraAsync(mappingId, new EventMappingCameraDto
            {
                CameraId = 3, TargetPresetId = 0, HomePresetId = 0, DelayTime = 5, Priority = 1
            });

            // ── Speaker 추가 ──
            await _svc.CreateMappingSpeakerAsync(mappingId, new EventMappingSpeakerDto
            {
                SpeakerId = 6, FileGroupId = 0, RepeatCount = 2, Priority = 1
            });

            // ── Lamp 추가 ──
            await _svc.CreateMappingLampAsync(mappingId, new EventMappingLampDto
            {
                EventMappingId = mappingId, LampId = 7, Color = "Red", LightMode = "steady", BuzzerSound = "PI-PI-PI", BuzzerTime = 5, IsEnable = true, Priority = 1
            });

            // ── 각 서브리소스 목록 조회 ──
            var camList = await _svc.GetMappingCamerasAsync(mappingId);
            Assert.True(camList.Success);
            Assert.NotNull(camList.Data);
            Assert.True(camList.Data.Count > 0, "Camera list should not be empty");

            var spkList = await _svc.GetMappingSpeakersAsync(mappingId);
            Assert.True(spkList.Success);
            Assert.NotNull(spkList.Data);
            Assert.True(spkList.Data.Count > 0, "Speaker list should not be empty");

            var lampList = await _svc.GetMappingLampsAsync(mappingId);
            Assert.True(lampList.Success);
            Assert.NotNull(lampList.Data);
            Assert.True(lampList.Data.Count > 0, "Lamp list should not be empty");

            _output.WriteLine($"Mapping ID={mappingId}, " +
                $"cameras={camList.Data.Count}, " +
                $"speakers={spkList.Data.Count}, " +
                $"lamps={lampList.Data.Count}");
        }
        finally
        {
            await _svc.DeleteEventMappingAsync(mappingId);
            _output.WriteLine($"Deleted Mapping ID={mappingId}");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task E20_EventMapping_DeleteCascade()
    {
        // ── Mapping 생성 + Camera 추가 ──
        var mappingDto = new EventMappingDto
        {
            NameEvent = "E20 Cascade Test",
            DeviceGroupId = 1,
            CategoryEventMapping = "FENCE_SENSOR_ONLY",
            Description = "E20 Cascade Delete Test",
            Status = true
        };
        var mappingResp = await _svc.CreateEventMappingAsync(mappingDto);
        Assert.True(mappingResp.Success);
        var mappingId = mappingResp.Data!.Id;

        await _svc.CreateMappingCameraAsync(mappingId, new EventMappingCameraDto
        {
            CameraId = 1, TargetPresetId = 1, HomePresetId = 0, DelayTime = 5, Priority = 1
        });

        // ── Mapping 삭제 ──
        var delResp = await _svc.DeleteEventMappingAsync(mappingId);
        Assert.True(delResp.Success, $"Delete failed: {delResp.Error?.Code}");
        _output.WriteLine($"Deleted Mapping ID={mappingId}");

        // ── Mapping 조회 → NotFound 확인 ──
        var getResp = await _svc.GetEventMappingByIdAsync(mappingId);
        Assert.False(getResp.Success, "Mapping should be deleted");

        // ── Camera 목록 조회 → 빈 목록 또는 에러 ──
        var camResp = await _svc.GetMappingCamerasAsync(mappingId);
        // 서버가 404 또는 빈 목록 반환 가능
        if (camResp.Success)
        {
            Assert.True(camResp.Data == null || camResp.Data.Count == 0,
                "Camera configs should be cascade deleted");
        }
        _output.WriteLine("Cascade delete verified");
    }
}

#endregion
