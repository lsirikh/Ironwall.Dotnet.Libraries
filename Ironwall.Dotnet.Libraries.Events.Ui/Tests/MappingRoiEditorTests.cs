using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : 프리셋 ROI 설정 창 — 클릭으로 점 · 선 · 다각형, 저장 · 삭제, 프리셋 관리 창 입구
   Created By   : Claude
   Created On   : 2026-10-04
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>서버 ROI 저장소 흉내 — 프리셋별 목록을 메모리에 둔다.</summary>
internal sealed class FakeRoiServer : IMappingRoiEditor
{
    private int _nextId = 5000;

    public Dictionary<int, List<MappingRoiDetail>> Store { get; } = new();

    public List<string> Calls { get; } = new();

    /// <summary>가장 최근 생성 · 교체에 실린 값.</summary>
    public MappingRoiDraft? LastDraft { get; private set; }

    /// <summary>다음 쓰기를 이 사유로 실패시킨다.</summary>
    public string? FailNextWrite { get; set; }

    public FakeRoiServer With(int presetId, params MappingRoiDetail[] rois)
    {
        Store[presetId] = rois.ToList();
        return this;
    }

    private List<MappingRoiDetail> Of(int presetId)
        => Store.TryGetValue(presetId, out var list) ? list : Store[presetId] = new List<MappingRoiDetail>();

    public async Task<MappingCallResult<IReadOnlyList<MappingRoiDetail>>> ListRoisAsync(int presetId, CancellationToken token = default)
    {
        Calls.Add($"list:{presetId}");
        await Task.Yield();
        return MappingCallResult<IReadOnlyList<MappingRoiDetail>>.Ok(Of(presetId).ToList());
    }

    public async Task<MappingCallResult<MappingRoiDetail>> CreateRoiAsync(int presetId, MappingRoiDraft draft, CancellationToken token = default)
    {
        Calls.Add($"create:{presetId}:{draft.Name}:{draft.Points.Count}");
        LastDraft = draft;
        await Task.Yield();
        if (TakeFailure() is { } fail) return MappingCallResult<MappingRoiDetail>.Fail(fail, "boom", 422);
        var made = ToDetail(_nextId++, presetId, draft);
        Of(presetId).Add(made);
        return MappingCallResult<MappingRoiDetail>.Ok(made, 201);
    }

    public async Task<MappingCallResult<MappingRoiDetail>> ReplaceRoiAsync(int presetId, int roiId, MappingRoiDraft draft, CancellationToken token = default)
    {
        Calls.Add($"replace:{presetId}:{roiId}");
        LastDraft = draft;
        await Task.Yield();
        if (TakeFailure() is { } fail) return MappingCallResult<MappingRoiDetail>.Fail(fail, "boom", 422);
        var list = Of(presetId);
        var i = list.FindIndex(r => r.Id == roiId);
        var updated = ToDetail(roiId, presetId, draft);
        list[i] = updated;
        return MappingCallResult<MappingRoiDetail>.Ok(updated);
    }

    public async Task<MappingCallResult<bool>> DeleteRoiAsync(int presetId, int roiId, CancellationToken token = default)
    {
        Calls.Add($"delete:{presetId}:{roiId}");
        await Task.Yield();
        if (TakeFailure() is { } fail) return MappingCallResult<bool>.Fail(fail, "boom", 404);
        Of(presetId).RemoveAll(r => r.Id == roiId);
        return MappingCallResult<bool>.Ok(true);
    }

    private static MappingRoiDetail ToDetail(int id, int presetId, MappingRoiDraft d)
        => new(id, presetId, d.Name, d.ResolutionWidth, d.ResolutionHeight, d.IsEnable, d.IsExclusion, d.Points.ToList(), d.IsPixelUnit);

    private string? TakeFailure()
    {
        var fail = FailNextWrite;
        FailNextWrite = null;
        return fail;
    }
}

/// <summary>프리셋 · ROI 를 함께 다루는 서버 흉내 — 실제 게이트웨이처럼 한 객체가 두 계약을 진다.</summary>
internal sealed class FakePresetRoiServer : IMappingPresetEditor, IMappingPresetSource, IMappingRoiEditor
{
    public FakePresetServer Presets { get; } = new FakePresetServer().With(370, (3701, 1, "정문"), (3702, 2, "후문"));

