using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Defines;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Providers;

/// <summary>
/// 통문(Gate) 장비 뷰 — <see cref="DeviceProvider"/> 공용 캐시에서 <see cref="IGateDeviceModel"/> 만 비춘다.
/// 다른 6 카테고리의 프로바이더와 같은 모양이다(device-console-v8 FR-01).
/// </summary>
public sealed class GateDeviceProvider : BaseDeviceProdiver<IGateDeviceModel>
{
    #region - Ctors -
    public GateDeviceProvider(ILogService log, DeviceProvider provider) : base(log, provider)
    {
    }
    #endregion
}
