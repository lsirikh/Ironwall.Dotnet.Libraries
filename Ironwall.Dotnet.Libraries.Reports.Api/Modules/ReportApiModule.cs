using Autofac;
using Ironwall.Dotnet.Libraries.Accounts.Api.Handlers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Modules;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;

namespace Ironwall.Dotnet.Libraries.Reports.Api.Modules;

/// <summary>
/// 보고서 API 모듈 (Autofac) — EventApiModule 패턴 복제.
/// Bearer 부착 HTTP 파이프라인 + ReportApiService 등록. 401 시 single-flight refresh + 최종실패 ForceLogout.
/// </summary>
public class ReportApiModule : Module
{
    #region - Ctors -
    public ReportApiModule(ILogService? log, ApiSetupModel setup, string name = "ReportApi", int count = 100)
    {
        _log = log;
        _setup = setup;
        _name = name;
        _count = count;
    }
    #endregion

    #region - Implementation of Interface -
    protected override void Load(ContainerBuilder builder)
    {
        try
        {
            // 1. ApiModule 등록(내부 IApiService) + Bearer 부착(AUTH_MODE=token 대비). ITokenStorageService 공유(미등록=DB모드→미부착).
            builder.RegisterModule(new ApiModule(_log, _setup, $"{_name}", _count, authHandlerFactory: ctx =>
            {
                var store = ctx.ResolveOptional<ITokenStorageService>();
                if (store is null) return null;   // DB모드 등 미등록 → Bearer 미부착
                var scope = ctx.Resolve<ILifetimeScope>();
                var handler = new BearerAuthHandler(store, () => scope.Resolve<IAccountApiService>(), _log);
                handler.SessionExpired += () =>
                {
                    try { scope.Resolve<ISessionLifecycle>().ForceLogoutOnce(EnumRevokeReason.Unauthorized); }
                    catch (Exception ex) { _log?.Warning($"[{nameof(ReportApiModule)}] SessionExpired→ForceLogout 실패: {ex.Message}"); }
                };
                return handler;
            }));

            // 2. ApiSetupModel 등록
            builder.RegisterInstance(_setup).Named<ApiSetupModel>(_name).SingleInstance();

            // 3. ReportApiService 등록
            builder.Register(ctx => new ReportApiService(
                    _log,
                    ctx.ResolveNamed<IApiService>($"{_name}"),
                    ctx.ResolveNamed<ApiSetupModel>(_name)
                ))
                .Named<IReportApiService>(_name)
                .AsImplementedInterfaces()
                .SingleInstance()
                .WithMetadata("Order", _count);

            _log?.Info($"[{nameof(ReportApiModule)}] Module loaded successfully with name: {_name}");
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(ReportApiModule)}] Failed to load module: {ex.Message}");
            throw;
        }
    }
    #endregion

    #region - Attributes -
    private readonly ILogService? _log;
    private readonly ApiSetupModel _setup;
    private readonly string _name;
    private readonly int _count;
    #endregion
}
