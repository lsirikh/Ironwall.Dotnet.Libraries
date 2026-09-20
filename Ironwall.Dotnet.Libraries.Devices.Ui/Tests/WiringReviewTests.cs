using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 적대 검토(2026-09-20)에서 나온 것들 — 고친 자리마다 이름 있는 시험 하나.
/// </summary>
/// <remarks>
/// 서버 계약은 <b>읽어서</b> 확정했다: 장비 PATCH 는 축을 RFC 7396 으로 <b>병합</b>한다
/// (<c>api-test-server/app/routers/sensors.py:241-261</c> → <c>app/services/device_axes_io.py:291-299</c> →
/// <c>app/services/json_merge.py:25-37</c>). 그래서 ① 안 보낸 키는 그대로 남고 ② 키를 빼는 것으로는 지워지지 않는다.
/// </remarks>
public class WiringReviewTests
{
    private static DragPayload Payload(params object[] items) => new(null!, items, "test");

    #region - C2 · 본문 감사(제품이 쓰는 길로) -
    /// <summary>
    /// 결선만 바꾼 PATCH — <b>제품과 같은 길</b>(진짜 <see cref="Ironwall.Dotnet.Libraries.Devices.Api.Services.DeviceApiService"/>)로
    /// 나간 본문에 null 이 없고, 우리가 건드리지 않은 칸은 아예 실리지 않는다(병합이라 그것이 "그대로 둔다" 다).
    /// </summary>
    [Fact]
    public async Task should_send_a_body_with_no_nulls_when_only_the_wiring_changed()
    {
        var body = await PatchBodyAsync(board =>
        {
            board.Unplace(board.Rows[1].Key);
            board.Place(board.Rows[1].Key, 2, 0);
        });

        AssertNoNulls(body);
        Assert.Equal(2, (int?)body.SelectToken("hardware_spec.spec.wiring.line"));
        Assert.Equal(1, (int?)body.SelectToken("hardware_spec.spec.wiring.order"));

        // 표 칸은 받은 값 그대로(비우면 PATCH 가 지운다)
        Assert.Equal(1102, (int?)body["number_device"]);
        Assert.Equal("북측 2구간 펜스", (string?)body["name_device"]);
        Assert.Equal("ACTIVATED", (string?)body["status"]);
        Assert.Equal(true, (bool?)body["is_enable"]);

        // 우리가 건드리지 않은 축은 본문에 없다 — 병합이라 서버 값이 그대로 남는다
        Assert.Null(body["geolocation"]);
        Assert.Null(body["group_ids"]);
        Assert.Null(body.SelectToken("hardware_spec.components"));
        Assert.Null(body.SelectToken("hardware_spec.manufacturer"));
        Assert.Null(body["type_sensor"]);
    }

    /// <summary>표 값만 바꾼 PATCH — 결선 축은 아예 나가지 않는다.</summary>
    [Fact]
    public async Task should_send_a_body_with_no_nulls_when_only_the_table_changed()
    {
        var body = await PatchBodyAsync(board =>
        {
            var row = board.Rows[1];
            row.Facts = row.Facts with { Name = "고친 이름", TypeText = "PIR", Zone = "정문 초소" };
        });

        AssertNoNulls(body);
        Assert.Equal("고친 이름", (string?)body["name_device"]);
        Assert.Equal("PIR", (string?)body["type_sensor"]);
        Assert.Equal("정문 초소", (string?)body.SelectToken("geolocation.location"));
        Assert.Equal(37.5, (double?)body.SelectToken("geolocation.latitude"));      // 좌표는 받은 값 그대로
        Assert.Equal(127.5, (double?)body.SelectToken("geolocation.longitude"));
        Assert.Null(body["hardware_spec"]);
    }

    /// <summary>둘 다 바꾼 PATCH — 호출은 한 번이고 본문에 둘 다 들어 있다.</summary>
    [Fact]
    public async Task should_send_one_body_with_both_when_table_and_wiring_changed()
    {
        var body = await PatchBodyAsync(board =>
        {
            var row = board.Rows[1];
            row.Facts = row.Facts with { Name = "고친 이름" };
            board.Unplace(row.Key);
            board.Place(row.Key, 2, 2);
        });

        AssertNoNulls(body);
        Assert.Equal("고친 이름", (string?)body["name_device"]);
        Assert.Equal(3, (int?)body.SelectToken("hardware_spec.spec.wiring.order"));
    }