    public FakeRoiServer Rois { get; } = new();

    public Task<MappingCallResult<IReadOnlyList<MappingPresetDetail>>> ListPresetDetailsAsync(int cameraId, CancellationToken token = default) => Presets.ListPresetDetailsAsync(cameraId, token);
    public Task<MappingCallResult<MappingPresetDetail>> CreatePresetAsync(int cameraId, MappingPresetDraft draft, CancellationToken token = default) => Presets.CreatePresetAsync(cameraId, draft, token);
    public Task<MappingCallResult<MappingPresetDetail>> PatchPresetAsync(int cameraId, int presetId, MappingPresetDraft draft, CancellationToken token = default) => Presets.PatchPresetAsync(cameraId, presetId, draft, token);
    public Task<MappingCallResult<bool>> DeletePresetAsync(int cameraId, int presetId, CancellationToken token = default) => Presets.DeletePresetAsync(cameraId, presetId, token);
    public Task<MappingCallResult<IReadOnlyList<MappingPresetInfo>>> ListPresetsAsync(int cameraId, CancellationToken token = default) => Presets.ListPresetsAsync(cameraId, token);
    public Task<MappingCallResult<IReadOnlyList<MappingRoiDetail>>> ListRoisAsync(int presetId, CancellationToken token = default) => Rois.ListRoisAsync(presetId, token);
    public Task<MappingCallResult<MappingRoiDetail>> CreateRoiAsync(int presetId, MappingRoiDraft draft, CancellationToken token = default) => Rois.CreateRoiAsync(presetId, draft, token);
    public Task<MappingCallResult<MappingRoiDetail>> ReplaceRoiAsync(int presetId, int roiId, MappingRoiDraft draft, CancellationToken token = default) => Rois.ReplaceRoiAsync(presetId, roiId, draft, token);
    public Task<MappingCallResult<bool>> DeleteRoiAsync(int presetId, int roiId, CancellationToken token = default) => Rois.DeleteRoiAsync(presetId, roiId, token);
}

public class MappingRoiEditorTests
{
    private static readonly MappingRoiPoint[] Square =
    {
        new(0.2, 0.2), new(0.6, 0.2), new(0.6, 0.6), new(0.2, 0.6),
    };

    private static async Task<(MappingRoiEditorViewModel Vm, FakeRoiServer Server)> OpenAsync(bool canEdit = true, bool withExisting = true)
    {
        var server = new FakeRoiServer();
        if (withExisting)
            server.With(3702, new MappingRoiDetail(51, 3702, "정문 앞", 1280, 720, true, null, Square));
        var vm = new MappingRoiEditorViewModel(server, 3702, "2. 후문", "CAM-370", canEdit);
        await vm.LoadAsync();
        return (vm, server);
    }

    private static void Draw(MappingRoiEditorViewModel vm, params (double X, double Y)[] points)
    {
        foreach (var (x, y) in points) Assert.True(vm.AddPoint(x, y), $"점 ({x},{y}) 을 찍지 못했다");
    }

    #region - 창 -
    [Fact]
    public async Task should_list_the_presets_rois_when_opened()
    {
        var (vm, server) = await OpenAsync();

        Assert.Equal("CAM-370 · 2. 후문 ROI 설정", vm.Title);
        Assert.Equal(new[] { 51 }, vm.Rois.Select(r => r.Id));
        Assert.Contains("list:3702", server.Calls);
        Assert.False(vm.HasForm);
        Assert.Equal("점 4개 · 1280×720", vm.Rois[0].Subtitle);
    }

    [Fact]
    public async Task should_fill_the_form_and_closed_shape_when_a_roi_is_selected()
    {
        var (vm, _) = await OpenAsync();

        vm.SelectedRoi = vm.Rois[0];

        Assert.True(vm.HasForm);
        Assert.Equal("정문 앞", vm.EditName);
        Assert.Equal("1280", vm.EditWidth);
        Assert.Equal("720", vm.EditHeight);
        Assert.Equal(1280.0 / 720.0, vm.CanvasAspect, 6);
        Assert.True(vm.IsClosed);
        Assert.Equal(Square, vm.Points);
        Assert.False(vm.CanSave);           // 바뀐 것이 없다
        Assert.False(vm.CanDraw);           // 닫힌 다각형에는 점을 더 찍지 않는다
        Assert.True(vm.CanMovePoints);
    }
    #endregion

