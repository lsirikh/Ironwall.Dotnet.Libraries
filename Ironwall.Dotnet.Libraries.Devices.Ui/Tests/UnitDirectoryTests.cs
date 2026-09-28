using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Devices.Units;
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
   Purpose      : TEST-16(사전 절반) — IUnitDirectory: 이름 · 상위 경로 · SYNC_UNIT 합침 재조회 · 이름 사전 한 벌(v1.3 ISSUE-46)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : 시나리오 SIM-M013 · M014 · M015 · M016 · N001 · N007 · N022 · N043 · N068.
                  v1.3: UnitDirectory 는 따로 캐시하지 않는다 — 장비 목록 · 상세가 쓰는 UnitNameDirectory 의 편제(같은 캐시 ·
                  같은 무효화 시점)를 감싼다. 호스트가 SYNC_UNIT 에 UnitNameDirectory.Invalidate() 를 부르면 관계도 사전도 함께 새로워진다.
                  시간은 주입한다(ManualDelay) — sleep 없음.
****************************************************************************/
public class UnitDirectoryTests
{
    private static readonly UnitMapFixture Org = UnitMapTestData.Standard200();

    private static (UnitDirectory Directory, UnitNameDirectory Names, GraphApi Api, ManualDelay Delay) Create(
        UnitGraphDto? graph = null, EnumServerContract contract = EnumServerContract.V8_0, IEventAggregator? events = null)
    {
        var api = new GraphApi(graph ?? Org.Graph);
        var delay = new ManualDelay();
        var names = new UnitNameDirectory(api, new FixedProbe(contract), delay: delay.Delay);
        return (new UnitDirectory(names, events, dispatch: a => a()), names, api, delay);
    }

    #region - 읽기 · 경로 -
    [Fact]
    public async Task should_describe_name_and_ancestor_path_when_loaded()
    {
        var (directory, _, _, _) = Create();
        await directory.EnsureLoadedAsync();

        Assert.Equal("7중대 · 2대대 › 1연대 › 제○○사단", directory.Describe(Org.IdOf("7중대")));
    }

    [Fact]
    public async Task should_describe_a_root_by_its_name_only()
    {
        var (directory, _, _, _) = Create();
        await directory.EnsureLoadedAsync();

        Assert.Equal("제○○사단", directory.Describe(Org.IdOf("제○○사단")));
    }

    [Fact]
    public async Task should_stop_the_path_at_the_graph_edge_when_parent_is_outside()
    {
        var orphan = UnitMapTestData.OrphanParent();
        var (directory, _, _, _) = Create(orphan.Graph);
        await directory.EnsureLoadedAsync();

        Assert.Equal("9중대", directory.Describe(orphan.IdOf("9중대")));
    }

    [Fact]
    public async Task should_return_null_when_unit_is_unknown()
    {
        var (directory, _, _, _) = Create();
        await directory.EnsureLoadedAsync();

        Assert.Null(directory.Describe(999_999));                            // SIM-M016
    }

    [Fact]
    public void should_return_null_before_anything_was_read()
    {
        var (directory, _, api, _) = Create();

        Assert.Null(directory.Describe(Org.IdOf("7중대")));
        Assert.Equal(0, api.Reads);                                          // Describe 는 네트워크에 나가지 않는다
    }

    [Fact]
    public async Task should_be_unavailable_and_never_call_the_server_when_contract_is_below_8_0()
    {
        var (directory, _, api, _) = Create(contract: EnumServerContract.V6_3);

        await directory.EnsureLoadedAsync();

        Assert.False(directory.IsAvailable);                                 // SIM-M015 — 줄을 숨긴다
        Assert.Equal(0, api.Reads);
    }

    [Fact]
    public async Task should_read_once_when_ensure_loaded_is_called_repeatedly()
    {
        var (directory, _, api, _) = Create();

        await Task.WhenAll(directory.EnsureLoadedAsync(), directory.EnsureLoadedAsync(), directory.EnsureLoadedAsync());

        Assert.Equal(1, api.Reads);
    }
    #endregion

