using Autofac;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Api.Modules;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Tests.LiveApi;
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
                  라이브 서버: IRONWALL_LIVE_API_URL(기본 https://localhost:8000/api)
                  + IRONWALL_E2E_CRED_FILE 전용 계정 — 없으면 건너뜀,
                  쓰기 시험은 loopback · 사설망 주소에서만 돈다
                  2026-10-12: 시험 서버엔 시드 자료가 없다(INIT_SAMPLE_DATA=false) — 고정 id(장비 2 · 카메라 3 ·
                  스피커 6 · 경광등 7 · 그룹 1)를 버리고 시험이 장비 · 프리셋을 만들어 쓰고 finally 에서 지운다
****************************************************************************/

#region - Fixture & Collection -

public sealed class EventApiFixture : IAsyncLifetime
{
    internal IEventApiService Service { get; private set; } = null!;
    internal ILogService Log { get; } = new LogService();

    /// <summary>이벤트(탐지 · 장애 · 연결)를 붙일 시험용 제어기 id. 쓰기 환경이 아니거나 만들지 못했으면 0.</summary>
    internal int DeviceId { get; private set; }

    private string? _deviceFailure;

    public async Task InitializeAsync()
    {
        // 라이브 환경이 없으면 모든 시험이 건너뛴다 — 픽스처도 네트워크에 닿지 않는다.
        if (LiveApiTarget.ReadSkipReason is not null) return;

        // 어셈블리 공용 로그인 세션에서 꺼낸다(초기화 · 로그인은 세션이 이미 했다).
        Service = await LiveApiSession.ResolveAsync<IEventApiService>();

        // 쓰기가 허용된 주소에서만 시험 장비를 만든다(원격 운영 주소는 네트워크에 닿기 전에 건너뛴다).
        //   실패해도 픽스처를 깨지 않는다 — 읽기 시험까지 같이 죽지 않게 사유만 들고 있다가 쓰기 시험에서 낸다.
        if (LiveApiTarget.WriteSkipReason is not null) return;
        try
        {
            DeviceId = await LiveEventTestData.CreateControllerAsync();
        }
        catch (Exception ex)
        {
            _deviceFailure = ex.Message;
        }
    }

    /// <summary>시험용 제어기 id — 만들지 못했으면 그 사유로 실패시킨다.</summary>
    internal int RequireDeviceId()
    {
        Assert.True(DeviceId > 0, $"시험용 제어기를 만들지 못했습니다: {_deviceFailure ?? "(사유 없음)"}");
        return DeviceId;
    }

    // 공용 세션은 어셈블리 내내 쓴다 — 여기서 서비스를 멈추거나 컨테이너를 버리지 않는다.
    // 시험 장비만 지운다(이벤트의 device_id 는 SET NULL 이라 남은 이벤트가 있어도 지워진다 — models/event.py:78).
    public Task DisposeAsync()
        => DeviceId > 0 ? LiveEventTestData.DeleteDeviceAsync(LiveEventTestData.Controllers, DeviceId) : Task.CompletedTask;
}

[CollectionDefinition(nameof(EventApiCollection))]
public sealed class EventApiCollection : ICollectionFixture<EventApiFixture> { }

#endregion

#region - Detection Event Tests (E01~E03) -

[Collection(nameof(EventApiCollection))]
[Trait("Scenario", "DEP-206")]
public class DetectionEventApiTests
{
    private readonly EventApiFixture _fixture;
    private readonly IEventApiService _svc;
    private readonly ITestOutputHelper _output;

    public DetectionEventApiTests(EventApiFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _svc = fixture.Service;
        _output = output;
    }

    [LiveApiWriteFact]
    [Trait("Category", "Integration")]
    public async Task E01_Detection_CRUD_Lifecycle()
    {
        // ── Create ──
        var createDto = new DetectionEventDto
        {
            TypeEvent = "Intrusion",
            DeviceId = _fixture.RequireDeviceId(),
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

    [LiveApiFact]
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

    [LiveApiFact]
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
[Trait("Scenario", "DEP-206")]
public class MalfunctionEventApiTests
{
    private readonly EventApiFixture _fixture;
    private readonly IEventApiService _svc;
    private readonly ITestOutputHelper _output;

    public MalfunctionEventApiTests(EventApiFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _svc = fixture.Service;
        _output = output;
    }

    [LiveApiWriteFact]
    [Trait("Category", "Integration")]
    public async Task E04_Malfunction_CRUD_Lifecycle()
    {
        // ── Create ──
        var createDto = new MalfunctionEventDto
        {
            TypeEvent = "Fault",
            DeviceId = _fixture.RequireDeviceId(),
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

    [LiveApiFact]
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
[Trait("Scenario", "DEP-206")]
public class ConnectionEventApiTests
{
    private readonly EventApiFixture _fixture;
    private readonly IEventApiService _svc;
    private readonly ITestOutputHelper _output;

    public ConnectionEventApiTests(EventApiFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _svc = fixture.Service;
        _output = output;
    }

    [LiveApiWriteFact]
    [Trait("Category", "Integration")]
    public async Task E06_Connection_CRUD_Lifecycle()
    {
        // ── Create ──
        var createDto = new ConnectionEventDto
        {
            TypeEvent = "Connection",
            DeviceId = _fixture.RequireDeviceId()
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
            // device_description 은 응답 전용 키라 서버가 무시하고 경고만 남긴다(app/schemas/event.py:177-184).
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

    [LiveApiFact]
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
[Trait("Scenario", "DEP-206")]
public class ActionEventApiTests
{
    private readonly EventApiFixture _fixture;
    private readonly IEventApiService _svc;
    private readonly ITestOutputHelper _output;

    public ActionEventApiTests(EventApiFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _svc = fixture.Service;
        _output = output;
    }

    [LiveApiWriteFact]
    [Trait("Category", "Integration")]
    public async Task E08_Action_CRUD_Lifecycle()
    {
        // ── 먼저 Detection 생성 (Action의 from_event 용) ──
        var detDto = new DetectionEventDto
        {
            TypeEvent = "Intrusion",
            DeviceId = _fixture.RequireDeviceId(),
            Result = "VIBRATION_SENSOR"
        };
        var detResp = await _svc.CreateDetectionEventAsync(detDto);
        Assert.True(detResp.Success, $"Detection create failed: {detResp.Error?.Code} - {detResp.Message}. Details: {detResp.Error?.Details}");
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
                // ── Delete Action ── (원본보다 먼저 — 조치가 달린 원본 삭제는 409, detections.py:634 · 648)
                var delResp = await _svc.DeleteActionEventAsync(id);
                Assert.True(delResp.Success, $"Delete Action failed: {delResp.Error?.Code}");
                _output.WriteLine($"Deleted Action ID={id}");
            }
        }
        finally
        {
            // ── Delete source Detection ──
            var delDet = await _svc.DeleteDetectionEventAsync(detId);
            Assert.True(delDet.Success, $"Delete source Detection failed: {delDet.Error?.Code} - {delDet.Message}");
            _output.WriteLine($"Deleted source Detection ID={detId}");
        }
    }

    [LiveApiFact]
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
[Trait("Scenario", "DEP-206")]
public class EventRelationApiTests
{
    private readonly EventApiFixture _fixture;
    private readonly IEventApiService _svc;
    private readonly ITestOutputHelper _output;

    public EventRelationApiTests(EventApiFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _svc = fixture.Service;
        _output = output;
    }

    [LiveApiWriteFact]
    [Trait("Category", "Integration")]
    public async Task E10_Detection_GetAction()
    {
        // ── Detection 생성 ──
        var detDto = new DetectionEventDto
        {
            TypeEvent = "Intrusion",
            DeviceId = _fixture.RequireDeviceId(),
            Result = "VIBRATION_SENSOR"
        };
        var detResp = await _svc.CreateDetectionEventAsync(detDto);
        Assert.True(detResp.Success, $"Detection create failed: {detResp.Error?.Code} - {detResp.Message}. Details: {detResp.Error?.Details}");
        var detId = detResp.Data!.Id;

        // 원본을 만든 순간부터 finally 가 지운다 — 중간 단계가 실패해도 남기지 않는다.
        try
        {
            // ── Action 생성 (from_event = detId) ──
            var actDto = new ActionEventCreateDto
            {
                TypeEvent = "Action",
                Content = "E10 Action for Detection",
                User = "test_operator",
                FromEventId = detId
            };
            var actResp = await _svc.CreateActionEventAsync(actDto);
            Assert.True(actResp.Success, $"Action create failed: {actResp.Error?.Code} - {actResp.Message}. Details: {actResp.Error?.Details}");
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
                var delAct = await _svc.DeleteActionEventAsync(actId);
                Assert.True(delAct.Success, $"Delete Action failed: {delAct.Error?.Code} - {delAct.Message}");
            }
        }
        finally
        {
            var delDet = await _svc.DeleteDetectionEventAsync(detId);
            Assert.True(delDet.Success, $"Delete Detection failed: {delDet.Error?.Code} - {delDet.Message}");
        }
    }

    [LiveApiWriteFact]
    [Trait("Category", "Integration")]
    public async Task E11_Malfunction_GetAction()
    {
        // ── Malfunction 생성 ──
        var malDto = new MalfunctionEventDto
        {
            TypeEvent = "Fault",
            DeviceId = _fixture.RequireDeviceId(),
            Reason = "FAULT_FENCE",
            Detail = new MalfunctionDetailDto
            {
                FirstStart = 10, FirstEnd = 10,
                SecondStart = 15, SecondEnd = 15
            }
        };
        var malResp = await _svc.CreateMalfunctionEventAsync(malDto);
        Assert.True(malResp.Success, $"Malfunction create failed: {malResp.Error?.Code} - {malResp.Message}. Details: {malResp.Error?.Details}");
        var malId = malResp.Data!.Id;

        try
        {
            // ── Action 생성 (from_event = malId) ──
            var actDto = new ActionEventCreateDto
            {
                TypeEvent = "Action",
                Content = "E11 Action for Malfunction",
                User = "test_operator",
                FromEventId = malId
            };
            var actResp = await _svc.CreateActionEventAsync(actDto);
            Assert.True(actResp.Success, $"Action create failed: {actResp.Error?.Code} - {actResp.Message}. Details: {actResp.Error?.Details}");
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
                var delAct = await _svc.DeleteActionEventAsync(actId);
                Assert.True(delAct.Success, $"Delete Action failed: {delAct.Error?.Code} - {delAct.Message}");
            }
        }
        finally
        {
            var delMal = await _svc.DeleteMalfunctionEventAsync(malId);
            Assert.True(delMal.Success, $"Delete Malfunction failed: {delMal.Error?.Code} - {delMal.Message}");
        }
    }

    [LiveApiFact]
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
[Trait("Scenario", "DEP-206")]
public class EventMappingApiTests
{
    private readonly IEventApiService _svc;
    private readonly ITestOutputHelper _output;

    public EventMappingApiTests(EventApiFixture fixture, ITestOutputHelper output)
    {
        _svc = fixture.Service;
        _output = output;
    }

    /// <summary>
    /// 시험용 매핑을 만든다. 장비그룹은 싣지 않는다 — 선택 필드이고(integration.py:125),
    /// 시드가 없는 서버에서 고정 그룹 id 는 404 다.
    /// </summary>
    private async Task<int> CreateMappingAsync(string label)
    {
        var resp = await _svc.CreateEventMappingAsync(new EventMappingDto
        {
            NameEvent = $"{label} {Guid.NewGuid().ToString("N")[..8]}",   // 재실행 구분용 꼬리(≤100자 — integration.py:124)
            CategoryEventMapping = "FENCE_SENSOR_ONLY",
            Description = $"{label} Test",
            Status = true
        });
        Assert.True(resp.Success,
            $"Mapping create failed: {resp.Error?.Code} - {resp.Message}. Details: {resp.Error?.Details}");
        var id = resp.Data!.Id;
        Assert.True(id > 0);
        _output.WriteLine($"Created Mapping ID={id}");
        return id;
    }

    /// <summary>매핑을 지운 결과를 돌려준다(단언은 장비 정리 뒤에 한다 — 단언이 장비 삭제를 건너뛰지 않게).</summary>
    private async Task<string?> DeleteMappingAsync(int mappingId)
    {
        if (mappingId <= 0) return null;
        var del = await _svc.DeleteEventMappingAsync(mappingId);
        _output.WriteLine($"Deleted Mapping ID={mappingId}: {del.Success}");
        return del.Success ? null : $"Delete mapping {mappingId} failed: {del.Error?.Code} - {del.Message}";
    }

    [LiveApiWriteFact]
    [Trait("Category", "Integration")]
    public async Task E13_EventMapping_CRUD_Lifecycle()
    {
        // ── Create ──
        var createDto = new EventMappingDto
        {
            NameEvent = "E13 Test Mapping",
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

            // ── Patch ── (보낸 키만 바뀐다 — event_mappings.py:299)
            var patchDto = new EventMappingDto { Description = "E13 Patched" };
            var patchResp = await _svc.PatchEventMappingAsync(id, patchDto);
            Assert.True(patchResp.Success,
                $"Patch failed: {patchResp.Error?.Code} - {patchResp.Message}. Details: {patchResp.Error?.Details}");
            Assert.Equal("E13 Patched", patchResp.Data!.Description);
            Assert.Equal("E13 Test Mapping", patchResp.Data.NameEvent);
            Assert.True(patchResp.Data.Status);

            // ── Put ──
            var putDto = new EventMappingDto
            {
                NameEvent = "E13 Updated Mapping",
                CategoryEventMapping = "FENCE_SENSOR_ONLY",
                Description = "E13 Full Update",
                Status = false
            };
            var putResp = await _svc.UpdateEventMappingAsync(id, putDto);
            Assert.True(putResp.Success,
                $"Put failed: {putResp.Error?.Code} - {putResp.Message}. Details: {putResp.Error?.Details}");
            Assert.Equal("E13 Updated Mapping", putResp.Data!.NameEvent);
            Assert.Equal("E13 Full Update", putResp.Data.Description);
            Assert.False(putResp.Data.Status);

            // ── Get after Put ──
            var afterPut = await _svc.GetEventMappingByIdAsync(id);
            Assert.True(afterPut.Success);
            Assert.Equal("E13 Updated Mapping", afterPut.Data!.NameEvent);
            Assert.False(afterPut.Data.Status);
        }
        finally
        {
            // ── Delete ──
            var delResp = await _svc.DeleteEventMappingAsync(id);
            Assert.True(delResp.Success, $"Delete failed: {delResp.Error?.Code}");
            _output.WriteLine($"Deleted EventMapping ID={id}");
        }
    }

    [LiveApiFact]
    [Trait("Category", "Integration")]
    public async Task E14_EventMapping_GetList()
    {
        var resp = await _svc.GetEventMappingsAsync(page: 1, limit: 5);
        Assert.True(resp.Success,
            $"GetList failed: {resp.Error?.Code} - {resp.Message}");
        Assert.NotNull(resp.Data);
        _output.WriteLine($"EventMapping list count={resp.Data.Count}, total={resp.Pagination?.Total}");
    }

    [LiveApiFact]
    [Trait("Category", "Integration")]
    public async Task E15_EventMapping_NotFound()
    {
        var resp = await _svc.GetEventMappingByIdAsync(999999);
        Assert.False(resp.Success);
        _output.WriteLine($"NotFound: {resp.Error?.Code} - {resp.Message}");
    }

    [LiveApiWriteFact]
    [Trait("Category", "Integration")]
    public async Task E16_MappingCamera_CRUD_Lifecycle()
    {
        int cameraId = 0, mappingId = 0;
        string? mappingCleanup = null;
        try
        {
            // ── 카메라 · 실제 프리셋 2개 (target/home 은 그 카메라 소유여야 한다 — event_mapping_cameras.py:63-84) ──
            cameraId = await LiveEventTestData.CreateCameraAsync();
            var targetPresetId = await LiveEventTestData.CreatePresetAsync(cameraId, 1);
            var homePresetId = await LiveEventTestData.CreatePresetAsync(cameraId, 2);

            // ── Mapping 생성 ──
            mappingId = await CreateMappingAsync("E16 Camera Mapping");

            // ── Create Camera Config ──
            var camDto = new EventMappingCameraDto
            {
                CameraId = cameraId,
                TargetPresetId = targetPresetId,
                HomePresetId = homePresetId,
                DelayTime = 5,
                Priority = 1
            };
            var createResp = await _svc.CreateMappingCameraAsync(mappingId, camDto);
            Assert.True(createResp.Success,
                $"Create Camera failed: {createResp.Error?.Code} - {createResp.Message}. Details: {createResp.Error?.Details}");
            _output.WriteLine($"Created MappingCamera for Mapping={mappingId}");

            // ── Get List ──
            var listResp = await _svc.GetMappingCamerasAsync(mappingId);
            Assert.True(listResp.Success, $"List failed: {listResp.Error?.Code} - {listResp.Message}");
            Assert.NotNull(listResp.Data);
            Assert.Single(listResp.Data);
            _output.WriteLine($"MappingCamera list count={listResp.Data.Count}");
        }
        finally
        {
            // ── Cleanup: 매핑 먼저(카메라 config 연쇄 삭제), 그다음 카메라(프리셋 연쇄 삭제) ──
            mappingCleanup = await DeleteMappingAsync(mappingId);
            await LiveEventTestData.DeleteDeviceAsync(LiveEventTestData.Cameras, cameraId);
        }
        Assert.Null(mappingCleanup);
    }

    [LiveApiWriteFact]
    [Trait("Category", "Integration")]
    public async Task E17_MappingSpeaker_CRUD_Lifecycle()
    {
        int speakerId = 0, mappingId = 0;
        string? mappingCleanup = null;
        try
        {
            speakerId = await LiveEventTestData.CreateSpeakerAsync();
            mappingId = await CreateMappingAsync("E17 Speaker Mapping");

            // ── Create Speaker Config ──
            // FileGroupId 0 = 음원 미지정 — 서비스가 본문에서 뺀다(L7, 서버는 0 을 실제 id 로 찾아 404 였다).
            var spkDto = new EventMappingSpeakerDto
            {
                SpeakerId = speakerId,
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
            Assert.True(listResp.Success, $"List failed: {listResp.Error?.Code} - {listResp.Message}");
            Assert.NotNull(listResp.Data);
            var row = Assert.Single(listResp.Data);
            Assert.Equal(3, row.RepeatCount);
            _output.WriteLine($"MappingSpeaker list count={listResp.Data.Count}");
        }
        finally
        {
            mappingCleanup = await DeleteMappingAsync(mappingId);
            await LiveEventTestData.DeleteDeviceAsync(LiveEventTestData.Speakers, speakerId);
        }
        Assert.Null(mappingCleanup);
    }

    [LiveApiWriteFact]
    [Trait("Category", "Integration")]
    public async Task E18_MappingLamp_CRUD_Lifecycle()
    {
        int lampId = 0, mappingId = 0;
        string? mappingCleanup = null;
        try
        {
            lampId = await LiveEventTestData.CreateLampAsync();
            mappingId = await CreateMappingAsync("E18 Lamp Mapping");

            // ── Create Lamp Config ──
            var lampDto = new EventMappingLampDto
            {
                EventMappingId = mappingId,   // 보내면 경로와 같아야 한다(integration.py:416-420)
                LampId = lampId,
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
            Assert.True(listResp.Success, $"List failed: {listResp.Error?.Code} - {listResp.Message}");
            Assert.NotNull(listResp.Data);
            var row = Assert.Single(listResp.Data);
            Assert.Equal("Red", row.Color);
            _output.WriteLine($"MappingLamp list count={listResp.Data.Count}");
        }
        finally
        {
            mappingCleanup = await DeleteMappingAsync(mappingId);
            await LiveEventTestData.DeleteDeviceAsync(LiveEventTestData.Lamps, lampId);
        }
        Assert.Null(mappingCleanup);
    }

    [LiveApiWriteFact]
    [Trait("Category", "Integration")]
    public async Task E19_EventMapping_WithAllDevices()
    {
        int cameraId = 0, speakerId = 0, lampId = 0, mappingId = 0;
        string? mappingCleanup = null;
        try
        {
            // ── 장비 준비 ──
            cameraId = await LiveEventTestData.CreateCameraAsync();
            var targetPresetId = await LiveEventTestData.CreatePresetAsync(cameraId, 1);
            var homePresetId = await LiveEventTestData.CreatePresetAsync(cameraId, 2);
            speakerId = await LiveEventTestData.CreateSpeakerAsync();
            lampId = await LiveEventTestData.CreateLampAsync();

            // ── Mapping 생성 ──
            mappingId = await CreateMappingAsync("E19 All Devices Mapping");

            // ── Camera 추가 ──
            var camResp = await _svc.CreateMappingCameraAsync(mappingId, new EventMappingCameraDto
            {
                CameraId = cameraId, TargetPresetId = targetPresetId, HomePresetId = homePresetId, DelayTime = 5, Priority = 1
            });
            Assert.True(camResp.Success, $"Create Camera failed: {camResp.Error?.Code} - {camResp.Message}. Details: {camResp.Error?.Details}");

            // ── Speaker 추가 ──
            var spkResp = await _svc.CreateMappingSpeakerAsync(mappingId, new EventMappingSpeakerDto
            {
                SpeakerId = speakerId, FileGroupId = 0, RepeatCount = 2, Priority = 1
            });
            Assert.True(spkResp.Success, $"Create Speaker failed: {spkResp.Error?.Code} - {spkResp.Message}. Details: {spkResp.Error?.Details}");

            // ── Lamp 추가 ──
            var lampResp = await _svc.CreateMappingLampAsync(mappingId, new EventMappingLampDto
            {
                EventMappingId = mappingId, LampId = lampId, Color = "Red", LightMode = "steady", BuzzerSound = "PI-PI-PI", BuzzerTime = 5, IsEnable = true, Priority = 1
            });
            Assert.True(lampResp.Success, $"Create Lamp failed: {lampResp.Error?.Code} - {lampResp.Message}. Details: {lampResp.Error?.Details}");

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
            // 매핑 먼저(하위 config 연쇄 삭제), 그다음 장비
            mappingCleanup = await DeleteMappingAsync(mappingId);
            await LiveEventTestData.DeleteDeviceAsync(LiveEventTestData.Cameras, cameraId);
            await LiveEventTestData.DeleteDeviceAsync(LiveEventTestData.Speakers, speakerId);
            await LiveEventTestData.DeleteDeviceAsync(LiveEventTestData.Lamps, lampId);
        }
        Assert.Null(mappingCleanup);
    }

    [LiveApiWriteFact]
    [Trait("Category", "Integration")]
    public async Task E20_EventMapping_DeleteCascade()
    {
        int cameraId = 0, mappingId = 0;
        var mappingDeleted = false;
        string? mappingCleanup = null;
        try
        {
            // ── Mapping 생성 + Camera 추가 ──
            cameraId = await LiveEventTestData.CreateCameraAsync();
            var targetPresetId = await LiveEventTestData.CreatePresetAsync(cameraId, 1);
            var homePresetId = await LiveEventTestData.CreatePresetAsync(cameraId, 2);
            mappingId = await CreateMappingAsync("E20 Cascade Test");

            var camResp = await _svc.CreateMappingCameraAsync(mappingId, new EventMappingCameraDto
            {
                CameraId = cameraId, TargetPresetId = targetPresetId, HomePresetId = homePresetId, DelayTime = 5, Priority = 1
            });
            Assert.True(camResp.Success, $"Create Camera failed: {camResp.Error?.Code} - {camResp.Message}. Details: {camResp.Error?.Details}");

            // ── Mapping 삭제 ──
            var delResp = await _svc.DeleteEventMappingAsync(mappingId);
            Assert.True(delResp.Success, $"Delete failed: {delResp.Error?.Code}");
            mappingDeleted = true;
            _output.WriteLine($"Deleted Mapping ID={mappingId}");

            // ── Mapping 조회 → NotFound 확인 ──
            var getResp = await _svc.GetEventMappingByIdAsync(mappingId);
            Assert.False(getResp.Success, "Mapping should be deleted");

            // ── Camera 목록 조회 → 빈 목록 또는 에러 ──
            var camList = await _svc.GetMappingCamerasAsync(mappingId);
            // 서버가 404 또는 빈 목록 반환 가능
            if (camList.Success)
            {
                Assert.True(camList.Data == null || camList.Data.Count == 0,
                    "Camera configs should be cascade deleted");
            }
            _output.WriteLine("Cascade delete verified");
        }
        finally
        {
            if (!mappingDeleted) mappingCleanup = await DeleteMappingAsync(mappingId);
            await LiveEventTestData.DeleteDeviceAsync(LiveEventTestData.Cameras, cameraId);
        }
        Assert.Null(mappingCleanup);
    }
}

#endregion