    #region - 그리기 -
    [Fact]
    public async Task should_build_a_polygon_from_clicks_and_create_it_when_closed()
    {
        var (vm, server) = await OpenAsync();
        vm.BeginNew();
        Assert.Equal("ROI 2", vm.EditName);
        Assert.Equal("1280", vm.EditWidth);     // 기존 ROI 의 해상도를 따른다
        Assert.Equal("첫 점을 찍으세요", vm.DrawStatus);

        Draw(vm, (0.1, 0.1), (0.5, 0.1));
        Assert.False(vm.CanClosePolygon);
        Assert.Equal("점 2개 — 계속 찍으세요", vm.DrawStatus);
        Draw(vm, (0.3, 0.5));
        Assert.Equal("점 3개 — 첫 점을 누르면 닫힙니다", vm.DrawStatus);
        Assert.False(vm.CanSave);
        Assert.Equal("첫 점을 눌러 다각형을 닫으세요.", vm.ValidationText);

        Assert.True(vm.ClosePolygon());
        Assert.True(vm.IsClosed);
        Assert.False(vm.CanDraw);
        Assert.True(vm.CanSave);
        await vm.SaveAsync();

        Assert.Contains("create:3702:ROI 2:3", server.Calls);
        Assert.Equal(new[] { new MappingRoiPoint(0.1, 0.1), new MappingRoiPoint(0.5, 0.1), new MappingRoiPoint(0.3, 0.5) }, server.LastDraft!.Points);
        Assert.True(vm.HasChanges);
        Assert.False(vm.IsNew);
        Assert.Equal("ROI 2", vm.SelectedRoi?.Name);
        Assert.Equal(2, vm.Rois.Count);
    }

    [Fact]
    public async Task should_not_close_with_fewer_than_three_points()
    {
        var (vm, _) = await OpenAsync(withExisting: false);
        vm.BeginNew();
        Assert.Equal("1920", vm.EditWidth);     // 기존 ROI 가 없으면 기본 해상도
        Draw(vm, (0.1, 0.1), (0.5, 0.1));

        Assert.False(vm.ClosePolygon());
        Assert.False(vm.IsClosed);
        Assert.Equal("점을 3개 이상 찍으세요.", vm.ValidationText);
    }

    [Fact]
    public async Task should_refuse_a_point_whose_line_crosses_an_earlier_line()
    {
        var (vm, _) = await OpenAsync();
        vm.BeginNew();
        Draw(vm, (0.1, 0.1), (0.9, 0.9), (0.9, 0.1));

        Assert.False(vm.AddPoint(0.1, 0.5));

        Assert.Equal(3, vm.Points.Count);
        Assert.Contains("겹치는", vm.Message);
    }

    [Fact]
    public async Task should_refuse_to_close_a_bow_tie()
    {
        var (vm, _) = await OpenAsync();
        vm.BeginNew();
        Draw(vm, (0.1, 0.1), (0.9, 0.1), (0.1, 0.9), (0.9, 0.9));

        Assert.False(vm.ClosePolygon());

        Assert.False(vm.IsClosed);
        Assert.Contains("겹칩니다", vm.Message);
    }

    [Fact]
    public async Task should_clamp_points_to_the_frame_and_ignore_a_repeated_click()
    {
        var (vm, _) = await OpenAsync();
        vm.BeginNew();

        Assert.True(vm.AddPoint(-0.2, 1.4));
        Assert.False(vm.AddPoint(0, 1));        // 같은 자리 두 번(더블클릭의 첫 클릭)

        Assert.Equal(new[] { new MappingRoiPoint(0, 1) }, vm.Points);
    }

    [Fact]
    public async Task should_reopen_then_remove_points_on_undo_and_clear_on_redraw()
    {
        var (vm, _) = await OpenAsync();
        vm.SelectedRoi = vm.Rois[0];

        vm.UndoPoint();
        Assert.False(vm.IsClosed);              // 닫힌 다각형은 먼저 연다
        Assert.Equal(4, vm.Points.Count);
        vm.UndoPoint();
        Assert.Equal(3, vm.Points.Count);
        Assert.True(vm.CanDraw);

        vm.Redraw();
        Assert.Empty(vm.Points);
        Assert.False(vm.CanSave);
    }

