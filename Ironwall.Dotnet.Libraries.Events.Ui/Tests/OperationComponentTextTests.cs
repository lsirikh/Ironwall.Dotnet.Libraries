using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Monitoring.Models.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 운영 이벤트에 "어느 부품이 어떻게 바뀌었나" 표시(2026-10-22 요청 — "통문 접점 감지, 액추에이터 열림 뭐 이렇게")
                  detail.component(부품 key) · previous · state 를 장비 캐시의 부품 선언으로 이름 붙여 카드 · 상세 · 목록 · 조치보고 창 글자로.
                  실측(로컬 시험 서버 DB, 2026-10-22): 함체 key "door"(DOOR_SENSOR) · 통문 key "actuator"(DOOR_ACTUATOR),
                  카탈로그 라벨 DOOR_SENSOR="도어 센서" · DOOR_ACTUATOR="문 구동부", 서버는 OPEN · CLOSED 전이만 이벤트로 낸다(RUNNING 은 previous 로만 온다).
   Created By   : Claude
   Created On   : 2026-10-22
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class OperationComponentTextTests
{
    internal static ComponentDefinitionModel Part(string key, string type, string? label = null) => new() { Key = key, Type = type, Label = label };

    private static readonly ComponentDefinitionModel[] GateParts =
    {
        Part("actuator", "DOOR_ACTUATOR"),
        Part("door", "DOOR_SENSOR"),
        Part("contact", "DOOR_SENSOR", "통문 접점"),
        Part("future", "SOMETHING_NEW"),
    };

    [Theory]
    // 부품 있음 — 캐시의 유형 한글(지도 · 장비 콘솔과 같은 이름)
    [InlineData("""{ "state": "OPEN", "previous": "CLOSED", "component": "door", "device_status": "ACTIVATED" }""", "도어 센서 닫힘 → 열림")]
    // 등록 때 붙인 이름이 먼저
    [InlineData("""{ "state": "OPEN", "previous": "CLOSED", "component": "contact", "device_status": "ACTIVATED" }""", "통문 접점 닫힘 → 열림")]
    // 첫 보고(previous null)
    [InlineData("""{ "state": "OPEN", "previous": null, "component": "actuator", "device_status": "ACTIVATED" }""", "문 구동부 열림")]
    // 구동부가 움직인 뒤 열림 — RUNNING 은 "구동 중"(지도 · 장비 콘솔과 같은 말)
    [InlineData("""{ "state": "OPEN", "previous": "RUNNING", "component": "actuator", "device_status": "ACTIVATED" }""", "문 구동부 구동 중 → 열림")]
    [InlineData("""{ "state": "CLOSED", "previous": "RUNNING", "component": "actuator" }""", "문 구동부 구동 중 → 닫힘")]
    // 캐시에 없는 key — key 그대로
    [InlineData("""{ "state": "CLOSED", "previous": "OPEN", "component": "rear_door" }""", "rear_door 열림 → 닫힘")]
    // 모르는 유형(이름 없음) — key 그대로
    [InlineData("""{ "state": "OPEN", "previous": "CLOSED", "component": "future" }""", "future 닫힘 → 열림")]
    // 모르는 상태 값 — 원문
    [InlineData("""{ "state": "HALF_OPEN", "previous": "CLOSED", "component": "door" }""", "도어 센서 닫힘 → HALF_OPEN")]
    // 장비 상태 · 모르는 키는 지금처럼 뒤에
    [InlineData("""{ "state": "OPEN", "previous": "CLOSED", "component": "door", "device_status": "DEACTIVATED", "extra": 1 }""", "도어 센서 닫힘 → 열림 · 장비 점검 중 · extra=1")]
    // 부품 없음(옛 행) — 지금과 같다
    [InlineData("""{ "previous": "CLOSED", "gate_status": "OPEN" }""", "닫힘 → 열림")]
    [InlineData("""{ "state": "RUNNING" }""", "구동 중")]
    public void should_name_the_component_that_changed_in_the_detail_line(string json, string expected)
        => Assert.Equal(expected, OperationEventText.DetailText(JObject.Parse(json), GateParts));

    [Theory]
    [InlineData("""{ "state": "OPEN", "previous": "CLOSED", "component": "door" }""", "door 닫힘 → 열림")]
    [InlineData("""{ "state": "OPEN", "previous": null, "component": "actuator" }""", "actuator 열림")]
    public void should_show_the_key_and_the_states_at_once_when_the_device_is_not_cached_yet(string json, string expected)
    {
        // 이벤트가 장비 캐시(SYNC_DEVICE)보다 먼저 왔다 — 상태는 detail 값이라 바로 보이고 이름만 key 다.
        Assert.Equal(expected, OperationEventText.DetailText(JObject.Parse(json)));
        Assert.Equal(expected, OperationEventText.ComponentChange(JObject.Parse(json))!.Text);
    }

    [Fact]
    public void should_have_no_component_change_without_a_component_key_or_a_state()
    {
        Assert.Null(OperationEventText.ComponentChange(null, GateParts));
        Assert.Null(OperationEventText.ComponentChange(JObject.Parse("""{ "gate_status": "OPEN", "previous": "CLOSED" }"""), GateParts));
        Assert.Null(OperationEventText.ComponentChange(JObject.Parse("""{ "component": "door" }"""), GateParts));
        Assert.Null(OperationEventText.ComponentChange(JObject.Parse("""{ "field": "temperature", "value": 65 }"""), GateParts));
    }

    [Fact]
    public void should_prefer_the_server_catalog_name_over_the_built_in_one()
    {
        var change = OperationEventText.ComponentChange(JObject.Parse("""{ "state": "OPEN", "previous": "RUNNING", "component": "actuator" }"""),
                                                        GateParts, new Labels(("DOOR_ACTUATOR", "도어 구동부")));

        Assert.Equal("도어 구동부 구동 중 → 열림", change!.Text);
        Assert.Equal("도어 구동부 열림", change.StepText);
    }

    [Fact]
    public void should_match_the_component_key_regardless_of_case()
        => Assert.Equal("도어 센서 닫힘 → 열림",
                        OperationEventText.ComponentChange(JObject.Parse("""{ "state": "OPEN", "previous": "CLOSED", "component": "DOOR" }"""), GateParts)!.Text);

    internal sealed class Labels : IComponentTypeLabels
    {
        private readonly Dictionary<string, string> _map;
        public Labels(params (string Type, string Label)[] pairs) => _map = pairs.ToDictionary(p => p.Type, p => p.Label);
        public string? TypeLabel(string? type) => type is not null && _map.TryGetValue(type, out var label) ? label : null;
    }
}

