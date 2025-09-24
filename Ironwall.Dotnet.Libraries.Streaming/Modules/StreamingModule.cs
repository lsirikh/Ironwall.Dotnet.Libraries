using Autofac.Core.Registration;
using Autofac.Core;
using Autofac;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Streaming.Models;
using Ironwall.Dotnet.Libraries.Streaming.Serivces;
using System;

namespace Ironwall.Dotnet.Libraries.Streaming.Modules;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/24/2025 3:31:38 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// Streaming Module for Autofac DI
/// </summary>
public class StreamingModule : Module
{
    #region - Ctors -
    public StreamingModule(IStreamingSetupModel model, ILogService log = default, int count = default)
    {
        _log = log;
        _count = count;
        _model = model;
    }
    #endregion

    #region - Implementation of Interface -
    protected override void Load(ContainerBuilder builder)
    {
        try
        {
            _log?.Info("[StreamingModule] Loading streaming module...");

            // Setup Model 등록
            var setupModel = new StreamingSetupModel(_model);
            builder.RegisterInstance(setupModel).As<StreamingSetupModel>()
                .As<IStreamingSetupModel>().SingleInstance();

            // Context Pool 등록
            builder.Register(c => new StreamingContextPool(setupModel.ContextPoolSize))
                .As<IStreamingContextPool>()
                .SingleInstance()
                .OnActivated(e =>
                {
                    _log?.Info("[StreamingModule] StreamingContextPool activated");
                });

            // RtspStreamingService 등록 (Autofac DI 사용)
            builder.RegisterType<RtspStreamingService>()
                .As<IRtspStreamingService>()
                .As<IService>()
                .SingleInstance()
                .WithMetadata("Order", _count)
                .OnActivated(e =>
                {
                    _log?.Info("[StreamingModule] RtspStreamingService activated");
                })
                .OnRelease(e =>
                {
                    _log?.Info("[StreamingModule] RtspStreamingService releasing...");
                    try
                    {
                        e?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _log?.Error($"[StreamingModule] Error disposing service: {ex.Message}");
                    }
                });

            _log?.Info($"[StreamingModule] Module loaded successfully with order: {_count}");
        }
        catch (Exception ex)
        {
            _log?.Error($"[StreamingModule] Failed to load module: {ex.Message}");
            throw;
        }
    }
    #endregion

    #region - Overrides -
    //protected override void AttachToComponentRegistration(
    //    IComponentRegistryBuilder componentRegistry,
    //    IComponentRegistration registration)
    //{
    //    base.AttachToComponentRegistration(componentRegistry, registration);

    //    // 컴포넌트 생성/해제 로깅
    //    registration.Activated += (sender, e) =>
    //    {
    //        var typeName = e.Instance.GetType().Name;
    //        _log?.Debug($"[StreamingModule] Component activated: {typeName}");
    //    };

    //    registration.Deactivating += (sender, e) =>
    //    {
    //        var typeName = e.Instance.GetType().Name;
    //        _log?.Debug($"[StreamingModule] Component deactivating: {typeName}");
    //    };
    //}
    #endregion

    #region - Attributes -
    private readonly ILogService _log;
    private readonly int _count;
    private readonly IStreamingSetupModel _model;
    #endregion
}