using Ironwall.Dotnet.Libraries.Base.DataProviders;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Monitoring.Models.Servers;

namespace Ironwall.Dotnet.Libraries.Devices.Providers;

/// <summary>
/// 방송서버 목록 캐시 Provider (스피커 패널 서버 드롭다운 데이터소스).
/// DeviceGroupProvider와 동일 패턴 — DeviceProviderService.FetchServersAsync로 startup 1회 적재.
/// ILoadable 미구현(빈 캐시 동결 방지) — DI는 .As&lt;ServerProvider&gt;().SingleInstance()만.
/// </summary>
public sealed class ServerProvider : BaseProvider<IServerModel>
{
    #region - Ctors -
    public ServerProvider(ILogService log) : base(log)
    {
    }
    #endregion
    #region - Properties -
    #endregion
    #region - Attributes -
    #endregion
}
