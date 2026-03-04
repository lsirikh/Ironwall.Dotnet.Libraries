using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties
{
    /****************************************************************************
       Purpose      : PidsGroup 마커의 속성을 제어하는 컨트롤                                                          
       Created By   : GHLee                                                
       Created On   : 9/23/2025                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class GMapPropertyPidsGroupControl : GMapPropertyBaseControl
    {
        #region - Ctors -
        static GMapPropertyPidsGroupControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapPropertyPidsGroupControl),
               new FrameworkPropertyMetadata(typeof(GMapPropertyPidsGroupControl)));
        }

        public GMapPropertyPidsGroupControl()
        {
            MarkerSizeEnabled = false;  // LineSymbol과 동일하게 사이즈 조정 비활성화
        }
        #endregion

        #region - Overrides -

        protected override void ClearSpecificBindings()
        {
            System.Diagnostics.Debug.WriteLine("=== PidsGroupControl ClearSpecificBindings 시작 ===");

            // Line 관련 바인딩 해제
            BindingOperations.ClearBinding(this, LinePatternProperty);
            BindingOperations.ClearBinding(this, LineOpacityProperty);
            BindingOperations.ClearBinding(this, IsClosedPathProperty);
            BindingOperations.ClearBinding(this, TotalDistanceProperty);
            BindingOperations.ClearBinding(this, PointCountProperty);

            // PidsGroup 관련 바인딩 해제
            BindingOperations.ClearBinding(this, LinkedDeviceGroupProperty);
            BindingOperations.ClearBinding(this, EventStatusProperty);

            System.Diagnostics.Debug.WriteLine("=== PidsGroupControl ClearSpecificBindings 완료 ===");
        }

        protected override void SetupSpecificBindings()
        {
            System.Diagnostics.Debug.WriteLine("=== PidsGroupControl SetupSpecificBindings 시작 ===");

            if (SelectedMarker is IPidsGroupEditableMarker pidsGroupMarker)
            {
                System.Diagnostics.Debug.WriteLine($"바인딩 대상: {pidsGroupMarker.GetType().Name}");

                // Line 스타일 속성 바인딩
                var linePatternBinding = CreateTwoWayBinding(nameof(pidsGroupMarker.LinePattern));
                SetBinding(LinePatternProperty, linePatternBinding);

                var lineOpacityBinding = CreateTwoWayBinding(nameof(pidsGroupMarker.LineOpacity));
                SetBinding(LineOpacityProperty, lineOpacityBinding);

                // Line 옵션 바인딩
                var isClosedPathBinding = CreateTwoWayBinding(nameof(pidsGroupMarker.IsClosedPath));
                SetBinding(IsClosedPathProperty, isClosedPathBinding);

                // PidsGroup 속성 바인딩
                var linkedDeviceGroupBinding = CreateTwoWayBinding(nameof(pidsGroupMarker.LinkedDeviceGroup));
                SetBinding(LinkedDeviceGroupProperty, linkedDeviceGroupBinding);

                var eventStatusBinding = CreateTwoWayBinding(nameof(pidsGroupMarker.EventStatus));
                SetBinding(EventStatusProperty, eventStatusBinding);

                // 읽기 전용 속성 바인딩 (OneWay)
                var totalDistanceBinding = new Binding(nameof(pidsGroupMarker.TotalDistance))
                {
                    Source = SelectedMarker,
                    Mode = BindingMode.OneWay
                };
                SetBinding(TotalDistanceProperty, totalDistanceBinding);

                var pointCountBinding = new Binding("RuntimePoints.Count")
                {
                    Source = SelectedMarker,
                    Mode = BindingMode.OneWay
                };
                SetBinding(PointCountProperty, pointCountBinding);
            }

            System.Diagnostics.Debug.WriteLine("=== PidsGroupControl SetupSpecificBindings 완료 ===");
        }

        protected override void SetupSpecificPropertiesFromMarker(IEditableMarker marker)
        {
            if (!(marker is IPidsGroupEditableMarker pidsGroupMarker)) return;

            // Line 속성 로드
            this.LinePattern = pidsGroupMarker.LinePattern;
            this.LineOpacity = pidsGroupMarker.LineOpacity;
            this.IsClosedPath = pidsGroupMarker.IsClosedPath;
            this.TotalDistance = pidsGroupMarker.TotalDistance;
            this.PointCount = pidsGroupMarker.RuntimePoints?.Count ?? 0;

            // PidsGroup 속성 로드
            this.LinkedDeviceGroup = pidsGroupMarker.LinkedDeviceGroup;
            this.EventStatus = pidsGroupMarker.EventStatus;

            System.Diagnostics.Debug.WriteLine($"PidsGroup 마커에서 속성 로드 완료");
        }

        protected override void UpdateSpecificProperties()
        {
            if (SelectedMarker is IPidsGroupEditableMarker pidsGroupMarker)
            {
                // Line 속성 업데이트
                pidsGroupMarker.LinePattern = this.LinePattern;
                pidsGroupMarker.LineOpacity = this.LineOpacity;
                pidsGroupMarker.IsClosedPath = this.IsClosedPath;

                // PidsGroup 속성 업데이트
                pidsGroupMarker.LinkedDeviceGroup = this.LinkedDeviceGroup;
                pidsGroupMarker.EventStatus = this.EventStatus;
            }
        }

        public override Type GetSupportedMarkerType()
        {
            return typeof(GMapPropertyPidsGroupControl);
        }

        #endregion

        #region - Dependency Properties -

        #region Line Properties

        /// <summary>
        /// 라인 패턴
        /// </summary>
        public EnumLinePattern LinePattern
        {
            get { return (EnumLinePattern)GetValue(LinePatternProperty); }
            set { SetValue(LinePatternProperty, value); }
        }

        public static readonly DependencyProperty LinePatternProperty =
            DependencyProperty.Register("LinePattern", typeof(EnumLinePattern),
                typeof(GMapPropertyPidsGroupControl),
                new PropertyMetadata(EnumLinePattern.Solid, OnLinePatternChanged));

        /// <summary>
        /// 라인 투명도
        /// </summary>
        public double LineOpacity
        {
            get { return (double)GetValue(LineOpacityProperty); }
            set { SetValue(LineOpacityProperty, value); }
        }

        public static readonly DependencyProperty LineOpacityProperty =
            DependencyProperty.Register("LineOpacity", typeof(double),
                typeof(GMapPropertyPidsGroupControl),
                new PropertyMetadata(1.0, OnLineOpacityChanged, CoerceDoubleValue));

        /// <summary>
        /// 닫힌 경로 여부
        /// </summary>
        public bool IsClosedPath
        {
            get { return (bool)GetValue(IsClosedPathProperty); }
            set { SetValue(IsClosedPathProperty, value); }
        }

        public static readonly DependencyProperty IsClosedPathProperty =
            DependencyProperty.Register("IsClosedPath", typeof(bool),
                typeof(GMapPropertyPidsGroupControl),
                new PropertyMetadata(false, OnIsClosedPathChanged));

        /// <summary>
        /// 총 거리 (읽기 전용)
        /// </summary>
        public double TotalDistance
        {
            get { return (double)GetValue(TotalDistanceProperty); }
            set { SetValue(TotalDistanceProperty, value); }
        }

        public static readonly DependencyProperty TotalDistanceProperty =
            DependencyProperty.Register("TotalDistance", typeof(double),
                typeof(GMapPropertyPidsGroupControl),
                new PropertyMetadata(0.0));

        /// <summary>
        /// 포인트 개수 (읽기 전용)
        /// </summary>
        public int PointCount
        {
            get { return (int)GetValue(PointCountProperty); }
            set { SetValue(PointCountProperty, value); }
        }

        public static readonly DependencyProperty PointCountProperty =
            DependencyProperty.Register("PointCount", typeof(int),
                typeof(GMapPropertyPidsGroupControl),
                new PropertyMetadata(0));

        #endregion

        #region PidsGroup Specific Properties

        /// <summary>
        /// 선택 가능한 디바이스 그룹 목록 (PropertyPanelFactory에서 주입)
        /// </summary>
        public ObservableCollection<IDeviceGroupModel> FilteredDeviceGroupList
        {
            get { return (ObservableCollection<IDeviceGroupModel>)GetValue(FilteredDeviceGroupListProperty); }
            set { SetValue(FilteredDeviceGroupListProperty, value); }
        }

        public static readonly DependencyProperty FilteredDeviceGroupListProperty =
            DependencyProperty.Register("FilteredDeviceGroupList", typeof(ObservableCollection<IDeviceGroupModel>),
                typeof(GMapPropertyPidsGroupControl), new PropertyMetadata(null));

        /// <summary>
        /// 연결된 디바이스 그룹 ID
        /// </summary>
        public int LinkedDeviceGroup
        {
            get { return (int)GetValue(LinkedDeviceGroupProperty); }
            set { SetValue(LinkedDeviceGroupProperty, value); }
        }

        public static readonly DependencyProperty LinkedDeviceGroupProperty =
            DependencyProperty.Register("LinkedDeviceGroup", typeof(int),
                typeof(GMapPropertyPidsGroupControl),
                new PropertyMetadata(0, OnLinkedDeviceGroupChanged));

        /// <summary>
        /// 이벤트 상태
        /// </summary>
        public EnumEventStatus EventStatus
        {
            get { return (EnumEventStatus)GetValue(EventStatusProperty); }
            set { SetValue(EventStatusProperty, value); }
        }

        public static readonly DependencyProperty EventStatusProperty =
            DependencyProperty.Register("EventStatus", typeof(EnumEventStatus),
                typeof(GMapPropertyPidsGroupControl),
                new PropertyMetadata(EnumEventStatus.Normal, OnEventStatusChanged));

        #endregion

        #endregion

        #region - Property Changed Methods -

        #region Line Property Changed Methods

        private static void OnLinePatternChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnLinePatternChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsGroupControl control &&
                control.SelectedMarker is IPidsGroupEditableMarker pidsGroupMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                pidsGroupMarker.LinePattern = (EnumLinePattern)e.NewValue;
                control.OnMarkerPropertyChanged("LinePattern", e.OldValue, e.NewValue);
            }
        }

        private static void OnLineOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnLineOpacityChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsGroupControl control &&
                control.SelectedMarker is IPidsGroupEditableMarker pidsGroupMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                pidsGroupMarker.LineOpacity = (double)e.NewValue;
                control.OnMarkerPropertyChanged("LineOpacity", e.OldValue, e.NewValue);
            }
        }

        private static void OnIsClosedPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnIsClosedPathChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsGroupControl control &&
                control.SelectedMarker is IPidsGroupEditableMarker pidsGroupMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                pidsGroupMarker.IsClosedPath = (bool)e.NewValue;
                control.OnMarkerPropertyChanged("IsClosedPath", e.OldValue, e.NewValue);
            }
        }

        #endregion

        #region PidsGroup Property Changed Methods

        private static void OnLinkedDeviceGroupChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnLinkedDeviceGroupChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsGroupControl control &&
                control.SelectedMarker is IPidsGroupEditableMarker pidsGroupMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                pidsGroupMarker.LinkedDeviceGroup = (int)e.NewValue;
                control.OnMarkerPropertyChanged("LinkedDeviceGroup", e.OldValue, e.NewValue);
            }
        }

        private static void OnEventStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnEventStatusChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsGroupControl control &&
                control.SelectedMarker is IPidsGroupEditableMarker pidsGroupMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                pidsGroupMarker.EventStatus = (EnumEventStatus)e.NewValue;
                control.OnMarkerPropertyChanged("EventStatus", e.OldValue, e.NewValue);
            }
        }

        #endregion

        #endregion
    }
}