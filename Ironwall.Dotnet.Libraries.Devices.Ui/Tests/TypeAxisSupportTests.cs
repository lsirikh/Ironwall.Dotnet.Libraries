using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Models;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// FR-09 · FR-10 — 종류축(<c>type_&lt;category&gt;</c>) 콤보·필터와 승계 불가 값의 저장 선차단.
/// 7개 패널이 <see cref="TypeAxisPanelSupport"/> 하나를 공유한다.
/// </summary>
[Collection("CaliburnIoC")]
public class TypeAxisSupportTests : IDisposable
{
    private readonly TestIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    private const string CatalogJson = """
        { "categories": [
            { "category_device": "camera", "type_axis": { "field": "type_camera", "required_on_create": true,
                "values": [ { "code": "FIXED", "label": "고정형" }, { "code": "PTZ", "label": "PTZ" }, { "code": "SPEED_DOME", "label": "SPEED_DOME" } ] } },
            { "category_device": "controller", "type_axis": { "field": "type_controller", "required_on_create": true,
                "values": [ { "code": "Controller", "label": "Controller" }, { "code": "SmartController", "label": "SmartController" }, { "code": "IoController", "label": "IoController" } ] } },
            { "category_device": "lamp", "type_axis": { "field": "type_lamp", "required_on_create": false, "unknown_code": "Unknown", "default": "Unknown",
                "values": [ { "code": "Beacon", "label": "Beacon" }, { "code": "Strobe", "label": "Strobe" }, { "code": "Unknown", "label": "Unknown" } ] } }
        ] }
        """;

    private static async Task<TypeAxisPanelSupport> Create(EnumDeviceCategory category, EnumServerContract contract)
    {
        var api = new MockDeviceApiService
        {
            CatalogResponseFactory = () => ApiResponse<DeviceSpecCatalogDto>.CreateSuccess(JsonConvert.DeserializeObject<DeviceSpecCatalogDto>(CatalogJson)!),
        };
        var policy = new DeviceQueryPolicy(new FixedProbe(contract));
        var support = new TypeAxisPanelSupport(category, new CatalogService(api, policy, null), policy);
        await support.EnsureAsync();
        return support;
    }

    [Fact]
    public async Task should_offer_catalog_values_as_options_when_contract_is_axis()
    {
        var support = await Create(EnumDeviceCategory.Camera, EnumServerContract.V8_0);

        Assert.True(support.IsAxisUi);
        Assert.Equal(new[] { "FIXED", "PTZ", "SPEED_DOME" }, support.Options.Select(o => o.Code));
        Assert.Equal(new[] { "", "FIXED", "PTZ", "SPEED_DOME" }, support.FilterOptions.Select(o => o.Code));   // 맨 앞은 "전체"
    }

    [Fact]
    public async Task should_stay_empty_and_hidden_when_contract_is_legacy()
    {
        var support = await Create(EnumDeviceCategory.Camera, EnumServerContract.V6_3);

        Assert.False(support.IsAxisUi);
        Assert.True(support.IsLegacyUi);
        Assert.Empty(support.Options);
        Assert.True(support.IsValid(null));          // 6.3 에서는 종류축으로 아무것도 막지 않는다
        Assert.True(support.Matches("anything"));
    }

    [Theory]
    [InlineData("SPEED_DOME", true)]
    [InlineData("ptz", true)]
    [InlineData("THERMAL", false)]     // 6.3 클라 전용 값 — 서버 어휘에 없다
    [InlineData("NONE", false)]
    [InlineData(null, false)]          // 카메라는 생성 시 필수
    [InlineData("", false)]
    public async Task should_validate_required_type_axis_against_catalog(string? code, bool expected)
    {
        var support = await Create(EnumDeviceCategory.Camera, EnumServerContract.V8_0);

        Assert.Equal(expected, support.IsValid(code));
    }

    [Theory]
    [InlineData("Strobe", true)]
    [InlineData(null, true)]           // 형상 축은 비워도 된다 — 서버가 Unknown 을 배정(8.0.1 실측)
    [InlineData("", true)]
    [InlineData("Siren", false)]
    public async Task should_allow_empty_type_axis_for_shape_categories(string? code, bool expected)
    {
        var support = await Create(EnumDeviceCategory.Lamp, EnumServerContract.V8_0);

        Assert.Equal(expected, support.IsValid(code));
    }

