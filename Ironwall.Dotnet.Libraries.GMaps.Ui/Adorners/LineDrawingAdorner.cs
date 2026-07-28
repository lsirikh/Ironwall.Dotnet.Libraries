using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners{
    /****************************************************************************
       Purpose      :
       Created By   : GHLee
       Created On   : 9/16/2025 10:42:39 AM
       Department   : SW Team
       Company      : Sensorway Co., Ltd.
       Email        : lsirikh@naver.com
    ****************************************************************************/
    /// <summary>
    /// 라인 드로잉을 위한 Adorner.
    /// 지도 위에 라인/미리보기/포인트를 직접 그리고, 표준 플로팅 HUD
    /// (<see cref="LineDrawingHudControl"/>)를 호스팅해 진행 상태·완료/되돌리기/닫기를 제공한다.
    /// HUD 헤더 드래그 이동과 위치 지속(드래그 후 절대 고정)은 이 어도너가 소유한다.
    /// </summary>
    public class LineDrawingAdorner : Adorner
    {
        #region Fields

        private readonly GMapControl _mapControl;
        private readonly ILogService _log;
        private readonly List<PointLatLng> _geoPoints = new List<PointLatLng>();
        private Point? _currentMousePosition;
        private readonly string _hudTitle;

        // HUD UI (표준 패널 컨트롤)
        private Canvas _controlCanvas;
        private LineDrawingHudControl _hud;

        // 렌더링용 펜/브러시
        private Pen _linePen;
        private Pen _previewPen;
        private Pen _pointPen;
        private Brush _pointFill = Brushes.White;
        private double _pointRadius = 5;

        // 테마 토큰 기반 시작/끝 강조 리소스 — 첫 렌더에서 1회 해석·캐싱(매 프레임 재할당 방지)
        private bool _markerResourcesReady;
        private Pen _startPen;
        private Pen _endPen;
        private Brush _startFill;
        private Brush _endFill;

        // 드래그 / 위치 지속
        //  · 드래그 전 → 첫 점 기준 상대 오프셋(Initial*)으로 배치(팬/줌 시 첫 점을 따라감)
        //  · 한 번 드래그하면 → 절대 캔버스 좌표로 '고정'(팬/줌·후속 클릭에도 그 자리 유지)
        //  · Clear()에서 초기화
        private const double InitialOffsetX = 20;
        private const double InitialOffsetY = -50;
        private const double HeaderHeight = 34;   // LineDrawingHudStyle 헤더 행 높이와 일치
        private bool _isDraggingControl;
        private Point _dragOffset;
        private bool _hasBeenDragged;
        private Point _absolutePosition;
        #endregion

        #region Events

        public event EventHandler CompleteRequested;
        public event EventHandler UndoRequested;
        public event EventHandler CancelRequested;

        #endregion

        #region Constructor

        public LineDrawingAdorner(UIElement adornedElement, GMapControl mapControl, ILogService log = null, string hudTitle = null)
            : base(adornedElement)
        {
            _mapControl = mapControl ?? throw new ArgumentNullException(nameof(mapControl));
            _log = log;
            _hudTitle = string.IsNullOrWhiteSpace(hudTitle) ? "그리기" : hudTitle;

            InitializePens();
            InitializeControlUI();

            // 마우스 이벤트 수신(HUD 히트테스트 및 드래그)
            IsHitTestVisible = true;
            Focusable = true;
            // 지도 이동/줌 이벤트 구독
            _mapControl.OnMapZoomChanged += OnMapChanged;
            _mapControl.OnMapDrag += OnMapChanged;

            _log?.Info("LineDrawingAdorner 생성 완료");
        }

        #endregion
        #region Initialization

        private void InitializePens()
        {
            _linePen = new Pen(Brushes.Red, 3)
            {
                LineJoin = PenLineJoin.Round,
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };

            _previewPen = new Pen(Brushes.Gray, 2)
            {
                DashStyle = new DashStyle(new double[] { 5, 3 }, 0),
                LineJoin = PenLineJoin.Round
            };

            _pointPen = new Pen(Brushes.DarkRed, 2);
        }

        private void InitializeControlUI()
        {
            _controlCanvas = new Canvas { IsHitTestVisible = true };

            _hud = new LineDrawingHudControl
            {
                HudTitle = _hudTitle,
                Visibility = Visibility.Collapsed
            };

            // 버튼 이벤트 → 어도너 공개 이벤트로 재라우팅(서비스 계약 불변)
            _hud.CompleteRequested += (_, __) => CompleteRequested?.Invoke(this, EventArgs.Empty);
            _hud.UndoRequested += (_, __) => UndoRequested?.Invoke(this, EventArgs.Empty);
            _hud.CloseRequested += (_, __) => CancelRequested?.Invoke(this, EventArgs.Empty);

            // 헤더 드래그(위치 지속 로직은 어도너 소유)
            _hud.MouseLeftButtonDown += OnHudMouseDown;
            _hud.MouseMove += OnHudMouseMove;
            _hud.MouseLeftButtonUp += OnHudMouseUp;

            _controlCanvas.Children.Add(_hud);

            // Visual 자식으로 추가
            AddVisualChild(_controlCanvas);
            AddLogicalChild(_controlCanvas);
        }

        #endregion

        #region Drag Handlers (헤더 이동)

        private void OnHudMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            // 헤더 영역에서만 드래그 시작(본문 클릭 제외), 버튼(닫기 등) 위는 제외
            if (e.GetPosition(_hud).Y > HeaderHeight) return;
            if (IsButton(e.OriginalSource as DependencyObject)) return;

            _isDraggingControl = true;
            _dragOffset = e.GetPosition(_hud);
            _hud.CaptureMouse();
            e.Handled = true;
        }

        private void OnHudMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingControl || e.LeftButton != MouseButtonState.Pressed) return;

            var cur = e.GetPosition(_controlCanvas);
            var newLeft = cur.X - _dragOffset.X;
            var newTop = cur.Y - _dragOffset.Y;

            // 화면 경계 클램프(드래그 중에만)
            newLeft = Math.Max(0, Math.Min(newLeft, _controlCanvas.ActualWidth - _hud.ActualWidth));
            newTop = Math.Max(0, Math.Min(newTop, _controlCanvas.ActualHeight - _hud.ActualHeight));

            Canvas.SetLeft(_hud, newLeft);
            Canvas.SetTop(_hud, newTop);

            // 이후 절대 위치 고정(팬/줌·후속 클릭에도 유지)
            _hasBeenDragged = true;
            _absolutePosition = new Point(newLeft, newTop);
            e.Handled = true;
        }

        private void OnHudMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingControl) return;
            _isDraggingControl = false;
            _hud.ReleaseMouseCapture();
            e.Handled = true;
        }

        // 클릭 원본이 버튼(또는 그 자식)인지 — 헤더 드래그와 버튼 클릭 충돌 방지
        private static bool IsButton(DependencyObject element)
        {
            while (element != null)
            {
                if (element is ButtonBase) return true;
                element = VisualTreeHelper.GetParent(element);
            }
            return false;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 포인트 추가
        /// </summary>
        public void AddPoint(PointLatLng geoPoint)
        {
            _geoPoints.Add(geoPoint);
            UpdateControlUI();
            InvalidateVisual();

            _log?.Info($"Adorner 포인트 추가: {geoPoint} (총 {_geoPoints.Count}개)");
        }

        /// <summary>
        /// 마지막 포인트 제거
        /// </summary>
        public bool RemoveLastPoint()
        {
            if (_geoPoints.Count > 0)
            {
                var removed = _geoPoints[_geoPoints.Count - 1];
                _geoPoints.RemoveAt(_geoPoints.Count - 1);
                UpdateControlUI();
                InvalidateVisual();

                _log?.Info($"Adorner 포인트 제거: {removed} (남은 개수: {_geoPoints.Count})");
                return true;
            }
            return false;
        }

        /// <summary>
        /// 마우스 위치 업데이트
        /// </summary>
        public void UpdateMousePosition(Point? screenPosition)
        {
            _currentMousePosition = screenPosition;
            InvalidateVisual();
        }

        /// <summary>
        /// 모든 포인트 클리어 + 위치 지속 상태 초기화(새 드로잉 대비)
        /// </summary>
        public void Clear()
        {
            _geoPoints.Clear();
            _currentMousePosition = null;
            _hasBeenDragged = false;
            _absolutePosition = default;
            if (_hud != null) _hud.Visibility = Visibility.Collapsed;
            InvalidateVisual();

            _log?.Info("모든 포인트 제거");
        }

        /// <summary>
        /// 확정 선 펜 스타일 설정(심볼 StrokeColor/두께/패턴)
        /// </summary>
        public void SetLineStyle(Brush brush, double thickness, DashStyle dashStyle = null)
        {
            _linePen.Brush = brush;
            _linePen.Thickness = thickness;
            if (dashStyle != null)
            {
                _linePen.DashStyle = dashStyle;
            }
            InvalidateVisual();
        }

        #endregion
        #region Control UI Management

        private void UpdateControlUI()
        {
            if (_geoPoints.Count > 0)
            {
                if (_hasBeenDragged)
                {
                    // 사용자가 옮긴 절대 위치 유지(팬/줌·후속 클릭 무관)
                    Canvas.SetLeft(_hud, _absolutePosition.X);
                    Canvas.SetTop(_hud, _absolutePosition.Y);
                }
                else
                {
                    // 최초/미드래그 상태 — 첫 점 기준 오프셋(현행 동작)
                    var firstScreenPoint = ConvertToScreenPoint(_geoPoints[0]);
                    Canvas.SetLeft(_hud, firstScreenPoint.X + InitialOffsetX);
                    Canvas.SetTop(_hud, firstScreenPoint.Y + InitialOffsetY);
                }

                _hud.Visibility = Visibility.Visible;
                UpdateStatusText();
                _hud.CanComplete = _geoPoints.Count >= 2;
            }
            else
            {
                _hud.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateStatusText()
        {
            try
            {
                if (_geoPoints.Count <= 1)
                {
                    _hud.PointText = $"{_geoPoints.Count} 점";
                    _hud.DistanceText = "—";
                }
                else
                {
                    _hud.PointText = $"{_geoPoints.Count} 점";
                    _hud.DistanceText = $"{TotalDistance:F1} m";
                }
            }
            catch (Exception ex)
            {
                _log?.Warning($"[Line] 상태 텍스트 갱신 실패: {ex.Message}");
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// 지리 좌표 포인트 리스트
        /// </summary>
        public List<PointLatLng> GeoPoints => new List<PointLatLng>(_geoPoints);

        /// <summary>
        /// 포인트 개수
        /// </summary>
        public int PointCount => _geoPoints.Count;

        /// <summary>
        /// 유효한 라인 여부 (최소 2개 포인트)
        /// </summary>
        public bool IsValid => _geoPoints.Count >= 2;

        /// <summary>
        /// 총 거리 (미터) — 열린 경로 기준(마지막→첫 점 폐합 미포함)
        /// </summary>
        public double TotalDistance
        {
            get
            {
                double distance = 0;
                for (int i = 1; i < _geoPoints.Count; i++)
                {
                    distance += _mapControl.MapProvider.Projection.GetDistance(
                        _geoPoints[i - 1], _geoPoints[i]) * 1000;
                }
                return distance;
            }
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// 렌더링 오버라이드
        /// </summary>
        protected override void OnRender(DrawingContext drawingContext)
        {
            if (_geoPoints.Count == 0) return;

            EnsureMarkerResources();

            try
            {
                // 지리 좌표를 화면 좌표로 변환
                var screenPoints = ConvertToScreenPoints(_geoPoints);
                if (screenPoints.Count == 0) return;

                // 1. 확정된 라인 그리기(심볼 StrokeColor)
                if (screenPoints.Count >= 2)
                {
                    for (int i = 1; i < screenPoints.Count; i++)
                    {
                        drawingContext.DrawLine(_linePen, screenPoints[i - 1], screenPoints[i]);
                    }
                }

                // 2. 미리보기 라인(Muted 점선)
                if (_currentMousePosition.HasValue && screenPoints.Count > 0)
                {
                    drawingContext.DrawLine(_previewPen,
                        screenPoints[screenPoints.Count - 1],
                        _currentMousePosition.Value);
                }

                // 3. 각 꼭짓점(테두리=Primary)
                foreach (var point in screenPoints)
                {
                    drawingContext.DrawEllipse(_pointFill, _pointPen, point, _pointRadius, _pointRadius);
                }

                // 4. 시작점(StatusNormal)·끝점(Accent) 강조
                drawingContext.DrawEllipse(_startFill, _startPen,
                    screenPoints[0], _pointRadius + 2, _pointRadius + 2);

                if (screenPoints.Count >= 2)
                {
                    drawingContext.DrawEllipse(_endFill, _endPen,
                        screenPoints[screenPoints.Count - 1], _pointRadius + 2, _pointRadius + 2);
                }
            }
            catch (Exception ex)
            {
                _log?.Error($"Adorner 렌더링 오류: {ex.Message}");
            }
        }

        /// <summary>테마 토큰 기반 마커 리소스 1회 해석·캐싱(펜 매 프레임 재할당 방지).</summary>
        private void EnsureMarkerResources()
        {
            if (_markerResourcesReady) return;

            var start = TryFindResource("StatusNormalBrush") as Brush ?? Brushes.LimeGreen;
            var end = TryFindResource("AccentBrush") as Brush ?? Brushes.Orange;
            var vertex = TryFindResource("PrimaryBrush") as Brush ?? Brushes.DeepSkyBlue;
            var muted = TryFindResource("TextMutedBrush") as Brush ?? Brushes.Gray;

            _startFill = start;
            _endFill = end;
            _startPen = MakePen(start, 2);
            _endPen = MakePen(end, 2);
            _pointPen = MakePen(vertex, 2);   // 꼭짓점 테두리 = Primary
            _previewPen.Brush = muted;        // 미리보기 = Muted 점선

            _markerResourcesReady = true;
        }

        private static Pen MakePen(Brush brush, double thickness)
        {
            var pen = new Pen(brush, thickness);
            if (pen.CanFreeze) pen.Freeze();
            return pen;
        }

        protected override Visual GetVisualChild(int index)
        {
            if (index == 0)
                return _controlCanvas;
            throw new ArgumentOutOfRangeException();
        }
        protected override int VisualChildrenCount => 1;

        protected override Size ArrangeOverride(Size finalSize)
        {
            _controlCanvas.Arrange(new Rect(finalSize));
            return base.ArrangeOverride(finalSize);
        }

        #endregion

        #region Private Methods
        private Point ConvertToScreenPoint(PointLatLng geoPoint)
        {
            var screenPoint = _mapControl.FromLatLngToLocal(geoPoint);
            return new Point(screenPoint.X, screenPoint.Y);
        }

        /// <summary>
        /// 지리 좌표를 화면 좌표로 변환
        /// </summary>
        private List<Point> ConvertToScreenPoints(List<PointLatLng> geoPoints)
        {
            var screenPoints = new List<Point>();
            foreach (var geoPoint in geoPoints)
            {
                var screenPoint = _mapControl.FromLatLngToLocal(geoPoint);
                screenPoints.Add(new Point(screenPoint.X, screenPoint.Y));
            }
            return screenPoints;
        }

        /// <summary>
        /// 지도 변경 이벤트 핸들러 (이동/줌) — UI 스레드 직렬화(백그라운드 발화 대비).
        /// </summary>
        private void OnMapChanged()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.InvokeAsync(OnMapChanged);
                return;
            }
            UpdateControlUI();
            InvalidateVisual();
        }

        #endregion
        #region Override Layout Methods

        protected override Size MeasureOverride(Size availableSize)
        {
            _controlCanvas?.Measure(availableSize);
            return base.MeasureOverride(availableSize);
        }

        // HitTest 오버라이드 — HUD 영역만 히트, 그 외(라인/포인트/빈 지도)는 null → 지도 클릭 통과(점 추가 유지)
        protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        {
            if (_hud != null && _hud.Visibility == Visibility.Visible)
            {
                var left = Canvas.GetLeft(_hud);
                var top = Canvas.GetTop(_hud);
                if (!double.IsNaN(left) && !double.IsNaN(top))
                {
                    var point = hitTestParameters.HitPoint;
                    var bounds = new Rect(left, top, _hud.ActualWidth, _hud.ActualHeight);
                    if (bounds.Contains(point))
                    {
                        return new PointHitTestResult(this, point);
                    }
                }
            }

            // 라인과 포인트는 클릭 무시
            return null;
        }

        #endregion
        #region Cleanup

        /// <summary>
        /// 리소스 정리
        /// </summary>
        public void Cleanup()
        {
            // HUD 이벤트 구독 해제(버튼 이벤트는 람다-구독으로 _hud와 함께 GC)
            if (_hud != null)
            {
                _hud.MouseLeftButtonDown -= OnHudMouseDown;
                _hud.MouseMove -= OnHudMouseMove;
                _hud.MouseLeftButtonUp -= OnHudMouseUp;
            }

            // 지도 이벤트 구독 해제
            if (_mapControl != null)
            {
                _mapControl.OnMapZoomChanged -= OnMapChanged;
                _mapControl.OnMapDrag -= OnMapChanged;
            }

            // Visual 제거
            RemoveVisualChild(_controlCanvas);
            RemoveLogicalChild(_controlCanvas);

            Clear();
            _log?.Info("LineDrawingAdorner 정리 완료");
        }

        #endregion
    }
}
