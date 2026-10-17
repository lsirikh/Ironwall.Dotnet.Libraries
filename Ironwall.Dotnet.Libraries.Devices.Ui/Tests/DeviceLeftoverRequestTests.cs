using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Groups;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Devices;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 장비 남은 결함(device-leftovers 라이브 왕복, 2026-09-26 서버 8.0.2)의 제품 사슬 고정 — 서버 없이.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>dl.2 — 그룹 등록은 이 클라이언트의 부대를 싣는다(빼면 기본 부대로 가서 이 부대 장비를 넣을 수 없다).</item>
/// <item>dl.2c — 서버가 그룹 넣기를 거부하면 끌어 놓기 결과 줄에 그 까닭이 보인다.</item>
/// <item>dl.3d — 부대 옮기기 본문이 저장된 <c>connection.type</c>(IP_CONVERTER)을 IP_DIRECT 로 덮지 않는다.</item>
/// <item>dl.4 — 설정 없는 팬은 무변경 저장에서 <c>enabled:false</c> 로 지어내지지 않고, 사람이 끄면 <c>false</c> 가 나간다.</item>
/// </list>
/// </remarks>
[Collection("CaliburnIoC")]   // UnitScopeGate 가 정적 IoC 를 읽는다 — 정적 IoC 를 바꾸는 이웃과 직렬화한다.
public class DeviceLeftoverRequestTests
{
    private static JObject Body(object dto) => JObject.Parse(JsonConvert.SerializeObject(dto));

    private const string EnclosureResponse =
        "{ \"id\": 9, \"number_device\": 1, \"name_device\": \"e\", \"status\": \"ACTIVATED\", \"type_enclosure\": \"Outdoor\", "
      + "\"hardware_spec\": { \"schema\": 1, \"components\": [ { \"key\": \"heater_1\", \"type\": \"HEATER\" }, { \"key\": \"fan_1\", \"type\": \"FAN\" } ] }, "
      + "\"device_config\": { \"schema\": 1, \"component_overrides\": { \"heater_1\": { \"enabled\": true } } } }";

    private static EnclosureDeviceModel ModelFromResponse()
        => JsonConvert.DeserializeObject<EnclosureDeviceDto>(EnclosureResponse)!.ToEnclosureDeviceModel();