    [Fact]
    public async Task should_drop_only_the_open_line_on_cancel()
    {
        var (vm, _) = await OpenAsync();
        vm.BeginNew();
        Draw(vm, (0.1, 0.1), (0.5, 0.1));

        vm.CancelDrawing();

        Assert.Empty(vm.Points);
        Assert.True(vm.HasForm);
    }
    #endregion

    #region - 고치기 · 지우기 -
    [Fact]
    public async Task should_replace_the_roi_with_the_moved_vertex()
    {
        var (vm, server) = await OpenAsync();
        vm.SelectedRoi = vm.Rois[0];

        vm.MovePoint(2, 0.8, 0.7);
        Assert.True(vm.CanSave);
        await vm.SaveAsync();

        Assert.Contains("replace:3702:51", server.Calls);
        Assert.Equal(new MappingRoiPoint(0.8, 0.7), server.LastDraft!.Points[2]);
        Assert.Equal(1280, server.LastDraft.ResolutionWidth);
        Assert.Equal(new MappingRoiPoint(0.8, 0.7), vm.SelectedRoi!.Points[2]);
        Assert.Contains("저장했습니다", vm.Message);
    }

    [Fact]
    public async Task should_block_saving_when_a_moved_vertex_makes_lines_cross()
    {
        var (vm, _) = await OpenAsync();
        vm.SelectedRoi = vm.Rois[0];

        vm.MovePoint(2, 0.4, 0.1);              // (0.6,0.6) → 윗변 너머로 넘겨 변이 윗변을 가로지른다

        Assert.Equal("선이 서로 겹칩니다. 꼭짓점을 옮기세요.", vm.ValidationText);
        Assert.False(vm.CanSave);
    }

    [Fact]
    public async Task should_save_name_and_enable_changes_without_redrawing()
    {
        var (vm, server) = await OpenAsync();
        vm.SelectedRoi = vm.Rois[0];

        vm.EditName = "정문 앞 B";
        vm.EditEnabled = false;
        await vm.SaveAsync();

        Assert.Equal("정문 앞 B", server.LastDraft!.Name);
        Assert.False(server.LastDraft.IsEnable);
        Assert.Equal(Square, server.LastDraft.Points);
        Assert.Equal("정문 앞 B (꺼짐)", vm.SelectedRoi!.Display);
    }

    [Fact]
    public async Task should_keep_the_drawing_when_the_server_refuses()
    {
        var (vm, server) = await OpenAsync();
        vm.BeginNew();
        Draw(vm, (0.1, 0.1), (0.5, 0.1), (0.3, 0.5));
        vm.ClosePolygon();
        server.FailNextWrite = "ROI 를 만들지 못했습니다. 입력값을 확인하세요.";

        await vm.SaveAsync();

        Assert.True(vm.IsNew);
        Assert.Equal(3, vm.Points.Count);
        Assert.True(vm.IsClosed);
        Assert.Contains("만들지 못했습니다", vm.Message);
        Assert.True(vm.CanSave);
    }

    [Fact]
    public async Task should_delete_after_confirmation()
    {
        var (vm, server) = await OpenAsync();
        vm.SelectedRoi = vm.Rois[0];

        vm.RequestDelete();
        Assert.True(vm.IsConfirmingDelete);
        Assert.Equal("'정문 앞' ROI 를 지울까요?", vm.DeleteConfirmText);
        await vm.ConfirmDeleteAsync();

        Assert.Contains("delete:3702:51", server.Calls);
        Assert.Empty(vm.Rois);
        Assert.True(vm.IsEmpty);
        Assert.False(vm.HasForm);
        Assert.True(vm.HasChanges);
    }

    [Fact]
    public async Task should_only_view_without_edit_permission()
    {
        var (vm, _) = await OpenAsync(canEdit: false);
        vm.SelectedRoi = vm.Rois[0];

        Assert.False(vm.CanBeginNew);
        Assert.False(vm.CanMovePoints);
        Assert.False(vm.CanUndoPoint);
        Assert.False(vm.CanDelete);
        Assert.False(vm.CanSave);
        vm.MovePoint(0, 0.9, 0.9);
        Assert.Equal(Square, vm.Points);
    }

