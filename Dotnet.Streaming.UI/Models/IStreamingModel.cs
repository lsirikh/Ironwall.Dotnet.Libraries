using RtspClientSharp;
using System;

namespace Dotnet.Streaming.UI.Models
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 5/3/2024 4:13:36 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public interface IStreamingModel
    {
        #region - Ctors -
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        void Start(ConnectionParameters connectionParameters);
        void Stop();
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        IVideoSource VideoSource { get; }

        #endregion
        #region - Attributes -
        event EventHandler<string> StatusChanged;
        #endregion
    }
}