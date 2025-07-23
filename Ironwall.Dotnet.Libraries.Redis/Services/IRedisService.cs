namespace Ironwall.Dotnet.Libraries.Redis.Services
{
    public interface IRedisService : IMessageService<IRedisService> 
    {
        string Channel { get;}
    }
}