/// <summary>흐름 — 같은 장비 30초 안의 부품 전이를 시간순 한 줄로.</summary>
public class OperationComponentFlowTests
{
    private static readonly DateTime T0 = new(2026, 10, 22, 10, 0, 0);

    [Fact]
    public void should_have_no_flow_for_the_first_transition_of_a_device()
        => Assert.Null(new OperationComponentFlow().Add(1, 10, T0, "문 구동부 열림"));

    [Fact]
    public void should_join_transitions_of_the_same_device_within_30_seconds_in_time_order()
    {
        var flow = new OperationComponentFlow();
        flow.Add(1, 10, T0, "문 구동부 열림");

        Assert.Equal("문 구동부 열림 → 도어 센서 열림", flow.Add(1, 11, T0.AddSeconds(3), "도어 센서 열림"));
    }

    [Fact]
    public void should_not_join_transitions_older_than_30_seconds_or_of_other_devices()
    {
        var flow = new OperationComponentFlow();
        flow.Add(1, 10, T0, "문 구동부 열림");
        flow.Add(2, 11, T0.AddSeconds(5), "도어 센서 열림");      // 다른 장비

        Assert.Null(flow.Add(1, 12, T0.AddSeconds(31), "문 구동부 닫힘"));
    }

    [Fact]
    public void should_keep_only_the_last_three_steps_and_count_a_resent_event_once()
    {
        var flow = new OperationComponentFlow();
        flow.Add(1, 10, T0, "A");
        flow.Add(1, 11, T0.AddSeconds(1), "B");
        flow.Add(1, 11, T0.AddSeconds(1), "B");                   // 재전송
        flow.Add(1, 12, T0.AddSeconds(2), "C");

        Assert.Equal("B → C → D", flow.Add(1, 13, T0.AddSeconds(3), "D"));
    }

