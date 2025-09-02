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
    public abstract class GMapPropertyBaseControl : Control
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
                new PropertyMetadata(null, OnSelectedMarkerChanged));

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
        /// 특정 컨텐츠
        /// </summary>
        public object SpecificContent
        {
            get { return (object)GetValue(SpecificContentProperty); }
            set { SetValue(SpecificContentProperty, value); }
        }

        // 특화 컨텐츠 DependencyProperty
        public static readonly DependencyProperty SpecificContentProperty =
            DependencyProperty.Register("SpecificContent", typeof(object),
                typeof(GMapPropertyBaseControl));

        public bool HasSpecificProperties
        {
            get { return (bool)GetValue(HasSpecificPropertiesProperty); }
            set { SetValue(HasSpecificPropertiesProperty, value); }
        }

        public static readonly DependencyProperty HasSpecificPropertiesProperty =
        DependencyProperty.Register("HasSpecificProperties", typeof(bool),
            typeof(GMapPropertyBaseControl), new PropertyMetadata(false));

        public string SpecificPropertiesTitle
        {
            get { return (string)GetValue(SpecificPropertiesTitleProperty); }
            set { SetValue(SpecificPropertiesTitleProperty, value); }
        }

        public static readonly DependencyProperty SpecificPropertiesTitleProperty =
            DependencyProperty.Register("SpecificPropertiesTitle", typeof(string),
                typeof(GMapPropertyBaseControl), new PropertyMetadata("추가 속성"));

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

        #region Abstract Methods

        /// <summary>
        /// 마커 타입별 특화 UI 영역 생성
        /// </summary>
        //protected abstract FrameworkElement CreateSpecificPropertiesPanel();

        /// <summary>
        /// 특화 속성 바인딩 설정
        /// </summary>
        protected abstract void SetupSpecificBindings();

        /// <summary>
        /// 특화 속성 바인딩 정리
        /// </summary>
        protected abstract void ClearSpecificBindings();

        /// <summary>
        /// 마커로부터 특화 속성값 읽어오기
        /// </summary>
        protected abstract void SetupSpecificPropertiesFromMarker(IEditableMarker marker);

        /// <summary>
        /// 지원하는 마커 타입 확인
        /// </summary>
        public abstract Type GetSupportedMarkerType();

        /// <summary>
        /// 특화 속성 업데이트
        /// </summary>
        protected abstract void UpdateSpecificProperties();

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
        private static void OnSelectedMarkerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control)
            {
                var oldMarker = e.OldValue as IEditableMarker;
                var newMarker = e.NewValue as IEditableMarker;


                System.Diagnostics.Debug.WriteLine($"OldMarker: {oldMarker?.Title ?? "null"}");
                System.Diagnostics.Debug.WriteLine($"NewMarker: {newMarker?.Title ?? "null"}");
                System.Diagnostics.Debug.WriteLine($"Control Type: {control.GetType().Name}");

                //// 1단계: 바인딩 정리 전에 Source를 먼저 차단
                // 중요: ClearAllBindings 전에 플래그 설정!
                control._isClearingBindings = true;
                control._isInitializing = true;

                System.Diagnostics.Debug.WriteLine("_isInitializing = true 설정");


                // 2단계: 이제 안전하게 바인딩 정리
                control.ClearAllBindings();
                System.Diagnostics.Debug.WriteLine("ClearAllBindings 완료");


                // 3단계: 새 마커 설정
                if (newMarker != null)
                {
                    System.Diagnostics.Debug.WriteLine("새 마커 속성 설정 시작");
                    control.MarkerTitle = newMarker.Title;
                    control.MarkerWidth = newMarker.Width;
                    control.MarkerHeight = newMarker.Height;
                    control.MarkerBearing = newMarker.Bearing;
                    control.MarkerFillColor = newMarker.FillColor;
                    control.MarkerStrokeColor = newMarker.StrokeColor;
                    control.MarkerStrokeThickness = newMarker.StrokeThickness;
                    control.ShowShape = newMarker.ShowShape;
                    control.ShowTitle = newMarker.ShowTitle;

                    // *** 특화 속성 설정 추가 ***
                    System.Diagnostics.Debug.WriteLine("SetupSpecificPropertiesFromMarker 호출");
                    control.SetupSpecificPropertiesFromMarker(newMarker);
                }

                System.Diagnostics.Debug.WriteLine("SetupMarkerBindings 호출");
                control.SetupMarkerBindings();
                
                control._isInitializing = false; // 플래그 해제
                control._isClearingBindings = false;
                System.Diagnostics.Debug.WriteLine("_isInitializing = false 설정");
            }
            System.Diagnostics.Debug.WriteLine("=== OnSelectedMarkerChanged 완료 ===");
        }

        private static void OnMarkerTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnMarkerTitleChanged: '{e.OldValue}' -> '{e.NewValue}'");
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                System.Diagnostics.Debug.WriteLine($"  마커에 전파: SelectedMarker.Title = '{e.NewValue}'");
                control.SelectedMarker.Title = (string)e.NewValue;
                control.OnMarkerPropertyChanged("Title", e.OldValue, e.NewValue);
            }
        }

        private static void OnMarkerHeightSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnMarkerHeightSizeChanged: {e.OldValue} -> {e.NewValue}");
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                System.Diagnostics.Debug.WriteLine($"  마커에 전파: SelectedMarker.Height = {control.MarkerHeight}");
                control.SelectedMarker.Height = control.MarkerHeight;
                control.OnMarkerPropertyChanged("Size", null, new { Width = control.MarkerWidth, Height = control.MarkerHeight });
            }
        }

        private static void OnMarkerWidthSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnMarkerWidthSizeChanged: {e.OldValue} -> {e.NewValue}");

            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                System.Diagnostics.Debug.WriteLine($"  마커에 전파: SelectedMarker.Width = {control.MarkerWidth}");
                control.SelectedMarker.Width = control.MarkerWidth;
                control.OnMarkerPropertyChanged("Size", null, new { Width = control.MarkerWidth, Height = control.MarkerHeight });
            }
        }

        private static void OnMarkerBearingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                control.SelectedMarker.Bearing = (double)e.NewValue;
                control.OnMarkerPropertyChanged("Bearing", e.OldValue, e.NewValue);
            }
        }

        private static void OnMarkerFillColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                control.SelectedMarker.FillColor = (EnumColorType)e.NewValue;
                control.OnMarkerPropertyChanged("FillColor", e.OldValue, e.NewValue);
            }
        }

        private static void OnMarkerStrokeColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                control.SelectedMarker.StrokeColor = (EnumColorType)e.NewValue;
                control.OnMarkerPropertyChanged("StrokeColor", e.OldValue, e.NewValue);
            }
        }

        private static void OnMarkerStrokeThicknessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                control.SelectedMarker.StrokeThickness = (double)e.NewValue;
                control.OnMarkerPropertyChanged("StrokeThickness", e.OldValue, e.NewValue);
            }
        }

        private static void OnShowShapeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                control.SelectedMarker.ShowShape = (bool)e.NewValue;
                control.OnMarkerPropertyChanged("ShowShape", e.OldValue, e.NewValue);
            }
        }

        private static void OnShowTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
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
                SetupCommonBiding();

                // 특화 바인딩 (추상 메서드 호출)
                SetupSpecificBindings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetupMarkerBindings 실패: {ex.Message}");
            }
        }

        private void SetupCommonBiding()
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

            System.Diagnostics.Debug.WriteLine("SetupCommonBiding 완료");
        }

        protected Binding CreateTwoWayBinding(string propertyName)
        {
            return new Binding(propertyName)
            {
                Source = SelectedMarker,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
        }

        protected Binding CreateOneWayBinding(string propertyName, IValueConverter converter = null)
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
            System.Diagnostics.Debug.WriteLine($"[*바인딩 해제 전] MarkerTitle: '{MarkerTitle}'");
            System.Diagnostics.Debug.WriteLine($"[*바인딩 해제 전] MarkerWidth: {MarkerWidth}");
            System.Diagnostics.Debug.WriteLine($"[*바인딩 해제 전] MarkerHeight: {MarkerHeight}");
            System.Diagnostics.Debug.WriteLine($"[*바인딩 해제 전] MarkerBearing: {MarkerBearing}");
            System.Diagnostics.Debug.WriteLine($"[*바인딩 해제 전] MarkerFillColor: {MarkerFillColor}");
            System.Diagnostics.Debug.WriteLine($"[*바인딩 해제 전] MarkerStrokeColor: {MarkerStrokeColor}");
            System.Diagnostics.Debug.WriteLine($"[*바인딩 해제 전] MarkerStrokeThickness: {MarkerStrokeThickness}");
            System.Diagnostics.Debug.WriteLine($"[*바인딩 해제 전] ShowShape: {ShowShape}");
            System.Diagnostics.Debug.WriteLine($"[*바인딩 해제 전] ShowTitle: {ShowTitle}");

            // 임시로 PropertyChanged 콜백 차단
            _isClearingBindings = true;

            ClearCommonBidings();

            ClearSpecificBindings();

            _isClearingBindings = false;
            
            // 바인딩 해제 후 속성값들 출력
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 후*] MarkerTitle: '{MarkerTitle}'");
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 후*] MarkerWidth: {MarkerWidth}");
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 후*] MarkerHeight: {MarkerHeight}");
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 후*] MarkerBearing: {MarkerBearing}");
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 후*] MarkerFillColor: {MarkerFillColor}");
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 후*] MarkerStrokeColor: {MarkerStrokeColor}");
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 후*] MarkerStrokeThickness: {MarkerStrokeThickness}");
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 후*] ShowShape: {ShowShape}");
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 후*] ShowTitle: {ShowTitle}");

            System.Diagnostics.Debug.WriteLine("ClearAllBindings 완료");
        }

        private void ClearCommonBidings()
        {
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
        protected static object CoerceDoubleValue(DependencyObject d, object value)
        {
            // 입력값을 소수점 2자리로 반올림해서 반환
            return Math.Round((double)value, 2);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            //var container = GetTemplateChild("PART_SpecificPropertiesContainer") as Border;
            //var presenter = GetTemplateChild("PART_SpecificProperties") as ContentPresenter;

            //// SpecificContent는 XAML 스타일에서 설정됨
            //if (container != null && presenter != null && SpecificContent != null)
            //{
            //    presenter.Content = SpecificContent;
            //    container.Visibility = Visibility.Visible;
            //}

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
        protected bool _isInitializing;
        protected bool _isClearingBindings;

        #endregion
    }


}