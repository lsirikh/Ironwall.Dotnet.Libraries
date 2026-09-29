using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : 이벤트 맵핑 워크벤치의 끌어 놓기 — 실창에서 잡힌 결함의 회귀망(M-1)
   Created By   : Claude
   Created On   : 2026-09-29
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 2026-09-29 사용자 보고("여기에 드래그 드롭 버그 오진다") — 실창(화면 밖 Window · 실제 캡처) 재현으로 잡은 것 중
/// 이 창(뷰모델 · 마크업)에 원인이 있던 것들.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>맵핑이 없거나 고르지 않았으면 손잡이가 아무 반응도 없었다 — 끌기 시작 자체가 꺼져 있어 거절 사유를 말할 기회도 없었다.</item>
/// <item>거절된 드롭(이미 등록된 장비 · 맵핑 없음)이 말없이 사라졌다.</item>
/// <item>보드가 비어 있을 때 가운데 안내 글("팔레트에서 장비를 끌어다 놓거나…")이 히트를 가로채 그 위에서 놓으면 아무 일도 없었다.</item>
/// <item>보드가 바뀔 때마다 행 뷰모델을 통째로 새로 만들어 목록의 선택이 날아갔다 — 화면은 선택 0 인데 상태줄은 "선택 N건",
/// <c>Alt+↑↓</c> 는 한 번만 움직이고 멈췄다. 팔레트도 통째로 다시 채워, 끄는 도중 보드가 다시 읽히면 잡은 카드가 사라졌다.</item>
/// </list>
/// 커널 쪽 원인(고스트가 팔레트 칸에 갇힘 · 다시 그려지면 끌기가 영영 안 끝남)은 <c>Utils.Tests/CaptureDragLifecycleTests</c>.
/// </remarks>
public class MappingDragInteractionTests
{
    private static async Task<(MappingWorkbenchViewModel Vm, CountingGateway Gateway)> LoadedAsync(int rows = 3)
    {
        var gateway = new CountingGateway();
        for (var i = 0; i < rows; i++)
            gateway.Cameras.Add(new MappingCameraReadDto
            {
                ConfigId = 700 + i, EventMappingId = 1, IsEnable = true, UpdatedAt = "T0",
                Camera = new MappingDeviceRefDto { Id = 370 + i, CategoryDevice = "Camera" },
            });
        var vm = new MappingWorkbenchViewModel(gateway, new StubDevices());
        await vm.ReloadAsync();
        return (vm, gateway);
    }

    private static MappingPaletteItemViewModel Card(int deviceId)
        => new(MappingActionKind.Camera, new MappingDeviceInfo(deviceId, $"카메라{deviceId}", "PTZ", new[] { 3 }, true));

    private static DropTarget CameraBoard(object data, int index = 0) => new(MappingKindText.BoardZone(MappingActionKind.Camera), data, index);

    #region - 맵핑 없음 : 끌기는 시작되고, 거절 사유를 말한다 -
    [Fact]
    public void should_let_the_palette_start_a_drag_when_no_mapping_is_chosen_yet()
    {
        // 끌기를 아예 꺼 두면 손잡이가 죽은 것처럼 보이고 "왜" 를 말할 곳이 없다 — 보드가 거절 모양(해치)과 사유로 답한다.
        var vm = new MappingWorkbenchViewModel(new CountingGateway(), new StubDevices());

        Assert.False(vm.HasMapping);
        Assert.True(vm.IsDragEnabled);
    }

    [Fact]
    public void should_refuse_with_a_create_first_reason_when_there_is_no_mapping_at_all()
    {
        var vm = new MappingWorkbenchViewModel(new CountingGateway(), new StubDevices());

        var verdict = OnSta(() => vm.Verdict(new DragPayload(new ListBox(), new object[] { Card(370) }, "카메라370"), CameraBoard(vm)));

        Assert.False(verdict.IsAllowed);
        Assert.Equal(MappingEligibility.NoMappingYetReason, verdict.Reason);
    }

    [Fact]
    public async Task should_say_why_on_the_status_line_when_a_refused_drop_is_released()
    {
        // 이미 등록된 카메라(370)를 다시 끌어다 놓았다 — 커널이 거절을 알리면 상태줄이 사유를 말한다.
        var (vm, _) = await LoadedAsync();

        OnSta(() =>
        {
            ((IDropRefusalHandler)vm).Refused(new DragPayload(new ListBox(), new object[] { Card(370) }, "카메라370"), CameraBoard(vm));
            return 0;
        });

        Assert.Equal("고른 장비가 이미 전부 등록되어 있습니다.", vm.StatusText);
        Assert.Equal(3, vm.BoardRows.Count);            // 아무것도 넣지 않았다
    }
    #endregion

