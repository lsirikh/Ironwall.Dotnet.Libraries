using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Models;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.ByComponent;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// FR-17 — "부품으로 찾기" 콘솔 탭. <c>GET /api/devices/by-component</c> 하나로 부품 유형·상태·건강을
/// 걸러 장비를 찾는 읽기 전용 조회 화면의 뷰모델(<see cref="ByComponentViewModel"/>)을 검증한다.
/// </summary>
public class ByComponentViewModelTests
{
    // component_type 어휘만 있으면 충분하다(CatalogServiceTests.CatalogJson 을 줄인 것).
    private const string CatalogJson = """
        {
          "vocabularies": {
            "component_type": [
              { "code": "DOOR_SENSOR", "label": "도어 센서" },
              { "code": "DOOR_ACTUATOR", "label": "도어 구동부" }
            ]
          }
        }
        """;

    private static async Task<(ByComponentViewModel vm, FakeComponentApiService api)> CreateAsync(EnumServerContract contract)
    {
        var catalogApi = new MockDeviceApiService
        {
            CatalogResponseFactory = () => ApiResponse<DeviceSpecCatalogDto>.CreateSuccess(
                JsonConvert.DeserializeObject<DeviceSpecCatalogDto>(CatalogJson)!)
        };
        var policy = new DeviceQueryPolicy(new FixedProbe(contract));
        var catalog = new CatalogService(catalogApi, policy, log: null);
        if (policy.IsAxisContract) await catalog.EnsureLoadedAsync();

        var api = new FakeComponentApiService();
        var vm = new ByComponentViewModel(api, catalog, new MockLogService(), policy);
        return (vm, api);
    }

    private static ComponentStateRowDto Row(int id, string category, string component, string? componentType, string? state, string? health, string? observedAt = null)
        => new()
        {
            Id = id,
            CategoryDevice = category,
            Component = component,
            ComponentType = componentType,
            State = state,
            Health = health,
            ObservedAt = observedAt,
        };

    private static ApiListResponse<ComponentStateRowDto> Ok(params ComponentStateRowDto[] rows)
        => ApiListResponse<ComponentStateRowDto>.CreateSuccess(rows.ToList());

    [Fact]
    public async Task should_not_call_api_and_report_unavailable_when_contract_is_legacy()
    {
        var (vm, api) = await CreateAsync(EnumServerContract.V6_3);

        Assert.False(vm.IsAvailable);

        await vm.SearchAsync();

        Assert.Empty(api.Calls);
        Assert.Empty(vm.Rows);
    }

    [Fact]
    public async Task should_query_with_selected_component_type_when_search_runs()
    {
        var (vm, api) = await CreateAsync(EnumServerContract.V8_0);
        api.ResponseFactory = _ => Task.FromResult(Ok());

        vm.SelectedComponentType = vm.ComponentTypes.Single(o => o.Code == "DOOR_ACTUATOR");
        await vm.SearchAsync();

        var call = Assert.Single(api.Calls);
        Assert.Equal("DOOR_ACTUATOR", call.ComponentType);
        Assert.Null(call.Component);
        Assert.Null(call.State);
        Assert.Null(call.Health);
    }

    [Fact]
    public async Task should_add_state_filter_and_clear_it_on_all_chip_when_state_chip_is_selected()
    {
        var (vm, api) = await CreateAsync(EnumServerContract.V8_0);
        vm.SelectedComponentType = vm.ComponentTypes.Single(o => o.Code == "DOOR_SENSOR");
        api.ResponseFactory = _ => Task.FromResult(Ok(
            Row(1, "enclosure", "door", "DOOR_SENSOR", "OPEN", "OK"),
            Row(2, "gate", "door", "DOOR_SENSOR", "CLOSED", "OK")));

        await vm.SearchAsync();   // 결과에서 상태 칩(전체·CLOSED·OPEN — 정렬)을 얻는다
        Assert.Equal(new[] { ByComponentViewModel.AllChip, "CLOSED", "OPEN" }, vm.StateChips);

        vm.SelectedState = "OPEN";
        await vm.SearchAsync();
        Assert.Equal("OPEN", api.Calls.Last().State);

        vm.SelectedState = ByComponentViewModel.AllChip;
        await vm.SearchAsync();
        Assert.Null(api.Calls.Last().State);
    }

