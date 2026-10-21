using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Devices;

/// <summary>
/// 장비 한 대의 단건 다시 받기 · 좁은 PATCH — 카테고리별 호출을 한 곳에 둔다.
/// 부대 콘솔 장비 창구(<see cref="UnitDeviceApiAdapter"/>)와 함체째 부대 옮기기(<see cref="EnclosureUnitMover"/>)가 함께 쓴다.
/// </summary>
internal static class UnitDeviceCalls
{
    /// <summary>카테고리 글(<c>controller</c> · <c>CAMERA</c> …, 대소문자 무관)을 enum 으로. 못 읽으면 <see cref="EnumDeviceCategory.None"/>.</summary>
    public static EnumDeviceCategory CategoryOf(string? category)
        => Enum.TryParse<EnumDeviceCategory>(category?.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : EnumDeviceCategory.None;

    /// <summary>단건 다시 받기. 실패하면 <c>null</c>.</summary>
    public static async Task<BaseDeviceDto?> RefetchAsync(IDeviceApiService api, EnumDeviceCategory category, int id, CancellationToken token) => category switch
    {
        EnumDeviceCategory.Controller => Read(await api.GetControllerByIdAsync(id, false, token).ConfigureAwait(false)),
        EnumDeviceCategory.Sensor => Read(await api.GetSensorByIdAsync(id, false, token).ConfigureAwait(false)),
        EnumDeviceCategory.Camera => Read(await api.GetCameraByIdAsync(id, token).ConfigureAwait(false)),
        EnumDeviceCategory.Speaker => Read(await api.GetSpeakerByIdAsync(id, token).ConfigureAwait(false)),
        EnumDeviceCategory.Enclosure => Read(await api.GetEnclosureByIdAsync(id, token).ConfigureAwait(false)),
        EnumDeviceCategory.Lamp => Read(await api.GetLampByIdAsync(id, token).ConfigureAwait(false)),
        EnumDeviceCategory.Gate => Read(await api.GetGateByIdAsync(id, token).ConfigureAwait(false)),
        _ => null,
    };

    /// <summary>좁은 PATCH 한 번. 결과를 공통 봉투로 올린다.</summary>
    public static async Task<ApiResponse<BaseDeviceDto>> PatchAsync(IDeviceApiService api, EnumDeviceCategory category, int id, BaseDeviceDto body, CancellationToken token)
    {
        return category switch
        {
            EnumDeviceCategory.Controller => Up(await api.PatchControllerAsync(id, (ControllerDeviceDto)body, token).ConfigureAwait(false)),
            EnumDeviceCategory.Sensor => Up(await api.PatchSensorAsync(id, (SensorDeviceDto)body, token).ConfigureAwait(false)),
            EnumDeviceCategory.Camera => Up(await api.PatchCameraAsync(id, (CameraDeviceDto)body, token).ConfigureAwait(false)),
            EnumDeviceCategory.Speaker => Up(await api.PatchSpeakerAsync(id, (SpeakerDeviceDto)body, token).ConfigureAwait(false)),
            EnumDeviceCategory.Enclosure => Up(await api.PatchEnclosureAsync(id, (EnclosureDeviceDto)body, token).ConfigureAwait(false)),
            EnumDeviceCategory.Lamp => Up(await api.PatchLampAsync(id, (LampDeviceDto)body, token).ConfigureAwait(false)),
            EnumDeviceCategory.Gate => Up(await api.PatchGateAsync(id, (GateDeviceDto)body, token).ConfigureAwait(false)),
            _ => ApiResponse<BaseDeviceDto>.CreateError(ApiErrorCodes.BadRequest, "소속 부대를 바꿀 수 없는 장비입니다."),
        };

        static ApiResponse<BaseDeviceDto> Up<T>(ApiResponse<T> response) where T : BaseDeviceDto => new()
        {
            Success = response.Success,
            Message = response.Message,
            Data = response.Data,
            Error = response.Error,
            StatusCode = response.StatusCode,
        };
    }

    private static BaseDeviceDto? Read<T>(ApiResponse<T> response) where T : BaseDeviceDto
        => response.Success ? response.Data : null;
}
