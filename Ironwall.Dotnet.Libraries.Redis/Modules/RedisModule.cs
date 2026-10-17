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
                // RedisSetupModel을 IRedisSetupModel과 RedisSetupModel 둘 다로 등록
                if (_setup is RedisSetupModel redisSetupModel)
                {
                    builder.RegisterInstance(redisSetupModel).As<IRedisSetupModel>().As<RedisSetupModel>().SingleInstance();
                }
                else
                {
                    // IRedisSetupModel을 받아서 RedisSetupModel 생성
                    var newRedisSetupModel = new RedisSetupModel(_setup);
                    builder.RegisterInstance(newRedisSetupModel).As<IRedisSetupModel>().As<RedisSetupModel>().SingleInstance();
                }

                // RedisService의 인스턴스를 생성하고 ConnectAsync 메서드를 동기적으로 호출합니다.
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