    [Fact]
    public void should_judge_simple_and_crossing_polygons()
    {
        Assert.False(MappingRoiGeometry.IsSelfIntersecting(Square));
        Assert.True(MappingRoiGeometry.IsSelfIntersecting(new MappingRoiPoint[] { new(0, 0), new(1, 0), new(0, 1), new(1, 1) }));
        Assert.Equal(0.16, MappingRoiGeometry.Area(Square), 6);
    }
    #endregion

    #region - 서버 본문 -
    [Fact]
    public void should_send_points_with_unique_orders_and_omit_exclusion_the_server_never_gave()
    {
        var body = MappingWorkbenchGateway.RoiBody(new MappingRoiDraft(" 정문 ", 1920, 1080, true, null, Square));

        Assert.Equal("정문", body.Value<string>("name"));
        Assert.Null(body["is_exclusion"]);          // 구판 서버는 모르는 키를 422 로 막는다
        Assert.Null(body["id"]);
        var points = (JArray)body["points"]!;
        Assert.Equal(new[] { 1, 2, 3, 4 }, points.Select(p => p.Value<int>("order")));
        Assert.Equal(0.6, points[1].Value<double>("x"), 6);
    }

    [Fact]
    public void should_send_back_exclusion_and_pixel_units_it_read()
    {
        var body = MappingWorkbenchGateway.RoiBody(new MappingRoiDraft("A", 1000, 500, false, true, Square, IsPixelUnit: true));

        Assert.True(body.Value<bool>("is_exclusion"));
        var first = body["points"]![0]!;
        Assert.Equal(200, first.Value<double>("x"), 6);
        Assert.Equal(100, first.Value<double>("y"), 6);
    }

    [Fact]
    public void should_read_pixel_points_as_normalised_and_remember_the_unit()
    {
        var json = JObject.Parse(@"{""id"":7,""preset_id"":3702,""name"":""P"",""resolution_width"":1000,""resolution_height"":500,
            ""is_enable"":true,""points"":[{""id"":3,""x"":500,""y"":250,""order"":2},{""id"":1,""x"":100,""y"":50,""order"":1},{""id"":2,""x"":900,""y"":50,""order"":3}]}");

        var roi = MappingWorkbenchGateway.ReadRoi(json, 3702)!;

        Assert.True(roi.IsPixelUnit);
        Assert.Null(roi.IsExclusion);
        Assert.Equal(new[] { new MappingRoiPoint(0.1, 0.1), new MappingRoiPoint(0.5, 0.5), new MappingRoiPoint(0.9, 0.1) }, roi.Points);
    }

