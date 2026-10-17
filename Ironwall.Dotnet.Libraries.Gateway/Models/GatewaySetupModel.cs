using Ironwall.Dotnet.Libraries.Base.Models;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.Gateway.Models;
/****************************************************************************
   Purpose      : Gateway DB 연결 설정 모델
   Created By   : GHLee
   Created On   : 10/29/2025 12:09:00 PM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class GatewaySetupModel : IMariaDbSetupModel
{
    #region - Ctors -
    public GatewaySetupModel()
    {
    }

    public GatewaySetupModel(IMariaDbSetupModel model)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));

        IpDbServer = model.IpDbServer;
        PortDbServer = model.PortDbServer;
        DbDatabase = model.DbDatabase;
        UidDbServer = model.UidDbServer;
        PasswordDbServer = model.PasswordDbServer;
    }
    #endregion

    [JsonProperty("ip_db_server", Order = 2)]
    public string IpDbServer { get; set; } = "localhost";

    [JsonProperty("port_db_server", Order = 3)]
    public int PortDbServer { get; set; } = 3306;

    [JsonProperty("uid_db_server", Order = 4)]
    public string UidDbServer { get; set; } = "root";

    [JsonProperty("password_db_server", Order = 5)]
    public string PasswordDbServer { get; set; } = string.Empty;

    [JsonProperty("db_database", Order = 6)]
    public string DbDatabase { get; set; } = "monitor_db";
}
