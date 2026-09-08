using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties
{
    /****************************************************************************
       Purpose      : PidsGroup 마커의 속성을 제어하는 컨트롤                                                          
       Created By   : GHLee                                                
       Created On   : 9/23/2025                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class GMapPropertyPidsGroupControl : GMapPropertyBaseControl
    {
        #region - Ctors -
        static GMapPropertyPidsGroupControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapPropertyPidsGroupControl),
               new FrameworkPropertyMetadata(typeof(GMapPropertyPidsGroupControl)));
        }

        public GMapPropertyPidsGroupControl()
        {
            MarkerSizeEnabled = false;  // LineSymbol과 동일하게 사이즈 조정 비활성화
            Is3DFeatureEnabled = Utils.Symbol3DFeature.IsEnabled;
            FencePostSpacingDefault = Utils.Symbol3DFeature.FencePostSpacingM;
            FenceHeightDefault = Utils.Symbol3DFeature.FenceHeightM;
            _spacingCommit = new DeferredCommit<double>((first, last) => CommitFence("PostSpacingM", first, last, m => m.PostSpacingM = last));
            _heightCommit = new DeferredCommit<double>((first, last) => CommitFence("FenceHeightM", first, last, m => m.FenceHeightM = last));
            ResetPostSpacingCommand = new PanelCommand(_ => ResetPostSpacing());
            ResetFenceHeightCommand = new PanelCommand(_ => ResetFenceHeight());
            SetPostSpacingPresetCommand = new PanelCommand(p => { if (p is not null && double.TryParse(p.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)) PostSpacingM = v; });
            Unloaded += (_, _) => { FlushDeferredCommits(); UnsubscribeFenceNotifier(); };
        }
        #endregion

        #region - 3D 철망(FR-03/04/06, D1 슬라이더 150ms 지연 커밋) -
        private readonly DeferredCommit<double> _spacingCommit;
        private readonly DeferredCommit<double> _heightCommit;
        private bool _syncingFence;   // 마커→패널 동기화 중(슬라이더 콜백이 다시 마커에 쓰지 않도록)

        public bool Is3DFeatureEnabled { get => (bool)GetValue(Is3DFeatureEnabledProperty); set => SetValue(Is3DFeatureEnabledProperty, value); }
        public static readonly DependencyProperty Is3DFeatureEnabledProperty = DependencyProperty.Register(nameof(Is3DFeatureEnabled), typeof(bool), typeof(GMapPropertyPidsGroupControl), new PropertyMetadata(false));
        public double FencePostSpacingDefault { get => (double)GetValue(FencePostSpacingDefaultProperty); set => SetValue(FencePostSpacingDefaultProperty, value); }
        public static readonly DependencyProperty FencePostSpacingDefaultProperty = DependencyProperty.Register(nameof(FencePostSpacingDefault), typeof(double), typeof(GMapPropertyPidsGroupControl), new PropertyMetadata(Helpers.Fence.FenceDefaults.PostSpacingM));
        public double FenceHeightDefault { get => (double)GetValue(FenceHeightDefaultProperty); set => SetValue(FenceHeightDefaultProperty, value); }
        public static readonly DependencyProperty FenceHeightDefaultProperty = DependencyProperty.Register(nameof(FenceHeightDefault), typeof(double), typeof(GMapPropertyPidsGroupControl), new PropertyMetadata(Helpers.Fence.FenceDefaults.FenceHeightM));

        /// <summary>그룹별 3D 렌더 on/off — TwoWay 바인딩(즉시 커밋).</summary>
        public bool Render3D { get => (bool)GetValue(Render3DProperty); set => SetValue(Render3DProperty, value); }
        public static readonly DependencyProperty Render3DProperty = DependencyProperty.Register(nameof(Render3D), typeof(bool), typeof(GMapPropertyPidsGroupControl),
            new PropertyMetadata(true, (d, e) => ((GMapPropertyPidsGroupControl)d).OnFenceFieldChanged("Render3D", e, m => m.Render3D = (bool)e.NewValue)));

        /// <summary>철망 형태(기둥 간격/센서 장착) — TwoWay 바인딩(즉시 커밋).</summary>
        public EnumFenceMode FenceMode { get => (EnumFenceMode)GetValue(FenceModeProperty); set => SetValue(FenceModeProperty, value); }
        public static readonly DependencyProperty FenceModeProperty = DependencyProperty.Register(nameof(FenceMode), typeof(EnumFenceMode), typeof(GMapPropertyPidsGroupControl),
            new PropertyMetadata(EnumFenceMode.Posts, (d, e) => ((GMapPropertyPidsGroupControl)d).OnFenceFieldChanged("FenceMode", e, m => m.FenceMode = (EnumFenceMode)e.NewValue)));

        /// <summary>기둥 간격(m) 슬라이더 값 — 마커 NULL 이면 전역 기본을 보여주고(IsPostSpacingInherited) 사용자가 움직이면 150ms 뒤 마커에 커밋.</summary>
        public double PostSpacingM { get => (double)GetValue(PostSpacingMProperty); set => SetValue(PostSpacingMProperty, value); }
        public static readonly DependencyProperty PostSpacingMProperty = DependencyProperty.Register(nameof(PostSpacingM), typeof(double), typeof(GMapPropertyPidsGroupControl),
            new PropertyMetadata(Helpers.Fence.FenceDefaults.PostSpacingM, (d, e) => ((GMapPropertyPidsGroupControl)d).OnSliderChanged(e, isSpacing: true), (_, v) => CoerceRange(v, Helpers.Fence.FenceDefaults.PostSpacingMinM, Helpers.Fence.FenceDefaults.PostSpacingMaxM, Helpers.Fence.FenceDefaults.PostSpacingStepM)));
        public bool IsPostSpacingInherited { get => (bool)GetValue(IsPostSpacingInheritedProperty); set => SetValue(IsPostSpacingInheritedProperty, value); }
        public static readonly DependencyProperty IsPostSpacingInheritedProperty = DependencyProperty.Register(nameof(IsPostSpacingInherited), typeof(bool), typeof(GMapPropertyPidsGroupControl), new PropertyMetadata(true));

        /// <summary>철망 높이(m) 슬라이더 값 — 기둥 간격과 같은 규약.</summary>
        public double FenceHeightM { get => (double)GetValue(FenceHeightMProperty); set => SetValue(FenceHeightMProperty, value); }
        public static readonly DependencyProperty FenceHeightMProperty = DependencyProperty.Register(nameof(FenceHeightM), typeof(double), typeof(GMapPropertyPidsGroupControl),
            new PropertyMetadata(Helpers.Fence.FenceDefaults.FenceHeightM, (d, e) => ((GMapPropertyPidsGroupControl)d).OnSliderChanged(e, isSpacing: false), (_, v) => CoerceRange(v, Helpers.Fence.FenceDefaults.FenceHeightMinM, Helpers.Fence.FenceDefaults.FenceHeightMaxM, Helpers.Fence.FenceDefaults.FenceHeightStepM)));
        public bool IsFenceHeightInherited { get => (bool)GetValue(IsFenceHeightInheritedProperty); set => SetValue(IsFenceHeightInheritedProperty, value); }
        public static readonly DependencyProperty IsFenceHeightInheritedProperty = DependencyProperty.Register(nameof(IsFenceHeightInherited), typeof(bool), typeof(GMapPropertyPidsGroupControl), new PropertyMetadata(true));

        /// <summary>"기둥 N개 · 실간격 x.xx m" / "센서 N개 · 간격 s m · 잔여 x m" — FenceLayout.Summary(순수 함수).</summary>
        public string FenceLayoutSummary { get => (string)GetValue(FenceLayoutSummaryProperty); set => SetValue(FenceLayoutSummaryProperty, value); }
        public static readonly DependencyProperty FenceLayoutSummaryProperty = DependencyProperty.Register(nameof(FenceLayoutSummary), typeof(string), typeof(GMapPropertyPidsGroupControl), new PropertyMetadata(string.Empty));

        public System.Windows.Input.ICommand ResetPostSpacingCommand { get; }
        public System.Windows.Input.ICommand ResetFenceHeightCommand { get; }
        public System.Windows.Input.ICommand SetPostSpacingPresetCommand { get; }

        /// <summary>슬라이더 값 정규화(눈금 양자화 + 클램프) — 정본은 <see cref="Helpers.Fence.FenceMath.Quantize"/>(WPF 무의존, 단위테스트 대상).</summary>
        private static object CoerceRange(object value, double min, double max, double step)
            => Helpers.Fence.FenceMath.Quantize(value is double d ? d : min, min, max, step);

        private bool CanWriteFence => !_isInitializing && !_isClearingBindings && !_syncingFence && !IsGroupEditing && SelectedMarker is IPidsGroupEditableMarker;

        private void OnFenceFieldChanged(string name, DependencyPropertyChangedEventArgs e, Action<IPidsGroupEditableMarker> apply)
        {
            if (!CanWriteFence || SelectedMarker is not IPidsGroupEditableMarker marker) return;
            apply(marker);
            OnMarkerPropertyChanged(name, e.OldValue, e.NewValue);
            UpdateFenceSummary();
        }

        private void OnSliderChanged(DependencyPropertyChangedEventArgs e, bool isSpacing)
        {
            UpdateFenceSummary();
            if (!CanWriteFence) return;
            if (isSpacing) { IsPostSpacingInherited = false; _spacingCommit.Touch((double)e.OldValue, (double)e.NewValue); }
            else { IsFenceHeightInherited = false; _heightCommit.Touch((double)e.OldValue, (double)e.NewValue); }
        }

        private void CommitFence(string name, double first, double last, Action<IPidsGroupEditableMarker> apply)
        {
            if (SelectedMarker is not IPidsGroupEditableMarker marker) return;
            object? before = name == "PostSpacingM" ? marker.PostSpacingM : marker.FenceHeightM;   // NULL(상속) → 값 전이도 undo 로 복원 가능
            apply(marker);
            OnMarkerPropertyChanged(name, before, last);
        }

        /// <summary>대기 중인 슬라이더 커밋을 즉시 확정(언로드·마커 교체·검증 훅).</summary>
        public void FlushDeferredCommits() { _spacingCommit.Flush(); _heightCommit.Flush(); }

        private void ResetPostSpacing()
        {
            if (SelectedMarker is not IPidsGroupEditableMarker marker || IsGroupEditing) return;
            _spacingCommit.Cancel();
            var before = marker.PostSpacingM;
            marker.PostSpacingM = null;
            OnMarkerPropertyChanged("PostSpacingM", before, null);
            SyncFenceFromMarker(marker);
        }

        private void ResetFenceHeight()
        {
            if (SelectedMarker is not IPidsGroupEditableMarker marker || IsGroupEditing) return;
            _heightCommit.Cancel();
            var before = marker.FenceHeightM;
            marker.FenceHeightM = null;
            OnMarkerPropertyChanged("FenceHeightM", before, null);
            SyncFenceFromMarker(marker);
        }

        /// <summary>모델 변경을 다시 읽어야 하는 3D 철망 절 필드 — 이름은 <see cref="GMapSymbols.GMapPidsGroupMarker"/> 의 통지명과 같다.</summary>
        private static readonly string[] FenceFieldNames = { "PostSpacingM", "FenceHeightM", "FenceMode", "Render3D", "ReverseSensorOrder" };
        private System.ComponentModel.INotifyPropertyChanged? _fenceNotifier;

        /// <summary>
        /// 마커 모델이 <b>패널 밖에서</b> 바뀌면(Undo/Redo · 그룹 일괄반영) 3D 철망 절을 다시 읽는다.
        /// 이 절은 다른 속성과 달리 TwoWay 바인딩이 아니라 <see cref="SyncFenceFromMarker"/> 로 마커 로드 시 1회만 값을 받으므로
        /// 구독이 없으면 Undo 가 모델을 되돌려도 슬라이더가 옛 값에 머물러 "undo 가 안 된다"로 보인다(사용자 보고 2026-09-08).
        /// 지연 커밋 대기 중(사용자가 슬라이더를 막 움직인 직후)이면 그 변경의 출처가 이 패널이므로 무시한다.
        /// </summary>
        private void OnMarkerModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_syncingFence || _spacingCommit.IsPending || _heightCommit.IsPending) return;
            if (e.PropertyName is not null && Array.IndexOf(FenceFieldNames, e.PropertyName) < 0) return;
            if (SelectedMarker is IPidsGroupEditableMarker marker) SyncFenceFromMarker(marker);
        }

        private void SubscribeFenceNotifier()
        {
            UnsubscribeFenceNotifier();
            if (SelectedMarker is System.ComponentModel.INotifyPropertyChanged npc)
            { _fenceNotifier = npc; npc.PropertyChanged += OnMarkerModelPropertyChanged; }
        }

        private void UnsubscribeFenceNotifier()
        {
            if (_fenceNotifier is null) return;
            _fenceNotifier.PropertyChanged -= OnMarkerModelPropertyChanged;
            _fenceNotifier = null;
        }

        private void SyncFenceFromMarker(IPidsGroupEditableMarker marker)
        {
            _syncingFence = true;
            try
            {
                IsPostSpacingInherited = marker.PostSpacingM is null;
                PostSpacingM = marker.PostSpacingM ?? FencePostSpacingDefault;
                IsFenceHeightInherited = marker.FenceHeightM is null;
                FenceHeightM = marker.FenceHeightM ?? FenceHeightDefault;
                Render3D = marker.Render3D;
                FenceMode = marker.FenceMode;
            }
            finally { _syncingFence = false; }
            UpdateFenceSummary();
        }

        private void UpdateFenceSummary()
        {
            if (SelectedMarker is not IPidsGroupEditableMarker marker || marker.RuntimePoints is not { Count: >= 2 } pts)
            { FenceLayoutSummary = string.Empty; return; }
            var local = Helpers.Fence.FenceLayout.ToLocalMeters(pts.Select(p => (p.Lat, p.Lng)).ToList());
            var result = Helpers.Fence.FenceLayout.Compute(local, PostSpacingM, FenceMode, marker.IsClosedPath);
            FenceLayoutSummary = Helpers.Fence.FenceLayout.Summary(result, PostSpacingM, FenceMode);
        }
        #endregion

        #region - Overrides -

        protected override void ClearSpecificBindings()
        {
            //System.Diagnostics.Debug.WriteLine("=== PidsGroupControl ClearSpecificBindings 시작 ===");

            // Line 관련 바인딩 해제
            BindingOperations.ClearBinding(this, LinePatternProperty);
            BindingOperations.ClearBinding(this, LineOpacityProperty);
            BindingOperations.ClearBinding(this, IsClosedPathProperty);
            BindingOperations.ClearBinding(this, TotalDistanceProperty);
            BindingOperations.ClearBinding(this, PointCountProperty);

            // PidsGroup 관련 바인딩 해제
            BindingOperations.ClearBinding(this, LinkedDeviceGroupProperty);
            BindingOperations.ClearBinding(this, EventStatusProperty);
            FlushDeferredCommits();   // 마커 교체 전 대기 커밋 확정(값 유실 방지)
            UnsubscribeFenceNotifier();

            //System.Diagnostics.Debug.WriteLine("=== PidsGroupControl ClearSpecificBindings 완료 ===");
        }

        protected override void SetupSpecificBindings()
        {
            //System.Diagnostics.Debug.WriteLine("=== PidsGroupControl SetupSpecificBindings 시작 ===");

            if (SelectedMarker is IPidsGroupEditableMarker pidsGroupMarker)
            {
                //System.Diagnostics.Debug.WriteLine($"바인딩 대상: {pidsGroupMarker.GetType().Name}");

                // Line 스타일 속성 바인딩
                var linePatternBinding = CreateTwoWayBinding(nameof(pidsGroupMarker.LinePattern));
                SetBinding(LinePatternProperty, linePatternBinding);

                var lineOpacityBinding = CreateTwoWayBinding(nameof(pidsGroupMarker.LineOpacity));
                SetBinding(LineOpacityProperty, lineOpacityBinding);

                // Line 옵션 바인딩
                var isClosedPathBinding = CreateTwoWayBinding(nameof(pidsGroupMarker.IsClosedPath));
                SetBinding(IsClosedPathProperty, isClosedPathBinding);

                // PidsGroup 속성 바인딩
                var linkedDeviceGroupBinding = CreateTwoWayBinding(nameof(pidsGroupMarker.LinkedDeviceGroup));
                SetBinding(LinkedDeviceGroupProperty, linkedDeviceGroupBinding);

                var eventStatusBinding = CreateTwoWayBinding(nameof(pidsGroupMarker.EventStatus));
                SetBinding(EventStatusProperty, eventStatusBinding);

                // 읽기 전용 속성 바인딩 (OneWay)
                var totalDistanceBinding = new Binding(nameof(pidsGroupMarker.TotalDistance))
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
                SubscribeFenceNotifier();   // 3D 철망 절은 수동 동기라 모델 변경(Undo/Redo)을 직접 구독해야 한다
            }

            //System.Diagnostics.Debug.WriteLine("=== PidsGroupControl SetupSpecificBindings 완료 ===");
        }

        protected override void SetupSpecificPropertiesFromMarker(IEditableMarker marker)
        {
            if (!(marker is IPidsGroupEditableMarker pidsGroupMarker)) return;

            // Line 속성 로드
            this.LinePattern = pidsGroupMarker.LinePattern;
            this.LineOpacity = pidsGroupMarker.LineOpacity;
            this.IsClosedPath = pidsGroupMarker.IsClosedPath;
            this.TotalDistance = pidsGroupMarker.TotalDistance;
            this.PointCount = pidsGroupMarker.RuntimePoints?.Count ?? 0;

            // PidsGroup 속성 로드
            this.LinkedDeviceGroup = pidsGroupMarker.LinkedDeviceGroup;
            this.EventStatus = pidsGroupMarker.EventStatus;
            SyncFenceFromMarker(pidsGroupMarker);   // 3D 철망(NULL=전역 기본 표시)

            //System.Diagnostics.Debug.WriteLine($"PidsGroup 마커에서 속성 로드 완료");
        }

        protected override void UpdateSpecificProperties()
        {
            if (SelectedMarker is IPidsGroupEditableMarker pidsGroupMarker)
            {
                // Line 속성 업데이트
                pidsGroupMarker.LinePattern = this.LinePattern;
                pidsGroupMarker.LineOpacity = this.LineOpacity;
                pidsGroupMarker.IsClosedPath = this.IsClosedPath;

                // PidsGroup 속성 업데이트
                pidsGroupMarker.LinkedDeviceGroup = this.LinkedDeviceGroup;
                pidsGroupMarker.EventStatus = this.EventStatus;
            }
        }

        public override Type GetSupportedMarkerType()
        {
            return typeof(GMapPropertyPidsGroupControl);
        }

        #endregion

        #region - Dependency Properties -

        #region Line Properties

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
                typeof(GMapPropertyPidsGroupControl),
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
                typeof(GMapPropertyPidsGroupControl),
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
                typeof(GMapPropertyPidsGroupControl),
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
                typeof(GMapPropertyPidsGroupControl),
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
                typeof(GMapPropertyPidsGroupControl),
                new PropertyMetadata(0));

        #endregion

        #region PidsGroup Specific Properties

        /// <summary>
        /// 선택 가능한 디바이스 그룹 목록 (PropertyPanelFactory에서 주입)
        /// </summary>
        public ObservableCollection<IDeviceGroupModel> FilteredDeviceGroupList
        {
            get { return (ObservableCollection<IDeviceGroupModel>)GetValue(FilteredDeviceGroupListProperty); }
            set { SetValue(FilteredDeviceGroupListProperty, value); }
        }

        public static readonly DependencyProperty FilteredDeviceGroupListProperty =
            DependencyProperty.Register("FilteredDeviceGroupList", typeof(ObservableCollection<IDeviceGroupModel>),
                typeof(GMapPropertyPidsGroupControl), new PropertyMetadata(null));

        /// <summary>
        /// 연결된 디바이스 그룹 ID
        /// </summary>
        public int LinkedDeviceGroup
        {
            get { return (int)GetValue(LinkedDeviceGroupProperty); }
            set { SetValue(LinkedDeviceGroupProperty, value); }
        }

        public static readonly DependencyProperty LinkedDeviceGroupProperty =
            DependencyProperty.Register("LinkedDeviceGroup", typeof(int),
                typeof(GMapPropertyPidsGroupControl),
                new PropertyMetadata(0, OnLinkedDeviceGroupChanged));

        /// <summary>
        /// 이벤트 상태
        /// </summary>
        public EnumEventStatus EventStatus
        {
            get { return (EnumEventStatus)GetValue(EventStatusProperty); }
            set { SetValue(EventStatusProperty, value); }
        }

        public static readonly DependencyProperty EventStatusProperty =
            DependencyProperty.Register("EventStatus", typeof(EnumEventStatus),
                typeof(GMapPropertyPidsGroupControl),
                new PropertyMetadata(EnumEventStatus.Normal, OnEventStatusChanged));

        #endregion

        #endregion

        #region - Property Changed Methods -

        #region Line Property Changed Methods

        private static void OnLinePatternChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnLinePatternChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsGroupControl control &&
                control.SelectedMarker is IPidsGroupEditableMarker pidsGroupMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                pidsGroupMarker.LinePattern = (EnumLinePattern)e.NewValue;
                control.OnMarkerPropertyChanged("LinePattern", e.OldValue, e.NewValue);
            }
        }

        private static void OnLineOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnLineOpacityChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsGroupControl control &&
                control.SelectedMarker is IPidsGroupEditableMarker pidsGroupMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                pidsGroupMarker.LineOpacity = (double)e.NewValue;
                control.OnMarkerPropertyChanged("LineOpacity", e.OldValue, e.NewValue);
            }
        }

        private static void OnIsClosedPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnIsClosedPathChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsGroupControl control &&
                control.SelectedMarker is IPidsGroupEditableMarker pidsGroupMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                pidsGroupMarker.IsClosedPath = (bool)e.NewValue;
                control.OnMarkerPropertyChanged("IsClosedPath", e.OldValue, e.NewValue);
            }
        }

        #endregion

        #region PidsGroup Property Changed Methods

        private static void OnLinkedDeviceGroupChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnLinkedDeviceGroupChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsGroupControl control &&
                control.SelectedMarker is IPidsGroupEditableMarker pidsGroupMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                pidsGroupMarker.LinkedDeviceGroup = (int)e.NewValue;
                control.OnMarkerPropertyChanged("LinkedDeviceGroup", e.OldValue, e.NewValue);
            }
        }

        private static void OnEventStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnEventStatusChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsGroupControl control &&
                control.SelectedMarker is IPidsGroupEditableMarker pidsGroupMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                pidsGroupMarker.EventStatus = (EnumEventStatus)e.NewValue;
                control.OnMarkerPropertyChanged("EventStatus", e.OldValue, e.NewValue);
            }
        }

        #endregion

        #endregion
    }
}