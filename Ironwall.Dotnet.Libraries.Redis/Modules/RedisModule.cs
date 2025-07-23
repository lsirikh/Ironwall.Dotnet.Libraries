using Autofac;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Redis.Models;
using Ironwall.Dotnet.Libraries.Redis.Services;
using StackExchange.Redis;
using System.Net;

namespace Ironwall.Dotnet.Libraries.Redis.Redis.Modules
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 7/10/2023 10:22:47 AM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class RedisModule : Module
    {
        #region - Ctors -
        public RedisModule(ILogService log)
        {
            _log = log;
            _setup = new RedisSetupModel();
        }

        public RedisModule(IRedisSetupModel setup, ILogService log, int count)
        {
            _log = log;
            _setup = setup;
            _count = count;
        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        protected override void Load(ContainerBuilder builder)
        {
            try
            {
                // RedisService의 인스턴스를 생성하고 ConnectAsync 메서드를 동기적으로 호출합니다.
                builder.RegisterInstance(_setup).AsSelf().SingleInstance();

                builder.Register(ctx =>
                {
                    _log?.Info($"{nameof(RedisModule)} is trying to create a single {nameof(RedisService)} instance by connecting to the Redis server.");
                    return (new RedisService(_log)).Connect(_setup);
                }).As<IRedisService>().As<IService>()
                .SingleInstance()
                .WithMetadata("Order", _count);
            }
            catch
            {
                throw;
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
        #endregion
        #region - Attributes -
        private ILogService _log;
        private IRedisSetupModel _setup;
        private int _count;
        #endregion
    }
}
