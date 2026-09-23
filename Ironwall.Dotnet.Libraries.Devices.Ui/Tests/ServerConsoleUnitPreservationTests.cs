using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 서버 콘솔은 <b>고치거나 배정하는 것만으로</b> 서버 · 장비의 소속 부대를 옮기지 않는다.
/// </summary>
/// <remarks>
/// 실서버 왕복(<c>tools/live-api-roundtrip</c> SC-a · SC-b · SC-c)에서 예전 본문이 이랬다 —
/// 수정 <c>{"server_config":…,"unit_id":1}</c> · 배정 <c>{"server_id":18,"unit_id":1}</c> · 되돌리기
/// <c>{"server_id":null,"unit_id":1}</c>. 부대 B(38) 의 서버 · 제어기가 전부 이 클라이언트의 부대(1)로 옮겨졌다.
/// 여기서는 같은 규칙을 서버 없이 <see cref="ServerConsoleService"/> 가 축 통로에 넘기는 값으로 고정한다.
/// </remarks>
public class ServerConsoleUnitPreservationTests
{
    private const int OWN_UNIT = 1;
    private const int OTHER_UNIT = 38;

    #region - Fakes -
    private sealed class FixedProbe : IServerContractProbe
    {
        public FixedProbe(EnumServerContract contract) => Contract = contract;
        public EnumServerContract Contract { get; }
        public string? RawVersion => Contract.ToString();
        public bool IsResolved => true;
        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    }

    private sealed class OwnUnitScope : IUnitScopeService
    {
        public bool IsUnitEra => true;
        public string? UnitCode => "unit001";
        public int? CurrentUnitId => OWN_UNIT;
        public bool IsResolved => true;
        public Task<int?> ResolveAsync(CancellationToken token = default) => Task.FromResult<int?>(OWN_UNIT);
        public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;
    }

    /// <summary>축 통로 — 무엇이 넘어왔는지 적는다. 서버 한 대(부대는 <see cref="FetchedUnit"/>)만 안다.</summary>
    private sealed class RecordingAxis : IServerAxisApiService
    {
        public EnumServerContract Contract => EnumServerContract.V8_0;
        public int? FetchedUnit { get; set; } = OTHER_UNIT;

        public List<(int Id, int? UnitId)> Patches { get; } = new();
        public List<int?> Creates { get; } = new();
        public List<(int DeviceId, int? ServerId, int? UnitId)> Assigns { get; } = new();

        public Task<IReadOnlyList<ServerCategoryView>> GetCategoriesAsync(CancellationToken token = default)
            => Task.FromResult<IReadOnlyList<ServerCategoryView>>(new[] { new ServerCategoryView(1, "프록시", "PROXY") });

        public Task<ServerAxisListResult> GetServersAsync(int? unitId, bool includeDescendants, CancellationToken token = default)
            => Task.FromResult(new ServerAxisListResult(true, Array.Empty<ServerAxisView>(), false, null));

        public Task<ServerAxisView?> GetServerAsync(int id, CancellationToken token = default)
            => Task.FromResult<ServerAxisView?>(new ServerAxisView
            {
                Id = id,
                TypeServer = "PROXY",
                Name = "proxy-b",
                IpAddress = "10.0.0.1",
                Port = 8100,
                UnitId = FetchedUnit,
            });

        public Task<ServerAxisResult> CreateServerAsync(ServerWriteIntent intent, CancellationToken token = default)
        {
            Creates.Add(intent.UnitId);
            return Task.FromResult(new ServerAxisResult(true, string.Empty, 201, 77));
        }

        public Task<ServerAxisResult> PatchServerAsync(int id, ServerWriteIntent intent, CancellationToken token = default)
        {
            Patches.Add((id, intent.UnitId));
            return Task.FromResult(new ServerAxisResult(true, string.Empty, 200, id));
        }

        public Task<ServerAxisResult> AssignDeviceServerAsync(
            EnumDeviceCategory category, int deviceId, int? serverId, int? unitId, CancellationToken token = default)
        {
            Assigns.Add((deviceId, serverId, unitId));
            return Task.FromResult(new ServerAxisResult(true, string.Empty, 200, deviceId));
        }
    }

