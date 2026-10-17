using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Dialogs;

/// <summary>
/// 보내기 직전에 <b>그 그룹 하나만</b> 다시 읽는 눈. 배정 창이 남의 변경을 밟지 않기 위한 최소한의 왕복이다.
/// </summary>
/// <remarks>
/// <para>새 인터페이스다 — <see cref="IDeviceApiService"/> 에 멤버를 더하면 이 저장소의 가짜 구현 십수 개가 한꺼번에 깨진다.</para>
/// <para>왜 전량 재조회가 아닌가: <c>IDeviceProviderService.FetchAllDevicesAsync</c> 는 서버 · 제어기 · 센서 · 카메라 ·
/// 스피커 · 함체 · 통문 · 경광등 · 그룹을 차례로 읽고 마지막에 <c>AllDevicesLoadedMessage</c> 를 뿌린다 —
/// 로그인 게이팅의 가림막 해제 · 심볼 대량 동기화 신호다. 저장 한 번마다 그것을 울릴 수는 없다.</para>
/// </remarks>
public interface IGroupMembershipProbe
{
    /// <summary>서버가 아는 그 그룹의 장비 수. 읽지 못했으면 <c>null</c> — 그때는 <b>보내지 않는다</b>.</summary>
    Task<int?> CountAsync(int groupId, CancellationToken token = default);
}

/// <summary>
/// 그룹 상세 조회 <b>한 번</b>(<c>GET /devices/groups/{id}</c>)으로 소속 장비 수를 읽는다.
/// </summary>
/// <remarks>
/// <b>수</b>만 본다 — 지금 <c>DeviceGroupDto</c> 에는 소속 장비 <b>id 목록이 없다</b>(<c>device_count</c> 뿐).
/// 서버 응답에는 <c>devices[{id, category_device}]</c> 가 있으므로, DTO 에 그 칸이 생기면 집합 비교로 올릴 수 있다
/// (<see cref="AssignDelta.Drift"/> 가 이미 집합 비교를 한다). 그때까지는 <b>수가 같은 맞교환</b>을 놓친다 —
/// 다만 보내는 것이 차분이라 그 경우에도 남의 변경을 덮어쓰지는 않는다.
/// </remarks>
public sealed class DeviceGroupMembershipProbe : IGroupMembershipProbe
{
    private readonly IDeviceApiService _api;
    private readonly ILogService? _log;

    public DeviceGroupMembershipProbe(IDeviceApiService api, ILogService? log = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _log = log;
    }

    public async Task<int?> CountAsync(int groupId, CancellationToken token = default)
    {
        if (groupId <= 0) return null;
        try
        {
            var response = await _api.GetDeviceGroupByIdAsync(groupId, token).ConfigureAwait(true);
            if (response.Success && response.Data is { } group) return group.DeviceCount;

            _log?.Warning($"[Assign] 그룹({groupId}) 재조회 실패: {response.Message}");
            return null;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Error($"[Assign] 그룹({groupId}) 재조회 오류: {ex.Message}");
            return null;
        }
    }
}
