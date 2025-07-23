namespace Ironwall.Dotnet.Libraries.Redis.Models;

public interface IRedisSetupModel
{
    string IpAddressRedis { get; set; }
    string? NameChannel { get; set; }
    string? PasswordRedis { get; set; }
    int PortRedis { get; set; }
}