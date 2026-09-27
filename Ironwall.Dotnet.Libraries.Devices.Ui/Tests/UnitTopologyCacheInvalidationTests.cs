using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using Ironwall.Dotnet.Libraries.Nats.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/****************************************************************************
   Purpose      : 서버 SYNC_UNIT 를 받은 호스트가 부대 캐시를 무효화하면 — 이름 사전은 배경에서 한 번 다시 읽고,
                  우리 부대 id 해석은 다음 쓰기에서 한 번 다시 확인한다(실패하면 알던 id 를 쓴다).
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
[Collection("CaliburnIoC")]
public class UnitTopologyCacheInvalidationTests
{
    #region - Fakes -
    private sealed class ManualDelay
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

    private sealed class FakeProbe : IServerContractProbe
    {
        public EnumServerContract Contract => EnumServerContract.V8_0;
        public string? RawVersion => null;
        public bool IsResolved => true;
        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    }

    private sealed class FakeGraph : IUnitGraphApi
    {
        public bool IsAvailable => true;
        public UnitGraphDto Graph { get; } = new();
        public int Reads { get; private set; }
        public Task<ApiResponse<UnitGraphDto>> GetGraphAsync(CancellationToken token = default)
        {
            Reads++;
            return Task.FromResult(ApiResponse<UnitGraphDto>.CreateSuccess(Graph));
        }
        public Task<ApiResponse<UnitDetailDto>> GetDetailAsync(int unitId, CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitDetailDto>.CreateSuccess(new UnitDetailDto { Id = unitId }));
        public Task<ApiResponse<UnitDto>> CreateAsync(UnitCreateDto dto, CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitDto>.CreateSuccess(new UnitDto()));
        public Task<ApiResponse<UnitDto>> PatchAsync(int unitId, UnitUpdateDto dto, CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitDto>.CreateSuccess(new UnitDto()));
        public Task<ApiResponse<UnitDeleteResultDto>> DeleteAsync(int unitId, CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitDeleteResultDto>.CreateSuccess(new UnitDeleteResultDto()));
    }

    private sealed class FakeUnitApi : IUnitApiService
    {
        public List<UnitListDto> Units { get; } = new();
        public int ListReads { get; private set; }
        public bool Fail { get; set; }

        public Task<ApiListResponse<UnitListDto>> GetUnitsAsync(int page = 1, int limit = 20, EnumUnitEchelon? echelon = null,
            int? parentId = null, bool? isEnable = null, CancellationToken token = default)
        {
            ListReads++;
            if (Fail) return Task.FromResult(ApiListResponse<UnitListDto>.CreateError("SERVER_ERROR", "down"));
            return Task.FromResult(ApiListResponse<UnitListDto>.CreateSuccess(Units.ToList()));
        }

        public Task<ApiResponse<UnitDetailDto>> GetUnitAsync(int unitId, string? include = null, CancellationToken token = default) => throw new NotSupportedException();
        public Task<ApiResponse<UnitGraphDto>> GetUnitGraphAsync(int? rootId = null, int? depth = null, CancellationToken token = default) => throw new NotSupportedException();
        public Task<ApiResponse<UnitDto>> CreateUnitAsync(UnitCreateDto dto, CancellationToken token = default) => throw new NotSupportedException();
        public Task<ApiResponse<UnitDto>> PatchUnitAsync(int unitId, UnitUpdateDto dto, CancellationToken token = default) => throw new NotSupportedException();
        public Task<ApiResponse<UnitDto>> ReplaceUnitAsync(int unitId, UnitReplaceDto dto, CancellationToken token = default) => throw new NotSupportedException();
        public Task<ApiResponse<UnitDeleteResultDto>> DeleteUnitAsync(int unitId, CancellationToken token = default) => throw new NotSupportedException();
        public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;
    }

    private static NatsSetupModel FakeNats() => new() { GroupNats = "unit001" };
    #endregion

    private static UnitListDto Unit(int id, string code, string name) => new() { Id = id, Code = code, Name = name, EchelonRaw = "Company", IsEnable = true };

    [Fact]
    public async Task should_read_the_names_again_once_when_invalidated_several_times()
    {
        var graph = new FakeGraph();
        graph.Graph.Nodes.Add(Unit(5, "u5", "1구역"));
        graph.Graph.Nodes.Add(Unit(7, "u7", "2구역"));
        var delay = new ManualDelay();
        var directory = new UnitNameDirectory(graph, new FakeProbe(), delay: delay.Delay);
        await directory.EnsureLoadedAsync();

        graph.Graph.Nodes[0].Name = "1구역(개칭)";
        graph.Graph.Nodes.RemoveAt(1);                          // 7 은 다른 곳에서 지워졌다
        directory.Invalidate();
        directory.Invalidate();
        directory.Invalidate();
        Assert.Equal("1구역", directory.TryGetName(5));         // 다시 읽기 전에도 옛 이름은 그대로 보인다
        delay.ReleaseAll();
        await directory.PendingReload;

        Assert.Equal(2, graph.Reads);                          // 처음 1 + 무효화 세 번 → 1
        Assert.Equal("1구역(개칭)", directory.TryGetName(5));
        Assert.Null(directory.TryGetName(7));
    }

    [Fact]
    public async Task should_not_call_the_server_when_invalidated_before_anyone_used_the_names()
    {
        var graph = new FakeGraph();
        var delay = new ManualDelay();
        var directory = new UnitNameDirectory(graph, new FakeProbe(), delay: delay.Delay);

        directory.Invalidate();
        delay.ReleaseAll();
        await directory.PendingReload;

        Assert.Equal(0, graph.Reads);
    }

    [Fact]
    public async Task should_check_our_unit_id_again_after_invalidation_and_pick_up_a_recreated_unit()
    {
        var api = new FakeUnitApi();
        api.Units.Add(Unit(1, "unit001", "본부"));
        var scope = new UnitScopeService(api, FakeNats(), new FakeProbe());
        Assert.Equal(1, await scope.ResolveAsync());
        Assert.Equal(1, await scope.ResolveAsync());
        Assert.Equal(1, api.ListReads);                        // 캐시 — 다시 나가지 않는다

        api.Units.Clear();
        api.Units.Add(Unit(42, "unit001", "본부"));             // 지워지고 같은 코드로 다시 만들어졌다
        scope.Invalidate();

        Assert.True(scope.IsStale);
        Assert.Equal(42, await scope.ResolveAsync());
        Assert.False(scope.IsStale);
        Assert.Equal(2, api.ListReads);
    }

    [Fact]
    public async Task should_keep_the_known_unit_id_when_the_check_after_invalidation_fails()
    {
        var api = new FakeUnitApi();
        api.Units.Add(Unit(1, "unit001", "본부"));
        var scope = new UnitScopeService(api, FakeNats(), new FakeProbe());
        await scope.ResolveAsync();

        api.Fail = true;
        scope.Invalidate();

        Assert.Equal(1, await scope.ResolveAsync());           // null 이면 서버가 기본 부대로 조용히 묶는다
        Assert.Equal(1, scope.CurrentUnitId);
    }
}