    /// <summary>만들기(POST) 본문 — null 이 하나도 없고 소속 제어기와 결선이 같이 실린다.</summary>
    [Fact]
    public async Task should_send_a_create_body_with_no_nulls_when_a_row_is_new()
    {
        var http = new CapturingHttp();
        var board = WiringDoubles.Board(1, placedOnFirst: 1);
        var row = board.AddRow(new SensorFacts(1301, "새 센서", "Fence", "북측 8구간"));
        board.Place(row.Key, 2, 0);

        var service = new WiringApplyService(new DeviceApiSensorGateway(http.Real()), null, null, WiringDoubles.AxisPolicy());
        await service.ApplyAsync(10, board);

        var post = http.Calls.Single(c => c.Method == "POST");
        var body = post.Body!;

        AssertNoNulls(body);
        Assert.Equal(1301, (int?)body["number_device"]);
        Assert.Equal(10, (int?)body["controller_id"]);
        Assert.Equal("Fence", (string?)body["type_sensor"]);
        Assert.Equal(2, (int?)body.SelectToken("hardware_spec.spec.wiring.line"));
        Assert.Equal("북측 8구간", (string?)body.SelectToken("geolocation.location"));
    }

    /// <summary>
    /// 자리를 비우는 PATCH — <b>여기만</b> null 이 허용된다(RFC 7396 의 삭제 표시). 다른 자리의 null 은 여전히 0 건이다.
    /// </summary>
    [Fact]
    public async Task should_send_exactly_one_null_when_a_sensor_is_taken_off_the_line()
    {
        var body = await PatchBodyAsync(board => board.Unplace(board.Rows[1].Key));

        var nulls = body.Descendants().OfType<JProperty>().Where(p => p.Value.Type == JTokenType.Null).ToList();
        var only = Assert.Single(nulls);
        Assert.Equal("hardware_spec.spec.wiring", only.Path);
    }

    /// <summary>제품 길로 나간 PATCH 는 <c>PATCH</c> 동사와 센서 경로를 쓴다(ShapeWrite 를 지난 증거).</summary>
    [Fact]
    public async Task should_use_the_patch_verb_on_the_sensor_endpoint_when_saving()
    {
        var http = new CapturingHttp();
        await RunAsync(http, board => board.Unplace(board.Rows[1].Key));

        var call = http.Calls.Single(c => c.Method is "PATCH" or "PUT");
        Assert.Equal("PATCH", call.Method);
        Assert.EndsWith("/devices/sensors/102", call.Endpoint, StringComparison.Ordinal);
    }
    #endregion

    #region - C3 · 저장 뒤 되돌리기 -
    [Fact]
    public async Task should_not_resurrect_a_saved_row_when_undoing_after_a_save()
    {
        var gateway = new WiringFakeGateway();
        var dialogs = new WiringFakeDialogs { Confirm = true };
        var vm = Open(gateway, dialogs, sensors: 1, placedOnFirst: 1);

        vm.AddOneRow();                       // 새 줄(Id=0)
        vm.Drop(Payload(vm.Palette[0]), new DropTarget(WiringViewModel.SlotZoneKey, vm.Line2[0], -1));   // 2차 선 — 루프를 닫는다
        Assert.True(vm.CanSave, vm.SaveBlockedReason);
        await vm.SaveAsync();

        Assert.Equal(1, gateway.CreateCount);
        Assert.False(vm.CanUndo);             // 저장이 기준을 옮겼다 — 그 전 장면은 버린다

        vm.Undo();
        await vm.SaveAsync();

        Assert.Equal(1, gateway.CreateCount);  // 같은 번호를 두 번 만들지 않는다
    }
    #endregion

