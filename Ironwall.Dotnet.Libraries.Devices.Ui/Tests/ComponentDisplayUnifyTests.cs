using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Models;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.ByComponent;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Forms;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Properties;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using Ironwall.Dotnet.Monitoring.Models.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 장비 콘솔 쪽 부품 표시 통일(component-display-unify FR-01 · FR-02 · FR-03) — 상세 "부품 · 부품 상태" 절 표 ·
/// 요약 줄 · 정렬 단추 · 설정/관측 나란히 · "부품으로 찾기" 사유 칸과 이름 규칙.
/// </summary>
[Collection("CaliburnIoC")]
public class ComponentDisplayUnifyTests : IDisposable
{
    private readonly TestIoCScope _ioc = new();   // 장비 행 VM 생성자가 IoC 를 부른다 — 순서에 기대지 않는다
    public void Dispose() => _ioc.Dispose();

    private static LampDeviceModel LampWithParts(int id, bool statusReceived = true) => new()
    {
        Id = id, DeviceNumber = id, DeviceName = $"경광등 {id}", Status = EnumDeviceStatus.ACTIVATED,
        Axes = BuildAxes(statusReceived),
    };

    private static DeviceAxesModel BuildAxes(bool statusReceived)
    {
        var spec = new HardwareSpecModel();
        spec.Components.Add(new ComponentDefinitionModel { Key = "lamp", Type = "LAMP_LIGHT", Channel = 1, Position = "TOP" });
        spec.Components.Add(new ComponentDefinitionModel { Key = "buzzer1", Type = "BUZZER", Label = "경보 부저" });
        spec.Components.Add(new ComponentDefinitionModel { Key = "spare", Type = "BUZZER", InService = false });
        var status = new DeviceStatusModel();
        status.Components["lamp"] = new ComponentStatusModel { State = "OFF", Health = "OK", ObservedAt = "2026-10-01T09:12:55.000000+09:00" };
        status.Components["buzzer1"] = new ComponentStatusModel { State = "ON", Health = "FAULT", FaultReason = "OVER_CURRENT", ObservedAt = "2026-10-01T09:41:07.000000+09:00" };
        return new DeviceAxesModel
        {
            HardwareSpec = spec,
            DeviceStatus = statusReceived ? status : null,
            DeviceConfig = new DeviceConfigModel { ComponentOverrides = JObject.Parse("""{ "lamp": { "enabled": true } }""") },
            Meta = new ResponseMeta("full", statusReceived
                ? new[] { "connection", "hardware_spec", "components", "device_status", "device_config" }
                : new[] { "connection", "hardware_spec", "components", "device_config" }),
        };
    }

    private static DevicePropertyFormViewModel Load(params LampDeviceModel[] models)
    {
        var form = new DevicePropertyFormViewModel(new ConsoleDetailPresenter());
        form.Load(models.Select(m => (object)new LampDeviceViewModel(m)).ToList(), EnumDeviceCategory.Lamp,
            isAxisContract: true, isCreating: false, isReadOnly: false, isUnitEra: true);
        return form;
    }

    [Fact]
    public void should_draw_status_table_with_summary_and_korean_when_one_device_is_selected()
    {
        var form = Load(LampWithParts(11));

        var section = form.Sections.Single(s => s.Section == DevicePropertySection.DeviceStatus);
        var table = Assert.IsType<ComponentTableModel>(section.StatusTable);
        Assert.Equal("고장 1 · 정상 1 · 사용 안 함 1", table.SummaryText);
        Assert.Equal(ComponentHealthKind.Crit, table.SummaryKind);
        Assert.DoesNotContain(form.Fields, f => f.Key == "device_status");      // 같은 내용의 글 칸은 표로 대신한다

        var lamp = table.Rows.Single(r => r.Key == "lamp");
        Assert.Equal("경광등", lamp.Name);                                        // label 없음 → 카탈로그/내장 한글
        Assert.Equal("설정 켬 / 관측 꺼짐", lamp.StateText);                      // 설정과 관측을 나란히
        Assert.True(lamp.IsIntentMismatch);
        var buzzer = table.Rows.Single(r => r.Key == "buzzer1");
        Assert.Equal(("경보 부저", "고장", "과전류"), (buzzer.Name, buzzer.HealthText, buzzer.FaultText));
        Assert.Equal("사용 안 함", table.Rows.Single(r => r.Key == "spare").HealthText);
    }

    [Fact]
    public void should_switch_between_declared_and_fault_first_when_sort_button_is_toggled()
    {
        var table = Load(LampWithParts(11)).Sections.Single(s => s.Section == DevicePropertySection.DeviceStatus).StatusTable!;
        Assert.Equal(new[] { "lamp", "buzzer1", "spare" }, table.Rows.Select(r => r.Key));   // 기본 = 선언 순서

        table.IsFaultFirst = true;

        Assert.Equal(new[] { "buzzer1", "lamp", "spare" }, table.Rows.Select(r => r.Key));
    }

