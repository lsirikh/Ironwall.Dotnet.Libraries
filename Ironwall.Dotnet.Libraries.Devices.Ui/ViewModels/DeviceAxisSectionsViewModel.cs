using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;

/// <summary>상세 절의 수신 상태. <b>미수신과 빈 값은 다른 것이다</b>(서버 D15).</summary>
public enum AxisSectionState
{
    /// <summary>이 응답에 그 절이 실리지 않았다(<c>view=basic</c> 등) — 값이 없다는 뜻이 아니다.</summary>
    NotReceived,
    /// <summary>절은 실려 왔는데 내용이 비어 있다.</summary>
    Empty,
    /// <summary>값이 있다.</summary>
    Present,
}

/// <summary>상세 절의 한 줄(이름 — 값).</summary>
public sealed record AxisRow(string Label, string Value);

/// <summary>부품 선언 한 줄(<c>hardware_spec.components[]</c>).</summary>
public sealed record ComponentRow(string Key, string Type, string Channel, string Position, string Label, string Model);

/// <summary>부품 관측 한 줄(<c>device_status.components.&lt;key&gt;</c>).</summary>
public sealed record ComponentStatusRow(string Key, string State, string Health, string FaultReason, string ObservedAt);

/// <summary>상세 절 하나 — 상태 + 줄들 + (값이 없을 때의) 안내 문구.</summary>
public sealed class AxisSection
{
    public AxisSection(AxisSectionState state, IReadOnlyList<AxisRow> rows, string emptyNotice = "—")
    {
        State = state;
        Rows = rows;
        Notice = state switch
        {
            AxisSectionState.NotReceived => DeviceAxisSectionsViewModel.NotReceivedNotice,
            AxisSectionState.Empty => emptyNotice,
            _ => string.Empty,
        };
    }

    public AxisSectionState State { get; }
    public IReadOnlyList<AxisRow> Rows { get; }
    public string Notice { get; }
    public bool IsPresent => State == AxisSectionState.Present;
    public bool IsNotReceived => State == AxisSectionState.NotReceived;
    public bool IsEmpty => State == AxisSectionState.Empty;

    public static readonly AxisSection Missing = new(AxisSectionState.NotReceived, Array.Empty<AxisRow>());
}

/// <summary>
/// 상세 폼의 v7.0+ <b>표현 축 절</b>(접속 · 형상 · 부품 · 상태 · 설정 · 응답 프로필) — 7 카테고리 공용, <b>읽기 전용</b>
/// (device-console-v8 FR-11 · FR-13).
/// </summary>
/// <remarks>
/// <para>형상·부품·상태·설정을 읽기 전용으로 둔 이유: 서버가 <c>components</c> 배열을 통째 교체한다 — 일부만 보내면
/// 나머지 부품이 경고 없이 삭제된다(8.0.1 실측). 부품 편집은 전체 배열을 read-modify-write 하는 조립기 화면의 몫이다.</para>
/// <para>6.3 계약에서는 <see cref="IsVisible"/> 가 <c>false</c> — 옛 상세 폼이 그대로 보인다.</para>
/// </remarks>
public sealed class DeviceAxisSectionsViewModel : PropertyChangedBase
{
    public const string NotReceivedNotice = "이 응답에 실리지 않았습니다";

    /// <summary>상세 폼의 절 순서(고정). 타입 특화 설정은 뒤쪽이다.</summary>
    public static readonly IReadOnlyList<string> SectionOrder = new[]
    {
        "common", "connection", "hardware_spec", "components", "device_status", "location", "groups", "device_config", "meta",
    };

    private DeviceAxisSectionsViewModel() { }