    #region - C4 · 불러오기 경고에서 빠져나올 수 있는가 -
    [Fact]
    public void should_let_the_user_recover_when_a_saved_wiring_value_cannot_be_read()
    {
        var board = new WiringBoard();
        board.Load(new[]
        {
            (Id: 1, Channel: (int?)1, Facts: WiringDoubles.Facts(1101, 1), Placement: (WiringPlacement?)null,
             Issue: (string?)"순번 0 이 쓸 수 있는 범위 밖입니다", Groups: (IReadOnlyList<int>?)null),
            (Id: 2, Channel: (int?)2, Facts: WiringDoubles.Facts(1102, 2), Placement: (WiringPlacement?)new WiringPlacement(2, 1),
             Issue: (string?)null, Groups: (IReadOnlyList<int>?)null),
        });

        Assert.True(WiringValidation.BlocksSave(WiringValidation.Evaluate(board)));

        board.Place(board.Rows[0].Key, 1, 0);      // 사람이 자리를 정해 준다 — 그것이 "다시 배치" 다

        Assert.Null(board.Rows[0].LoadIssue);
        Assert.False(WiringValidation.BlocksSave(WiringValidation.Evaluate(board)));
    }

    [Fact]
    public void should_clear_the_load_issue_when_auto_layout_reassigns_every_slot()
    {
        var board = new WiringBoard();
        board.Load(new[]
        {
            (Id: 1, Channel: (int?)1, Facts: WiringDoubles.Facts(1101, 1), Placement: (WiringPlacement?)null,
             Issue: (string?)"겹칩니다", Groups: (IReadOnlyList<int>?)null),
            (Id: 2, Channel: (int?)2, Facts: WiringDoubles.Facts(1102, 2), Placement: (WiringPlacement?)null,
             Issue: (string?)null, Groups: (IReadOnlyList<int>?)null),
        });

        Assert.True(board.AutoLayoutByNumber());

        Assert.All(board.Rows, r => Assert.Null(r.LoadIssue));
    }
    #endregion

    #region - C6 · 선을 건너는 키보드 길 -
    [Fact]
    public void should_move_to_the_other_line_when_the_cross_line_key_is_used()
    {
        var vm = Open(new WiringFakeGateway(), new WiringFakeDialogs(), sensors: 1, placedOnFirst: 1);
        vm.Line1[0].IsSelected = true;

        vm.MoveSelectedToOtherLine();

        Assert.True(vm.Line1[0].IsEmpty);
        Assert.NotNull(vm.Line2[0].Row);              // 같은 자리로 건너간다
        Assert.True(vm.Line2[0].IsSelected);          // 선택도 따라간다(C8 과 같은 요구)
    }

    [Fact]
    public void should_take_the_first_empty_slot_when_the_same_position_is_taken_on_the_other_line()
    {
        var vm = Open(new WiringFakeGateway(), new WiringFakeDialogs(), sensors: 2, placedOnFirst: 2);
        vm.Drop(Payload(vm.Line1[1]), new DropTarget(WiringViewModel.SlotZoneKey, vm.Line2[0], -1));
        vm.Line1[0].IsSelected = true;

        vm.MoveSelectedToOtherLine();

        Assert.NotNull(vm.Line2[1].Row);              // 0번 자리는 찼으니 다음 빈 칸
    }
    #endregion

    #region - C7 · 만들었는데 id 가 없다 -
    [Fact]
    public async Task should_not_count_a_create_as_done_when_the_server_returns_no_id()
    {
        var gateway = new WiringFakeGateway { CreateWithoutId = true };
        var dialogs = new WiringFakeDialogs { Confirm = true };
        var vm = Open(gateway, dialogs, sensors: 1, placedOnFirst: 1);

        vm.AddOneRow();
        vm.Drop(Payload(vm.Palette[0]), new DropTarget(WiringViewModel.SlotZoneKey, vm.Line2[0], -1));
        await vm.SaveAsync();

        Assert.Equal(1, gateway.ListCount);            // 번호로 되찾아 본다
        Assert.True(vm.HasSaveResults);                 // 못 찾았으면 실패로 남긴다
        Assert.Contains("번호 확인 필요", vm.SaveResults[0].Message);
    }