    [Fact]
    public async Task should_add_health_filter_and_clear_it_on_all_chip_when_health_chip_is_selected()
    {
        var (vm, api) = await CreateAsync(EnumServerContract.V8_0);
        vm.SelectedComponentType = vm.ComponentTypes.Single(o => o.Code == "DOOR_SENSOR");
        api.ResponseFactory = _ => Task.FromResult(Ok());

        vm.SelectedHealth = vm.HealthChips.Single(o => o.Code == "FAULT");
        await vm.SearchAsync();
        Assert.Equal("FAULT", api.Calls.Last().Health);

        vm.SelectedHealth = vm.HealthChips.Single(o => o.Code == ByComponentViewModel.AllChip);
        await vm.SearchAsync();
        Assert.Null(api.Calls.Last().Health);
    }

    /// <summary>
    /// 건강 칩은 화면에 한글("정상"·"주의"·"고장"·"미확인")을 보이지만, 서버로는 항상 원문 코드가 나가야
    /// 한다(하드 규칙: 필터 칩은 라벨이 아니라 wire value 를 보낸다). 칩 4종 전부를 한 번에 검증한다.
    /// </summary>
    [Theory]
    [InlineData("OK", "정상")]
    [InlineData("DEGRADED", "주의")]
    [InlineData("FAULT", "고장")]
    [InlineData("UNKNOWN", "미확인")]
    public async Task should_send_wire_code_when_korean_health_chip_is_selected(string code, string korean)
    {
        var (vm, api) = await CreateAsync(EnumServerContract.V8_0);
        vm.SelectedComponentType = vm.ComponentTypes.Single(o => o.Code == "DOOR_SENSOR");
        api.ResponseFactory = _ => Task.FromResult(Ok());

        var chip = vm.HealthChips.Single(o => o.Code == code);
        Assert.Equal(korean, chip.Label);   // 화면에 보이는 라벨은 한글
        Assert.Equal($"{korean} ({code})", chip.Display);

        vm.SelectedHealth = chip;
        await vm.SearchAsync();

        Assert.Equal(code, api.Calls.Last().Health);   // 서버로는 원문 코드(wire value)만 나간다
    }

    [Fact]
    public async Task should_show_only_korean_without_code_when_all_health_chip_is_selected()
    {
        // "전체" 는 Code==Label 이라 CatalogOption.Display 규칙상 괄호 병기가 없다(목업 규칙: 같으면 코드 숨김).
        var (vm, _) = await CreateAsync(EnumServerContract.V8_0);
        var chip = vm.HealthChips.Single(o => o.Code == ByComponentViewModel.AllChip);
        Assert.Equal("전체", chip.Display);
    }

    [Fact]
    public async Task should_reset_state_selection_to_all_when_component_type_changes()
    {
        var (vm, _) = await CreateAsync(EnumServerContract.V8_0);
        vm.SelectedComponentType = vm.ComponentTypes.Single(o => o.Code == "DOOR_SENSOR");
        vm.SelectedState = "OPEN";

        vm.SelectedComponentType = vm.ComponentTypes.Single(o => o.Code == "DOOR_ACTUATOR");

        Assert.Equal(ByComponentViewModel.AllChip, vm.SelectedState);
        Assert.Equal(new[] { ByComponentViewModel.AllChip }, vm.StateChips);
    }

    [Fact]
    public async Task should_keep_two_rows_with_same_device_id_when_row_keys_differ()
    {
        var (vm, api) = await CreateAsync(EnumServerContract.V8_0);
        vm.SelectedComponentType = vm.ComponentTypes.Single(o => o.Code == "DOOR_SENSOR");
        api.ResponseFactory = _ => Task.FromResult(Ok(
            Row(5, "enclosure", "door_a", "DOOR_SENSOR", "OPEN", "OK"),
            Row(5, "enclosure", "door_b", "DOOR_SENSOR", "CLOSED", "OK")));

        await vm.SearchAsync();

        Assert.Equal(2, vm.Rows.Count);
        Assert.Equal(2, vm.Rows.Select(r => r.RowKey).Distinct().Count());
        Assert.All(vm.Rows, r => Assert.Equal(5, r.DeviceNumber));
    }

