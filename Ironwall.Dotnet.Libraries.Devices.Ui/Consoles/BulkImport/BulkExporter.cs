using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Forms;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Properties;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.BulkImport;
/****************************************************************************
   Purpose      : 장비 대량 등록 — 내보내기 · 양식 만들기 (device-bulk-import FR-01~FR-06)
   Created By   : GHLee
   Created On   : 10/13/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 장비 목록 → 표, 빈 양식 → 표. 머리 줄은 한국어 표시명(필수 칸은 끝에 *), 키 줄은 <c>@키</c>. 비밀번호 칸은 늘 빈칸이다(FR-03).
/// </summary>
public static class BulkExporter
{
    public static IReadOnlyList<string> Headers(IReadOnlyList<BulkColumn> columns)
        => columns.Select(c => c.IsRequiredOnCreate ? c.Header + "*" : c.Header).ToList();

    public static IReadOnlyList<string> Keys(IReadOnlyList<BulkColumn> columns) => columns.Select(c => c.Key).ToList();

    /// <summary>장비마다 한 줄 — 행 뷰모델로 읽은 지금 값(선택 칸은 코드). 비밀번호는 쓰지 않는다.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> Rows(IReadOnlyList<BulkColumn> columns, IEnumerable<IBaseDeviceModel> devices)
        => devices.Select(device =>
        {
            var row = BulkDeviceRows.Wrap(device);
            return (IReadOnlyList<string>)columns.Select(c => c.IsSecret ? string.Empty : BulkDeviceRows.ReadText(device, row, c)).ToList();
        }).ToList();

    /// <summary>양식의 예시 한 줄 — ID 칸 "예시"(올릴 때 건너뛴다). 선택 칸은 첫 선택값.</summary>
    public static IReadOnlyList<string> ExampleRow(IReadOnlyList<BulkColumn> columns, EnumDeviceCategory category, IDevicePropertyOptions? provider)
    {
        var label = BulkColumnCatalog.CategoryLabel(category);
        return columns.Select(c =>
        {
            switch (c.Kind)
            {
                case BulkColumnKind.Id: return BulkColumnCatalog.ExampleMarker;
                case BulkColumnKind.Controller: return "제어기 1";
                case BulkColumnKind.Unit: return string.Empty;
            }
            if (c.IsSecret) return string.Empty;
            if (c.IsChoice) return BulkDeviceRows.OptionsFor(c, category, provider).FirstOrDefault()?.Text ?? string.Empty;
            return c.Key switch
            {
                BulkColumnCatalog.NumberKey => "1",
                BulkColumnCatalog.NameKey => $"{label} 1",
                BulkColumnCatalog.IpKey => "192.168.0.10",
                BulkColumnCatalog.PortKey => category == EnumDeviceCategory.Controller ? "5000" : "80",
                "connection.user_name" => "admin",
                "location" => "1초소",
                "latitude" => "37.5665",
                "longitude" => "126.978",
                _ => string.Empty,
            };
        }).ToList();
    }

    /// <summary>선택 칸 → 고를 수 있는 코드(양식의 유효성 목록).</summary>
    public static IReadOnlyDictionary<int, IReadOnlyList<string>> Choices(IReadOnlyList<BulkColumn> columns, EnumDeviceCategory category, IDevicePropertyOptions? provider)
    {
        var choices = new Dictionary<int, IReadOnlyList<string>>();
        for (var i = 0; i < columns.Count; i++)
        {
            if (!columns[i].IsChoice) continue;
            var values = BulkDeviceRows.OptionsFor(columns[i], category, provider).Select(o => o.Text!).Where(t => t.Length > 0).ToList();
            if (values.Count > 0) choices[i] = values;
        }
        return choices;
    }

    /// <summary>한 카테고리 표를 파일로 쓴다(CSV 또는 Excel).</summary>
    public static void Write(Stream stream, BulkFileFormat format, EnumDeviceCategory category, IReadOnlyList<BulkColumn> columns,
        IReadOnlyList<IReadOnlyList<string>> rows, IReadOnlyDictionary<int, IReadOnlyList<string>>? choices = null)
    {
        if (format == BulkFileFormat.Csv)
        {
            BulkFileWriter.WriteCsv(stream, Headers(columns), Keys(columns), rows);
            return;
        }
        BulkFileWriter.WriteXlsx(stream, new[]
        {
            new BulkFileWriter.SheetContent(BulkColumnCatalog.CategoryLabel(category), Headers(columns), Keys(columns), rows, choices),
        });
    }
}
