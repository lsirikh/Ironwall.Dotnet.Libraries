using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
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
        /// 제목 사이즈
        /// </summary>
        public double TitleSize
        {
            get { return (double)GetValue(TitleSizeProperty); }
            set { SetValue(TitleSizeProperty, value); }
        }

        public static readonly DependencyProperty TitleSizeProperty =
            DependencyProperty.Register("TitleSize", typeof(double), typeof(GMapPropertyBaseControl),
                new PropertyMetadata(10.0, OnTitleSizeChanged));


        /// <summary>
        /// MarkerSize 활성화
        /// </summary>
        public bool MarkerSizeEnabled
        {
            get { return (bool)GetValue(MarkerSizeEnabledProperty); }
            set { SetValue(MarkerSizeEnabledProperty, value); }
        }

        public static readonly DependencyProperty MarkerSizeEnabledProperty =
            DependencyProperty.Register("MarkerSizeEnabled", typeof(bool),
                typeof(GMapPropertyBaseControl),
                new PropertyMetadata(true)); // 기본값은 true (활성화)

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

        /// <summary>
        /// Z-order 순위 표시 문자열 ("3 / 15") — MapViewModel이 set
        /// </summary>
        public string MarkerZOrderDisplay
        {
            get => (string)GetValue(MarkerZOrderDisplayProperty);
            set => SetValue(MarkerZOrderDisplayProperty, value);
        }

        public static readonly DependencyProperty MarkerZOrderDisplayProperty =
            DependencyProperty.Register(nameof(MarkerZOrderDisplay), typeof(string),
                typeof(GMapPropertyBaseControl), new PropertyMetadata("- / -"));

        /// <summary>
        /// 편집 모드 여부 — Z-order 버튼 IsEnabled 게이트
        /// </summary>
        public bool IsEditModeEnabled
        {
            get => (bool)GetValue(IsEditModeEnabledProperty);
            set => SetValue(IsEditModeEnabledProperty, value);
        }

        public static readonly DependencyProperty IsEditModeEnabledProperty =
            DependencyProperty.Register(nameof(IsEditModeEnabled), typeof(bool),
                typeof(GMapPropertyBaseControl), new PropertyMetadata(false));

        /// <summary>
        /// 마커 최소 표시 줌 레벨 (0 = 모든 줌에서 표시)
        /// </summary>
        public double MarkerZoom
        {
            get => (double)GetValue(MarkerZoomProperty);
            set => SetValue(MarkerZoomProperty, value);
        }

        public static readonly DependencyProperty MarkerZoomProperty =
            DependencyProperty.Register(nameof(MarkerZoom), typeof(double),
                typeof(GMapPropertyBaseControl), new PropertyMetadata(0.0, OnMarkerZoomChanged));

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
        public event EventHandler<MarkerPropertyChangedEventArgs>? MarkerPropertyChanged;


        /// <summary>
        /// 닫기 버튼 클릭 이벤트
        /// </summary>
        public event EventHandler? CloseRequested;

        /// <summary>
        /// Z-order 버튼(<</>>/< />) 클릭 이벤트 — PropertyPanelEventBehavior → EventAggregator → MapViewModel 체인
        /// </summary>
        public event EventHandler<ZOrderChangeRequestedEventArgs>? ZOrderChangeRequested;

        /// <summary>
        /// "현재위치 적용" 클릭 — 심볼 현재 위치를 연결 디바이스 Model/API에 반영 요청(MapViewModel 처리).
        /// </summary>
        public event EventHandler? DeviceLocationApplyRequested;
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

            InitializeDragSupport();

            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseLeftButtonUp;

            // "현재위치 적용" 버튼은 PIDS SpecificContent(ContentPresenter 별도 namescope)에 있어
            // GetTemplateChild로 못 찾는다 → 버블링되는 ButtonBase.Click을 컨트롤 레벨에서 잡아 이름으로 식별.
            AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler(OnAnyButtonClick));
        }
        #endregion

        #region Event Handlers
        #endregion

        #region PropertyWindow Control Method
        private void InitializeDragSupport()
        {
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"=== MouseLeftButtonDown 이벤트 ===");
            //System.Diagnostics.Debug.WriteLine($"IsDraggable: {IsDraggable}");

            if (!IsDraggable) return;

            var position = e.GetPosition(this);
            //System.Diagnostics.Debug.WriteLine($"마우스 위치: ({position.X}, {position.Y})");

            if (position.Y > 45)
            {
                //System.Diagnostics.Debug.WriteLine("헤더 영역 밖 클릭");
                return;
            }

            // ContentPresenter와 Canvas 찾기
            var contentPresenter = FindParentOfType<ContentPresenter>(this);
            var canvas = contentPresenter?.Parent as Canvas;

            if (canvas == null)
            {
                //System.Diagnostics.Debug.WriteLine("Canvas를 찾을 수 없어서 드래그 시작 불가");
                return;
            }

            _isDragging = true;
            _lastMousePosition = e.GetPosition(canvas);
            //System.Diagnostics.Debug.WriteLine($"드래그 시작: ({_lastMousePosition.X}, {_lastMousePosition.Y})");

            CaptureMouse();
            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || !IsDraggable) return;

            //System.Diagnostics.Debug.WriteLine("=== MouseMove 드래그 중 ===");
            //System.Diagnostics.Debug.WriteLine($"this.Parent: {this.Parent?.GetType().Name}");

            // Visual Tree를 따라 올라가면서 ContentPresenter 찾기
            var contentPresenter = FindParentOfType<ContentPresenter>(this);
            if (contentPresenter == null)
            {
                //System.Diagnostics.Debug.WriteLine("ContentPresenter를 찾을 수 없음");
                return;
            }

            //System.Diagnostics.Debug.WriteLine($"ContentPresenter 발견: {contentPresenter.GetType().Name}");

            // Canvas 찾기
            var canvas = contentPresenter.Parent as Canvas;
            if (canvas == null)
            {
                //System.Diagnostics.Debug.WriteLine($"Canvas를 찾을 수 없음. ContentPresenter.Parent: {contentPresenter.Parent?.GetType().Name}");
                return;
            }

            //System.Diagnostics.Debug.WriteLine($"Canvas 발견: {canvas.GetType().Name}");

            var currentPosition = e.GetPosition(canvas);
            var deltaX = currentPosition.X - _lastMousePosition.X;
            var deltaY = currentPosition.Y - _lastMousePosition.Y;

            //System.Diagnostics.Debug.WriteLine($"이동량: ({deltaX}, {deltaY})");

            // ContentPresenter의 Canvas 위치 업데이트
            var currentLeft = Canvas.GetLeft(contentPresenter);
            var currentTop = Canvas.GetTop(contentPresenter);

            if (double.IsNaN(currentLeft)) currentLeft = 0;
            if (double.IsNaN(currentTop)) currentTop = 0;

            var newLeft = currentLeft + deltaX;
            var newTop = currentTop + deltaY;

            Canvas.SetLeft(contentPresenter, newLeft);
            Canvas.SetTop(contentPresenter, newTop);

            //System.Diagnostics.Debug.WriteLine($"ContentPresenter 새 위치: ({newLeft}, {newTop})");

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

        // 헬퍼 메서드 추가
        private T? FindParentOfType<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);

            while (parent != null && !(parent is T))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }

            return parent as T;
        }
        #endregion

        #region Property Changed Callbacks
        private static void OnSelectedMarkerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control)
            {
                var oldMarker = e.OldValue as IEditableMarker;
                var newMarker = e.NewValue as IEditableMarker;


                //System.Diagnostics.Debug.WriteLine($"OldMarker: {oldMarker?.Title ?? "null"}, NewMarker: {newMarker?.Title ?? "null"}, Control Type: {control.GetType().Name}");

                //// 1단계: 바인딩 정리 전에 Source를 먼저 차단
                // 중요: ClearAllBindings 전에 플래그 설정!
                control._isClearingBindings = true;
                control._isInitializing = true;

                //System.Diagnostics.Debug.WriteLine("_isInitializing = true 설정");


                // 2단계: 이제 안전하게 바인딩 정리
                control.ClearAllBindings();
                //System.Diagnostics.Debug.WriteLine("ClearAllBindings 완료");


                // 3단계: 새 마커 설정
                if (newMarker != null)
                {
                    //System.Diagnostics.Debug.WriteLine("새 마커 속성 설정 시작");
                    control.MarkerTitle = newMarker.Title;
                    control.TitleSize = newMarker.TitleSize;
                    control.MarkerWidth = newMarker.Width;
                    control.MarkerHeight = newMarker.Height;
                    control.MarkerBearing = newMarker.Bearing;
                    control.MarkerFillColor = newMarker.FillColor;
                    control.MarkerStrokeColor = newMarker.StrokeColor;
                    control.MarkerStrokeThickness = newMarker.StrokeThickness;
                    control.ShowShape = newMarker.ShowShape;
                    control.ShowTitle = newMarker.ShowTitle;
                    control.MarkerZoom = newMarker.Zoom;

                    // *** 특화 속성 설정 추가 ***
                    //System.Diagnostics.Debug.WriteLine("SetupSpecificPropertiesFromMarker 호출");
                    control.SetupSpecificPropertiesFromMarker(newMarker);
                }
                else
                {
                    control.MarkerZOrderDisplay = "- / -";
                }

                //System.Diagnostics.Debug.WriteLine("SetupMarkerBindings 호출");
                control.SetupMarkerBindings();

                control._isInitializing = false; // 플래그 해제
                control._isClearingBindings = false;
                //System.Diagnostics.Debug.WriteLine("_isInitializing = false 설정");
            }
            //System.Diagnostics.Debug.WriteLine("=== OnSelectedMarkerChanged 완료 ===");
        }

        private static void OnMarkerTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnMarkerTitleChanged: '{e.OldValue}' -> '{e.NewValue}'");
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                //System.Diagnostics.Debug.WriteLine($"  마커에 전파: SelectedMarker.Title = '{e.NewValue}'");
                if (!control.IsGroupMode) control.SelectedMarker.Title = (string)e.NewValue;   // 그룹=VM이 전원 일괄 적용
                control.OnMarkerPropertyChanged("Title", e.OldValue, e.NewValue);
            }
        }

        private static void OnTitleSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnTitleSizeChanged: '{e.OldValue}' -> '{e.NewValue}'");
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                //System.Diagnostics.Debug.WriteLine($"  마커에 전파: SelectedMarker.TitleSize = '{e.NewValue}'");
                if (!control.IsGroupMode) control.SelectedMarker.TitleSize = (double)e.NewValue;
                control.OnMarkerPropertyChanged("TitleSize", e.OldValue, e.NewValue);
            }
        }

        private static void OnMarkerHeightSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnMarkerHeightSizeChanged: {e.OldValue} -> {e.NewValue}");
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                //System.Diagnostics.Debug.WriteLine($"  마커에 전파: SelectedMarker.Height = {control.MarkerHeight}");
                if (!control.IsGroupMode) control.SelectedMarker.Height = control.MarkerHeight;
                // 실제 "Height" 발화(과거 합성 "Size"+null before는 ApplyProperty 미지원→undo 무효, CMD-01)
                control.OnMarkerPropertyChanged("Height", e.OldValue, e.NewValue);
            }
        }

        private static void OnMarkerWidthSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnMarkerWidthSizeChanged: {e.OldValue} -> {e.NewValue}");

            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                //System.Diagnostics.Debug.WriteLine($"  마커에 전파: SelectedMarker.Width = {control.MarkerWidth}");
                if (!control.IsGroupMode) control.SelectedMarker.Width = control.MarkerWidth;
                // 실제 "Width" 발화(과거 합성 "Size"+null before는 ApplyProperty 미지원→undo 무효, CMD-01)
                control.OnMarkerPropertyChanged("Width", e.OldValue, e.NewValue);
            }
        }

        private static void OnMarkerBearingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnMarkerBearingChanged: {e.OldValue} -> {e.NewValue}");

            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                if (!control.IsGroupMode) control.SelectedMarker.Bearing = (double)e.NewValue;
                control.OnMarkerPropertyChanged("Bearing", e.OldValue, e.NewValue);
            }
        }

        private static void OnMarkerFillColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                if (!control.IsGroupMode) control.SelectedMarker.FillColor = (EnumColorType)e.NewValue;
                control.OnMarkerPropertyChanged("FillColor", e.OldValue, e.NewValue);
            }
        }

        private static void OnMarkerStrokeColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                if (!control.IsGroupMode) control.SelectedMarker.StrokeColor = (EnumColorType)e.NewValue;
                control.OnMarkerPropertyChanged("StrokeColor", e.OldValue, e.NewValue);
            }
        }

        private static void OnMarkerStrokeThicknessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                if (!control.IsGroupMode) control.SelectedMarker.StrokeThickness = (double)e.NewValue;
                control.OnMarkerPropertyChanged("StrokeThickness", e.OldValue, e.NewValue);
            }
        }

        private static void OnShowShapeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                if (!control.IsGroupMode) control.SelectedMarker.ShowShape = (bool)e.NewValue;
                control.OnMarkerPropertyChanged("ShowShape", e.OldValue, e.NewValue);
            }
        }

        private static void OnShowTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                if (!control.IsGroupMode) control.SelectedMarker.ShowTitle = (bool)e.NewValue;
                control.OnMarkerPropertyChanged("ShowTitle", e.OldValue, e.NewValue);
            }
        }

        private static void OnMarkerZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                if (!control.IsGroupMode) control.SelectedMarker.Zoom = (double)e.NewValue;
                control.OnMarkerPropertyChanged("Zoom", e.OldValue, e.NewValue);
            }
        }

        #endregion

        #region Binding Setup Methods

        private void SetupMarkerBindings()
        {
            if (SelectedMarker == null) return;

            //System.Diagnostics.Debug.WriteLine($"SetupMarkerBindings 시작 - 마커: {SelectedMarker.Title}");

            try
            {
                SetupCommonBiding();

                // 특화 바인딩 (추상 메서드 호출)
                SetupSpecificBindings();
            }
            catch (Exception ex)
            {
                //System.Diagnostics.Debug.WriteLine($"SetupMarkerBindings 실패: {ex.Message}");
            }
        }

        private void SetupCommonBiding()
        {
            // 기본 속성 바인딩
            SetBinding(MarkerTitleProperty, CreateTwoWayBinding(nameof(SelectedMarker.Title)));
            SetBinding(TitleSizeProperty, CreateTwoWayBinding(nameof(SelectedMarker.TitleSize)));
            SetBinding(MarkerWidthProperty, CreateTwoWayBinding(nameof(SelectedMarker.Width)));
            SetBinding(MarkerHeightProperty, CreateTwoWayBinding(nameof(SelectedMarker.Height)));
            SetBinding(MarkerBearingProperty, CreateTwoWayBinding(nameof(SelectedMarker.Bearing)));
            SetBinding(MarkerStrokeThicknessProperty, CreateTwoWayBinding(nameof(SelectedMarker.StrokeThickness)));
            SetBinding(ShowShapeProperty, CreateTwoWayBinding(nameof(SelectedMarker.ShowShape)));
            SetBinding(ShowTitleProperty, CreateTwoWayBinding(nameof(SelectedMarker.ShowTitle)));
            SetBinding(MarkerZoomProperty, CreateTwoWayBinding(nameof(SelectedMarker.Zoom)));

            // 색상 속성은 EnumColorType이므로 직접 바인딩
            SetBinding(MarkerFillColorProperty, CreateTwoWayBinding(nameof(SelectedMarker.FillColor)));
            SetBinding(MarkerStrokeColorProperty, CreateTwoWayBinding(nameof(SelectedMarker.StrokeColor)));

            //System.Diagnostics.Debug.WriteLine("SetupCommonBiding 완료");
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
            //System.Diagnostics.Debug.WriteLine("ClearAllBindings 시작");

            // 임시로 PropertyChanged 콜백 차단
            _isClearingBindings = true;

            ClearCommonBidings();

            ClearSpecificBindings();

            _isClearingBindings = false;
        }

        private void ClearCommonBidings()
        {
            // 바인딩 해제
            BindingOperations.ClearBinding(this, MarkerTitleProperty);
            BindingOperations.ClearBinding(this, TitleSizeProperty);
            BindingOperations.ClearBinding(this, MarkerWidthProperty);
            BindingOperations.ClearBinding(this, MarkerHeightProperty);
            BindingOperations.ClearBinding(this, MarkerBearingProperty);
            BindingOperations.ClearBinding(this, MarkerFillColorProperty);
            BindingOperations.ClearBinding(this, MarkerStrokeColorProperty);
            BindingOperations.ClearBinding(this, MarkerStrokeThicknessProperty);
            BindingOperations.ClearBinding(this, ShowShapeProperty);
            BindingOperations.ClearBinding(this, ShowTitleProperty);
            BindingOperations.ClearBinding(this, MarkerZoomProperty);
        }

        #endregion

        #region Group (멀티셀렉트) Pending 모드 — 기능 ② 피드백

        // 그룹 Pending 표기 sentinel: 숫자=NaN, 색=(EnumColorType)(-1) → 콤보 무선택/텍스트 빈칸 렌더.
        internal const double MIXED_DOUBLE = double.NaN;
        internal static readonly EnumColorType MIXED_COLOR = (EnumColorType)(-1);

        /// <summary>그룹 선택(≥2) 시 대상 마커 집합. null/1개=단일 모드(기존 동작).</summary>
        public System.Collections.Generic.IReadOnlyList<IEditableMarker>? GroupMarkers { get; private set; }

        /// <summary>그룹 편집 모드 여부 — 콜백의 대표마커 직접쓰기 억제 게이트(VM이 전원 일괄 적용).</summary>
        protected bool IsGroupMode => GroupMarkers is { Count: >= 2 };

        /// <summary>
        /// 그룹 편집 모드 진입(팩토리에서 SelectedMarker 세팅 직후 호출).
        /// ① 대표마커 TwoWay 바인딩 해제 — sentinel이 대표에 오염되거나 외부 갱신이 Pending을 덮는 것 차단.
        /// ② 공통 필드를 "전원 동일=값 / 서로 다름=빈칸(Pending)"으로 재구성(VS 속성그리드 방식).
        /// 이후 편집은 콜백이 이벤트만 발화 → MapViewModel이 전원(대표 포함)에 일괄 적용+배치 Undo.
        /// </summary>
        public void EnterGroupMode(System.Collections.Generic.IReadOnlyList<IEditableMarker> markers)
        {
            if (markers == null || markers.Count < 2) return;
            GroupMarkers = markers;

            _isInitializing = true;
            try
            {
                ClearAllBindings();                 // DP가 기본값으로 리셋됨 → 아래서 전 필드 재구성
                ApplyGroupPendingBlanks();
                if (SelectedMarker != null)
                    SetupSpecificPropertiesFromMarker(SelectedMarker);   // 특화 필드는 대표값 표시(대표 전용 편집)
            }
            finally { _isInitializing = false; }
        }

        /// <summary>공통 필드별로 그룹 전원 값 비교 → 동일=그 값, 다름=Pending sentinel 세팅.</summary>
        private void ApplyGroupPendingBlanks()
        {
            var g = GroupMarkers;
            if (g == null || g.Count == 0) return;

            MarkerTitle = AllEqual("Title", out var title) ? (title as string ?? string.Empty) : string.Empty;
            TitleSize = AllEqual("TitleSize", out var ts) ? ToD(ts) : MIXED_DOUBLE;
            MarkerWidth = AllEqual("Width", out var w) ? ToD(w) : MIXED_DOUBLE;
            MarkerHeight = AllEqual("Height", out var h) ? ToD(h) : MIXED_DOUBLE;
            MarkerBearing = AllEqual("Bearing", out var br) ? ToD(br) : MIXED_DOUBLE;
            MarkerZoom = AllEqual("Zoom", out var z) ? ToD(z) : MIXED_DOUBLE;
            MarkerStrokeThickness = AllEqual("StrokeThickness", out var st) ? ToD(st) : MIXED_DOUBLE;
            MarkerFillColor = AllEqual("FillColor", out var fc) && fc is EnumColorType f ? f : MIXED_COLOR;
            MarkerStrokeColor = AllEqual("StrokeColor", out var sc) && sc is EnumColorType s ? s : MIXED_COLOR;
            // bool(모양/제목 표시)은 3상태 미지원 → 대표값 표시(변경 시엔 전원 일괄 적용됨)
            var rep = SelectedMarker ?? g[0];
            ShowShape = rep.ShowShape;
            ShowTitle = rep.ShowTitle;

            static double ToD(object? v) => v is double d ? d : MIXED_DOUBLE;
        }

        /// <summary>그룹 전원의 prop 값이 동일한지(ReadProperty 단일출처). 동일하면 first에 그 값.</summary>
        private bool AllEqual(string prop, out object? first)
        {
            var g = GroupMarkers!;
            first = Services.Undo.Commands.UndoableCommandBase.ReadProperty(g[0], prop);
            for (int i = 1; i < g.Count; i++)
            {
                var v = Services.Undo.Commands.UndoableCommandBase.ReadProperty(g[i], prop);
                if (!Equals(first, v)) return false;
            }
            return true;
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

            // 닫기 버튼 이벤트 연결
            if (GetTemplateChild("PART_CloseButton") is Button closeButton)
            {
                closeButton.Click += OnCloseButtonClick;
                //System.Diagnostics.Debug.WriteLine("닫기 버튼 이벤트 연결 완료");
            }

            // 헤더 커서 설정
            if (GetTemplateChild("PART_HeaderArea") is FrameworkElement headerArea)
            {
                headerArea.Cursor = IsDraggable ? Cursors.SizeAll : Cursors.Arrow;
            }

            // Z-order 버튼 연결
            if (GetTemplateChild("PART_ZOrderTopButton") is Button topBtn)
                topBtn.Click += (_, _) => ZOrderChangeRequested?.Invoke(this, new ZOrderChangeRequestedEventArgs(ZOrderDirection.ToTop));
            if (GetTemplateChild("PART_ZOrderUpButton") is Button upBtn)
                upBtn.Click += (_, _) => ZOrderChangeRequested?.Invoke(this, new ZOrderChangeRequestedEventArgs(ZOrderDirection.Up));
            if (GetTemplateChild("PART_ZOrderDownButton") is Button downBtn)
                downBtn.Click += (_, _) => ZOrderChangeRequested?.Invoke(this, new ZOrderChangeRequestedEventArgs(ZOrderDirection.Down));
            if (GetTemplateChild("PART_ZOrderBottomButton") is Button bottomBtn)
                bottomBtn.Click += (_, _) => ZOrderChangeRequested?.Invoke(this, new ZOrderChangeRequestedEventArgs(ZOrderDirection.ToBottom));

            // "현재위치 적용" 버튼 배선은 생성자 AddHandler(ButtonBase.ClickEvent) + OnAnyButtonClick에서 처리.
            // (SpecificContent는 ContentPresenter 별도 namescope라 GetTemplateChild로 못 찾음)
        }

        /// <summary>버블링된 모든 버튼 클릭에서 "현재위치 적용" 버튼만 식별해 처리 — SpecificContent 버튼 배선용.</summary>
        private void OnAnyButtonClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not Button btn || btn.Name != "PART_ApplyDeviceLocationButton")
                return;
            //System.Diagnostics.Debug.WriteLine("[DeviceLocation] PART_ApplyDeviceLocationButton 클릭 감지");
            if (_applyLocBusy) { e.Handled = true; return; }   // 진행 중 재클릭 무시
            _applyLocBtn = btn;
            BeginDeviceLocationApply();
            DeviceLocationApplyRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        /// <summary>버튼을 진행 상태로 전환 — 내부에 무한 프로그래스바 표시(원래 콘텐츠 백업).</summary>
        private void BeginDeviceLocationApply()
        {
            if (_applyLocBtn == null) return;
            _applyLocBusy = true;
            _applyLocOriginalContent = _applyLocBtn.Content;
            _applyLocBtn.Content = new ProgressBar
            {
                IsIndeterminate = true,
                Height = 8,
                Width = 120,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
        }

        /// <summary>작업 완료(성공/실패) 시 버튼을 원래 상태로 복원 — MapViewModel이 호출.</summary>
        public void EndDeviceLocationApply(bool success)
        {
            if (_applyLocBtn == null) return;
            _applyLocBusy = false;
            if (_applyLocOriginalContent != null)
                _applyLocBtn.Content = _applyLocOriginalContent;
        }
        #endregion
        #region Fields
        private bool _isDragging;
        private Point _lastMousePosition;
        protected bool _isInitializing;
        protected bool _isClearingBindings;
        // "현재위치 적용" 버튼 진행 상태(Symbol_Apply_DeviceLocation)
        private Button? _applyLocBtn;
        private object? _applyLocOriginalContent;
        private bool _applyLocBusy;
        #endregion
    }


}