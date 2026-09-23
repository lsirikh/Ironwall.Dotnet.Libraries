using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Lists;
/****************************************************************************
   Purpose      :
   Created By   : GHLee
   Created On   : 9/19/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 카테고리별 콘솔 목록 열 정본 — <c>N03</c>(그리드 조립)이 이 하나만 보고 <c>DataGridColumn</c> 을 만든다.
/// </summary>
/// <remarks>
/// <para>순서·기본 6열·계약별 겹치지 않는 "종류" 열은 목업(<c>docs/design/window-layout-system-storyboard.html</c>
/// <c>TYPES[].cols</c> · <c>EXTRA_COLS</c> · <c>#cols</c> 표)에서 그대로 옮겼다 — 화면마다 다른 임의값을
/// 새로 짓지 않는다.</para>
/// <para><b>계약 유출을 막는 지점은 여기 하나다.</b> 카테고리별 원본 목록에는 "종류" 열이 축/레거시 두
/// 버전으로 나란히 들어있고(<see cref="DeviceColumnSpec.AxisContractOnly"/>/<see cref="DeviceColumnSpec.LegacyContractOnly"/>),
/// <see cref="For"/> 가 호출 시점 계약과 맞지 않는 쪽을 걸러낸다. 그래서 반환값은 항상 카테고리당 정확히
/// 기본 6열이고, 호출자는 계약 분기를 다시 하지 않는다.</para>
/// </remarks>
public static class DeviceColumnCatalog
{
    /// <summary>카테고리 + 계약 → 그 화면이 실제로 그릴 열 목록(기본 6열 + 선택 열), 표시 순서 그대로.</summary>
    /// <param name="category">레일이 고른 카테고리. 그룹은 <see cref="ForGroups"/> 로 별도.</param>
    /// <param name="isAxisContract">v7.0+ 축 계약이면 참(<c>DeviceQueryPolicy.IsAxisContract</c>) — 6.3 이면 거짓.</param>
    /// <param name="isUnitEra">
    /// v8.0+ 부대 편제 계약이면 참(<c>IUnitScopeService.IsUnitEra</c>·<c>DeviceContractGateViewModel.IsUnitEra</c>) —
    /// 거짓이면 "소속 부대"(<see cref="UNIT_COLUMN_KEY"/>) 열을 <b>레코드 계약을 늘리지 않고</b> 여기서 걸러낸다(D-14).
    /// <see cref="DeviceColumnSpec.AxisContractOnly"/>/<see cref="DeviceColumnSpec.LegacyContractOnly"/> 는 v7.0 경계
    /// 전용이라 v8.0 경계에는 쓸 수 없다 — 그렇다고 계약이 고정된 레코드에 세 번째 플래그를 늘리지 않는다.
    /// </param>
    public static IReadOnlyList<DeviceColumnSpec> For(EnumDeviceCategory category, bool isAxisContract, bool isUnitEra = false)
    {
        var raw = category switch
        {
            EnumDeviceCategory.Controller => Controller(),
            EnumDeviceCategory.Sensor => Sensor(),
            EnumDeviceCategory.Camera => Camera(),
            EnumDeviceCategory.Speaker => Speaker(),
            EnumDeviceCategory.Enclosure => Enclosure(),
            EnumDeviceCategory.Lamp => Lamp(),
            EnumDeviceCategory.Gate => Gate(),
            _ => Array.Empty<DeviceColumnSpec>(),
        };

        return raw
            .Where(s => !(isAxisContract ? s.LegacyContractOnly : s.AxisContractOnly))
            .Where(s => isUnitEra || s.Key != UNIT_COLUMN_KEY)
            .ToArray();
    }

    /// <summary>"소속 부대" 열의 안정 키 — v8.0 미만에서 <see cref="For"/> 가 이 키로 걸러낸다.</summary>
    private const string UNIT_COLUMN_KEY = "unit";

    /// <summary>장비 그룹 목록 — 카테고리 축과 무관한 별개 리소스라 계약 분기가 없다.</summary>
    public static IReadOnlyList<DeviceColumnSpec> ForGroups() => new[]
    {
        new DeviceColumnSpec("name", "이름", nameof(DeviceGroupViewModel.Name), DeviceColumnKind.Text, IsDefault: true, Width: 0),
        new DeviceColumnSpec("description", "설명", nameof(DeviceGroupViewModel.Description), DeviceColumnKind.Text, IsDefault: true, Width: 220),
        new DeviceColumnSpec("count", "장비수", nameof(DeviceGroupViewModel.DeviceCount), DeviceColumnKind.Mono, IsDefault: true, Width: 90),
    };

    // ── 공통 6열 뼈대 ──
    // status·number·name·kind(축/레거시 두 버전)·{카테고리 5번째 열}·enabled 순서는 목업 #cols 표를 그대로 따른다.
    // kind 두 버전 중 하나는 For() 가 항상 걸러내므로, 실제로 화면에 남는 기본열은 언제나 6개다.

    private static DeviceColumnSpec Status() =>
        new("status", "상태", nameof(DeviceViewModel.StatusDisplay), DeviceColumnKind.StatusPill, IsDefault: true, Width: 88);

    private static DeviceColumnSpec Number() =>
        new("number", "장비번호", nameof(DeviceViewModel.DeviceNumber), DeviceColumnKind.Mono, IsDefault: true, Width: 96);

    private static DeviceColumnSpec Name() =>
        new("name", "장비명", nameof(DeviceViewModel.DeviceName), DeviceColumnKind.Text, IsDefault: true, Width: 0);

    private static DeviceColumnSpec KindAxis() =>
        new("kind", "종류", nameof(DeviceViewModel.TypeAxisDisplay), DeviceColumnKind.Text, IsDefault: true, Width: 130, AxisContractOnly: true);

    private static DeviceColumnSpec KindLegacy() =>
        new("kind", "종류", nameof(DeviceViewModel.DeviceTypeDisplay), DeviceColumnKind.Text, IsDefault: true, Width: 130, LegacyContractOnly: true);

    private static DeviceColumnSpec Enabled() =>
        new("enabled", "활성화", nameof(DeviceViewModel.IsEnable), DeviceColumnKind.Check, IsDefault: true, Width: 72);

    // ── 카테고리 공통 선택 열(위치/좌표/그룹/버전) — 전 카테고리가 BaseDeviceViewModel 을 통해 갖는다 ──

    private static IEnumerable<DeviceColumnSpec> CommonOptional() => new[]
    {
        new DeviceColumnSpec("location", "위치", nameof(DeviceViewModel.Location), DeviceColumnKind.Text, IsDefault: false, Width: 160),
        new DeviceColumnSpec("latitude", "위도", nameof(DeviceViewModel.Latitude), DeviceColumnKind.Mono, IsDefault: false, Width: 96),
        new DeviceColumnSpec("longitude", "경도", nameof(DeviceViewModel.Longitude), DeviceColumnKind.Mono, IsDefault: false, Width: 96),
        new DeviceColumnSpec("groups", "그룹", nameof(DeviceViewModel.DeviceGroupsText), DeviceColumnKind.Text, IsDefault: false, Width: 160),
        // v7.0+ 은 형상축 hardware_spec.firmware 가 이 자리를 대신한다 — 옛 화면에만 열로 남긴다.
        new DeviceColumnSpec("version", "버전", nameof(DeviceViewModel.Version), DeviceColumnKind.Text, IsDefault: false, Width: 90, LegacyContractOnly: true),
        // D-14: 소속 부대(서버 8.0+ unit_id). 6.3·7.0 에서는 For() 가 UNIT_COLUMN_KEY 로 걸러낸다(레코드에
        // 세 번째 계약 플래그를 늘리지 않는다) — 여기 선언은 계약 무관하게 항상 있지만 걸러지면 화면에 안 나온다.
        new DeviceColumnSpec(UNIT_COLUMN_KEY, "소속 부대", nameof(DeviceViewModel.UnitDisplay), DeviceColumnKind.Text, IsDefault: false, Width: 140),
    };

    // ── 카테고리별 조립 ──

    private static IReadOnlyList<DeviceColumnSpec> Controller() => Base(AddressColumn()).ToArray();

    private static IReadOnlyList<DeviceColumnSpec> Sensor() => Base(ControllerColumn()).ToArray();

    private static IReadOnlyList<DeviceColumnSpec> Camera() => Base(AddressColumn())
        .Concat(new[]
        {
            // v7.0 에서 제거된 옛 필드 — 스토리보드 열 표의 "상세 패널로" 항목이지만
            // 6.3 화면은 여전히 이 값을 목록에서 봐야 하므로 레거시 전용 선택 열로만 남긴다.
            new DeviceColumnSpec("mode", "모드", nameof(CameraDeviceViewModel.Mode), DeviceColumnKind.Text, IsDefault: false, Width: 110, LegacyContractOnly: true),
            new DeviceColumnSpec("category", "분류", nameof(CameraDeviceViewModel.Category), DeviceColumnKind.Text, IsDefault: false, Width: 100, LegacyContractOnly: true),
            new DeviceColumnSpec("is_record", "녹화", nameof(CameraDeviceViewModel.IsRecord), DeviceColumnKind.Check, IsDefault: false, Width: 80, LegacyContractOnly: true),
        })
        .ToArray();

    private static IReadOnlyList<DeviceColumnSpec> Speaker() => Base(ServerColumn())
        .Concat(new[]
        {
            new DeviceColumnSpec("description", "설명", nameof(SpeakerDeviceViewModel.Description), DeviceColumnKind.Text, IsDefault: false, Width: 180),
        })
        .ToArray();

    private static IReadOnlyList<DeviceColumnSpec> Enclosure() => Base(DoorColumn(nameof(EnclosureDeviceViewModel.DoorStatusDisplay)))
        .Concat(new[]
        {
            new DeviceColumnSpec("heater", "히터", nameof(EnclosureDeviceViewModel.HeaterEnabled), DeviceColumnKind.Check, IsDefault: false, Width: 80),
            new DeviceColumnSpec("fan", "팬", nameof(EnclosureDeviceViewModel.FanEnabled), DeviceColumnKind.Check, IsDefault: false, Width: 80),
        })
        .ToArray();

    private static IReadOnlyList<DeviceColumnSpec> Lamp() => Base(AddressColumn())
        .Concat(new[]
        {
            new DeviceColumnSpec("description", "설명", nameof(LampDeviceViewModel.Description), DeviceColumnKind.Text, IsDefault: false, Width: 180),
        })
        .ToArray();

    private static IReadOnlyList<DeviceColumnSpec> Gate() => Base(DoorColumn(nameof(GateDeviceViewModel.DoorPosition))).ToArray();

    /// <summary>기본 6열(4번째 자리는 카테고리마다 다른 <paramref name="fifth"/>) + 공통 선택 열.</summary>
    private static IEnumerable<DeviceColumnSpec> Base(DeviceColumnSpec fifth) =>
        new[] { Status(), Number(), Name(), KindAxis(), KindLegacy(), fifth, Enabled() }.Concat(CommonOptional());

    // IP:포트 — 제어기·카메라·경광등 공용. 표시 문자열은 각 VM 의 AddressDisplay 가 만든다(빈 IP→"—", 포트 0→IP만).
    private static DeviceColumnSpec AddressColumn() =>
        new("address", "IP:포트", "AddressDisplay", DeviceColumnKind.Mono, IsDefault: true, Width: 150);

    private static DeviceColumnSpec ControllerColumn() =>
        new("controller", "제어기", nameof(SensorDeviceViewModel.ControllerDisplay), DeviceColumnKind.Mono, IsDefault: true, Width: 130);

    private static DeviceColumnSpec ServerColumn() =>
        new("server", "방송서버", nameof(SpeakerDeviceViewModel.ServerDisplay), DeviceColumnKind.Mono, IsDefault: true, Width: 130);

    private static DeviceColumnSpec DoorColumn(string bindingPath) =>
        new("door", "문 위치", bindingPath, DeviceColumnKind.StatusPill, IsDefault: true, Width: 110);
}
