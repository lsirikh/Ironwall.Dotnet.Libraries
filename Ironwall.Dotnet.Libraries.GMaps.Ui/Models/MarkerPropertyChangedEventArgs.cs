using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Models{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/1/2025 11:32:36 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    /// <summary>
    /// 마커 속성 변경 이벤트 아규먼트
    /// </summary>
    public class MarkerPropertyChangedEventArgs : EventArgs
    {
        public string PropertyName { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }
        public IEditableMarker Marker { get; set; }
    }

    public class PropertyPanelCloseRequestedEvent {}
}