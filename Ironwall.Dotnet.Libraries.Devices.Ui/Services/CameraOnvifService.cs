using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Db.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models;
using Ironwall.Dotnet.Libraries.OnvifSolution.Models;
using Ironwall.Dotnet.Libraries.OnvifSolution.Services;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Services{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/13/2025 6:14:52 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class CameraOnvifService : TaskService
    {
        
        #region - Ctors -
        public CameraOnvifService(IEventAggregator eventAggregator,
                                ILogService log, 
                                IOnvifService onvifService, 
                                CameraDeviceProvider cameraDeviceProvider,
                                IDeviceDbService dbService)
        {
            _onvifService = onvifService;
            _deviceProvider = cameraDeviceProvider;
            _eventAggregator = eventAggregator;
            _log = log;
            _dbService = dbService;

        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        protected override Task RunTask(CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        protected override Task ExitTask(CancellationToken token = default)
        {
            return Task.CompletedTask;
        }
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        public async Task<ICameraOnvifModel?> CreateOnvifInstance(ICameraDeviceModel model, CancellationToken ct = default)
        {
            try
            {
                IConnectionModel conn = new ConnectionModel()
                {
                    Username = model.Username,
                    Password = model.Password,
                    IpAddress = model.IpAddress,
                    Port = model.Port,
                };
                return await _onvifService.InitializeFullAsync(conn, ct);
            }
            catch (TaskCanceledException ex)
            {
                _log.Error(ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                _log.Error(ex.Message);
                return null;
            }
        }
        public void CameraInsert(ICameraOnvifModel model)
        {
            try
            {

            }
            catch (Exception)
            {

                throw;
            }
        }

        public void CameraDelete(ICameraDeviceModel model)
        {
            try
            {

            }
            catch (Exception)
            {

                throw;
            }
        }

        public void GetPreset(ICameraDeviceModel model)
        {
            try
            {

            }
            catch (Exception)
            {

                throw;
            }
        }

        public void GetProfile(ICameraDeviceModel model)
        {
            try
            {

            }
            catch (Exception)
            {

                throw;
            }
        }
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        #endregion
        #region - Attributes -
        private IOnvifService _onvifService;
        private CameraDeviceProvider _deviceProvider;
        private IEventAggregator _eventAggregator;
        private ILogService _log;
        private IDeviceDbService _dbService;
        #endregion

    }
}