    [Theory]
    [InlineData("FIXED", "고정형 (FIXED)")]
    [InlineData("PTZ", "PTZ")]                     // 라벨=코드면 코드를 두 번 쓰지 않는다
    [InlineData("THERMAL", "미대응: THERMAL")]      // 원값을 숨기지 않는다
    [InlineData(null, "선택 필요")]
    public async Task should_describe_type_axis_value_for_display(string? code, string expected)
    {
        var support = await Create(EnumDeviceCategory.Camera, EnumServerContract.V8_0);

        Assert.Equal(expected, support.Describe(code));
    }

    [Fact]
    public async Task should_describe_empty_shape_axis_as_server_default()
    {
        var support = await Create(EnumDeviceCategory.Lamp, EnumServerContract.V8_0);

        Assert.Equal("Unknown (서버 기본값)", support.Describe(null));
    }

    [Fact]
    public async Task should_filter_rows_by_selected_type_axis()
    {
        var support = await Create(EnumDeviceCategory.Camera, EnumServerContract.V8_0);

        Assert.True(support.Matches("FIXED"));       // 필터 없음 = 전부
        support.SelectedFilterCode = "PTZ";
        Assert.True(support.Matches("ptz"));
        Assert.False(support.Matches("FIXED"));
        Assert.False(support.Matches(null));
        support.SelectedFilterCode = "";
        Assert.True(support.Matches(null));
    }

    [Fact]
    public async Task should_list_rows_that_block_saving()
    {
        var support = await Create(EnumDeviceCategory.Camera, EnumServerContract.V8_0);
        var rows = new[]
        {
            new CameraDeviceViewModel(new CameraDeviceModel { DeviceName = "ok", TypeAxisCode = "PTZ" }),
            new CameraDeviceViewModel(new CameraDeviceModel { DeviceName = "thermal", TypeAxisCode = "THERMAL" }),
            new CameraDeviceViewModel(new CameraDeviceModel { DeviceName = "draft", TypeAxisCode = null }),
        };

        var blocked = support.FindBlocked(rows).Select(r => r.DeviceName).ToArray();

        Assert.Equal(new[] { "thermal", "draft" }, blocked);
    }

    [Fact]
    public void should_sync_client_enum_when_row_type_axis_is_representable()
    {
        var camera = new CameraDeviceModel { Category = EnumCameraType.FIXED, TypeAxisCode = "FIXED" };
        var row = new CameraDeviceViewModel(camera);

        row.TypeAxisCode = "PTZ";

        Assert.Equal("PTZ", camera.TypeAxisCode);
        Assert.Equal(EnumCameraType.PTZ, camera.Category);                       // enum 이 옛 값으로 남으면 되쓰기에서 enum 이 이겨 편집이 사라진다
        Assert.Equal("PTZ", camera.ToCameraDeviceDto().TypeCameraAxis);
    }

    [Fact]
    public void should_keep_original_code_when_client_enum_cannot_represent_it()
    {
        var camera = new CameraDeviceModel { Category = EnumCameraType.PTZ, TypeAxisCode = "PTZ" };
        var row = new CameraDeviceViewModel(camera);

        row.TypeAxisCode = "SPEED_DOME";

        Assert.Equal("SPEED_DOME", camera.TypeAxisCode);
        Assert.Equal("SPEED_DOME", camera.ToCameraDeviceDto().TypeCameraAxis);
    }

    [Fact]
    public void should_sync_device_type_for_controller_row()
    {
        var controller = new ControllerDeviceModel { DeviceType = EnumDeviceType.Controller, TypeAxisCode = "Controller" };
        var row = new ControllerDeviceViewModel(controller);

        row.TypeAxisCode = "IoController";

        Assert.Equal(EnumDeviceType.IoController, controller.DeviceType);
    }

    [Fact]
    public void should_ignore_null_written_back_by_combo_when_items_source_is_replaced()
    {
        // WPF ComboBox 는 ItemsSource 가 바뀌어 현재 값이 목록에 없으면 SelectedValue=null 을 소스에 되쓴다.
        // 카탈로그 재조회(SYNC_CATALOG) 한 번에 모든 행의 종류축이 지워지면 안 된다(ISSUE-25).
        var camera = new CameraDeviceModel { TypeAxisCode = "SPEED_DOME" };
        var row = new CameraDeviceViewModel(camera);

        row.TypeAxisCode = null;
        row.TypeAxisCode = "   ";

        Assert.Equal("SPEED_DOME", camera.TypeAxisCode);
    }

    [Fact]
    public async Task should_rebuild_options_and_notify_when_catalog_changes()
    {
        var support = await Create(EnumDeviceCategory.Camera, EnumServerContract.V8_0);
        var raised = new List<string?>();
        support.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        await support.RefreshAsync();

        Assert.Equal(3, support.Options.Count);
        Assert.Contains(nameof(TypeAxisPanelSupport.Options), raised);
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