    [Fact]
    public void should_end_the_flow_at_this_transition_when_an_older_one_arrives_late()
    {
        var flow = new OperationComponentFlow();
        flow.Add(1, 11, T0.AddSeconds(5), "도어 센서 열림");

        Assert.Null(flow.Add(1, 10, T0, "문 구동부 열림"));      // 늦게 온 앞 전이 — 그 뒤 일을 싣지 않는다
    }
}

/// <summary>변환(목록 · 상세 · 카드가 같이 쓰는 <see cref="DtoToModelHelper.ToOperationEventModel"/>) — 장비 캐시의 부품 선언을 읽는가.</summary>
[Collection("IoC-Dependent")]
public class OperationComponentModelTests : IoCStubbedTestBase
{
    private static OperationEventDto Dto(int deviceId, string detail) => new()
    {
        Id = 77, Reason = "GATE_OPEN", Severity = "WARNING", CreatedAt = "2026-10-22T10:00:00+09:00",
        Device = new BaseDeviceDto { Id = deviceId, CategoryDevice = "gate" },
        Detail = JObject.Parse(detail),
    };

    internal static BaseDeviceModel Gate(int id, params ComponentDefinitionModel[] parts)
    {
        var spec = new HardwareSpecModel();
        foreach (var p in parts) spec.Components.Add(p);
        return new BaseDeviceModel { Id = id, DeviceType = EnumDeviceType.Gate, CategoryDevice = EnumDeviceCategory.Gate, Axes = new DeviceAxesModel { HardwareSpec = spec } };
    }

    [Fact]
    public void should_name_the_component_from_the_cached_device_in_every_text()
    {
        DeviceProviderStub.Add(Gate(501, OperationComponentTextTests.Part("actuator", "DOOR_ACTUATOR"), OperationComponentTextTests.Part("door", "DOOR_SENSOR")));

        var model = Dto(501, """{ "state": "OPEN", "previous": "RUNNING", "component": "actuator", "device_status": "ACTIVATED" }""")
            .ToOperationEventModel(DeviceProviderStub);

        Assert.Equal("문 구동부 구동 중 → 열림", model.ComponentChange!.Text);
        Assert.Equal("문 구동부 구동 중 → 열림", model.DetailText);
        Assert.Equal("문 구동부 구동 중 → 열림", new OperationEventViewModel(model).DetailText);     // 콘솔 목록 행 · 상세 칸
        var card = new OperationEventCardViewModel(model);
        Assert.Equal("문 구동부 구동 중 → 열림", card.ComponentText);                               // 카드 부품 줄
        Assert.Equal("문 구동부 구동 중 → 열림", card.DetailText);                                  // 조치보고 창 상세
        Assert.True(card.HasComponentText);
    }

    [Fact]
    public void should_show_the_key_with_the_states_when_the_event_comes_before_the_device_cache()
    {
        var model = Dto(502, """{ "state": "OPEN", "previous": "CLOSED", "component": "door" }""").ToOperationEventModel(DeviceProviderStub);

        Assert.Equal("door 닫힘 → 열림", model.ComponentChange!.Text);
        Assert.Equal("door 닫힘 → 열림", model.DetailText);
    }

    [Fact]
    public void should_use_the_server_catalog_name_from_the_container_when_registered()
    {
        IoC.GetAllInstances = type => type == typeof(IComponentTypeLabels)
            ? new object[] { new OperationComponentTextTests.Labels(("DOOR_SENSOR", "문 접점")) }
            : Enumerable.Empty<object>();
        DeviceProviderStub.Add(Gate(503, OperationComponentTextTests.Part("door", "DOOR_SENSOR")));

        var model = Dto(503, """{ "state": "OPEN", "previous": "CLOSED", "component": "door" }""").ToOperationEventModel(DeviceProviderStub);

        Assert.Equal("문 접점 닫힘 → 열림", model.DetailText);
    }

