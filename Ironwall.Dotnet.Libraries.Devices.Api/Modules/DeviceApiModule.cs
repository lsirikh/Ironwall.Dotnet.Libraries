using Autofac;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Modules;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Modules;
/****************************************************************************
   Purpose      : Device API Module
   Created By   : GHLee
   Created On   : 11/10/2025 6:00:00 PM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// Device API 모듈 (Autofac)
/// </summary>
public class DeviceApiModule : Module
{
    #region - Ctors -
    public DeviceApiModule(ILogService log, ApiSetupModel setup, string name = "DeviceApi")
    {
        _log = log;
        _setup = setup;
        _name = name;
    }
    #endregion

    #region - Implementation of Interface -
    protected override void Load(ContainerBuilder builder)
    {
        try
        {
            // 1. ApiModule 등록 (내부에서 IApiService 등록)
            // ApiSetupModel은 이제 Timeout 속성을 포함하므로 직접 사용 가능
            builder.RegisterModule(new ApiModule(_log, _setup, $"{_name}-Base"));

            // 2. ApiSetupModel 등록
            builder.RegisterInstance(_setup)
                .Named<ApiSetupModel>(_name)
                .SingleInstance();

            // 3. DeviceApiService 등록
            builder.Register(ctx => new DeviceApiService(
                    _log,
                    ctx.ResolveNamed<IApiService>($"{_name}-Base"),
                    ctx.ResolveNamed<ApiSetupModel>(_name)
                ))
                .Named<IDeviceApiService>(_name)
                .AsImplementedInterfaces()
                .SingleInstance()
                .WithMetadata("Order", 4);

            _log?.Info($"[{nameof(DeviceApiModule)}] Module loaded successfully with name: {_name}");
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeviceApiModule)}] Failed to load module: {ex.Message}");
            throw;
        }
    }
    #endregion

    #region - Attributes -
    private readonly ILogService _log;
    private readonly ApiSetupModel _setup;
    private readonly string _name;
    #endregion
}
