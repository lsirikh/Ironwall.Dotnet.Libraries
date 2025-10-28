using Autofac;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;

namespace Ironwall.Dotnet.Libraries.Nats.Modules;

/****************************************************************************
    Purpose      : NATS 서비스 Autofac DI 모듈
    Created By   : GHLee
    Created On   : 10/28/2025
    Department   : SW Team
    Company      : Sensorway Co., Ltd.
    Email        : lsirikh@naver.com
 ****************************************************************************/

/// <summary>
/// Autofac 컨테이너에 NatsService를 등록하는 모듈
/// </summary>
public class NatsModule : Module
{
    #region - Ctors -
    public NatsModule(ILogService log)
    {
        _log = log;
        _setup = new NatsSetupModel();
    }

    public NatsModule(INatsSetupModel setup, ILogService log, int count = 0)
    {
        _log = log;
        _setup = setup;
        _count = count;
    }
    #endregion

    #region - Overrides -
    protected override void Load(ContainerBuilder builder)
    {
        try
        {
            // NatsSetupModel을 INatsSetupModel과 NatsSetupModel 둘 다로 등록
            if (_setup is NatsSetupModel natsSetupModel)
            {
                builder.RegisterInstance(natsSetupModel).As<INatsSetupModel>().As<NatsSetupModel>().SingleInstance();
            }
            else
            {
                // INatsSetupModel을 받아서 NatsSetupModel 생성
                var newNatsSetupModel = new NatsSetupModel(_setup);
                builder.RegisterInstance(newNatsSetupModel).As<INatsSetupModel>().As<NatsSetupModel>().SingleInstance();
            }
            
            // NatsService 싱글톤 등록
            builder.Register(ctx =>
            {
                _log?.Info($"{nameof(NatsModule)} is creating a single {nameof(NatsService)} instance by connecting to NATS server.");
                return new NatsService(_log).Connect(_setup);
            })
            .As<INatsService>()
            .As<IService>()
            .SingleInstance()
            .WithMetadata("Order", _count);

            _log?.Info($"{nameof(NatsModule)} registered successfully with Order={_count}");
        }
        catch (Exception ex)
        {
            _log?.Error($"{nameof(NatsModule)} registration failed: {ex.Message}");
            throw;
        }
    }
    #endregion

    #region - Attributes -
    private readonly ILogService _log;
    private readonly INatsSetupModel _setup;
    private readonly int _count;
    #endregion
}
