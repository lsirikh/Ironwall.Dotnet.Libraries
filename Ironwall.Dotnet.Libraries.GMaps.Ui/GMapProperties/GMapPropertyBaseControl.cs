using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
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
        public event EventHandler<MarkerPropertyChangedEventArgs>? MarkerPropertyChanged;


        /// <summary>
        /// 닫기 버튼 클릭 이벤트
        /// </summary>
        public event EventHandler? CloseRequested;
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
            //System.Diagnostics.Debug.WriteLine($"=== MouseLeftButtonDown 이벤트 ===");
            //System.Diagnostics.Debug.WriteLine($"IsDraggable: {IsDraggable}");

            if (!IsDraggable) return;

            var position = e.GetPosition(this);
            //System.Diagnostics.Debug.WriteLine($"마우스 위치: ({position.X}, {position.Y})");

            if (position.Y > 45)
            {
                System.Diagnostics.Debug.WriteLine("헤더 영역 밖 클릭");
                return;
            }

            // ContentPresenter와 Canvas 찾기
            var contentPresenter = FindParentOfType<ContentPresenter>(this);
            var canvas = contentPresenter?.Parent as Canvas;

            if (canvas == null)
            {
                System.Diagnostics.Debug.WriteLine("Canvas를 찾을 수 없어서 드래그 시작 불가");
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
                System.Diagnostics.Debug.WriteLine("ContentPresenter를 찾을 수 없음");
                return;
            }

            //System.Diagnostics.Debug.WriteLine($"ContentPresenter 발견: {contentPresenter.GetType().Name}");

            // Canvas 찾기
            var canvas = contentPresenter.Parent as Canvas;
            if (canvas == null)
            {
                System.Diagnostics.Debug.WriteLine($"Canvas를 찾을 수 없음. ContentPresenter.Parent: {contentPresenter.Parent?.GetType().Name}");
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
                    control.TitleSize = newMarker.TitleSize;
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

        private static void OnTitleSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnTitleSizeChanged: '{e.OldValue}' -> '{e.NewValue}'");
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                System.Diagnostics.Debug.WriteLine($"  마커에 전파: SelectedMarker.TitleSize = '{e.NewValue}'");
                control.SelectedMarker.TitleSize = (double)e.NewValue;
                control.OnMarkerPropertyChanged("TitleSize", e.OldValue, e.NewValue);
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
            System.Diagnostics.Debug.WriteLine($"OnMarkerBearingChanged: {e.OldValue} -> {e.NewValue}");

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
            SetBinding(TitleSizeProperty, CreateTwoWayBinding(nameof(SelectedMarker.TitleSize)));
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
            System.Diagnostics.Debug.WriteLine($"[*바인딩 해제 전] TitleSize: '{TitleSize}'");
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
            System.Diagnostics.Debug.WriteLine($"[바인딩 해제 후*] TitleSize: '{TitleSize}'");
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
            BindingOperations.ClearBinding(this, TitleSizeProperty);
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