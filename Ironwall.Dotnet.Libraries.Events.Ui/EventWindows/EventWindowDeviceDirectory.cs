using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.Events.Ui.EventWindows;

/****************************************************************************
   Purpose      : 이벤트 창에 쓸 장비 · 구역 이름 찾기 (PRD camera-popup-modes FR-09 · 16)
   Created By   : Claude (T-06)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>탐지 트리거가 카메라 모델 · 장비 이름 · 구역 이름을 찾는 창구(배경 스레드에서 불린다).</summary>
public interface IEventWindowDeviceDirectory
{
    ICameraDeviceModel? FindCamera(int cameraId);

    string? DeviceName(int deviceId, EnumDeviceType deviceType);

    string? GroupName(int groupId);
}

/// <summary>
/// 장비 캐시(<see cref="DeviceProvider"/> · <see cref="DeviceGroupProvider"/>)에서 찾는다. 캐시는 UI 스레드가 고치므로
/// 배경 스레드에서 열거하다 바뀌면 한 번 더 시도하고, 그래도 안 되면 모른다(null)고 답한다 — 창은 이름 없이도 뜬다.
/// </summary>
public sealed class DeviceProviderEventWindowDirectory : IEventWindowDeviceDirectory
{
    private readonly DeviceProvider? _devices;
    private readonly DeviceGroupProvider? _groups;
    private readonly ILogService? _log;

    public DeviceProviderEventWindowDirectory(DeviceProvider? devices, DeviceGroupProvider? groups, ILogService? log = null)
    {
        _devices = devices;
        _groups = groups;
        _log = log;
    }

    public ICameraDeviceModel? FindCamera(int cameraId)
        => cameraId <= 0 || _devices is null
            ? null
            : Safe(() => _devices.OfType<ICameraDeviceModel>().FirstOrDefault(c => c.Id == cameraId));

    public string? DeviceName(int deviceId, EnumDeviceType deviceType)
    {
        if (deviceId <= 0 || _devices is null) return null;
        // 종류를 모르는(NONE) 엔트리는 번호만으로 고르지 않는다 — 다른 종류의 같은 번호 장비 이름을 띄우지 않게(WP-8 H2 와 같은 원칙).
        if (deviceType == EnumDeviceType.NONE) return null;
        var device = Safe(() => _devices.FirstOrDefault(d => d.Id == deviceId && d.DeviceType == deviceType));
        return string.IsNullOrWhiteSpace(device?.DeviceName) ? null : device!.DeviceName;
    }

    public string? GroupName(int groupId)
    {
        if (groupId <= 0 || _groups is null) return null;
        var group = Safe(() => _groups.FirstOrDefault(g => g.Id == groupId));
        return string.IsNullOrWhiteSpace(group?.Name) ? null : group!.Name;
    }

    private T? Safe<T>(Func<T?> read) where T : class
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try { return read(); }
            catch (InvalidOperationException) { /* 열거 중 컬렉션 변경 — 한 번 더 */ }
            catch (Exception ex)
            {
                _log?.Warning($"[EventWindow] 장비 캐시 읽기 실패: {ex.GetType().Name} {ex.Message}");
                return null;
            }
        }
        return null;
    }
}
