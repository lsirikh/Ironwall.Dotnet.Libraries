using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/****************************************************************************
   Purpose      : 부대 콘솔이 "다른 곳에서 편제가 바뀌었다"(SYNC_UNIT → UnitTopologyChangedMessage)를 받았을 때
                  — 깨끗하면 몰려온 알림을 한 번의 재조회로 합치고, 적용 안 한 편집 · 끌기 중이면 덮지 않고 알린다.
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
[Collection("CaliburnIoC")]
public class UnitConsoleExternalChangeTests
{
    #region - Fakes -
    /// <summary>창(500 ms)이 끝나는 순간을 시험이 정한다 — sleep 없이.</summary>
    private sealed class ManualDelay
    {
        private readonly List<TaskCompletionSource> _pending = new();
        public int Requested { get; private set; }

        public Task Delay(TimeSpan window, CancellationToken token)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_pending) { _pending.Add(tcs); Requested++; }
            token.Register(() => tcs.TrySetCanceled());
            return tcs.Task;
        }

        public void ReleaseAll()
        {
            List<TaskCompletionSource> now;
            lock (_pending) { now = _pending.ToList(); _pending.Clear(); }
            foreach (var t in now) t.TrySetResult();
        }
    }

    private sealed class FakeUnitApi : IUnitGraphApi
    {
        public bool IsAvailable => true;
        public UnitGraphDto Graph { get; set; } = new();
        public int GraphReads { get; private set; }
        public int DetailReads { get; private set; }

        public Task<ApiResponse<UnitGraphDto>> GetGraphAsync(CancellationToken token = default)
        {
            GraphReads++;
            return Task.FromResult(ApiResponse<UnitGraphDto>.CreateSuccess(Graph));
        }

        public Task<ApiResponse<UnitDetailDto>> GetDetailAsync(int unitId, CancellationToken token = default)
        {
            DetailReads++;
            var node = Graph.Nodes.FirstOrDefault(n => n.Id == unitId);
            return Task.FromResult(ApiResponse<UnitDetailDto>.CreateSuccess(new UnitDetailDto
            {
                Id = unitId, Code = node?.Code ?? "x", Name = node?.Name ?? "x",
                EchelonRaw = node?.EchelonRaw ?? "Company", ParentId = node?.ParentId, IsEnable = true,
            }));
        }

        public Task<ApiResponse<UnitDto>> CreateAsync(UnitCreateDto dto, CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitDto>.CreateSuccess(new UnitDto { Id = 900 }));
        public Task<ApiResponse<UnitDto>> PatchAsync(int unitId, UnitUpdateDto dto, CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitDto>.CreateSuccess(new UnitDto { Id = unitId }));
        public Task<ApiResponse<UnitDeleteResultDto>> DeleteAsync(int unitId, CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitDeleteResultDto>.CreateSuccess(new UnitDeleteResultDto { Id = unitId }));
    }

    private sealed class FakeDeviceApi : IUnitDeviceApi
    {
        public bool IsAvailable => true;
        public List<UnitDeviceItem> Items { get; } = new();
        public Task<UnitDeviceLoadResult> LoadAllAsync(CancellationToken token = default)
            => Task.FromResult(new UnitDeviceLoadResult(Items.ToList(), Array.Empty<string>()));
        public Task<UnitDeviceAssignResult> AssignAsync(UnitDeviceItem device, int unitId, CancellationToken token = default)
            => Task.FromResult(new UnitDeviceAssignResult(true, "바꿨습니다"));
    }
    #endregion

    #region - Fixture -
    private static UnitListDto Node(int id, string code, string echelon, int? parentId = null)
        => new() { Id = id, Code = code, Name = code, EchelonRaw = echelon, ParentId = parentId };

    private static UnitGraphDto Sample() => new()
    {
        Nodes = new List<UnitListDto> { Node(1, "d01", "Division"), Node(2, "r01", "Regiment", 1), Node(6, "c06", "Company", 2) },
        Edges = new UnitGraphEdgesDto
        {
            Hierarchy = new List<List<int>> { new() { 1, 2 }, new() { 2, 6 } },
            Adjacency = new List<List<int>>(),
        },
    };

    private sealed record Harness(UnitConsoleViewModel Console, FakeUnitApi Units, FakeDeviceApi Devices, ManualDelay Delay);

    private static bool _dragging;

    private static async Task<Harness> OpenAsync()
    {
        _dragging = false;
        var units = new FakeUnitApi { Graph = Sample() };
        var devices = new FakeDeviceApi();
        devices.Items.Add(new UnitDeviceItem(501, 1, "센서-1", EnumDeviceCategory.Sensor, null));
        var delay = new ManualDelay();
        var console = new UnitConsoleViewModel(units, devices, log: null, myUnitCode: () => "c06",
                                               canEdit: () => true, canDelete: () => true,
                                               isDragging: () => _dragging, delay: delay.Delay);
        await ((IActivate)console).ActivateAsync();
        return new Harness(console, units, devices, delay);
    }

    private static UnitTopologyChangedMessage Changed(int id = 6) => new("UPDATED", id);

    /// <summary>알림 한 건을 보내고 창이 끝나기 전의 작업을 돌려준다.</summary>
    private static async Task<Task> SendAsync(UnitConsoleViewModel console, UnitTopologyChangedMessage message)
    {
        await console.HandleAsync(message, CancellationToken.None);
        return console.ExternalChangeTask;
    }
    #endregion

    [Fact]
    public async Task should_reload_the_graph_once_when_a_burst_of_changes_arrives_and_nothing_is_pending()
    {
        var h = await OpenAsync();
        var readsBefore = h.Units.GraphReads;
        h.Units.Graph.Nodes.First(n => n.Id == 6).Name = "제6중대(개칭)";

        Task last = Task.CompletedTask;
        for (var i = 0; i < 5; i++) last = await SendAsync(h.Console, Changed());
        h.Delay.ReleaseAll();
        await last;

        Assert.Equal(readsBefore + 1, h.Units.GraphReads);     // 다섯 건 → 재조회 한 번
        Assert.Equal("제6중대(개칭)", h.Console.Rows.First(r => r.Id == 6).Name);
        Assert.False(h.Console.IsExternallyChanged);
    }

    [Fact]
    public async Task should_keep_the_last_action_message_when_the_background_reload_runs()
    {
        var h = await OpenAsync();
        await h.Console.MoveAsync(6, 1);
        var said = h.Console.StatusText;

        var pending = await SendAsync(h.Console, Changed());
        h.Delay.ReleaseAll();
        await pending;

        Assert.Equal(said, h.Console.StatusText);     // "옮겼습니다" 를 "부대 N개를 불러왔습니다" 로 덮지 않는다
    }

    [Fact]
    public async Task should_not_reload_and_show_the_notice_when_the_detail_has_unapplied_edits()
    {
        var h = await OpenAsync();
        await h.Console.SelectByIdAsync(6);
        h.Console.Detail.Tracker.Touch("name", "c06", "고치는 중");
        var readsBefore = h.Units.GraphReads;

        var pending = await SendAsync(h.Console, Changed());
        h.Delay.ReleaseAll();
        await pending;

        Assert.Equal(readsBefore, h.Units.GraphReads);
        Assert.True(h.Console.IsExternallyChanged);
        Assert.Equal(UnitConsoleViewModel.EXTERNAL_CHANGE_NOTICE, h.Console.ExternalChangeText);
        Assert.True(h.Console.Detail.IsDirty);                 // 사용자의 편집은 그대로다
    }

    [Fact]
    public async Task should_hold_the_reload_during_a_drag_and_run_it_after_the_drag_ends()
    {
        var h = await OpenAsync();
        var readsBefore = h.Units.GraphReads;
        _dragging = true;

        var first = await SendAsync(h.Console, Changed());
        h.Delay.ReleaseAll();
        await first;

        Assert.Equal(readsBefore, h.Units.GraphReads);
        Assert.True(h.Console.IsExternallyChanged);

        _dragging = false;                                     // 놓았다 — 미뤄 둔 재조회가 다음 창에서 돈다
        var retry = h.Console.ExternalChangeTask;
        Assert.NotSame(first, retry);
        h.Delay.ReleaseAll();
        await retry;

        Assert.Equal(readsBefore + 1, h.Units.GraphReads);
        Assert.False(h.Console.IsExternallyChanged);
    }

    [Fact]
    public async Task should_clear_the_notice_when_the_person_reads_again()
    {
        var h = await OpenAsync();
        await h.Console.SelectByIdAsync(6);
        h.Console.Detail.Tracker.Touch("name", "c06", "고치는 중");
        var pending = await SendAsync(h.Console, Changed());
        h.Delay.ReleaseAll();
        await pending;
        Assert.True(h.Console.IsExternallyChanged);

        h.Console.Revert();                                    // 편집을 버리고
        await h.Console.ReloadAsync();                         // [다시 읽기]

        Assert.False(h.Console.IsExternallyChanged);
    }

    [Fact]
    public async Task should_refresh_the_selected_detail_and_drop_it_when_the_unit_was_deleted_elsewhere()
    {
        var h = await OpenAsync();
        await h.Console.SelectByIdAsync(6);
        var detailsBefore = h.Units.DetailReads;

        // 이름만 바뀐 경우 — 상세도 새로 읽는다(옛 DTO 를 붙들면 다음 PATCH 가 옛 값을 다시 보낸다).
        h.Units.Graph.Nodes.First(n => n.Id == 6).Name = "제6중대(개칭)";
        var pending = await SendAsync(h.Console, Changed());
        h.Delay.ReleaseAll();
        await pending;
        Assert.Equal(detailsBefore + 1, h.Units.DetailReads);
        Assert.Equal(6, h.Console.SelectedRow?.Id);

        // 지워진 경우 — 고른 것을 놓는다.
        h.Units.Graph.Nodes.RemoveAll(n => n.Id == 6);
        h.Units.Graph.Edges.Hierarchy.RemoveAll(e => e[1] == 6);
        pending = await SendAsync(h.Console, new UnitTopologyChangedMessage("DELETED", 6));
        h.Delay.ReleaseAll();
        await pending;
        Assert.Null(h.Console.SelectedRow);
        Assert.False(h.Console.IsDetailRequested && h.Console.Form.Original is UnitDetailDto { Id: 6 });
    }

    [Fact]
    public async Task should_ignore_changes_after_the_console_is_closed()
    {
        var h = await OpenAsync();
        var readsBefore = h.Units.GraphReads;
        await ((IDeactivate)h.Console).DeactivateAsync(close: true);

        await h.Console.HandleAsync(Changed(), CancellationToken.None);
        h.Delay.ReleaseAll();
        await h.Console.ExternalChangeTask;

        Assert.Equal(readsBefore, h.Units.GraphReads);
    }
}