    [Fact]
    public void should_draw_declaration_table_in_korean_when_one_device_is_selected()
    {
        var form = Load(LampWithParts(11));
        var table = form.Sections.Single(s => s.Section == DevicePropertySection.Components).DeclarationTable!;
        Assert.Equal("부품 3개 · 사용 안 함 1", table.DeclarationSummaryText);
        var lamp = table.DeclaredRows.First();
        Assert.Equal(("경광등", "경광등", "1", "TOP"), (lamp.Name, lamp.TypeName, lamp.ChannelText, lamp.PositionText));
        Assert.Equal("사용 안 함", table.DeclaredRows.Last().ServiceText);
    }

    [Fact]
    public void should_keep_text_fields_in_korean_without_tables_when_several_devices_are_selected()
    {
        var form = Load(LampWithParts(11), LampWithParts(12));

        Assert.All(form.Sections, s => { Assert.Null(s.StatusTable); Assert.Null(s.DeclarationTable); });
        var status = form.Fields.Single(f => f.Key == "device_status");
        Assert.Contains("경보 부저 · 켜짐 · 고장 · 과전류", status.Text);            // 영문 코드(FAULT · OVER_CURRENT) 없이
        Assert.DoesNotContain("FAULT", status.Text);
        Assert.Contains("경광등 · 켜기", form.Fields.Single(f => f.Key == "device_config.component_overrides").Text);
    }

    [Fact]
    public void should_show_not_received_box_instead_of_table_when_status_axis_is_missing()
    {
        var form = Load(LampWithParts(11, statusReceived: false));
        var section = form.Sections.Single(s => s.Section == DevicePropertySection.DeviceStatus);
        Assert.Null(section.StatusTable);
        Assert.True(section.IsNotReceived);
    }

    [Fact]
    public void should_fill_name_and_fault_reason_from_device_cache_when_by_component_row_has_lookup()
    {
        var device = LampWithParts(11);
        var dto = new ComponentStateRowDto { Id = 11, CategoryDevice = "lamp", Component = "buzzer1", ComponentType = "BUZZER", State = "ON", Health = "FAULT" };

        var withCache = new ByComponentRowViewModel(dto, Catalog(), id => id == 11 ? device : null);
        Assert.Equal("경보 부저", withCache.ComponentName);
        Assert.Equal("과전류", withCache.FaultReason);
        Assert.Equal("부저", withCache.ComponentTypeLabel);
        Assert.Equal("고장", withCache.HealthText);
        Assert.Equal("켜짐", withCache.StateText);

        var withoutCache = new ByComponentRowViewModel(dto, Catalog());
        Assert.Equal("부저", withoutCache.ComponentName);            // label 을 모르면 유형 한글
        Assert.Equal(string.Empty, withoutCache.FaultReason);
    }

    [Fact]
    public void should_not_attach_stale_reason_when_by_component_row_is_healthy()
    {
        var device = LampWithParts(11);
        var dto = new ComponentStateRowDto { Id = 11, CategoryDevice = "lamp", Component = "buzzer1", ComponentType = "BUZZER", State = "ON", Health = "OK" };
        Assert.Equal(string.Empty, new ByComponentRowViewModel(dto, Catalog(), _ => device).FaultReason);
    }

    [Fact]
    public void should_use_shared_korean_in_legacy_axis_sections_when_status_is_received()
    {
        var vm = DeviceAxisSectionsViewModel.From(new IBaseDeviceModel[] { LampWithParts(11) }, new DeviceQueryPolicy(new Probe(EnumServerContract.V8_0)));
        Assert.Equal("고장 1 · 정상 1 · 사용 안 함 1", vm.ComponentSummaryText);
        var buzzer = vm.StatusRows.Single(r => r.Key == "buzzer1");
        Assert.Equal(("경보 부저", "켜짐", "고장", "과전류", ComponentHealthKind.Crit), (buzzer.Name, buzzer.StateText, buzzer.HealthText, buzzer.FaultText, buzzer.HealthKind));
        Assert.Equal("FAULT", buzzer.Health);   // 원문 칸은 그대로(서버 값 대조용)
    }

    private static CatalogService Catalog()
        => new(new MockDeviceApiService(), new DeviceQueryPolicy(new Probe(EnumServerContract.V6_3)));   // 6.3 — 카탈로그를 부르지 않는다 → 내장 사전

    private sealed class Probe : IServerContractProbe
    {
        public Probe(EnumServerContract contract) => Contract = contract;
        public EnumServerContract Contract { get; }
        public string? RawVersion => Contract.ToString();
        public bool IsResolved => true;
        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    }
}
