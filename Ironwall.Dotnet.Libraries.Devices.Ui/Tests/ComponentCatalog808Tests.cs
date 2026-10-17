using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Models;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Presets;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 서버 8.0.8 부품 카탈로그 계약 — 단자 부품 8종 · 카테고리 제한 폐지(<c>applies_to</c> = 즐겨찾기).
/// 같은 시험을 8.0.8 응답과 그 전 서버(6.3.2 · 8.0.6) 응답에 둘 다 돌린다.
/// </summary>
/// <remarks>
/// 카탈로그 모양은 로컬 시험 서버(<c>api-test-server</c> 8.0.8)의 <c>GET /api/devices/spec</c> 을 줄였다 —
/// 전체 카탈로그는 모든 유형을 <c>applies_to</c> 와 함께 준다. 8.0.6 판은 단자 8종이 없다.
/// </remarks>
public class ComponentCatalog808Tests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ironwall-808-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true); }
        catch (IOException) { }
    }

    #region - 판본 판정 -
    [Theory]
    [InlineData("8.0.8", true)]
    [InlineData("8.0.10", true)]
    [InlineData("v8.1.0", true)]
    [InlineData("8.0.8-rc1", true)]
    [InlineData("9.0", true)]
    [InlineData("8.0.7", false)]
    [InlineData("8.0.6", false)]
    [InlineData("6.3.2", false)]
    [InlineData("V8_0", false)]     // 판본을 못 읽으면 종전 동작
    public void should_open_component_catalog_only_from_808(string version, bool expected)
    {
        Assert.Equal(expected, new DeviceQueryPolicy(new VersionProbe(version)).AttachesComponentsAnywhere);
    }

    [Fact]
    public void should_keep_legacy_rules_when_no_probe_is_registered()
    {
        Assert.False(new DeviceQueryPolicy().AttachesComponentsAnywhere);
    }
    #endregion

    #region - 카탈로그 · 팔레트 -
    [Fact]
    public async Task should_read_empty_applies_to_as_favorite_nowhere_when_server_is_808()
    {
        var catalog = await LoadAsync("8.0.8", withTerminals: true);

        var usb = catalog.Find("USB_PORT")!;
        Assert.Empty(usb.AppliesTo!);                                  // [] = 어느 카테고리의 즐겨찾기도 아님
        Assert.True(usb.CanAttachTo(EnumDeviceCategory.Camera));      // 그래도 달 수는 있다
        Assert.DoesNotContain(catalog.ComponentTypes(EnumDeviceCategory.Controller), t => t.Code == "USB_PORT");
        Assert.Contains(catalog.AllComponentTypes(), t => t.Code == "USB_PORT");
    }

    [Fact]
    public async Task should_offer_favorites_and_all_in_palette_when_server_is_808()
    {
        var vm = await OpenAsync(AssemblyViewModel.Compose(EnumDeviceCategory.Controller,
            await LoadAsync("8.0.8", withTerminals: true), NewStore(), new NoDialogs()));

        Assert.True(vm.CanShowAllComponents);
        Assert.True(vm.ShowFavoriteComponents);
        Assert.Equal(new[] { "CONTACT_INPUT", "NETWORK_INTERFACE", "RELAY_OUTPUT", "RS485_PORT" }, Codes(vm));

        vm.ShowAllComponents = true;
        Assert.False(vm.ShowFavoriteComponents);
        Assert.Equal(new[] { "CONTACT_INPUT", "DOOR_SENSOR", "NETWORK_INTERFACE", "PTZ_UNIT", "RELAY_OUTPUT", "RS485_PORT", "USB_PORT" }, Codes(vm));

        vm.AddFromPalette(vm.Palette.Single(p => p.Code == "PTZ_UNIT"));
        Assert.Contains("추가했습니다", vm.StatusText);

        vm.ShowFavoriteComponents = true;
        Assert.DoesNotContain("PTZ_UNIT", Codes(vm));
    }

    [Fact]
    public async Task should_keep_single_palette_when_server_is_806()
    {
        var vm = await OpenAsync(AssemblyViewModel.Compose(EnumDeviceCategory.Controller,
            await LoadAsync("8.0.6", withTerminals: false), NewStore(), new NoDialogs()));

        Assert.False(vm.CanShowAllComponents);
        vm.ShowAllComponents = true;                                   // 단추가 숨어 있어도 목록은 종전 그대로
        Assert.Equal(new[] { "CONTACT_INPUT", "NETWORK_INTERFACE" }, Codes(vm));
    }
    #endregion

    #region - 시드 -
    [Fact]
    public async Task should_seed_controller_with_two_rs485_ports_when_server_is_808()
    {
        var store = NewStore(serialPorts: true);
        var controller = store.Presets.Single(p => p.Id == DevicePresetSeeds.ControllerStandardId);

        Assert.Equal(new[] { "rs485_1", "rs485_2", "nic", "ci_1", "ci_2" }, controller.Components.Select(c => c.Key));
        Assert.Equal(new int?[] { 1, 2 }, controller.Components.Where(c => c.Type == "RS485_PORT").Select(c => c.Channel));
        Assert.Empty(PresetRequestBuilder.Validate(controller, Instance(), await LoadAsync("8.0.8", withTerminals: true)));
    }

    [Fact]
    public void should_keep_old_controller_seed_when_server_is_older_than_808()
    {
        var controller = NewStore().Presets.Single(p => p.Id == DevicePresetSeeds.ControllerStandardId);

        Assert.Equal(new[] { "ci_1", "ci_2", "nic" }, controller.Components.Select(c => c.Key));
    }

    [Fact]
    public void should_swap_untouched_controller_seed_with_server_version()
    {
        NewStore(serialPorts: true);                                    // 8.0.8 에서 처음 심음

        var older = NewStore(serialPorts: false);                        // 같은 파일을 그 전 서버에서 연다
        Assert.Equal(3, older.Presets.Single(p => p.Id == DevicePresetSeeds.ControllerStandardId).Components.Count);

        var newer = NewStore(serialPorts: true);
        Assert.Equal(5, newer.Presets.Single(p => p.Id == DevicePresetSeeds.ControllerStandardId).Components.Count);
    }
    #endregion

    #region - 결선 · 센서 → 제어기 포트 -
    [Fact]
    public async Task should_patch_port_key_when_server_is_808()
    {
        var (board, gateway) = WiringArrange();
        board.Rows[0].Facts = board.Rows[0].Facts with { PortKey = "rs485_2" };

        var result = await new WiringApplyService(gateway, null, null, new DeviceQueryPolicy(new VersionProbe("8.0.8"))).ApplyAsync(10, board);

        Assert.True(result.IsSuccess, result.Message);
        var body = JObject.Parse(Wire(gateway.Patched.Single().Dto));
        Assert.Equal("rs485_2", (string?)body.SelectToken("connection.port_key"));
        Assert.Null(body.SelectToken("connection.type"));               // 병합 PATCH — 다른 접속 칸은 건드리지 않는다
    }

    [Fact]
    public async Task should_not_send_port_key_when_server_is_806()
    {
        var (board, gateway) = WiringArrange();
        board.Rows[0].Facts = board.Rows[0].Facts with { PortKey = "rs485_2" };

        await new WiringApplyService(gateway, null, null, new DeviceQueryPolicy(new VersionProbe("8.0.6"))).ApplyAsync(10, board);

        var body = JObject.Parse(Wire(gateway.Patched.Single().Dto));
        Assert.Null(body.SelectToken("connection.port_key"));           // 그 전 서버는 extra=forbid — 실리면 422
    }

    [Fact]
    public async Task should_not_resend_unchanged_port_key_when_only_name_changed()
    {
        var (board, gateway) = WiringArrange(portKey: "rs485_1");
        board.Rows[0].Facts = board.Rows[0].Facts with { Name = "새 이름" };

        await new WiringApplyService(gateway, null, null, new DeviceQueryPolicy(new VersionProbe("8.0.8"))).ApplyAsync(10, board);

        var body = JObject.Parse(Wire(gateway.Patched.Single().Dto));
        Assert.Null(body.SelectToken("connection"));                     // 이름만 고친 저장이 포트 재검사(422)에 걸리지 않게
    }

    [Fact]
    public async Task should_stop_when_another_user_changed_the_port_meanwhile()
    {
        var (board, gateway) = WiringArrange(portKey: "rs485_1");
        gateway.Fetched[101].ConnectionAxis = new ConnectionAxisDto { PortKey = "rs485_2" };
        board.Rows[0].Facts = board.Rows[0].Facts with { Name = "새 이름" };

        var result = await new WiringApplyService(gateway, null, null, new DeviceQueryPolicy(new VersionProbe("8.0.8"))).ApplyAsync(10, board);

        Assert.True(result.IsConflict);
        Assert.Contains("제어기 포트", result.Message);
        Assert.Empty(gateway.Patched);
    }

    [Fact]
    public async Task should_create_sensor_with_port_key_when_server_is_808()
    {
        var (board, gateway) = WiringArrange();
        board.AddRow(new SensorFacts(2001, "새 센서", "Fence", "북측") { PortKey = "rs485_1" });

        await new WiringApplyService(gateway, null, null, new DeviceQueryPolicy(new VersionProbe("8.0.8"))).ApplyAsync(10, board);

        var body = JObject.Parse(Wire(gateway.Created.Single()));
        Assert.Equal("rs485_1", (string?)body.SelectToken("connection.port_key"));
    }

    [Fact]
    public void should_mark_row_draft_when_port_is_picked_in_the_table()
    {
        var (board, _) = WiringArrange();
        var item = new SensorRowViewModel(board.Rows[0], r => board.PlacementOf(r.Key));

        Assert.Equal("—", item.PortText);
        item.PortKey = "rs485_1";
        item.PortKey = null;                                             // 비우기는 받지 않는다

        Assert.Equal("rs485_1", board.Rows[0].Facts.PortKey);
        Assert.True(item.IsDraft);
    }

    [Fact]
    public void should_read_port_key_from_server_response_without_echoing_it_back()
    {
        var dto = JsonConvert.DeserializeObject<SensorDeviceDto>("""{"id":5,"connection":{"type":"RS485","channel":3,"port_key":"rs485_2"}}""")!;
        Assert.Equal("rs485_2", dto.ReceivedConnection?.PortKey);

        dto.UseAxisWrite = true;
        var body = JObject.Parse(Wire(dto));
        Assert.Null(body.SelectToken("connection.port_key"));           // 받은 DTO 를 고쳐 보내도 port_key 는 다시 실리지 않는다
        Assert.Equal(3, (int?)body.SelectToken("connection.channel"));
    }

    [Fact]
    public void should_show_port_choices_only_when_controller_has_ports()
    {
        var with = WiringViewModel.ForController(new WiringControllerInfo(10, 1, "제어기", "10.0.0.1") { PortKeys = new[] { "rs485_1", "rs485_2" } },
            Array.Empty<WiringSensorSeed>(), new[] { "Fence" }, null, new WiringFakeDialogs());
        var without = WiringViewModel.ForController(new WiringControllerInfo(10, 1, "제어기", "10.0.0.1"),
            Array.Empty<WiringSensorSeed>(), new[] { "Fence" }, null, new WiringFakeDialogs());

        Assert.True(with.HasControllerPorts);
        Assert.Equal(new[] { "rs485_1", "rs485_2" }, with.ControllerPortKeys);
        Assert.False(without.HasControllerPorts);
    }
    #endregion

    #region - 조립 보드 · 프리셋 검증 -
    [Fact]
    public async Task should_attach_a_type_outside_favorites_when_server_is_808()
    {
        var catalog = await LoadAsync("8.0.8", withTerminals: true);
        var board = new AssemblyBoard(EnumDeviceCategory.Controller, catalog.Find);

        Assert.NotNull(board.Add("PTZ_UNIT"));                 // 카메라 즐겨찾기지만 제어기에도 단다(서버 201)
        Assert.NotNull(board.Add("RS485_PORT"));
        Assert.Empty(board.Problems);
        Assert.False(board.HasErrors);
    }

    [Theory]
    [InlineData("6.3.2")]
    [InlineData("8.0.6")]
    public async Task should_keep_category_limit_when_server_is_older_than_808(string version)
    {
        var catalog = await LoadAsync(version, withTerminals: false);
        var board = new AssemblyBoard(EnumDeviceCategory.Controller, catalog.Find);

        Assert.Null(board.Add("PTZ_UNIT"));                    // 종전 그대로 — 그 전 서버는 422(… 전용입니다)
        Assert.Null(board.Add("RS485_PORT"));                  // 카탈로그에 없다
    }

    [Fact]
    public async Task should_expand_a_type_outside_favorites_when_server_is_808()
    {
        var catalog = await LoadAsync("8.0.8", withTerminals: true);
        var board = new AssemblyBoard(EnumDeviceCategory.Camera, catalog.Find);

        var made = board.Expand(new RepeatExpandSpec("RS485_PORT", 2, 1, "rs485_{d}"));

        Assert.Equal(2, made.Count);
    }

    [Fact]
    public async Task should_flag_loaded_foreign_type_only_when_server_is_older_than_808()
    {
        var parts = new[] { new ComponentDefinitionModel { Key = "ptz", Type = "PTZ_UNIT" } };

        var open = new AssemblyBoard(EnumDeviceCategory.Controller, (await LoadAsync("8.0.8", withTerminals: true)).Find);
        open.Load(parts, null);
        Assert.Empty(open.Problems);

        var legacy = new AssemblyBoard(EnumDeviceCategory.Controller, (await LoadAsync("8.0.6", withTerminals: false)).Find);
        legacy.Load(parts, null);
        Assert.Contains(legacy.Problems, p => p.Contains("이 카테고리에 달 수 없습니다", StringComparison.Ordinal));
    }

    [Fact]
    public async Task should_validate_serial_port_controller_preset_when_server_is_808()
    {
        var catalog = await LoadAsync("8.0.8", withTerminals: true);

        var problems = PresetRequestBuilder.Validate(SerialControllerPreset(), Instance(), catalog);

        Assert.Empty(problems);
    }

    [Fact]
    public async Task should_block_serial_port_controller_preset_when_server_is_806()
    {
        var catalog = await LoadAsync("8.0.6", withTerminals: false);

        var problems = PresetRequestBuilder.Validate(SerialControllerPreset(), Instance(), catalog);

        // 종전 그대로 — RS485_PORT 는 그 전 서버 카탈로그에 없어 막힌다 · PTZ_UNIT 은 카테고리로 막힌다.
        Assert.Contains(problems, p => p.Contains("'RS485_PORT'이(가) 목록에 없습니다", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("'PTZ_UNIT' 은(는) 제어기 에 달 수 없습니다", StringComparison.Ordinal));
    }
    #endregion

    #region - Fixture -
    private DevicePresetStore NewStore(bool serialPorts = false)
    {
        var store = new DevicePresetStore(Path.Combine(_directory, "presets.json"), () => new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero))
        {
            SerialPortSeeds = serialPorts,
        };
        store.Load();
        return store;
    }

    private static async Task<AssemblyViewModel> OpenAsync(AssemblyViewModel vm)
    {
        await ((IActivate)vm).ActivateAsync();
        return vm;
    }

    private static string[] Codes(AssemblyViewModel vm) => vm.Palette.Select(p => p.Code).OrderBy(c => c, StringComparer.Ordinal).ToArray();

    private static string Wire(object dto) => JsonConvert.SerializeObject(dto, PresetRequestBuilder.WireSettings);

    /// <summary>센서 1대(1차 1번) — 서버 쪽 DTO 도 같은 값(포트 포함)으로 채운다.</summary>
    private static (WiringBoard Board, WiringFakeGateway Gateway) WiringArrange(string? portKey = null)
    {
        var board = new WiringBoard();
        board.Load(new[]
        {
            (Id: 101, Channel: (int?)1, Facts: WiringDoubles.Facts(1101, 1) with { PortKey = portKey },
             Placement: (WiringPlacement?)new WiringPlacement(1, 1), Issue: (string?)null, Groups: (IReadOnlyList<int>?)null),
        });

        var gateway = new WiringFakeGateway();
        var server = WiringDoubles.ServerSensor(101, 1101, 1, new WiringPlacement(1, 1));
        if (portKey is not null) server.ConnectionAxis = new ConnectionAxisDto { PortKey = portKey };
        gateway.Fetched[101] = server;
        return (board, gateway);
    }

    private sealed class NoDialogs : IAssemblyDialogs
    {
        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(true);
        public Task<string?> AskTextAsync(string title, string label, string initial) => Task.FromResult<string?>(null);
        public Task<RepeatExpandSpec?> AskRepeatExpandAsync(IReadOnlyList<PaletteItemViewModel> palette, PaletteItemViewModel? preselected, IReadOnlyCollection<string> existingKeys)
            => Task.FromResult<RepeatExpandSpec?>(null);
        public Task<int?> OpenRegisterAsync(DevicePreset preset) => Task.FromResult<int?>(null);
    }

    internal static async Task<CatalogService> LoadAsync(string version, bool withTerminals)
    {
        var api = new MockDeviceApiService
        {
            CatalogResponseFactory = () => ApiResponse<DeviceSpecCatalogDto>.CreateSuccess(BuildCatalog(withTerminals)),
        };
        var service = new CatalogService(api, new DeviceQueryPolicy(new VersionProbe(version)), log: null);
        await service.EnsureLoadedAsync();      // 6.3 은 카탈로그를 부르지 않는다(안 읽힌 채 — 종전 그대로)
        return service;
    }

    /// <summary>
    /// 전체 카탈로그(<c>GET /api/devices/spec</c>) 일부 — 8.0.8 시험 서버의 실제 <c>applies_to</c> 그대로.
    /// <c>USB_PORT</c> 는 즐겨찾기 해제로 생기는 <c>[]</c>(어느 카테고리의 즐겨찾기도 아님).
    /// </summary>
    internal static DeviceSpecCatalogDto BuildCatalog(bool withTerminals)
    {
        var rows = new List<(string Code, string Label, string[]? AppliesTo, JObject Definition)>
        {
            ("CONTACT_INPUT", "접점 입력", new[] { "controller", "enclosure", "gate" }, new JObject { ["states"] = new JArray("ON", "OFF") }),
            ("NETWORK_INTERFACE", "네트워크 인터페이스", null, new JObject()),
            ("PTZ_UNIT", "PTZ 구동부", new[] { "camera" }, new JObject { ["states"] = new JArray("IDLE", "RUNNING") }),
            ("DOOR_SENSOR", "도어 센서", new[] { "enclosure", "gate" }, new JObject { ["states"] = new JArray("OPEN", "CLOSED") }),
        };
        if (withTerminals)
        {
            var serial = new JObject { ["controllable"] = false, ["spec"] = new JObject { ["baud_rate"] = new JObject(), ["data_bits"] = new JObject(), ["parity"] = new JObject(), ["stop_bits"] = new JObject() } };
            rows.Add(("RS485_PORT", "RS485 포트", new[] { "controller" }, serial));
            rows.Add(("RELAY_OUTPUT", "접점 출력", new[] { "controller", "enclosure", "gate" },
                new JObject { ["states"] = new JArray("ON", "OFF"), ["commands"] = new JArray("ON", "OFF"), ["controllable"] = true }));
            rows.Add(("USB_PORT", "USB 포트", Array.Empty<string>(), new JObject { ["controllable"] = false }));
        }

        return new DeviceSpecCatalogDto
        {
            Vocabularies = new Dictionary<string, List<VocabularyEntryDto>>
            {
                ["component_type"] = rows.Select(r => new VocabularyEntryDto
                {
                    Code = r.Code,
                    Label = r.Label,
                    AppliesTo = r.AppliesTo?.ToList(),
                    Definition = r.Definition,
                }).ToList(),
            },
        };
    }

    /// <summary>GIS 가 서버에 보낸 제어기 실물 — rs485_1 · rs485_2 · nic · ci_1 · ci_2 + 즐겨찾기 밖 PTZ.</summary>
    private static DevicePreset SerialControllerPreset() => new()
    {
        Id = "p-serial",
        Name = "제어기",
        Category = EnumDeviceCategory.Controller,
        TypeAxisCode = "IoController",
        Components = new List<ComponentDefinitionModel>
        {
            new() { Key = "rs485_1", Type = "RS485_PORT", Channel = 1 },
            new() { Key = "rs485_2", Type = "RS485_PORT", Channel = 2 },
            new() { Key = "nic", Type = "NETWORK_INTERFACE" },
            new() { Key = "ci_1", Type = "CONTACT_INPUT", Channel = 1 },
            new() { Key = "ptz", Type = "PTZ_UNIT" },
        },
    };

    private static PresetInstanceInfo Instance() => new() { DeviceNumber = 1, DeviceName = "제어기 1", IpAddress = "10.0.0.1", IpPort = 8080 };

    /// <summary>서버가 알려 준 판본 문자열(<c>info.version</c>)을 그대로 돌려주는 프로브.</summary>
    internal sealed class VersionProbe : IServerContractProbe
    {
        public VersionProbe(string version)
        {
            RawVersion = version;
            Contract = version.StartsWith("6", StringComparison.Ordinal) ? EnumServerContract.V6_3 : EnumServerContract.V8_0;
        }

        public EnumServerContract Contract { get; }
        public string? RawVersion { get; }
        public bool IsResolved => true;
        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    }
    #endregion
}
