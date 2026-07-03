using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using System;
using System.Security.Permissions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/2/2025 3:05:17 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class GMapPropertyGeometricControl : GMapPropertyBaseControl
    {
    #region - Ctors -
        static GMapPropertyGeometricControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapPropertyGeometricControl),
                new FrameworkPropertyMetadata(typeof(GMapPropertyGeometricControl)));
        }

        public GMapPropertyGeometricControl()
        {
        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -

        protected override void ClearSpecificBindings()
        {
            //System.Diagnostics.Debug.WriteLine("=== ClearSpecificBindings 시작 ===");
            BindingOperations.ClearBinding(this, MarkerOpacityProperty);
            //System.Diagnostics.Debug.WriteLine("=== ClearSpecificBindings 완료 ===");
        }

        protected override void SetupSpecificBindings()
        {
            //System.Diagnostics.Debug.WriteLine("=== SetupSpecificBindings 시작 ===");

            if (SelectedMarker is IGeoEditableMarker geoMarker)
            {
                //System.Diagnostics.Debug.WriteLine($"바인딩 대상: {geoMarker.GetType().Name}");

                var binding = CreateTwoWayBinding(nameof(geoMarker.Opacity));
                SetBinding(MarkerOpacityProperty, binding);
            }

            //System.Diagnostics.Debug.WriteLine("=== SetupSpecificBindings 완료 ===");

        }

        protected override void SetupSpecificPropertiesFromMarker(IEditableMarker marker)
        {
            if (!(marker is IGeoEditableMarker geoMarker)) return;

            //geoMarker.ShapeType = this.ShapeType;
            this.MarkerOpacity = geoMarker.Opacity;

            //System.Diagnostics.Debug.WriteLine($"마커에서 MarkerOpacity 로드: {geoMarker.Opacity}");

        }

        protected override void UpdateSpecificProperties()
        {
            if (SelectedMarker is IGeoEditableMarker geoMarker)
            {
                //geoMarker.ShapeType = this.ShapeType;
                geoMarker.Opacity = this.MarkerOpacity;
            }
        }

        public override Type GetSupportedMarkerType()
        {
            return typeof(GMapPropertyGeometricControl);
        }

        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        private static void OnMarkerOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

            //System.Diagnostics.Debug.WriteLine($"=== PropertyPanel.OnMarkerOpacityChanged ===");
            //System.Diagnostics.Debug.WriteLine($"값 변경: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyGeometricControl control)
            {
                //System.Diagnostics.Debug.WriteLine($"컨트롤 확인: {control.GetType().Name}");
                //System.Diagnostics.Debug.WriteLine($"_isInitializing: {control._isInitializing}");
                //System.Diagnostics.Debug.WriteLine($"_isClearingBindings: {control._isClearingBindings}");
                //System.Diagnostics.Debug.WriteLine($"SelectedMarker: {control.SelectedMarker?.Title ?? "null"}");

                if (control.SelectedMarker is IGeoEditableMarker geoMarker)
                {
                    //System.Diagnostics.Debug.WriteLine($"마커 Opacity (변경 전): {geoMarker.Opacity}");

                    if (!control._isInitializing && !control._isClearingBindings)
                    {
                        geoMarker.Opacity = (double)e.NewValue;
                        control.OnMarkerPropertyChanged("MarkerOpacity", e.OldValue, e.NewValue);
                        //System.Diagnostics.Debug.WriteLine($"마커 Opacity 업데이트 완료: {geoMarker.Opacity}");
                    }
                    else
                    {
                        //System.Diagnostics.Debug.WriteLine("플래그로 인해 마커 업데이트 생략");
                    }
                }
            }
        }

        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        public double MarkerOpacity
        {
            get { return (double)GetValue(MarkerOpacityProperty); }
            set { SetValue(MarkerOpacityProperty, value); }
        }

        public static readonly DependencyProperty MarkerOpacityProperty =
            DependencyProperty.Register("MarkerOpacity", typeof(double),
                typeof(GMapPropertyGeometricControl),
                new PropertyMetadata(0.0, OnMarkerOpacityChanged, CoerceDoubleValue));

        #endregion
        #region - Attributes -
        #endregion
    }
}