    #region - dl.4 설정 없는 팬 -
    [Fact]
    public void should_not_send_fan_enabled_when_panel_saves_an_untouched_enclosure_whose_fan_has_no_intent()
    {
        var model = ModelFromResponse();

        var dto = model.ToEnclosureDeviceDto();
        dto.UseAxisWrite = true;
        var overrides = (JObject)Body(dto).SelectToken("device_config.component_overrides")!;

        Assert.False(model.FanEnabledKnown);
        Assert.Equal(true, (bool?)overrides["heater_1"]?["enabled"]);
        Assert.False(overrides.ContainsKey("fan_1"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void should_send_fan_enabled_when_operator_sets_it_on_the_row(bool enabled)
    {
        using var ioc = new TestIoCScope();   // 행 VM 생성자가 IoC.Get<IEventAggregator>() 를 부른다
        var model = ModelFromResponse();
        var row = new EnclosureDeviceViewModel(model) { FanEnabled = enabled };

        var dto = model.ToEnclosureDeviceDto();
        dto.UseAxisWrite = true;

        Assert.True(row.FanEnabled == enabled);
        Assert.Equal(enabled, (bool?)Body(dto).SelectToken("device_config.component_overrides.fan_1.enabled"));
    }

    [Fact]
    public void should_carry_no_intent_through_assign_body_when_enclosure_changes_unit()
    {
        var fetched = JsonConvert.DeserializeObject<EnclosureDeviceDto>(EnclosureResponse)!;

        var body = (EnclosureDeviceDto)UnitAssignRequestBuilder.Build(fetched, 42);

        Assert.True(body.HeaterEnabledKnown);
        Assert.False(body.FanEnabledKnown);
    }
    #endregion

    #region - dl.3d 부대 옮기기의 connection.type -
    [Fact]
    public void should_keep_stored_connection_type_when_assign_body_rebuilds_the_controller_connection()
    {
        var fetched = JsonConvert.DeserializeObject<ControllerDeviceDto>(
            "{ \"id\": 41, \"number_device\": 5, \"name_device\": \"c\", \"status\": \"ACTIVATED\", \"type_controller\": \"Controller\", "
          + "\"connection\": { \"schema\": 1, \"type\": \"IP_CONVERTER\", \"ip_address\": \"10.0.0.7\", \"ip_port\": 9000 } }")!;

        var body = JObject.Parse(JsonConvert.SerializeObject(UnitAssignRequestBuilder.Build(fetched, 42), PresetRequestBuilder.WireSettings));

        Assert.Equal("IP_CONVERTER", (string?)body.SelectToken("connection.type"));
        Assert.Equal("10.0.0.7", (string?)body.SelectToken("connection.ip_address"));
        Assert.Equal(42, (int?)body["unit_id"]);
    }
    #endregion

    #region - dl.2c 거부 까닭 -
    [Fact]
    public async Task should_show_server_reason_when_group_assign_is_refused()
    {
        // 서버 오류 봉투 모양 — top-level message 는 비고, 까닭은 error 에 있다(라이브 422 그대로).
        const string reason = "그룹의 부대(1) 또는 그 예하 부대의 장비만 넣을 수 있습니다 — 다른 부대 장비: [128]";
        var api = new MockDeviceApiService
        {
            AssignHook = (_, _) => new Messages.Defines.Apis.ApiResponse<DeviceGroupAssignResultDto>
            {
                Success = false,
                Error = new Messages.Defines.Apis.ApiError { Code = "VALIDATION_ERROR", Message = reason },
            },
        };
        var devices = new List<IBaseDeviceModel> { new LampDeviceModel { Id = 5, DeviceNumber = 5, DeviceName = "L5", DeviceGroups = new List<int>() } };
        var drop = new DeviceGroupDropHandler(api, () => devices);

        var line = await drop.AssignAsync(9, "G", devices);

        Assert.Contains(reason, line);
    }
    #endregion

    #region - dl.2 그룹 등록의 소속 부대 -
    private sealed class FixedUnitScope : IUnitScopeService
    {
        public FixedUnitScope(bool isUnitEra, int? unitId) { IsUnitEra = isUnitEra; CurrentUnitId = unitId; }
        public bool IsUnitEra { get; }
        public string? UnitCode => "unit-under-test";
        public int? CurrentUnitId { get; }
        public bool IsResolved => true;
        public Task<int?> ResolveAsync(CancellationToken token = default) => Task.FromResult(CurrentUnitId);
        public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;
    }

    private static async Task<DeviceGroupDto> CreateBodyWith(IUnitScopeService? scope)
    {
        var previous = IoC.GetInstance;
        IoC.GetInstance = (type, key) => type == typeof(IUnitScopeService) ? scope! : null!;
        try
        {
            return await DeviceGroupWriteRequest.ForCreateAsync(new DeviceGroupModel { Name = "G", Description = "d" });
        }
        finally { IoC.GetInstance = previous; }
    }

    [Fact]
    public async Task should_stamp_operator_unit_when_creating_group_on_unit_contract()
    {
        var dto = await CreateBodyWith(new FixedUnitScope(isUnitEra: true, unitId: 128));

        Assert.Equal(128, dto.UnitId);
        Assert.Equal("G", dto.Name);
    }

    [Fact]
    public async Task should_not_stamp_unit_when_creating_group_below_unit_contract()
    {
        var dto = await CreateBodyWith(new FixedUnitScope(isUnitEra: false, unitId: 128));

        Assert.Null(dto.UnitId);
    }

    [Fact]
    public async Task should_not_stamp_unit_when_no_unit_scope_is_registered()
    {
        var dto = await CreateBodyWith(null);

        Assert.Null(dto.UnitId);
    }
    #endregion
}
