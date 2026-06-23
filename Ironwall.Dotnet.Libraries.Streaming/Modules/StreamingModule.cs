using Autofac.Core.Registration;
using Autofac.Core;
using Autofac;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Streaming.Base.Hub;
using Ironwall.Dotnet.Libraries.Streaming.Hub;
using Ironwall.Dotnet.Libraries.Streaming.Infrastructure;
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
            // (1) 서비스/Hub용 concrete 스냅샷. 팝업 수명은 MapViewModel이 관장하므로 서비스 내부
            //     자동폐기(CheckTimeouts)는 비활성 — 더블/리셋 시 라이브 타임아웃과 어긋나지 않게 한다.
            var setupModel = new StreamingSetupModel(_model) { IsAutoDiscard = false };
            builder.RegisterInstance(setupModel).As<StreamingSetupModel>().SingleInstance();

            // (2) 라이브러리 소비자(MapViewModel)가 라이브 설정(IsCameraPopupUsed/TimeoutSeconds/IsAutoDiscard)을
            //     읽도록 메인 SetupModel(라이브 인스턴스)을 IStreamingSetupModel로 등록.
            builder.RegisterInstance(_model).As<IStreamingSetupModel>().SingleInstance();

            // Context Pool 등록
            builder.Register(c => new StreamingContextPool(setupModel.ContextPoolSize))
                .As<IStreamingContextPool>()
                .SingleInstance()
                .OnActivated(e =>
                {
                    _log?.Info("[StreamingModule] StreamingContextPool activated");
                });

            // ImprovedRtspStreamingService 등록 (IRtspStreamingService와 IPlayerRegistry 인터페이스로)
            builder.RegisterType<ImprovedRtspStreamingService>()
                .As<IImprovedRtspStreamingService>()
                .As<IPlayerRegistry>()
                .As<IService>()
                .SingleInstance()
                .WithMetadata("Order", _count)
                .OnActivated(e =>
                {
                    _log?.Info("[ImprovedStreamingModule] ImprovedRtspStreamingService activated");

                    // Service 초기화 확인
                    var service = e.Instance as ImprovedRtspStreamingService;
                    if (service != null)
                    {
                        _log?.Info($"[ImprovedStreamingModule] Service ready - Type: {service.GetType().Name}");
                    }
                })
                .OnRelease(e =>
                {
                    _log?.Info("[ImprovedStreamingModule] ImprovedRtspStreamingService releasing...");
                    try
                    {
                        e?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _log?.Error($"[ImprovedStreamingModule] Error disposing service: {ex.Message}");
                    }
                });

            // RelayPortPool 등록 (기본 포트 범위 15554~15700)
            builder.RegisterType<RelayPortPool>()
                .AsSelf()
                .SingleInstance();

            // CameraStreamHub 등록
            builder.RegisterType<CameraStreamHub>()
                .As<ISharedCameraStreamHub>()
                .SingleInstance()
                .OnRelease(h => ((IDisposable)h).Dispose());

            // PopupViewerService 등록 — ISharedCameraStreamHub 자동 주입
            builder.RegisterType<PopupViewerService>()
                .As<IPopupViewerService>()
                .SingleInstance()
                .OnActivated(e =>
                {
                    _log?.Info("[StreamingModule] PopupViewerService activated");
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
    #endregion

    #region - Attributes -
    private readonly ILogService _log;
    private readonly int _count;
    private readonly IStreamingSetupModel _model;
    #endregion
}