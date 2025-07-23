using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/19/2025 9:48:20 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class OnvifDialogViewModel: BasePanelViewModel
    {
        #region - Ctors -
        public OnvifDialogViewModel(IEventAggregator eventAggregator
                                    , ILogService log) 
                                    : base(eventAggregator, log)
        {
        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        protected override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            try
            {
                await base.OnActivateAsync(cancellationToken);
                try
                {
                    if (_model == null) return;
                    /*────────── 하위 VM 인스턴스 생성 ──────────*/
                    CameraInfoViewModel = new CameraInfoViewModel(_model.Identification?? new CameraInfoModel());
                    CameraOpticsViewModel = new CameraOpticsViewModel(_model.Optics ?? new CameraOpticsModel());
                    CameraPositionViewModel = new CameraPositionViewModel(_model.Position ?? new CameraPositionModel());
                    CameraPtzCapabilityViewModel = new CameraPtzCapabilityViewModel(_model.PtzCapability ?? new CameraPtzCapabilityModel());
                    CameraPresetsViewModel = new CameraPresetsViewModel(_model.Presets ?? new List<ICameraPresetModel>());

                    
                    /*────────── Conductor(탭) 에 등록 & 활성화 ──────────*/
                    await ActivateItemAsync(CameraInfoViewModel, cancellationToken);
                    await ActivateItemAsync(CameraOpticsViewModel, cancellationToken);
                    await ActivateItemAsync(CameraPositionViewModel, cancellationToken);
                    await ActivateItemAsync(CameraPtzCapabilityViewModel, cancellationToken);
                    await ActivateItemAsync(CameraPresetsViewModel, cancellationToken);


                    Refresh();
                }
                catch (OperationCanceledException)
                {
                    // 화면 열기 도중 창이 닫히면 무시
                }
                catch (Exception ex)
                {
                    _log?.Error($"[{GetType().Name}] 활성화 실패 : {ex.Message}");
                }

            }
            catch (Exception)
            {
            }
        }

        protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            try
            {
                base.OnDeactivateAsync(close, cancellationToken);

            }
            catch (Exception)
            {
            }

            return Task.CompletedTask;
        }
        #endregion
        #region - Binding Methods -
        public void UpdateModel(ICameraDeviceModel model)
        {
            _model = model;
            Refresh();
        }
        #endregion
        #region - Processes -
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        public CameraInfoViewModel? CameraInfoViewModel { get; private set; }
        public CameraOpticsViewModel? CameraOpticsViewModel { get; private set; }
        public CameraPositionViewModel? CameraPositionViewModel { get; private set; }
        public CameraPtzCapabilityViewModel? CameraPtzCapabilityViewModel { get; private set; }
        public CameraPresetsViewModel? CameraPresetsViewModel { get; private set; }
        #endregion
        #region - Attributes -
        private ICameraDeviceModel? _model;

        #endregion
    }
}