    /// <summary>선택된 장비(들)에서 절을 만든다. 축 정보는 <b>한 대를 골랐을 때만</b> 보인다.</summary>
    public static DeviceAxisSectionsViewModel From(IReadOnlyList<IBaseDeviceModel> selection, DeviceQueryPolicy? policy = null)
    {
        policy ??= DeviceQueryPolicy.Resolve();
        var vm = new DeviceAxisSectionsViewModel { IsVisible = policy.IsAxisContract && selection.Count > 0 };
        if (!vm.IsVisible) return vm;

        var category = DeviceAxesMapper.CategoryOf(selection[0]);
        vm._category = category;
        vm.CategoryText = category == EnumDeviceCategory.None ? "—" : category.ToString().ToLowerInvariant();
        vm.PathText = category == EnumDeviceCategory.None ? "—" : $"/api/devices/{vm.CategoryText}s";
        vm.IsSingle = selection.Count == 1;
        if (!vm.IsSingle) return vm;

        var axes = selection[0].Axes;
        vm.MetaView = axes?.Meta?.View ?? "—";
        vm.MetaSections = axes?.Meta?.Sections ?? Array.Empty<string>();

        vm.Connection = BuildConnection(axes?.Connection);
        vm.HardwareSpec = BuildHardwareSpec(axes?.HardwareSpec);

        if (axes?.HardwareSpec != null)
        {
            vm.ComponentRows = axes.HardwareSpec.Components
                .Select(c => new ComponentRow(c.Key, c.Type, Text(c.Channel), Text(c.Position), Text(c.Label), Text(c.Model)))
                .ToList();
            vm.Components = new AxisSection(vm.ComponentRows.Count == 0 ? AxisSectionState.Empty : AxisSectionState.Present,
                Array.Empty<AxisRow>(), emptyNotice: "형상 미입력");
        }

        if (axes?.DeviceStatus != null)
        {
            vm.StatusRows = axes.DeviceStatus.Components
                .Select(kv => new ComponentStatusRow(kv.Key, Text(kv.Value.State), Text(kv.Value.Health), Text(kv.Value.FaultReason), Text(kv.Value.ObservedAt)))
                .ToList();
            vm.DeviceStatus = new AxisSection(vm.StatusRows.Count == 0 ? AxisSectionState.Empty : AxisSectionState.Present, Array.Empty<AxisRow>());
        }

        vm.DeviceConfig = BuildConfig(axes?.DeviceConfig);
        return vm;
    }

    /// <summary>방위(heading)가 의미를 갖는 장비인가 — 방향성이 있는 카메라·스피커·센서만.</summary>
    public static bool IsHeadingApplicable(EnumDeviceCategory category)
        => category is EnumDeviceCategory.Camera or EnumDeviceCategory.Speaker or EnumDeviceCategory.Sensor;

    #region - Properties -
    /// <summary>축 절을 보일지 — v7.0+ 계약이고 선택이 있을 때.</summary>
    public bool IsVisible { get; private set; }

    /// <summary>방위각 입력란을 보일지 — 6.3 화면에서는 종전대로 항상, 축 화면에서는 방향성 있는 장비(카메라·스피커·센서)만.</summary>
    public bool IsHeadingFieldVisible => !IsVisible || IsHeadingApplicable(_category);
    private EnumDeviceCategory _category = EnumDeviceCategory.None;

    /// <summary>한 대만 선택됐는가. 여러 대면 축 절 대신 안내를 보인다.</summary>
    public bool IsSingle { get; private set; }
    public bool IsMultiple => IsVisible && !IsSingle;

    /// <summary>판별자(<c>category_device</c>) — 읽기 전용 배지. 경로가 정본이고 바뀌지 않는다.</summary>
    public string CategoryText { get; private set; } = "—";

    /// <summary>이 장비가 속한 API 경로 — 판별자가 "어디서 왔는가"임을 보인다.</summary>
    public string PathText { get; private set; } = "—";

    public AxisSection Connection { get; private set; } = AxisSection.Missing;
    public AxisSection HardwareSpec { get; private set; } = AxisSection.Missing;
    public AxisSection Components { get; private set; } = AxisSection.Missing;
    public AxisSection DeviceStatus { get; private set; } = AxisSection.Missing;
    public AxisSection DeviceConfig { get; private set; } = AxisSection.Missing;