    #region - 이름 사전 한 벌 (v1.3 ISSUE-46) -
    [Fact]
    public async Task should_share_the_cache_so_one_read_serves_both_the_name_list_and_the_map()
    {
        var (directory, names, api, _) = Create();

        await names.EnsureLoadedAsync();                                     // 장비 목록이 먼저 읽었다

        Assert.Equal("7중대 · 2대대 › 1연대 › 제○○사단", directory.Describe(Org.IdOf("7중대")));
        Assert.Equal(1, api.Reads);                                          // 관계도 사전이 다시 읽지 않는다
    }

    [Fact]
    public async Task should_not_return_the_old_name_after_the_host_invalidates_the_name_list()
    {
        // 호스트는 SYNC_UNIT 에 UnitNameDirectory.Invalidate() 를 부른다(NatsBrokerService :340). 관계도 사전도 같은 시점에 새로워진다.
        var graph = UnitMapTestData.Mixed().Graph;
        var (directory, names, _, delay) = Create(graph);
        await directory.EnsureLoadedAsync();
        var changed = 0;
        directory.Changed += (_, _) => changed++;

        graph.Nodes.Single(n => n.Id == 10).Name = "1대대(개칭)";
        names.Invalidate();
        delay.ReleaseAll();
        await names.PendingReload;

        Assert.Equal("2중대 · 1대대(개칭)", directory.Describe(12));
        Assert.Equal("1대대(개칭)", names.TryGetName(10));
        Assert.Equal(1, changed);
    }
    #endregion

    #region - SYNC_UNIT 합침 재조회 -
    [Fact]
    public async Task should_reload_once_and_raise_changed_once_when_several_topology_notices_arrive()
    {
        var (directory, names, api, delay) = Create();
        await directory.EnsureLoadedAsync();
        var changed = 0;
        directory.Changed += (_, _) => changed++;

        await Task.WhenAll(                                                  // PUT 한 번이 몰아서 낸 알림(SIM-N001)
            Handle(directory, "CREATED", 201),
            Handle(directory, "UPDATED", 12),
            Handle(directory, "UPDATED", 12));
        names.Invalidate();                                                  // 호스트의 무효화도 같은 창에 합쳐진다
        delay.ReleaseAll();
        await names.PendingReload;

        Assert.Equal(2, api.Reads);                                          // 처음 1 + 합친 1
        Assert.Equal(1, changed);
    }

    [Fact]
    public async Task should_not_call_the_server_when_a_notice_arrives_before_anyone_used_the_directory()
    {
        var (directory, names, api, delay) = Create();

        await Handle(directory, "UPDATED", 3);
        delay.ReleaseAll();
        await names.PendingReload;

        Assert.Equal(0, api.Reads);                                          // 아무도 안 쓴 사전을 위해 서버를 부르지 않는다
    }

    [Fact]
    public async Task should_reread_only_the_graph_when_a_topology_notice_arrives()
    {
        // 분석 ISSUE-28 · SIM-N001 — 편제 알림 뒤 재조회는 /graph 한 번뿐이다. 상세 · 쓰기 경로가 불리면 OtherCalls 가 센다.
        var (directory, names, api, delay) = Create();
        await directory.EnsureLoadedAsync();

        await Handle(directory, "UPDATED", 12);
        delay.ReleaseAll();
        await names.PendingReload;

        Assert.Equal(2, api.Reads);
        Assert.Equal(0, api.OtherCalls);
    }

