namespace Ironwall.Dotnet.Libraries.Gateway.Models;

public interface IGatewaySetupModel
{
    string IpDbServer { get; set; }
    int PortDbServer { get; set; }
    string UidDbServer { get; set; }
    string PasswordDbServer { get; set; }
    string DbDatabase { get; set; }
}
