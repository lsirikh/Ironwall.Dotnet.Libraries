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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/****************************************************************************
   Purpose      : 부대 콘솔 — 편제를 다시 읽어도 트리 목록(실제 WPF ListBox)의 선택이 살아남는가
                  (헤디드 3회차 SC-UNT-022: SYNC_UNIT 재조회 뒤 "고른 부대가 다른 곳에서 삭제되었습니다" · [옮기기] 무반응)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 재조회는 행 인스턴스를 전부 새로 만든다(<c>RestoreSelection</c> 이 캐시를 비운다). 옛 인스턴스가 목록에서 빠지는 순간
/// WPF <see cref="Selector"/> 는 선택을 비우고 <c>SelectionChanged(null)</c> 를 올리며, 뷰는 그것을 그대로
/// <see cref="UnitConsoleViewModel.SelectRowAsync"/> 로 옮긴다 — 그래서 <b>아직 있는 부대</b>의 선택이 사라졌다.
/// 가짜 목록으로는 이 Selector 거동이 재현되지 않으므로 실제 <see cref="ListBox"/> 를 STA 에 띄워 뷰와 같은 결선
/// (<see cref="UnitTreeSelectionBridge"/>)으로 붙인다.
/// </summary>
[Collection("CaliburnIoC")]
public class UnitConsoleTreeSelectionReloadTests
{
    #region - Fakes -
    private sealed class ManualDelay
    {
        private readonly List<TaskCompletionSource> _pending = new();

        public Task Delay(TimeSpan window, CancellationToken token)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_pending) _pending.Add(tcs);
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
        public int Patches { get; private set; }

        public Task<ApiResponse<UnitGraphDto>> GetGraphAsync(CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitGraphDto>.CreateSuccess(Graph));

        public Task<ApiResponse<UnitDetailDto>> GetDetailAsync(int unitId, CancellationToken token = default)
        {
            var node = Graph.Nodes.FirstOrDefault(n => n.Id == unitId);
            return Task.FromResult(ApiResponse<UnitDetailDto>.CreateSuccess(new UnitDetailDto
            {
                Id = unitId, Code = node?.Code ?? "x", Name = node?.Name ?? "x",
                EchelonRaw = node?.EchelonRaw ?? "Company", ParentId = node?.ParentId, IsEnable = true,
            }));
        }

        /// <summary>서버처럼 편제에 새 부대를 넣는다 — 다음 <c>/graph</c> 에 나온다.</summary>
        public Task<ApiResponse<UnitDto>> CreateAsync(UnitCreateDto dto, CancellationToken token = default)
        {
            const int NEW_ID = 900;
            Graph.Nodes.Add(new UnitListDto { Id = NEW_ID, Code = dto.Code, Name = dto.Name, EchelonRaw = "Company", ParentId = dto.ParentId });
            if (dto.ParentId is int parentId) Graph.Edges.Hierarchy.Add(new List<int> { parentId, NEW_ID });
            return Task.FromResult(ApiResponse<UnitDto>.CreateSuccess(new UnitDto { Id = NEW_ID }));
        }

        public Task<ApiResponse<UnitDto>> PatchAsync(int unitId, UnitUpdateDto dto, CancellationToken token = default)
        {
            Patches++;
            return Task.FromResult(ApiResponse<UnitDto>.CreateSuccess(new UnitDto { Id = unitId }));
        }

        public Task<ApiResponse<UnitDeleteResultDto>> DeleteAsync(int unitId, CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitDeleteResultDto>.CreateSuccess(new UnitDeleteResultDto { Id = unitId }));
    }

    private sealed class FakeDeviceApi : IUnitDeviceApi
    {
        public bool IsAvailable => true;
        public Task<UnitDeviceLoadResult> LoadAllAsync(CancellationToken token = default)
            => Task.FromResult(new UnitDeviceLoadResult(new List<UnitDeviceItem>(), Array.Empty<string>()));
        public Task<UnitDeviceAssignResult> AssignAsync(UnitDeviceItem device, int unitId, CancellationToken token = default)
            => Task.FromResult(new UnitDeviceAssignResult(true, "바꿨습니다"));
    }
    #endregion

    #region - Fixture -
    private const string SELECTED_UNIT_DELETED = "고른 부대가 다른 곳에서 삭제되었습니다.";

    private static UnitListDto Node(int id, string code, string echelon, int? parentId = null)
        => new() { Id = id, Code = code, Name = code, EchelonRaw = echelon, ParentId = parentId };

    private static UnitGraphDto Sample() => new()
    {
        Nodes = new List<UnitListDto>
        {
            Node(1, "d01", "Division"), Node(2, "r01", "Regiment", 1), Node(3, "b01", "Battalion"), Node(6, "c06", "Company", 2),
        },
        Edges = new UnitGraphEdgesDto
        {
            Hierarchy = new List<List<int>> { new() { 1, 2 }, new() { 2, 6 } },
            Adjacency = new List<List<int>>(),
        },
    };

    private sealed record Harness(UnitConsoleViewModel Console, FakeUnitApi Units, ManualDelay Delay, ListBox Tree, Window Window);

    private static async Task<Harness> OpenAsync()
    {
        var units = new FakeUnitApi { Graph = Sample() };
        var delay = new ManualDelay();
        var console = new UnitConsoleViewModel(units, new FakeDeviceApi(), log: null, myUnitCode: () => null,
                                               canEdit: () => true, canDelete: () => true,
                                               isDragging: () => false, delay: delay.Delay);
        await ((IActivate)console).ActivateAsync();

        // UnitConsoleView.xaml 의 트리와 같은 결선 — ItemsSource=Rows · SelectedItem OneWay · SelectionChanged → SelectRowAsync.
        var tree = new ListBox { ItemsSource = console.Rows, SelectionMode = SelectionMode.Single };
        tree.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(UnitConsoleViewModel.SelectedRow)) { Source = console, Mode = BindingMode.OneWay });
        // 뷰(UnitConsoleView.OnTreeSelectionChanged)가 쓰는 바로 그 몸통.
        var bridge = new UnitTreeSelectionBridge();
        tree.SelectionChanged += async (_, e) => await bridge.OnSelectionChangedAsync(tree, e, console);

        var window = new Window
        {
            Content = tree, Width = 300, Height = 400, ShowInTaskbar = false, WindowStyle = WindowStyle.None,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000, ShowActivated = false,
        };
        window.Show();
        return new Harness(console, units, delay, tree, window);
    }

    /// <summary>STA 스레드에 디스패처 동기화 문맥을 깔고 본문을 끝까지 돌린다(ConfigureAwait(true) 연속이 이 스레드로 돌아온다).</summary>
    private static void RunOnSta(Func<Task> body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            var frame = new DispatcherFrame();
            dispatcher.BeginInvoke(new Func<Task>(async () =>
            {
                try { await body(); }
                catch (Exception ex) { failure = ex; }
                finally { frame.Continue = false; }
            }));
            Dispatcher.PushFrame(frame);
            dispatcher.InvokeShutdown();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "STA 스레드가 제시간에 끝나지 않았다");
        if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    private static async Task SettleExternalChangeAsync(Harness h, UnitTopologyChangedMessage message)
    {
        await h.Console.HandleAsync(message, CancellationToken.None);
        var pending = h.Console.ExternalChangeTask;
        h.Delay.ReleaseAll();
        await pending;
    }
    #endregion

    [Fact]
    public void should_keep_the_selected_unit_on_the_list_and_in_the_console_when_the_graph_is_read_again()
    {
        RunOnSta(async () =>
        {
            var h = await OpenAsync();
            await h.Console.SelectByIdAsync(6);
            Assert.Same(h.Console.SelectedRow, h.Tree.SelectedItem);

            await h.Console.ReloadAsync(CancellationToken.None, quiet: true, bypassGuard: true);

            Assert.Equal(6, h.Console.SelectedRow?.Id);                     // 아직 있는 부대다 — 선택이 풀리면 안 된다
            Assert.Same(h.Console.SelectedRow, h.Tree.SelectedItem);          // 목록도 새 인스턴스를 가리킨다
            Assert.True(h.Console.CanMoveSelected);                           // [옮기기] · [최상위로] 가 살아 있다

            await h.Console.SelectByIdAsync(3);                               // 결선(OneWay)이 끊기지 않았다 — 뷰모델의 선택이 목록에 닿는다
            Assert.Same(h.Console.SelectedRow, h.Tree.SelectedItem);
            Assert.Equal(3, (h.Tree.SelectedItem as UnitNodeRowViewModel)?.Id);
            h.Window.Close();
        });
    }

    [Fact]
    public void should_not_say_deleted_and_keep_move_button_working_when_a_sync_notice_follows_a_move()
    {
        RunOnSta(async () =>
        {
            // SC-UNT-022 ③ → 폴백: 최상위로 옮긴 뒤 서버의 SYNC_UNIT 이 재조회를 부르고, 그 다음 [옮기기] 를 누른다.
            var h = await OpenAsync();
            await h.Console.SelectByIdAsync(6);
            Assert.True(await h.Console.MoveAsync(6, null));
            var said = h.Console.StatusText;

            await SettleExternalChangeAsync(h, new UnitTopologyChangedMessage("UPDATED", 6));

            Assert.NotEqual(SELECTED_UNIT_DELETED, h.Console.StatusText);
            Assert.Equal(said, h.Console.StatusText);                         // 방금 한 일의 문장을 덮지 않는다
            Assert.Equal(6, h.Console.SelectedRow?.Id);
            Assert.Same(h.Console.SelectedRow, h.Tree.SelectedItem);
            Assert.True(h.Console.CanMoveSelected);

            var patches = h.Units.Patches;
            Assert.True(await h.Console.MoveAsync(h.Console.SelectedRow!.Id, 3));   // [옮기기] 와 같은 호출
            Assert.Equal(patches + 1, h.Units.Patches);
            h.Window.Close();
        });
    }

    [Fact]
    public void should_keep_the_new_unit_selected_when_the_sync_echo_of_our_own_create_arrives()
    {
        RunOnSta(async () =>
        {
            // SC-UNT-008(헤디드 3회차): 등록 직후 우리 쓰기의 SYNC_UNIT CREATED 메아리가 재조회를 부르면 새 부대 선택이 풀렸다.
            var h = await OpenAsync();
            h.Console.BeginCreate();
            h.Console.Form.Echelon = EnumUnitEchelon.Company;
            h.Console.Form.ParentId = 2;
            h.Console.Form.Code = "c07";
            h.Console.Form.Name = "7중대";
            await h.Console.ApplyAsync();
            Assert.Equal(900, h.Console.SelectedRow?.Id);
            var said = h.Console.StatusText;

            await SettleExternalChangeAsync(h, new UnitTopologyChangedMessage("CREATED", 900));

            Assert.NotEqual(SELECTED_UNIT_DELETED, h.Console.StatusText);
            Assert.Equal(said, h.Console.StatusText);
            Assert.Equal(900, h.Console.SelectedRow?.Id);
            Assert.Same(h.Console.SelectedRow, h.Tree.SelectedItem);
            h.Window.Close();
        });
    }

    [Fact]
    public void should_still_drop_the_selection_when_a_search_hides_the_selected_unit()
    {
        RunOnSta(async () =>
        {
            // 재조회의 갈아 끼우기만 막는다 — 필터가 고른 행을 가리면 종전대로 선택을 놓는다.
            var h = await OpenAsync();
            await h.Console.SelectByIdAsync(6);

            h.Console.SearchText = "d01";

            Assert.DoesNotContain(h.Console.Rows, r => r.Id == 6);
            Assert.Null(h.Console.SelectedRow);
            Assert.Null(h.Tree.SelectedItem);
            h.Window.Close();
        });
    }

    [Fact]
    public void should_drop_the_selection_and_say_deleted_when_the_selected_unit_really_vanished_elsewhere()
    {
        RunOnSta(async () =>
        {
            var h = await OpenAsync();
            await h.Console.SelectByIdAsync(6);

            h.Units.Graph.Nodes.RemoveAll(n => n.Id == 6);
            h.Units.Graph.Edges.Hierarchy.RemoveAll(e => e[1] == 6);
            await SettleExternalChangeAsync(h, new UnitTopologyChangedMessage("DELETED", 6));

            Assert.Null(h.Console.SelectedRow);
            Assert.Null(h.Tree.SelectedItem);
            Assert.Equal(SELECTED_UNIT_DELETED, h.Console.StatusText);
            Assert.False(h.Console.CanMoveSelected);
            h.Window.Close();
        });
    }
}
