using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Properties;
using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.BulkImport;
/****************************************************************************
   Purpose      : 장비 대량 등록 — 파일의 열 하나 = 카탈로그 속성 하나 (device-bulk-import FR-02 · FR-04)
   Created By   : GHLee
   Created On   : 10/13/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>열이 무엇을 담는가 — 행 칸(명세) 말고는 대조 · 참조 칸이다.</summary>
public enum BulkColumnKind
{
    /// <summary>서버 장비 ID — 대조 열쇠(비어 있으면 신규 또는 이름 대조).</summary>
    Id,
    /// <summary>행 뷰모델 칸(<see cref="DevicePropertySpec.ViewModelPath"/>).</summary>
    Field,
    /// <summary>센서의 소속 제어기 — 이름 · IP · IP:포트 · #번호로 찾는다(PRD D-5).</summary>
    Controller,
    /// <summary>소속 부대(8.0+) — 부대 ID 또는 이름(PRD D-6).</summary>
    Unit,
}

/// <summary>
/// 파일의 열 하나. <see cref="Key"/> 는 숨은 키 줄에 그대로 쓰고(<c>@키</c>), <see cref="Header"/> 는 사람이 보는 머리 줄이다.
/// </summary>
public sealed record BulkColumn(string Key, string Header, BulkColumnKind Kind, DevicePropertySpec? Spec, bool IsRequiredOnCreate)
{
    /// <summary>비밀번호 칸 — 내보내지 않고(FR-03) 미리보기 · 결과 · 로그에 남기지 않는다(FR-16).</summary>
    public bool IsSecret => Spec?.Editor == DevicePropertyEditor.Password;

    /// <summary>고를 값이 정해진 칸(종류 · 운영 상태 · 참/거짓 …) — 양식의 유효성 목록 · 어휘 검사 대상.</summary>
    public bool IsChoice => Spec?.Editor is DevicePropertyEditor.Choice or DevicePropertyEditor.Boolean;
}

/// <summary>
/// 카테고리 · 계약 세대별 열 목록 — 상세 폼과 같은 정본(<see cref="DevicePropertyCatalog.For"/>)에서 쓰기 가능한 행 칸만 고른다.
/// </summary>
/// <remarks>
/// <para><b>빼는 것</b>: 읽기 전용(카테고리 · 부품 · 상태 · 그룹) · 축 값 전용 칸(<see cref="DevicePropertySpec.AxisWritePath"/> 만 있는
/// 제조사 · 모델 등 — 등록 한 줄이 POST + PATCH 두 번이 되면 반쯤 된 상태가 생긴다, PRD §2) · 스피커 관리 서버(목록 객체).</para>
/// <para><b>더하는 것</b>: 맨 앞 ID(대조 열쇠) · 센서의 소속 제어기 · 8.0+ 소속 부대.</para>
/// </remarks>
public static class BulkColumnCatalog
{
    public const string IdKey = "id";
    public const string ControllerKey = "controller_id";
    public const string UnitKey = "unit_id";
    public const string NumberKey = "number_device";
    public const string NameKey = "name_device";
    public const string IpKey = "connection.ip_address";
    public const string PortKey = "connection.ip_port";

    /// <summary>숨은 키 줄의 칸 머리표 — 이 글로 시작하는 칸만 있는 줄이 키 줄이다.</summary>
    public const string KeyPrefix = "@";

    /// <summary>양식의 예시 줄 표지 — ID 칸이 이 글이면 올릴 때 건너뛴다(FR-05).</summary>
    public const string ExampleMarker = "예시";

    public static IReadOnlyList<BulkColumn> For(EnumDeviceCategory category, bool isAxisContract, bool isUnitEra)
    {
        var columns = new List<BulkColumn> { new(IdKey, "ID", BulkColumnKind.Id, null, false) };
        foreach (var spec in DevicePropertyCatalog.For(category, isAxisContract, isUnitEra))
        {
            if (spec.Key == UnitKey)
            {
                columns.Add(new BulkColumn(UnitKey, spec.Label, BulkColumnKind.Unit, spec, false));
                continue;
            }
            if (spec.OptionSource == DevicePropertyOptionSource.Controllers)
            {
                columns.Add(new BulkColumn(ControllerKey, spec.Label, BulkColumnKind.Controller, spec, spec.IsRequiredOnCreate));
                continue;
            }
            if (!IsImportable(spec)) continue;
            // 같은 키가 두 줄(계약별)일 수 있다 — For 가 이미 계약으로 걸렀으니 한 카테고리 안에서는 하나뿐이다. 그래도 겹치면 앞의 것.
            if (columns.Any(c => c.Key == spec.Key)) continue;
            columns.Add(new BulkColumn(spec.Key, spec.Label, BulkColumnKind.Field, spec, spec.IsRequiredOnCreate));
        }
        return columns;
    }

    /// <summary>행 뷰모델로 읽고 쓰는 쓰기 가능한 칸만.</summary>
    private static bool IsImportable(DevicePropertySpec spec)
        => spec.Writable != DevicePropertyWritable.No
           && spec.Editor != DevicePropertyEditor.ReadOnly
           && spec.ViewModelPath is not null
           && spec.OptionSource is not (DevicePropertyOptionSource.Controllers or DevicePropertyOptionSource.Servers);

    /// <summary>카테고리 한국어 이름 — 시트 이름 · 미리보기의 "시트" 칸.</summary>
    public static string CategoryLabel(EnumDeviceCategory category) => category switch
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

    /// <summary>시트 이름 → 카테고리(한국어 이름 또는 영문 열거 이름). 모르면 null.</summary>
    public static EnumDeviceCategory? CategoryOfSheet(string? sheetName)
    {
        var name = (sheetName ?? string.Empty).Trim();
        foreach (var category in Categories)
        {
            if (string.Equals(CategoryLabel(category), name, StringComparison.Ordinal)
                || string.Equals(category.ToString(), name, StringComparison.OrdinalIgnoreCase))
                return category;
        }
        return null;
    }

    /// <summary>대량 등록을 받는 7 카테고리 — 쓰기 순서이기도 하다(제어기가 먼저, FR-14).</summary>
    public static IReadOnlyList<EnumDeviceCategory> Categories { get; } = new[]
    {
        EnumDeviceCategory.Controller, EnumDeviceCategory.Sensor, EnumDeviceCategory.Camera,
        EnumDeviceCategory.Speaker, EnumDeviceCategory.Enclosure, EnumDeviceCategory.Lamp, EnumDeviceCategory.Gate,
    };
}