    [Fact]
    public void should_hide_the_card_component_line_for_environment_alarms()
    {
        var model = Dto(504, """{ "field": "temperature", "value": 65, "threshold": 60, "direction": "HIGH" }""").ToOperationEventModel(DeviceProviderStub);
        var card = new OperationEventCardViewModel(model);

        Assert.Null(model.ComponentChange);
        Assert.False(card.HasComponentText);
        Assert.Equal(string.Empty, card.ComponentText);
    }

    [Fact]
    public void should_refresh_the_component_line_when_the_server_updates_the_event()
    {
        var card = new OperationEventCardViewModel(Dto(505, """{ "state": "OPEN", "component": "door" }""").ToOperationEventModel(DeviceProviderStub));
        var changed = new List<string?>();
        card.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        card.ApplyServerUpdate(Dto(505, """{ "state": "CLOSED", "previous": "OPEN", "component": "door" }""").ToOperationEventModel(DeviceProviderStub));

        Assert.Equal("door 열림 → 닫힘", card.ComponentText);
        Assert.Contains(nameof(OperationEventCardViewModel.ComponentLineText), changed);
    }
}

/// <summary>실제 수신 경로(NATS OPERATION_EVENT → 카드 패널) — 카드에 부품 줄 · 흐름이 실제로 붙는가. 창은 띄우지 않는다.</summary>
[Collection("IoC-Dependent")]
public class OperationComponentCardScenarioTests
{
    private static List<OperationEventCardViewModel> Cards(EventMapRig rig)
        => rig.Panel.ViewModelProvider.OfType<OperationEventCardViewModel>().ToList();

    private static Task PublishAsync(EventMapRig rig, int deviceId, int eventId, string reason, string createdAt, string detail)
        => rig.PublishRawAsync($$"""
        { "id": "{{rig.NewEnvelopeId()}}", "m_type": "PUB", "cmd": "OPERATION_EVENT", "from": "server",
          "body": { "id": {{eventId}}, "reason": "{{reason}}", "severity": "WARNING", "created_at": "{{createdAt}}",
                    "device": { "id": {{deviceId}}, "category_device": "gate" }, "detail": {{detail}} } }
        """);

    private static void GiveParts(EventMapRig rig, int deviceId, params ComponentDefinitionModel[] parts)
    {
        var spec = new HardwareSpecModel();
        foreach (var p in parts) spec.Components.Add(p);
        rig.ServerDevices[deviceId].Axes = new DeviceAxesModel { HardwareSpec = spec };   // 캐시와 같은 인스턴스
    }

    [Fact]
    [Trait("Scenario", "OPC-017")]
    public async Task should_show_which_component_changed_on_the_operation_card()
    {
        using var rig = new EventMapRig();
        rig.Device(1701, EnumDeviceType.Gate);
        GiveParts(rig, 1701, OperationComponentTextTests.Part("door", "DOOR_SENSOR", "통문 접점"));

        await PublishAsync(rig, 1701, 61, "GATE_OPEN", "2026-10-22T10:00:00+09:00",
                           """{ "component": "door", "state": "OPEN", "previous": "CLOSED", "device_status": "ACTIVATED" }""");
        await rig.Panel.FlushPendingCardsNowAsync();

        var card = Assert.Single(Cards(rig));
        Assert.Equal("통문 열림", card.ReasonText);
        Assert.True(card.HasComponentText);
        Assert.Equal("통문 접점 닫힘 → 열림", card.ComponentLineText);
        Assert.False(card.HasFlowText);
    }

    [Fact]
    [Trait("Scenario", "OPC-018")]
    public async Task should_show_the_key_on_the_card_at_once_when_the_device_is_not_cached()
    {
        using var rig = new EventMapRig();

        await PublishAsync(rig, 1702, 62, "GATE_OPEN", "2026-10-22T10:00:00+09:00", """{ "component": "actuator", "state": "OPEN", "previous": null }""");
        await rig.Panel.FlushPendingCardsNowAsync();

        Assert.Equal("actuator 열림", Assert.Single(Cards(rig)).ComponentLineText);
    }