    [Fact]
    public async Task should_listen_through_the_event_aggregator_until_disposed()
    {
        var events = new EventAggregator();
        var (directory, names, api, delay) = Create(events: events);
        await directory.EnsureLoadedAsync();

        await events.PublishOnCurrentThreadAsync(new UnitTopologyChangedMessage("UPDATED", 5));
        delay.ReleaseAll();
        await names.PendingReload;
        Assert.Equal(2, api.Reads);

        directory.Dispose();
        var changed = 0;
        directory.Changed += (_, _) => changed++;
        await events.PublishOnCurrentThreadAsync(new UnitTopologyChangedMessage("UPDATED", 5));
        delay.ReleaseAll();
        await names.PendingReload;
        Assert.Equal(2, api.Reads);                                          // 구독 해제 뒤에는 듣지 않는다
        Assert.Equal(0, changed);
    }

    [Fact]
    public async Task should_raise_changed_through_the_dispatcher_hook()
    {
        var api = new GraphApi(Org.Graph);
        var delay = new ManualDelay();
        var names = new UnitNameDirectory(api, new FixedProbe(EnumServerContract.V8_0), delay: delay.Delay);
        var dispatched = 0;
        var directory = new UnitDirectory(names, dispatch: a => { dispatched++; a(); });
        await directory.EnsureLoadedAsync();
        dispatched = 0;

        await Handle(directory, "DELETED", 7);
        delay.ReleaseAll();
        await names.PendingReload;

        Assert.Equal(1, dispatched);                                         // NATS 스레드 → UI 스레드로 옮겨 발화
    }

    [Fact]
    public void should_be_an_iunitdirectory_for_the_map_side()
    {
        Assert.IsAssignableFrom<IUnitDirectory>(new UnitDirectory(new UnitNameDirectory()));
    }
    #endregion

    #region - Fakes -
    private static Task Handle(UnitDirectory directory, string action, int resourceId)
        => directory.HandleAsync(new UnitTopologyChangedMessage(action, resourceId), CancellationToken.None);

    private sealed class FixedProbe : IServerContractProbe
    {
        public FixedProbe(EnumServerContract contract) => Contract = contract;
        public EnumServerContract Contract { get; }
        public string? RawVersion => null;
        public bool IsResolved => true;
        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    }

    internal sealed class ManualDelay
    {
        private readonly List<TaskCompletionSource> _pending = new();

        public Task Delay(TimeSpan window, CancellationToken token)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_pending) _pending.Add(tcs);
            return tcs.Task;
        }

        public void ReleaseAll()
        {
            List<TaskCompletionSource> now;
            lock (_pending) { now = _pending.ToList(); _pending.Clear(); }
            foreach (var t in now) t.TrySetResult();
        }
    }

    internal sealed class GraphApi : IUnitGraphApi
    {
        private readonly UnitGraphDto _graph;
        private int _reads;

        public GraphApi(UnitGraphDto graph) => _graph = graph;

        public bool Fail { get; set; }
        public int Reads => Volatile.Read(ref _reads);
        public bool IsAvailable => true;
        public int OtherCalls { get; private set; }

        public Task<ApiResponse<UnitGraphDto>> GetGraphAsync(CancellationToken token = default)
        {
            Interlocked.Increment(ref _reads);
            return Task.FromResult(Fail
                ? ApiResponse<UnitGraphDto>.CreateError("SERVER_ERROR", "down")
                : ApiResponse<UnitGraphDto>.CreateSuccess(_graph));
        }

        public Task<ApiResponse<UnitDetailDto>> GetDetailAsync(int unitId, CancellationToken token = default) { OtherCalls++; throw new NotSupportedException(); }
        public Task<ApiResponse<UnitDto>> CreateAsync(UnitCreateDto dto, CancellationToken token = default) { OtherCalls++; throw new NotSupportedException(); }
        public Task<ApiResponse<UnitDto>> PatchAsync(int unitId, UnitUpdateDto dto, CancellationToken token = default) { OtherCalls++; throw new NotSupportedException(); }
        public Task<ApiResponse<UnitDeleteResultDto>> DeleteAsync(int unitId, CancellationToken token = default) { OtherCalls++; throw new NotSupportedException(); }
    }
    #endregion
}