    [Fact]
    public async Task should_recover_the_id_by_number_when_the_create_response_omits_it()
    {
        var gateway = new WiringFakeGateway { CreateWithoutId = true };
        gateway.ListResult.Add(WiringDoubles.ServerSensor(777, 1102, 2, null));
        var board = WiringDoubles.Board(1, placedOnFirst: 1);
        var row = board.AddRow(new SensorFacts(1102, "새 센서", "Fence", ""));
        board.Place(row.Key, 2, 0);

        var result = await new WiringApplyService(gateway, null, null, WiringDoubles.AxisPolicy()).ApplyAsync(10, board);

        Assert.True(result.IsSuccess);
        Assert.Equal(777, result.Rows.Single().NewId);
    }
    #endregion

    #region - C10 · 드리프트는 우리가 쓰는 칸 전부를 본다 -
    [Theory]
    [InlineData("name")]
    [InlineData("number")]
    [InlineData("type")]
    [InlineData("zone")]
    public async Task should_send_nothing_when_another_user_changed_a_field_this_window_writes(string field)
    {
        var gateway = new WiringFakeGateway();
        for (var i = 0; i < 2; i++)
            gateway.Fetched[101 + i] = WiringDoubles.ServerSensor(101 + i, 1101 + i, i + 1, new WiringPlacement(1, i + 1));

        var board = WiringDoubles.Board(2, placedOnFirst: 2);
        board.Rows[0].Facts = board.Rows[0].Facts with { Name = "내가 고친 이름" };

        var server = gateway.Fetched[101];
        switch (field)
        {
            case "name": server.NameDevice = "남이 고친 이름"; break;
            case "number": server.NumberDevice = 9999; break;
            case "type": server.TypeDevice = "PIR"; break;
            case "zone": server.Geolocation!.Location = "남이 고친 구역"; break;
        }

        var result = await new WiringApplyService(gateway, null, null, WiringDoubles.AxisPolicy()).ApplyAsync(10, board);

        Assert.True(result.IsConflict);
        Assert.Equal(0, gateway.PatchCount);
    }

    [Fact]
    public async Task should_send_anyway_when_only_the_status_changed_on_the_server()
    {
        var gateway = new WiringFakeGateway();
        gateway.Fetched[101] = WiringDoubles.ServerSensor(101, 1101, 1, new WiringPlacement(1, 1));
        gateway.Fetched[101].Status = "ERROR";          // 장비가 스스로 바꾸는 칸 — 우리가 쓰지 않는다

        var board = WiringDoubles.Board(1, placedOnFirst: 1);
        board.Rows[0].Facts = board.Rows[0].Facts with { Name = "고친 이름" };

        var result = await new WiringApplyService(gateway, null, null, WiringDoubles.AxisPolicy()).ApplyAsync(10, board);

        Assert.True(result.IsSuccess);
        Assert.Equal("ERROR", (string?)JObject.FromObject(gateway.Patched.Single().Dto)["status"]);
    }
    #endregion

    #region - C11 · 작은 것들 -
    [Fact]
    public void should_not_push_an_undo_step_when_the_slot_cap_is_reached()
    {
        var vm = Open(new WiringFakeGateway(), new WiringFakeDialogs(), sensors: 1);
        while (vm.Line1.Count < WiringBoard.MAX_SLOTS) vm.AddSlotPrimary();

        var before = vm.CanUndo;
        vm.AddSlotPrimary();

        Assert.Equal(before, vm.CanUndo);
        Assert.Equal(WiringBoard.MAX_SLOTS, vm.Line1.Count);
        Assert.Contains("64", vm.StatusText);
    }

    [Fact]
    public void should_undo_a_multi_row_enter_in_one_step()
    {
        var vm = Open(new WiringFakeGateway(), new WiringFakeDialogs(), sensors: 3);

        vm.PlaceManyFromPalette(vm.Palette.ToList());
        Assert.Empty(vm.Palette);

        vm.Undo();

        Assert.Equal(3, vm.Palette.Count);        // 한 걸음으로 전부 돌아온다
        Assert.False(vm.CanUndo);
    }

    [Fact]
    public void should_refuse_auto_layout_when_the_sensors_do_not_fit()
    {
        var board = WiringDoubles.Board(0);
        for (var i = 0; i < WiringBoard.MAX_SLOTS * 2 + 2; i++) board.AddRow(new SensorFacts(2000 + i, $"센서 {i}", "Fence", ""));

        Assert.False(board.AutoLayoutByNumber());
        Assert.Empty(board.Placed(1));            // 아무것도 바꾸지 않는다
    }