    [Fact]
    [Trait("Scenario", "OPC-019")]
    public async Task should_show_a_short_flow_on_the_later_card_when_components_change_within_30_seconds()
    {
        using var rig = new EventMapRig();
        rig.Device(1703, EnumDeviceType.Gate);
        GiveParts(rig, 1703, OperationComponentTextTests.Part("actuator", "DOOR_ACTUATOR"), OperationComponentTextTests.Part("door", "DOOR_SENSOR"));

        // 서버 실측 순서: 구동부 RUNNING→OPEN(이벤트) · 도어 센서 CLOSED→OPEN(이벤트) — 한 묶음 버퍼(150 ms) 안에 둘 다 와도 흐름이 선다.
        await PublishAsync(rig, 1703, 63, "GATE_OPEN", "2026-10-22T10:00:00+09:00", """{ "component": "actuator", "state": "OPEN", "previous": "RUNNING" }""");
        await PublishAsync(rig, 1703, 64, "GATE_OPEN", "2026-10-22T10:00:02+09:00", """{ "component": "door", "state": "OPEN", "previous": "CLOSED" }""");
        await rig.Panel.FlushPendingCardsNowAsync();

        var cards = Cards(rig);
        Assert.Equal(new[] { 64, 63 }, cards.Select(c => c.Model.Id));                 // 카드는 합치지 않는다
        Assert.Equal("문 구동부 구동 중 → 열림", cards[1].ComponentLineText);            // 앞 카드는 제 전이만
        Assert.False(cards[1].HasFlowText);
        Assert.True(cards[0].HasFlowText);
        Assert.Equal("문 구동부 열림 → 도어 센서 열림", cards[0].ComponentLineText);       // 나중 카드에 흐름
        Assert.Contains("도어 센서 닫힘 → 열림", cards[0].ComponentToolTip);              // 제 전이 전체는 말풍선에
    }

    [Fact]
    [Trait("Scenario", "OPC-019")]
    public async Task should_not_flow_across_more_than_30_seconds_or_other_devices()
    {
        using var rig = new EventMapRig();
        rig.Device(1704, EnumDeviceType.Gate);
        rig.Device(1705, EnumDeviceType.Gate);

        await PublishAsync(rig, 1704, 65, "GATE_OPEN", "2026-10-22T10:00:00+09:00", """{ "component": "door", "state": "OPEN", "previous": "CLOSED" }""");
        await PublishAsync(rig, 1705, 66, "GATE_OPEN", "2026-10-22T10:00:05+09:00", """{ "component": "door", "state": "OPEN", "previous": "CLOSED" }""");
        await PublishAsync(rig, 1704, 67, "GATE_CLOSED", "2026-10-22T10:00:45+09:00", """{ "component": "door", "state": "CLOSED", "previous": "OPEN" }""");
        await rig.Panel.FlushPendingCardsNowAsync();

        Assert.All(Cards(rig), c => Assert.False(c.HasFlowText));
        Assert.Equal(3, Cards(rig).Count);
    }

    [Fact]
    [Trait("Scenario", "OPC-017")]
    public void should_bind_the_component_line_but_not_the_raw_detail_on_the_card()
    {
        var xaml = OperationCardXaml();
        Assert.Contains("Text=\"{Binding ComponentLineText, Mode=OneWay}\"", xaml);
        Assert.Contains("Visibility=\"{Binding HasComponentText, Mode=OneWay, Converter={StaticResource BoolToVis}}\"", xaml);
        Assert.Contains("Visibility=\"{Binding HasFlowText, Mode=OneWay, Converter={StaticResource BoolToVis}}\"", xaml);
        Assert.DoesNotContain("DetailText", xaml);    // OPC-016 — 상세 원문은 여전히 카드에 없다
        Assert.True(Enum.TryParse<PackIconKind>("History", out _));
    }

    private static string OperationCardXaml([CallerFilePath] string here = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "..", "Views", "Events", "OperationEventCardView.xaml"));
}