    [Fact]
    public void should_read_normalised_points_and_exclusion_as_given()
    {
        var json = JObject.Parse(@"{""id"":8,""name"":"""",""resolution_width"":1920,""resolution_height"":1080,""is_enable"":false,""is_exclusion"":false,
            ""points"":[{""x"":0.1,""y"":0.2,""order"":1},{""x"":0.3,""y"":0.2,""order"":2},{""x"":0.2,""y"":0.4,""order"":3}]}");

        var roi = MappingWorkbenchGateway.ReadRoi(json, 3702)!;

        Assert.False(roi.IsPixelUnit);
        Assert.False(roi.IsExclusion);
        Assert.Equal(3702, roi.PresetId);
        Assert.Equal("ROI 8", roi.Name);
        Assert.False(roi.IsEnable);
        Assert.Equal(new MappingRoiPoint(0.1, 0.2), roi.Points[0]);
    }
    #endregion

    #region - 프리셋 관리 창 입구 -
    [Fact]
    public async Task should_offer_roi_settings_only_for_a_saved_preset_when_wired()
    {
        var server = new FakePresetRoiServer();
        var manager = new MappingPresetManagerViewModel(server, 370, "CAM-370");
        await manager.LoadAsync();
        Assert.False(manager.HasRoiEditor);

        manager.ShowRoiEditor = _ => Task.CompletedTask;
        Assert.True(manager.HasRoiEditor);
        Assert.False(manager.CanEditRois);          // 고른 것이 없다

        manager.SelectedPreset = manager.Presets.Single(p => p.Id == 3702);
        Assert.True(manager.CanEditRois);

        manager.BeginNew();
        Assert.False(manager.CanEditRois);          // 새 프리셋은 먼저 만들어야 한다
    }

    [Fact]
    public async Task should_hide_roi_settings_when_the_editor_cannot_reach_rois()
    {
        var manager = new MappingPresetManagerViewModel(new FakePresetServer().With(370, (3701, 1, "정문")), 370, "CAM-370")
        {
            ShowRoiEditor = _ => Task.CompletedTask,
        };
        await manager.LoadAsync();
        manager.SelectedPreset = manager.Presets.Single();

        Assert.False(manager.HasRoiEditor);
        Assert.False(manager.CanEditRois);
    }

    [Fact]
    public async Task should_open_the_selected_presets_roi_window_and_report_changes()
    {
        var server = new FakePresetRoiServer();
        MappingRoiEditorViewModel? opened = null;
        var manager = new MappingPresetManagerViewModel(server, 370, "CAM-370", canEdit: true)
        {
            ShowRoiEditor = async editor =>
            {
                opened = editor;
                await editor.LoadAsync();
                editor.BeginNew();
                Draw(editor, (0.1, 0.1), (0.5, 0.1), (0.3, 0.5));
                editor.ClosePolygon();
                await editor.SaveAsync();
            },
        };
        await manager.LoadAsync();
        manager.SelectedPreset = manager.Presets.Single(p => p.Id == 3702);

        await manager.EditRoisAsync();

        Assert.Equal(3702, opened?.PresetId);
        Assert.Equal("CAM-370 · 2. 후문 ROI 설정", opened?.Title);
        Assert.Contains("create:3702:ROI 1:3", server.Rois.Calls);
        Assert.Contains("ROI 를 바꿨습니다", manager.Message);
    }

    [Fact]
    public async Task should_open_the_roi_window_read_only_without_edit_permission()
    {
        var server = new FakePresetRoiServer();
        MappingRoiEditorViewModel? opened = null;
        var manager = new MappingPresetManagerViewModel(server, 370, "CAM-370", canEdit: false)
        {
            ShowRoiEditor = editor => { opened = editor; return Task.CompletedTask; },
        };
        await manager.LoadAsync();
        manager.SelectedPreset = manager.Presets.First();

        await manager.EditRoisAsync();

        Assert.NotNull(opened);
        Assert.False(opened!.CanEdit);
    }

    [Fact]
    public async Task should_survive_a_roi_window_that_fails_to_open()
    {
        var server = new FakePresetRoiServer();
        var manager = new MappingPresetManagerViewModel(server, 370, "CAM-370")
        {
            ShowRoiEditor = _ => throw new InvalidOperationException("no window"),
        };
        await manager.LoadAsync();
        manager.SelectedPreset = manager.Presets.First();

        await manager.EditRoisAsync();

        Assert.Equal("ROI 설정 창을 열지 못했습니다.", manager.Message);
    }

    [Fact]
    public async Task should_pass_the_roi_window_presenter_from_the_workbench_to_the_preset_manager()
    {
        var gateway = new CountingGateway();
        gateway.Cameras.Add(new MappingCameraReadDto
        {
            ConfigId = 700, EventMappingId = 1, DelayTime = 3, IsEnable = true, UpdatedAt = "T0",
            Camera = new MappingDeviceRefDto { Id = 370, CategoryDevice = "Camera" },
        });
        var server = new FakePresetRoiServer();
        var vm = new MappingWorkbenchViewModel(gateway, new StubDevices(), presets: server, presetEditor: server);
        await vm.ReloadAsync();
        vm.SelectedBoardRows.Clear();
        vm.SelectedBoardRows.Add(vm.BoardRows[0]);
        vm.OnSelectionChanged();
        await vm.PresetLoadTask;

        Func<MappingRoiEditorViewModel, Task> presenter = _ => Task.CompletedTask;
        vm.ShowRoiEditor = presenter;
        MappingPresetManagerViewModel? opened = null;
        vm.ShowPresetManager = manager => { opened = manager; return Task.CompletedTask; };

        await vm.ManagePresetsAsync();

        Assert.Same(presenter, opened?.ShowRoiEditor);
        Assert.True(opened?.HasRoiEditor);
    }
    #endregion
}
