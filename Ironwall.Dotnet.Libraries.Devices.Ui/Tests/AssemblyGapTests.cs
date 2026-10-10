using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 조립기 · 부품 적용 — 카탈로그 §6.8 의 GAP 중 창 없이 확인되는 것(ASM-013 · 023 · 026 · 205). 헤드리스.
/// </summary>
public class AssemblyGapTests
{
    #region - Delete 키 (ASM-013) -
    [Theory]
    [InlineData(Key.Delete, false, false, true, true)]                                      // 보드 칸에서 Delete → 빠진다
    [InlineData(Key.Delete, true, false, true, false)]                                      // 글 칸 안의 Delete 는 글자만
    [InlineData(Key.Delete, false, true, true, false)]                                      // 끄는 도중은 받지 않는다
    [InlineData(Key.Delete, false, false, false, false)]                                    // 고른 칸이 없다
    [InlineData(Key.Back, false, false, true, false)]
    [Trait("Scenario", "ASM-013")]
    public void should_remove_on_delete_only_from_the_board_and_never_inside_a_text_box_or_mid_drag_ASM013(
        Key key, bool fromTextBox, bool leftPressed, bool canRemove, bool expected)
    {
        var decided = RunSta(() => AssemblyView.ShouldRemoveOnDelete(key, fromTextBox ? new TextBox() : new object(), leftPressed, canRemove));

        Assert.Equal(expected, decided);
    }

    private static T RunSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new System.Threading.Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { failure = ex; }
            finally { StaCleanup.ShutdownDispatcher(); }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new AggregateException(failure);
        return result;
    }
    #endregion

    #region - 부품 칸 (ASM-023 · ASM-026 · 정적 판독 D4) -
    [Fact]
    [Trait("Scenario", "ASM-023,ASM-026")]
    public void should_carry_position_in_service_and_identity_fields_into_the_definition_and_count_one_change_ASM023_ASM026()
    {
        var board = new AssemblyBoard(EnumDeviceCategory.Enclosure, _ => null);
        board.Load(new[] { new ComponentDefinitionModel { Key = "heater", Type = "HEATER" } }, null);
        var slot = board.Slots[0];

        slot.Position = "함체 아래";
        slot.InService = false;
        slot.Manufacturer = "Sensorway";
        slot.Model = "HT-20";
        slot.Serial = "HT20-0007";
        var definition = slot.ToDefinition();
        var changed = board.Diff().Changed.Single().ChangedFields;

        Assert.Equal(("함체 아래", (bool?)false, "Sensorway", "HT-20", "HT20-0007"),
                     (definition.Position, definition.InService, definition.Manufacturer, definition.Model, definition.Serial));
        Assert.Equal(1, board.UnsavedChangeCount);                                          // 한 부품의 여러 칸 = 바뀐 부품 1개
        Assert.True(changed.Count >= 5, string.Join(",", changed));
    }

    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    [Fact]
    public void should_give_every_slot_field_of_the_inspector_an_automation_id_D4()
    {
        // 정적 판독 D4 — 제조사 · 모델 · 일련번호 칸만 자동화 id 가 없어 헤디드 시험이 찾지 못했다(ASM-026 GAP-N). 고친 뒤의 회귀 시험.
        var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Devices.Ui", "Consoles", "Assembly", "AssemblyView.xaml"));

        foreach (var field in new[] { "key", "label", "channel", "position", "in_service", "manufacturer", "model", "serial" })
            Assert.Contains($"AutomationProperties.AutomationId=\"Devices.Assembly.Field.{field}\"", xaml);
    }
    #endregion

    #region - 결선된 센서에 부품 적용 (ASM-205) -
    [Fact]
    [Trait("Scenario", "ASM-205")]
    public async Task should_keep_the_sensor_wiring_and_bus_address_when_components_are_applied_to_a_wired_sensor_ASM205()
    {
        // Arrange — 결선이 저장된 RS485 센서(spec.wiring · 버스 주소 3) — 서버가 돌려줄 한 건
        var http = new CapturingHttp();
        var server = WiringDoubles.ServerSensor(101, 1101, 3, new WiringPlacement(1, 3));
        server.Channel = 3;
        server.UseAxisWrite = true;
        server.HardwareSpec!.AllowComponentsWrite = true;                                   // 응답에는 부품 배열이 실려 온다
        http.GetResponseJson = new JObject { ["success"] = true, ["data"] = JObject.FromObject(server), ["meta"] = new JObject() }.ToString();
        var wiringBefore = (JObject)server.HardwareSpec!.Spec!["wiring"]!.DeepClone();

        var service = new ComponentApplyService(http.Real(), new MockDeviceProviderService(), new MockLogService(), WiringDoubles.AxisPolicy());
        var device = new SensorDeviceModel { Id = 101, DeviceNumber = 1101, DeviceName = "북측 3구간 펜스", CategoryDevice = EnumDeviceCategory.Sensor };
        var baseline = new[] { new ComponentDefinitionModel { Key = "vib", Type = "VIBRATION_SENSOR" } };
        var desired = baseline.Append(new ComponentDefinitionModel { Key = "nic", Type = "NETWORK_INTERFACE" }).ToArray();

        var fetched = await http.Real().GetSensorByIdAsync(101, false);
        var serverParts = fetched.Data!.HardwareSpec!.Components;
        Assert.True(ComponentApplyService.SameComponents(baseline, serverParts),
            $"{Newtonsoft.Json.JsonConvert.SerializeObject(PresetRequestBuilder.ToComponentDto(baseline[0]))} ≠ {Newtonsoft.Json.JsonConvert.SerializeObject(serverParts)}");
        http.Calls.Clear();

        // Act
        var result = await service.ApplyAsync(device, baseline, desired, null);

        // Assert — 부품 배열만 바뀌고 결선 · 버스 주소는 지워지지도 바뀌지도 않는다(PATCH 병합: 빠진 키 = 그대로, null = 삭제)
        Assert.True(result.IsSuccess, result.Message);
        var patch = http.Calls.Single(c => c.Method == "PATCH");
        var body = patch.Body!;
        Assert.DoesNotContain(body.Descendants().OfType<JProperty>(), p => p.Value.Type == JTokenType.Null);
        Assert.Equal(new[] { "vib", "nic" }, ((JArray)body.SelectToken("hardware_spec.components")!).Select(c => (string?)c["key"]));
        if (body.SelectToken("hardware_spec.spec") is { } spec)
            Assert.True(JToken.DeepEquals(wiringBefore, spec["wiring"]), $"spec.wiring 이 바뀌었다: {spec}");
        if (body.SelectToken("connection.channel") is { } channel)
            Assert.Equal(3, (int)channel);
    }
    #endregion
}
