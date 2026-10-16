using System.Linq;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>지도 3D 2차 — 오버레이 DTO · patch 비교 · 메시지, pick/resync 해석, 입력 모드 · 이동 후 판정.</summary>
public class Map3DOverlayTests
{
    private static Map3DOverlayPoint Pids(EnumCompositeEventStatus composite = EnumCompositeEventStatus.Normal,
                                          bool visible = true, bool layer = true, string title = "카메라 1")
        => Map3DOverlay.Point(Map3DOverlay.KindPids, 7, title, 37.5, 127.1, 3.5f, 90,
            EnumDeviceType.IpCamera, "camera.ptz", composite, EnumOperationState.ACTIVATED, EnumDoorState.Unknown,
            showTitle: true, modelVisible: visible, layerEnabled: layer, minZoom: 15);

    // ── DTO ──

    [Fact]
    public void should_copy_enum_names_and_fields_when_building_pids_point()
    {
        var o = Map3DOverlay.ToJson(Pids(EnumCompositeEventStatus.FaultedDetecting));

        Assert.Equal("pids", o.Value<string>("kind"));
        Assert.Equal(7, o.Value<int>("id"));
        Assert.Equal("카메라 1", o.Value<string>("title"));
        Assert.Equal(37.5, o.Value<double>("lat"));
        Assert.Equal(127.1, o.Value<double>("lon"));
        Assert.Equal(3.5, o.Value<double>("altM"));
        Assert.Equal(90, o.Value<double>("bearing"));
        Assert.Equal("IpCamera", o.Value<string>("deviceType"));
        Assert.Equal("camera.ptz", o.Value<string>("model"));
        Assert.Equal("FaultedDetecting", o["state"]!.Value<string>("composite"));
        Assert.Equal("ACTIVATED", o["state"]!.Value<string>("operation"));
        Assert.Equal("Unknown", o["state"]!.Value<string>("door"));
        Assert.True(o["label"]!.Value<bool>("show"));
        Assert.True(o.Value<bool>("visible"));
        Assert.Equal(15, o.Value<double>("minZoom"));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public void should_combine_model_visible_and_layer_when_building_visible(bool visible, bool layer, bool expected)
        => Assert.Equal(expected, Pids(visible: visible, layer: layer).Visible);

    [Fact]
    public void should_null_device_fields_when_building_plain_symbol()
    {
        var o = Map3DOverlay.ToJson(Map3DOverlay.Point(Map3DOverlay.KindSymbol, 3, null, 1, 2, double.NaN, double.PositiveInfinity,
            null, null, null, EnumOperationState.NONE, null, false, true, true, 0));

        Assert.Equal("symbol", o.Value<string>("kind"));
        Assert.Equal("", o.Value<string>("title"));
        Assert.Equal(JTokenType.Null, o["deviceType"]!.Type);
        Assert.Equal(JTokenType.Null, o["model"]!.Type);
        Assert.Equal(JTokenType.Null, o["state"]!["composite"]!.Type);
        Assert.Equal(JTokenType.Null, o["state"]!["door"]!.Type);
        Assert.Equal("NONE", o["state"]!.Value<string>("operation"));
        Assert.Equal(0, o.Value<double>("altM"));      // NaN → 0
        Assert.Equal(0, o.Value<double>("bearing"));   // ∞ → 0
    }

    [Fact]
    public void should_fill_fence_defaults_when_group_fence_values_missing()
    {
        var line = Map3DOverlay.Line(Map3DOverlay.KindPidsGroup, 11, "구역 A", new[] { (37.0, 127.0), (37.1, 127.2) }, true,
            "#F44336", 2.5, hasFence: true, fenceHeightM: null, postSpacingM: 4.0, render3D: true,
            EnumCompositeEventStatus.Detecting, true, true, 16);
        var o = Map3DOverlay.ToJson(line);

        Assert.Equal("pidsGroup", o.Value<string>("kind"));
        Assert.Equal(new[] { 37.0, 127.0 }, o["pts"]![0]!.Values<double>().ToArray());
        Assert.Equal(new[] { 37.1, 127.2 }, o["pts"]![1]!.Values<double>().ToArray());
        Assert.True(o.Value<bool>("closed"));
        Assert.Equal("#F44336", o.Value<string>("stroke"));
        Assert.Equal(2.5, o.Value<double>("strokeW"));
        Assert.Equal(2.4, o["fence"]!.Value<double>("heightM"));     // FenceDefaults.FenceHeightM
        Assert.Equal(4.0, o["fence"]!.Value<double>("postSpacingM"));
        Assert.True(o["fence"]!.Value<bool>("render3d"));
        Assert.Equal("Detecting", o["state"]!.Value<string>("composite"));
    }

    [Fact]
    public void should_send_null_fence_and_empty_points_when_plain_line_has_no_points()
    {
        var o = Map3DOverlay.ToJson(Map3DOverlay.Line(Map3DOverlay.KindLine, 2, "선", null, false, "#2196F3", 1,
            hasFence: false, null, null, false, null, true, false, 0));

        Assert.Equal(JTokenType.Null, o["fence"]!.Type);
        Assert.Empty(o["pts"]!);
        Assert.Equal(JTokenType.Null, o["state"]!["composite"]!.Type);
        Assert.False(o.Value<bool>("visible"));
    }

    // ── patch 비교(코얼레싱 뒤 보낼지 판정) ──

    [Fact]
    public void should_return_null_when_nothing_changed()
        => Assert.Null(Map3DOverlay.Diff(Map3DOverlay.ToJson(Pids()), Map3DOverlay.ToJson(Pids())));

    [Fact]
    public void should_patch_only_changed_state_subfield_when_status_changes()
    {
        var set = Map3DOverlay.Diff(Map3DOverlay.ToJson(Pids()), Map3DOverlay.ToJson(Pids(EnumCompositeEventStatus.Detecting)))!;

        Assert.Single(set.Properties());
        var state = (JObject)set["state"]!;
        Assert.Single(state.Properties());
        Assert.Equal("Detecting", state.Value<string>("composite"));
    }

    [Fact]
    public void should_patch_title_and_visible_when_they_change()
    {
        var set = Map3DOverlay.Diff(Map3DOverlay.ToJson(Pids()), Map3DOverlay.ToJson(Pids(layer: false, title: "새 이름")))!;

        Assert.Equal(new[] { "title", "visible" }, set.Properties().Select(p => p.Name).OrderBy(n => n).ToArray());
        Assert.False(set.Value<bool>("visible"));
        Assert.Null(set["kind"]);
        Assert.Null(set["id"]);
    }

    // ── 메시지 ──

    [Fact]
    public void should_build_overlay_messages_with_rev()
    {
        var item = Map3DOverlay.ToJson(Pids());
        var snap = JObject.Parse(Map3DOverlay.SnapshotJson(1, new[] { item }, System.Array.Empty<JObject>()));
        Assert.Equal("overlay.snapshot", snap.Value<string>("type"));
        Assert.Equal(1, snap.Value<long>("rev"));
        Assert.Single(snap["points"]!);
        Assert.Empty(snap["lines"]!);

        var patch = JObject.Parse(Map3DOverlay.PatchJson(2, "pids", 7, new JObject { ["visible"] = false }));
        Assert.Equal("overlay.patch", patch.Value<string>("type"));
        Assert.Equal(2, patch.Value<long>("rev"));
        Assert.Equal("pids", patch.Value<string>("kind"));
        Assert.Equal(7, patch.Value<int>("id"));
        Assert.False(patch["set"]!.Value<bool>("visible"));

        var add = JObject.Parse(Map3DOverlay.AddJson(3, item));
        Assert.Equal("overlay.add", add.Value<string>("type"));
        Assert.Equal(7, add["item"]!.Value<int>("id"));

        var remove = JObject.Parse(Map3DOverlay.RemoveJson(4, "line", 9));
        Assert.Equal("overlay.remove", remove.Value<string>("type"));
        Assert.Equal(4, remove.Value<long>("rev"));
        Assert.Equal("line", remove.Value<string>("kind"));
        Assert.Equal(9, remove.Value<int>("id"));
    }

    [Fact]
    public void should_separate_keys_by_kind_when_ids_collide()
        => Assert.NotEqual(Map3DOverlay.Key("pids", 1), Map3DOverlay.Key("symbol", 1));

    // ── 3D → GIS 해석 ──

    [Fact]
    public void should_parse_pick_when_marker_clicked()
    {
        var msg = Map3DMath.ParseMessage("""{"type":"pick","action":"dblclick","kind":"pidsGroup","id":12,"screen":{"x":100.5,"y":40}}""");

        Assert.NotNull(msg);
        Assert.Equal("pick", msg!.Type);
        Assert.Equal(new Map3DPick("dblclick", "pidsGroup", 12, 100.5, 40), msg.Pick);
    }

    [Fact]
    public void should_parse_empty_pick_when_kind_or_id_missing()
    {
        var pick = Map3DMath.ParseMessage("""{"type":"pick","action":"click","kind":"pids","id":null}""")!.Pick!;

        Assert.Null(pick.Kind);
        Assert.Null(pick.Id);
        Assert.Null(pick.ScreenX);
        Assert.Null(pick.ScreenY);
    }

    [Theory]
    [InlineData("""{"type":"pick","action":"hover","kind":"pids","id":1}""")]
    [InlineData("""{"type":"pick","kind":"pids","id":1}""")]
    public void should_return_null_when_pick_action_unknown(string json)
        => Assert.Null(Map3DMath.ParseMessage(json));

    [Fact]
    public void should_parse_resync_when_3d_requests_snapshot()
    {
        var msg = Map3DMath.ParseMessage("""{"type":"overlay.resync"}""");

        Assert.Equal("overlay.resync", msg!.Type);
        Assert.Null(msg.Pick);
        Assert.Null(msg.Camera);
    }

    // ── 입력 모드 · 이동 후 판정 ──

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public void should_block_3d_when_edit_or_exclusive_mode(bool edit, bool exclusive, bool expected)
        => Assert.Equal(expected, Map3DMath.IsBlockedByInputMode(edit, exclusive));

    [Theory]
    [InlineData(16.5, true, true)]
    [InlineData(16.49, true, false)]
    [InlineData(18, false, false)]
    [InlineData(double.NaN, true, false)]
    public void should_stay_3d_after_move_when_inside_scene_and_zoom_enough(double zoom, bool inside, bool expected)
        => Assert.Equal(expected, Map3DMath.ShouldStay3DAfterMove(zoom, inside));
}