    #region - 보드 · 팔레트는 다시 만들지 않고 맞춘다 -
    [Fact]
    public async Task should_keep_the_same_row_view_model_when_a_row_moves_so_the_list_keeps_its_selection()
    {
        var (vm, _) = await LoadedAsync();
        var moving = vm.BoardRows[2];
        var resets = Record(vm.BoardRows);
        vm.SelectedBoardRows.Clear();
        vm.SelectedBoardRows.Add(moving);
        vm.OnSelectionChanged();

        vm.MoveUp();

        Assert.Same(moving, vm.BoardRows[1]);
        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, resets);
        Assert.DoesNotContain(NotifyCollectionChangedAction.Remove, resets);   // 옮김은 Move 이지 빼고 넣기가 아니다
        Assert.Same(moving, Assert.Single(vm.SelectedBoardRows));
    }

    [Fact]
    public async Task should_step_the_same_row_again_when_moved_twice_in_a_row()
    {
        // Alt+↑ 를 두 번 — 첫 번째 뒤에도 같은 행을 쥐고 있어야 한다.
        var (vm, _) = await LoadedAsync();
        var moving = vm.BoardRows[2];
        vm.SelectedBoardRows.Add(moving);
        vm.OnSelectionChanged();

        vm.MoveUp();
        vm.MoveUp();

        Assert.Same(moving, vm.BoardRows[0]);
        Assert.Equal(new int?[] { 372, 370, 371 }, vm.BoardRows.Select(r => r.Row.DeviceId).ToArray());
    }

    [Fact]
    public async Task should_refresh_the_row_marks_in_place_when_a_row_is_released()
    {
        // 해제는 같은 행을 취소선으로 남긴다 — 다시 만들지 않으니 표시(IsRemoved)를 제자리에서 새로 알려야 한다.
        var (vm, _) = await LoadedAsync();
        var row = vm.BoardRows[0];
        var changed = new List<string?>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        vm.SelectedBoardRows.Add(row);
        vm.OnSelectionChanged();

        vm.ReleaseSelected();

        Assert.Same(row, vm.BoardRows[0]);
        Assert.True(row.IsRemoved);
        Assert.True(changed.Contains(nameof(MappingRowViewModel.IsRemoved)) || changed.Contains(string.Empty) || changed.Contains(null));
    }

    [Fact]
    public async Task should_not_rebuild_the_palette_when_the_board_changes_so_a_held_card_survives()
    {
        var (vm, _) = await LoadedAsync();
        var before = vm.PaletteItems.ToList();
        var changes = Record(vm.PaletteItems);

        vm.AddDevices(new[] { 374 }, -1);
        await vm.RevertAsync();                         // 보드를 서버에서 다시 읽는다 — 끄는 도중에 끝날 수 있는 일

        Assert.Equal(before, vm.PaletteItems.ToList()); // 같은 카드 객체 그대로(컨테이너가 살아남는다)
        Assert.Empty(changes);
        Assert.True(vm.PaletteItems.Single(p => p.Id == 370).IsRegistered);
        Assert.False(vm.PaletteItems.Single(p => p.Id == 374).IsRegistered);
    }

    [Fact]
    public async Task should_keep_palette_order_and_filter_when_search_changes()
    {
        var (vm, _) = await LoadedAsync();

        vm.PaletteSearch = "373";
        Assert.Equal(new[] { 373 }, vm.PaletteItems.Select(p => p.Id).ToArray());
        vm.PaletteSearch = string.Empty;

        Assert.Equal(new[] { 370, 371, 372, 373, 374, 375 }, vm.PaletteItems.Select(p => p.Id).ToArray());
    }
    #endregion

    #region - 마크업 : 빈 보드 안내 글이 드롭을 가로채지 않는다 -
    [Fact]
    public void should_let_drops_through_the_empty_board_hint()
    {
        var markup = Markup();
        var hint = Regex.Match(markup, @"<TextBlock(?<body>[^>]*?끌어다 놓거나[^>]*?)>", RegexOptions.Singleline);

        Assert.True(hint.Success, "빈 보드 안내 글을 찾지 못했다");
        Assert.Contains("IsHitTestVisible=\"False\"", hint.Groups["body"].Value, StringComparison.Ordinal);
    }

    private static string Markup()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Ironwall.Dotnet.Libraries.Events.Ui.csproj")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, "Consoles", "Mapping", "MappingWorkbenchView.xaml"));
    }
    #endregion

    private static List<NotifyCollectionChangedAction> Record(INotifyCollectionChanged collection)
    {
        var actions = new List<NotifyCollectionChangedAction>();
        collection.CollectionChanged += (_, e) => actions.Add(e.Action);
        return actions;
    }

    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) throw new InvalidOperationException("STA body failed", error);
        return result;
    }
}
