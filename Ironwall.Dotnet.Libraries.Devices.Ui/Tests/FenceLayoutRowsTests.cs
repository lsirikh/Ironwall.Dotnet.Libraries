using Ironwall.Dotnet.Monitoring.Models.Fences;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 펜스 구성 저장소의 SQL 밖 규칙(<see cref="FenceLayoutRows"/>) · 순서를 바꿀 때 자리 종류가 센서를 따라가는가(<see cref="FenceLayoutMath.Reconcile"/>).
/// </summary>
public class FenceLayoutRowsTests
{
    [Theory]
    [InlineData("https://10.0.0.5:8000/api", "10.0.0.5:8000")]
    [InlineData("https://10.0.0.5:8000", "10.0.0.5:8000")]
    [InlineData("HTTPS://GOP.Example.LOCAL/api/v1", "gop.example.local:443")]
    [InlineData("http://127.0.0.1/api", "127.0.0.1:80")]
    [InlineData("10.0.0.5:8000", "10.0.0.5:8000")]
    [InlineData("  ", FenceLayoutRows.UNKNOWN_SERVER)]
    [InlineData(null, FenceLayoutRows.UNKNOWN_SERVER)]
    public void should_key_the_server_by_host_and_port_when_given_an_api_url(string? url, string expected)
        => Assert.Equal(expected, FenceLayoutRows.ServerKeyOf(url));

    [Fact]
    public void should_keep_production_and_loopback_test_servers_apart_when_the_controller_id_is_the_same()
    {
        var production = new FenceLayoutKey(FenceLayoutRows.ServerKeyOf("https://10.0.0.5:8000/api"), 10);
        var loopback = new FenceLayoutKey(FenceLayoutRows.ServerKeyOf("https://127.0.0.1:8000/api"), 10);

        Assert.NotEqual(production, loopback);
    }

    [Fact]
    public void should_tell_no_row_from_an_unreadable_row_when_interpreting_what_was_read()
    {
        var none = FenceLayoutRows.Interpret(false, 0, null, null);
        var broken = FenceLayoutRows.Interpret(true, 7, "{ not json", null);
        var good = FenceLayoutRows.Interpret(true, 3, FenceLayoutJson.Serialize(new FenceLayoutDocument { ControllerId = 10, Panels = new[] { FencePanelSpec.Default() } }), null);

        Assert.Equal(FenceLayoutLoadStatus.NotFound, none.Status);
        Assert.False(none.IsReadFailure);
        Assert.Equal(FenceLayoutLoadStatus.Unreadable, broken.Status);
        Assert.Equal(7, broken.RowRevision);
        Assert.True(broken.IsReadFailure);
        Assert.Equal(FenceLayoutLoadStatus.Loaded, good.Status);
        Assert.Equal(3, good.Document!.Revision);
    }

    [Fact]
    public void should_plan_insert_update_or_overwrite_when_given_the_loaded_revision_and_mode()
    {
        Assert.Equal(new FenceLayoutSavePlan(FenceLayoutSaveKind.Insert, 1), FenceLayoutRows.PlanSave(0, FenceLayoutSaveMode.Normal));
        Assert.Equal(new FenceLayoutSavePlan(FenceLayoutSaveKind.Update, 5), FenceLayoutRows.PlanSave(4, FenceLayoutSaveMode.Normal));
        Assert.Equal(FenceLayoutSaveKind.Overwrite, FenceLayoutRows.PlanSave(4, FenceLayoutSaveMode.Overwrite).Kind);
        Assert.Equal(FenceLayoutSaveKind.Overwrite, FenceLayoutRows.PlanSave(0, FenceLayoutSaveMode.Overwrite).Kind);
    }

    [Fact]
    public void should_report_a_conflict_only_when_a_revision_checked_write_touched_no_row()
    {
        var insert = FenceLayoutRows.PlanSave(0, FenceLayoutSaveMode.Normal);
        var update = FenceLayoutRows.PlanSave(4, FenceLayoutSaveMode.Normal);
        var overwrite = FenceLayoutRows.PlanSave(4, FenceLayoutSaveMode.Overwrite);

        Assert.Equal(FenceLayoutSaveStatus.Saved, FenceLayoutRows.ResultOf(insert, 0, 1).Status);
        Assert.Equal(FenceLayoutSaveStatus.Conflict, FenceLayoutRows.ResultOf(insert, 0, 0).Status);
        Assert.Equal(5, FenceLayoutRows.ResultOf(update, 4, 1).Revision);
        Assert.Equal(FenceLayoutSaveStatus.Conflict, FenceLayoutRows.ResultOf(update, 4, 0).Status);
        var over = FenceLayoutRows.ResultOf(overwrite, 4, 2, revisionAfter: 9);
        Assert.Equal(FenceLayoutSaveStatus.Saved, over.Status);
        Assert.Equal(9, over.Revision);
        Assert.Equal(FenceLayoutSaveStatus.Failed, FenceLayoutRows.ResultOf(overwrite, 4, 0, revisionAfter: null).Status);
    }

    [Fact]
    public void should_keep_spot_height_and_facing_with_each_sensor_when_two_sensors_swap_order()
    {
        // Arrange — 101 = 기둥 0 위(높이 +0.5 · 뒤), 102 = 망 0 가운데
        var panels = Enumerable.Repeat(FencePanelSpec.Default(), 2).ToList();
        var mounts = new Dictionary<int, SensorMountSpec>
        {
            [101] = new(0, FenceMountSpot.PostTop, 0.5, true),
            [102] = new(0, FenceMountSpot.PanelCenter),
        };

        // Act — 순서를 102, 101 로
        var (result, _) = FenceLayoutMath.Reconcile(new[] { 101, 102 }, new[] { 102, 101 }, mounts, panels, _ => FenceSensorCategory.Other);

        // Assert — 위치(망 번호)만 서로 주고받고 종류 · 높이 · 방향은 제 것
        Assert.Equal(FenceMountSpot.PanelCenter, result[102].Spot);
        Assert.Equal(FenceMountSpot.PostTop, result[101].Spot);
        Assert.Equal(0.5, result[101].HeightOffsetM);
        Assert.True(result[101].FacesBack);
        Assert.Equal(new[] { 102, 101 }, FenceLayoutMath.PositionOrder(result.Select(p => (p.Key, p.Value)), new[] { 102, 101 }));
    }
}
