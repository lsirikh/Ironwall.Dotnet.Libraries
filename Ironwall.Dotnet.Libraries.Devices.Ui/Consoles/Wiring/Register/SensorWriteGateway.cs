using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;

/// <summary>
/// 결선 저장이 쓰는 <b>좁은 통로</b> — 센서 한 대 받아오기 · 만들기 · 고치기, 이 셋뿐이다.
/// </summary>
/// <remarks>
/// <para><b>왜 인터페이스를 하나 더 두는가</b> — <see cref="IDeviceApiService"/> 는 넓히지 않는다는 계약이 있고
/// (멤버 하나를 더하면 목 · 페이크 15종이 한꺼번에 깨진다), 저장 본문을 <b>글자 그대로 단언</b>하려면
/// 보낸 DTO 를 붙잡는 자리가 필요하다. 그 두 요구를 같이 만족시키는 가장 작은 방법이 이 어댑터다.</para>
/// <para>제품에서는 <see cref="DeviceApiSensorGateway"/> 가 그대로 <see cref="IDeviceApiService"/> 로 넘긴다 —
/// 판단도 변환도 없다.</para>
/// </remarks>
public interface ISensorWriteGateway
{
    Task<ApiResponse<SensorDeviceDto>> GetAsync(int id, CancellationToken token = default);

    /// <summary>그 제어기에 달린 센서 전부 — 만들고서 <c>id</c> 를 못 받았을 때 번호로 되찾는다(C7).</summary>
    Task<ApiListResponse<SensorDeviceDto>> ListByControllerAsync(int controllerId, CancellationToken token = default);
    Task<ApiResponse<SensorDeviceDto>> CreateAsync(SensorDeviceDto dto, CancellationToken token = default);
    Task<ApiResponse<SensorDeviceDto>> PatchAsync(int id, SensorDeviceDto dto, CancellationToken token = default);

    /// <summary>그룹에 장비를 한꺼번에 넣는다 — <b>그룹 하나에 호출 한 번</b>(W2).</summary>
    Task<ApiResponse<DeviceGroupAssignResultDto>> AssignToGroupAsync(int groupId, IReadOnlyList<int> deviceIds, CancellationToken token = default);

    /// <summary>그룹에서 장비를 한꺼번에 뺀다(body-DELETE 배치).</summary>
    Task<ApiResponse<DeviceGroupBulkRemoveResultDto>> RemoveFromGroupAsync(int groupId, IReadOnlyList<int> deviceIds, CancellationToken token = default);
}

/// <inheritdoc cref="ISensorWriteGateway"/>
public sealed class DeviceApiSensorGateway : ISensorWriteGateway
{
    private readonly IDeviceApiService _api;

    public DeviceApiSensorGateway(IDeviceApiService api) => _api = api ?? throw new ArgumentNullException(nameof(api));

    /// <summary>
    /// 단건 조회는 서버 기본이 이미 <c>meta.view=full</c> 이라 <c>?view=full</c> 을 붙이지 않는다
    /// (목록에만 붙인다 — <see cref="Helpers.DeviceQueryPolicy.View"/>).
    /// </summary>
    public Task<ApiResponse<SensorDeviceDto>> GetAsync(int id, CancellationToken token = default)
        => _api.GetSensorByIdAsync(id, false, token);

    /// <summary>한 제어기의 센서는 한 화면에 다 들어온다 — 넉넉한 한 쪽으로 받는다.</summary>
    public Task<ApiListResponse<SensorDeviceDto>> ListByControllerAsync(int controllerId, CancellationToken token = default)
        => _api.GetSensorsAsync(controllerId: controllerId, page: 1, limit: MAX_SENSORS_PER_CONTROLLER, token: token);

    /// <summary>한 제어기가 가질 수 있는 센서 수의 상한 — 결선 칸 상한(선 2 × 64)보다 넉넉하게 잡는다.</summary>
    private const int MAX_SENSORS_PER_CONTROLLER = 500;

    public Task<ApiResponse<SensorDeviceDto>> CreateAsync(SensorDeviceDto dto, CancellationToken token = default)
        => _api.CreateSensorAsync(dto, token);

    public Task<ApiResponse<SensorDeviceDto>> PatchAsync(int id, SensorDeviceDto dto, CancellationToken token = default)
        => _api.PatchSensorAsync(id, dto, token);

    public Task<ApiResponse<DeviceGroupAssignResultDto>> AssignToGroupAsync(int groupId, IReadOnlyList<int> deviceIds, CancellationToken token = default)
        => _api.AssignDevicesToGroupAsync(groupId, new DeviceGroupAssignRequestDto { DeviceIds = deviceIds.ToList() }, token);

    public Task<ApiResponse<DeviceGroupBulkRemoveResultDto>> RemoveFromGroupAsync(int groupId, IReadOnlyList<int> deviceIds, CancellationToken token = default)
        => _api.RemoveDevicesFromGroupAsync(groupId, new DeviceGroupAssignRequestDto { DeviceIds = deviceIds.ToList() }, token);
}
