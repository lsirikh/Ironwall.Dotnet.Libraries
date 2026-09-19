using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;

// 조립기 · 프리셋 · 등록이 함께 쓰는 계약(device-assembly-preset PRD 3절). 네 조각이 이 파일만 공유한다 —
// 여기의 모양을 바꾸면 네 조각이 같이 바뀐다.

/// <summary>
/// 부품의 가족 — 블록의 <b>형태</b>를 정한다(색이 아니라 형태로 가른다, FR-04).
/// 감지 = 둥근 칩 + 왼쪽 위 홈 · 구동 = 네모 + 오른쪽 아래 삼각 · 전원/환경 = 육각 · 네트워크 = 마름모 · 광학 = 둥근 칩 + 원 · 기타 = 둥근 칩.
/// </summary>
public enum ComponentFamily
{
    Sensing,
    Actuation,
    PowerEnvironment,
    Network,
    Optics,
    Other,
}

/// <summary>
/// 카탈로그(<c>GET /api/devices/spec</c> 의 <c>component_type</c>) 한 줄 — <b>유형 공통 사실</b>.
/// 이 값들은 보여 주기만 한다. 요청에 실으면 서버가 422 로 거절한다(states · commands · produces … — AS L213-215).
/// </summary>
public sealed record ComponentTypeInfo
{
    /// <summary>유형 코드(대문자 정규화). 예: <c>DOOR_SENSOR</c>.</summary>
    public required string Code { get; init; }

    public required string Label { get; init; }

    public ComponentFamily Family { get; init; } = ComponentFamily.Other;

    /// <summary>달 수 있는 카테고리. null = 전 카테고리.</summary>
    public IReadOnlyCollection<EnumDeviceCategory>? AppliesTo { get; init; }

    public IReadOnlyList<string> States { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Commands { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Produces { get; init; } = Array.Empty<string>();

    /// <summary>재정의할 수 있는 설정 이름(<c>device_config.component_overrides.&lt;key&gt;.*</c>).</summary>
    public IReadOnlyList<string> OverrideParams { get; init; } = Array.Empty<string>();

    public bool IsDeprecated { get; init; }

    /// <summary>동작 상태를 보고하는 유형인가. 아니면 건강만 보고한다 — 조립 때 알려 줘야 "상태가 왜 안 오냐"가 안 생긴다.</summary>
    public bool ReportsState => States.Count > 0;

    public bool AppliesToCategory(EnumDeviceCategory category) => AppliesTo is null || AppliesTo.Contains(category);

    /// <summary>팔레트 · 말풍선에 보일 글.</summary>
    public string Display => string.Equals(Code, Label, StringComparison.Ordinal) ? Label : $"{Label} ({Code})";
}

/// <summary>
/// 부품 유형 카탈로그. <see cref="Services.ICatalogService"/> 를 넓히지 않고 따로 둔다 — 그 인터페이스에 멤버를 더하면
/// 테스트의 가짜 구현들이 한꺼번에 깨진다(실증된 함정). 같은 <c>CatalogService</c> 가 둘 다 구현한다.
/// </summary>
public interface IComponentCatalog
{
    bool IsLoaded { get; }

    /// <summary>카탈로그가 다시 읽혔다.</summary>
    event EventHandler? CatalogChanged;

    /// <summary>필요하면 읽는다(세션당 1회 · 6.3 계약이면 서버를 부르지 않고 false). 예외를 던지지 않는다.</summary>
    Task<bool> EnsureLoadedAsync(CancellationToken token = default);

    /// <summary>그 카테고리에 달 수 있는 유형들(팔레트). 폐기된 유형은 기본으로 뺀다.</summary>
    IReadOnlyList<ComponentTypeInfo> ComponentTypes(EnumDeviceCategory category, bool includeDeprecated = false);

    /// <summary>코드로 찾는다(대소문자 무시 · 폐기된 것도 찾는다). 없으면 null — 카탈로그에서 사라진 유형이다.</summary>
    ComponentTypeInfo? Find(string? code);
}

/// <summary>
/// 프리셋 — 조립 결과의 <b>구조</b>만 담는다(FR-09). 그 장비만의 것(번호 · 이름 · 접속 · 일련번호 · 위치 · 그룹)과
/// 서버의 것(관측 · 유형 공통 사실)은 담지 않는다.
/// </summary>
public sealed record DevicePreset
{
    /// <summary>안정된 식별자(Guid "N"). 이름은 바뀔 수 있다.</summary>
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>프리셋은 카테고리에 묶인다 — 함체 프리셋을 카메라에 쓸 수 없다(부품의 applies_to 가 다르다).</summary>
    public required EnumDeviceCategory Category { get; init; }

    /// <summary>종류 축(<c>type_&lt;category&gt;</c>) 코드. 담아 두면 등록 창이 한 칸 덜 묻는다.</summary>
    public string? TypeAxisCode { get; init; }

    /// <summary>부가 축(스피커의 <c>speaker_role</c>).</summary>
    public string? ExtraAxisCode { get; init; }

    public string? Description { get; init; }

    /// <summary>부품의 장비별 사실. <c>Serial</c> · <c>InstalledAt</c> · <c>ReplacedAt</c> 은 저장할 때 비운다(그 장비만의 것).</summary>
    public IReadOnlyList<ComponentDefinitionModel> Components { get; init; } = Array.Empty<ComponentDefinitionModel>();

    // hardware_spec 제원 기본값
    public string? Manufacturer { get; init; }
    public string? Model { get; init; }
    public string? Firmware { get; init; }
    public string? HardwareRev { get; init; }
    public double? MaxDetectionRange { get; init; }
    public string? OnvifVersion { get; init; }

    // device_config 기본값
    public JObject? Thresholds { get; init; }
    public JObject? Modes { get; init; }

    /// <summary>부품별 재정의(<c>component_overrides.&lt;key&gt;</c>). 부품 칸의 값과 섞지 않는다 — <c>enabled</c> 는 여기서만 정상이다.</summary>
    public JObject? ComponentOverrides { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>처음 실행 때 심어 주는 본보기 프리셋인가.</summary>
    public bool IsSeed { get; init; }
}

/// <summary>프리셋으로 등록할 때 사람이 넣는 것 — 프리셋이 <b>담지 않는</b> 개체 정보(FR-12).</summary>
public sealed record PresetInstanceInfo
{
    public required int DeviceNumber { get; init; }
    public required string DeviceName { get; init; }
    public string? Description { get; init; }

    // 접속(있는 카테고리만 쓴다)
    public string? IpAddress { get; init; }
    public int? IpPort { get; init; }
    public string? UserName { get; init; }
    public string? UserPassword { get; init; }

    /// <summary>센서의 소속 제어기(센서 등록에 필수).</summary>
    public IControllerDeviceModel? Controller { get; init; }
}
