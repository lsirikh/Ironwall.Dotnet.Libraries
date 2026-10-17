using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Shapes;
using GMap.NET;

using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/23/2025 10:29:03 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class GMapMarkerPidsGroupControl : GMapMarkerBaseControl<GMapPidsGroupMarker>
    {
        #region Static Constructor
        static GMapMarkerPidsGroupControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapMarkerPidsGroupControl),
                new FrameworkPropertyMetadata(typeof(GMapMarkerPidsGroupControl)));
        }

        public GMapMarkerPidsGroupControl()
        {
            
        }

        /// <summary>
        /// GMapPidsGroupMarker 함께 생성하는 생성자
        /// </summary>
        /// <param name="groupMarker">연결할 라인 마커</param>
        public GMapMarkerPidsGroupControl(GMapPidsGroupMarker groupMarker) : base(groupMarker)
        {
            // 기본 클래스에서 UpdateFromMarker(), SetupDataBindings() 호출됨
            Loaded += OnControlLoaded;
            Unloaded += OnControlUnloaded;
        }
        #endregion
        #region - Ctors -
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        private void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("=== GMapMarkerPidsGroupControl Loaded ===");

            // Visual Tree가 완성된 후 MapControl 찾기
            _mapControl = FindParentMapControl();

            if (_mapControl != null)
            {
                //System.Diagnostics.Debug.WriteLine($"MapControl 찾음: {_mapControl.GetType().Name}");

                // 지도 이벤트 구독
                _mapControl.OnMapZoomChanged += OnMapChanged;
                _mapControl.OnMapDrag += OnMapChanged;
                _mapControl.OnPositionChanged += OnMapPositionChanged;
                // [FR-C4] 창 리사이즈/모니터 이동 시 폴리라인 픽셀캐시 stale → 재계산(줌/팬 없이도). 2026-07-15 멀티모니터 소실.
                _mapControl.SizeChanged += OnMapSizeChanged;
                // 회전 통지(R-14) — 회전만 변경 시에도 정점 재투영. Unloaded서 해제
                (_mapControl as GMapCustoms.GMapCustomControl)?.SubscribeViewport(OnViewportSnapshot);

                // 초기 라인 그리기
                UpdateLineGeometry();
            }
            else
            {
                //System.Diagnostics.Debug.WriteLine("MapControl을 찾을 수 없음!");
            }

            // IsVisible 변경 감지: Visibility=false 후 true로 복원 시 기하 재계산
            if (Marker != null)
                Marker.PropertyChanged += OnMarkerPropertyChanged;

            UpdateActionReportCursor();   // 로드 시점에 이미 이벤트 중인 구역이면 손가락 커서 (FR-06)
        }

        private void OnControlUnloaded(object sender, RoutedEventArgs e)
        {
            if (_mapControl != null)
            {
                _mapControl.OnMapZoomChanged -= OnMapChanged;
                _mapControl.OnMapDrag -= OnMapChanged;
                _mapControl.OnPositionChanged -= OnMapPositionChanged;
                _mapControl.SizeChanged -= OnMapSizeChanged;
                (_mapControl as GMapCustoms.GMapCustomControl)?.UnsubscribeViewport(OnViewportSnapshot);
            }

            if (Marker != null)
                Marker.PropertyChanged -= OnMarkerPropertyChanged;
        }

        private void OnMarkerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_mapControl == null) return;

            // 점 변경(리사이즈 스케일·Undo)에 폴리라인/하위오버레이 재계산 — 이전엔 IsVisible만 반응해
            // ApplyGeometry로 점을 바꿔도 미갱신(화면 stale, 창 전환 전까지 안 보임·Undo 후 어긋남). LineArea_Symbol_Resize 수정.
            if (e.PropertyName == nameof(GMapPidsGroupMarker.RuntimePoints)
                || e.PropertyName == nameof(GMapPidsGroupMarker.LinePoints))
            {
                Dispatcher.InvokeAsync(UpdateLineGeometry, System.Windows.Threading.DispatcherPriority.Render);
                return;
            }

            // 이벤트 상태 전이(탐지/장애 발생·해제) → 손가락 커서 On/Off (FR-06)
            if (e.PropertyName == nameof(GMapPidsGroupMarker.CompositeStatus))
            {
                Dispatcher.InvokeAsync(UpdateActionReportCursor);
                return;
            }

            // Visibility가 false→true로 바뀔 때 형상 재계산(RestoreLayerVisibility 이후 stale 복구).
            if (e.PropertyName == nameof(GMapPidsGroupMarker.IsVisible) && Marker?.IsVisible == true)
            {
                Dispatcher.InvokeAsync(UpdateLineGeometry, System.Windows.Threading.DispatcherPriority.Render);
            }
        }

        private void OnMapChanged()
        {
            //System.Diagnostics.Debug.WriteLine("지도 변경 감지 - 라인 업데이트");
            UpdateLineGeometry();
        }

        /// <summary>뷰포트(회전) snapshot 수신 — 정점 재투영(회전 포함, R-14). base −θ 추가 금지(R-36).</summary>
        private void OnViewportSnapshot(GMapCustoms.MapViewportSnapshot _) => OnMapChanged();

        /// <summary>[Rotation R-36] 정점 재투영 계열 — RenderTransform −θ 미적용(이중회전 방지).</summary>
        public override bool AppliesMapRotation => false;

        private void OnMapPositionChanged(PointLatLng point)
        {
            UpdateLineGeometry();
        }

        /// <summary>[FR-C4] 맵 크기 변경(창 리사이즈·최대화·모니터 이동 반영) → 폴리라인 재계산(레이아웃 안정 후).</summary>
        private void OnMapSizeChanged(object sender, SizeChangedEventArgs e)
            => Dispatcher.InvokeAsync(UpdateLineGeometry, System.Windows.Threading.DispatcherPriority.Render);

        /// <summary>[FR-C4] 모니터 간 이동 등 DPI 변경 → 픽셀 좌표계 변화 반영(줌/팬 없이도 재계산).</summary>
        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            Dispatcher.InvokeAsync(UpdateLineGeometry, System.Windows.Threading.DispatcherPriority.Render);
        }

        /// <summary>
        /// GMapLineMarker 전용 UI 업데이트 구현
        /// </summary>
        protected override void UpdateFromSpecificMarker()
        {
            if (Marker == null) return;

            // GMapLineMarker 전용 속성 동기화 (타입 안전)
            LinePattern = Marker.LinePattern;
            LineOpacity = Marker.LineOpacity;
            IsClosedPath = Marker.IsClosedPath;
            ShowArrowHead = Marker.ShowArrowHead;
            CompositeStatus = Marker.CompositeStatus;

            // Polyline 속성 강제 업데이트 (중요!)
            if (MainPolyline != null)
            {
                MainPolyline.Stroke = ColorHelper.ToBrush(Marker.StrokeColor);
                MainPolyline.StrokeThickness = Marker.StrokeThickness;
                MainPolyline.Opacity = Marker.LineOpacity;
            }

            // 라인 심볼 전용 모양 업데이트
            UpdateLineGeometry();
        }

        /// <summary>
        /// GMapLineMarker 전용 바인딩 설정 구현
        /// </summary>
        protected override void SetupSpecificBindings()
        {
            if (Marker == null) return;


            // GMapLineMarker 전용 바인딩 (타입 안전)
            SetupPropertyBinding(LinePatternProperty, nameof(Marker.LinePattern));
            SetupPropertyBinding(LineOpacityProperty, nameof(Marker.LineOpacity));
            SetupPropertyBinding(IsClosedPathProperty, nameof(Marker.IsClosedPath));
            SetupPropertyBinding(ShowArrowHeadProperty, nameof(Marker.ShowArrowHead));
            SetupPropertyBinding(CompositeStatusProperty, nameof(Marker.CompositeStatus));

            // Polyline에 직접 바인딩 추가
            if (MainPolyline != null)
            {
                var strokeBinding = new Binding(nameof(Marker.StrokeColor))
                {
                    Source = Marker,
                    Mode = BindingMode.OneWay,
                    Converter = new ColorTypeToBrushConverter()
                };
                MainPolyline.SetBinding(Polyline.StrokeProperty, strokeBinding);

                var thicknessBinding = new Binding(nameof(Marker.StrokeThickness))
                {
                    Source = Marker,
                    Mode = BindingMode.OneWay
                };
                MainPolyline.SetBinding(Polyline.StrokeThicknessProperty, thicknessBinding);
            }
        }

        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        /// <summary>
        /// 컨트롤 초기화 완료 후 호출 오버라이드
        /// </summary>
        protected override void OnControlInitialized()
        {
            base.OnControlInitialized();

            // Polyline이 있으면 속성 동기화
            if (MainPolyline != null && Marker != null)
            {
                MainPolyline.Stroke = ColorHelper.ToBrush(Marker.StrokeColor);
                MainPolyline.StrokeThickness = Marker.StrokeThickness;
                MainPolyline.Opacity = Marker.LineOpacity;
            }
            //System.Diagnostics.Debug.WriteLine("GMapMarkerLineControl 초기화 완료");
        }

        /// <summary>
        /// 마커 모양 업데이트 오버라이드 (LinePattern 고려)
        /// </summary>
        protected override void UpdateMarkerAppearance()
        {
            // 기본 색상 설정 먼저
            base.UpdateMarkerAppearance();

            // 라인 심볼 전용 모양 업데이트
            UpdateLineAppearance();
        }

        /// <summary>
        /// 템플릿 적용 시 호출
        /// </summary>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // 템플릿에서 UI 요소 찾기
            _lineCanvas = GetTemplateChild("PART_LineCanvas") as Canvas;
            MainPolyline = GetTemplateChild("PART_MainPolyline") as Polyline;
            EventPolyline = GetTemplateChild("PART_EventPolyline") as Polyline;
            DetectionPolyline = GetTemplateChild("PART_DetectionPolyline") as Polyline;

            // 초기값 설정 및 바인딩 복구
            if (MainPolyline != null && Marker != null)
            {
                MainPolyline.Stroke = ColorHelper.ToBrush(Marker.StrokeColor);
                MainPolyline.StrokeThickness = Marker.StrokeThickness;
                MainPolyline.Opacity = Marker.LineOpacity;
            }

            // EventPolyline (FaultOverlay) — 두께를 Marker.StrokeThickness에 바인딩(로컬 할당은 변경추적을 덮음).
            //   (사용자 지적: 탐지/장애 깜빡임 선 두께가 심볼 두께를 안 따르던 버그 — 기존엔 MainPolyline만 갱신)
            if (EventPolyline != null && Marker != null)
            {
                EventPolyline.SetBinding(Polyline.StrokeThicknessProperty,
                    new Binding(nameof(Marker.StrokeThickness)) { Source = Marker, Mode = BindingMode.OneWay });
                EventPolyline.Opacity = Marker.LineOpacity;
            }

            // DetectionPolyline (DetectionOverlay) — 동일하게 두께 바인딩(깜빡임 선도 심볼 두께 반영)
            if (DetectionPolyline != null && Marker != null)
            {
                DetectionPolyline.SetBinding(Polyline.StrokeThicknessProperty,
                    new Binding(nameof(Marker.StrokeThickness)) { Source = Marker, Mode = BindingMode.OneWay });
                DetectionPolyline.Opacity = Marker.LineOpacity;
            }

            //System.Diagnostics.Debug.WriteLine($"Template 적용: Canvas={_lineCanvas != null}, MainPolyline={MainPolyline != null}, EventPolyline={EventPolyline != null}, DetectionPolyline={DetectionPolyline != null}");
        }

        /// <summary>
        /// 단일 클릭 처리 오버라이드 (라인 심볼 전용 로직)
        /// </summary>
        protected override void OnMarkerSingleClicked(MouseButtonEventArgs e)
        {
            base.OnMarkerSingleClicked(e);
        }

        /// <summary>
        /// 더블클릭 처리 오버라이드 (편집 모드 토글 등)
        /// </summary>
        protected override void OnMarkerDoubleClicked(MouseButtonEventArgs e)
        {
            base.OnMarkerDoubleClicked(e);
        }

        /// <summary>
        /// 클릭으로 선택과 비선택에 따른 이벤트 콜백
        /// </summary>
        protected override void OnSelectionChanged(bool isSelected)
        {
            base.OnSelectionChanged(isSelected);
        }
        #endregion

        #region - 그룹 조치보고 더블클릭 (GMap_PidsGroup_DoubleClick_ActionReport) -
        /// <summary>
        /// 이벤트가 살아있는 구역인지 — 탐지 깜빡임 / 장애 색상 / 장애+탐지 공존 / 제어기 무통신(검정). (FR-02)
        /// 정상·연결 상태는 조치할 이벤트가 없으므로 더블클릭을 받지 않는다.
        /// </summary>
        private bool IsEventActive()
            => Marker?.CompositeStatus is EnumCompositeEventStatus.Detecting
                                       or EnumCompositeEventStatus.Faulted
                                       or EnumCompositeEventStatus.FaultedDetecting
                                       or EnumCompositeEventStatus.Blackout;

        /// <summary>
        /// 라인 위 좌클릭. 이벤트가 살아있는 구역을 더블클릭하면 그룹 조치보고를 요청한다. (FR-01/FR-09)
        /// <para><b>왜 여기서 받나</b>: 템플릿 루트 <c>PART_LineCanvas</c>에 Background가 없어 WPF는
        /// <b>폴리라인이 실제로 그려진 곳에서만</b> 히트한다 → 별도 거리 계산 없이 기하학적으로 정확하다.
        /// 부모의 AABB 스캔(<c>GMapCustomControl.GetMarkerAtScreen</c>)은 구역의 화면 bbox 전체를 클릭
        /// 영역으로 잡고(<c>Width=Max(40, bbox+10)</c>) 잠금 심볼을 아예 제외하므로 이 기능엔 쓰지 않는다.</para>
        /// <para>편집 모드에서는 진입하지 않는다 — 편집 중 더블클릭은 기존 편집 동작이 우선.</para>
        /// </summary>
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2
                && Marker != null
                && _mapControl != null
                && !_mapControl.IsEditMode
                && IsEventActive())
            {
                _mapControl.TriggerGroupActionReport(Marker);
                e.Handled = true;   // base 미호출 = 팬 Armed·선택 토글 억제
                return;
            }

            base.OnMouseLeftButtonDown(e);
        }

        /// <summary>이벤트 활성 구역이면 라인 위에서 손가락 커서. (FR-06)
        /// Canvas에 배경이 없으므로 커서도 폴리라인 위에서만 바뀐다. 편집 모드는 기존 드래그 커서 유지.</summary>
        private void UpdateActionReportCursor()
        {
            var active = IsEventActive() && _mapControl is { IsEditMode: false };
            Cursor = active ? Cursors.Hand : null;   // null = 상위 커서 상속(원복)
        }

        #endregion

        #region Line Control Specific Methods

        /// <summary>
        /// 라인 컨트롤 초기화 완료 후 호출 (가상 메서드)
        /// </summary>
        protected virtual void OnLineControlInitialized()
        {
            // 상속 클래스에서 구현 가능
        }

        /// <summary>
        /// 라인 심볼 전용 모양 업데이트
        /// </summary>
        protected virtual void UpdateLineAppearance()
        {
            if (Marker == null || _isUpdatingFromMarker) return;

            // LinePattern에 따른 스타일 적용
            if (MainPolyline != null)
            {
                ApplyLinePattern(MainPolyline, LinePattern);
            }
        }


        /// <summary>
        /// 라인 기하학 업데이트 (개선된 버전)
        /// </summary>
        /// <summary>파생 컨트롤(3D 철망)이 기하 재계산을 요청할 때 쓴다.</summary>
        protected void RefreshLineGeometry() => UpdateLineGeometry();

        /// <summary>
        /// 라인 기하가 갱신된 직후 호출되는 훅(FR-07) — <paramref name="localPoints"/> 는 컨트롤 좌상단 기준 로컬 px(닫힘 중복점 없음),
        /// <paramref name="origin"/> 은 그 원점의 지도 화면 좌표. 2D 컨트롤은 아무것도 하지 않는다.
        /// </summary>
        protected virtual void OnLineGeometryUpdated(IReadOnlyList<Point> localPoints, Point origin) { }

        private void UpdateLineGeometry()
        {
            if (Marker == null || MainPolyline == null || _mapControl == null)
                return;

            try
            {
                var points = Marker.RuntimePoints;
                if (points == null || points.Count < 2)
                {
                    MainPolyline.Points = new PointCollection();
                    OnLineGeometryUpdated(Array.Empty<Point>(), default);
                    return;
                }

                // 1. 먼저 모든 포인트의 화면 좌표 계산
                var screenPoints = new List<Point>();
                foreach (var geoPoint in points)
                {
                    var gPoint = _mapControl.FromLatLngToLocal(geoPoint);
                    var screenPoint = new Point(gPoint.X, gPoint.Y);
                    screenPoints.Add(screenPoint);
                }

                // 2. 경계 박스 계산
                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;

                foreach (var point in screenPoints)
                {
                    minX = Math.Min(minX, point.X);
                    minY = Math.Min(minY, point.Y);
                    maxX = Math.Max(maxX, point.X);
                    maxY = Math.Max(maxY, point.Y);
                }

                // 3. 실제 필요한 크기 계산
                double actualWidth = maxX - minX;
                double actualHeight = maxY - minY;


                // 최소 크기와 여백 추가
                const double padding = 5;
                const double minSize = 40;

                // 실제 크기 + 여백, 최소 100 보장
                Width = Math.Max(minSize, actualWidth + padding * 2);
                Height = Math.Max(minSize, actualHeight + padding * 2);

                // 4. 컨트롤 원점(좌상단)을 폴리라인 bbox에 고정 — Adorner 박스 정합 (Adorner_Box_Mismatch_Fix)
                //
                //    종전: 점을 'Position(중심) 기준'으로 배치하고 Offset은 GMapBaseMarker.UpdateOffset()이
                //          _model.Width/Height(생성 시점 스냅샷, 여백 ±50·최소 100)로 계산했다.
                //          컨트롤 실제 크기는 여기서 매 줌마다 '화면 bbox + 여백 ±5·최소 40'으로 다시 잡히므로
                //          둘이 서로 다른 소스라 어긋났고, 정점 편집으로 Position이 bbox 중심을 벗어나면
                //          폴리라인이 컨트롤 안에서 치우쳐 사각형 밖으로 삐져나왔다.
                //          MarkerEditAdorner(파란 점선)·GroupSelectionAdorner(하늘색 점선)·GetMarkerAtScreen(AABB)이
                //          전부 이 컨트롤 사각형을 쓰기 때문에 박스와 히트영역이 같이 틀어졌다.
                //
                //    수정: 원점을 bbox에서 직접 구하고 Offset을 (원점 − Position화면좌표)로 세팅한다.
                //          → 컨트롤 사각형이 항상 선을 padding 여백으로 정확히 감싸고, 폴리라인은 제 지리위치에 그려진다.
                //          Position이 bbox 중심이 아니어도 성립한다(정점 편집 후 중심 재계산 불필요).
                //          최소크기(40) 보정으로 남는 여백은 상하/좌우 균등 분배.
                //          (GMapMarkerImageControl 이 Marker.Offset 을 화면 크기로 세팅하는 것과 동일 패턴)
                double originX = minX - (Width - actualWidth) / 2.0;
                double originY = minY - (Height - actualHeight) / 2.0;

                var centerGPoint = _mapControl.FromLatLngToLocal(Marker.Position);
                var newOffset = new Point(originX - centerGPoint.X, originY - centerGPoint.Y);
                if (Marker.Offset != newOffset)
                    Marker.Offset = newOffset;   // 팬 중에는 origin·center가 같이 이동해 값이 유지됨(불필요 재배치 없음)

                var pointCollection = new PointCollection();
                var localPoints = new List<Point>(screenPoints.Count);

                foreach (var screenPoint in screenPoints)
                {
                    // 컨트롤 좌상단(원점) 기준 로컬 좌표 — 화면상 실제 위치와 1:1
                    var local = new Point(screenPoint.X - originX, screenPoint.Y - originY);
                    pointCollection.Add(local);
                    localPoints.Add(local);
                }

                // 닫힌 경로 처리
                if (IsClosedPath && pointCollection.Count > 2)
                {
                    pointCollection.Add(pointCollection[0]);
                }

                MainPolyline.Points = pointCollection;

                // FaultOverlay / DetectionOverlay에도 동일한 Points 적용
                if (EventPolyline != null)
                    EventPolyline.Points = pointCollection;

                if (DetectionPolyline != null)
                    DetectionPolyline.Points = pointCollection;

                // Canvas 크기도 업데이트
                if (_lineCanvas != null)
                {
                    _lineCanvas.Width = Width;
                    _lineCanvas.Height = Height;
                }

                OnLineGeometryUpdated(localPoints, new Point(originX, originY));   // 3D 철망 훅(FR-07)
            }
            catch (Exception ex)
            {
                // [FR-C4] 무음 삼킴 금지 — 현장 진단 불가였던 결함(2026-07-15 분석 §6.2). 기하 실패는 화면 소실로 직결.
                _log?.Warning($"[PidsGroup] UpdateLineGeometry 실패 '{Marker?.Title}': {ex.Message}");
            }
        }


        /// <summary>
        /// 라인 패턴 적용
        /// </summary>
        private void ApplyLinePattern(Polyline polyline, EnumLinePattern pattern)
        {
            if (polyline == null) return;

            switch (pattern)
            {
                case EnumLinePattern.Solid:
                    polyline.StrokeDashArray = null;
                    break;
                case EnumLinePattern.Dashed:
                    polyline.StrokeDashArray = new DoubleCollection { 10, 5 };
                    break;
                case EnumLinePattern.Dotted:
                    polyline.StrokeDashArray = new DoubleCollection { 2, 3 };
                    break;
                case EnumLinePattern.DashDot:
                    polyline.StrokeDashArray = new DoubleCollection { 10, 3, 2, 3 };
                    break;
            }
        }


        #endregion
        #region Static Property Changed Callbacks

        /// <summary>
        /// LinePattern 변경 시 호출
        /// </summary>
        protected static void OnLinePatternChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapMarkerPidsGroupControl control && control.Marker != null)
            {
                // UI 업데이트
                control.UpdateLineAppearance();

            }
        }

        /// <summary>
        /// 투명도 변경 시 호출
        /// </summary>
        protected static void OnLineOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapMarkerPidsGroupControl control && control.Marker != null)
            {
                // _mainPolyline이 아직 초기화되지 않았으면 리턴
                if (control.MainPolyline == null) return;

                // UI 투명도 적용
                control.MainPolyline.Opacity = (double)e.NewValue;
            }
        }

        /// <summary>
        /// 닫힌 경로 변경 시 호출
        /// </summary>
        protected static void OnIsClosedPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapMarkerPidsGroupControl control && control.Marker != null)
            {
                // UI 업데이트
                control.UpdateLineGeometry();

            }
        }

        /// <summary>
        /// 화살표 표시 변경 시 호출
        /// </summary>
        protected static void OnShowArrowHeadChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapMarkerPidsGroupControl control && control.Marker != null)
            {
                // UI 업데이트
                control.UpdateLineAppearance();

            }
        }

        /// <summary>
        /// 드로잉 상태 변경 시 호출
        /// </summary>
        protected static void OnIsDrawingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapMarkerPidsGroupControl control && control.Marker != null)
            {
                // UI 업데이트
                control.UpdateLineGeometry();
            }
        }

        /// <summary>
        /// 이벤트 상태 변경 시 호출 (애니메이션 트리거 핵심)
        /// </summary>
        protected static void OnEventStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GMapMarkerPidsGroupControl control && control.Marker != null)
            {
                // UI 색상 즉시 업데이트
                control.UpdateMarkerAppearance();

                // 마커 데이터와 동기화
                if (control.Marker != null && control.Marker.EventStatus != (EnumEventStatus)e.NewValue)
                {
                    control.Marker.EventStatus = (EnumEventStatus)e.NewValue;
                }

                //System.Diagnostics.Debug.WriteLine($"EventStatus 변경: {e.OldValue} → {e.NewValue}");
            }
        }

        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        /// <summary>
        /// 라인 패턴 타입
        /// </summary>
        public EnumLinePattern LinePattern
        {
            get { return (EnumLinePattern)GetValue(LinePatternProperty); }
            set { SetValue(LinePatternProperty, value); }
        }

        public static readonly DependencyProperty LinePatternProperty =
            DependencyProperty.Register("LinePattern", typeof(EnumLinePattern), typeof(GMapMarkerPidsGroupControl),
                new PropertyMetadata(EnumLinePattern.Solid, OnLinePatternChanged));

        /// <summary>
        /// 라인 투명도 (0.0 ~ 1.0)
        /// </summary>
        public double LineOpacity
        {
            get { return (double)GetValue(LineOpacityProperty); }
            set { SetValue(LineOpacityProperty, value); }
        }

        public static readonly DependencyProperty LineOpacityProperty =
            DependencyProperty.Register("LineOpacity", typeof(double), typeof(GMapMarkerPidsGroupControl),
                new PropertyMetadata(1.0, OnLineOpacityChanged));

        /// <summary>
        /// 닫힌 경로 여부
        /// </summary>
        public bool IsClosedPath
        {
            get { return (bool)GetValue(IsClosedPathProperty); }
            set { SetValue(IsClosedPathProperty, value); }
        }

        public static readonly DependencyProperty IsClosedPathProperty =
            DependencyProperty.Register("IsClosedPath", typeof(bool), typeof(GMapMarkerPidsGroupControl),
                new PropertyMetadata(false, OnIsClosedPathChanged));

        /// <summary>
        /// 화살표 표시 여부
        /// </summary>
        public bool ShowArrowHead
        {
            get { return (bool)GetValue(ShowArrowHeadProperty); }
            set { SetValue(ShowArrowHeadProperty, value); }
        }

        public static readonly DependencyProperty ShowArrowHeadProperty =
            DependencyProperty.Register("ShowArrowHead", typeof(bool), typeof(GMapMarkerPidsGroupControl),
                new PropertyMetadata(false, OnShowArrowHeadChanged));


        /// <summary>
        /// 이벤트 상태 (애니메이션 트리거)
        /// </summary>
        public EnumEventStatus EventStatus
        {
            get { return (EnumEventStatus)GetValue(EventStatusProperty); }
            set { SetValue(EventStatusProperty, value); }
        }

        public static readonly DependencyProperty EventStatusProperty =
            DependencyProperty.Register("EventStatus", typeof(EnumEventStatus), typeof(GMapMarkerPidsGroupControl),
                new PropertyMetadata(EnumEventStatus.Normal, OnEventStatusChanged));

        public EnumCompositeEventStatus CompositeStatus
        {
            get { return (EnumCompositeEventStatus)GetValue(CompositeStatusProperty); }
            set { SetValue(CompositeStatusProperty, value); }
        }

        public static readonly DependencyProperty CompositeStatusProperty =
            DependencyProperty.Register("CompositeStatus", typeof(EnumCompositeEventStatus), typeof(GMapMarkerPidsGroupControl),
                new PropertyMetadata(EnumCompositeEventStatus.Normal));

        public Polyline? MainPolyline { get; private set; }

        /// <summary>
        /// FaultOverlay Polyline (Faulted / FaultedDetecting 상태에서 표시)
        /// </summary>
        public Polyline? EventPolyline { get; private set; }

        /// <summary>
        /// DetectionOverlay Polyline (Detecting / FaultedDetecting 상태에서 표시)
        /// </summary>
        public Polyline? DetectionPolyline { get; private set; }

        // Adorner에서 접근할 수 있도록 Public 속성 추가
        public PointCollection LinePoints => MainPolyline?.Points ?? new PointCollection();

        // 라인의 실제 경계를 반환하는 속성 추가
        public Rect ActualLineBounds
        {
            get
            {
                if (MainPolyline?.Points == null || MainPolyline.Points.Count < 2)
                    return new Rect(0, 0, Width, Height);

                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;

                foreach (var point in MainPolyline.Points)
                {
                    minX = Math.Min(minX, point.X);
                    minY = Math.Min(minY, point.Y);
                    maxX = Math.Max(maxX, point.X);
                    maxY = Math.Max(maxY, point.Y);
                }

                return new Rect(minX, minY, maxX - minX, maxY - minY);
            }
        }
        #endregion
        #region - Attributes -
        private Canvas? _lineCanvas;
        private GMapCustomControl? _mapControl;
        /// <summary>부모 지도(Loaded 후 확보) — 파생 컨트롤의 m/px·줌 산출용.</summary>
        protected GMapCustomControl? MapControl => _mapControl;
        #endregion
    }
}