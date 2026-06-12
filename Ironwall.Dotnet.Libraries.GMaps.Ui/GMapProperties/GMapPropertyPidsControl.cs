using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties
{
    /****************************************************************************
       Purpose      : PIDS 디바이스 마커의 속성을 제어하는 컨트롤                                                          
       Created By   : GHLee                                                
       Created On   : 9/3/2025                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class GMapPropertyPidsControl : GMapPropertyBaseControl
    {
        #region - Ctors -
        static GMapPropertyPidsControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapPropertyPidsControl),
                new FrameworkPropertyMetadata(typeof(GMapPropertyPidsControl)));
        }

        public GMapPropertyPidsControl()
        {
        }
        #endregion
        
        #region - Overrides -

        protected override void ClearSpecificBindings()
        {
            //System.Diagnostics.Debug.WriteLine("=== PidsControl ClearSpecificBindings 시작 ===");

            BindingOperations.ClearBinding(this, LinkedDeviceIdProperty);
            BindingOperations.ClearBinding(this, LinkedDeviceProperty);
            BindingOperations.ClearBinding(this, DetectionRangeProperty);
            BindingOperations.ClearBinding(this, DetectionAngleProperty);
            BindingOperations.ClearBinding(this, DetectionBearingProperty);
            BindingOperations.ClearBinding(this, BaseBearingProperty);
            BindingOperations.ClearBinding(this, ShowFOVProperty);
            BindingOperations.ClearBinding(this, FOVColorProperty);
            BindingOperations.ClearBinding(this, FOVOpacityProperty);

            //System.Diagnostics.Debug.WriteLine("=== PidsControl ClearSpecificBindings 완료 ===");
        }

        protected override void SetupSpecificBindings()
        {
            //System.Diagnostics.Debug.WriteLine("=== PidsControl SetupSpecificBindings 시작 ===");

            if (SelectedMarker is IPidsEditableMarker pidsMarker)
            {
                //System.Diagnostics.Debug.WriteLine($"바인딩 대상: {pidsMarker.GetType().Name}");

                // LinkedDeviceId 바인딩
                var linkedDeviceIdBinding = CreateTwoWayBinding(nameof(pidsMarker.LinkedDeviceId));
                SetBinding(LinkedDeviceIdProperty, linkedDeviceIdBinding);

                // LinkedDevice 바인딩 (런타임 디바이스 객체)
                var linkedDeviceBinding = CreateTwoWayBinding(nameof(pidsMarker.LinkedDevice));
                SetBinding(LinkedDeviceProperty, linkedDeviceBinding);

                // ShowFOV 바인딩
                var showFOVBinding = CreateTwoWayBinding(nameof(pidsMarker.ShowFOV));
                SetBinding(ShowFOVProperty, showFOVBinding);

                // FOVColor 바인딩
                var fovColorBinding = CreateTwoWayBinding(nameof(pidsMarker.FOVColor));
                SetBinding(FOVColorProperty, fovColorBinding);

                // FOVOpacity 바인딩
                var fovOpacityBinding = CreateTwoWayBinding(nameof(pidsMarker.FOVOpacity));
                SetBinding(FOVOpacityProperty, fovOpacityBinding);

                // Detection 속성들 바인딩 추가
                var detectionRangeBinding = CreateTwoWayBinding(nameof(pidsMarker.DetectionRange));
                SetBinding(DetectionRangeProperty, detectionRangeBinding);

                var detectionAngleBinding = CreateTwoWayBinding(nameof(pidsMarker.DetectionAngle));
                SetBinding(DetectionAngleProperty, detectionAngleBinding);

                var detectionBearingBinding = CreateTwoWayBinding(nameof(pidsMarker.DetectionBearing));
                SetBinding(DetectionBearingProperty, detectionBearingBinding);

                var baseBearingBinding = CreateTwoWayBinding(nameof(pidsMarker.BaseBearing));
                SetBinding(BaseBearingProperty, baseBearingBinding);
            }

            //System.Diagnostics.Debug.WriteLine("=== PidsControl SetupSpecificBindings 완료 ===");
        }

        protected override void SetupSpecificPropertiesFromMarker(IEditableMarker marker)
        {
            if (!(marker is IPidsEditableMarker pidsMarker)) return;

            //System.Diagnostics.Debug.WriteLine($"=== SetupSpecificPropertiesFromMarker 시작 ===");
            //System.Diagnostics.Debug.WriteLine($"  마커 Title: {pidsMarker.Title}");
            //System.Diagnostics.Debug.WriteLine($"  마커 LinkedDeviceId: {pidsMarker.LinkedDeviceId}");
            //System.Diagnostics.Debug.WriteLine($"  마커 LinkedDevice: {pidsMarker.LinkedDevice?.DeviceName ?? "null"}");
            //System.Diagnostics.Debug.WriteLine($"  FilteredDeviceList Count: {FilteredDeviceList?.Count ?? 0}");

            this.LinkedDeviceId = pidsMarker.LinkedDeviceId;
            this.LinkedDevice = pidsMarker.LinkedDevice;
            this.ShowFOV = pidsMarker.ShowFOV;
            this.FOVColor = pidsMarker.FOVColor;
            this.FOVOpacity = pidsMarker.FOVOpacity;

            // Detection 속성들 추가
            this.DetectionRange = pidsMarker.DetectionRange;
            this.DetectionAngle = pidsMarker.DetectionAngle;
            this.DetectionBearing = pidsMarker.DetectionBearing;
            this.BaseBearing = pidsMarker.BaseBearing;

            //System.Diagnostics.Debug.WriteLine($"  설정 후 Panel LinkedDevice: {this.LinkedDevice?.DeviceName ?? "null"}");
            //System.Diagnostics.Debug.WriteLine($"=== SetupSpecificPropertiesFromMarker 완료 ===");
        }

        protected override void UpdateSpecificProperties()
        {
            if (SelectedMarker is IPidsEditableMarker pidsMarker)
            {
                pidsMarker.LinkedDeviceId = this.LinkedDeviceId;
                pidsMarker.LinkedDevice = this.LinkedDevice;
                pidsMarker.ShowFOV = this.ShowFOV;
                pidsMarker.FOVColor = this.FOVColor;
                pidsMarker.FOVOpacity = this.FOVOpacity;

                // Detection 속성들 추가
                pidsMarker.DetectionRange = this.DetectionRange;
                pidsMarker.DetectionAngle = this.DetectionAngle;
                pidsMarker.DetectionBearing = this.DetectionBearing;
                pidsMarker.BaseBearing = this.BaseBearing;
            }
        }

        public override Type GetSupportedMarkerType()
        {
            return typeof(GMapPropertyPidsControl);
        }

        #endregion
        
        #region - Dependency Properties -

        /// <summary>
        /// 연결된 디바이스 ID
        /// </summary>
        public int LinkedDeviceId
        {
            get { return (int)GetValue(LinkedDeviceIdProperty); }
            set { SetValue(LinkedDeviceIdProperty, value); }
        }

        public static readonly DependencyProperty LinkedDeviceIdProperty =
            DependencyProperty.Register("LinkedDeviceId", typeof(int),
                typeof(GMapPropertyPidsControl),
                new PropertyMetadata(0, OnLinkedDeviceIdChanged));

        /// <summary>
        /// 연결된 디바이스 객체 (런타임 바인딩용)
        /// <para>ComboBox에서 선택된 디바이스를 바인딩합니다.</para>
        /// </summary>
        public IBaseDeviceModel? LinkedDevice
        {
            get { return (IBaseDeviceModel?)GetValue(LinkedDeviceProperty); }
            set { SetValue(LinkedDeviceProperty, value); }
        }

        public static readonly DependencyProperty LinkedDeviceProperty =
            DependencyProperty.Register("LinkedDevice", typeof(IBaseDeviceModel),
                typeof(GMapPropertyPidsControl),
                new PropertyMetadata(null, OnLinkedDeviceChanged));

        /// <summary>
        /// DeviceType에 따라 필터링된 디바이스 목록 (ComboBox ItemsSource)
        /// </summary>
        public ObservableCollection<IBaseDeviceModel> FilteredDeviceList
        {
            get { return (ObservableCollection<IBaseDeviceModel>)GetValue(FilteredDeviceListProperty); }
            set { SetValue(FilteredDeviceListProperty, value); }
        }

        public static readonly DependencyProperty FilteredDeviceListProperty =
            DependencyProperty.Register("FilteredDeviceList", typeof(ObservableCollection<IBaseDeviceModel>),
                typeof(GMapPropertyPidsControl),
                new PropertyMetadata(null));

        /// <summary>
        /// 탐지 범위 (미터)
        /// </summary>
        public double DetectionRange
        {
            get { return (double)GetValue(DetectionRangeProperty); }
            set { SetValue(DetectionRangeProperty, value); }
        }

        public static readonly DependencyProperty DetectionRangeProperty =
            DependencyProperty.Register("DetectionRange", typeof(double),
                typeof(GMapPropertyPidsControl),
                new PropertyMetadata(30.0, OnDetectionRangeChanged, CoerceDoubleValue));

        /// <summary>
        /// 탐지 각도 (도)
        /// </summary>
        public double DetectionAngle
        {
            get { return (double)GetValue(DetectionAngleProperty); }
            set { SetValue(DetectionAngleProperty, value); }
        }

        public static readonly DependencyProperty DetectionAngleProperty =
            DependencyProperty.Register("DetectionAngle", typeof(double),
                typeof(GMapPropertyPidsControl),
                new PropertyMetadata(90.0, OnDetectionAngleChanged, CoerceDoubleValue));

        /// <summary>
        /// 탐지 방향 (도)
        /// </summary>
        public double DetectionBearing
        {
            get { return (double)GetValue(DetectionBearingProperty); }
            set { SetValue(DetectionBearingProperty, value); }
        }

        public static readonly DependencyProperty DetectionBearingProperty =
            DependencyProperty.Register("DetectionBearing", typeof(double),
                typeof(GMapPropertyPidsControl),
                new PropertyMetadata(0.0, OnDetectionBearingChanged, CoerceDoubleValue));

        /// <summary>
        /// 기준 방향 각도 (카메라 물리적 설치 방향, 도)
        /// </summary>
        public double BaseBearing
        {
            get { return (double)GetValue(BaseBearingProperty); }
            set { SetValue(BaseBearingProperty, value); }
        }

        public static readonly DependencyProperty BaseBearingProperty =
            DependencyProperty.Register("BaseBearing", typeof(double),
                typeof(GMapPropertyPidsControl),
                new PropertyMetadata(0.0, OnBaseBearingChanged, CoerceDoubleValue));

        /// <summary>
        /// FOV 표시 여부
        /// </summary>
        public bool ShowFOV
        {
            get { return (bool)GetValue(ShowFOVProperty); }
            set { SetValue(ShowFOVProperty, value); }
        }

        public static readonly DependencyProperty ShowFOVProperty =
            DependencyProperty.Register("ShowFOV", typeof(bool),
                typeof(GMapPropertyPidsControl),
                new PropertyMetadata(false, OnShowFOVChanged));

        /// <summary>
        /// FOV 색상
        /// </summary>
        public EnumColorType FOVColor
        {
            get { return (EnumColorType)GetValue(FOVColorProperty); }
            set { SetValue(FOVColorProperty, value); }
        }

        public static readonly DependencyProperty FOVColorProperty =
            DependencyProperty.Register("FOVColor", typeof(EnumColorType),
                typeof(GMapPropertyPidsControl),
                new PropertyMetadata(EnumColorType.Purple, OnFOVColorChanged));

        /// <summary>
        /// FOV 투명도
        /// </summary>
        public double FOVOpacity
        {
            get { return (double)GetValue(FOVOpacityProperty); }
            set { SetValue(FOVOpacityProperty, value); }
        }

        public static readonly DependencyProperty FOVOpacityProperty =
            DependencyProperty.Register("FOVOpacity", typeof(double),
                typeof(GMapPropertyPidsControl),
                new PropertyMetadata(0.8, OnFOVOpacityChanged, CoerceDoubleValue));

        #endregion

        #region - Property Changed Methods -

        private static void OnLinkedDeviceIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnLinkedDeviceIdChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsControl control &&
                control.SelectedMarker is IPidsEditableMarker pidsMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                pidsMarker.LinkedDeviceId = (int)e.NewValue;
                // 주의: OnMarkerPropertyChanged 호출하지 않음
                // LinkedDevice 변경 시 LinkedDeviceId가 자동 동기화되므로,
                // LinkedDevice 변경 핸들러에서만 DB 업데이트를 수행합니다.
                // 이중 업데이트로 인한 MySQL 동시성 충돌 방지
            }
        }

        private static void OnLinkedDeviceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnLinkedDeviceChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsControl control &&
                control.SelectedMarker is IPidsEditableMarker pidsMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                pidsMarker.LinkedDevice = (IBaseDeviceModel?)e.NewValue;
                control.OnMarkerPropertyChanged("LinkedDevice", e.OldValue, e.NewValue);
            }
        }

       

        private static void OnDetectionRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnDetectionRangeChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsControl control &&
                control.SelectedMarker is IPidsEditableMarker pidsMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                // DetectionRange는 런타임 전용 (DB 저장 안 함)
                pidsMarker.DetectionRange = (double)e.NewValue;
                // OnMarkerPropertyChanged 호출 안 함 (DB UPDATE 트리거 방지)
            }
        }

        private static void OnDetectionAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnDetectionAngleChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsControl control &&
                control.SelectedMarker is IPidsEditableMarker pidsMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                // DetectionAngle은 런타임 전용 (DB 저장 안 함)
                pidsMarker.DetectionAngle = (double)e.NewValue;
                // OnMarkerPropertyChanged 호출 안 함 (DB UPDATE 트리거 방지)
            }
        }

        private static void OnDetectionBearingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnDetectionBearingChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsControl control &&
                control.SelectedMarker is IPidsEditableMarker pidsMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                // DetectionBearing은 런타임 전용 (DB 저장 안 함)
                pidsMarker.DetectionBearing = (double)e.NewValue;
                // OnMarkerPropertyChanged 호출 안 함 (DB UPDATE 트리거 방지)
            }
        }

        private static void OnBaseBearingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnBaseBearingChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsControl control &&
                control.SelectedMarker is IPidsEditableMarker pidsMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                pidsMarker.BaseBearing = (double)e.NewValue;
                control.OnMarkerPropertyChanged("BaseBearing", e.OldValue, e.NewValue);
            }
        }

        private static void OnShowFOVChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnShowFOVChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsControl control && 
                control.SelectedMarker is IPidsEditableMarker pidsMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                pidsMarker.ShowFOV = (bool)e.NewValue;
                control.OnMarkerPropertyChanged("ShowFOV", e.OldValue, e.NewValue);
            }
        }

        private static void OnFOVColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnFOVColorChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsControl control && 
                control.SelectedMarker is IPidsEditableMarker pidsMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                pidsMarker.FOVColor = (EnumColorType)e.NewValue;
                control.OnMarkerPropertyChanged("FOVColor", e.OldValue, e.NewValue);
            }
        }

        private static void OnFOVOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnFOVOpacityChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsControl control && 
                control.SelectedMarker is IPidsEditableMarker pidsMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                pidsMarker.FOVOpacity = (double)e.NewValue;
                control.OnMarkerPropertyChanged("FOVOpacity", e.OldValue, e.NewValue);
            }
        }

        #endregion
    }
}