    [Fact]
    public void should_report_the_loop_as_open_when_only_the_second_line_has_sensors()
    {
        var board = WiringDoubles.Board(1);
        board.Place(board.Rows[0].Key, 2, 0);

        var issues = WiringValidation.Evaluate(board);

        Assert.Contains(issues, i => i.Code == WiringValidation.CODE_LOOP_OPEN && i.Message.Contains("1차 선이 비어"));
    }

    [Fact]
    public void should_keep_the_typed_text_and_say_why_when_the_number_is_not_a_number()
    {
        var vm = Open(new WiringFakeGateway(), new WiringFakeDialogs(), sensors: 1);
        var row = vm.Rows[0];

        row.NumberText = "열둘";

        Assert.Equal("열둘", row.NumberText);          // 친 글자를 버리지 않는다
        Assert.True(row.HasNumberError);
        Assert.Equal(1101, row.Row.Facts.Number);      // 값은 아직 옛 번호다
    }

    [Fact]
    public async Task should_fill_numbers_in_display_order_when_rows_were_picked_out_of_order()
    {
        var dialogs = new WiringFakeDialogs { TextAnswer = "1201" };
        var vm = Open(new WiringFakeGateway(), dialogs, sensors: 3);

        // 화면 순서와 반대로 고른다
        vm.OnSelectionChanged(new[] { vm.Rows[2], vm.Rows[0] });
        await vm.FillSequentialAsync();

        Assert.Equal("1201", vm.Rows[0].NumberText);
        Assert.Equal("1202", vm.Rows[2].NumberText);
    }
    #endregion

    #region - W2 · 그룹 3상태 -
    [Fact]
    public void should_report_mixed_when_only_some_rows_are_in_the_group()
    {
        var board = BoardWithGroups();

        Assert.Equal(GroupCheck.All, SensorGroupEdit.StateOf(board.Rows, 1));
        Assert.Equal(GroupCheck.Mixed, SensorGroupEdit.StateOf(board.Rows, 2));
        Assert.Equal(GroupCheck.None, SensorGroupEdit.StateOf(board.Rows, 3));
    }

    [Fact]
    public void should_leave_untouched_groups_alone_when_applying_one_group()
    {
        var board = BoardWithGroups();

        SensorGroupEdit.Apply(board.Rows, new Dictionary<int, bool> { [3] = true });

        Assert.All(board.Rows, r => Assert.Contains(3, r.Groups));
        Assert.Contains(2, board.Rows[0].Groups);       // 섞여 있던 그룹은 그대로
        Assert.DoesNotContain(2, board.Rows[1].Groups);
    }

    [Fact]
    public void should_plan_one_call_per_group_and_direction_when_saving_groups()
    {
        var board = BoardWithGroups();
        SensorGroupEdit.Apply(board.Rows, new Dictionary<int, bool> { [3] = true, [1] = false });

        var calls = SensorGroupEdit.Plan(board.Rows);

        Assert.Equal(2, calls.Count);                                    // 센서가 셋이어도 호출은 둘
        var add = calls.Single(c => c.Add);
        Assert.Equal(3, add.GroupId);
        Assert.Equal(3, add.DeviceIds.Count);
        var remove = calls.Single(c => !c.Add);
        Assert.Equal(1, remove.GroupId);
        Assert.Equal(3, remove.DeviceIds.Count);
    }

    [Fact]
    public void should_send_nothing_for_a_group_when_the_checkbox_was_not_touched()
    {
        var board = BoardWithGroups();

        Assert.Empty(SensorGroupEdit.Plan(board.Rows));
    }

