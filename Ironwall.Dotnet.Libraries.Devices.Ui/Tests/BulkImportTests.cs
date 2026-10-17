using Caliburn.Micro;
using ClosedXML.Excel;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.BulkImport;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 장비 대량 등록(device-bulk-import) 헤드리스 시험 — 창 없이 파일 왕복 · 판정 · 쓰기 · 미리보기 뷰모델을 본다.
/// </summary>
/// <remarks>쓰기기가 부대 관문(정적 IoC)을 밟으므로 IoC 를 만지는 다른 시험과 직렬화한다.</remarks>
[Collection("CaliburnIoC")]
public class BulkImportTests : IDisposable
{
    #region - Fixtures -
    // 행 뷰모델(패널 목록의 행과 같은 형식)은 만들 때 정적 IoC 에서 이벤트 집계기를 찾는다 — 앱처럼 하나 넣어 둔다.
    private readonly Func<Type, string, object> _getInstance = IoC.GetInstance;
    private readonly Func<Type, IEnumerable<object>> _getAllInstances = IoC.GetAllInstances;
    private IUnitScopeService? _unitScope;

    public BulkImportTests()
    {
        var aggregator = new EventAggregator();
        IoC.GetInstance = (type, key) => type == typeof(IEventAggregator) ? aggregator
                                       : type == typeof(IUnitScopeService) && _unitScope is not null ? _unitScope
                                       : null!;
        IoC.GetAllInstances = type => Enumerable.Empty<object>();
    }

    public void Dispose()
    {
        IoC.GetInstance = _getInstance;
        IoC.GetAllInstances = _getAllInstances;
    }

    private static ControllerDeviceModel Ctl(int id, string name, int number, string ip = "10.0.0.1", int port = 5000)
        => new() { Id = id, DeviceName = name, DeviceNumber = number, IpAddress = ip, Port = port, Status = EnumDeviceStatus.ACTIVATED };

    private static SensorDeviceModel Sensor(int id, string name, int number, ControllerDeviceModel controller)
        => new() { Id = id, DeviceName = name, DeviceNumber = number, Controller = controller };

    private static CameraDeviceModel Camera(int id, string name, int number, string ip, int port, string user = "admin", string password = "s3cret!")
        => new() { Id = id, DeviceName = name, DeviceNumber = number, IpAddress = ip, IpPort = port, UserName = user, UserPassword = password };

    private static BulkImportContext Context(EnumDeviceCategory rail, params IBaseDeviceModel[] devices)
        => new() { RailCategory = rail, Devices = devices };

    private static IReadOnlyList<BulkColumn> Columns(EnumDeviceCategory category) => BulkColumnCatalog.For(category, false, false);

    private static byte[] ExportCsv(EnumDeviceCategory category, params IBaseDeviceModel[] devices)
    {
        using var stream = new MemoryStream();
        var columns = Columns(category);
        BulkExporter.Write(stream, BulkFileFormat.Csv, category, columns, BulkExporter.Rows(columns, devices));
        return stream.ToArray();
    }

    private static byte[] ExportXlsx(EnumDeviceCategory category, params IBaseDeviceModel[] devices)
    {
        using var stream = new MemoryStream();
        var columns = Columns(category);
        BulkExporter.Write(stream, BulkFileFormat.Excel, category, columns, BulkExporter.Rows(columns, devices));
        return stream.ToArray();
    }

    private static BulkImportPlan PlanCsv(byte[] bytes, BulkImportContext context)
        => BulkImportPlanner.Plan(BulkFileReader.SelectSheets(new[] { BulkFileReader.ReadCsv(new MemoryStream(bytes), "file") }, context.RailCategory), context);

    private static BulkImportPlan PlanCsv(string text, BulkImportContext context)
        => PlanCsv(new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray(), context);

    private static BulkImportPlan PlanXlsx(byte[] bytes, BulkImportContext context)
        => BulkImportPlanner.Plan(BulkFileReader.SelectSheets(BulkFileReader.ReadXlsx(new MemoryStream(bytes)), context.RailCategory), context);

    /// <summary>제어기 CSV 머리(키 줄 없이 한국어 머리만) — 손으로 쓴 파일 흉내.</summary>
    private const string ControllerHeader = "ID,장비번호,이름,활성화,운영 상태,IP,포트\r\n";

    private static string Csv(byte[] bytes) => BulkFileReader.Decode(bytes);
    #endregion

