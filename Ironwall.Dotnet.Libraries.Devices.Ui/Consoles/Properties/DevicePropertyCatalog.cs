using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Properties;
/****************************************************************************
   Purpose      : 7 카테고리 전 속성의 정규화된 카탈로그 — 상세 폼은 이 목록만 읽고 그린다 (FR-07~FR-09)
   Created By   : GHLee
   Created On   : 9/19/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 정규화된 장비 속성 카탈로그. 값 자체가 <b>단일 정본</b>이다 — 상세 폼·검증·저장이 전부 이 목록을 참조하고,
/// 새 속성이 생기면 여기 한 줄만 늘린다(스토리보드 <c>window-layout-system-storyboard.html</c> L1953-2098 이 정본 순서).
/// </summary>
/// <remarks>
/// <para><b>못 쓰는 속성도 싣는다</b> — <c>category_device</c>·<c>device_status</c>·<c>components[]</c> 처럼
/// 서버가 거부하는 칸도 <c>Writable=No</c> + 사유로 목록에 있다. 안 그러면 "이 속성이 왜 안 보이지?" 를
/// 매번 코드에서 다시 뒤져야 한다.</para>
/// <para><b>종류축이 명세 두 줄인 이유</b> — <c>type_&lt;category&gt;</c> 는 제어기·센서·카메라만 생성 시 필수다
/// (형상 4축은 비워도 서버가 <c>Unknown</c> 을 배정). <see cref="DevicePropertySpec.IsRequiredOnCreate"/> 가
/// 카테고리마다 달라 한 줄로 못 담고 <see cref="DevicePropertySpec.Categories"/> 가 겹치지 않는 두 줄로 쪼갰다 —
/// 겹치지 않으므로 같은 <see cref="DevicePropertySpec.Key"/>("type_axis")를 써도 (카테고리, 계약) 유일성이 깨지지 않는다.</para>
/// </remarks>
public static class DevicePropertyCatalog
{
    // ⚠ 카테고리 조합 상수는 반드시 All 보다 먼저 선언한다 — C# 정적 필드 이니셜라이저는 선언 순서대로
    // 실행되므로, Build()(=All 의 이니셜라이저)가 먼저 오면 아래 상수들이 전부 null 인 채로 Build() 가
    // 실행돼 모든 명세의 Categories 가 null 이 된다(실측: 전 테스트가 NullReferenceException 으로 실패).
    private static readonly IReadOnlyCollection<EnumDeviceCategory> All7 = new[]
    {
        EnumDeviceCategory.Controller, EnumDeviceCategory.Sensor, EnumDeviceCategory.Camera,
        EnumDeviceCategory.Speaker, EnumDeviceCategory.Enclosure, EnumDeviceCategory.Lamp, EnumDeviceCategory.Gate,
    };

    private static readonly IReadOnlyCollection<EnumDeviceCategory> TypeAxisRequired = new[]
    { EnumDeviceCategory.Controller, EnumDeviceCategory.Sensor, EnumDeviceCategory.Camera };

    private static readonly IReadOnlyCollection<EnumDeviceCategory> TypeAxisOptional = new[]
    { EnumDeviceCategory.Speaker, EnumDeviceCategory.Enclosure, EnumDeviceCategory.Lamp, EnumDeviceCategory.Gate };

    private static readonly IReadOnlyCollection<EnumDeviceCategory> HeadingApplicable = new[]
    { EnumDeviceCategory.Camera, EnumDeviceCategory.Speaker, EnumDeviceCategory.Sensor };

    private const string AxisReadOnlyReason = "이번 판에서는 읽기 전용 — 모델에서 서버로 보내는 경로가 아직 없다";

    #region - Public API -
    public static IReadOnlyList<DevicePropertySpec> All { get; } = Build();

    /// <summary>
    /// 카테고리 · 계약 세대로 걸러 <b>화면 순서</b>(절 순서 → 선언 순서)로 돌려준다.
    /// <see cref="OrderBy{TSource, TKey}"/> 는 안정 정렬이라 같은 절 안에서는 <see cref="All"/> 의 선언 순서가 그대로 남는다.
    /// </summary>
    public static IReadOnlyList<DevicePropertySpec> For(EnumDeviceCategory category, bool isAxisContract)
        => All.Where(s => s.Categories.Contains(category))
              .Where(s => isAxisContract ? !s.LegacyContractOnly : !s.AxisContractOnly)
              .OrderBy(s => SectionIndex(s.Section))
              .ToList();

    public static IReadOnlyList<DevicePropertySection> SectionOrder { get; } = new[]
    {
        DevicePropertySection.Common,
        DevicePropertySection.Connection,
        DevicePropertySection.HardwareSpec,
        DevicePropertySection.Components,
        DevicePropertySection.DeviceStatus,
        DevicePropertySection.Location,
        DevicePropertySection.Groups,
        DevicePropertySection.DeviceConfig,
        DevicePropertySection.Extra,
    };

    public static string SectionTitle(DevicePropertySection section) => section switch
    {
        DevicePropertySection.Common => "장비 공통",
        DevicePropertySection.Connection => "접속 축",
        DevicePropertySection.HardwareSpec => "형상 축",
        DevicePropertySection.Components => "부품",
        DevicePropertySection.DeviceStatus => "상태 축",
        DevicePropertySection.Location => "위치 정보",
        DevicePropertySection.Groups => "그룹",
        DevicePropertySection.DeviceConfig => "설정 축",
        DevicePropertySection.Extra => "부가",
        _ => section.ToString(),
    };

    public static string? SectionAxisName(DevicePropertySection section) => section switch
    {
        DevicePropertySection.Connection => "connection",
        DevicePropertySection.HardwareSpec => "hardware_spec",
        DevicePropertySection.Components => "hardware_spec.components[]",
        DevicePropertySection.DeviceStatus => "device_status",
        DevicePropertySection.Location => "geolocation",
        DevicePropertySection.Groups => "group_ids",
        DevicePropertySection.DeviceConfig => "device_config",
        _ => null,
    };
    #endregion

    #region - Build -
    private static int SectionIndex(DevicePropertySection section)
    {
        for (var i = 0; i < SectionOrder.Count; i++)
            if (SectionOrder[i] == section) return i;
        return SectionOrder.Count;
    }

    private static IReadOnlyList<DevicePropertySpec> Build()
    {
        var list = new List<DevicePropertySpec>
        {
            // ── 1. 장비 공통 ──────────────────────────────────────────────
            new()
            {
                Key = "category_device", Label = "카테고리", ApiPath = "category_device",
                Section = DevicePropertySection.Common, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No,
                LockReason = "경로가 정본이다 — 바꾸려면 지우고 다시 만든다",
                Categories = All7, ViewModelPath = "CategoryDevice",
            },
            new()
            {
                Key = "number_device", Label = "장비번호", ApiPath = "number_device",
                Section = DevicePropertySection.Common, Editor = DevicePropertyEditor.Integer,
                Categories = All7, ViewModelPath = "DeviceNumber",
                IsRequiredOnCreate = true, AllowMultiEdit = false,
            },
            new()
            {
                Key = "name_device", Label = "이름", ApiPath = "name_device",
                Section = DevicePropertySection.Common, Editor = DevicePropertyEditor.Text,
                Categories = All7, ViewModelPath = "DeviceName", IsRequiredOnCreate = true,
            },
            new()
            {
                Key = "type_axis", Label = "종류", ApiPath = "type_<category>",
                Section = DevicePropertySection.Common, Editor = DevicePropertyEditor.Choice,
                Categories = TypeAxisRequired, ViewModelPath = "TypeAxisCode",
                OptionSource = DevicePropertyOptionSource.TypeAxis, AxisContractOnly = true,
                IsRequiredOnCreate = true,
            },
            new()
            {
                Key = "type_axis", Label = "종류", ApiPath = "type_<category>",
                Section = DevicePropertySection.Common, Editor = DevicePropertyEditor.Choice,
                Categories = TypeAxisOptional, ViewModelPath = "TypeAxisCode",
                OptionSource = DevicePropertyOptionSource.TypeAxis, AxisContractOnly = true,
            },
            new()
            {
                Key = "device_type", Label = "종류(레거시)", ApiPath = "device_type",
                Section = DevicePropertySection.Common, Editor = DevicePropertyEditor.Choice,
                Categories = All7, ViewModelPath = "DeviceType",
                OptionSource = DevicePropertyOptionSource.ClrEnum, EnumType = typeof(EnumDeviceType),
                LegacyContractOnly = true,
            },
            new()
            {
                Key = "speaker_role", Label = "종류(방송)", ApiPath = "speaker_role",
                Section = DevicePropertySection.Common, Editor = DevicePropertyEditor.Choice,
                Categories = Cat(EnumDeviceCategory.Speaker), ViewModelPath = "SpeakerType",
                OptionSource = DevicePropertyOptionSource.ExtraAxis, VocabularyName = "speaker_role",
            },
            new()
            {
                Key = "description", Label = "설명", ApiPath = "description",
                Section = DevicePropertySection.Common, Editor = DevicePropertyEditor.Text,
                Categories = Cat(EnumDeviceCategory.Speaker, EnumDeviceCategory.Lamp), ViewModelPath = "Description",
            },
            new()
            {
                Key = "unit_id", Label = "부대", ApiPath = "unit_id",
                Section = DevicePropertySection.Common, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No,
                LockReason = "소속 부대는 저장할 때 이 클라이언트의 부대로 찍힌다 — 다부대 편집은 다음 판",
                Categories = All7, AxisReader = m => m.UnitId?.ToString(CultureInfo.InvariantCulture),
                AxisContractOnly = true,
            },
            new()
            {
                Key = "controller_id", Label = "소속 제어기", ApiPath = "controller_id",
                Section = DevicePropertySection.Common, Editor = DevicePropertyEditor.Choice,
                Categories = Cat(EnumDeviceCategory.Sensor), ViewModelPath = "Controller",
                OptionSource = DevicePropertyOptionSource.Controllers, IsRequiredOnCreate = true,
            },
            new()
            {
                Key = "server_id", Label = "관리 서버", ApiPath = "server_id",
                Section = DevicePropertySection.Common, Editor = DevicePropertyEditor.Choice,
                Categories = Cat(EnumDeviceCategory.Speaker), ViewModelPath = "Server",
                OptionSource = DevicePropertyOptionSource.Servers,
            },
            new()
            {
                Key = "is_enable", Label = "활성화", ApiPath = "is_enable",
                Section = DevicePropertySection.Common, Editor = DevicePropertyEditor.Boolean,
                Categories = All7, ViewModelPath = "IsEnable",
            },
            new()
            {
                Key = "status", Label = "운영 상태", ApiPath = "status",
                Section = DevicePropertySection.Common, Editor = DevicePropertyEditor.Choice,
                Categories = All7, ViewModelPath = "Status",
                OptionSource = DevicePropertyOptionSource.ClrEnum, EnumType = typeof(EnumDeviceStatus),
            },
            new()
            {
                Key = "version", Label = "버전", ApiPath = "version",
                Section = DevicePropertySection.Common, Editor = DevicePropertyEditor.Text,
                Categories = All7, ViewModelPath = "Version", LegacyContractOnly = true,
            },

            // ── 2. 접속 축 connection ─────────────────────────────────────
            new()
            {
                Key = "connection.ip_address", Label = "IP", ApiPath = "connection.ip_address",
                Section = DevicePropertySection.Connection, Editor = DevicePropertyEditor.Text,
                Categories = Cat(EnumDeviceCategory.Controller, EnumDeviceCategory.Camera, EnumDeviceCategory.Lamp),
                ViewModelPath = "IpAddress",
            },
            new()
            {
                Key = "connection.ip_port", Label = "포트", ApiPath = "connection.ip_port",
                Section = DevicePropertySection.Connection, Editor = DevicePropertyEditor.Integer,
                Categories = Cat(EnumDeviceCategory.Controller), ViewModelPath = "Port", Min = 1, Max = 65535,
            },
            new()
            {
                Key = "connection.ip_port", Label = "포트", ApiPath = "connection.ip_port",
                Section = DevicePropertySection.Connection, Editor = DevicePropertyEditor.Integer,
                Categories = Cat(EnumDeviceCategory.Camera, EnumDeviceCategory.Lamp), ViewModelPath = "IpPort",
                Min = 1, Max = 65535,
            },
            new()
            {
                Key = "connection.user_name", Label = "접속 사용자명", ApiPath = "connection.credentials.user_name",
                Section = DevicePropertySection.Connection, Editor = DevicePropertyEditor.Text,
                Categories = Cat(EnumDeviceCategory.Camera, EnumDeviceCategory.Lamp), ViewModelPath = "UserName",
            },
            new()
            {
                Key = "connection.user_password", Label = "비밀번호", ApiPath = "connection.credentials.user_password",
                Section = DevicePropertySection.Connection, Editor = DevicePropertyEditor.Password,
                Categories = Cat(EnumDeviceCategory.Camera, EnumDeviceCategory.Lamp), ViewModelPath = "UserPassword",
            },
            new()
            {
                Key = "connection.type", Label = "접속 방식", ApiPath = "connection.type",
                Section = DevicePropertySection.Connection, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No, LockReason = AxisReadOnlyReason,
                Categories = All7, AxisReader = m => m.Axes?.Connection?.Type,
                AxisSection = "connection", AxisContractOnly = true,
            },
            new()
            {
                Key = "connection.parent_device_id", Label = "상위 장비", ApiPath = "connection.parent_device_id",
                Section = DevicePropertySection.Connection, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No, LockReason = AxisReadOnlyReason,
                Categories = All7, AxisReader = m => m.Axes?.Connection?.ParentDeviceId?.ToString(CultureInfo.InvariantCulture),
                AxisSection = "connection", AxisContractOnly = true,
            },
            new()
            {
                Key = "connection.channel", Label = "채널", ApiPath = "connection.channel",
                Section = DevicePropertySection.Connection, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No, LockReason = AxisReadOnlyReason,
                Categories = All7, AxisReader = m => m.Axes?.Connection?.Channel?.ToString(CultureInfo.InvariantCulture),
                AxisSection = "connection", AxisContractOnly = true,
            },
            new()
            {
                Key = "connection.protocol", Label = "프로토콜", ApiPath = "connection.protocol",
                Section = DevicePropertySection.Connection, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No, LockReason = AxisReadOnlyReason,
                Categories = All7, AxisReader = m => m.Axes?.Connection?.Protocol,
                AxisSection = "connection", AxisContractOnly = true,
            },
            new()
            {
                Key = "connection.urls", Label = "장비 링크", ApiPath = "connection.urls",
                Section = DevicePropertySection.Connection, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No, LockReason = AxisReadOnlyReason,
                Categories = All7, AxisReader = ReadConnectionUrls,
                AxisSection = "connection", AxisContractOnly = true,
            },
            new()
            {
                Key = "mode", Label = "제어 모드", ApiPath = "mode",
                Section = DevicePropertySection.Connection, Editor = DevicePropertyEditor.Choice,
                Categories = Cat(EnumDeviceCategory.Camera), ViewModelPath = "Mode",
                OptionSource = DevicePropertyOptionSource.ClrEnum, EnumType = typeof(EnumCameraMode),
                LegacyContractOnly = true,
            },
            new()
            {
                Key = "category", Label = "카메라 유형(레거시)", ApiPath = "category",
                Section = DevicePropertySection.Connection, Editor = DevicePropertyEditor.Choice,
                Categories = Cat(EnumDeviceCategory.Camera), ViewModelPath = "Category",
                OptionSource = DevicePropertyOptionSource.ClrEnum, EnumType = typeof(EnumCameraType),
                LegacyContractOnly = true,
            },
            new()
            {
                Key = "is_record", Label = "녹화", ApiPath = "is_record",
                Section = DevicePropertySection.Connection, Editor = DevicePropertyEditor.Boolean,
                Categories = Cat(EnumDeviceCategory.Camera), ViewModelPath = "IsRecord", LegacyContractOnly = true,
            },

            // ── 3. 형상 축 hardware_spec (읽기 전용) ─────────────────────
            new()
            {
                Key = "hardware_spec.manufacturer", Label = "제조사", ApiPath = "hardware_spec.manufacturer",
                Section = DevicePropertySection.HardwareSpec, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No, LockReason = AxisReadOnlyReason,
                Categories = All7, AxisReader = m => m.Axes?.HardwareSpec?.Manufacturer,
                AxisSection = "hardware_spec", AxisContractOnly = true,
            },
            new()
            {
                Key = "hardware_spec.model", Label = "모델", ApiPath = "hardware_spec.model",
                Section = DevicePropertySection.HardwareSpec, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No, LockReason = AxisReadOnlyReason,
                Categories = All7, AxisReader = m => m.Axes?.HardwareSpec?.Model,
                AxisSection = "hardware_spec", AxisContractOnly = true,
            },
            new()
            {
                Key = "hardware_spec.serial", Label = "일련번호", ApiPath = "hardware_spec.serial",
                Section = DevicePropertySection.HardwareSpec, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No, LockReason = AxisReadOnlyReason,
                Categories = All7, AxisReader = m => m.Axes?.HardwareSpec?.Serial,
                AxisSection = "hardware_spec", AxisContractOnly = true,
            },
            new()
            {
                Key = "hardware_spec.firmware", Label = "펌웨어", ApiPath = "hardware_spec.firmware",
                Section = DevicePropertySection.HardwareSpec, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No, LockReason = AxisReadOnlyReason,
                Categories = All7, AxisReader = m => m.Axes?.HardwareSpec?.Firmware,
                AxisSection = "hardware_spec", AxisContractOnly = true,
            },
            new()
            {
                Key = "hardware_spec.hardware_rev", Label = "HW 리비전", ApiPath = "hardware_spec.hardware_rev",
                Section = DevicePropertySection.HardwareSpec, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No, LockReason = AxisReadOnlyReason,
                Categories = All7, AxisReader = m => m.Axes?.HardwareSpec?.HardwareRev,
                AxisSection = "hardware_spec", AxisContractOnly = true,
            },
            new()
            {
                Key = "hardware_spec.mac_address", Label = "MAC", ApiPath = "hardware_spec.mac_address",
                Section = DevicePropertySection.HardwareSpec, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No, LockReason = AxisReadOnlyReason,
                Categories = All7, AxisReader = m => m.Axes?.HardwareSpec?.MacAddress,
                AxisSection = "hardware_spec", AxisContractOnly = true,
            },
            new()
            {
                Key = "hardware_spec.max_detection_range", Label = "탐지거리(m)", ApiPath = "hardware_spec.max_detection_range",
                Section = DevicePropertySection.HardwareSpec, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No, LockReason = AxisReadOnlyReason,
                Categories = Cat(EnumDeviceCategory.Camera, EnumDeviceCategory.Sensor),
                AxisReader = m => m.Axes?.HardwareSpec?.MaxDetectionRange?.ToString(CultureInfo.InvariantCulture),
                AxisSection = "hardware_spec", AxisContractOnly = true,
            },
            new()
            {
                Key = "hardware_spec.onvif_version", Label = "ONVIF 버전", ApiPath = "hardware_spec.onvif_version",
                Section = DevicePropertySection.HardwareSpec, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No, LockReason = AxisReadOnlyReason,
                Categories = Cat(EnumDeviceCategory.Camera), AxisReader = m => m.Axes?.HardwareSpec?.OnvifVersion,
                AxisSection = "hardware_spec", AxisContractOnly = true,
            },

            // ── 4. 부품 components[] (읽기 전용 — 조립기 전용) ───────────
            new()
            {
                Key = "components", Label = "부품", ApiPath = "hardware_spec.components",
                Section = DevicePropertySection.Components, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No,
                LockReason = "부품은 조립기에서만 고친다 — 서버가 배열을 통째로 바꿔 일부만 보내면 나머지가 지워진다",
                Categories = All7, AxisReader = ReadComponentsSummary,
                AxisSection = "components", AxisContractOnly = true,
            },

            // ── 5. 상태 축 device_status (관측 · 읽기 전용) ──────────────
            new()
            {
                Key = "device_status", Label = "부품 상태", ApiPath = "device_status",
                Section = DevicePropertySection.DeviceStatus, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No,
                LockReason = "관측값이다 — 요청에 실으면 서버가 거부한다(422 OBSERVED_FIELD)",
                Categories = All7, AxisReader = ReadDeviceStatusSummary,
                AxisSection = "device_status", AxisContractOnly = true,
            },
            new()
            {
                Key = "device_status.door", Label = "문 위치", ApiPath = "device_status.components.door.state",
                Section = DevicePropertySection.DeviceStatus, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No,
                LockReason = "관측값이다 — 개폐 명령은 지도에서 보낸다",
                Categories = Cat(EnumDeviceCategory.Enclosure), ViewModelPath = "DoorStatusDisplay",
                AxisSection = "device_status",
            },
            new()
            {
                Key = "device_status.door", Label = "문 위치", ApiPath = "device_status.components.door.state",
                Section = DevicePropertySection.DeviceStatus, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No,
                LockReason = "관측값이다 — 개폐 명령은 지도에서 보낸다",
                Categories = Cat(EnumDeviceCategory.Gate), ViewModelPath = "DoorPosition",
                AxisSection = "device_status",
            },

            // ── 6. 위치 정보 geolocation ──────────────────────────────────
            new()
            {
                Key = "location", Label = "위치", ApiPath = "location",
                Section = DevicePropertySection.Location, Editor = DevicePropertyEditor.Text,
                Categories = All7, ViewModelPath = "Location",
            },
            new()
            {
                Key = "latitude", Label = "위도", ApiPath = "latitude",
                Section = DevicePropertySection.Location, Editor = DevicePropertyEditor.Number,
                Categories = All7, ViewModelPath = "Latitude", Min = -90, Max = 90,
            },
            new()
            {
                Key = "longitude", Label = "경도", ApiPath = "longitude",
                Section = DevicePropertySection.Location, Editor = DevicePropertyEditor.Number,
                Categories = All7, ViewModelPath = "Longitude", Min = -180, Max = 180,
            },
            new()
            {
                Key = "altitude", Label = "고도(m)", ApiPath = "altitude",
                Section = DevicePropertySection.Location, Editor = DevicePropertyEditor.Number,
                Categories = All7, ViewModelPath = "Altitude",
            },
            new()
            {
                Key = "heading", Label = "방위각(°)", ApiPath = "heading",
                Section = DevicePropertySection.Location, Editor = DevicePropertyEditor.Number,
                Categories = HeadingApplicable, ViewModelPath = "Bearing", Min = 0, Max = 360,
            },

            // ── 7. 그룹 group_ids ─────────────────────────────────────────
            new()
            {
                Key = "group_ids", Label = "그룹", ApiPath = "group_ids",
                Section = DevicePropertySection.Groups, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No,
                LockReason = "그룹은 아래 칩에 끌어 놓거나 그룹 패널에서 바꾼다",
                Categories = All7, ViewModelPath = "DeviceGroupsText",
            },

            // ── 8. 설정 축 device_config ──────────────────────────────────
            new()
            {
                Key = "device_config.thresholds", Label = "임계치", ApiPath = "device_config.thresholds",
                Section = DevicePropertySection.DeviceConfig, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No,
                LockReason = "설정 축 편집은 다음 판 — 지금은 카메라 설정·함체 임계값 창에서 고친다",
                Categories = All7, AxisReader = m => FlattenKeys(m.Axes?.DeviceConfig?.Thresholds),
                AxisSection = "device_config", AxisContractOnly = true,
            },
            new()
            {
                Key = "device_config.modes", Label = "동작 모드", ApiPath = "device_config.modes",
                Section = DevicePropertySection.DeviceConfig, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No,
                LockReason = "설정 축 편집은 다음 판 — 지금은 카메라 설정·함체 임계값 창에서 고친다",
                Categories = Cat(EnumDeviceCategory.Camera), AxisReader = m => FlattenKeys(m.Axes?.DeviceConfig?.Modes),
                AxisSection = "device_config", AxisContractOnly = true,
            },
            new()
            {
                Key = "device_config.component_overrides", Label = "부품별 설정", ApiPath = "device_config.component_overrides",
                Section = DevicePropertySection.DeviceConfig, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No,
                LockReason = "설정 축 편집은 다음 판 — 지금은 카메라 설정·함체 임계값 창에서 고친다",
                Categories = All7, AxisReader = m => FlattenKeys(m.Axes?.DeviceConfig?.ComponentOverrides),
                AxisSection = "device_config", AxisContractOnly = true,
            },
            new()
            {
                Key = "heater_enabled", Label = "히터", ApiPath = "heater_enabled",
                Section = DevicePropertySection.DeviceConfig, Editor = DevicePropertyEditor.Boolean,
                Categories = Cat(EnumDeviceCategory.Enclosure), ViewModelPath = "HeaterEnabled",
            },
            new()
            {
                Key = "fan_enabled", Label = "팬", ApiPath = "fan_enabled",
                Section = DevicePropertySection.DeviceConfig, Editor = DevicePropertyEditor.Boolean,
                Categories = Cat(EnumDeviceCategory.Enclosure), ViewModelPath = "FanEnabled",
            },

            // ── 9. 부가 ────────────────────────────────────────────────────
            new()
            {
                Key = "meta.view", Label = "응답 프로필", ApiPath = "meta.view",
                Section = DevicePropertySection.Extra, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No,
                LockReason = "이 값은 서버 응답 메타데이터다 — 편집 대상이 아니다",
                Categories = All7, AxisReader = m => m.Axes?.Meta?.View, AxisContractOnly = true,
            },
            new()
            {
                Key = "meta.sections", Label = "실린 절", ApiPath = "meta.sections",
                Section = DevicePropertySection.Extra, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No,
                LockReason = "이 값은 서버 응답 메타데이터다 — 편집 대상이 아니다",
                Categories = All7, AxisReader = ReadMetaSections, AxisContractOnly = true,
            },
        };

        return list;
    }

    private static IReadOnlyCollection<EnumDeviceCategory> Cat(params EnumDeviceCategory[] categories) => categories;

    private static string? ReadConnectionUrls(IBaseDeviceModel model)
    {
        var urls = model.Axes?.Connection?.Urls;
        if (urls == null) return null;
        var present = urls.Where(kv => !string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key).ToList();
        return present.Count == 0 ? "—" : string.Join(", ", present);
    }

    private static string? ReadComponentsSummary(IBaseDeviceModel model)
    {
        var list = model.Axes?.HardwareSpec?.Components;
        if (list == null) return null;
        return list.Count == 0 ? "형상 미입력" : $"{list.Count}개";
    }

    private static string? ReadDeviceStatusSummary(IBaseDeviceModel model)
    {
        var components = model.Axes?.DeviceStatus?.Components;
        if (components == null) return null;
        return components.Count == 0 ? "관측 없음" : $"{components.Count}건 관측";
    }

    private static string? ReadMetaSections(IBaseDeviceModel model)
    {
        var sections = model.Axes?.Meta?.Sections;
        if (sections == null) return null;
        return sections.Count == 0 ? "—" : string.Join(", ", sections);
    }

    // 임계치·모드·부품 덮어쓰기는 카탈로그가 정한 자유 키다 — 값까지 펴지 않고 "어떤 키가 실렸는가"만 요약한다.
    private static string? FlattenKeys(JObject? token)
    {
        if (token == null) return null;
        return token.Count == 0 ? "—" : string.Join(", ", token.Properties().Select(p => p.Name));
    }
    #endregion
}
