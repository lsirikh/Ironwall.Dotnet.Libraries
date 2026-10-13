using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.BulkImport;
/****************************************************************************
   Purpose      : 장비 대량 등록 — CSV · .xlsx 읽기/쓰기 (device-bulk-import FR-04~FR-07 · FR-15)
   Created By   : GHLee
   Created On   : 10/13/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

public enum BulkFileFormat { Csv, Excel }

/// <summary>파일에서 읽은 한 줄 — <see cref="Line"/> 은 엑셀에서 보이는 행 번호(머리 줄 = 1).</summary>
public sealed record BulkSheetRow(int Line, IReadOnlyList<string> Cells);

/// <summary>
/// 파일에서 읽은 표 하나(CSV 한 개 또는 시트 하나). 뜻(어느 열이 무엇인가)은 아직 모른다 — 판정기가 정한다.
/// </summary>
public sealed class BulkSheet
{
    public required string Name { get; init; }

    /// <summary>시트 이름이 카테고리 이름이면 그 카테고리(CSV 는 null — 지금 레일을 쓴다).</summary>
    public EnumDeviceCategory? Category { get; init; }

    public required IReadOnlyList<string> Headers { get; init; }

    /// <summary>숨은 키 줄(<c>@키</c>)이 있으면 그 키들(머리표를 뗀 것), 없으면 null.</summary>
    public IReadOnlyList<string>? Keys { get; init; }

    public required IReadOnlyList<BulkSheetRow> Rows { get; init; }

    /// <summary>상한을 넘어 읽기를 멈췄다 — 판정기가 파일 전체를 거절한다(FR-15).</summary>
    public bool IsTruncated { get; init; }
}

/// <summary>파일을 아예 읽을 수 없다 — 운영자에게 보일 문장만 싣는다(예외 원문 · 경로 없음).</summary>
public sealed class BulkFileException : Exception
{
    public BulkFileException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>
/// CSV · .xlsx 읽기. 줄 수 상한(<see cref="BulkImportPlanner.MaxRows"/>)을 조금 넘게 읽으면 멈춘다 — 거대한 파일을 다 올리지 않는다.
/// </summary>
public static class BulkFileReader
{
    /// <summary>상한을 넘은 것을 알기에 충분한 만큼만 더 읽는다.</summary>
    private const int ReadSlack = 1;

    public static IReadOnlyList<BulkSheet> Read(string path, CancellationToken token = default)
    {
        var extension = Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
        if (extension is not (".csv" or ".xlsx"))
            throw new BulkFileException("CSV(.csv) 또는 Excel(.xlsx) 파일만 올릴 수 있습니다.");

        try
        {
            using var stream = new FileStream(path!, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return extension == ".csv"
                ? new[] { ReadCsv(stream, Path.GetFileNameWithoutExtension(path)!, token) }
                : ReadXlsx(stream, token);
        }
        catch (BulkFileException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (IOException ex)
        {
            throw new BulkFileException("파일을 열 수 없습니다 — 다른 프로그램(Excel 등)에서 열려 있으면 닫고 다시 올리세요.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new BulkFileException("파일을 읽을 권한이 없습니다.", ex);
        }
        catch (Exception ex)
        {
            throw new BulkFileException("파일 내용을 읽지 못했습니다 — 형식이 맞는지 확인하세요.", ex);
        }
    }

    #region - CSV -
    /// <summary>CSV 한 개 — 인코딩은 BOM · 엄격한 UTF-8 해독 · CP949 순으로 판별한다(PRD D-8).</summary>
    public static BulkSheet ReadCsv(Stream stream, string name, CancellationToken token = default)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var text = Decode(buffer.ToArray());

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            BadDataFound = null,
            MissingFieldFound = null,
            DetectColumnCountChanges = false,
            IgnoreBlankLines = false,
        };

        var records = new List<string[]>();
        using (var reader = new StringReader(text))
        using (var parser = new CsvParser(reader, config))
        {
            while (parser.Read())
            {
                token.ThrowIfCancellationRequested();
                records.Add(parser.Record ?? Array.Empty<string>());
                if (records.Count > BulkImportPlanner.MaxRows + 2 + ReadSlack) break;
            }
        }
        return ToSheet(name, null, records, records.Count > BulkImportPlanner.MaxRows + 2 + ReadSlack);
    }

    /// <summary>바이트 → 글. BOM 이 있으면 그 인코딩, 없으면 UTF-8 로 엄격히 풀어 보고 깨지면 CP949.</summary>
    public static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);

        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Cp949().GetString(bytes);
        }
    }

    /// <summary>CP949(한국어 Windows 기본) — .NET 은 코드 페이지 공급자를 등록해야 쓸 수 있다.</summary>
    public static Encoding Cp949()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(949);
    }
    #endregion

    #region - XLSX -
    /// <summary>통합 문서의 보이는 시트 전부(선택값 시트 제외). 어느 시트를 쓸지는 <see cref="SelectSheets"/>.</summary>
    public static IReadOnlyList<BulkSheet> ReadXlsx(Stream stream, CancellationToken token = default)
    {
        using var workbook = new XLWorkbook(stream);
        var sheets = new List<BulkSheet>();
        foreach (var sheet in workbook.Worksheets)
        {
            token.ThrowIfCancellationRequested();
            if (sheet.Visibility != XLWorksheetVisibility.Visible || sheet.Name == BulkFileWriter.ChoiceSheetName) continue;

            var used = sheet.RangeUsed();
            var records = new List<string[]>();
            if (used is not null)
            {
                var lastColumn = used.LastColumn().ColumnNumber();
                var lastRow = used.LastRow().RowNumber();
                for (var r = 1; r <= lastRow; r++)
                {
                    token.ThrowIfCancellationRequested();
                    var cells = new string[lastColumn];
                    for (var c = 1; c <= lastColumn; c++) cells[c - 1] = CellText(sheet.Cell(r, c));
                    records.Add(cells);
                    if (records.Count > BulkImportPlanner.MaxRows + 2 + ReadSlack) break;
                }
            }
            sheets.Add(ToSheet(sheet.Name, BulkColumnCatalog.CategoryOfSheet(sheet.Name), records,
                records.Count > BulkImportPlanner.MaxRows + 2 + ReadSlack));
        }
        return sheets;
    }

    /// <summary>칸 값 → 글. 숫자는 문화권 없는 표기(천 단위 쉼표 · 지수 없이), 참/거짓은 true/false.</summary>
    private static string CellText(IXLCell cell)
    {
        var value = cell.Value;
        return value.Type switch
        {
            XLDataType.Blank => string.Empty,
            XLDataType.Text => value.GetText(),
            XLDataType.Number => value.GetNumber().ToString("0.###############", CultureInfo.InvariantCulture),
            XLDataType.Boolean => value.GetBoolean() ? "true" : "false",
            XLDataType.DateTime => value.GetDateTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            _ => cell.GetFormattedString(),
        };
    }

    /// <summary>
    /// 쓸 시트 고르기 — 카테고리 이름 시트가 하나라도 있으면 그것들 전부(제어기 → 센서 같은 순서 의존, FR-14),
    /// 없으면 첫 시트를 지금 레일의 카테고리로 읽는다.
    /// </summary>
    public static IReadOnlyList<BulkSheet> SelectSheets(IReadOnlyList<BulkSheet> sheets, EnumDeviceCategory? railCategory)
    {
        var named = sheets.Where(s => s.Category is not null).ToList();
        if (named.Count > 0) return named;
        var first = sheets.FirstOrDefault();
        if (first is null) return Array.Empty<BulkSheet>();
        return new[]
        {
            new BulkSheet { Name = first.Name, Category = railCategory, Headers = first.Headers, Keys = first.Keys, Rows = first.Rows, IsTruncated = first.IsTruncated },
        };
    }
    #endregion

    /// <summary>날 줄들 → 머리 줄 · 키 줄 · 데이터 줄(빈 줄은 뺀다).</summary>
    private static BulkSheet ToSheet(string name, EnumDeviceCategory? category, IReadOnlyList<string[]> records, bool truncated)
    {
        if (records.Count == 0)
            return new BulkSheet { Name = name, Category = category, Headers = Array.Empty<string>(), Rows = Array.Empty<BulkSheetRow>() };

        var headers = records[0].Select(h => (h ?? string.Empty).Trim().TrimStart('﻿')).ToArray();
        var start = 1;
        string[]? keys = null;
        if (records.Count > 1 && IsKeyRow(records[1]))
        {
            keys = records[1].Select(k => (k ?? string.Empty).Trim().TrimStart('@').Trim()).ToArray();
            start = 2;
        }

        var rows = new List<BulkSheetRow>();
        for (var i = start; i < records.Count; i++)
        {
            var cells = records[i];
            if (cells.All(string.IsNullOrWhiteSpace)) continue;
            rows.Add(new BulkSheetRow(i + 1, cells.Select(c => c ?? string.Empty).ToArray()));
        }
        return new BulkSheet { Name = name, Category = category, Headers = headers, Keys = keys, Rows = rows, IsTruncated = truncated };
    }

    /// <summary>값이 있는 칸이 하나 이상이고 모두 <c>@</c> 로 시작하면 숨은 키 줄이다.</summary>
    private static bool IsKeyRow(string[] cells)
    {
        var filled = cells.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        return filled.Count > 0 && filled.All(c => c.Trim().StartsWith(BulkColumnCatalog.KeyPrefix, StringComparison.Ordinal));
    }
}

/// <summary>내보내기 · 양식 · 결과 파일 쓰기.</summary>
public static class BulkFileWriter
{
    /// <summary>양식의 선택값 목록 시트(숨김) — 올릴 때는 읽지 않는다.</summary>
    public const string ChoiceSheetName = "선택값";

    /// <summary>양식 유효성 목록을 거는 줄 수(머리 · 키 줄 다음부터).</summary>
    private const int ValidationRows = BulkImportPlanner.MaxRows;

    /// <summary>CSV — UTF-8 BOM · 쉼표 · 큰따옴표(필요한 칸만) · CRLF(FR-06).</summary>
    public static void WriteCsv(Stream stream, IReadOnlyList<string> headers, IReadOnlyList<string>? keys, IEnumerable<IReadOnlyList<string>> rows)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { NewLine = "\r\n", HasHeaderRecord = false };
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true);
        using var csv = new CsvWriter(writer, config);
        WriteRecord(csv, headers);
        if (keys is not null) WriteRecord(csv, keys.Select(k => BulkColumnCatalog.KeyPrefix + k).ToList());
        foreach (var row in rows) WriteRecord(csv, row);
        csv.Flush();
        writer.Flush();

        static void WriteRecord(CsvWriter csv, IReadOnlyList<string> cells)
        {
            foreach (var cell in cells) csv.WriteField(cell ?? string.Empty, shouldQuote: NeedsQuote(cell));
            csv.NextRecord();
        }
    }

    /// <summary>RFC 4180 — 쉼표 · 큰따옴표 · 줄바꿈 · 앞뒤 공백이 있는 칸만 감싼다.</summary>
    private static bool NeedsQuote(string? cell)
        => !string.IsNullOrEmpty(cell)
           && (cell.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 || cell[0] == ' ' || cell[^1] == ' ');

    /// <summary>한 시트의 내용 — 이름 · 머리 · 키 · 줄 · (양식이면) 칸별 선택값.</summary>
    public sealed record SheetContent(
        string Name,
        IReadOnlyList<string> Headers,
        IReadOnlyList<string> Keys,
        IReadOnlyList<IReadOnlyList<string>> Rows,
        IReadOnlyDictionary<int, IReadOnlyList<string>>? Choices = null);

    /// <summary>
    /// .xlsx — 1줄 머리(굵게) · 2줄 숨은 키 줄 · 칸은 글자 형식(IP · 앞자리 0 보존). 선택값이 있으면 숨은 <see cref="ChoiceSheetName"/>
    /// 시트에 목록을 두고 그 칸에 데이터 유효성 목록을 건다(FR-05).
    /// </summary>
    public static void WriteXlsx(Stream stream, IReadOnlyList<SheetContent> sheets)
    {
        using var workbook = new XLWorkbook();
        IXLWorksheet? choiceSheet = null;
        var choiceColumn = 0;

        foreach (var content in sheets)
        {
            var sheet = workbook.Worksheets.Add(SafeSheetName(content.Name));
            for (var c = 0; c < content.Headers.Count; c++)
            {
                var header = sheet.Cell(1, c + 1);
                header.Value = content.Headers[c];
                header.Style.Font.Bold = true;
                sheet.Cell(2, c + 1).Value = BulkColumnCatalog.KeyPrefix + content.Keys[c];
                sheet.Column(c + 1).Style.NumberFormat.Format = "@";
            }
            sheet.Row(2).Hide();

            for (var r = 0; r < content.Rows.Count; r++)
            {
                var row = content.Rows[r];
                for (var c = 0; c < row.Count && c < content.Headers.Count; c++)
                    sheet.Cell(r + 3, c + 1).SetValue(row[c] ?? string.Empty);
            }

            if (content.Choices is { Count: > 0 } choices)
            {
                choiceSheet ??= workbook.Worksheets.Add(ChoiceSheetName);
                foreach (var (column, values) in choices)
                {
                    if (values.Count == 0) continue;
                    choiceColumn++;
                    choiceSheet.Cell(1, choiceColumn).Value = content.Headers[column];
                    for (var i = 0; i < values.Count; i++) choiceSheet.Cell(i + 2, choiceColumn).SetValue(values[i]);
                    var list = choiceSheet.Range(2, choiceColumn, values.Count + 1, choiceColumn);
                    var target = sheet.Range(3, column + 1, ValidationRows + 2, column + 1);
                    var validation = target.CreateDataValidation();
                    validation.List(list, true);
                    validation.IgnoreBlanks = true;
                    validation.ErrorTitle = "고를 수 있는 값이 아닙니다";
                    validation.ErrorMessage = "목록에서 고르세요.";
                }
            }

            sheet.SheetView.FreezeRows(2);
            sheet.Columns(1, Math.Max(1, content.Headers.Count)).AdjustToContents(1, 3);
        }

        if (choiceSheet is not null) choiceSheet.Visibility = XLWorksheetVisibility.Hidden;
        workbook.SaveAs(stream);
    }

    /// <summary>엑셀 시트 이름 규칙 — 31자 · 금지 글자([]:*?/\) 없음.</summary>
    private static string SafeSheetName(string name)
    {
        var cleaned = new string((name ?? "장비").Where(ch => "[]:*?/\\".IndexOf(ch) < 0).ToArray()).Trim();
        if (cleaned.Length == 0) cleaned = "장비";
        return cleaned.Length > 31 ? cleaned[..31] : cleaned;
    }
}
