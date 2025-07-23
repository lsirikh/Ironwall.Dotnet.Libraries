using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/1/2025 8:27:51 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public abstract class EventCardViewModel<T> : EventCardBaseViewModel where T : IExEventModel
    {
        #region - Ctors -
        protected EventCardViewModel(T model) 
        {
            _model = model;
        }

        protected EventCardViewModel(IEventAggregator ea, ILogService log, T model)
            : base(ea, log)
        {
            _model = model;
        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        public override Task TaskFinal() => SendAction(content: Contents, idUser: IdUser);
        protected abstract Task CloseDialog();
        public async virtual Task SendAction(string? content, string? idUser)
        {
            _log?.Info($"CardViewModel SendAction : User({IdUser}), Content({Contents}), Device({Device?.DeviceName}), DateTime({DateTime})");
            if (Cts != null && !Cts.IsCancellationRequested)
            {
                Cts.Cancel();
                Cts.Dispose();
            }
            await CloseDialog();
        }
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        public string? IdUser { get; set; } 
        public string? Contents { get; set; }
        public string? EventGroup => _model.EventGroup;  
        public IBaseDeviceModel? Device => _model.Device;
        public EnumTrueFalse Status => _model.Status;
        public EnumEventType MessageType => _model.MessageType;
        public override IExEventModel Model => _model;
        #endregion
        #region - Attributes -
        protected readonly T _model;
        #endregion
    }
}