    [Fact]
    public async Task should_keep_microseconds_verbatim_and_show_no_observation_text_when_observed_at_is_empty()
    {
        var (vm, api) = await CreateAsync(EnumServerContract.V8_0);
        vm.SelectedComponentType = vm.ComponentTypes.Single(o => o.Code == "DOOR_SENSOR");
        api.ResponseFactory = _ => Task.FromResult(Ok(
            Row(1, "enclosure", "door", "DOOR_SENSOR", "OPEN", "OK", "2026-09-19T10:11:12.123456+09:00"),
            Row(2, "enclosure", "door", "DOOR_SENSOR", "CLOSED", "OK", null)));

        await vm.SearchAsync();

        Assert.Equal("2026-09-19T10:11:12.123456+09:00", vm.Rows[0].LastChangeText);
        Assert.Equal("보고 없음", vm.Rows[1].LastChangeText);
    }

    [Fact]
    public async Task should_clear_rows_and_show_korean_status_when_api_call_fails()
    {
        var (vm, api) = await CreateAsync(EnumServerContract.V8_0);
        vm.SelectedComponentType = vm.ComponentTypes.Single(o => o.Code == "DOOR_SENSOR");
        api.ResponseFactory = _ => Task.FromResult(
            ApiListResponse<ComponentStateRowDto>.CreateError("INTERNAL_ERROR", "down"));

        await vm.SearchAsync();   // 절대 던지지 않는다

        Assert.Empty(vm.Rows);
        Assert.Contains("실패", vm.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task should_keep_result_of_newer_search_when_older_search_is_slower()
    {
        var (vm, api) = await CreateAsync(EnumServerContract.V8_0);
        vm.SelectedComponentType = vm.ComponentTypes.Single(o => o.Code == "DOOR_SENSOR");

        var gate = new TaskCompletionSource<bool>();
        var callIndex = 0;
        api.ResponseFactory = async _ =>
        {
            var mine = System.Threading.Interlocked.Increment(ref callIndex);
            if (mine == 1)
            {
                await gate.Task;   // 첫 호출은 테스트가 풀어줄 때까지 붙잡혀 있는다(느린 응답 흉내)
                return Ok(Row(1, "enclosure", "door", "DOOR_SENSOR", "OPEN", "OK"));
            }
            return Ok(Row(2, "enclosure", "door", "DOOR_SENSOR", "CLOSED", "OK"));
        };

        var first = vm.SearchAsync();    // 1번 호출 시작 — gate 에서 대기
        Assert.Single(api.Calls);
        var second = vm.SearchAsync();   // 2번 호출이 1번을 취소하고 먼저 끝낸다
        await second;

        Assert.Single(vm.Rows);
        Assert.Equal("닫힘", vm.Rows[0].StateText);   // 2번(CLOSED) 결과

        gate.SetResult(true);            // 1번을 풀어준다 — 이미 취소됐으므로 결과를 버려야 한다
        await first;

        Assert.Single(vm.Rows);
        Assert.Equal("닫힘", vm.Rows[0].StateText);   // 1번(OPEN) 이 덮어쓰지 않았다
    }

    /// <summary>
    /// <see cref="MockDeviceApiService"/> 는 <c>GetDevicesByComponentAsync</c> 를 항상 빈 성공으로 고정
    /// 반환하며(<c>UnitTest.cs</c>) 구성할 방법이 없다 — 이 파일은 그 파일을 고치지 않으므로, 부모를
    /// 상속하면서 그 메서드만 <c>new</c> 로 가리고 <see cref="IDeviceApiService"/> 를 다시 선언해
    /// 인터페이스 디스패치가 이 재구현을 타도록 한다(부모 메서드가 virtual 이 아니라 override 로는 안 된다).
    /// 나머지 80여 개 멤버는 부모(Mock)의 미구현/빈 구현을 그대로 물려받는다.
    /// </summary>
    private sealed class FakeComponentApiService : MockDeviceApiService, IDeviceApiService
    {
        public sealed record Query(string? ComponentType, string? Component, string? State, string? Health, string? DeviceType);

        public List<Query> Calls { get; } = new();
        public Func<Query, Task<ApiListResponse<ComponentStateRowDto>>>? ResponseFactory { get; set; }

        public new async Task<ApiListResponse<ComponentStateRowDto>> GetDevicesByComponentAsync(
            string? componentType = null,
            string? component = null,
            string? state = null,
            string? health = null,
            string? deviceType = null,
            CancellationToken token = default)
        {
            var query = new Query(componentType, component, state, health, deviceType);
            Calls.Add(query);
            if (ResponseFactory != null) return await ResponseFactory(query);
            return ApiListResponse<ComponentStateRowDto>.CreateSuccess(new List<ComponentStateRowDto>());
        }
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
}
