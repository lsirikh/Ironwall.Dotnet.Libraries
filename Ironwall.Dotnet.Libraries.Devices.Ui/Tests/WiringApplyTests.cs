using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 결선 저장(device-wiring-setup FR-27 ~ FR-31) — 계약 가드 · 재조회 비교 · 본문 감사 · 부분 실패.
/// </summary>
/// <remarks>
/// 본문 감사가 이 묶음의 핵심이다 — PATCH 는 RFC 7396 병합이라 <b>비운 칸이 곧 삭제</b>다.
/// 부품 배열(<c>components</c>)은 서버가 통째로 바꾸므로 결선 저장 본문에 <b>실리면 안 된다</b>.
/// <para><b>정적 IoC 는 건드리지 않는다</b> — <see cref="UnitScopeGate"/> 의 <c>unit_id</c> 스탬프는
/// 그 관문 자체의 시험(<c>PresetRegisterTests</c> 의 unit_id 관문 절)이 이미 못 박고 있고, 여기서 델리게이트를
/// 갈아 끼우면 나란히 도는 다른 테스트 클래스가 무작위로 깨진다(<see cref="TestIoCScope"/> 설명 참고).
/// 이 묶음은 "부대 서비스가 없으면 키가 나가지 않는다"만 확인한다.</para>
/// </remarks>
public class WiringApplyTests
{
    #region - Guard · no-op -
    [Fact]
    public async Task should_send_nothing_when_the_server_contract_has_no_place_for_wiring()
    {
        var (board, gateway) = Arrange();
        MovePlacement(board);
        var service = new WiringApplyService(gateway, null, null, LegacyPolicy());

        var result = await service.ApplyAsync(10, board);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsBlocked);
        Assert.Equal(0, gateway.GetCount);              // 다시 받기조차 하지 않는다
        Assert.Equal(0, gateway.PatchCount);
    }

    [Fact]
    public async Task should_send_nothing_when_no_row_changed()
    {
        var (board, gateway) = Arrange();

        var result = await Service(gateway).ApplyAsync(10, board);

        Assert.False(result.IsSuccess);
        Assert.Contains("바뀐 줄이 없습니다", result.Message);
        Assert.Equal(0, gateway.PatchCount);
    }

    [Fact]
    public async Task should_refuse_when_the_controller_is_not_chosen()
    {
        var (board, gateway) = Arrange();
        MovePlacement(board);

        var result = await Service(gateway).ApplyAsync(0, board);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, gateway.PatchCount);
    }
    #endregion

    #region - Conflict -
    [Fact]
    public async Task should_send_nothing_when_another_user_changed_the_wiring_meanwhile()
    {
        var (board, gateway) = Arrange();
        MovePlacement(board);
        gateway.Fetched[101].HardwareSpec!.Spec = WiringSpec.Apply(null, new WiringPlacement(2, 5));

        var result = await Service(gateway).ApplyAsync(10, board);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsConflict);
        Assert.Contains("다른 사람이", result.Message);
        Assert.Equal(0, gateway.PatchCount);
    }

    [Fact]
    public async Task should_send_nothing_when_a_sensor_cannot_be_fetched_again()
    {
        var (board, gateway) = Arrange();
        MovePlacement(board);
        gateway.FetchFails.Add(101);

        var result = await Service(gateway).ApplyAsync(10, board);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, gateway.PatchCount);
    }
    #endregion

    #region - Body audit -
    [Fact]
    public async Task should_never_null_or_reset_a_fetched_value_in_the_patch_body()
    {
        var (board, gateway) = Arrange();
        MovePlacement(board);

        var result = await Service(gateway).ApplyAsync(10, board);
        Assert.True(result.IsSuccess);

        var sent = gateway.Patched.First(p => p.Id == 101);
        var body = JObject.Parse(Wire(sent.Dto));

        // (a) 본문에 null 은 하나도 없다 — PATCH 에서 null 은 삭제다.
        Assert.DoesNotContain(body.Descendants().OfType<JProperty>(), p => p.Value.Type == JTokenType.Null);

        // (b) hardware_spec · unit_id 밖의 키는 "아무것도 바꾸지 않는 본문" 과 같다.
        var fetched = gateway.Fetched[101];
        fetched.UseAxisWrite = true;
        var unchanged = JObject.Parse(Wire(fetched));

        foreach (var property in body.Properties())
        {
            if (property.Name is "hardware_spec" or "unit_id") continue;
            Assert.True(JToken.DeepEquals(property.Value, unchanged[property.Name]),
                        $"{property.Name}: {property.Value} ≠ {unchanged[property.Name]}");
        }

        // (c) 우리가 바꾸는 것은 spec.wiring 한 칸뿐이다.
        Assert.Equal(1, (int?)body.SelectToken("hardware_spec.spec.wiring.line"));
        Assert.Equal(2, (int?)body.SelectToken("hardware_spec.spec.wiring.order"));
    }

    [Fact]
    public async Task should_not_send_components_when_saving_wiring()
    {
        var (board, gateway) = Arrange();
        MovePlacement(board);

        await Service(gateway).ApplyAsync(10, board);

        var body = JObject.Parse(Wire(gateway.Patched.First().Dto));
        Assert.Null(body.SelectToken("hardware_spec.components"));      // 서버가 배열을 통째로 바꾼다 — 실으면 남의 부품이 지워진다
    }

    [Fact]
    public async Task should_keep_other_spec_keys_when_saving_wiring()
    {
        var (board, gateway) = Arrange();
        gateway.Fetched[101].HardwareSpec!.Spec = JObject.Parse("""{"detection_range":120,"wiring":{"line":1,"order":1}}""");
        MovePlacement(board);

        await Service(gateway).ApplyAsync(10, board);

        var body = JObject.Parse(Wire(gateway.Patched.First().Dto));
        Assert.Equal(120, (int?)body.SelectToken("hardware_spec.spec.detection_range"));
    }

    [Fact]
    public async Task should_not_send_controller_id_when_patching_an_existing_sensor()
    {
        var (board, gateway) = Arrange();
        MovePlacement(board);

        await Service(gateway).ApplyAsync(10, board);

        var body = JObject.Parse(Wire(gateway.Patched.First().Dto));
        Assert.Null(body["controller_id"]);          // 0 이 실리면 서버가 404 를 돌려준다
    }

    [Fact]
    public async Task should_omit_the_type_axis_when_the_type_did_not_change()
    {
        var (board, gateway) = Arrange();
        MovePlacement(board);

        await Service(gateway).ApplyAsync(10, board);

        var body = JObject.Parse(Wire(gateway.Patched.First().Dto));
        Assert.Null(body["type_sensor"]);
        Assert.Null(body["type_device"]);
    }

    [Fact]
    public async Task should_send_the_type_axis_when_the_type_changed()
    {
        var (board, gateway) = Arrange();
        board.Rows[0].Facts = board.Rows[0].Facts with { TypeText = "PIR" };

        await Service(gateway).ApplyAsync(10, board);

        var body = JObject.Parse(Wire(gateway.Patched.First().Dto));
        Assert.Equal("PIR", (string?)body["type_sensor"]);
    }

    [Fact]
    public async Task should_carry_the_coordinates_when_only_the_zone_changed()
    {
        var (board, gateway) = Arrange();
        board.Rows[0].Facts = board.Rows[0].Facts with { Zone = "정문 초소" };

        await Service(gateway).ApplyAsync(10, board);

        var body = JObject.Parse(Wire(gateway.Patched.First().Dto));
        Assert.Equal("정문 초소", (string?)body.SelectToken("geolocation.location"));
        Assert.Equal(37.5, (double?)body.SelectToken("geolocation.latitude"));
        Assert.Equal(127.5, (double?)body.SelectToken("geolocation.longitude"));
    }

    [Fact]
    public async Task should_skip_the_zone_and_say_so_when_the_device_has_no_coordinates()
    {
        var (board, gateway) = Arrange();
        gateway.Fetched[101].Geolocation = null;
        board.Rows[0].Facts = board.Rows[0].Facts with { Zone = "정문 초소" };

        var result = await Service(gateway).ApplyAsync(10, board);

        var body = JObject.Parse(Wire(gateway.Patched.First().Dto));
        Assert.Null(body["geolocation"]);            // 0,0 으로 박는 것보다 저장하지 않는 편이 낫다
        Assert.Contains("구역은", result.Rows[0].Message);
    }
    #endregion

    #region - Create · counts · partial failure -
    [Fact]
    public async Task should_create_with_the_wiring_in_one_call_when_the_row_is_new()
    {
        var (board, gateway) = Arrange();
        var row = board.AddRow(new SensorFacts(1301, "새 센서", "Fence", "북측 7구간"));
        board.Place(row.Key, 2, 0);

        var result = await Service(gateway).ApplyAsync(10, board);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, gateway.CreateCount);
        Assert.Equal(0, gateway.PatchCount);

        var body = JObject.Parse(Wire(gateway.Created.Single()));
        Assert.Equal(1301, (int?)body["number_device"]);
        Assert.Equal(10, (int?)body["controller_id"]);
        Assert.Equal("Fence", (string?)body["type_sensor"]);
        Assert.Equal(2, (int?)body.SelectToken("hardware_spec.spec.wiring.line"));
        Assert.Equal(1, (int?)body.SelectToken("hardware_spec.spec.wiring.order"));
    }

    [Fact]
    public async Task should_call_once_per_changed_sensor_when_saving()
    {
        var (board, gateway) = Arrange();
        MoveAllToSecondLine(board);          // 세 줄 모두 자리가 바뀐다

        var result = await Service(gateway).ApplyAsync(10, board);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, gateway.PatchCount);
        Assert.Equal(3, gateway.GetCount);
        Assert.Equal(3, result.OkCount);
    }

    [Fact]
    public async Task should_keep_the_failed_rows_when_some_calls_fail()
    {
        var (board, gateway) = Arrange();
        MoveAllToSecondLine(board);
        gateway.PatchFails.Add(102);

        var result = await Service(gateway).ApplyAsync(10, board);

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(2, result.OkKeys.Count);
        Assert.DoesNotContain(102, result.OkKeys);
        Assert.Contains("실패", result.Message);
    }

    [Fact]
    public async Task should_report_progress_for_every_row_when_saving()
    {
        var (board, gateway) = Arrange();
        MoveAllToSecondLine(board);
        var seen = new List<WiringProgress>();

        await Service(gateway).ApplyAsync(10, board, new Progress<WiringProgress>(seen.Add));

        Assert.All(seen, p => Assert.Equal(3, p.Total));
    }

    [Fact]
    public async Task should_reload_devices_once_when_anything_was_saved()
    {
        var (board, gateway) = Arrange();
        MovePlacement(board);
        var provider = new CountingProvider();

        await new WiringApplyService(gateway, provider, null, AxisPolicy()).ApplyAsync(10, board);

        Assert.Equal(1, provider.FetchCount);
    }
    #endregion

    #region - Unit scope -
    [Fact]
    public async Task should_not_stamp_the_unit_when_no_unit_service_is_registered()
    {
        var (board, gateway) = Arrange();
        MovePlacement(board);

        await Service(gateway).ApplyAsync(10, board);

        Assert.Null(gateway.Patched.First().Dto.UnitId);
    }
    #endregion

    #region - Helpers -
    private static string Wire(object dto) => JsonConvert.SerializeObject(dto, PresetRequestBuilder.WireSettings);

    private static WiringApplyService Service(FakeGateway gateway) => new(gateway, null, null, AxisPolicy());

    private static DeviceQueryPolicy AxisPolicy() => new(new FixedProbe(EnumServerContract.V8_0));
    private static DeviceQueryPolicy LegacyPolicy() => new(new FixedProbe(EnumServerContract.V6_3));

    /// <summary>센서 3대(1차 1·2·3번). 서버 쪽 DTO 도 같은 자리로 채워 둔다.</summary>
    private static (WiringBoard Board, FakeGateway Gateway) Arrange()
    {
        var board = new WiringBoard();
        board.Load(Enumerable.Range(0, 3).Select(i => (
            Id: 101 + i,
            Channel: (int?)(i + 1),
            Facts: new SensorFacts(1101 + i, $"북측 {i + 1}구간 펜스", "Fence", "북측 7구간"),
            Placement: (WiringPlacement?)new WiringPlacement(1, i + 1),
            Issue: (string?)null)));

        var gateway = new FakeGateway();
        for (var i = 0; i < 3; i++)
        {
            gateway.Fetched[101 + i] = new SensorDeviceDto
            {
                Id = 101 + i,
                NumberDevice = 1101 + i,
                NameDevice = $"북측 {i + 1}구간 펜스",
                TypeDevice = "Fence",
                Status = "ACTIVATED",
                IsEnable = true,
                ControllerId = 10,
                Geolocation = new GeolocationDto { Location = "북측 7구간", Latitude = 37.5, Longitude = 127.5 },
                HardwareSpec = new HardwareSpecDto
                {
                    Manufacturer = "Sensorway",
                    Spec = WiringSpec.Apply(null, new WiringPlacement(1, i + 1)),
                    Components = new List<ComponentDefinitionDto> { new() { Key = "vib", Type = "VIBRATION_SENSOR" } },
                },
            };
        }
        return (board, gateway);
    }

    /// <summary>첫 센서를 2차 선으로 옮긴다 — 한 줄만 바뀌게.</summary>
    private static void MovePlacement(WiringBoard board)
    {
        var first = board.Rows[0];
        board.Unplace(board.Rows[1].Key);
        board.Unplace(board.Rows[2].Key);
        board.Place(first.Key, 1, 1);        // 순번 1 → 2 (칸만 옮긴다)
        board.Place(board.Rows[1].Key, 1, 0);
        board.Place(board.Rows[2].Key, 1, 2);
    }

    /// <summary>세 줄 모두 2차 선으로 — 한 번에 세 건이 나가는 경우를 만든다.</summary>
    private static void MoveAllToSecondLine(WiringBoard board)
    {
        for (var i = 0; i < board.Rows.Count; i++) board.Place(board.Rows[i].Key, 2, i);
    }

    private sealed class FixedProbe : IServerContractProbe
    {
        public FixedProbe(EnumServerContract contract) => Contract = contract;
        public EnumServerContract Contract { get; }
        public string? RawVersion => Contract.ToString();
        public bool IsResolved => true;
        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    }

    /// <summary>보낸 DTO 를 그대로 붙잡는 가짜 통로 — 본문 감사가 이것을 직렬화한다.</summary>
    private sealed class FakeGateway : ISensorWriteGateway
    {
        public Dictionary<int, SensorDeviceDto> Fetched { get; } = new();
        public List<(int Id, SensorDeviceDto Dto)> Patched { get; } = new();
        public List<SensorDeviceDto> Created { get; } = new();
        public HashSet<int> FetchFails { get; } = new();
        public HashSet<int> PatchFails { get; } = new();

        public int GetCount { get; private set; }
        public int PatchCount => Patched.Count;
        public int CreateCount => Created.Count;

        public Task<ApiResponse<SensorDeviceDto>> GetAsync(int id, CancellationToken token = default)
        {
            GetCount++;
            if (FetchFails.Contains(id) || !Fetched.TryGetValue(id, out var dto))
                return Task.FromResult(ApiResponse<SensorDeviceDto>.CreateError("NOT_FOUND", "없는 장비"));
            return Task.FromResult(ApiResponse<SensorDeviceDto>.CreateSuccess(dto));
        }

        public Task<ApiResponse<SensorDeviceDto>> CreateAsync(SensorDeviceDto dto, CancellationToken token = default)
        {
            Created.Add(dto);
            return Task.FromResult(ApiResponse<SensorDeviceDto>.CreateSuccess(new SensorDeviceDto { Id = 900 + Created.Count }));
        }

        public Task<ApiResponse<SensorDeviceDto>> PatchAsync(int id, SensorDeviceDto dto, CancellationToken token = default)
        {
            Patched.Add((id, dto));
            if (PatchFails.Contains(id))
                return Task.FromResult(ApiResponse<SensorDeviceDto>.CreateError("CONSTRAINT", "저장 실패"));
            return Task.FromResult(ApiResponse<SensorDeviceDto>.CreateSuccess(dto));
        }
    }

    private sealed class CountingProvider : MockDeviceProviderService, IDeviceProviderService
    {
        public int FetchCount { get; private set; }

        public new Task FetchAllDevicesAsync(CancellationToken token = default)
        {
            FetchCount++;
            return Task.CompletedTask;
        }
    }

    #endregion
}
