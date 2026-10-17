using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
/****************************************************************************
   Purpose      : UnitNameDirectory 회귀 가드 — unit_id → 부대 이름 캐시(D-14)
   Created By   : GHLee
   Created On   : 9/23/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 장비 목록 "소속 부대" 열·상세 "소속 부대" 칸이 공유하는 이름 사전. 이름을 지어내지 않는다는 계약
/// (못 구하면 원값 id, 배정 없으면 "미배치")과 v8.0 게이트를 여기서 고정한다.
/// </summary>
public class UnitNameDirectoryTests
{
    [Theory]
    [InlineData(EnumServerContract.V6_3, false)]
    [InlineData(EnumServerContract.V7_0, false)]
    [InlineData(EnumServerContract.V8_0, true)]
    public void should_gate_unit_era_when_contract_given(EnumServerContract contract, bool expected)
    {
        var directory = new UnitNameDirectory(new FakeUnitGraphApi(), new FakeProbe(contract));

        Assert.Equal(expected, directory.IsUnitEra);
    }

    [Fact]
    public void should_return_unassigned_when_unit_id_is_null()
    {
        var directory = new UnitNameDirectory(new FakeUnitGraphApi(), new FakeProbe(EnumServerContract.V8_0));

        Assert.Equal(UnitNameDirectory.Unassigned, directory.Display(null));
    }

    [Fact]
    public void should_fallback_to_raw_id_when_name_not_cached_yet()
    {
        var directory = new UnitNameDirectory(new FakeUnitGraphApi(), new FakeProbe(EnumServerContract.V8_0));

        Assert.Equal("7", directory.Display(7));
    }

    [Fact]
    public async Task should_resolve_name_when_graph_loaded()
    {
        var api = new FakeUnitGraphApi();
        api.Graph.Nodes.Add(new UnitListDto { Id = 5, Code = "unit005", Name = "1구역", EchelonRaw = "Company", IsEnable = true });
        var directory = new UnitNameDirectory(api, new FakeProbe(EnumServerContract.V8_0));

        await directory.EnsureLoadedAsync();

        Assert.Equal("1구역", directory.TryGetName(5));
        Assert.Equal("1구역", directory.Display(5));
    }

    [Fact]
    public async Task should_not_cache_empty_name_when_graph_loaded()
    {
        var api = new FakeUnitGraphApi();
        api.Graph.Nodes.Add(new UnitListDto { Id = 9, Code = "unit009", Name = "", EchelonRaw = "Company", IsEnable = true });
        var directory = new UnitNameDirectory(api, new FakeProbe(EnumServerContract.V8_0));

        await directory.EnsureLoadedAsync();

        Assert.Null(directory.TryGetName(9));
        Assert.Equal("9", directory.Display(9));   // 지어내지 않는다 — 빈 이름 대신 원값 id
    }

    [Fact]
    public async Task should_not_call_server_when_contract_below_unit_era()
    {
        var api = new FakeUnitGraphApi();
        var directory = new UnitNameDirectory(api, new FakeProbe(EnumServerContract.V6_3));

        await directory.EnsureLoadedAsync();

        Assert.Equal(0, api.GraphReads);
    }

    [Fact]
    public async Task should_load_only_once_when_called_repeatedly()
    {
        var api = new FakeUnitGraphApi();
        api.Graph.Nodes.Add(new UnitListDto { Id = 1, Code = "unit001", Name = "본부", EchelonRaw = "Company", IsEnable = true });
        var directory = new UnitNameDirectory(api, new FakeProbe(EnumServerContract.V8_0));

        await directory.EnsureLoadedAsync();
        await directory.EnsureLoadedAsync();

        Assert.Equal(1, api.GraphReads);
    }

    #region - Fakes -
    private sealed class FakeProbe : IServerContractProbe
    {
        public FakeProbe(EnumServerContract contract) => Contract = contract;
        public EnumServerContract Contract { get; }
        public string? RawVersion => null;
        public bool IsResolved => true;
        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    }

    private sealed class FakeUnitGraphApi : IUnitGraphApi
    {
        public bool IsAvailable => true;
        public UnitGraphDto Graph { get; } = new();
        public int GraphReads { get; private set; }

        public Task<ApiResponse<UnitGraphDto>> GetGraphAsync(CancellationToken token = default)
        {
            GraphReads++;
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
    #endregion
}
