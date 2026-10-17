using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Groups;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 장비 → 그룹 끌어 놓기의 판정(device-console-redesign FR-14). 화면 없이 순수 함수만 본다 —
/// 끌기 제스처 자체는 커널(Utils.Tests · 갤러리 재현)이 검증한다.
/// </summary>
public class DeviceGroupDropTests
{
    private static LampDeviceModel Lamp(int id, params int[] groups)
        => new() { Id = id, DeviceNumber = Math.Max(id, 1), DeviceName = $"L{id}", DeviceGroups = new List<int>(groups) };

    [Fact]
    public void should_send_only_saved_devices_not_in_group_when_planned()
    {
        var plan = DeviceGroupDrop.Plan(9, new[] { Lamp(1), Lamp(2, 9), Lamp(0), Lamp(3, 4) });

        Assert.True(plan.CanSend);
        Assert.Equal(new[] { 1, 3 }, plan.DeviceIds);
        Assert.Equal(1, plan.AlreadyIn);
        Assert.Equal(1, plan.DraftExcluded);
    }

    [Fact]
    public void should_block_when_group_is_not_saved()
    {
        var plan = DeviceGroupDrop.Plan(0, new[] { Lamp(1) });

        Assert.False(plan.CanSend);
        Assert.Empty(plan.DeviceIds);
    }

    [Fact]
    public void should_block_when_every_device_is_already_in_group()
    {
        var plan = DeviceGroupDrop.Plan(9, new[] { Lamp(1, 9), Lamp(2, 9) });

        Assert.False(plan.CanSend);
        Assert.Equal("이미 이 그룹에 들어 있습니다.", plan.BlockReason);
    }

    [Fact]
    public void should_block_when_only_drafts_are_dragged()
    {
        var plan = DeviceGroupDrop.Plan(9, new[] { Lamp(0), Lamp(-1) });

        Assert.False(plan.CanSend);
        Assert.Equal(2, plan.DraftExcluded);
    }

    [Fact]
    public void should_block_when_nothing_is_dragged()
    {
        Assert.False(DeviceGroupDrop.Plan(9, Array.Empty<IBaseDeviceModel>()).CanSend);
    }

    [Fact]
    public void should_send_each_device_once_when_same_device_appears_twice()
    {
        var lamp = Lamp(5);

        var plan = DeviceGroupDrop.Plan(9, new[] { lamp, lamp });

        Assert.Equal(new[] { 5 }, plan.DeviceIds);
    }

    [Fact]
    public void should_report_assigned_skipped_and_excluded_when_result_line_is_built()
    {
        var plan = DeviceGroupDrop.Plan(9, new[] { Lamp(1), Lamp(2), Lamp(3, 9), Lamp(0) });

        var line = DeviceGroupDrop.ResultLine("동측", plan, assigned: new[] { 1 }, skipped: new[] { 2 });

        Assert.Contains("'동측'에 1대를 넣었습니다", line);
        Assert.Contains("1대는 넣지 못했습니다", line);
        Assert.Contains("1대는 이미 들어 있어 건너뛰었습니다", line);
        Assert.Contains("저장 전 1대", line);
    }

    [Fact]
    public void should_unwrap_models_from_row_view_models_when_payload_holds_rows()
    {
        var model = Lamp(4);
        var rows = new object[] { new RowStub(model), model, "not a row" };

        var models = DeviceGroupDropHandler.ModelsOf(rows);

        Assert.Equal(2, models.Count);
        Assert.All(models, m => Assert.Same(model, m));
    }

    private sealed class RowStub
    {
        public RowStub(IBaseDeviceModel model) => Model = model;
        public IBaseDeviceModel Model { get; }
    }
}