    #region - 파일 왕복 · 인코딩 -
    [Fact, Trait("Scenario", "BLK-001")]
    public void should_judge_every_row_same_when_an_exported_csv_is_uploaded_back()
    {
        var devices = new IBaseDeviceModel[] { Ctl(1, "제어기 가", 1), Ctl(2, "제어기 나", 2, "10.0.0.2", 5001) };

        var plan = PlanCsv(ExportCsv(EnumDeviceCategory.Controller, devices), Context(EnumDeviceCategory.Controller, devices));

        Assert.Null(plan.FileError);
        Assert.Equal(2, plan.Rows.Count);
        Assert.All(plan.Rows, r => Assert.Equal(BulkRowVerdict.Same, r.Verdict));
        Assert.Equal("10.0.0.2", plan.Rows[1].CellOf(BulkColumnCatalog.IpKey)!.Raw);
    }

    [Fact, Trait("Scenario", "BLK-002")]
    public void should_keep_the_same_values_when_an_exported_xlsx_is_uploaded_back()
    {
        var devices = new IBaseDeviceModel[] { Ctl(1, "제어기 가", 1), Ctl(2, "제어기 나", 2, "10.0.0.2", 5001) };
        var bytes = ExportXlsx(EnumDeviceCategory.Controller, devices);

        var plan = PlanXlsx(bytes, Context(EnumDeviceCategory.Sensor, devices));   // 레일이 달라도 시트 이름(제어기)이 정한다

        Assert.All(plan.Rows, r => Assert.Equal(EnumDeviceCategory.Controller, r.Category));
        Assert.All(plan.Rows, r => Assert.Equal(BulkRowVerdict.Same, r.Verdict));
        Assert.Equal(new[] { "제어기 가", "제어기 나" }, plan.Rows.Select(r => r.Name));
        Assert.Equal("5001", plan.Rows[1].CellOf(BulkColumnCatalog.PortKey)!.Raw);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("제어기");
        Assert.True(sheet.Row(2).IsHidden);                        // 숨은 키 줄
        Assert.Equal("@" + BulkColumnCatalog.IdKey, sheet.Cell(2, 1).GetString());
    }

    [Theory, Trait("Scenario", "BLK-003")]
    [InlineData("utf8")]
    [InlineData("utf8bom")]
    [InlineData("cp949")]
    public void should_read_korean_names_when_the_csv_encoding_is(string encoding)
    {
        var text = ControllerHeader + ",7,북문 제어기,true,ACTIVATED,10.1.1.7,5000\r\n";
        var bytes = encoding switch
        {
            "utf8" => Encoding.UTF8.GetBytes(text),
            "utf8bom" => new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray(),
            _ => BulkFileReader.Cp949().GetBytes(text),
        };

        var plan = PlanCsv(bytes, Context(EnumDeviceCategory.Controller));

        Assert.Equal("북문 제어기", Assert.Single(plan.Rows).Name);
        Assert.Equal(BulkRowVerdict.New, plan.Rows[0].Verdict);
    }

