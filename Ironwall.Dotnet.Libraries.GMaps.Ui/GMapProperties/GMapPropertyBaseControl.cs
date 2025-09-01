using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 8/28/2025 8:22:17 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class GMapPropertyBaseControl : Control
    {
        #region Static Constructor
        static GMapPropertyBaseControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapPropertyBaseControl),
                new FrameworkPropertyMetadata(typeof(GMapPropertyBaseControl)));
        }
        #endregion

        #region Dependency Properties

        /// <summary>
        /// 편집할 마커
        /// </summary>
        public IEditableMarker SelectedMarker
        {
            get { return (IEditableMarker)GetValue(SelectedMarkerProperty); }
            set { SetValue(SelectedMarkerProperty, value); }
        }

        public static readonly DependencyProperty SelectedMarkerProperty =
            DependencyProperty.Register("SelectedMarker", typeof(IEditableMarker),
                typeof(GMapPropertyBaseControl),
                new PropertyMetadata(null, OnSelectedMarkerChanged, CoerceSelectedMarkerValue));

        /// <summary>
        /// 사용 가능한 색상 목록
        /// </summary>
        public EnumColorType[] AvailableColors
        {
            get { return (EnumColorType[])GetValue(AvailableColorsProperty); }
            set { SetValue(AvailableColorsProperty, value); }
        }

        public static readonly DependencyProperty AvailableColorsProperty =
            DependencyProperty.Register("AvailableColors", typeof(EnumColorType[]),
                typeof(GMapPropertyBaseControl),
                new PropertyMetadata(null));

        /// <summary>
        /// 사용 가능한 선 두께 목록
        /// </summary>
        public double[] AvailableSizes
        {
            get { return (double[])GetValue(AvailableSizesProperty); }
            set { SetValue(AvailableSizesProperty, value); }
        }

        public static readonly DependencyProperty AvailableSizesProperty =
            DependencyProperty.Register("AvailableSizes", typeof(double[]),
                typeof(GMapPropertyBaseControl),
                new PropertyMetadata(null));

        /// <summary>
        /// 마커 제목
        /// </summary>
        public string MarkerTitle
        {
            get { return (string)GetValue(MarkerTitleProperty); }
            set { SetValue(MarkerTitleProperty, value); }
        }

        public static readonly DependencyProperty MarkerTitleProperty =
            DependencyProperty.Register("MarkerTitle", typeof(string),
                typeof(GMapPropertyBaseControl),
                new PropertyMetadata("", OnMarkerTitleChanged));

        /// <summary>
        /// 마커 너비
        /// </summary>
        public double MarkerWidth
        {
            get { return (double)GetValue(MarkerWidthProperty); }
            set { SetValue(MarkerWidthProperty, value); }
        }

        public static readonly DependencyProperty MarkerWidthProperty =
            DependencyProperty.Register("MarkerWidth", typeof(double),
                typeof(GMapPropertyBaseControl),
                new PropertyMetadata(32.0, OnMarkerWidthSizeChanged, CoerceDoubleValue));

        /// <summary>
        /// 마커 높이
        /// </summary>
        public double MarkerHeight
        {
            get { return (double)GetValue(MarkerHeightProperty); }
            set { SetValue(MarkerHeightProperty, value); }
        }

        public static readonly DependencyProperty MarkerHeightProperty =
            DependencyProperty.Register("MarkerHeight", typeof(double),
                typeof(GMapPropertyBaseControl),
                new PropertyMetadata(32.0, OnMarkerHeightSizeChanged, CoerceDoubleValue));

        /// <summary>
        /// 마커 회전 각도
        /// </summary>
        public double MarkerBearing
        {
            get { return (double)GetValue(MarkerBearingProperty); }
            set { SetValue(MarkerBearingProperty, value); }
        }

        public static readonly DependencyProperty MarkerBearingProperty =
            DependencyProperty.Register("MarkerBearing", typeof(double),
                typeof(GMapPropertyBaseControl),
                new PropertyMetadata(0.0, OnMarkerBearingChanged, CoerceDoubleValue));

        /// <summary>
        /// 마커 채우기 색상
        /// </summary>
        public EnumColorType MarkerFillColor
        {
            get { return (EnumColorType)GetValue(MarkerFillColorProperty); }
            set { SetValue(MarkerFillColorProperty, value); }
        }

        public static readonly DependencyProperty MarkerFillColorProperty =
            DependencyProperty.Register("MarkerFillColor", typeof(EnumColorType),
                typeof(GMapPropertyBaseControl),
                new PropertyMetadata(EnumColorType.Blue, OnMarkerFillColorChanged));

        /// <summary>
        /// 마커 테두리 색상
        /// </summary>
        public EnumColorType MarkerStrokeColor
        {
            get { return (EnumColorType)GetValue(MarkerStrokeColorProperty); }
            set { SetValue(MarkerStrokeColorProperty, value); }
        }

        public static readonly DependencyProperty MarkerStrokeColorProperty =
            DependencyProperty.Register("MarkerStrokeColor", typeof(EnumColorType),
                typeof(GMapPropertyBaseControl),
                new PropertyMetadata(EnumColorType.White, OnMarkerStrokeColorChanged));

        /// <summary>
        /// 마커 테두리 두께
        /// </summary>
        public double MarkerStrokeThickness
        {
            get { return (double)GetValue(MarkerStrokeThicknessProperty); }
            set { SetValue(MarkerStrokeThicknessProperty, value); }
        }

        public static readonly DependencyProperty MarkerStrokeThicknessProperty =
            DependencyProperty.Register("MarkerStrokeThickness", typeof(double),
                typeof(GMapPropertyBaseControl),
                new PropertyMetadata(1.0, OnMarkerStrokeThicknessChanged));

        /// <summary>
        /// Shape 표시 여부
        /// </summary>
        public bool ShowShape
        {
            get { return (bool)GetValue(ShowShapeProperty); }
            set { SetValue(ShowShapeProperty, value); }
        }

        public static readonly DependencyProperty ShowShapeProperty =
            DependencyProperty.Register("ShowShape", typeof(bool),
                typeof(GMapPropertyBaseControl),
                new PropertyMetadata(true, OnShowShapeChanged));

        /// <summary>
        /// 제목 표시 여부
        /// </summary>
        public bool ShowTitle
        {
            get { return (bool)GetValue(ShowTitleProperty); }
            set { SetValue(ShowTitleProperty, value); }
        }

        public static readonly DependencyProperty ShowTitleProperty =
            DependencyProperty.Register("ShowTitle", typeof(bool),
                typeof(GMapPropertyBaseControl),
                new PropertyMetadata(false, OnShowTitleChanged));


        /// <summary>
        /// 드래그 가능 여부
        /// </summary>
        public bool IsDraggable
        {
            get { return (bool)GetValue(IsDraggableProperty); }
            set { SetValue(IsDraggableProperty, value); }
        }

        public static readonly DependencyProperty IsDraggableProperty =
            DependencyProperty.Register("IsDraggable", typeof(bool),
                typeof(GMapPropertyBaseControl),
                new PropertyMetadata(true));

        #endregion

        #region Events

        /// <summary>
        /// 마커 속성 변경 이벤트
        /// </summary>
        public event EventHandler<MarkerPropertyChangedEventArgs> MarkerPropertyChanged;


        /// <summary>
        /// 닫기 버튼 클릭 이벤트
        /// </summary>
        public event EventHandler CloseRequested;
        #endregion

        #region Constructor

        public GMapPropertyBaseControl()
        {
            // 기본값 설정
            AvailableColors = new EnumColorType[]
            {
                EnumColorType.Red, EnumColorType.Blue, EnumColorType.Green,
                EnumColorType.Yellow, EnumColorType.Orange, EnumColorType.Purple,
                EnumColorType.White, EnumColorType.Black, EnumColorType.Gray
            };

            AvailableSizes = new double[] { 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 4.0, 5.0 };
            
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseLeftButtonUp;

        }
        #endregion

        #region Event Handlers
        #endregion
        #region PropertyWindow Control Method

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsDraggable) return;

            // 헤더 영역에서만 드래그 허용 (Y < 45)
            var position = e.GetPosition(this);
            if (position.Y > 45) return;

            _isDragging = true;
            _lastMousePosition = e.GetPosition(Parent as UIElement);
            CaptureMouse();
            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || !IsDraggable) return;

            var currentPosition = e.GetPosition(Parent as UIElement);
            var deltaX = currentPosition.X - _lastMousePosition.X;
            var deltaY = currentPosition.Y - _lastMousePosition.Y;

            // Canvas 또는 Grid에서 위치 업데이트
            if (Parent is Canvas canvas)
            {
                var currentLeft = Canvas.GetLeft(this);
                var currentTop = Canvas.GetTop(this);

                // NaN 체크
                if (double.IsNaN(currentLeft)) currentLeft = 0;
                if (double.IsNaN(currentTop)) currentTop = 0;

                Canvas.SetLeft(this, currentLeft + deltaX);
                Canvas.SetTop(this, currentTop + deltaY);
            }
            else if (Parent is Grid grid)
            {
                // Grid의 Margin을 이용한 위치 조정
                var currentMargin = Margin;
                Margin = new Thickness(
                    currentMargin.Left + deltaX,
                    currentMargin.Top + deltaY,
                    currentMargin.Right,
                    currentMargin.Bottom);
            }

            _lastMousePosition = currentPosition;
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging) return;

            _isDragging = false;
            ReleaseMouseCapture();
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        #endregion
        #region Property Changed Callbacks
        // CoerceValueCallback이 PropertyChangedCallback보다 먼저 실행됨
        private static object CoerceSelectedMarkerValue(DependencyObject d, object value)
        {
            if (d is GMapPropertyBaseControl control)
            {
                var newMarker = value as IEditableMarker;
                var oldMarker = control.SelectedMarker;

                System.Diagnostics.Debug.WriteLine("CoerceValue: 바인딩 정리 실행");

                // 새 값이 설정되기 전에 바인딩 정리
                if (oldMarker != null && newMarker != null && oldMarker != newMarker)
                {
                    control.ClearAllBindings();
                }
            }

            return value; // 원래 값 그대로 반환
        }


        private static void OnSelectedMarkerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control)
            {
                // 핵심 수정: e.NewValue에서 직접 원본 값 추출
                if (!(e.NewValue is IEditableMarker newMarker)) return;

                // 바인딩 설정 전에 DP 값을 마커 값으로 초기화
                if (control.SelectedMarker != null)
                {
                    control._isInitializing = true; // 플래그 설정

                    // 저장된 원본 값으로 설정 (변경되지 않은 값들)
                    control.MarkerTitle = newMarker.Title;
                    control.MarkerWidth = newMarker.Width;
                    control.MarkerHeight = newMarker.Height;
                    control.MarkerBearing = newMarker.Bearing;
                    control.MarkerFillColor = newMarker.FillColor;
                    control.MarkerStrokeColor = newMarker.StrokeColor;
                    control.MarkerStrokeThickness = newMarker.StrokeThickness;
                    control.ShowShape = newMarker.ShowShape;
                    control.ShowTitle = newMarker.ShowTitle;

                    control._isInitializing = false; // 플래그 해제
                }

                control.SetupMarkerBindings();
            }
        }

        private static void OnMarkerTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing)
            {
                control.SelectedMarker.Title = (string)e.NewValue;
                control.OnMarkerPropertyChanged("Title", e.OldValue, e.NewValue);
            }
        }

        private static void OnMarkerHeightSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing)
            {
                control.SelectedMarker.Height = control.MarkerHeight;
                control.OnMarkerPropertyChanged("Size", null, new { Width = control.MarkerWidth, Height = control.MarkerHeight });
            }
        }

        private static void OnMarkerWidthSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing)
            {
                control.SelectedMarker.Width = control.MarkerWidth;
                control.OnMarkerPropertyChanged("Size", null, new { Width = control.MarkerWidth, Height = control.MarkerHeight });
            }
        }

        private static void OnMarkerBearingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing)
            {
                control.SelectedMarker.Bearing = (double)e.NewValue;
                control.OnMarkerPropertyChanged("Bearing", e.OldValue, e.NewValue);
            }
        }

        private static void OnMarkerFillColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing)
            {
                control.SelectedMarker.FillColor = (EnumColorType)e.NewValue;
                control.OnMarkerPropertyChanged("FillColor", e.OldValue, e.NewValue);
            }
        }

        private static void OnMarkerStrokeColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing)
            {
                control.SelectedMarker.StrokeColor = (EnumColorType)e.NewValue;
                control.OnMarkerPropertyChanged("StrokeColor", e.OldValue, e.NewValue);
            }
        }

        private static void OnMarkerStrokeThicknessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing)
            {
                control.SelectedMarker.StrokeThickness = (double)e.NewValue;
                control.OnMarkerPropertyChanged("StrokeThickness", e.OldValue, e.NewValue);
            }
        }

        private static void OnShowShapeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing)
            {
                control.SelectedMarker.ShowShape = (bool)e.NewValue;
                control.OnMarkerPropertyChanged("ShowShape", e.OldValue, e.NewValue);
            }
        }

        private static void OnShowTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing)
            {
                control.SelectedMarker.ShowTitle = (bool)e.NewValue;
                control.OnMarkerPropertyChanged("ShowTitle", e.OldValue, e.NewValue);
            }
        }

        #endregion

        #region Binding Setup Methods

        private void SetupMarkerBindings()
        {
            if (SelectedMarker == null) return;

            System.Diagnostics.Debug.WriteLine($"SetupMarkerBindings 시작 - 마커: {SelectedMarker.Title}");

            try
            {
                // 기본 속성 바인딩
                SetBinding(MarkerTitleProperty, CreateTwoWayBinding(nameof(SelectedMarker.Title)));
                SetBinding(MarkerWidthProperty, CreateTwoWayBinding(nameof(SelectedMarker.Width)));
                SetBinding(MarkerHeightProperty, CreateTwoWayBinding(nameof(SelectedMarker.Height)));
                SetBinding(MarkerBearingProperty, CreateTwoWayBinding(nameof(SelectedMarker.Bearing)));
                SetBinding(MarkerStrokeThicknessProperty, CreateTwoWayBinding(nameof(SelectedMarker.StrokeThickness)));
                SetBinding(ShowShapeProperty, CreateTwoWayBinding(nameof(SelectedMarker.ShowShape)));
                SetBinding(ShowTitleProperty, CreateTwoWayBinding(nameof(SelectedMarker.ShowTitle)));

                // 색상 속성은 EnumColorType이므로 직접 바인딩
                SetBinding(MarkerFillColorProperty, CreateTwoWayBinding(nameof(SelectedMarker.FillColor)));
                SetBinding(MarkerStrokeColorProperty, CreateTwoWayBinding(nameof(SelectedMarker.StrokeColor)));

                System.Diagnostics.Debug.WriteLine("SetupMarkerBindings 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetupMarkerBindings 실패: {ex.Message}");
            }
        }

        private Binding CreateTwoWayBinding(string propertyName)
        {
            return new Binding(propertyName)
            {
                Source = SelectedMarker,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
        }

        private Binding CreateOneWayBinding(string propertyName, IValueConverter converter = null)
        {
            var binding = new Binding(propertyName)
            {
                Source = SelectedMarker,
                Mode = BindingMode.OneWay
            };

            if (converter != null)
                binding.Converter = converter;

            return binding;
        }

        public void ClearAllBindings()
        {
            System.Diagnostics.Debug.WriteLine("ClearAllBindings 시작");

            // 바인딩 해제 전 속성값들 출력
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 전] MarkerTitle: '{MarkerTitle}'");
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 전] MarkerWidth: {MarkerWidth}");
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 전] MarkerHeight: {MarkerHeight}");
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 전] MarkerBearing: {MarkerBearing}");
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 전] MarkerFillColor: {MarkerFillColor}");
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 전] MarkerStrokeColor: {MarkerStrokeColor}");
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 전] MarkerStrokeThickness: {MarkerStrokeThickness}");
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 전] ShowShape: {ShowShape}");
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 전] ShowTitle: {ShowTitle}");

            // 바인딩 해제
            BindingOperations.ClearBinding(this, MarkerTitleProperty);
            BindingOperations.ClearBinding(this, MarkerWidthProperty);
            BindingOperations.ClearBinding(this, MarkerHeightProperty);
            BindingOperations.ClearBinding(this, MarkerBearingProperty);
            BindingOperations.ClearBinding(this, MarkerFillColorProperty);
            BindingOperations.ClearBinding(this, MarkerStrokeColorProperty);
            BindingOperations.ClearBinding(this, MarkerStrokeThicknessProperty);
            BindingOperations.ClearBinding(this, ShowShapeProperty);
            BindingOperations.ClearBinding(this, ShowTitleProperty);

            // 바인딩 해제 후 속성값들 출력
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 후] MarkerTitle: '{MarkerTitle}'");
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 후] MarkerWidth: {MarkerWidth}");
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 후] MarkerHeight: {MarkerHeight}");
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 후] MarkerBearing: {MarkerBearing}");
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 후] MarkerFillColor: {MarkerFillColor}");
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 후] MarkerStrokeColor: {MarkerStrokeColor}");
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 후] MarkerStrokeThickness: {MarkerStrokeThickness}");
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 후] ShowShape: {ShowShape}");
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 후] ShowTitle: {ShowTitle}");

            System.Diagnostics.Debug.WriteLine("ClearAllBindings 완료");
        }

        #endregion

        #region Protected Methods
        /// <summary>
        /// 마커 속성 변경 이벤트 발생
        /// </summary>
        protected virtual void OnMarkerPropertyChanged(string propertyName, object oldValue, object newValue)
        {
            MarkerPropertyChanged?.Invoke(this, new MarkerPropertyChangedEventArgs
            {
                PropertyName = propertyName,
                OldValue = oldValue,
                NewValue = newValue,
                Marker = SelectedMarker
            });
        }
        // CoerceValueCallback 정의
        private static object CoerceDoubleValue(DependencyObject d, object value)
        {
            // 입력값을 소수점 2자리로 반올림해서 반환
            return Math.Round((double)value, 2);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // 닫기 버튼 이벤트 연결
            if (GetTemplateChild("PART_CloseButton") is Button closeButton)
            {
                closeButton.Click += OnCloseButtonClick;
                System.Diagnostics.Debug.WriteLine("닫기 버튼 이벤트 연결 완료");

            }

            // 헤더 커서 설정
            if (GetTemplateChild("PART_HeaderArea") is FrameworkElement headerArea)
            {
                headerArea.Cursor = IsDraggable ? Cursors.SizeAll : Cursors.Arrow;
            }
        }
        #endregion
        #region Fields
        private bool _isDragging;
        private Point _lastMousePosition;
        private bool _isInitializing;
        #endregion
    }


}