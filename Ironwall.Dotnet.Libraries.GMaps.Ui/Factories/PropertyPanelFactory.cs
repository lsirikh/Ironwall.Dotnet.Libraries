using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Factories{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/2/2025 2:06:34 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class PropertyPanelFactory : IPropertyPanelFactory
    {
        #region - Ctors -
        public PropertyPanelFactory(ILogService log)
        {
            _log = log;

            _markerToPanelMap = new Dictionary<Type, Type>
        {
            { typeof(GMapCustomMarker), typeof(GMapPropertyCustomControl) },
            { typeof(GMapGeometricMarker), typeof(GMapPropertyGeometricControl) },
            { typeof(GMapPidsMarker), typeof(GMapPropertyPidsControl) },
            { typeof(GMapMilitarySymbolMarker), typeof(GMapPropertyMilitaryControl) },
            { typeof(GMapLineMarker), typeof(GMapPropertyLineControl) }
        };
        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        public GMapPropertyBaseControl CreatePropertyPanel(IEditableMarker marker)
        {
            if (marker == null) return null;

            var markerType = marker.GetType();

            if (_markerToPanelMap.TryGetValue(markerType, out var panelType))
            {
                var panel = Activator.CreateInstance(panelType) as GMapPropertyBaseControl;
                panel.SelectedMarker = marker;

                _log?.Info($"Created {panelType.Name} for {markerType.Name}");
                return panel;
            }

            _log?.Warning($"No property panel registered for {markerType.Name}");
            return null;
        }
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        #endregion
        #region - Attributes -
        private ILogService _log;
        private Dictionary<Type, Type> _markerToPanelMap;
        #endregion
    }
       
}