using Ironwall.Dotnet.Libraries.Base.DataProviders;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Monitoring.Models.GatewayEvents;

namespace Ironwall.Dotnet.Libraries.Gateway.Providers;
/****************************************************************************
   Purpose      : Gateway 이벤트 메모리 관리 Provider                                                         
   Created By   : GHLee                                                
   Created On   : 10/29/2025 12:16:00 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public sealed class GatewayEventProvider : BaseProvider<IGatewayEventModel>
{
    public GatewayEventProvider(ILogService log) : base(log)
    {
    }
}
