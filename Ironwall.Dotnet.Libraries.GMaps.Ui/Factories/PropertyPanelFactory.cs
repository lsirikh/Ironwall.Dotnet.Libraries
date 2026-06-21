using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

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
        public PropertyPanelFactory(ILogService log, DeviceProvider deviceProvider, DeviceGroupProvider deviceGroupProvider)
        {
            _log = log;
            _deviceProvider = deviceProvider;
            _deviceGroupProvider = deviceGroupProvider;

            _markerToPanelMap = new Dictionary<Type, Type>
        {
            { typeof(GMapCustomMarker), typeof(GMapPropertyCustomControl) },
            { typeof(GMapGeometricMarker), typeof(GMapPropertyGeometricControl) },
            { typeof(GMapPidsMarker), typeof(GMapPropertyPidsControl) },
            { typeof(GMapMilitarySymbolMarker), typeof(GMapPropertyMilitaryControl) },
            { typeof(GMapLineMarker), typeof(GMapPropertyLineControl) },
            { typeof(GMapInfraMarker), typeof(GMapPropertyInfraControl) },
            { typeof(GMapPidsGroupMarker), typeof(GMapPropertyPidsGroupControl) },
            { typeof(GMapImageMarker), typeof(GMapPropertyImageControl) },
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

                // 중요: PIDS 패널인 경우 FilteredDeviceList를 SelectedMarker 설정 전에 먼저 초기화
                // WPF ComboBox는 ItemsSource가 먼저 설정되어야 SelectedItem이 제대로 매칭됨
                if (panel is GMapPropertyPidsControl pidsPanel && marker is IPidsEditableMarker pidsMarker)
                {
                    SetupPidsDeviceList(pidsPanel, pidsMarker);
                    _log?.Info($"PIDS FilteredDeviceList 선행 설정 완료 (SelectedMarker 설정 전)");
                }

                // PidsGroup 패널인 경우 FilteredDeviceGroupList를 SelectedMarker 설정 전에 초기화
                if (panel is GMapPropertyPidsGroupControl pidsGroupPanel)
                {
                    SetupPidsGroupList(pidsGroupPanel);
                    _log?.Info($"PidsGroup FilteredDeviceGroupList 선행 설정 완료 (SelectedMarker 설정 전)");
                }

                // FilteredDeviceList 설정 후에 SelectedMarker 설정
                // → OnSelectedMarkerChanged에서 LinkedDevice가 FilteredDeviceList와 매칭됨
                panel.SelectedMarker = marker;

                //_log?.Info($"Created {panelType.Name} for {markerType.Name}");
                return panel;
            }

            _log?.Warning($"No property panel registered for {markerType.Name}");
            return null;
        }

        /// <summary>
        /// PIDS 마커의 DeviceType에 따라 FilteredDeviceList를 설정합니다.
        /// </summary>
        private void SetupPidsDeviceList(GMapPropertyPidsControl panel, IPidsEditableMarker marker)
        {
            if (_deviceProvider == null) return;

            var deviceType = marker.DeviceType;
            // (B6 방어계층) Temp(미저장 Id≤0) 장비는 PIDS 픽커 후보 제외 — Draft 격리와 이중 방어.
            var allDevices = _deviceProvider.Where(d => d.Id > 0).ToList();

            // DeviceType에 따라 필터링
            var filteredDevices = FilterDevicesByType(allDevices, deviceType);
            panel.FilteredDeviceList = new ObservableCollection<IBaseDeviceModel>(filteredDevices);

            _log?.Info($"PIDS Panel FilteredDeviceList 설정: {filteredDevices.Count()}개 (DeviceType: {deviceType})");
        }

        /// <summary>
        /// PidsGroup 패널의 FilteredDeviceGroupList를 DeviceGroupProvider로 채웁니다.
        /// </summary>
        private void SetupPidsGroupList(GMapPropertyPidsGroupControl panel)
        {
            var groups = _deviceGroupProvider?.ToList() ?? new System.Collections.Generic.List<IDeviceGroupModel>();
            panel.FilteredDeviceGroupList = new ObservableCollection<IDeviceGroupModel>(groups);
            _log?.Info($"PidsGroup Panel FilteredDeviceGroupList 설정: {groups.Count}개");
        }

        private IEnumerable<IBaseDeviceModel> FilterDevicesByType(
            IEnumerable<IBaseDeviceModel> devices,
            Libraries.Enums.EnumDeviceType targetType)
        {
            return DeviceFilterHelper.FilterDevicesByType(devices, targetType);
        }
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        #endregion
        #region - Attributes -
        private ILogService _log;
        private DeviceProvider _deviceProvider;
        private DeviceGroupProvider _deviceGroupProvider;
        private Dictionary<Type, Type> _markerToPanelMap;
        #endregion
    }
       
}