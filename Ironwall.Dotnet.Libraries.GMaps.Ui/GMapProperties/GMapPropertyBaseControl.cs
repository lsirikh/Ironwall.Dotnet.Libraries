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
        /// Shape 표시 여부 — bool?(3상태): null=그룹 Pending(값 서로 다름, indeterminate 표시).
        /// </summary>
        public bool? ShowShape
        {
            get { return (bool?)GetValue(ShowShapeProperty); }
            set { SetValue(ShowShapeProperty, value); }
        }

        public static readonly DependencyProperty ShowShapeProperty =
            DependencyProperty.Register("ShowShape", typeof(bool?),
                typeof(GMapPropertyBaseControl),
                new PropertyMetadata(true, OnShowShapeChanged));

        /// <summary>
        /// 제목 표시 여부 — bool?(3상태): null=그룹 Pending(값 서로 다름, indeterminate 표시).
        /// </summary>
        public bool? ShowTitle
        {
            get { return (bool?)GetValue(ShowTitleProperty); }
            set { SetValue(ShowTitleProperty, value); }
        }


        public static readonly DependencyProperty ShowTitleProperty =
            DependencyProperty.Register("ShowTitle", typeof(bool?),
                typeof(GMapPropertyBaseControl),
                new PropertyMetadata(false, OnShowTitleChanged));

        // ── 라벨 스타일 DP 6종 (Overlay_Title FR-07·13) — 기본값=마커/DB 기본값과 삼위일치(P2-06). ──

        /// <summary>라벨 글자색(packed ARGB). null=그룹 Pending.</summary>
        public int? TitleColorArgb
        {
            get { return (int?)GetValue(TitleColorArgbProperty); }
            set { SetValue(TitleColorArgbProperty, value); }
        }
        public static readonly DependencyProperty TitleColorArgbProperty =
            DependencyProperty.Register("TitleColorArgb", typeof(int?), typeof(GMapPropertyBaseControl),
                new PropertyMetadata(unchecked((int)0xF0F0F4F8), OnTitleColorArgbChanged));

        /// <summary>라벨 배경색(packed ARGB, 0=투명). null=그룹 Pending.</summary>
        public int? TitleBackgroundArgb
        {
            get { return (int?)GetValue(TitleBackgroundArgbProperty); }
            set { SetValue(TitleBackgroundArgbProperty, value); }
        }
        public static readonly DependencyProperty TitleBackgroundArgbProperty =
            DependencyProperty.Register("TitleBackgroundArgb", typeof(int?), typeof(GMapPropertyBaseControl),
                new PropertyMetadata(unchecked((int)0xCD1C1E22), OnTitleBackgroundArgbChanged));

        /// <summary>라벨 폰트 패밀리명(빈값=Segoe UI). 그룹 다름=빈값.</summary>
        public string TitleFontFamilyName
        {
            get { return (string)GetValue(TitleFontFamilyNameProperty); }
            set { SetValue(TitleFontFamilyNameProperty, value); }
        }
        public static readonly DependencyProperty TitleFontFamilyNameProperty =
            DependencyProperty.Register("TitleFontFamilyName", typeof(string), typeof(GMapPropertyBaseControl),
                new PropertyMetadata(string.Empty, OnTitleFontFamilyNameChanged));

        /// <summary>라벨 굵게. null=그룹 Pending.</summary>
        public bool? TitleBoldFlag
        {
            get { return (bool?)GetValue(TitleBoldFlagProperty); }
            set { SetValue(TitleBoldFlagProperty, value); }
        }
        public static readonly DependencyProperty TitleBoldFlagProperty =
            DependencyProperty.Register("TitleBoldFlag", typeof(bool?), typeof(GMapPropertyBaseControl),
                new PropertyMetadata(false, OnTitleBoldFlagChanged));

        /// <summary>라벨 이탤릭. null=그룹 Pending.</summary>
        public bool? TitleItalicFlag
        {
            get { return (bool?)GetValue(TitleItalicFlagProperty); }
            set { SetValue(TitleItalicFlagProperty, value); }
        }
        public static readonly DependencyProperty TitleItalicFlagProperty =
            DependencyProperty.Register("TitleItalicFlag", typeof(bool?), typeof(GMapPropertyBaseControl),
                new PropertyMetadata(false, OnTitleItalicFlagChanged));

        /// <summary>라벨 최대 폭 px(말줄임 지점, FR-13③ 숫자 정밀 입력 — 맵 WYSIWYG 핸들과 동일 값). NaN=그룹 Pending.</summary>
        public double TitleMaxWidthPx
        {
            get { return (double)GetValue(TitleMaxWidthPxProperty); }
            set { SetValue(TitleMaxWidthPxProperty, value); }
        }
        public static readonly DependencyProperty TitleMaxWidthPxProperty =
            DependencyProperty.Register("TitleMaxWidthPx", typeof(double), typeof(GMapPropertyBaseControl),
                new PropertyMetadata(200.0, OnTitleMaxWidthPxChanged));

        /// <summary>라벨 글자색 팔레트 — 기존 채우기/테두리 콤보 구조 차용(스와치+이름, SelectedValue=Argb).
        /// 첫 항목=종전 하드코딩 기본값(선택 표시 보장). 편집 hex 콤보는 렌더 결함으로 폐기(v2.3).</summary>
        public static LabelColorOption[] LabelTextColorOptions { get; } =
        {
            new("기본(밝음)", unchecked((int)0xF0F0F4F8)),
            new("흰색", unchecked((int)0xFFFFFFFF)),
            new("검정", unchecked((int)0xFF000000)),
            new("빨강", unchecked((int)0xFFFF5252)),
            new("주황", unchecked((int)0xFFFF8A65)),
            new("노랑", unchecked((int)0xFFFFC107)),
            new("초록", unchecked((int)0xFF4CAF50)),
            new("하늘", unchecked((int)0xFF00AAFF)),
            new("파랑", unchecked((int)0xFF2962FF)),
            new("보라", unchecked((int)0xFFB388FF)),
            new("회색", unchecked((int)0xFF9E9E9E)),
        };

        /// <summary>라벨 배경(칩)색 팔레트 — 투명·반투명 칩 중심 + 원색. 첫 항목=종전 기본 칩.</summary>
        public static LabelColorOption[] LabelBackgroundColorOptions { get; } =
        {
            new("기본(칩)", unchecked((int)0xCD1C1E22)),
            new("투명", 0),
            new("검정 반투명", unchecked((int)0x99000000)),
            new("흰색 반투명", unchecked((int)0xB3FFFFFF)),
            new("검정", unchecked((int)0xFF000000)),
            new("흰색", unchecked((int)0xFFFFFFFF)),
            new("빨강", unchecked((int)0xFFB71C1C)),
            new("파랑", unchecked((int)0xFF0D47A1)),
            new("초록", unchecked((int)0xFF1B5E20)),
            new("노랑", unchecked((int)0xFFF9A825)),
        };

        /// <summary>라벨 폰트 큐레이션 — 한글 Windows 표준 탑재 폰트 위주. 한글 폰트는 한글명, 영문 폰트는 영문명(v2.4).</summary>
        public static LabelFontOption[] LabelFontOptions { get; } =
        {
            new("기본 (Segoe UI)", string.Empty),
            new("맑은 고딕", "Malgun Gothic"),
            new("굴림", "Gulim"),
            new("돋움", "Dotum"),
            new("바탕", "Batang"),
            new("궁서", "Gungsuh"),
            new("Segoe UI", "Segoe UI"),
            new("Arial", "Arial"),
            new("Verdana", "Verdana"),
            new("Tahoma", "Tahoma"),
            new("Times New Roman", "Times New Roman"),
            new("Consolas", "Consolas"),
        };

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
                    control.TitleColorArgb = newMarker.TitleColor;
                    control.TitleBackgroundArgb = newMarker.TitleBackground;
                    control.TitleFontFamilyName = newMarker.TitleFontFamily;
                    control.TitleBoldFlag = newMarker.TitleBold;
                    control.TitleItalicFlag = newMarker.TitleItalic;
                    control.TitleMaxWidthPx = newMarker.TitleMaxWidth;

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

                // 마커 전환/패널 오픈 시 항상 BASIC(제목)부터 — LABEL 섹션 추가로 길어진 패널이
                // 이전 스크롤 위치를 물려받아 중간부터 보이는 문제 방지(v2.4 후속).
                control._contentScroll?.ScrollToTop();
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
                if (e.NewValue is not bool nsShape) return;   // null=그룹 Pending(indeterminate) — 마커 미전파
                if (!control.IsGroupMode) control.SelectedMarker.ShowShape = nsShape;
                control.OnMarkerPropertyChanged("ShowShape", e.OldValue, nsShape);
            }
        }

        private static void OnShowTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                if (e.NewValue is not bool nsTitle) return;   // null=그룹 Pending(indeterminate) — 마커 미전파
                if (!control.IsGroupMode) control.SelectedMarker.ShowTitle = nsTitle;
                control.OnMarkerPropertyChanged("ShowTitle", e.OldValue, nsTitle);
            }
        }

        // ── 라벨 스타일 콜백 6종 (FR-07) — 기존 패턴 답습: 3중 가드 + null(Pending) 미전파 + IsGroupMode 직접쓰기 억제. ──

        private static void OnTitleColorArgbChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                if (e.NewValue is not int v) return;   // null=그룹 Pending — 마커 미전파
                if (!control.IsGroupMode) control.SelectedMarker.TitleColor = v;
                control.OnMarkerPropertyChanged("TitleColor", e.OldValue, v);
            }
        }

        private static void OnTitleBackgroundArgbChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                if (e.NewValue is not int v) return;
                if (!control.IsGroupMode) control.SelectedMarker.TitleBackground = v;
                control.OnMarkerPropertyChanged("TitleBackground", e.OldValue, v);
            }
        }

        private static void OnTitleFontFamilyNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                var v = e.NewValue as string ?? string.Empty;
                if (!control.IsGroupMode) control.SelectedMarker.TitleFontFamily = v;
                control.OnMarkerPropertyChanged("TitleFontFamily", e.OldValue, v);
            }
        }

        private static void OnTitleBoldFlagChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                if (e.NewValue is not bool v) return;   // null=그룹 Pending
                if (!control.IsGroupMode) control.SelectedMarker.TitleBold = v;
                control.OnMarkerPropertyChanged("TitleBold", e.OldValue, v);
            }
        }

        private static void OnTitleItalicFlagChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                if (e.NewValue is not bool v) return;
                if (!control.IsGroupMode) control.SelectedMarker.TitleItalic = v;
                control.OnMarkerPropertyChanged("TitleItalic", e.OldValue, v);
            }
        }

        private static void OnTitleMaxWidthPxChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapPropertyBaseControl control && control.SelectedMarker != null && !control._isInitializing && !control._isClearingBindings)
            {
                if (e.NewValue is not double v || double.IsNaN(v)) return;   // NaN=그룹 Pending
                if (!control.IsGroupMode) control.SelectedMarker.TitleMaxWidth = v;
                control.OnMarkerPropertyChanged("TitleMaxWidth", e.OldValue, v);
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

            // 라벨 스타일 (FR-07) — Setup/Clear 쌍 유지(체크리스트 ⑦)
            SetBinding(TitleColorArgbProperty, CreateTwoWayBinding(nameof(SelectedMarker.TitleColor)));
            SetBinding(TitleBackgroundArgbProperty, CreateTwoWayBinding(nameof(SelectedMarker.TitleBackground)));
            SetBinding(TitleFontFamilyNameProperty, CreateTwoWayBinding(nameof(SelectedMarker.TitleFontFamily)));
            SetBinding(TitleBoldFlagProperty, CreateTwoWayBinding(nameof(SelectedMarker.TitleBold)));
            SetBinding(TitleItalicFlagProperty, CreateTwoWayBinding(nameof(SelectedMarker.TitleItalic)));
            SetBinding(TitleMaxWidthPxProperty, CreateTwoWayBinding(nameof(SelectedMarker.TitleMaxWidth)));

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
            BindingOperations.ClearBinding(this, TitleColorArgbProperty);
            BindingOperations.ClearBinding(this, TitleBackgroundArgbProperty);
            BindingOperations.ClearBinding(this, TitleFontFamilyNameProperty);
            BindingOperations.ClearBinding(this, TitleBoldFlagProperty);
            BindingOperations.ClearBinding(this, TitleItalicFlagProperty);
            BindingOperations.ClearBinding(this, TitleMaxWidthPxProperty);
        }

        #endregion

        #region Group (멀티셀렉트) Pending 모드 — 기능 ② 피드백

        // 그룹 Pending 표기 sentinel: 숫자=NaN, 색=(EnumColorType)(-1) → 콤보 무선택/텍스트 빈칸 렌더.
        internal const double MIXED_DOUBLE = double.NaN;
        internal static readonly EnumColorType MIXED_COLOR = (EnumColorType)(-1);

        /// <summary>그룹 선택(≥2) 시 대상 마커 집합. null/1개=단일 모드(기존 동작).</summary>
        public System.Collections.Generic.IReadOnlyList<IEditableMarker>? GroupMarkers { get; private set; }

        /// <summary>그룹 편집 모드 여부(XAML 트리거용 — 예: 디바이스 연결 콤보 비활성).</summary>
        public bool IsGroupEditing
        {
            get => (bool)GetValue(IsGroupEditingProperty);
            set => SetValue(IsGroupEditingProperty, value);
        }
        public static readonly DependencyProperty IsGroupEditingProperty =
            DependencyProperty.Register(nameof(IsGroupEditing), typeof(bool),
                typeof(GMapPropertyBaseControl), new PropertyMetadata(false));

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
            IsGroupEditing = true;

            _isInitializing = true;
            try
            {
                ClearAllBindings();                 // DP가 기본값으로 리셋됨 → 아래서 전 필드 재구성
                ApplyGroupPendingBlanks();
                ApplyGroupPendingSpecific();        // 특화 필드 — 기본=대표값, 타입 패널이 override로 Pending 확장(PIDS 등)
            }
            finally { _isInitializing = false; }
        }

        /// <summary>그룹 모드의 특화 필드 구성 — 기본: 대표 마커 값 표시(대표 전용 편집).
        /// 타입 패널(PIDS 등)이 override하여 "동일=값/다름=Pending"으로 확장.</summary>
        protected virtual void ApplyGroupPendingSpecific()
        {
            if (SelectedMarker != null)
                SetupSpecificPropertiesFromMarker(SelectedMarker);
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
            // bool(모양/제목 표시) 3상태: 다르면 null(indeterminate) — 클릭 시 true부터 순환(전원 적용)
            ShowShape = AllEqual("ShowShape", out var ss) && ss is bool sb ? sb : (bool?)null;
            ShowTitle = AllEqual("ShowTitle", out var stt) && stt is bool tb ? tb : (bool?)null;

            // 라벨 스타일 (FR-07) — int?/bool?=null Pending, 폰트 다름=빈값, 폭 다름=NaN
            TitleColorArgb = AllEqual("TitleColor", out var tcv) && tcv is int tci ? tci : (int?)null;
            TitleBackgroundArgb = AllEqual("TitleBackground", out var tbv) && tbv is int tbi ? tbi : (int?)null;
            TitleFontFamilyName = AllEqual("TitleFontFamily", out var tfv) ? (tfv as string ?? string.Empty) : string.Empty;
            TitleBoldFlag = AllEqual("TitleBold", out var tbf) && tbf is bool tbb ? tbb : (bool?)null;
            TitleItalicFlag = AllEqual("TitleItalic", out var tif) && tif is bool tib ? tib : (bool?)null;
            TitleMaxWidthPx = AllEqual("TitleMaxWidth", out var tmw) ? ToD(tmw) : MIXED_DOUBLE;

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

            // 내용 스크롤 캐시 — 마커 선택 시 BASIC(제목)부터 보이게 상단 복귀(LABEL 섹션 추가로 패널이 길어져
            // 이전 스크롤 위치가 남으면 열자마자 중간부터 보이는 문제, Overlay_Title v2.4 후속).
            _contentScroll = GetTemplateChild("PART_ContentScroll") as ScrollViewer;
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
        // 내용 스크롤(마커 전환 시 상단 복귀용)
        private ScrollViewer? _contentScroll;
        #endregion
    }

    /// <summary>라벨 색 팔레트 항목(이름 + packed ARGB) — 기존 채우기/테두리 색 콤보 구조를 라벨 색에 차용하기 위한
    /// 아이템 모델(Overlay_Title FR-07 v2.3). SelectedValuePath=Argb로 int? DP와 직결.</summary>
    public sealed class LabelColorOption
    {
        public string Name { get; }
        public int Argb { get; }
        public LabelColorOption(string name, int argb) { Name = name; Argb = argb; }
        public override string ToString() => Name;
    }

    /// <summary>라벨 폰트 항목 — 큐레이션 리스트(한글 폰트=한글명, 영문 폰트=영문명, FR-07 v2.4).
    /// 시스템 전체 열거({x:Static Fonts.SystemFontFamilies})는 설치 폰트 자원 상태에 따라 패널 로드/드롭다운에서
    /// 예외로 앱이 죽는 크래시 벡터라 제거. 미탑재 폰트 선택 시 WPF 무음 폴백(크래시 없음).</summary>
    public sealed class LabelFontOption
    {
        public string DisplayName { get; }
        /// <summary>저장값(FontFamily invariant name). 빈값=Segoe UI 기본 규약.</summary>
        public string Family { get; }
        /// <summary>드롭다운 미리보기용(빈값이면 Segoe UI).</summary>
        public string PreviewFamily => string.IsNullOrEmpty(Family) ? "Segoe UI" : Family;
        public LabelFontOption(string displayName, string family) { DisplayName = displayName; Family = family; }
        public override string ToString() => DisplayName;
    }


}