using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
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
        public PropertyPanelFactory(ILogService log, DeviceProvider deviceProvider)
        {
            _log = log;
            _deviceProvider = deviceProvider;

            _markerToPanelMap = new Dictionary<Type, Type>
        {
            { typeof(GMapCustomMarker), typeof(GMapPropertyCustomControl) },
            { typeof(GMapGeometricMarker), typeof(GMapPropertyGeometricControl) },
            { typeof(GMapPidsMarker), typeof(GMapPropertyPidsControl) },
            { typeof(GMapMilitarySymbolMarker), typeof(GMapPropertyMilitaryControl) },
            { typeof(GMapLineMarker), typeof(GMapPropertyLineControl) },
            { typeof(GMapInfraMarker), typeof(GMapPropertyInfraControl) },
            { typeof(GMapPidsGroupMarker), typeof(GMapPropertyPidsGroupControl) },
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

                // FilteredDeviceList 설정 후에 SelectedMarker 설정
                // → OnSelectedMarkerChanged에서 LinkedDevice가 FilteredDeviceList와 매칭됨
                panel.SelectedMarker = marker;

                _log?.Info($"Created {panelType.Name} for {markerType.Name}");
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
            var allDevices = _deviceProvider.ToList();

            // DeviceType에 따라 필터링
            var filteredDevices = FilterDevicesByType(allDevices, deviceType);
            panel.FilteredDeviceList = new ObservableCollection<IBaseDeviceModel>(filteredDevices);

            _log?.Info($"PIDS Panel FilteredDeviceList 설정: {filteredDevices.Count()}개 (DeviceType: {deviceType})");
        }

        /// <summary>
        /// DeviceType에 따라 디바이스 목록을 필터링합니다.
        /// </summary>
        private IEnumerable<IBaseDeviceModel> FilterDevicesByType(
            IEnumerable<IBaseDeviceModel> devices,
            Libraries.Enums.EnumDeviceType targetType)
        {
            // DeviceType 필터링 규칙:
            // - Controller: Controller만
            // - Multi: Multi만
            // - Fence 계열 (Fence, Underground, Contact, PIR, Laser, Cable, OpticalCable): 센서 계열
            // - IpCamera: IpCamera만

            return targetType switch
            {
                Libraries.Enums.EnumDeviceType.Controller =>
                    devices.Where(d => d.DeviceType == Libraries.Enums.EnumDeviceType.Controller),

                Libraries.Enums.EnumDeviceType.Multi =>
                    devices.Where(d => d.DeviceType == Libraries.Enums.EnumDeviceType.Multi),

                Libraries.Enums.EnumDeviceType.IpCamera =>
                    devices.Where(d => d.DeviceType == Libraries.Enums.EnumDeviceType.IpCamera),

                // Fence 계열 센서들
                Libraries.Enums.EnumDeviceType.Fence or
                Libraries.Enums.EnumDeviceType.Underground or
                Libraries.Enums.EnumDeviceType.Contact or
                Libraries.Enums.EnumDeviceType.PIR or
                Libraries.Enums.EnumDeviceType.Laser or
                Libraries.Enums.EnumDeviceType.Cable or
                Libraries.Enums.EnumDeviceType.OpticalCable =>
                    devices.Where(d =>
                        d.DeviceType == Libraries.Enums.EnumDeviceType.Fence ||
                        d.DeviceType == Libraries.Enums.EnumDeviceType.Underground ||
                        d.DeviceType == Libraries.Enums.EnumDeviceType.Contact ||
                        d.DeviceType == Libraries.Enums.EnumDeviceType.PIR ||
                        d.DeviceType == Libraries.Enums.EnumDeviceType.Laser ||
                        d.DeviceType == Libraries.Enums.EnumDeviceType.Cable ||
                        d.DeviceType == Libraries.Enums.EnumDeviceType.OpticalCable),

                // 기타: 전체 목록
                _ => devices
            };
        }
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        #endregion
        #region - Attributes -
        private ILogService _log;
        private DeviceProvider _deviceProvider;
        private Dictionary<Type, Type> _markerToPanelMap;
        #endregion
    }
       
}