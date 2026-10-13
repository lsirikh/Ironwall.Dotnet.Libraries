using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Forms;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Properties;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.BulkImport;
/****************************************************************************
   Purpose      : 장비 대량 등록 — 읽은 줄의 판정(신규 · 같음 · 수정 · 오류) (device-bulk-import FR-08~FR-11 · FR-15)
   Created By   : GHLee
   Created On   : 10/13/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

public enum BulkRowVerdict
{
    /// <summary>서버에 없는 장비 — 등록한다.</summary>
    New,
    /// <summary>서버 값과 같다 — 보내지 않는다.</summary>
    Same,
    /// <summary>서버 장비의 칸이 바뀌었다 — 바뀐 칸을 보낸다.</summary>
    Update,
    /// <summary>보낼 수 없다 — 사유를 칸 · 줄에 단다.</summary>
    Error,
}

/// <summary>미리보기 표의 칸 하나.</summary>
public sealed class BulkCell
{
    public required BulkColumn Column { get; init; }

    /// <summary>파일에 적힌 글(앞뒤 공백 제거).</summary>
    public required string Raw { get; init; }

    /// <summary>보낼 값(코드 · true/false · 숫자 글). 비면 "보내지 않음".</summary>
    public string? Value { get; set; }

    /// <summary>서버 값과 다르다(수정 줄).</summary>
    public bool IsChanged { get; set; }

    /// <summary>이 칸의 오류 사유 — 있으면 붉게 표시한다.</summary>
    public string? Error { get; set; }

    /// <summary>화면 · 결과에 보일 글 — 비밀번호는 가린다(FR-16).</summary>
    public string Display => Column.IsSecret && Raw.Length > 0 ? MaskedSecret : Raw;

    public const string MaskedSecret = "●●●●";
}

/// <summary>미리보기 표의 줄 하나(Temp-state Draft — 서버에는 [등록] 때만 쓴다).</summary>
public sealed class BulkImportRow
{
    public required int Line { get; init; }
    public required string SheetName { get; init; }
    public required EnumDeviceCategory Category { get; init; }
    public required IReadOnlyList<BulkCell> Cells { get; init; }

    /// <summary>대조된 서버 장비(수정 · 같음). 신규면 null.</summary>
    public IBaseDeviceModel? Existing { get; set; }

    public BulkRowVerdict Verdict { get; set; }

    /// <summary>이 줄의 표에 아예 없는 필수 칸(머리 글) — 새 장비 줄이면 오류다.</summary>
    public IReadOnlyList<string> MissingRequired { get; init; } = Array.Empty<string>();

    /// <summary>칸에 매이지 않는 오류(중복 · 권한 · 충돌).</summary>
    public List<string> RowErrors { get; } = new();

    /// <summary>센서의 소속 제어기 — 이미 서버에 있는 것.</summary>
    public IControllerDeviceModel? Controller { get; set; }

    /// <summary>센서의 소속 제어기가 <b>같은 파일의 새 제어기</b>면 그 이름(쓰기기가 제어기 등록 뒤 잇는다, FR-14).</summary>
    public string? PendingControllerName { get; set; }

    public BulkCell? CellOf(string key) => Cells.FirstOrDefault(c => c.Column.Key == key);

    public string Name => CellOf(BulkColumnCatalog.NameKey)?.Raw is { Length: > 0 } name ? name : Existing?.DeviceName ?? string.Empty;

    /// <summary>오류 사유 전부(칸 · 줄) — 한 줄 글.</summary>
    public IEnumerable<string> Errors => Cells.Where(c => c.Error is not null).Select(c => $"{c.Column.Header}: {c.Error}").Concat(RowErrors);
}

/// <summary>판정 결과 — 파일 전체를 거절했으면 <see cref="FileError"/>.</summary>
public sealed class BulkImportPlan
{
    public IReadOnlyList<BulkImportRow> Rows { get; init; } = Array.Empty<BulkImportRow>();
    public string? FileError { get; init; }

    /// <summary>파일에 있었지만 모르는 열(무시함) — 머리 글 그대로.</summary>
    public IReadOnlyList<string> IgnoredColumns { get; init; } = Array.Empty<string>();

    public int Count(BulkRowVerdict verdict) => Rows.Count(r => r.Verdict == verdict);
}

/// <summary>판정에 필요한 서버 쪽 사실 — 대시보드가 채운다(시험은 손으로).</summary>
public sealed class BulkImportContext
{
    /// <summary>CSV · 카테고리 이름 없는 시트를 읽을 카테고리(지금 레일).</summary>
    public EnumDeviceCategory? RailCategory { get; init; }
    public bool IsAxisContract { get; init; }
    public bool IsUnitEra { get; init; }

    /// <summary>서버 캐시의 장비 전부(모든 카테고리 — 센서의 소속 제어기 찾기에도 쓴다).</summary>
    public IReadOnlyList<IBaseDeviceModel> Devices { get; init; } = Array.Empty<IBaseDeviceModel>();

    /// <summary>종류축 · 부가 축 선택지 공급자(대시보드). 없으면 그 칸은 어휘 검사를 하지 않는다.</summary>
    public IDevicePropertyOptions? Options { get; init; }

    /// <summary>부대 (ID, 이름) — 부대 칸을 이름으로 적었을 때 찾는다.</summary>
    public IReadOnlyList<(int Id, string Name)> Units { get; init; } = Array.Empty<(int, string)>();

    /// <summary>장비를 고칠 권한.</summary>
    public bool CanEdit { get; init; } = true;
}

/// <summary>
/// 읽은 표 → 줄마다 판정. 서버에는 아무것도 보내지 않는다(순수 함수 — 작업 스레드에서 돈다).
/// </summary>
/// <remarks>
/// <para><b>대조 열쇠(PRD D-2)</b> — ID 칸에 값이 있으면 ID, 없으면 (카테고리, 이름). IP:포트 · 번호는 대조가 아니라 충돌 검사다.</para>
/// <para><b>빈 칸(D-3)</b> — 신규면 기본값, 수정이면 "바꾸지 않음". 필수 칸은 신규일 때만 따진다.</para>
/// </remarks>
public static class BulkImportPlanner
{
    /// <summary>한 번에 올릴 수 있는 데이터 줄 수(FR-15 · D-9).</summary>
    public const int MaxRows = 2000;

    public static BulkImportPlan Plan(IReadOnlyList<BulkSheet> sheets, BulkImportContext context, CancellationToken token = default)
    {
        if (sheets.Count == 0) return new BulkImportPlan { FileError = "읽을 표가 없습니다." };

        var rows = new List<BulkImportRow>();
        var ignored = new List<string>();
        foreach (var sheet in sheets)
        {
            token.ThrowIfCancellationRequested();
            if (sheet.Category is not { } category)
                return new BulkImportPlan { FileError = "어느 장비의 표인지 알 수 없습니다 — 장비 목록(레일)을 고른 뒤 올리세요." };
            if (sheet.IsTruncated || sheet.Rows.Count(r => !IsExample(r, sheet)) > MaxRows)
                return new BulkImportPlan { FileError = $"줄이 너무 많습니다 — 한 번에 {MaxRows.ToString("N0", CultureInfo.InvariantCulture)}줄까지 올릴 수 있습니다. 나눠서 올리세요." };

            var columns = BulkColumnCatalog.For(category, context.IsAxisContract, context.IsUnitEra);
            var map = MapColumns(sheet, columns, ignored);
            if (!map.Values.Any(c => c.Key is BulkColumnCatalog.NameKey or BulkColumnCatalog.IdKey))
                return new BulkImportPlan { FileError = $"'{sheet.Name}' 표에서 이름 칸을 찾지 못했습니다 — [양식 받기]로 받은 머리 줄을 쓰세요." };

            // 필수 칸이 파일에 아예 없으면 새 장비 줄은 만들 수 없다(기존 장비 수정은 된다 — 빈 칸 = 바꾸지 않음).
            var missing = columns.Where(c => c.IsRequiredOnCreate && !map.ContainsValue(c)).Select(c => c.Header).ToList();
            foreach (var source in sheet.Rows)
            {
                token.ThrowIfCancellationRequested();
                if (IsExample(source, sheet)) continue;
                var cells = map.Select(kv => new BulkCell { Column = kv.Value, Raw = kv.Key < source.Cells.Count ? source.Cells[kv.Key].Trim() : string.Empty })
                               .ToList();
                rows.Add(new BulkImportRow { Line = source.Line, SheetName = sheet.Name, Category = category, Cells = cells, MissingRequired = missing });
            }
        }

        var devices = context.Devices;
        foreach (var row in rows) { token.ThrowIfCancellationRequested(); Judge(row, rows, context); }
        MarkDuplicatesInFile(rows);
        MarkServerConflicts(rows, devices);

        foreach (var row in rows)
        {
            if (row.Existing is null && row.MissingRequired.Count > 0)
                row.RowErrors.Add($"새 장비에 꼭 있어야 하는 칸이 파일에 없습니다: {string.Join(", ", row.MissingRequired)}");
            if (row.Verdict != BulkRowVerdict.Same && !context.CanEdit) row.RowErrors.Add("장비를 고칠 권한이 없습니다.");
            if (row.Errors.Any()) row.Verdict = BulkRowVerdict.Error;
        }

        return new BulkImportPlan { Rows = rows, IgnoredColumns = ignored.Distinct().ToList() };
    }

    #region - Columns -
    /// <summary>파일 열 번호 → 열. 키 줄이 있으면 키로, 없으면 머리 글(표시명 · 키 · 끝의 *)로 찾는다. 모르는 열은 무시한다.</summary>
    private static Dictionary<int, BulkColumn> MapColumns(BulkSheet sheet, IReadOnlyList<BulkColumn> columns, List<string> ignored)
    {
        var map = new Dictionary<int, BulkColumn>();
        var count = Math.Max(sheet.Headers.Count, sheet.Keys?.Count ?? 0);
        for (var i = 0; i < count; i++)
        {
            var key = sheet.Keys is { } keys && i < keys.Count ? keys[i] : null;
            var header = i < sheet.Headers.Count ? sheet.Headers[i] : string.Empty;
            var column = (string.IsNullOrWhiteSpace(key) ? null : columns.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase)))
                         ?? FindByHeader(columns, header);
            if (column is null || map.ContainsValue(column))
            {
                if (!string.IsNullOrWhiteSpace(header)) ignored.Add(header);
                continue;
            }
            map[i] = column;
        }
        return map;
    }

    private static BulkColumn? FindByHeader(IReadOnlyList<BulkColumn> columns, string header)
    {
        var text = (header ?? string.Empty).Trim().TrimEnd('*').Trim();
        if (text.Length == 0) return null;
        return columns.FirstOrDefault(c => string.Equals(c.Header, text, StringComparison.Ordinal))
               ?? columns.FirstOrDefault(c => string.Equals(c.Key, text, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>양식의 예시 줄 — ID 칸(키 줄 또는 머리 "ID")이 "예시"면 건너뛴다.</summary>
    private static bool IsExample(BulkSheetRow row, BulkSheet sheet)
    {
        for (var i = 0; i < row.Cells.Count; i++)
        {
            var isId = (sheet.Keys is { } keys && i < keys.Count && string.Equals(keys[i], BulkColumnCatalog.IdKey, StringComparison.OrdinalIgnoreCase))
                       || (i < sheet.Headers.Count && string.Equals(sheet.Headers[i].Trim(), "ID", StringComparison.OrdinalIgnoreCase));
            if (isId) return string.Equals(row.Cells[i].Trim(), BulkColumnCatalog.ExampleMarker, StringComparison.Ordinal);
        }
        return false;
    }
    #endregion

    #region - Judge -
    private static void Judge(BulkImportRow row, IReadOnlyList<BulkImportRow> all, BulkImportContext context)
    {
        var sameCategory = context.Devices.Where(d => BulkDeviceRows.CategoryOf(d) == row.Category).ToList();
        row.Existing = Match(row, sameCategory);
        var creating = row.Existing is null;
        var existingRow = row.Existing is null ? null : BulkDeviceRows.Wrap(row.Existing);
        var changed = false;

        foreach (var cell in row.Cells)
        {
            switch (cell.Column.Kind)
            {
                case BulkColumnKind.Id:
                    continue;
                case BulkColumnKind.Controller:
                    changed |= JudgeController(row, cell, all, context, creating);
                    continue;
                case BulkColumnKind.Unit:
                    changed |= JudgeUnit(row, cell, context);
                    continue;
            }

            if (cell.Raw.Length == 0)
            {
                if (creating && cell.Column.IsRequiredOnCreate) cell.Error = "필수 칸이 비었습니다.";
                continue;
            }

            var value = Normalize(cell, row.Category, context);
            if (cell.Error is not null) continue;

            // 서버 값과 같은 칸은 따지지도 보내지도 않는다 — 내보낸 파일을 그대로 올리면 옛 값(포트 0 등)이 검증에 걸려 "오류"가 되던 것을 막는다.
            if (row.Existing is not null && existingRow is not null
                && SameText(BulkDeviceRows.ReadText(row.Existing, existingRow, cell.Column, includeSecret: true), value!))
                continue;

            var spec = cell.Column.Spec!;
            var invalid = DevicePropertyAccessor.Validate(spec, value, creating);
            if (invalid is null && cell.Column.Key == BulkColumnCatalog.IpKey && !IsIpv4(value)) invalid = "IP 형식이 아닙니다(예: 192.168.0.10).";
            if (invalid is not null) { cell.Error = invalid; continue; }

            cell.Value = value;
            cell.IsChanged = row.Existing is not null;
            changed |= cell.IsChanged;
        }

        row.Verdict = row.Existing is null ? BulkRowVerdict.New : changed ? BulkRowVerdict.Update : BulkRowVerdict.Same;
    }

    /// <summary>대조 — ID → (카테고리, 이름). 못 찾으면 null(신규). ID 가 틀리면 칸 오류.</summary>
    private static IBaseDeviceModel? Match(BulkImportRow row, IReadOnlyList<IBaseDeviceModel> sameCategory)
    {
        var idCell = row.CellOf(BulkColumnCatalog.IdKey);
        if (idCell is { Raw.Length: > 0 })
        {
            if (!int.TryParse(idCell.Raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) || id <= 0)
            {
                idCell.Error = "ID 는 1 이상의 숫자여야 합니다. 새 장비면 비워 두세요.";
                return null;
            }
            var byId = sameCategory.FirstOrDefault(d => d.Id == id);
            if (byId is null) idCell.Error = $"서버에 ID {id} 인 {BulkColumnCatalog.CategoryLabel(row.Category)} 장비가 없습니다. 새 장비면 ID 칸을 비우세요.";
            return byId;
        }

        var name = row.CellOf(BulkColumnCatalog.NameKey)?.Raw;
        if (string.IsNullOrEmpty(name)) return null;
        var byName = sameCategory.Where(d => SameName(d.DeviceName, name)).ToList();
        if (byName.Count > 1)
        {
            row.RowErrors.Add($"서버에 이름이 '{name}' 인 장비가 {byName.Count}대 있습니다 — ID 칸으로 어느 장비인지 정하세요.");
            return null;
        }
        return byName.FirstOrDefault();
    }

    /// <summary>선택 칸은 코드 · 화면 이름 둘 다 받아 코드로 바꾼다(D-4). 참/거짓은 예/아니오 · O/X · 1/0 도 받는다.</summary>
    private static string? Normalize(BulkCell cell, EnumDeviceCategory category, BulkImportContext context)
    {
        var raw = cell.Raw;
        var spec = cell.Column.Spec!;
        if (spec.Editor == DevicePropertyEditor.Boolean)
        {
            if (TryBool(raw, out var flag)) return flag ? "true" : "false";
            cell.Error = "예/아니오(true/false)로 적으세요.";
            return null;
        }
        if (spec.Editor != DevicePropertyEditor.Choice) return raw;

        var options = BulkDeviceRows.OptionsFor(cell.Column, category, context.Options);
        if (options.Count == 0) return raw;   // 어휘를 모른다 — 서버가 최종 판정한다
        var hit = options.FirstOrDefault(o => string.Equals(o.Text, raw, StringComparison.OrdinalIgnoreCase))
                  ?? options.FirstOrDefault(o => string.Equals(o.Display, raw, StringComparison.Ordinal));
        if (hit?.Text is { } code) return code;
        cell.Error = $"고를 수 있는 값이 아닙니다 — {string.Join(" · ", options.Take(8).Select(o => o.Text))}{(options.Count > 8 ? " …" : string.Empty)}";
        return null;
    }

    private static bool JudgeController(BulkImportRow row, BulkCell cell, IReadOnlyList<BulkImportRow> all, BulkImportContext context, bool creating)
    {
        if (cell.Raw.Length == 0)
        {
            if (creating) cell.Error = "필수 칸이 비었습니다.";
            return false;
        }

        var controllers = context.Devices.OfType<IControllerDeviceModel>().Where(c => c.Id > 0).ToList();
        var found = FindController(controllers, cell.Raw, out var ambiguous);
        if (ambiguous)
        {
            cell.Error = $"'{cell.Raw}' 에 맞는 제어기가 여럿입니다 — IP 나 #번호로 적으세요.";
            return false;
        }

        if (found is null)
        {
            // 같은 파일의 새 제어기(FR-14) — 쓰기기가 제어기를 먼저 등록하고 이 이름으로 잇는다.
            var pending = all.FirstOrDefault(r => r.Category == EnumDeviceCategory.Controller && r.Existing is null
                                                 && SameName(r.CellOf(BulkColumnCatalog.NameKey)?.Raw, cell.Raw));
            if (pending is null)
            {
                cell.Error = $"소속 제어기 '{cell.Raw}' 를 찾을 수 없습니다 — 제어기 이름 · IP · #번호로 적으세요.";
                return false;
            }
            row.PendingControllerName = pending.CellOf(BulkColumnCatalog.NameKey)!.Raw;
            cell.Value = row.PendingControllerName;
            cell.IsChanged = row.Existing is not null;
            return cell.IsChanged;
        }

        row.Controller = found;
        cell.Value = found.Id.ToString(CultureInfo.InvariantCulture);
        if (row.Existing is ISensorDeviceModel sensor)
        {
            cell.IsChanged = sensor.Controller?.Id != found.Id;
            return cell.IsChanged;
        }
        return false;
    }

    /// <summary>제어기 찾기 — 이름 → IP → IP:포트 → #번호(D-5).</summary>
    internal static IControllerDeviceModel? FindController(IReadOnlyList<IControllerDeviceModel> controllers, string text, out bool ambiguous)
    {
        ambiguous = false;
        var value = text.Trim();
        IEnumerable<IControllerDeviceModel>[] stages =
        {
            controllers.Where(c => SameName(c.DeviceName, value)),
            controllers.Where(c => string.Equals(c.IpAddress?.Trim(), value, StringComparison.OrdinalIgnoreCase)),
            controllers.Where(c => string.Equals($"{c.IpAddress?.Trim()}:{c.Port}", value, StringComparison.OrdinalIgnoreCase)),
            value.StartsWith('#') && int.TryParse(value[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                ? controllers.Where(c => c.DeviceNumber == number) : Enumerable.Empty<IControllerDeviceModel>(),
        };
        foreach (var stage in stages)
        {
            var hits = stage.ToList();
            if (hits.Count == 1) return hits[0];
            if (hits.Count > 1) { ambiguous = true; return null; }
        }
        return null;
    }

    private static bool JudgeUnit(BulkImportRow row, BulkCell cell, BulkImportContext context)
    {
        if (cell.Raw.Length == 0) return false;
        int? id = null;
        if (int.TryParse(cell.Raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0) id = parsed;
        else
        {
            var hits = context.Units.Where(u => string.Equals(u.Name?.Trim(), cell.Raw, StringComparison.Ordinal)).ToList();
            if (hits.Count == 1) id = hits[0].Id;
            else if (hits.Count > 1) { cell.Error = $"이름이 '{cell.Raw}' 인 부대가 여럿입니다 — 부대 ID 로 적으세요."; return false; }
        }
        if (id is null) { cell.Error = $"부대 '{cell.Raw}' 를 찾을 수 없습니다 — 부대 ID 나 이름으로 적으세요."; return false; }

        cell.Value = id.Value.ToString(CultureInfo.InvariantCulture);
        cell.IsChanged = row.Existing is not null && row.Existing.UnitId != id;
        return cell.IsChanged;
    }
    #endregion

    #region - Conflicts -
    /// <summary>같은 파일 안 같은 카테고리에서 ID · 이름 · 번호 · IP:포트가 겹치는 줄 — 겹친 줄 모두에 단다.</summary>
    private static void MarkDuplicatesInFile(IReadOnlyList<BulkImportRow> rows)
    {
        foreach (var group in rows.GroupBy(r => r.Category))
        {
            Mark(group, r => r.CellOf(BulkColumnCatalog.IdKey)?.Raw, "ID");
            Mark(group, r => r.CellOf(BulkColumnCatalog.NameKey)?.Raw?.ToUpperInvariant(), "이름");
            Mark(group, r => r.CellOf(BulkColumnCatalog.NumberKey)?.Raw, "장비번호");
            Mark(group, r => RawAddressOf(r), "IP:포트");
        }

        static void Mark(IEnumerable<BulkImportRow> rows, Func<BulkImportRow, string?> keyOf, string what)
        {
            foreach (var same in rows.Select(r => (Row: r, Key: keyOf(r))).Where(x => !string.IsNullOrEmpty(x.Key)).GroupBy(x => x.Key).Where(g => g.Count() > 1))
            {
                var lines = same.Select(x => x.Row.Line).ToList();
                foreach (var (row, _) in same)
                    row.RowErrors.Add($"같은 파일의 {string.Join(", ", lines.Where(l => l != row.Line))}줄과 {what}{(what == "ID" ? "가" : "이")} 겹칩니다.");
            }
        }
    }

    /// <summary>등록 · 수정할 줄의 번호 · IP:포트가 <b>다른</b> 서버 장비(같은 카테고리)와 겹치면 충돌.</summary>
    private static void MarkServerConflicts(IReadOnlyList<BulkImportRow> rows, IReadOnlyList<IBaseDeviceModel> devices)
    {
        foreach (var row in rows.Where(r => r.Verdict is BulkRowVerdict.New or BulkRowVerdict.Update))
        {
            var others = devices.Where(d => BulkDeviceRows.CategoryOf(d) == row.Category && d.Id != (row.Existing?.Id ?? 0)).ToList();

            if (row.CellOf(BulkColumnCatalog.NumberKey)?.Value is { } numberText
                && int.TryParse(numberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                && others.FirstOrDefault(d => d.DeviceNumber == number) is { } byNumber)
                row.RowErrors.Add($"장비번호 {number} 은(는) 서버의 '{byNumber.DeviceName}'(ID {byNumber.Id})이 쓰고 있습니다.");

            if (AddressOf(row) is { } address
                && others.FirstOrDefault(d => string.Equals(ExistingAddress(d), address, StringComparison.OrdinalIgnoreCase)) is { } byAddress)
                row.RowErrors.Add($"IP:포트 {address} 은(는) 서버의 '{byAddress.DeviceName}'(ID {byAddress.Id})이 쓰고 있습니다.");
        }
    }

    /// <summary>줄의 "IP:포트"(포트가 없으면 IP 만). IP 칸이 없거나 비면 null.</summary>
    private static string? AddressOf(BulkImportRow row)
    {
        var ip = row.CellOf(BulkColumnCatalog.IpKey)?.Value;
        if (string.IsNullOrWhiteSpace(ip)) return null;
        var port = row.CellOf(BulkColumnCatalog.PortKey)?.Value;
        return string.IsNullOrWhiteSpace(port) ? ip.Trim() : $"{ip.Trim()}:{port.Trim()}";
    }

    /// <summary>파일에 적힌 그대로의 "IP:포트"(같은 파일 안 중복 검사 — 서버 값과 같아 보내지 않는 칸도 따진다).</summary>
    private static string? RawAddressOf(BulkImportRow row)
    {
        var ip = row.CellOf(BulkColumnCatalog.IpKey)?.Raw;
        if (string.IsNullOrWhiteSpace(ip)) return null;
        var port = row.CellOf(BulkColumnCatalog.PortKey)?.Raw;
        return (string.IsNullOrWhiteSpace(port) ? ip : $"{ip}:{port}").ToUpperInvariant();
    }

    private static string? ExistingAddress(IBaseDeviceModel device)
    {
        var (ip, port) = device switch
        {
            IControllerDeviceModel c => (c.IpAddress, (int?)c.Port),
            ICameraDeviceModel c => (c.IpAddress, (int?)c.IpPort),
            ILampDeviceModel l => (l.IpAddress, (int?)l.IpPort),
            _ => (null, null),
        };
        if (string.IsNullOrWhiteSpace(ip)) return null;
        return port is > 0 ? $"{ip.Trim()}:{port}" : ip.Trim();
    }
    #endregion

    #region - Helpers -
    internal static bool SameName(string? a, string? b)
        => !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b)
           && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>숫자는 "45" 와 "45.0" 을 같게, 나머지는 대소문자 무시.</summary>
    private static bool SameText(string current, string value)
    {
        if (string.Equals(current.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
        return double.TryParse(current, NumberStyles.Float, CultureInfo.InvariantCulture, out var a)
               && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var b)
               && a.Equals(b);
    }

    private static bool IsIpv4(string? text)
    {
        var parts = (text ?? string.Empty).Split('.');
        return parts.Length == 4 && parts.All(p => p.Length is > 0 and <= 3 && p.All(char.IsAsciiDigit)) && IPAddress.TryParse(text, out _)
               && parts.All(p => int.Parse(p, CultureInfo.InvariantCulture) <= 255);
    }

    internal static bool TryBool(string text, out bool value)
    {
        switch ((text ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "true": case "예": case "y": case "yes": case "o": case "1": case "참": case "사용": case "켜기":
                value = true; return true;
            case "false": case "아니오": case "n": case "no": case "x": case "0": case "거짓": case "미사용": case "끄기":
                value = false; return true;
            default:
                value = false; return false;
        }
    }
    #endregion
}