    private static (ServerConsoleService Service, RecordingAxis Axis) Build()
    {
        var axis = new RecordingAxis();
        var service = new ServerConsoleService(
            axis,
            new ServerApiService(null, null!, new ApiSetupModel()),          // 계측 · 프록시 설정만 쓴다 — 이 테스트는 부르지 않는다
            new DeviceQueryPolicy(new FixedProbe(EnumServerContract.V8_0)),
            units: null,
            unitScope: new Lazy<IUnitScopeService>(() => new OwnUnitScope()));
        return (service, axis);
    }
    #endregion

    #region - 수정 (PATCH /servers/{id}) -
    [Fact]
    public async Task should_carry_the_servers_own_unit_when_saving_a_threshold_edit()
    {
        var (service, axis) = Build();

        var result = await service.SaveAsync(10, new ServerWriteIntent { CpuWarning = 61, CpuCritical = 91 });

        Assert.True(result.IsSuccess);
        var patch = Assert.Single(axis.Patches);
        Assert.Equal(OTHER_UNIT, patch.UnitId);                 // 내 부대(1) 가 아니다
    }

    [Fact]
    public async Task should_put_the_servers_own_unit_on_the_wire_when_saving_a_threshold_edit()
    {
        // 실제로 나갈 본문까지 따라간다 — 뜻(intent)만 보면 쓰기 직렬화가 다시 덮어쓰는 회귀를 놓친다.
        var (service, _) = Build();
        var intent = new ServerWriteIntent { CpuWarning = 61, CpuCritical = 91 };

        await service.SaveAsync(10, intent);
        var body = ServerAxisWriter.BuildPatch(intent, EnumServerContract.V8_0);

        Assert.Equal(OTHER_UNIT, (int?)body["unit_id"]);
    }

    [Fact]
    public async Task should_not_send_a_unit_when_the_fetched_server_has_none()
    {
        var (service, axis) = Build();
        axis.FetchedUnit = null;

        var intent = new ServerWriteIntent { Port = 8200 };
        await service.SaveAsync(10, intent);

        Assert.Null(Assert.Single(axis.Patches).UnitId);        // 미전송 = 그대로 두라(PATCH) — 내 부대로 채우지 않는다
        Assert.Null(ServerAxisWriter.BuildPatch(intent, EnumServerContract.V8_0)["unit_id"]);
    }

    [Fact]
    public async Task should_keep_the_unit_the_operator_picked_when_saving()
    {
        var (service, axis) = Build();

        await service.SaveAsync(10, new ServerWriteIntent { Port = 8200, UnitId = 5 });

        Assert.Equal(5, Assert.Single(axis.Patches).UnitId);
    }
    #endregion

    #region - 등록 (POST /servers) — 의미 불변 -
    [Fact]
    public async Task should_stamp_the_clients_own_unit_when_creating_without_a_chosen_unit()
    {
        var (service, axis) = Build();
        var category = new ServerCategoryOption(1, "프록시", EnumServerType.PROXY, "PROXY");

        var (result, newId) = await service.CreateAsync(category, new ServerWriteIntent { Name = "n", IpAddress = "10.0.0.2", Port = 8101 });

        Assert.True(result.IsSuccess);
        Assert.Equal(77, newId);
        Assert.Equal(OWN_UNIT, Assert.Single(axis.Creates));
    }
    #endregion

    #region - 장비 → 서버 배정 (PATCH /devices/{category}/{id}) -
    [Fact]
    public async Task should_not_send_a_unit_when_assigning_a_device_to_a_server()
    {
        var (service, axis) = Build();

        var result = await service.AssignDeviceAsync(EnumDeviceCategory.Controller, 21, 18);

        Assert.True(result.IsSuccess);
        var assign = Assert.Single(axis.Assigns);
        Assert.Equal(18, assign.ServerId);
        Assert.Null(assign.UnitId);
        Assert.Null(ServerAxisWriter.BuildDeviceAssign(assign.ServerId, assign.UnitId, EnumServerContract.V8_0)["unit_id"]);
    }

    [Fact]
    public async Task should_not_send_a_unit_when_undoing_an_assignment()
    {
        var (service, axis) = Build();

        await service.AssignDeviceAsync(EnumDeviceCategory.Controller, 21, null);

        var assign = Assert.Single(axis.Assigns);
        Assert.Null(assign.ServerId);
        Assert.Null(assign.UnitId);
    }
    #endregion
}
