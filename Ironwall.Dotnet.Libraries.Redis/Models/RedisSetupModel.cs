using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Redis.Models
{
    public class RedisSetupModel : IRedisSetupModel
    {
        public RedisSetupModel()
        {
        }

        public RedisSetupModel(IRedisSetupModel model)
        {
            IpAddressRedis = model.IpAddressRedis;
            PortRedis = model.PortRedis;
            PasswordRedis = model.PasswordRedis;
            NameChannel = model.NameChannel;
        }
        public string IpAddressRedis { get; set; } = string.Empty;
        public int PortRedis { get; set; } = 6379;
        public string? PasswordRedis { get; set; }
        public string? NameChannel { get; set; }
    }
}
