using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Register;

/****************************************************************************
   Purpose      : 프리셋 + 개체 정보 → 카테고리별 생성 본문 (device-assembly-preset FR-12·FR-13)
   Created By   : GHLee
   Created On   : 9/19/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 보낼 준비가 끝난 생성 요청 한 건. <see cref="PreviewJson"/> 이 <b>그대로 나가는 본문</b>이다(FR-12 3단계).
/// </summary>
/// <param name="Category">경로를 정하는 카테고리 — 본문에는 <c>category_device</c> 로 실리지 않는다(경로가 정본).</param>
/// <param name="Dto">보낼 DTO 인스턴스. 플래그 3종이 <b>이 인스턴스에만</b> 켜져 있다.</param>
/// <param name="PreviewJson">
/// <see cref="Dto"/> 를 <c>ApiService</c> 와 <b>같은 직렬화 설정</b>으로 찍은 문자열 — 와이어 바이트와 같다.
/// 사람이 읽게 들여쓰려면 창에서 <c>JToken.Parse(PreviewJson).ToString(Formatting.Indented)</c> 로 다시 찍는다
/// (여기서 들여쓰면 "그대로 나간다"가 거짓이 된다).
/// <para>⚠ <c>unit_id</c> 는 여기에 <b>없다</b> — 보내기 직전 <c>UnitScopeGate</c> 가 찍고(패널과 같은 관문),
/// <c>UseAxisWrite</c> 도 <c>DeviceApiService</c> 가 서버 계약 세대로 다시 정한다.</para>
/// </param>
public sealed record PresetRequest(EnumDeviceCategory Category, BaseDeviceDto Dto, string PreviewJson);

/// <summary>
/// 프리셋(구조) + 개체 정보(그 장비만의 것)를 <b>POST 1건의 본문</b>으로 만든다(FR-12 · FR-13).
/// </summary>
/// <remarks>
/// <para><b>기존 모델 → DTO 매핑을 그대로 탄다</b>(<see cref="DtoToModelHelper"/>) — 카테고리별 종류축 이름,
/// 빈 값 생략, 레거시 키 억제 같은 규칙이 거기에 이미 적혀 있고, 여기서 다시 쓰면 두 곳이 갈린다.
/// 이 클래스가 더하는 것은 <b>축 쓰기 플래그 3종</b>과 <c>hardware_spec</c>·<c>device_config</c> 뿐이다.</para>
/// <para><b>플래그는 인스턴스 값이다</b>(정적 아님) — 그래서 패널의 저장 경로가 만든 DTO 는 한 바이트도
/// 달라지지 않는다(NFR-01). 대신 등록은 <b>전용 경로</b>여야 한다(PRD 5-B).</para>
/// </remarks>
public static class PresetRequestBuilder
{
    #region - Validate -
    /// <summary>
    /// 보내면 안 되는 상태를 사람이 읽는 문장으로 모은다. 빈 목록 = 보내도 된다.
    /// </summary>
    /// <param name="catalog">
    /// 부품 유형 카탈로그. <c>IsLoaded</c> 가 아니면 유형 관련 검사는 <b>건너뛴다</b> —
    /// 못 읽은 카탈로그로 "이 유형은 없다"고 판정하면 정상 프리셋이 전부 막힌다(6.3 계약에서는 아예 안 읽는다).
    /// </param>
    public static IReadOnlyList<string> Validate(DevicePreset preset, PresetInstanceInfo info, IComponentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(info);

        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(info.DeviceName))
            problems.Add("장비 이름을 입력해야 합니다.");

        if (info.DeviceNumber < 1)
            problems.Add("장비 번호는 1 이상이어야 합니다.");

        if (preset.Category == EnumDeviceCategory.Sensor && (info.Controller == null || info.Controller.Id <= 0))
            problems.Add("센서는 소속 제어기를 지정해야 합니다(제어기 없이 등록하면 서버가 404 로 거절합니다).");

        // 포트는 "그 카테고리가 접속 칸을 보낼 때만" 본다 — 접속 축이 아예 없는 카테고리(통문 — 결선은
        // link_info/channel 이지 PresetInstanceInfo.IpPort 가 아니다)는 BuildCategoryDto 가 이 값을 쓰지 않으므로
        // 범위 검사해 봐야 아무 데도 안 닿는다(등록 창은 ShowsConnection 으로 입력 자체를 막지만, Validate 를
        // 직접 부르는 호출자에게는 "검사를 통과했다"가 "전달된다"를 뜻하지 않는 거짓 안전감을 준다).
        // (D-21 수정) 센서·스피커·함체도 서버 ConnectionAxis 를 실제로 받는다 — "접속 축이 없다"는 결함이었다.
        if (HasConnectionAxis(preset.Category) && info.IpPort is { } port && (port < 1 || port > 65535))
            problems.Add($"접속 포트({port})는 1~65535 범위여야 합니다.");

        var loaded = catalog is { IsLoaded: true };
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var component in preset.Components ?? Array.Empty<ComponentDefinitionModel>())
        {
            if (component == null) continue;

            var key = component.Key ?? string.Empty;
            if (!ComponentKeyPattern.IsMatch(key))
                problems.Add($"부품 key '{key}' 는 형식이 올바르지 않습니다 — ^[a-z][a-z0-9_]*$ 만 받습니다.");
            else if (!seen.Add(key))
                problems.Add($"부품 key '{key}' 가 중복입니다 — 한 장비 안에서 유일해야 합니다.");

            var type = component.Type;
            if (string.IsNullOrWhiteSpace(type))
            {
                problems.Add($"부품 '{key}' 의 유형이 비어 있습니다.");
            }
            else if (loaded)
            {
                var info2 = catalog.Find(type);
                if (info2 == null)
                    problems.Add($"부품 유형 '{type}' 이(가) 카탈로그에 없습니다 — 프리셋을 고쳐야 등록할 수 있습니다.");
                else if (!info2.AppliesToCategory(preset.Category))
                    problems.Add($"부품 유형 '{type}' 은(는) {CategoryText(preset.Category)} 에 달 수 없습니다.");
            }

            // 유형 공통 사실 8개 — 부품 칸에 섞여 있으면 첫 POST 가 422 로 죽는다(AS L213-215).
            foreach (var forbidden in ForbiddenOnComponent(component.Spec))
                problems.Add($"부품 '{key}' 에 유형 공통 사실 '{forbidden}' 이(가) 들어 있습니다 — 요청에 실으면 422 입니다.");
        }

        // component_overrides 의 enabled 는 정상이다(같은 낱말이 부품 칸에서는 금지 · 재정의 칸에서는 정본).
        foreach (var (overrideKey, forbidden) in ForbiddenInOverrides(preset.ComponentOverrides))
            problems.Add($"재정의 '{overrideKey}' 에 유형 공통 사실 '{forbidden}' 이(가) 들어 있습니다 — 요청에 실으면 422 입니다.");

        return problems;
    }
    #endregion

    #region - Build -
    /// <summary>
    /// 카테고리별 생성 DTO 를 만든다. 지원하지 않는 카테고리면 <see cref="ArgumentException"/>.
    /// </summary>
    /// <remarks>
    /// 본문에 <b>절대 없는 것</b>: <c>id</c>(기본값 생략) · <c>category_device</c>(경로가 정본) ·
    /// <c>device_status</c>(관측 · 422) · <c>serial</c>·<c>mac_address</c>(그 장비만의 것) ·
    /// <c>geolocation</c>·<c>group_ids</c>(배치 — 프리셋이 담지 않는다) · 유형 공통 사실 8개.
    /// </remarks>
    public static PresetRequest Build(DevicePreset preset, PresetInstanceInfo info)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(info);

        var spec = BuildHardwareSpec(preset);
        var config = BuildDeviceConfig(preset);
        var dto = BuildCategoryDto(preset, info, spec);

        // ① 축 쓰기 ② 부품 배열 ③ 설정 축 — 셋 다 인스턴스 플래그다(정적 아님).
        dto.UseAxisWrite = true;
        dto.AllowDeviceConfigWrite = true;
        dto.DeviceConfigWrite = config;

        var preview = JsonConvert.SerializeObject(dto, WireSettings);
        return new PresetRequest(preset.Category, dto, preview);
    }

    /// <summary>
    /// <c>ApiService._jsonSettings</c> 와 같은 설정(그 필드가 <c>private static</c> 이라 값을 옮겨 적는다).
    /// 바뀌면 미리보기와 와이어가 갈린다 — 그쪽을 고치면 여기도 고친다.
    /// </summary>
    internal static readonly JsonSerializerSettings WireSettings = new()
    {
        DateFormatHandling = DateFormatHandling.IsoDateFormat,
        DateTimeZoneHandling = DateTimeZoneHandling.Local,
    };

    private static HardwareSpecDto BuildHardwareSpec(DevicePreset preset)
    {
        var components = new List<ComponentDefinitionDto>();
        foreach (var c in preset.Components ?? Array.Empty<ComponentDefinitionModel>())
        {
            if (c == null) continue;
            components.Add(ToComponentDto(c));
        }

        return new HardwareSpecDto
        {
            Schema = 1,
            Manufacturer = preset.Manufacturer ?? string.Empty,
            Model = preset.Model ?? string.Empty,
            Firmware = preset.Firmware ?? string.Empty,
            // 축 모드에서 'hardware' 는 꺼지고 'hardware_rev' 가 이 값을 싣는다(HardwareSpecDto.HardwareRevAxis).
            Hardware = preset.HardwareRev ?? string.Empty,
            OnvifVersion = preset.OnvifVersion ?? string.Empty,
            MaxDetectionRange = preset.MaxDetectionRange,
            Components = components,
            UseAxisWrite = true,
            // ★ FR-13 의 한 줄 — 빠뜨리면 POST 가 부품을 조용히 빼고 성공한다(HardwareSpecDto:145-147).
            AllowComponentsWrite = true,
        };
    }

    /// <summary>부품 선언 한 개 — <b>장비별 사실만</b>. 유형 공통 사실은 담을 자리가 애초에 없다.</summary>
    internal static ComponentDefinitionDto ToComponentDto(ComponentDefinitionModel c) => new()
    {
        Key = c.Key,
        Type = c.Type,
        Label = c.Label,
        InService = c.InService,
        Channel = c.Channel,
        Position = c.Position,
        Manufacturer = c.Manufacturer,
        Model = c.Model,
        Serial = c.Serial,
        Firmware = c.Firmware,
        HardwareRev = c.HardwareRev,
        InstalledAt = c.InstalledAt,
        ReplacedAt = c.ReplacedAt,
        Spec = c.Spec,
    };

    /// <summary>
    /// 임계치 · 모드 · 부품 재정의를 공통 통로(<see cref="BaseDeviceDto.DeviceConfigWrite"/>)에 담는다.
    /// </summary>
    /// <remarks>
    /// 어떤 묶음을 받아 주는지는 <b>서버가 판정한다</b>(모드는 카메라만 등) — 여기서 임의로 걸러 내면
    /// 값이 조용히 사라지고 아무도 모른다. 셋 다 비면 <c>null</c> 이라 <c>device_config</c> 키 자체가 안 나간다.
    /// </remarks>
    private static DeviceConfigAxisDto? BuildDeviceConfig(DevicePreset preset)
    {
        var config = new DeviceConfigAxisDto
        {
            Thresholds = Clone(preset.Thresholds),
            Modes = Clone(preset.Modes),
            ComponentOverrides = Clone(preset.ComponentOverrides),
        };
        return config.IsEmpty ? null : config;
    }

    private static JObject? Clone(JObject? source)
        => source == null || source.Count == 0 ? null : (JObject)source.DeepClone();

    private static BaseDeviceDto BuildCategoryDto(DevicePreset preset, PresetInstanceInfo info, HardwareSpecDto spec)
    {
        var type = NullIfEmpty(preset.TypeAxisCode);
        var extra = NullIfEmpty(preset.ExtraAxisCode);

        switch (preset.Category)
        {
            case EnumDeviceCategory.Controller:
            {
                var model = new ControllerDeviceModel
                {
                    DeviceNumber = info.DeviceNumber,
                    DeviceName = info.DeviceName,
                    IpAddress = info.IpAddress ?? string.Empty,
                    Port = info.IpPort ?? 0,
                };
                var dto = model.ToControllerDeviceDto();
                dto.TypeControllerAxis = type;   // setter 가 TypeDevice 로 되돌려 실어 준다
                dto.HardwareSpec = spec;
                dto.Description = info.Description;
                return dto;
            }

            case EnumDeviceCategory.Sensor:
            {
                var model = new SensorDeviceModel
                {
                    DeviceNumber = info.DeviceNumber,
                    DeviceName = info.DeviceName,
                };
                // 패널과 같은 방식 — 모델은 제어기 '객체'를 들고, DTO 는 controller_id 만 싣는다.
                if (info.Controller is ControllerDeviceModel concrete) model.Controller = concrete;
                var dto = model.ToSensorDeviceDto();
                dto.ControllerId = info.Controller?.Id ?? 0;   // 0 이면 ShouldSerializeControllerId 가 뺀다
                dto.TypeSensorAxis = type;
                dto.HardwareSpec = spec;
                dto.Description = info.Description;
                // (D-21) IP 기반 센서만 쓴다 — RS485 버스 주소는 이 창이 아직 받지 않는다(향후 확장).
                dto.IpAddress = info.IpAddress;
                dto.IpPort = info.IpPort;
                return dto;
            }

            case EnumDeviceCategory.Camera:
            {
                var model = new CameraDeviceModel
                {
                    DeviceNumber = info.DeviceNumber,
                    DeviceName = info.DeviceName,
                    IpAddress = info.IpAddress ?? string.Empty,
                    IpPort = info.IpPort ?? 0,
                    UserName = info.UserName,
                    UserPassword = info.UserPassword,
                };
                var dto = model.ToCameraDeviceDto();
                dto.TypeCameraAxis = type;
                dto.HardwareSpec = spec;
                dto.Description = info.Description;
                return dto;
            }

            case EnumDeviceCategory.Speaker:
            {
                var model = new SpeakerDeviceModel
                {
                    DeviceNumber = info.DeviceNumber,
                    DeviceName = info.DeviceName,
                    Description = info.Description,
                    // 스피커만 종류축이 둘이다 — 역할(speaker_role)은 부가 축에서 온다.
                    SpeakerType = extra ?? "NORMAL",
                    // (D-21) 서버 SpeakerCreate.connection 은 선택 축이다 — 방송서버(server_id) 경유와 별개로
                    // IP_DIRECT 접속도 받는다(app/schemas/device.py:718).
                    IpAddress = info.IpAddress,
                    IpPort = info.IpPort,
                };
                var dto = model.ToSpeakerDeviceDto();
                dto.TypeSpeaker = type;   // 하우징 형상 축
                dto.HardwareSpec = spec;
                dto.IpAddress = info.IpAddress;
                dto.IpPort = info.IpPort;
                return dto;
            }

            case EnumDeviceCategory.Enclosure:
            {
                var model = new EnclosureDeviceModel
                {
                    DeviceNumber = info.DeviceNumber,
                    DeviceName = info.DeviceName,
                    // (D-21) 서버 EnclosureCreate.connection 은 선택 축이지만 실제로 IP_DIRECT 를 받는다
                    // (app/schemas/device.py:776) — 이 두 칸이 빠져 있던 것이 라이브 하네스 FAIL 3b2 의 원인이었다.
                    IpAddress = info.IpAddress,
                    IpPort = info.IpPort,
                };
                var dto = model.ToEnclosureDeviceDto();
                dto.TypeEnclosure = type;
                dto.HardwareSpec = spec;
                dto.Description = info.Description;
                dto.IpAddress = info.IpAddress;
                dto.IpPort = info.IpPort;
                return dto;
            }

            case EnumDeviceCategory.Lamp:
            {
                var model = new LampDeviceModel
                {
                    DeviceNumber = info.DeviceNumber,
                    DeviceName = info.DeviceName,
                    IpAddress = info.IpAddress ?? string.Empty,
                    IpPort = info.IpPort ?? 0,
                    UserName = info.UserName,
                    UserPassword = info.UserPassword,
                    Description = info.Description,
                };
                var dto = model.ToLampDeviceDto();
                dto.TypeLamp = type;
                dto.HardwareSpec = spec;
                return dto;
            }

            case EnumDeviceCategory.Gate:
            {
                var model = new GateDeviceModel
                {
                    DeviceNumber = info.DeviceNumber,
                    DeviceName = info.DeviceName,
                    TypeAxisCode = type,
                };
                var dto = model.ToGateDeviceDto();
                dto.HardwareSpec = spec;
                dto.Description = info.Description;
                return dto;
            }

            default:
                throw new ArgumentException($"프리셋으로 등록할 수 없는 카테고리입니다: {preset.Category}", nameof(preset));
        }
    }
    #endregion

    #region - Forbidden type-level facts -
    /// <summary>
    /// 요청에 실으면 <b>전부 422</b> 인 유형 공통 사실 8개(AS L213-215). 카탈로그가 정본이고 장비가 되풀이해 갖지 않는다.
    /// </summary>
    internal static readonly string[] FORBIDDEN_TYPE_FACTS =
    {
        "states", "commands", "command_params", "readable", "controllable", "produces", "critical", "enabled",
    };

    /// <summary><c>enabled</c> 가 정상인 유일한 자리는 <c>component_overrides</c> 다 — 거기서는 이 낱말을 뺀다.</summary>
    private static readonly string[] FORBIDDEN_IN_OVERRIDES =
    {
        "states", "commands", "command_params", "readable", "controllable", "produces", "critical",
    };

    private static IEnumerable<string> ForbiddenOnComponent(JObject? spec)
    {
        if (spec == null) yield break;
        foreach (var name in FORBIDDEN_TYPE_FACTS)
        {
            if (spec.Property(name, StringComparison.OrdinalIgnoreCase) != null) yield return name;
        }
    }

    private static IEnumerable<(string Key, string Fact)> ForbiddenInOverrides(JObject? overrides)
    {
        if (overrides == null) yield break;
        foreach (var property in overrides.Properties())
        {
            if (property.Value is not JObject entry) continue;
            foreach (var name in FORBIDDEN_IN_OVERRIDES)
            {
                if (entry.Property(name, StringComparison.OrdinalIgnoreCase) != null) yield return (property.Name, name);
            }
        }
    }
    #endregion

    #region - Helpers -
    /// <summary>부품 key 형식 — 서버가 이 정규식으로 검사하고 벗어나면 422 다.</summary>
    internal static readonly Regex ComponentKeyPattern =
        new("^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// 이 카테고리의 DTO 가 접속(IP/포트) 축을 실제로 갖는가 — <see cref="BuildCategoryDto"/> 가 <c>IpAddress</c>·
    /// <c>IpPort</c> 를 <see cref="PresetInstanceInfo"/> 에서 채우는 카테고리와 정확히 같다.
    /// </summary>
    /// <remarks>
    /// <para><b>D-21 정정(2026-09-23)</b> — "센서·스피커·함체·통문은 서버 계약에 접속 축이 없다"는 예전 결론은
    /// <b>틀렸다</b>. 서버 스키마(<c>app/schemas/device.py:97-109</c>)는 <b>7 카테고리 전부</b>에 <c>connection</c>
    /// 을 선언한다 — 실제로 없었던 것은 <b>클라 쪽 구현</b>이다(8.0 이 접속을 <c>connection{}</c> 축으로 옮기며
    /// 우리 <c>ConnectionAxis</c> 는 <c>Controller</c>·<c>Camera</c>·<c>Gate</c>·<c>Lamp</c> DTO 4개에만 붙었고
    /// 함체·센서·스피커는 빠졌다 — 라이브 하네스 FAIL 3b2 로 실측 확인).</para>
    /// <para><b>통문만 제외</b>한다 — 통문의 결선(<c>link_info</c>·<c>channel</c>)은 <see cref="PresetInstanceInfo"/>
    /// 의 <c>IpAddress</c>·<c>IpPort</c> 를 쓰지 않고 프리셋 쪽에서 오므로, 이 게이트("<c>info</c> 의 IP 를
    /// 이 카테고리가 쓰는가")의 대상이 아니다.</para>
    /// </remarks>
    private static bool HasConnectionAxis(EnumDeviceCategory category)
        => category is EnumDeviceCategory.Controller or EnumDeviceCategory.Camera or EnumDeviceCategory.Lamp
            or EnumDeviceCategory.Enclosure or EnumDeviceCategory.Speaker or EnumDeviceCategory.Sensor;

    private static string CategoryText(EnumDeviceCategory category) => category switch
    {
        EnumDeviceCategory.Controller => "제어기",
        EnumDeviceCategory.Sensor => "센서",
        EnumDeviceCategory.Camera => "카메라",
        EnumDeviceCategory.Speaker => "스피커",
        EnumDeviceCategory.Enclosure => "함체",
        EnumDeviceCategory.Lamp => "경광등",
        EnumDeviceCategory.Gate => "통문",
        _ => category.ToString(),
    };
    #endregion
}