    [Fact]
    public async Task should_call_the_bulk_group_endpoints_once_per_group_when_saving()
    {
        var gateway = new WiringFakeGateway();
        for (var i = 0; i < 2; i++)
            gateway.Fetched[101 + i] = WiringDoubles.ServerSensor(101 + i, 1101 + i, i + 1, new WiringPlacement(1, i + 1));

        var board = WiringDoubles.Board(2, placedOnFirst: 2);
        SensorGroupEdit.Apply(board.Rows, new Dictionary<int, bool> { [5] = true });

        var result = await new WiringApplyService(gateway, null, null, WiringDoubles.AxisPolicy()).ApplyAsync(10, board);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, gateway.PatchCount);                     // 그룹만 바뀌면 장비는 건드리지 않는다
        var call = Assert.Single(gateway.GroupCalls);
        Assert.True(call.Add);
        Assert.Equal(5, call.GroupId);
        Assert.Equal(new[] { 101, 102 }, call.DeviceIds);
    }

    [Fact]
    public async Task should_put_a_new_row_into_its_group_after_it_has_an_id()
    {
        var gateway = new WiringFakeGateway();
        var board = WiringDoubles.Board(0);
        var row = board.AddRow(new SensorFacts(1301, "새 센서", "Fence", ""));
        board.Place(row.Key, 1, 0);
        board.Place(board.AddRow(new SensorFacts(1302, "짝", "Fence", "")).Key, 2, 0);
        SensorGroupEdit.Apply(new[] { row }, new Dictionary<int, bool> { [4] = true });

        var result = await new WiringApplyService(gateway, null, null, WiringDoubles.AxisPolicy()).ApplyAsync(10, board);

        Assert.True(result.IsSuccess);
        var call = Assert.Single(gateway.GroupCalls);
        Assert.Equal(new[] { 901 }, call.DeviceIds);             // 막 받은 id 로 나간다
    }

    [Fact]
    public void should_apply_the_group_delta_through_the_view_model_when_the_checkbox_is_clicked()
    {
        var vm = Open(new WiringFakeGateway(), new WiringFakeDialogs(), sensors: 2, groups: new[]
        {
            new WiringGroupInfo(1, "북측"), new WiringGroupInfo(2, "정문"),
        });

        vm.OnSelectionChanged(new[] { vm.Rows[0], vm.Rows[1] });
        vm.ToggleGroup(vm.GroupChecks[0]);

        Assert.True(vm.CanApplyEdit);
        Assert.Contains("북측 추가", vm.EditPreview);

        vm.ApplyEdit();

        Assert.All(vm.Rows, r => Assert.Contains(1, r.Row.Groups));
        Assert.True(vm.HasChanges);
    }
    #endregion

    #region - W7 · 열 매핑 -
    [Fact]
    public void should_read_by_the_chosen_columns_when_the_mapping_is_changed()
    {
        const string text = "1301\t북측 A\tFence\t북측 8구간";

        var first = TsvPaste.Parse(text, Array.Empty<int>());
        Assert.Equal("북측 A", first.Accepted[0].Facts.Name);

        // 2번째 열을 "구역" 으로 바꾸면 이름은 비고 구역이 그 값이 된다
        var mapping = first.Columns.With(1, PasteColumn.Zone);
        var second = TsvPaste.Parse(text, Array.Empty<int>(), mapping: mapping);

        Assert.Equal("북측 A", second.Accepted[0].Facts.Zone);
        Assert.Equal("센서 1301", second.Accepted[0].Facts.Name);
    }

    [Fact]
    public void should_refuse_when_no_column_is_the_number()
    {
        var report = TsvPaste.Parse("1301\tA", Array.Empty<int>(),
                                    mapping: new PasteColumnMap(new[] { PasteColumn.Name, PasteColumn.Zone }));

        Assert.NotNull(report.FatalError);
        Assert.Empty(report.Accepted);
    }

    [Fact]
    public void should_move_the_role_off_the_old_column_when_the_same_role_is_chosen_twice()
    {
        var map = new PasteColumnMap(new[] { PasteColumn.Number, PasteColumn.Name });

        var next = map.With(1, PasteColumn.Number);

        Assert.Equal(PasteColumn.Ignore, next.At(0));
        Assert.Equal(PasteColumn.Number, next.At(1));
    }

    [Fact]
    public void should_detect_the_header_roles_when_the_first_line_names_the_columns()
    {
        var report = TsvPaste.Parse("이름\t번호\n센서 A\t1301", Array.Empty<int>());

        Assert.True(report.HeaderDetected);
        Assert.Equal(PasteColumn.Name, report.Columns.At(0));
        Assert.Equal(PasteColumn.Number, report.Columns.At(1));
        Assert.Equal(1301, report.Accepted[0].Facts.Number);
    }

    [Fact]
    public void should_keep_the_source_text_so_the_dialog_can_reparse()
    {
        var report = TsvPaste.Parse("1301\tA", Array.Empty<int>());

        Assert.Equal("1301\tA", report.Text);
        Assert.Equal(2, TsvPaste.Split(report.Text)[0].Count);
    }
    #endregion

    #region - Helpers -
    private static WiringBoard BoardWithGroups()
    {
        var board = new WiringBoard();
        board.Load(new[]
        {
            (Id: 101, Channel: (int?)1, Facts: WiringDoubles.Facts(1101, 1), Placement: (WiringPlacement?)new WiringPlacement(1, 1),
             Issue: (string?)null, Groups: (IReadOnlyList<int>?)new[] { 1, 2 }),
            (Id: 102, Channel: (int?)2, Facts: WiringDoubles.Facts(1102, 2), Placement: (WiringPlacement?)new WiringPlacement(1, 2),
             Issue: (string?)null, Groups: (IReadOnlyList<int>?)new[] { 1 }),
            (Id: 103, Channel: (int?)3, Facts: WiringDoubles.Facts(1103, 3), Placement: (WiringPlacement?)new WiringPlacement(2, 1),
             Issue: (string?)null, Groups: (IReadOnlyList<int>?)new[] { 1 }),
        });
        return board;
    }

    private static WiringViewModel Open(WiringFakeGateway gateway, WiringFakeDialogs dialogs, int sensors, int placedOnFirst = 0,
                                        IReadOnlyList<WiringGroupInfo>? groups = null)
    {
        for (var i = 0; i < sensors; i++)
            gateway.Fetched[101 + i] = WiringDoubles.ServerSensor(101 + i, 1101 + i, i + 1,
                i < placedOnFirst ? new WiringPlacement(1, i + 1) : null);

        var seeds = Enumerable.Range(0, sensors).Select(i => new WiringSensorSeed(
            101 + i, i + 1, WiringDoubles.Facts(1101 + i, i + 1),
            i < placedOnFirst ? new WiringPlacement(1, i + 1) : null));

        var apply = new WiringApplyService(gateway, null, null, WiringDoubles.AxisPolicy());
        return WiringViewModel.ForController(
            new WiringControllerInfo(10, 1, "북측 제어기 B", "10.20.1.103"),
            seeds, new[] { "Fence", "PIR" }, apply, dialogs, groups);
    }

    /// <summary>제품이 쓰는 길로 저장하고 그 PATCH 본문을 돌려준다(두 번째 센서를 건드린다).</summary>
    private static async Task<JObject> PatchBodyAsync(Action<WiringBoard> change)
    {
        var http = new CapturingHttp();
        await RunAsync(http, change);
        return http.Calls.Last(c => c.Method == "PATCH").Body!;
    }

    private static async Task RunAsync(CapturingHttp http, Action<WiringBoard> change)
    {
        var board = WiringDoubles.Board(3, placedOnFirst: 3);
        // 재조회가 돌려줄 서버 쪽 상태 — 모든 칸이 채워져 있다.
        http.GetResponseJson = ServerEnvelope(WiringDoubles.ServerSensor(102, 1102, 2, new WiringPlacement(1, 2)));

        change(board);

        var service = new WiringApplyService(new DeviceApiSensorGateway(http.Real()), null, null, WiringDoubles.AxisPolicy());
        var result = await service.ApplyAsync(10, board);
        Assert.True(result.IsSuccess, result.Message);
    }

    private static string ServerEnvelope(SensorDeviceDto dto)
    {
        dto.UseAxisWrite = true;          // 7.0+ 응답에는 축이 실려 온다 — 그래야 결선을 읽는다
        return Envelope(dto);
    }

    private static string Envelope(SensorDeviceDto dto)
        => new JObject
        {
            ["success"] = true,
            ["data"] = JObject.FromObject(dto),
            ["meta"] = new JObject(),
        }.ToString();

    private static void AssertNoNulls(JObject body)
        => Assert.DoesNotContain(body.Descendants().OfType<JProperty>(), p => p.Value.Type == JTokenType.Null);
    #endregion
}
