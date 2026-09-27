using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
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

    // 표시 전용 칸의 까닭 — 목록에는 남기되(명세 규칙: Writable=No 는 까닭을 동반) 화면에는 띄우지 않는다
    // (ShowLockReason=false). 운영자에게 "아직 안 된다"는 개발 메모를 보이지 않는다(2026-09-27 완성도 수정).
    private const string DisplayOnlyReason = "표시 전용 값";

    // 임계값 칸 자리표시 — Build() 가 이 자리에 계약별 명세 두 벌을 끼운다(목록 초기화식은 IEnumerable 을 못 받는다).
    private static readonly DevicePropertySpec ThresholdAnchor = new()
    {
        Key = "#threshold-anchor", Label = "#", ApiPath = "#", Section = DevicePropertySection.DeviceConfig,
        Editor = DevicePropertyEditor.ReadOnly, Categories = Array.Empty<EnumDeviceCategory>(),
    };

    #region - Public API -
    public static IReadOnlyList<DevicePropertySpec> All { get; } = Build();

    /// <summary>
    /// 카테고리 · 계약 세대로 걸러 <b>화면 순서</b>(절 순서 → 선언 순서)로 돌려준다.
    /// <see cref="OrderBy{TSource, TKey}"/> 는 안정 정렬이라 같은 절 안에서는 <see cref="All"/> 의 선언 순서가 그대로 남는다.
    /// </summary>
    /// <param name="isAxisContract">v7.0+ 축 계약이면 참.</param>
    /// <param name="isUnitEra">
    /// v8.0+ 부대 편제 계약이면 참(D-14). 거짓이면 <see cref="UNIT_FIELD_KEY"/> 칸을 걸러낸다 — <c>unit_id</c> 는
    /// <see cref="DevicePropertySpec.AxisContractOnly"/>(v7.0 경계)가 아니라 v8.0 경계가 필요해 키로 따로 거른다.
    /// </param>
    public static IReadOnlyList<DevicePropertySpec> For(EnumDeviceCategory category, bool isAxisContract, bool isUnitEra = false)
        => All.Where(s => s.Categories.Contains(category))
              .Where(s => isAxisContract ? !s.LegacyContractOnly : !s.AxisContractOnly)
              .Where(s => isUnitEra || s.Key != UNIT_FIELD_KEY)
              .OrderBy(s => SectionIndex(s.Section))
              .ToList();

    /// <summary>"부대"(<c>unit_id</c>) 칸의 안정 키 — v8.0 미만에서 <see cref="For"/> 가 이 키로 걸러낸다.</summary>
    private const string UNIT_FIELD_KEY = "unit_id";

    public static IReadOnlyList<DevicePropertySection> SectionOrder { get; } = new[]
    {
        DevicePropertySection.GroupInfo,
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
        DevicePropertySection.GroupInfo => "그룹 정보",
        DevicePropertySection.Common => "장비 공통",
        DevicePropertySection.Connection => "접속 정보",
        DevicePropertySection.HardwareSpec => "하드웨어 정보",
        DevicePropertySection.Components => "부품",
        DevicePropertySection.DeviceStatus => "부품 상태",
        DevicePropertySection.Location => "위치 정보",
        DevicePropertySection.Groups => "그룹",
        DevicePropertySection.DeviceConfig => "운용 설정",
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
                LockReason = "카테고리는 바꿀 수 없습니다. 다른 카테고리로 옮기려면 삭제한 뒤 다시 등록하세요.",
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
            // 종류축은 카테고리마다 필드 이름이 다르다(type_sensor · type_camera …) — 캡션이 "type_<category>" 로 새지 않게
            // 카테고리마다 한 줄씩 만든다(TypeAxisSpecs, 이 자리 = "device_type" 바로 앞). 생성 필수 여부도 카테고리마다 다르다.
            new()
            {
                Key = "device_type", Label = "종류", ApiPath = "device_type",
                Section = DevicePropertySection.Common, Editor = DevicePropertyEditor.Choice,
                Categories = All7, ViewModelPath = "DeviceType",
                OptionSource = DevicePropertyOptionSource.ClrEnum, EnumType = typeof(EnumDeviceType),
                LegacyContractOnly = true,
            },
            new()
            {
                Key = "speaker_role", Label = "스피커 역할", ApiPath = "speaker_role",
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
                Key = UNIT_FIELD_KEY, Label = "소속 부대", ApiPath = "unit_id",
                Section = DevicePropertySection.Common, Editor = DevicePropertyEditor.Choice,
                OptionSource = DevicePropertyOptionSource.Units,
                // 값은 부대 id(저장 값), 화면은 부대 이름(선택지 · UnitNameDirectory). 이름을 모르는 id 는 id 그대로 보인다(지어내지 않는다).
                Categories = All7, AxisReader = m => m.UnitId?.ToString(CultureInfo.InvariantCulture),
                AxisWritePath = "unit_id", AxisValueKind = DeviceAxisValueKind.Integer, AxisAllowsClear = false,
                EmptyDisplay = UnitNameDirectory.Unassigned,
                // ⚠ v7.0 경계용 AxisContractOnly 가 아니라 v8.0 경계가 필요해 For() 가 UNIT_FIELD_KEY 로 따로 거른다.
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
                // 패널의 등록은 포트가 1 이상이어야 보낸다(ControllerDevicePanelViewModel 등록 조건) — 폼이 필수로 말하지 않으면
                // [등록] 뒤 "1건은 필수값(제어기/IP 등) 미충족으로 보류했습니다" 한 줄만 뜨고 어느 칸인지 모른다(GIS 실창 WP-2 SC-DEV-012).
                IsRequiredOnCreate = true,
            },
            new()
            {
                Key = "connection.ip_port", Label = "포트", ApiPath = "connection.ip_port",
                Section = DevicePropertySection.Connection, Editor = DevicePropertyEditor.Integer,
                Categories = Cat(EnumDeviceCategory.Camera, EnumDeviceCategory.Lamp), ViewModelPath = "IpPort",
                Min = 1, Max = 65535,
                IsRequiredOnCreate = true,   // 카메라 · 경광등 패널도 포트 1 이상일 때만 등록한다(위 제어기와 같은 까닭 — SC-DEV-014)
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
            // ── 접속 축 편집(7.0+) — 행 뷰모델이 아니라 축 값 부분 수정(PATCH, 보낸 키만 바뀐다)으로 보낸다 ──
            new()
            {
                Key = "connection.type", Label = "접속 방식", ApiPath = "connection.type",
                Section = DevicePropertySection.Connection, Editor = DevicePropertyEditor.Choice,
                OptionSource = DevicePropertyOptionSource.Fixed, FixedOptions = DeviceEnumDisplay.ConnectionTypes,
                Categories = All7, AxisReader = m => m.Axes?.Connection?.Type,
                AxisWritePath = "connection.type", AxisAllowsClear = false,
                AxisSection = "connection", AxisContractOnly = true,
            },
            new()
            {
                // 센서의 상위는 소속 제어기 하나뿐이다 — 센서에 parent_device_id 를 보내면 서버가 거부한다(D13).
                Key = "connection.parent_device_id", Label = "상위 장비 번호", ApiPath = "connection.parent_device_id",
                Section = DevicePropertySection.Connection, Editor = DevicePropertyEditor.Integer,
                Categories = Cat(EnumDeviceCategory.Controller, EnumDeviceCategory.Camera, EnumDeviceCategory.Speaker,
                                 EnumDeviceCategory.Enclosure, EnumDeviceCategory.Lamp, EnumDeviceCategory.Gate),
                AxisReader = m => m.Axes?.Connection?.ParentDeviceId?.ToString(CultureInfo.InvariantCulture),
                AxisWritePath = "connection.parent_device_id", AxisValueKind = DeviceAxisValueKind.Integer, Min = 1,
                AxisSection = "connection", AxisContractOnly = true,
            },
            new()
            {
                Key = "connection.channel", Label = "채널", ApiPath = "connection.channel",
                Section = DevicePropertySection.Connection, Editor = DevicePropertyEditor.Integer,
                Categories = All7, AxisReader = m => m.Axes?.Connection?.Channel?.ToString(CultureInfo.InvariantCulture),
                AxisWritePath = "connection.channel", AxisValueKind = DeviceAxisValueKind.Integer, Min = 0,
                AllowMultiEdit = false,
                AxisSection = "connection", AxisContractOnly = true,
            },
            new()
            {
                // 카메라는 제어 프로토콜이 필수 어휘(없음 · ONVIF · 엠스톤 · 이노뎁 · 기타)라 지울 수 없다.
                Key = "connection.protocol", Label = "제어 프로토콜", ApiPath = "connection.protocol",
                Section = DevicePropertySection.Connection, Editor = DevicePropertyEditor.Choice,
                OptionSource = DevicePropertyOptionSource.Fixed, FixedOptions = DeviceEnumDisplay.CameraProtocols,
                Categories = Cat(EnumDeviceCategory.Camera), AxisReader = m => m.Axes?.Connection?.Protocol,
                AxisWritePath = "connection.protocol", AxisAllowsClear = false,
                AxisSection = "connection", AxisContractOnly = true,
            },
            new()
            {
                Key = "connection.protocol", Label = "프로토콜", ApiPath = "connection.protocol",
                Section = DevicePropertySection.Connection, Editor = DevicePropertyEditor.Text, MaxLength = 50,
                Categories = Cat(EnumDeviceCategory.Controller, EnumDeviceCategory.Sensor, EnumDeviceCategory.Speaker,
                                 EnumDeviceCategory.Enclosure, EnumDeviceCategory.Lamp, EnumDeviceCategory.Gate),
                AxisReader = m => m.Axes?.Connection?.Protocol,
                AxisWritePath = "connection.protocol",
                AxisSection = "connection", AxisContractOnly = true,
            },
            new()
            {
                Key = "connection.urls", Label = "장비 링크", ApiPath = "connection.urls",
                Section = DevicePropertySection.Connection, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No, LockReason = DisplayOnlyReason, ShowLockReason = false,
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

            // ── 3. 형상 축 hardware_spec — 스칼라만 편집(PATCH 는 축 객체 병합이라 components[] 는 그대로 남는다) ──
            HardwareText("hardware_spec.manufacturer", "제조사", All7, m => m.Axes?.HardwareSpec?.Manufacturer, maxLength: 200),
            HardwareText("hardware_spec.model", "모델", All7, m => m.Axes?.HardwareSpec?.Model, maxLength: 200),
            HardwareText("hardware_spec.serial", "일련번호", All7, m => m.Axes?.HardwareSpec?.Serial, maxLength: 200, multiEdit: false),
            HardwareText("hardware_spec.firmware", "펌웨어", All7, m => m.Axes?.HardwareSpec?.Firmware, maxLength: 50),
            HardwareText("hardware_spec.hardware_rev", "HW 리비전", All7, m => m.Axes?.HardwareSpec?.HardwareRev, maxLength: 200),
            HardwareText("hardware_spec.mac_address", "MAC", All7, m => m.Axes?.HardwareSpec?.MacAddress, maxLength: 17, multiEdit: false),
            new()
            {
                Key = "hardware_spec.max_detection_range", Label = "탐지거리(m)", ApiPath = "hardware_spec.max_detection_range",
                Section = DevicePropertySection.HardwareSpec, Editor = DevicePropertyEditor.Number, Min = 0,
                Categories = Cat(EnumDeviceCategory.Camera, EnumDeviceCategory.Sensor),
                AxisReader = m => m.Axes?.HardwareSpec?.MaxDetectionRange?.ToString(CultureInfo.InvariantCulture),
                AxisWritePath = "hardware_spec.max_detection_range", AxisValueKind = DeviceAxisValueKind.Number,
                AxisSection = "hardware_spec", AxisContractOnly = true,
            },
            HardwareText("hardware_spec.onvif_version", "ONVIF 버전", Cat(EnumDeviceCategory.Camera), m => m.Axes?.HardwareSpec?.OnvifVersion, maxLength: 50),

            // ── 4. 부품 components[] (읽기 전용 — 조립기 전용) ───────────
            new()
            {
                Key = "components", Label = "부품", ApiPath = "hardware_spec.components",
                Section = DevicePropertySection.Components, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No,
                LockReason = "부품 구성은 [부품 구성 바꾸기]에서 바꿉니다.",
                Categories = All7, AxisReader = ReadComponentsSummary,
                AxisSection = "components", AxisContractOnly = true,
            },

            // ── 5. 상태 축 device_status (관측 · 읽기 전용) ──────────────
            new()
            {
                Key = "device_status", Label = "부품 상태", ApiPath = "device_status",
                Section = DevicePropertySection.DeviceStatus, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No,
                LockReason = "장비가 보고한 값입니다.",
                Categories = All7, AxisReader = ReadDeviceStatusSummary,
                AxisSection = "device_status", AxisContractOnly = true,
            },
            new()
            {
                Key = "device_status.door", Label = "문 위치", ApiPath = "device_status.components.door.state",
                Section = DevicePropertySection.DeviceStatus, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No,
                LockReason = "장비가 보고한 값입니다. 문 열기 · 닫기는 지도에서 합니다.",
                Categories = Cat(EnumDeviceCategory.Enclosure), ViewModelPath = "DoorStatusDisplay",
                AxisSection = "device_status",
            },
            new()
            {
                Key = "device_status.door", Label = "문 위치", ApiPath = "device_status.components.door.state",
                Section = DevicePropertySection.DeviceStatus, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No,
                LockReason = "장비가 보고한 값입니다. 문 열기 · 닫기는 지도에서 합니다.",
                Categories = Cat(EnumDeviceCategory.Gate), ViewModelPath = "DoorPositionDisplay",
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
                LockReason = "목록 아래 그룹 칩에 장비를 끌어 놓아 바꿉니다.",
                Categories = All7, ViewModelPath = "DeviceGroupsText",
            },

            // ── 8. 운용 설정 device_config — 함체 임계값 · 카메라 모드는 칸으로 고친다(7.0+ 는 축 값 부분 수정, 빈 칸 = 삭제) ──
            // (임계값 칸은 한 줄이 계약별 두 명세라 Build() 끝에서 이 자리에 끼운다.)
            ThresholdAnchor,
            CameraMode("weather_mode", "기상 모드", DeviceEnumDisplay.WeatherModes),
            CameraMode("camera_mode", "영상 모드", DeviceEnumDisplay.CameraVideoModes),
            CameraMode("day_night_mode", "주야 모드", DeviceEnumDisplay.DayNightModes),
            CameraMode("focus_mode", "초점", DeviceEnumDisplay.AutoManualModes),
            CameraMode("iris_mode", "조리개", DeviceEnumDisplay.AutoManualModes),
            CameraMode("palette", "열상 색상", DeviceEnumDisplay.Palettes),
            new()
            {
                Key = "device_config.modes.is_record", Label = "녹화", ApiPath = "device_config.modes.is_record",
                Section = DevicePropertySection.DeviceConfig, Editor = DevicePropertyEditor.Boolean,
                Categories = Cat(EnumDeviceCategory.Camera), AxisReader = m => ReadMode(m, "is_record"),
                AxisWritePath = "device_config.modes.is_record", AxisValueKind = DeviceAxisValueKind.Boolean,
                AxisSection = "device_config", AxisContractOnly = true,
            },
            new()
            {
                Key = "device_config.component_overrides", Label = "부품별 설정", ApiPath = "device_config.component_overrides",
                Section = DevicePropertySection.DeviceConfig, Editor = DevicePropertyEditor.ReadOnly,
                Writable = DevicePropertyWritable.No, LockReason = DisplayOnlyReason, ShowLockReason = false,
                Categories = All7, AxisReader = ReadComponentOverrides,
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

        };

        list.InsertRange(list.FindIndex(x => x.Key == "device_type"), TypeAxisSpecs());
        var anchor = list.IndexOf(ThresholdAnchor);
        list.RemoveAt(anchor);
        list.InsertRange(anchor, new[]
        {
            Threshold(DeviceThresholdAxis.Temperature, "high", "온도 상한(°C)", "ThresholdTempHigh"),
            Threshold(DeviceThresholdAxis.Temperature, "low", "온도 하한(°C)", "ThresholdTempLow"),
            Threshold(DeviceThresholdAxis.Humidity, "high", "습도 상한(%)", "ThresholdHumidityHigh"),
            Threshold(DeviceThresholdAxis.Current, "high", "전류 상한(A)", "ThresholdCurrentHigh"),
            Threshold(DeviceThresholdAxis.Voltage, "low", "전압 하한(V)", "ThresholdVoltageLow"),
            Threshold(DeviceThresholdAxis.Vibration, "high", "진동 상한", "ThresholdVibrationHigh"),
            Threshold(DeviceThresholdAxis.UpsBatteryLevel, "low", "UPS 배터리 하한(%)", null),
        }.SelectMany(x => x));
        return list;
    }

    /// <summary>종류축 — 카테고리마다 한 줄(필드 이름 · 생성 필수 여부가 카테고리마다 다르다).</summary>
    private static IEnumerable<DevicePropertySpec> TypeAxisSpecs()
        => All7.Select(category => new DevicePropertySpec
        {
            Key = "type_axis", Label = "종류", ApiPath = "type_" + category.ToString().ToLowerInvariant(),
            Section = DevicePropertySection.Common, Editor = DevicePropertyEditor.Choice,
            Categories = Cat(category), ViewModelPath = "TypeAxisCode",
            OptionSource = DevicePropertyOptionSource.TypeAxis, AxisContractOnly = true,
            IsRequiredOnCreate = TypeAxisRequired.Contains(category),
        });

    /// <summary>형상 축 스칼라 한 칸 — 글자 편집, 빈 칸은 서버에서 지운다(JSON null).</summary>
    private static DevicePropertySpec HardwareText(string path, string label, IReadOnlyCollection<EnumDeviceCategory> categories,
        Func<IBaseDeviceModel, string?> reader, int maxLength, bool multiEdit = true) => new()
    {
        Key = path, Label = label, ApiPath = path,
        Section = DevicePropertySection.HardwareSpec, Editor = DevicePropertyEditor.Text, MaxLength = maxLength,
        Categories = categories, AxisReader = reader, AxisWritePath = path, AllowMultiEdit = multiEdit,
        AxisSection = "hardware_spec", AxisContractOnly = true,
    };

    /// <summary>
    /// 함체 임계값 한 칸. 7.0+ 는 <c>device_config.thresholds.{metric}.{bound}</c> 로 축 값 부분 수정(빈 칸 = 삭제),
    /// 6.3 은 행 뷰모델의 평면 칸(<paramref name="legacyPath"/>)을 패널 저장이 보낸다. <paramref name="legacyPath"/> 가 없으면 7.0+ 에만 있다.
    /// </summary>
    private static IEnumerable<DevicePropertySpec> Threshold(string metric, string bound, string label, string? legacyPath)
    {
        var path = $"device_config.thresholds.{metric}.{bound}";
        yield return new DevicePropertySpec
        {
            Key = path, Label = label, ApiPath = path,
            Section = DevicePropertySection.DeviceConfig, Editor = DevicePropertyEditor.Number,
            Categories = Cat(EnumDeviceCategory.Enclosure), AxisReader = m => ReadThreshold(m, metric, bound),
            AxisWritePath = path, AxisValueKind = DeviceAxisValueKind.Number,
            AxisSection = "device_config", AxisContractOnly = true,
        };
        if (legacyPath is null) yield break;
        yield return new DevicePropertySpec
        {
            Key = path, Label = label, ApiPath = "threshold_config",
            Section = DevicePropertySection.DeviceConfig,
            Editor = legacyPath == "ThresholdVibrationHigh" ? DevicePropertyEditor.Integer : DevicePropertyEditor.Number,
            Categories = Cat(EnumDeviceCategory.Enclosure), ViewModelPath = legacyPath, LegacyContractOnly = true,
        };
    }

    /// <summary>카메라 동작 모드 한 칸 — 고정 어휘 콤보. "지정 안 함" = 키 삭제.</summary>
    private static DevicePropertySpec CameraMode(string key, string label, IReadOnlyList<(string Code, string Display)> options) => new()
    {
        Key = "device_config.modes." + key, Label = label, ApiPath = "device_config.modes." + key,
        Section = DevicePropertySection.DeviceConfig, Editor = DevicePropertyEditor.Choice,
        OptionSource = DevicePropertyOptionSource.Fixed, FixedOptions = options,
        Categories = Cat(EnumDeviceCategory.Camera), AxisReader = m => ReadMode(m, key),
        AxisWritePath = "device_config.modes." + key,
        AxisSection = "device_config", AxisContractOnly = true,
    };

    private static IReadOnlyCollection<EnumDeviceCategory> Cat(params EnumDeviceCategory[] categories) => categories;

    // ── 표시 전용 요약 — 운영자가 읽는 한국어 문장으로(영문 키 목록을 그대로 늘어놓지 않는다) ──

    private static readonly (string Key, string Label)[] UrlLabels =
    {
        ("homepage", "장비 홈"), ("management", "관리 화면"), ("image", "정지 영상"),
        ("onvif", "ONVIF"), ("streams", "영상 스트림"), ("snapshot", "채널 스냅샷"),
    };

    private static string? ReadConnectionUrls(IBaseDeviceModel model)
    {
        var urls = model.Axes?.Connection?.Urls;
        if (urls == null) return null;
        var present = UrlLabels.Where(u => urls.TryGetValue(u.Key, out var value) && !string.IsNullOrWhiteSpace(value))
                               .Select(u => u.Label).ToList();
        var extra = urls.Count(kv => !string.IsNullOrWhiteSpace(kv.Value) && UrlLabels.All(u => u.Key != kv.Key));
        if (extra > 0) present.Add($"기타 {extra}개");
        return present.Count == 0 ? "등록된 링크가 없습니다" : string.Join(" · ", present);
    }

    /// <summary>부품 목록 — 한 줄에 하나(이름 · 유형 · 채널 · 위치). 이름은 부품의 label, 없으면 key.</summary>
    private static string? ReadComponentsSummary(IBaseDeviceModel model)
    {
        var list = model.Axes?.HardwareSpec?.Components;
        if (list == null) return null;
        if (list.Count == 0) return "등록된 부품이 없습니다 — [부품 구성 바꾸기]에서 추가하세요";
        return string.Join(Environment.NewLine, list.Where(c => c != null).Select(c =>
        {
            var parts = new List<string> { string.IsNullOrWhiteSpace(c.Label) ? c.Key : c.Label!, ComponentTypeLabel(c.Type) };
            if (c.Channel is { } channel) parts.Add($"채널 {channel.ToString(CultureInfo.InvariantCulture)}");
            if (!string.IsNullOrWhiteSpace(c.Position)) parts.Add(c.Position!);
            if (c.InService == false) parts.Add("사용 안 함");
            return string.Join(" · ", parts);
        }));
    }

    /// <summary>부품 상태 — 한 줄에 하나(부품 · 동작 상태 · 건강 · 고장 사유 · 관측 시각).</summary>
    private static string? ReadDeviceStatusSummary(IBaseDeviceModel model)
    {
        var components = model.Axes?.DeviceStatus?.Components;
        if (components == null) return null;
        if (components.Count == 0) return "아직 보고된 부품 상태가 없습니다";
        return string.Join(Environment.NewLine, components.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv =>
        {
            var status = kv.Value;
            var parts = new List<string> { kv.Key };
            if (!string.IsNullOrWhiteSpace(status?.State)) parts.Add(DeviceEnumDisplay.DoorStateKorean(status!.State));
            parts.Add(DeviceEnumDisplay.ComponentHealthKorean(status?.Health));
            if (!string.IsNullOrWhiteSpace(status?.FaultReason)) parts.Add(status!.FaultReason!);
            if (TryShortTime(status?.ObservedAt, out var when)) parts.Add(when);
            return string.Join(" · ", parts);
        }));
    }

    /// <summary>부품별 설정(component_overrides) — "히터 켜기 · 경광등 색 Red" 처럼 한국어 한 줄씩.</summary>
    private static string? ReadComponentOverrides(IBaseDeviceModel model)
    {
        var config = model.Axes?.DeviceConfig;
        if (config == null) return null;
        var overrides = config.ComponentOverrides;
        if (overrides == null || overrides.Count == 0) return "따로 정한 부품 설정이 없습니다";
        return string.Join(Environment.NewLine, overrides.Properties().Select(p =>
        {
            var entry = p.Value as JObject;
            var enabled = entry?["enabled"]?.Type == JTokenType.Boolean ? (bool?)entry["enabled"] : null;
            var color = entry?["color"]?.ToString();
            var what = enabled is { } on ? (on ? "켜기" : "끄기") : !string.IsNullOrWhiteSpace(color) ? $"색 {color}" : "설정 있음";
            return $"{p.Name} · {what}";
        }));
    }

    /// <summary>함체 임계값 한 경계의 저장 값(숫자 글). 없으면 null(빈 칸).</summary>
    private static string? ReadThreshold(IBaseDeviceModel model, string metric, string bound)
    {
        var token = model.Axes?.DeviceConfig?.Thresholds?[metric]?[bound];
        return token is JValue { Value: not null } value && (value.Type is JTokenType.Integer or JTokenType.Float)
            ? Convert.ToDouble(value.Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)
            : null;
    }

    /// <summary>카메라 모드 한 키의 저장 값 — 코드 글(모드) 또는 "true"/"false"(녹화).</summary>
    private static string? ReadMode(IBaseDeviceModel model, string key)
    {
        var token = model.Axes?.DeviceConfig?.Modes?[key];
        return token switch
        {
            null => null,
            { Type: JTokenType.Null } => null,
            { Type: JTokenType.Boolean } => (bool)token ? "true" : "false",
            _ => token.ToString(),
        };
    }

    /// <summary>부품 유형 코드 → 카탈로그의 한국어 이름. 카탈로그를 못 쓰면 코드 그대로(부품 유형은 서버 어휘가 앞서 간다).</summary>
    private static string ComponentTypeLabel(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return DeviceEnumDisplay.UnknownValue;
        try
        {
            if (Caliburn.Micro.IoC.Get<ICatalogService>() is Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.IComponentCatalog catalog
                && catalog.Find(code) is { } info && !string.IsNullOrWhiteSpace(info.Label))
                return info.Label;
        }
        catch { /* 컨테이너 미구성(시험 · 미리보기) — 코드 그대로 */ }
        return code!;
    }

    /// <summary>관측 시각(ISO 8601, 오프셋 포함) → "MM-dd HH:mm". 못 읽으면 false.</summary>
    private static bool TryShortTime(string? iso, out string text)
    {
        text = string.Empty;
        if (string.IsNullOrWhiteSpace(iso)) return false;
        if (!DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)) return false;
        text = at.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
        return true;
    }
    #endregion
}