    [Fact, Trait("Scenario", "BLK-004")]
    public void should_keep_commas_quotes_and_line_breaks_when_a_name_has_them()
    {
        var tricky = "A, \"B\"\r\n둘째 줄";
        var devices = new IBaseDeviceModel[] { Ctl(1, tricky, 1) };
        var bytes = ExportCsv(EnumDeviceCategory.Controller, devices);

        var plan = PlanCsv(bytes, Context(EnumDeviceCategory.Controller, devices));

        Assert.Equal(tricky, Assert.Single(plan.Rows).Name);
        Assert.Equal(BulkRowVerdict.Same, plan.Rows[0].Verdict);
        Assert.True(bytes.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));   // UTF-8 BOM(엑셀 한글)
        Assert.Contains("\"A, \"\"B\"\"", Csv(bytes));                                // 큰따옴표 두 번 규칙
    }

    [Fact, Trait("Scenario", "BLK-005")]
    public void should_map_by_korean_header_and_ignore_unknown_columns_when_the_order_is_shuffled()
    {
        var text = "포트,메모,IP,이름,장비번호\r\n5000,아무 말,10.2.2.2,새 제어기,9\r\n";

        var plan = PlanCsv(text, Context(EnumDeviceCategory.Controller));

        var row = Assert.Single(plan.Rows);
        Assert.Equal("10.2.2.2", row.CellOf(BulkColumnCatalog.IpKey)!.Value);
        Assert.Equal("5000", row.CellOf(BulkColumnCatalog.PortKey)!.Value);
        Assert.Equal(BulkRowVerdict.New, row.Verdict);
        Assert.Contains("메모", plan.IgnoredColumns);
    }

    [Fact, Trait("Scenario", "BLK-006")]
    public void should_skip_blank_lines_and_the_example_row_when_reading_a_template()
    {
        var columns = Columns(EnumDeviceCategory.Controller);
        using var stream = new MemoryStream();
        BulkExporter.Write(stream, BulkFileFormat.Csv, EnumDeviceCategory.Controller, columns,
            new[] { BulkExporter.ExampleRow(columns, EnumDeviceCategory.Controller, null) });
        var text = Csv(stream.ToArray()) + "\r\n,,,\r\n" + string.Join(",", columns.Select(c => c.Key switch
        {
            BulkColumnCatalog.NumberKey => "3",
            BulkColumnCatalog.NameKey => "진짜 제어기",
            BulkColumnCatalog.IpKey => "10.3.3.3",
            BulkColumnCatalog.PortKey => "5000",
            _ => string.Empty,
        })) + "\r\n\r\n";

        var plan = PlanCsv(text, Context(EnumDeviceCategory.Controller));

        var row = Assert.Single(plan.Rows);
        Assert.Equal("진짜 제어기", row.Name);
        Assert.Equal(BulkRowVerdict.New, row.Verdict);
    }
    #endregion

    #region - 판정 -
    [Fact, Trait("Scenario", "BLK-007")]
    public void should_mark_each_bad_cell_with_a_reason_when_values_are_invalid()
    {
        var text = ControllerHeader
                   + ",x,,true,ACTIVATED,10.0.0.300,5000\r\n"     // 번호 숫자 아님 · 이름 빔 · IP 형식
                   + ",4,포트 틀림,true,ACTIVATED,10.0.0.4,70000\r\n"
                   + ",5,상태 틀림,아마도,BAD,10.0.0.5,5000\r\n";

        var plan = PlanCsv(text, Context(EnumDeviceCategory.Controller));

        Assert.All(plan.Rows, r => Assert.Equal(BulkRowVerdict.Error, r.Verdict));
        var first = plan.Rows[0];
        Assert.NotNull(first.CellOf(BulkColumnCatalog.NumberKey)!.Error);
        Assert.Equal("필수 칸이 비었습니다.", first.CellOf(BulkColumnCatalog.NameKey)!.Error);
        Assert.Contains("IP 형식", first.CellOf(BulkColumnCatalog.IpKey)!.Error);
        Assert.Contains("65535", plan.Rows[1].CellOf(BulkColumnCatalog.PortKey)!.Error);
        Assert.NotNull(plan.Rows[2].CellOf("is_enable")!.Error);
        Assert.Contains("고를 수 있는 값이 아닙니다", plan.Rows[2].CellOf("status")!.Error);
    }

    [Fact, Trait("Scenario", "BLK-007")]
    public void should_accept_korean_display_names_when_a_choice_cell_uses_them()
    {
        var plan = PlanCsv(ControllerHeader + ",6,한글 값,예,운영,10.0.0.6,5000\r\n", Context(EnumDeviceCategory.Controller));

        var row = Assert.Single(plan.Rows);
        Assert.Equal(BulkRowVerdict.New, row.Verdict);
        Assert.Equal("true", row.CellOf("is_enable")!.Value);
        Assert.Equal("ACTIVATED", row.CellOf("status")!.Value);
    }

    [Fact, Trait("Scenario", "BLK-008")]
    public void should_match_by_id_first_then_by_name_when_judging_existing_devices()
    {
        var a = Ctl(11, "갑", 1);
        var b = Ctl(12, "을", 2, "10.0.0.2");
        var text = ControllerHeader
                   + "11,1,갑 새이름,,,,\r\n"      // ID 로 대조 → 이름이 바뀐 수정
                   + ",2,을,,,10.0.0.22,\r\n"     // 이름으로 대조 → IP 수정
                   + "99,3,없는 장비,,,,\r\n";    // 없는 ID → 오류

        var plan = PlanCsv(text, Context(EnumDeviceCategory.Controller, a, b));

        Assert.Same(a, plan.Rows[0].Existing);
        Assert.Equal(BulkRowVerdict.Update, plan.Rows[0].Verdict);
        Assert.True(plan.Rows[0].CellOf(BulkColumnCatalog.NameKey)!.IsChanged);
        Assert.False(plan.Rows[0].CellOf(BulkColumnCatalog.NumberKey)!.IsChanged);

        Assert.Same(b, plan.Rows[1].Existing);
        Assert.Equal(BulkRowVerdict.Update, plan.Rows[1].Verdict);
        Assert.Equal(new[] { BulkColumnCatalog.IpKey }, plan.Rows[1].Cells.Where(c => c.IsChanged).Select(c => c.Column.Key));

        Assert.Equal(BulkRowVerdict.Error, plan.Rows[2].Verdict);
        Assert.Contains("ID 99", plan.Rows[2].CellOf(BulkColumnCatalog.IdKey)!.Error);
    }

    [Fact, Trait("Scenario", "BLK-009")]
    public void should_flag_duplicates_in_the_file_and_conflicts_with_the_server_when_keys_collide()
    {
        var server = Ctl(21, "기존", 50, "10.9.9.9", 5000);
        var text = ControllerHeader
                   + ",1,겹침,,,10.0.0.1,5000\r\n"
                   + ",2,겹침,,,10.0.0.2,5000\r\n"     // 이름 중복(파일 안)
                   + ",50,번호 충돌,,,10.0.0.3,5000\r\n"   // 서버 번호 충돌
                   + ",51,주소 충돌,,,10.9.9.9,5000\r\n";  // 서버 IP:포트 충돌

        var plan = PlanCsv(text, Context(EnumDeviceCategory.Controller, server));

        Assert.Contains(plan.Rows[0].RowErrors, e => e.Contains("3줄") && e.Contains("이름"));
        Assert.Contains(plan.Rows[1].RowErrors, e => e.Contains("2줄"));
        Assert.Contains(plan.Rows[2].RowErrors, e => e.Contains("장비번호 50"));
        Assert.Contains(plan.Rows[3].RowErrors, e => e.Contains("10.9.9.9:5000"));
        Assert.All(plan.Rows, r => Assert.Equal(BulkRowVerdict.Error, r.Verdict));
    }

    [Fact, Trait("Scenario", "BLK-010")]
    public void should_find_the_parent_controller_by_name_ip_or_number_when_sensors_are_uploaded()
    {
        var c1 = Ctl(31, "동문 제어기", 7, "10.7.7.7", 5000);
        var header = "장비번호,이름,소속 제어기\r\n";
        var text = header + "1,센서 A,동문 제어기\r\n2,센서 B,10.7.7.7\r\n3,센서 C,#7\r\n4,센서 D,없는 제어기\r\n5,센서 E,\r\n";

        var plan = PlanCsv(text, Context(EnumDeviceCategory.Sensor, c1));

        Assert.All(plan.Rows.Take(3), r => Assert.Same(c1, r.Controller));
        Assert.All(plan.Rows.Take(3), r => Assert.Equal(BulkRowVerdict.New, r.Verdict));
        Assert.Contains("찾을 수 없습니다", plan.Rows[3].CellOf(BulkColumnCatalog.ControllerKey)!.Error);
        Assert.Equal("필수 칸이 비었습니다.", plan.Rows[4].CellOf(BulkColumnCatalog.ControllerKey)!.Error);
    }

    [Fact, Trait("Scenario", "BLK-011")]
    public void should_refuse_the_whole_file_when_it_has_more_than_the_row_limit()
    {
        var builder = new StringBuilder(ControllerHeader);
        for (var i = 1; i <= BulkImportPlanner.MaxRows + 1; i++) builder.Append($",{i},제어기 {i},,,,\r\n");

        var plan = PlanCsv(builder.ToString(), Context(EnumDeviceCategory.Controller));

        Assert.Contains("2,000", plan.FileError);
        Assert.Empty(plan.Rows);
    }

    [Fact, Trait("Scenario", "BLK-012")]
    public void should_mark_every_change_as_an_error_when_the_operator_cannot_edit_devices()
    {
        var context = new BulkImportContext { RailCategory = EnumDeviceCategory.Controller, CanEdit = false };

        var plan = PlanCsv(ControllerHeader + ",1,새것,,,10.0.0.1,5000\r\n", context);

        Assert.Equal(BulkRowVerdict.Error, Assert.Single(plan.Rows).Verdict);
        Assert.Contains(plan.Rows[0].RowErrors, e => e.Contains("권한"));
    }
    #endregion

    #region - 비밀번호 -
    [Fact, Trait("Scenario", "BLK-013")]
    public void should_never_write_the_password_when_cameras_are_exported()
    {
        var camera = Camera(41, "정문 카메라", 1, "10.4.4.4", 80, "admin", "s3cret!");

        var csv = Csv(ExportCsv(EnumDeviceCategory.Camera, camera));
        string xlsxText;
        using (var workbook = new XLWorkbook(new MemoryStream(ExportXlsx(EnumDeviceCategory.Camera, camera))))
            xlsxText = string.Join("|", workbook.Worksheets.SelectMany(s => s.CellsUsed()).Select(c => c.GetString()));

        Assert.DoesNotContain("s3cret!", csv);
        Assert.DoesNotContain("s3cret!", xlsxText);
        Assert.Contains("admin", csv);   // 사용자명은 나간다
    }

    [Fact, Trait("Scenario", "BLK-013")]
    public async Task should_send_the_uploaded_password_only_in_the_body_when_previews_and_results_hide_it()
    {
        var text = "장비번호,이름,IP,포트,접속 사용자명,비밀번호\r\n1,새 카메라,10.4.4.5,80,admin,Pa55!word\r\n";
        var plan = PlanCsv(text, Context(EnumDeviceCategory.Camera));
        var row = Assert.Single(plan.Rows);
        var api = new FakeApi();
        var writer = new BulkImportWriter(api, null, () => Array.Empty<IBaseDeviceModel>());

        var outcomes = await writer.WriteAsync(plan.Rows);

        Assert.Equal(BulkCell.MaskedSecret, row.CellOf("connection.user_password")!.Display);
        Assert.Equal("Pa55!word", ((CameraDeviceDto)api.Calls.Single().Dto).UserPassword);
        var vm = new BulkImportRowViewModel(row) { Outcome = outcomes.Single() };
        Assert.DoesNotContain(vm.Cells, c => c.Display.Contains("Pa55"));
        using var result = new MemoryStream();
        BulkImportWriter.WriteResultCsv(result, new[] { (row, "완료", "", vm.ReasonText) });
        Assert.DoesNotContain("Pa55", Csv(result.ToArray()));
    }
    #endregion

    #region - 쓰기 -
    [Fact, Trait("Scenario", "BLK-014")]
    public async Task should_create_the_controller_first_and_attach_the_sensor_to_it_when_both_are_in_one_workbook()
    {
        using var stream = new MemoryStream();
        BulkFileWriter.WriteXlsx(stream, new[]
        {
            // 센서 시트를 앞에 둬도 쓰기 순서는 제어기 먼저다.
            Sheet(EnumDeviceCategory.Sensor, new[] { BulkColumnCatalog.NumberKey, BulkColumnCatalog.NameKey, BulkColumnCatalog.ControllerKey }, new[] { "1", "새 센서", "새 제어기" }),
            Sheet(EnumDeviceCategory.Controller, new[] { BulkColumnCatalog.NumberKey, BulkColumnCatalog.NameKey, BulkColumnCatalog.IpKey, BulkColumnCatalog.PortKey }, new[] { "9", "새 제어기", "10.5.5.5", "5000" }),
        });
        var plan = PlanXlsx(stream.ToArray(), Context(EnumDeviceCategory.Sensor));
        Assert.All(plan.Rows, r => Assert.Equal(BulkRowVerdict.New, r.Verdict));
        Assert.Equal("새 제어기", plan.Rows.Single(r => r.Category == EnumDeviceCategory.Sensor).PendingControllerName);

        var api = new FakeApi { NextId = 77 };
        var provider = new FakeProvider();
        var outcomes = await new BulkImportWriter(api, provider, () => Array.Empty<IBaseDeviceModel>()).WriteAsync(plan.Rows);

        Assert.Equal(new[] { "CreateController", "CreateSensor" }, api.Calls.Select(c => c.Method));
        Assert.Equal(77, ((SensorDeviceDto)api.Calls[1].Dto).ControllerId);
        Assert.All(outcomes, o => Assert.Equal(BulkWriteStatus.Succeeded, o.Status));
        Assert.True(provider.FetchCount >= 1);
    }

    [Fact, Trait("Scenario", "BLK-014")]
    public async Task should_fail_the_sensor_when_its_new_controller_could_not_be_created()
    {
        using var stream = new MemoryStream();
        BulkFileWriter.WriteXlsx(stream, new[]
        {
            Sheet(EnumDeviceCategory.Controller, new[] { BulkColumnCatalog.NumberKey, BulkColumnCatalog.NameKey, BulkColumnCatalog.IpKey, BulkColumnCatalog.PortKey }, new[] { "9", "새 제어기", "10.5.5.5", "5000" }),
            Sheet(EnumDeviceCategory.Sensor, new[] { BulkColumnCatalog.NumberKey, BulkColumnCatalog.NameKey, BulkColumnCatalog.ControllerKey }, new[] { "1", "새 센서", "새 제어기" }),
        });
        var plan = PlanXlsx(stream.ToArray(), Context(EnumDeviceCategory.Controller));
        var api = new FakeApi { Fail = dto => dto.NameDevice == "새 제어기" ? 422 : null };

        var outcomes = await new BulkImportWriter(api, null, () => Array.Empty<IBaseDeviceModel>()).WriteAsync(plan.Rows);

        Assert.Single(api.Calls);   // 센서는 보내지 않았다
        var sensor = outcomes.Single(o => o.Row.Category == EnumDeviceCategory.Sensor);
        Assert.Equal(BulkFailureKind.Conflict, sensor.Kind);
        Assert.Contains("새 제어기", sensor.Message);
    }

    [Fact, Trait("Scenario", "BLK-015")]
    public async Task should_sort_failures_into_four_kinds_and_retry_only_the_failed_rows()
    {
        var text = ControllerHeader
                   + ",1,성공,,,10.0.0.1,5000\r\n,2,거부,,,10.0.0.2,5000\r\n,3,권한,,,10.0.0.3,5000\r\n"
                   + ",4,충돌,,,10.0.0.4,5000\r\n,5,연결,,,10.0.0.5,5000\r\n";
        var plan = PlanCsv(text, Context(EnumDeviceCategory.Controller));
        var codes = new Dictionary<string, int> { ["거부"] = 422, ["권한"] = 403, ["충돌"] = 409, ["연결"] = 503 };
        var api = new FakeApi { Fail = dto => codes.TryGetValue(dto.NameDevice ?? "", out var code) ? code : null };
        var vm = new BulkImportViewModel(() => Context(EnumDeviceCategory.Controller), () => new BulkImportWriter(api, null, () => Array.Empty<IBaseDeviceModel>()));
        var path = TempFile(".csv", new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray());
        try
        {
            await vm.OpenFileAsync(path);
            Assert.True(vm.CanRegister);
            await vm.RegisterAsync();

            var kinds = vm.Rows.ToDictionary(r => r.Name, r => r.Outcome!.Kind);
            Assert.Null(kinds["성공"]);
            Assert.Equal(BulkFailureKind.Rejected, kinds["거부"]);
            Assert.Equal(BulkFailureKind.Permission, kinds["권한"]);
            Assert.Equal(BulkFailureKind.Conflict, kinds["충돌"]);
            Assert.Equal(BulkFailureKind.Connection, kinds["연결"]);
            Assert.Contains("실패 4", vm.ResultSummary());
            Assert.False(vm.CanRegister);   // 한 번 보낸 뒤에는 [실패 줄 다시] 만

            codes.Clear();
            api.Calls.Clear();
            await vm.RetryFailedAsync();

            Assert.Equal(4, api.Calls.Count);
            Assert.DoesNotContain(api.Calls, c => c.Dto.NameDevice == "성공");
            Assert.All(vm.Rows, r => Assert.True(r.IsSucceeded));
        }
        finally { File.Delete(path); }
    }

    [Fact, Trait("Scenario", "BLK-016")]
    public async Task should_update_with_the_existing_id_and_drop_unit_id_when_the_server_is_6_3()
    {
        var existing = Ctl(61, "기존 제어기", 3);
        existing.UnitId = 9;
        var plan = PlanCsv(ControllerHeader + "61,3,기존 제어기,,,10.6.6.6,\r\n", Context(EnumDeviceCategory.Controller, existing));
        var api = new FakeApi();

        await new BulkImportWriter(api, null, () => new IBaseDeviceModel[] { existing }).WriteAsync(plan.Rows);

        var call = Assert.Single(api.Calls);
        Assert.Equal("UpdateController", call.Method);
        Assert.Equal(61, call.Id);
        Assert.Equal("10.6.6.6", ((ControllerDeviceDto)call.Dto).IpAddress);
        Assert.Equal(5000, ((ControllerDeviceDto)call.Dto).IpPort);   // 빈 칸 = 바꾸지 않음
        Assert.Null(call.Dto.UnitId);                                  // 8.0 미만 — 관문이 지운다
        Assert.Equal("10.0.0.1", existing.IpAddress);                  // 캐시 모델은 고치지 않는다(사본에 썼다)
    }

    [Fact, Trait("Scenario", "BLK-016")]
    public async Task should_send_the_unit_from_the_file_and_stamp_new_rows_when_the_server_is_8_0()
    {
        var existing = Ctl(62, "부대 바꿀 제어기", 4);
        existing.UnitId = 1;
        var context = new BulkImportContext
        {
            RailCategory = EnumDeviceCategory.Controller, IsUnitEra = true, IsAxisContract = true,
            Devices = new IBaseDeviceModel[] { existing }, Units = new[] { (5, "1대대") },
        };
        var text = "ID,장비번호,이름,종류,소속 부대,IP,포트\r\n62,4,부대 바꿀 제어기,,1대대,10.0.0.1,5000\r\n,5,새 제어기,Controller,,10.0.0.9,5000\r\n";
        var plan = PlanCsv(text, context);
        Assert.Equal(BulkRowVerdict.Update, plan.Rows[0].Verdict);
        var api = new FakeApi();

        _unitScope = new UnitEra(unitId: 3);
        await new BulkImportWriter(api, null, () => new IBaseDeviceModel[] { existing }).WriteAsync(plan.Rows);

        Assert.Equal(5, api.Calls.Single(c => c.Method == "UpdateController").Dto.UnitId);   // 파일의 부대(이름 → ID)
        Assert.Equal(3, api.Calls.Single(c => c.Method == "CreateController").Dto.UnitId);   // 새 장비 = 이 단말 부대
    }

    [Fact, Trait("Scenario", "BLK-017")]
    public async Task should_send_nothing_for_same_rows_and_mark_the_rest_cancelled_when_cancelled()
    {
        var existing = Ctl(71, "같은 것", 1);
        var plan = PlanCsv(ControllerHeader + "71,1,같은 것,,,,\r\n,2,새것 1,,,10.0.0.2,5000\r\n,3,새것 2,,,10.0.0.3,5000\r\n",
            Context(EnumDeviceCategory.Controller, existing));
        Assert.Equal(BulkRowVerdict.Same, plan.Rows[0].Verdict);
        var api = new FakeApi();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var outcomes = await new BulkImportWriter(api, null, () => Array.Empty<IBaseDeviceModel>()).WriteAsync(plan.Rows, token: cts.Token);

        Assert.Empty(api.Calls);
        Assert.Equal(2, outcomes.Count);   // 같음 줄은 보낼 대상이 아니다
        Assert.All(outcomes, o => Assert.Equal(BulkWriteStatus.Cancelled, o.Status));
    }
    #endregion

    #region - 미리보기 뷰모델 · 양식 -
    [Fact, Trait("Scenario", "BLK-018")]
    public async Task should_enable_register_only_when_errors_are_zero_or_excluded()
    {
        var text = ControllerHeader + ",1,좋은 줄,,,10.0.0.1,5000\r\n,x,,,,,\r\n";
        var api = new FakeApi();
        var vm = new BulkImportViewModel(() => Context(EnumDeviceCategory.Controller), () => new BulkImportWriter(api, null, () => Array.Empty<IBaseDeviceModel>()));
        var written = 0;
        vm.Written += (_, n) => written = n;
        var path = TempFile(".csv", Encoding.UTF8.GetBytes(text));
        try
        {
            await vm.OpenFileAsync(path);

            Assert.True(vm.IsOpen);
            Assert.Equal(1, vm.ErrorCount);
            Assert.False(vm.CanRegister);
            Assert.Contains("오류 1줄", vm.RegisterBlockedReason);
            Assert.Equal("신규 1 · 수정 0 · 같음 0 · 오류 1", vm.SummaryText);

            vm.ExcludeErrorRows = true;
            Assert.True(vm.CanRegister);
            await vm.RegisterAsync();

            Assert.Single(api.Calls);
            Assert.Equal(1, written);
            Assert.True(vm.CanSaveResult);
            using var result = new MemoryStream();
            vm.SaveResult(result);
            var csv = Csv(result.ToArray());
            Assert.Contains("좋은 줄", csv);
            Assert.Contains("보내지 않음", csv);
        }
        finally { File.Delete(path); }
    }

    [Fact, Trait("Scenario", "BLK-018")]
    public async Task should_show_a_plain_reason_and_no_rows_when_the_file_is_not_a_device_list()
    {
        var vm = new BulkImportViewModel(() => Context(EnumDeviceCategory.Controller), () => new BulkImportWriter(new FakeApi(), null, () => Array.Empty<IBaseDeviceModel>()));
        var path = TempFile(".csv", Encoding.UTF8.GetBytes("가,나,다\r\n1,2,3\r\n"));
        try
        {
            await vm.OpenFileAsync(path);

            Assert.Empty(vm.Rows);
            Assert.Contains("이름 칸", vm.StatusText);
            Assert.False(vm.CanRegister);
        }
        finally { File.Delete(path); }
    }

    [Fact, Trait("Scenario", "BLK-019")]
    public void should_put_a_choice_list_on_the_type_cells_when_an_excel_template_is_made()
    {
        var columns = Columns(EnumDeviceCategory.Controller);
        using var stream = new MemoryStream();
        BulkExporter.Write(stream, BulkFileFormat.Excel, EnumDeviceCategory.Controller, columns,
            new[] { BulkExporter.ExampleRow(columns, EnumDeviceCategory.Controller, null) },
            BulkExporter.Choices(columns, EnumDeviceCategory.Controller, null));

        using var workbook = new XLWorkbook(new MemoryStream(stream.ToArray()));
        var sheet = workbook.Worksheet("제어기");
        var choices = workbook.Worksheet(BulkFileWriter.ChoiceSheetName);
        Assert.Equal(XLWorksheetVisibility.Hidden, choices.Visibility);
        Assert.NotEmpty(sheet.DataValidations);
        Assert.Contains(choices.CellsUsed(), c => c.GetString() == "ACTIVATED");
        Assert.Equal(BulkColumnCatalog.ExampleMarker, sheet.Cell(3, 1).GetString());
        Assert.Empty(PlanXlsx(stream.ToArray(), Context(EnumDeviceCategory.Controller)).Rows);   // 예시 줄은 올라가지 않는다
    }

    [Fact, Trait("Scenario", "BLK-020")]
    public void should_list_id_first_and_keep_secrets_and_read_only_fields_out_when_columns_are_built()
    {
        var camera = BulkColumnCatalog.For(EnumDeviceCategory.Camera, isAxisContract: true, isUnitEra: true);
        var sensor = BulkColumnCatalog.For(EnumDeviceCategory.Sensor, isAxisContract: false, isUnitEra: false);

        Assert.Equal(BulkColumnCatalog.IdKey, camera[0].Key);
        Assert.Contains(camera, c => c.Key == "type_axis");
        Assert.Contains(camera, c => c.Kind == BulkColumnKind.Unit);
        Assert.Single(camera, c => c.IsSecret);
        Assert.DoesNotContain(camera, c => c.Key is "category_device" or "components" or "group_ids" or "hardware_spec.model");
        Assert.Contains(sensor, c => c.Kind == BulkColumnKind.Controller && c.IsRequiredOnCreate);
        Assert.DoesNotContain(sensor, c => c.Kind == BulkColumnKind.Unit);   // 8.0 미만에는 부대 칸이 없다
    }
    #endregion

    #region - Doubles -
    private static BulkFileWriter.SheetContent Sheet(EnumDeviceCategory category, string[] keys, string[] values)
    {
        var columns = Columns(category);
        var headers = keys.Select(k => columns.Single(c => c.Key == k).Header).ToList();
        return new BulkFileWriter.SheetContent(BulkColumnCatalog.CategoryLabel(category), headers, keys, new[] { (IReadOnlyList<string>)values });
    }

    private static string TempFile(string extension, byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"bulk-import-test-{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>생성 · 수정만 다시 구현한 가짜 API — 받은 DTO 원본을 그대로 쌓는다.</summary>
    private sealed class FakeApi : MockDeviceApiService, Ironwall.Dotnet.Libraries.Devices.Api.Services.IDeviceApiService
    {
        public List<(string Method, int Id, BaseDeviceDto Dto)> Calls { get; } = new();

        /// <summary>실패시킬 상태 코드(이름별). null 이면 성공.</summary>
        public Func<BaseDeviceDto, int?> Fail { get; set; } = _ => null;

        public int NextId { get; set; } = 1000;

        private Task<ApiResponse<T>> Handle<T>(string method, int id, T dto) where T : BaseDeviceDto
        {
            lock (Calls) Calls.Add((method, id, dto));
            if (Fail(dto) is { } status)
            {
                var error = ApiResponse<T>.CreateError("FAILED", $"실패 {status}");
                error.StatusCode = status;
                return Task.FromResult(error);
            }
            if (id == 0) dto.Id = NextId++;
            var ok = ApiResponse<T>.CreateSuccess(dto);
            ok.StatusCode = id == 0 ? 201 : 200;
            return Task.FromResult(ok);
        }

        public new Task<ApiResponse<ControllerDeviceDto>> CreateControllerAsync(ControllerDeviceDto dto, CancellationToken token = default) => Handle("CreateController", 0, dto);
        public new Task<ApiResponse<ControllerDeviceDto>> UpdateControllerAsync(int id, ControllerDeviceDto dto, CancellationToken token = default) => Handle("UpdateController", id, dto);
        public new Task<ApiResponse<SensorDeviceDto>> CreateSensorAsync(SensorDeviceDto dto, CancellationToken token = default) => Handle("CreateSensor", 0, dto);
        public new Task<ApiResponse<SensorDeviceDto>> UpdateSensorAsync(int id, SensorDeviceDto dto, CancellationToken token = default) => Handle("UpdateSensor", id, dto);
        public new Task<ApiResponse<CameraDeviceDto>> CreateCameraAsync(CameraDeviceDto dto, CancellationToken token = default) => Handle("CreateCamera", 0, dto);
        public new Task<ApiResponse<CameraDeviceDto>> UpdateCameraAsync(int id, CameraDeviceDto dto, CancellationToken token = default) => Handle("UpdateCamera", id, dto);
    }

    private sealed class FakeProvider : MockDeviceProviderService, IDeviceProviderService
    {
        public int FetchCount { get; private set; }

        public new Task FetchAllDevicesAsync(CancellationToken token = default)
        {
            FetchCount++;
            return Task.CompletedTask;
        }
    }

    /// <summary>8.0 부대 서비스 — 부대 관문(정적 IoC)이 이 단말의 부대로 찾는다.</summary>
    private sealed class UnitEra : IUnitScopeService
    {
        private readonly int _unitId;
        public UnitEra(int unitId) { _unitId = unitId; }
        public bool IsUnitEra => true;
        public string? UnitCode => "unit001";
        public int? CurrentUnitId => _unitId;
        public bool IsResolved => true;
        public Task<int?> ResolveAsync(CancellationToken token = default) => Task.FromResult<int?>(_unitId);
        public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;
    }
    #endregion
}
