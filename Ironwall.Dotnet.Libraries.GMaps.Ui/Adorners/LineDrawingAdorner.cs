using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using static MaterialDesignThemes.Wpf.Theme;
using Button = System.Windows.Controls.Button;

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
    /// 라인 드로잉을 위한 Adorner
    /// 지도 위에 직접 라인을 그리고 포인트를 관리
    /// </summary>
    public class LineDrawingAdorner : Adorner
    {
        #region Fields

        private readonly GMapControl _mapControl;
        private readonly ILogService _log;
        private readonly List<PointLatLng> _geoPoints = new List<PointLatLng>();
        private Point? _currentMousePosition;

        // 컨트롤 UI
        private Canvas _controlCanvas;
        private Border _controlBorder;
        private Button _completeButton;
        private Button _undoButton;
        private Button _cancelButton;
        private TextBlock _statusText;

        // 렌더링용 펜
        private Pen _linePen;
        private Pen _previewPen;
        private Pen _pointPen;
        private Brush _pointFill = Brushes.White;
        private double _pointRadius = 5;

        #endregion
        #region Events

        public event EventHandler CompleteRequested;
        public event EventHandler UndoRequested;
        public event EventHandler CancelRequested;

        #endregion

        #region Constructor

        public LineDrawingAdorner(UIElement adornedElement, GMapControl mapControl, ILogService log = null)
            : base(adornedElement)
        {
            _mapControl = mapControl ?? throw new ArgumentNullException(nameof(mapControl));
            _log = log;

            InitializePens();
            InitializeControlUI();

            // 이벤트 차단 안함 (마우스 이벤트 받아야 함)
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
            // 컨트롤 Canvas
            _controlCanvas = new Canvas
            {
                IsHitTestVisible = true  // 명시적으로 설정
            };

            // 테두리 추가
            _controlBorder = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(5),
                Background = new SolidColorBrush(Color.FromArgb(240, 255, 255, 255)),
                IsHitTestVisible = true,  // 명시적으로 설정
                Focusable = true,  // 추가
                Visibility = Visibility.Collapsed,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 10,
                    ShadowDepth = 3,
                    Opacity = 0.5,
                    Color = Colors.Black
                }
            };

            // 테두리 추가
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(5),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 10,
                    ShadowDepth = 3,
                    Opacity = 0.5,
                    Color = Colors.Black
                }
            };

            // 버튼 패널
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(5)
            };

            // 상태 텍스트
            _statusText = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 5, 0),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.DarkBlue
            };

            // 완료 버튼
            _completeButton = CreateButton("✓", "완료 (Enter)", Brushes.Green);
            _completeButton.Click += (s, e) => CompleteRequested?.Invoke(this, EventArgs.Empty);

            // 취소(Undo) 버튼
            _undoButton = CreateButton("↶", "마지막 점 취소 (Backspace)", Brushes.Orange);
            _undoButton.Click += (s, e) => UndoRequested?.Invoke(this, EventArgs.Empty);

            // 전체 취소 버튼
            _cancelButton = CreateButton("✕", "취소 (Esc)", Brushes.Red);
            _cancelButton.Click += (s, e) => CancelRequested?.Invoke(this, EventArgs.Empty);

            

            // 컨트롤 구성
            buttonPanel.Children.Add(_statusText);
            buttonPanel.Children.Add(_completeButton);
            buttonPanel.Children.Add(_undoButton);
            buttonPanel.Children.Add(_cancelButton);

            _controlBorder.Child = buttonPanel;
            _controlCanvas.Children.Add(_controlBorder);

            // Visual 자식으로 추가
            AddVisualChild(_controlCanvas);
            AddLogicalChild(_controlCanvas);
        }

        private Button CreateButton(string content, string tooltip, Brush foreground)
        {
            var button = new Button
            {
                Content = content,
                ToolTip = tooltip,
                Width = 30,
                Height = 30,
                Margin = new Thickness(2),
                Background = Brushes.White,
                Foreground = foreground,
                BorderBrush = foreground,
                BorderThickness = new Thickness(1),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand,
                IsHitTestVisible = true,  // 명시적으로 설정
                Focusable = true
            };

            // 기본 스타일 설정 (Template 오버라이드 방지)
            button.Template = GetButtonTemplate();

            return button;
        }

        // 버튼 템플릿 (간단한 버전)
        private ControlTemplate GetButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));

            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            border.AppendChild(contentPresenter);
            template.VisualTree = border;

            // 호버 효과를 위한 트리거
            var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty,
                new SolidColorBrush(Color.FromArgb(50, 0, 120, 215))));
            template.Triggers.Add(hoverTrigger);

            return template;
        }


        // 버튼 클릭 핸들러들
        private void OnCompleteButtonClick(object sender, RoutedEventArgs e)
        {
            _log?.Info("완료 버튼 클릭됨");
            CompleteRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void OnUndoButtonClick(object sender, RoutedEventArgs e)
        {
            _log?.Info("Undo 버튼 클릭됨");
            UndoRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void OnCancelButtonClick(object sender, RoutedEventArgs e)
        {
            _log?.Info("취소 버튼 클릭됨");
            CancelRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
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
                var removed = _geoPoints.Last();
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
        /// 모든 포인트 클리어
        /// </summary>
        public void Clear()
        {
            _geoPoints.Clear();
            _currentMousePosition = null;
            _controlBorder.Visibility = Visibility.Collapsed;
            InvalidateVisual();

            _log?.Info("모든 포인트 제거");
        }

        /// <summary>
        /// 펜 스타일 설정
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
                // 첫 번째 포인트 근처에 컨트롤 표시
                var firstScreenPoint = ConvertToScreenPoint(_geoPoints[0]);
                Canvas.SetLeft(_controlCanvas.Children[0], firstScreenPoint.X + 20);
                Canvas.SetTop(_controlCanvas.Children[0], firstScreenPoint.Y - 50);

                _controlBorder.Visibility = Visibility.Visible;

                // 상태 텍스트 업데이트
                UpdateStatusText();

                // 버튼 활성화 상태 업데이트
                _completeButton.IsEnabled = _geoPoints.Count >= 2;
                _undoButton.IsEnabled = _geoPoints.Count > 0;
            }
            else
            {
                _controlBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateStatusText()
        {
            if (_geoPoints.Count == 0)
            {
                _statusText.Text = "클릭하여 시작";
            }
            else if (_geoPoints.Count == 1)
            {
                _statusText.Text = "1개 점";
            }
            else
            {
                var distance = CalculateTotalDistance();
                _statusText.Text = $"{_geoPoints.Count}개 점 | {distance:F1}m";
            }
        }

        private double CalculateTotalDistance()
        {
            double distance = 0;
            for (int i = 1; i < _geoPoints.Count; i++)
            {
                distance += _mapControl.MapProvider.Projection.GetDistance(
                    _geoPoints[i - 1], _geoPoints[i]) * 1000;
            }
            return distance;
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
        /// 총 거리 (미터)
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

            try
            {
                // 지리 좌표를 화면 좌표로 변환
                var screenPoints = ConvertToScreenPoints(_geoPoints);
                if (screenPoints.Count == 0) return;

                // 1. 확정된 라인 그리기
                if (screenPoints.Count >= 2)
                {
                    for (int i = 1; i < screenPoints.Count; i++)
                    {
                        drawingContext.DrawLine(_linePen, screenPoints[i - 1], screenPoints[i]);
                    }
                }

                // 2. 미리보기 라인 그리기
                if (_currentMousePosition.HasValue && screenPoints.Count > 0)
                {
                    drawingContext.DrawLine(_previewPen,
                        screenPoints[screenPoints.Count - 1],
                        _currentMousePosition.Value);
                }

                // 3. 각 포인트에 원 그리기
                foreach (var point in screenPoints)
                {
                    drawingContext.DrawEllipse(_pointFill, _pointPen, point, _pointRadius, _pointRadius);
                }

                // 4. 시작점과 끝점 강조
                if (screenPoints.Count > 0)
                {
                    // 시작점 - 녹색
                    drawingContext.DrawEllipse(Brushes.LightGreen,
                        new Pen(Brushes.Green, 2),
                        screenPoints[0], _pointRadius + 2, _pointRadius + 2);

                    // 끝점 - 주황색 (2개 이상일 때)
                    if (screenPoints.Count >= 2)
                    {
                        drawingContext.DrawEllipse(Brushes.Orange,
                            new Pen(Brushes.DarkOrange, 2),
                            screenPoints[screenPoints.Count - 1], _pointRadius + 2, _pointRadius + 2);
                    }
                }

                // 5. 거리 정보 표시 (옵션)
                //if (screenPoints.Count >= 2)
                //{
                //    var distance = TotalDistance;
                //    var text = new FormattedText(
                //        $"{distance:F1}m",
                //        System.Globalization.CultureInfo.CurrentCulture,
                //        FlowDirection.LeftToRight,
                //        new Typeface("Arial"),
                //        12,
                //        Brushes.Red,
                //        1.0);

                //    var lastPoint = screenPoints[screenPoints.Count - 1];
                //    drawingContext.DrawText(text, new Point(lastPoint.X + 10, lastPoint.Y - 20));
                //}
            }
            catch (Exception ex)
            {
                _log?.Error($"Adorner 렌더링 오류: {ex.Message}");
            }
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
        /// 지도 변경 이벤트 핸들러 (이동/줌)
        /// </summary>
        private void OnMapChanged()
        {
            UpdateControlUI();
            InvalidateVisual();
        }

        #endregion
        #region Override Layout Methods

        // MeasureOverride 추가
        protected override Size MeasureOverride(Size availableSize)
        {
            _controlCanvas?.Measure(availableSize);
            return base.MeasureOverride(availableSize);
        }

        // HitTest 오버라이드 추가
        protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
        {
            // 컨트롤 영역 체크
            if (_controlBorder?.Visibility == Visibility.Visible)
            {
                var point = hitTestParameters.HitPoint;
                var borderBounds = new Rect(
                    Canvas.GetLeft(_controlBorder),
                    Canvas.GetTop(_controlBorder),
                    _controlBorder.ActualWidth,
                    _controlBorder.ActualHeight);

                if (borderBounds.Contains(point))
                {
                    return new PointHitTestResult(this, point);
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
            // 버튼 이벤트 핸들러 해제
            if (_completeButton != null)
            {
                _completeButton.Click -= OnCompleteButtonClick;
            }
            if (_undoButton != null)
            {
                _undoButton.Click -= OnUndoButtonClick;
            }
            if (_cancelButton != null)
            {
                _cancelButton.Click -= OnCancelButtonClick;
            }

            // 이벤트 구독 해제
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