    public IReadOnlyList<ComponentRow> ComponentRows { get; private set; } = Array.Empty<ComponentRow>();
    public IReadOnlyList<ComponentStatusRow> StatusRows { get; private set; } = Array.Empty<ComponentStatusRow>();

    public string MetaView { get; private set; } = "—";
    public IReadOnlyList<string> MetaSections { get; private set; } = Array.Empty<string>();
    #endregion

    #region - Builders -
    private static AxisSection BuildConnection(IConnectionAxisModel? c)
    {
        if (c == null) return AxisSection.Missing;

        var rows = new List<AxisRow>();
        Add(rows, "접속 방식", c.Type);
        Add(rows, "IP", c.IpAddress);
        Add(rows, "포트", c.IpPort?.ToString());
        Add(rows, "계정", c.UserName);          // 비밀번호는 화면에 싣지 않는다
        Add(rows, "상위 장비", c.ParentDeviceId?.ToString());
        Add(rows, "채널", c.Channel?.ToString());
        Add(rows, "프로토콜", c.Protocol);
        foreach (var url in c.Urls) Add(rows, $"링크 · {url.Key}", url.Value);
        return new AxisSection(rows.Count == 0 ? AxisSectionState.Empty : AxisSectionState.Present, rows);
    }

    private static AxisSection BuildHardwareSpec(IHardwareSpecModel? h)
    {
        if (h == null) return AxisSection.Missing;

        var rows = new List<AxisRow>();
        Add(rows, "제조사", h.Manufacturer);
        Add(rows, "모델", h.Model);
        Add(rows, "일련번호", h.Serial);
        Add(rows, "펌웨어", h.Firmware);
        Add(rows, "하드웨어 판", h.HardwareRev);
        Add(rows, "MAC", h.MacAddress);
        Add(rows, "최대 탐지거리(m)", h.MaxDetectionRange?.ToString());
        Add(rows, "ONVIF", h.OnvifVersion);
        Flatten(rows, "spec", h.Spec);
        return new AxisSection(rows.Count == 0 ? AxisSectionState.Empty : AxisSectionState.Present, rows);
    }

    private static AxisSection BuildConfig(IDeviceConfigModel? c)
    {
        if (c == null) return AxisSection.Missing;

        var rows = new List<AxisRow>();
        Flatten(rows, "thresholds", c.Thresholds);
        Flatten(rows, "modes", c.Modes);
        Flatten(rows, "component_overrides", c.ComponentOverrides);
        return new AxisSection(rows.Count == 0 ? AxisSectionState.Empty : AxisSectionState.Present, rows);
    }

    private static void Add(List<AxisRow> rows, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) rows.Add(new AxisRow(label, value!.Trim()));
    }

    // 키 구성이 카탈로그에 달린 자유 객체 — "a.b.c = 값" 으로 펴서 모르는 키도 버리지 않고 보인다.
    private static void Flatten(List<AxisRow> rows, string prefix, JToken? token)
    {
        switch (token)
        {
            case null:
            case JValue { Type: JTokenType.Null }:
                return;
            case JObject obj:
                foreach (var property in obj.Properties()) Flatten(rows, $"{prefix}.{property.Name}", property.Value);
                return;
            case JArray array:
                rows.Add(new AxisRow(prefix, array.ToString(Newtonsoft.Json.Formatting.None)));
                return;
            case JValue { Type: JTokenType.Boolean } flag:
                rows.Add(new AxisRow(prefix, (bool)flag ? "true" : "false"));
                return;
            default:
                rows.Add(new AxisRow(prefix, token.ToString()));
                return;
        }
    }

    private static string Text(object? value)
    {
        var text = value?.ToString();
        return string.IsNullOrWhiteSpace(text) ? "—" : text!;
    }
    #endregion
}
