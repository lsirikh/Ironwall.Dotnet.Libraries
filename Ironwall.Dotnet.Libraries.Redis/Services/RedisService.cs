using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Redis.Models;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Redis.Services
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 02/02/2024 10:22:47 AM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/
    internal class RedisService : MessageService<IRedisService>, IRedisService
    {
        #region - Ctors -
        public RedisService()
        {
        }
        public RedisService(ILogService log) : base(log)
        {
        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        public override IRedisService Connect(IRedisSetupModel setupModel)
        {
            try
            {
                _channelName = setupModel.NameChannel ?? "Channel1"; //Default Channel Name
                var configuration = ConfigurationOptions.Parse($"{setupModel.IpAddressRedis}:{setupModel.PortRedis}");
                configuration.AllowAdmin = true;
                configuration.Password = setupModel.PasswordRedis;

                var connectionMultiplexer = ConnectionMultiplexer.Connect(configuration);
                _log?.Info($"Redis ConnectionMultiplexer was connected({setupModel.IpAddressRedis}:{setupModel.PortRedis}).");
                Subscriber = connectionMultiplexer.GetSubscriber();
                _log?.Info($"Redis Subscriber was activated.");
            }
            catch (Exception ex)
            {
                _log?.Error(ex.Message);
            }
            return this;
        }

        public override async Task<IRedisService?> ConnectAsync(IRedisSetupModel setupModel)
        {
            try
            {
                _channelName = setupModel.NameChannel ?? "Channel1"; //Default Channel Name
                var configuration = ConfigurationOptions.Parse($"{setupModel.IpAddressRedis}:{setupModel.PortRedis}");
                configuration.AllowAdmin = true;
                configuration.Password = setupModel.PasswordRedis;

                var connectionMultiplexer = await ConnectionMultiplexer.ConnectAsync(configuration);
                _log?.Info($"Redis ConnectionMultiplexer was connected({setupModel.IpAddressRedis}:{setupModel.PortRedis}).");
                Subscriber = connectionMultiplexer.GetSubscriber();
                _log?.Info($"Redis Subscriber was activated.");
                return this;
            }
            catch (Exception ex)
            {
                _log?.Error(ex.Message);
                return null;
            }
        }

        public override async Task PublishAsync(string channel, string msg)
        {
            try
            {
                if (Subscriber == null) return;
                RedisChannel redisChannel = RedisChannel.Literal(channel);
                await Subscriber.PublishAsync(redisChannel, msg, CommandFlags.None);
            }
            catch (Exception ex)
            {
                _log?.Error(ex.Message);
            }
        }
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        public string Channel => _channelName;
        #endregion
        #region - Attributes -
        #endregion
    }
}
