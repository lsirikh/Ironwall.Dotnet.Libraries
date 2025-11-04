using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using System;
using System.Windows.Data;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/19/2025 10:17:42 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class GMapPropertyLineControl : GMapPropertyBaseControl
    {
        #region - Ctors -
        static GMapPropertyLineControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapPropertyLineControl),
               new FrameworkPropertyMetadata(typeof(GMapPropertyLineControl)));
        }

        public GMapPropertyLineControl()
        {
            MarkerSizeEnabled = false;
        }
        #endregion

        #region - Implementation of Interface -

        protected override void ClearSpecificBindings()
        {
            System.Diagnostics.Debug.WriteLine("=== LineControl ClearSpecificBindings 시작 ===");

            BindingOperations.ClearBinding(this, LinePatternProperty);
            BindingOperations.ClearBinding(this, LineOpacityProperty);
            BindingOperations.ClearBinding(this, IsClosedPathProperty);
            BindingOperations.ClearBinding(this, TotalDistanceProperty);
            BindingOperations.ClearBinding(this, PointCountProperty);

            System.Diagnostics.Debug.WriteLine("=== LineControl ClearSpecificBindings 완료 ===");
        }

        protected override void SetupSpecificBindings()
        {
            System.Diagnostics.Debug.WriteLine("=== LineControl SetupSpecificBindings 시작 ===");

            if (SelectedMarker is ILineEditableMarker lineMarker)
            {
                System.Diagnostics.Debug.WriteLine($"바인딩 대상: {lineMarker.GetType().Name}");

                // 라인 스타일 속성 바인딩
                var linePatternBinding = CreateTwoWayBinding(nameof(lineMarker.LinePattern));
                SetBinding(LinePatternProperty, linePatternBinding);

                var lineOpacityBinding = CreateTwoWayBinding(nameof(lineMarker.LineOpacity));
                SetBinding(LineOpacityProperty, lineOpacityBinding);

                // 라인 옵션 바인딩
                var isClosedPathBinding = CreateTwoWayBinding(nameof(lineMarker.IsClosedPath));
                SetBinding(IsClosedPathProperty, isClosedPathBinding);

             

                // 읽기 전용 속성 바인딩 (OneWay)
                var totalDistanceBinding = new Binding(nameof(lineMarker.TotalDistance))
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

            System.Diagnostics.Debug.WriteLine("=== LineControl SetupSpecificBindings 완료 ===");
        }

        protected override void SetupSpecificPropertiesFromMarker(IEditableMarker marker)
        {
            if (!(marker is ILineEditableMarker lineMarker)) return;

            this.LinePattern = lineMarker.LinePattern;
            this.LineOpacity = lineMarker.LineOpacity;
            this.IsClosedPath = lineMarker.IsClosedPath;
            this.TotalDistance = lineMarker.TotalDistance;
            this.PointCount = lineMarker.RuntimePoints?.Count ?? 0;

            System.Diagnostics.Debug.WriteLine($"라인 마커에서 속성 로드 완료");
        }

        protected override void UpdateSpecificProperties()
        {
            if (SelectedMarker is ILineEditableMarker lineMarker)
            {
                lineMarker.LinePattern = this.LinePattern;
                lineMarker.LineOpacity = this.LineOpacity;
                lineMarker.IsClosedPath = this.IsClosedPath;
            }
        }

        public override Type GetSupportedMarkerType()
        {
            return typeof(GMapPropertyLineControl);
        }

        #endregion

        #region - Dependency Properties -

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
                typeof(GMapPropertyLineControl),
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
                typeof(GMapPropertyLineControl),
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
                typeof(GMapPropertyLineControl),
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
                typeof(GMapPropertyLineControl),
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
                typeof(GMapPropertyLineControl),
                new PropertyMetadata(0));

        #endregion

        #region - Property Changed Methods -

        private static void OnLinePatternChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnLinePatternChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyLineControl control &&
                control.SelectedMarker is ILineEditableMarker lineMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                lineMarker.LinePattern = (EnumLinePattern)e.NewValue;
                control.OnMarkerPropertyChanged("LinePattern", e.OldValue, e.NewValue);
            }
        }

        private static void OnLineOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnLineOpacityChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyLineControl control &&
                control.SelectedMarker is ILineEditableMarker lineMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                lineMarker.LineOpacity = (double)e.NewValue;
                control.OnMarkerPropertyChanged("LineOpacity", e.OldValue, e.NewValue);
            }
        }

        private static void OnIsClosedPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnIsClosedPathChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyLineControl control &&
                control.SelectedMarker is ILineEditableMarker lineMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                lineMarker.IsClosedPath = (bool)e.NewValue;
                control.OnMarkerPropertyChanged("IsClosedPath", e.OldValue, e.NewValue);
            }
        }
       
        #endregion
    }
}