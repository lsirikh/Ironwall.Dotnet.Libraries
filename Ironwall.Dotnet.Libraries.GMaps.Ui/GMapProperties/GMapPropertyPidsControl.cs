using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties
{
    /****************************************************************************
       Purpose      : PIDS 디바이스 마커의 속성을 제어하는 컨트롤                                                          
       Created By   : GHLee                                                
       Created On   : 9/3/2025                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class GMapPropertyPidsControl : GMapPropertyBaseControl
    {
        public static readonly DependencyProperty ModelVariantProperty = DependencyProperty.Register(
            nameof(ModelVariant), typeof(string), typeof(GMapPropertyPidsControl), new PropertyMetadata(null, (d, e) =>
            {
                var control = (GMapPropertyPidsControl)d;
                if (control._isInitializing || control._isClearingBindings || control.IsGroupMode || control.SelectedMarker is not GMapPidsMarker marker) return;
                marker.ModelVariant = (string?)e.NewValue;
                control.OnMarkerPropertyChanged(nameof(ModelVariant), e.OldValue, e.NewValue);
            }));
        public string? ModelVariant { get => (string?)GetValue(ModelVariantProperty); set => SetValue(ModelVariantProperty, value); }
        public IReadOnlyList<KeyValuePair<string, string>> ModelVariants { get; } = new[]
        {
            new KeyValuePair<string, string>("Fixed", "고정형 · 불릿"),
            new KeyValuePair<string, string>("Dome", "돔형 · 매달림"),
            new KeyValuePair<string, string>("Ptz", "PTZ · 스피드돔")
        };
        #region - Ctors -
        static GMapPropertyPidsControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapPropertyPidsControl),
                new FrameworkPropertyMetadata(typeof(GMapPropertyPidsControl)));
        }

        public GMapPropertyPidsControl()
        {
            _gateWidthCommit = new DeferredCommit<double>((first, last) =>
            {
                if (SelectedMarker is not IPidsEditableMarker marker) return;
                var before = marker.GateWidthM;
                marker.GateWidthM = last;
                OnMarkerPropertyChanged("GateWidthM", before, last);
            });
            ResetGateWidthCommand = new PanelCommand(_ => ResetGateWidth());
            OpenDoorCommand = new PanelCommand(_ => SendDoorCommand(Ironwall.Dotnet.Libraries.Messages.Dto.Devices.DoorControlRequestDto.Open));
            CloseDoorCommand = new PanelCommand(_ => SendDoorCommand(Ironwall.Dotnet.Libraries.Messages.Dto.Devices.DoorControlRequestDto.Close));
            Unloaded += (_, _) => { _gateWidthCommit.Flush(); UnsubscribeGateNotifier(); StopDoorTimer(); };
        }
        #endregion

        #region - 통문 개폐(FR-12/18) -
        private readonly DeferredCommit<double> _gateWidthCommit;
        private bool _syncingGate;

        /// <summary>통문 폭(m) 슬라이더 — 마커 NULL 이면 기본 4.0 표시(IsGateWidthInherited), 150ms 지연 커밋.</summary>
        public double GateWidthM { get => (double)GetValue(GateWidthMProperty); set => SetValue(GateWidthMProperty, value); }
        public static readonly DependencyProperty GateWidthMProperty = DependencyProperty.Register(nameof(GateWidthM), typeof(double), typeof(GMapPropertyPidsControl),
            new PropertyMetadata(Helpers.Fence.FenceDefaults.GateWidthM, (d, e) =>
            {
                var control = (GMapPropertyPidsControl)d;
                if (control._isInitializing || control._isClearingBindings || control._syncingGate || control.IsGroupMode || control.SelectedMarker is not IPidsEditableMarker) return;
                control.IsGateWidthInherited = false;
                control._gateWidthCommit.Touch((double)e.OldValue, (double)e.NewValue);
            }, (_, v) => Helpers.Fence.FenceMath.Quantize(v is double x ? x : Helpers.Fence.FenceDefaults.GateWidthM,
                    Helpers.Fence.FenceDefaults.GateWidthMinM, Helpers.Fence.FenceDefaults.GateWidthMaxM, Helpers.Fence.FenceDefaults.GateWidthStepM)));
        public bool IsGateWidthInherited { get => (bool)GetValue(IsGateWidthInheritedProperty); set => SetValue(IsGateWidthInheritedProperty, value); }
        public static readonly DependencyProperty IsGateWidthInheritedProperty = DependencyProperty.Register(nameof(IsGateWidthInherited), typeof(bool), typeof(GMapPropertyPidsControl), new PropertyMetadata(true));

        /// <summary>접점 ON 을 '열림'으로 해석(true) / 반전(false) — 통문·함체 공통, TwoWay 즉시 커밋.</summary>
        public bool OpenOnContactOn { get => (bool)GetValue(OpenOnContactOnProperty); set => SetValue(OpenOnContactOnProperty, value); }
        public static readonly DependencyProperty OpenOnContactOnProperty = DependencyProperty.Register(nameof(OpenOnContactOn), typeof(bool), typeof(GMapPropertyPidsControl),
            new PropertyMetadata(true, (d, e) =>
            {
                var control = (GMapPropertyPidsControl)d;
                if (control._isInitializing || control._isClearingBindings || control._syncingGate || control.IsGroupMode || control.SelectedMarker is not IPidsEditableMarker marker) return;
                marker.OpenOnContactOn = (bool)e.NewValue;
                control.OnMarkerPropertyChanged(nameof(OpenOnContactOn), e.OldValue, e.NewValue);
            }));
        /// <summary>속성창 절 표시 게이트 — 통문/함체(개폐 형태를 가진 타입).</summary>
        public bool HasDoor { get => (bool)GetValue(HasDoorProperty); set => SetValue(HasDoorProperty, value); }
        public static readonly DependencyProperty HasDoorProperty = DependencyProperty.Register(nameof(HasDoor), typeof(bool), typeof(GMapPropertyPidsControl), new PropertyMetadata(false));
        public bool IsGate { get => (bool)GetValue(IsGateProperty); set => SetValue(IsGateProperty, value); }
        public static readonly DependencyProperty IsGateProperty = DependencyProperty.Register(nameof(IsGate), typeof(bool), typeof(GMapPropertyPidsControl), new PropertyMetadata(false));
        public System.Windows.Input.ICommand ResetGateWidthCommand { get; }

        #region - 문 개폐 제어(FR-02~08) -
        private readonly Helpers.Door.DoorCommandGate _doorGate = new();
        private System.Windows.Threading.DispatcherTimer? _doorTimer;

        /// <summary>
        /// 개폐 명령 전송기 — <c>PropertyPanelFactory</c> 가 주입한다(장비 Id · 장비 타입 · "OPEN"/"CLOSE" -&gt; 성공 여부).
        /// 미주입이면 버튼은 비활성이다(배선 없는 환경에서 조용히 아무 일도 안 일어나는 것 방지).
        /// </summary>
        public Func<int, EnumDeviceType, string, Task<bool>>? DoorCommandSender { get; set; }

        /// <summary>장비 제어 권한(devices:control) 판정기 — 미주입이면 <b>false</b>(fail-closed, FR-27).</summary>
        public Func<bool>? CanControlDevice { get; set; }

        /// <summary>명령 실패·타임아웃 통지기 — 미주입이면 무동작(FR-28/NFR-03).</summary>
        public Action<string>? NotifyDoorIssue { get; set; }

        public System.Windows.Input.ICommand OpenDoorCommand { get; }
        public System.Windows.Input.ICommand CloseDoorCommand { get; }

        /// <summary>화면 상태 — Unknown/Closed/Open/Pending. XAML 트리거·텍스트가 이걸 본다.</summary>
        public Helpers.Door.DoorUiState DoorDisplayState { get => (Helpers.Door.DoorUiState)GetValue(DoorDisplayStateProperty); private set => SetValue(DoorDisplayStateProperty, value); }
        public static readonly DependencyProperty DoorDisplayStateProperty = DependencyProperty.Register(nameof(DoorDisplayState), typeof(Helpers.Door.DoorUiState), typeof(GMapPropertyPidsControl), new PropertyMetadata(Helpers.Door.DoorUiState.Unknown));

        /// <summary>열림 버튼 활성 — FR-04~07 조건의 AND.</summary>
        public bool CanOpenDoor { get => (bool)GetValue(CanOpenDoorProperty); private set => SetValue(CanOpenDoorProperty, value); }
        public static readonly DependencyProperty CanOpenDoorProperty = DependencyProperty.Register(nameof(CanOpenDoor), typeof(bool), typeof(GMapPropertyPidsControl), new PropertyMetadata(false));

        /// <summary>닫힘 버튼 활성.</summary>
        public bool CanCloseDoor { get => (bool)GetValue(CanCloseDoorProperty); private set => SetValue(CanCloseDoorProperty, value); }
        public static readonly DependencyProperty CanCloseDoorProperty = DependencyProperty.Register(nameof(CanCloseDoor), typeof(bool), typeof(GMapPropertyPidsControl), new PropertyMetadata(false));

        /// <summary>상태 텍스트 — 열림/닫힘/대기/미수신/사유.</summary>
        public string DoorStateText { get => (string)GetValue(DoorStateTextProperty); private set => SetValue(DoorStateTextProperty, value); }
        public static readonly DependencyProperty DoorStateTextProperty = DependencyProperty.Register(nameof(DoorStateText), typeof(string), typeof(GMapPropertyPidsControl), new PropertyMetadata(string.Empty));

        /// <summary>서버·현장이 보고한 상태를 게이트에 반영하고 화면을 다시 계산한다.</summary>
        private void SyncDoorFromMarker(IPidsEditableMarker marker)
        {
            _doorGate.OnStateReported(marker.DoorState switch
            {
                EnumDoorState.Open => Helpers.Door.DoorUiState.Open,
                EnumDoorState.Closed => Helpers.Door.DoorUiState.Closed,
                _ => Helpers.Door.DoorUiState.Unknown,
            });
            StopDoorTimer();
            RefreshDoorUi();
        }

        /// <summary>
        /// 버튼 클릭 -&gt; 명령 발행. <b>성공해도 상태를 바꾸지 않는다</b> — 실제 전이는 매니저 보고
        /// (OPERATION_EVENT -&gt; 마커 DoorState -&gt; <see cref="SyncDoorFromMarker"/>)로만 온다(FR-03).
        /// </summary>
        private async void SendDoorCommand(string command)
        {
            if (SelectedMarker is not IPidsEditableMarker marker) return;
            if (!IsDoorControlAllowed(marker)) return;
            if (!_doorGate.TryBegin(command, DateTime.Now)) return;   // 연타·미수신·동일상태 차단

            RefreshDoorUi();
            StartDoorTimer();

            bool ok = false;
            try
            {
                ok = DoorCommandSender is not null
                     && await DoorCommandSender(marker.LinkedDeviceId, marker.DeviceType, command);
            }
            catch (Exception ex)
            {
                NotifyDoorIssue?.Invoke($"개폐 명령 전송 실패: {ex.Message}");
            }

            if (!ok)
            {
                // 전송 자체가 실패했으면 대기할 이유가 없다 — 즉시 풀고 알린다(NFR-03).
                _doorGate.OnStateReported(_doorGate.ReportedState);
                StopDoorTimer();
                RefreshDoorUi();
                NotifyDoorIssue?.Invoke("개폐 명령을 보내지 못했습니다.");
            }
        }

        /// <summary>장비 연결(FR-06) 그리고 권한(FR-07). 게이트 자체 조건(대기·미수신)은 DoorCommandGate.CanSend.</summary>
        private bool IsDoorControlAllowed(IPidsEditableMarker marker)
            => marker.LinkedDeviceId > 0 && (CanControlDevice?.Invoke() ?? false);

        private void RefreshDoorUi()
        {
            var marker = SelectedMarker as IPidsEditableMarker;
            DoorDisplayState = _doorGate.DisplayState;

            bool linked = marker is not null && marker.LinkedDeviceId > 0;
            bool permitted = CanControlDevice?.Invoke() ?? false;
            bool wired = DoorCommandSender is not null;
            bool baseOk = linked && permitted && wired && _doorGate.CanSend;

            CanOpenDoor = baseOk && _doorGate.ReportedState != Helpers.Door.DoorUiState.Open;
            CanCloseDoor = baseOk && _doorGate.ReportedState != Helpers.Door.DoorUiState.Closed;

            if (!linked) DoorStateText = "장비 미연결";
            else if (!permitted) DoorStateText = "권한 없음(devices:control)";
            else if (!wired) DoorStateText = "제어 배선 없음";
            else if (_doorGate.LastCommandTimedOut) DoorStateText = "응답 없음 — 다시 시도하세요";
            else DoorStateText = _doorGate.DisplayState switch
            {
                Helpers.Door.DoorUiState.Pending => "명령 보냄 · 응답 대기",
                Helpers.Door.DoorUiState.Open => "열림",
                Helpers.Door.DoorUiState.Closed => "닫힘",
                _ => "상태 미수신(?)",
            };
        }

        private void StartDoorTimer()
        {
            _doorTimer ??= CreateDoorTimer();
            _doorTimer.Stop();
            _doorTimer.Start();
        }

        private System.Windows.Threading.DispatcherTimer CreateDoorTimer()
        {
            var t = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background, Dispatcher)
            { Interval = TimeSpan.FromSeconds(1) };
            t.Tick += (_, _) =>
            {
                if (!_doorGate.Tick(DateTime.Now)) return;
                StopDoorTimer();
                RefreshDoorUi();
                NotifyDoorIssue?.Invoke("개폐 응답이 오지 않았습니다. 장비 상태를 확인하세요.");
            };
            return t;
        }

        private void StopDoorTimer() => _doorTimer?.Stop();
        #endregion

        /// <summary>모델 변경을 다시 읽어야 하는 통문·함체 절 필드 — 이름은 마커 통지명과 같다.</summary>
        private static readonly string[] GateFieldNames = { "GateWidthM", "OpenOnContactOn", "DoorState" };
        private System.ComponentModel.INotifyPropertyChanged? _gateNotifier;

        /// <summary>
        /// 마커 모델이 <b>패널 밖에서</b> 바뀌면(Undo/Redo · 그룹 일괄반영) 통문 절을 다시 읽는다.
        /// 이 절은 TwoWay 바인딩이 아니라 <see cref="SyncGateFromMarker"/> 로 마커 로드 시 1회만 값을 받으므로
        /// 구독이 없으면 Undo 가 모델을 되돌려도 슬라이더가 옛 값에 머물러 "undo 가 안 된다"로 보인다(사용자 보고 2026-09-08).
        /// </summary>
        private void OnMarkerModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_syncingGate || _gateWidthCommit.IsPending) return;
            if (e.PropertyName is not null && Array.IndexOf(GateFieldNames, e.PropertyName) < 0) return;
            if (SelectedMarker is IPidsEditableMarker marker) SyncGateFromMarker(marker);
        }

        private void SubscribeGateNotifier()
        {
            UnsubscribeGateNotifier();
            if (SelectedMarker is System.ComponentModel.INotifyPropertyChanged npc)
            { _gateNotifier = npc; npc.PropertyChanged += OnMarkerModelPropertyChanged; }
        }

        private void UnsubscribeGateNotifier()
        {
            if (_gateNotifier is null) return;
            _gateNotifier.PropertyChanged -= OnMarkerModelPropertyChanged;
            _gateNotifier = null;
        }

        private void SyncGateFromMarker(IPidsEditableMarker marker)
        {
            _syncingGate = true;
            try
            {
                HasDoor = Ironwall.Dotnet.Monitoring.Models.Helpers.DoorStateMachine.HasDoor(marker.DeviceType);
                IsGate = marker.DeviceType == EnumDeviceType.Gate;
                IsGateWidthInherited = marker.GateWidthM is null;
                GateWidthM = marker.GateWidthM ?? Helpers.Fence.FenceDefaults.GateWidthM;
                OpenOnContactOn = marker.OpenOnContactOn;
            }
            finally { _syncingGate = false; }
            SyncDoorFromMarker(marker);   // FR-02~05: 문 상태·버튼 활성 재계산
        }

        private void ResetGateWidth()
        {
            if (SelectedMarker is not IPidsEditableMarker marker || IsGroupMode) return;
            _gateWidthCommit.Cancel();
            var before = marker.GateWidthM;
            marker.GateWidthM = null;
            OnMarkerPropertyChanged("GateWidthM", before, null);
            SyncGateFromMarker(marker);
        }
        #endregion
        
        #region - Overrides -

        protected override void ClearSpecificBindings()
        {
            //System.Diagnostics.Debug.WriteLine("=== PidsControl ClearSpecificBindings 시작 ===");

            BindingOperations.ClearBinding(this, LinkedDeviceIdProperty);
            BindingOperations.ClearBinding(this, LinkedDeviceProperty);
            BindingOperations.ClearBinding(this, DetectionRangeProperty);
            BindingOperations.ClearBinding(this, DetectionAngleProperty);
            BindingOperations.ClearBinding(this, DetectionBearingProperty);
            BindingOperations.ClearBinding(this, BaseBearingProperty);
            BindingOperations.ClearBinding(this, ModelVariantProperty);
            BindingOperations.ClearBinding(this, ShowFOVProperty);
            BindingOperations.ClearBinding(this, FOVColorProperty);
            BindingOperations.ClearBinding(this, FOVOpacityProperty);
            _gateWidthCommit.Flush();   // 마커 교체 전 대기 커밋 확정
            UnsubscribeGateNotifier();

            //System.Diagnostics.Debug.WriteLine("=== PidsControl ClearSpecificBindings 완료 ===");
        }

        protected override void SetupSpecificBindings()
        {
            //System.Diagnostics.Debug.WriteLine("=== PidsControl SetupSpecificBindings 시작 ===");

            if (SelectedMarker is IPidsEditableMarker pidsMarker)
            {
                //System.Diagnostics.Debug.WriteLine($"바인딩 대상: {pidsMarker.GetType().Name}");

                // LinkedDeviceId 바인딩
                var linkedDeviceIdBinding = CreateTwoWayBinding(nameof(pidsMarker.LinkedDeviceId));
                SetBinding(LinkedDeviceIdProperty, linkedDeviceIdBinding);

                // LinkedDevice 바인딩 (런타임 디바이스 객체)
                var linkedDeviceBinding = CreateTwoWayBinding(nameof(pidsMarker.LinkedDevice));
                SetBinding(LinkedDeviceProperty, linkedDeviceBinding);

                // ShowFOV 바인딩
                var showFOVBinding = CreateTwoWayBinding(nameof(pidsMarker.ShowFOV));
                SetBinding(ShowFOVProperty, showFOVBinding);

                // FOVColor 바인딩
                var fovColorBinding = CreateTwoWayBinding(nameof(pidsMarker.FOVColor));
                SetBinding(FOVColorProperty, fovColorBinding);

                // FOVOpacity 바인딩
                var fovOpacityBinding = CreateTwoWayBinding(nameof(pidsMarker.FOVOpacity));
                SetBinding(FOVOpacityProperty, fovOpacityBinding);

                // Detection 속성들 바인딩 추가
                var detectionRangeBinding = CreateTwoWayBinding(nameof(pidsMarker.DetectionRange));
                SetBinding(DetectionRangeProperty, detectionRangeBinding);

                var detectionAngleBinding = CreateTwoWayBinding(nameof(pidsMarker.DetectionAngle));
                SetBinding(DetectionAngleProperty, detectionAngleBinding);

                var detectionBearingBinding = CreateTwoWayBinding(nameof(pidsMarker.DetectionBearing));
                SetBinding(DetectionBearingProperty, detectionBearingBinding);

                var baseBearingBinding = CreateTwoWayBinding(nameof(pidsMarker.BaseBearing));
                SetBinding(BaseBearingProperty, baseBearingBinding);
                SetBinding(ModelVariantProperty, CreateTwoWayBinding(nameof(GMapPidsMarker.ModelVariant)));
            }

            //System.Diagnostics.Debug.WriteLine("=== PidsControl SetupSpecificBindings 완료 ===");
        }

        protected override void SetupSpecificPropertiesFromMarker(IEditableMarker marker)
        {
            if (!(marker is IPidsEditableMarker pidsMarker)) return;

            //System.Diagnostics.Debug.WriteLine($"=== SetupSpecificPropertiesFromMarker 시작 ===");
            //System.Diagnostics.Debug.WriteLine($"  마커 Title: {pidsMarker.Title}");
            //System.Diagnostics.Debug.WriteLine($"  마커 LinkedDeviceId: {pidsMarker.LinkedDeviceId}");
            //System.Diagnostics.Debug.WriteLine($"  마커 LinkedDevice: {pidsMarker.LinkedDevice?.DeviceName ?? "null"}");
            //System.Diagnostics.Debug.WriteLine($"  FilteredDeviceList Count: {FilteredDeviceList?.Count ?? 0}");

            this.LinkedDeviceId = pidsMarker.LinkedDeviceId;
            this.LinkedDevice = pidsMarker.LinkedDevice;
            this.ShowFOV = pidsMarker.ShowFOV;
            this.FOVColor = pidsMarker.FOVColor;
            this.FOVOpacity = pidsMarker.FOVOpacity;

            // Detection 속성들 추가
            this.DetectionRange = pidsMarker.DetectionRange;
            this.DetectionAngle = pidsMarker.DetectionAngle;
            this.DetectionBearing = pidsMarker.DetectionBearing;
            this.BaseBearing = pidsMarker.BaseBearing;
            this.ModelVariant = (pidsMarker as GMapPidsMarker)?.ModelVariant;
            SyncGateFromMarker(pidsMarker);   // 통문/함체 개폐 절(FR-18)
            SubscribeGateNotifier();          // 수동 동기 절이라 모델 변경(Undo/Redo)을 직접 구독

            //System.Diagnostics.Debug.WriteLine($"  설정 후 Panel LinkedDevice: {this.LinkedDevice?.DeviceName ?? "null"}");
            //System.Diagnostics.Debug.WriteLine($"=== SetupSpecificPropertiesFromMarker 완료 ===");
        }

        protected override void UpdateSpecificProperties()
        {
            if (SelectedMarker is IPidsEditableMarker pidsMarker)
            {
                pidsMarker.LinkedDeviceId = this.LinkedDeviceId;
                pidsMarker.LinkedDevice = this.LinkedDevice;
                pidsMarker.ShowFOV = this.ShowFOV ?? pidsMarker.ShowFOV;   // null=그룹 Pending → 미변경
                pidsMarker.FOVColor = this.FOVColor;
                pidsMarker.FOVOpacity = this.FOVOpacity;

                // Detection 속성들 추가
                pidsMarker.DetectionRange = this.DetectionRange;
                pidsMarker.DetectionAngle = this.DetectionAngle;
                pidsMarker.DetectionBearing = this.DetectionBearing;
                pidsMarker.BaseBearing = this.BaseBearing;
            }
        }

        public override Type GetSupportedMarkerType()
        {
            return typeof(GMapPropertyPidsControl);
        }

        /// <summary>그룹 Pending — PIDS 특화 필드도 "전원 동일=값 / 다름=빈칸(3상태)"(기능 ② 확장).
        /// 디바이스 연결은 심볼별 고유라 대표값 표시+비활성(IsGroupEditing 트리거).</summary>
        protected override void ApplyGroupPendingSpecific()
        {
            var pids = GroupMarkers!.OfType<IPidsEditableMarker>().ToList();
            if (pids.Count < 2) { base.ApplyGroupPendingSpecific(); return; }
            var rep = (SelectedMarker as IPidsEditableMarker) ?? pids[0];

            // 디바이스 연결: 대표값 표시(편집은 비활성 — PidsPropertyStyle DataTrigger)
            LinkedDeviceId = rep.LinkedDeviceId;
            LinkedDevice = rep.LinkedDevice;

            ShowFOV = AllEq(pids, p => p.ShowFOV) ? rep.ShowFOV : (bool?)null;
            FOVColor = AllEq(pids, p => p.FOVColor) ? rep.FOVColor : MIXED_COLOR;
            FOVOpacity = AllEq(pids, p => p.FOVOpacity) ? rep.FOVOpacity : MIXED_DOUBLE;
            BaseBearing = AllEq(pids, p => p.BaseBearing) ? rep.BaseBearing : MIXED_DOUBLE;
            DetectionRange = AllEq(pids, p => p.DetectionRange) ? rep.DetectionRange : MIXED_DOUBLE;
            DetectionAngle = AllEq(pids, p => p.DetectionAngle) ? rep.DetectionAngle : MIXED_DOUBLE;
            DetectionBearing = AllEq(pids, p => p.DetectionBearing) ? rep.DetectionBearing : MIXED_DOUBLE;

            static bool AllEq<T>(List<IPidsEditableMarker> list, Func<IPidsEditableMarker, T> sel)
            {
                var first = sel(list[0]);
                for (int i = 1; i < list.Count; i++)
                    if (!EqualityComparer<T>.Default.Equals(first, sel(list[i]))) return false;
                return true;
            }
        }

        #endregion
        
        #region - Dependency Properties -

        /// <summary>
        /// 연결된 디바이스 ID
        /// </summary>
        public int LinkedDeviceId
        {
            get { return (int)GetValue(LinkedDeviceIdProperty); }
            set { SetValue(LinkedDeviceIdProperty, value); }
        }

        public static readonly DependencyProperty LinkedDeviceIdProperty =
            DependencyProperty.Register("LinkedDeviceId", typeof(int),
                typeof(GMapPropertyPidsControl),
                new PropertyMetadata(0, OnLinkedDeviceIdChanged));

        /// <summary>
        /// 연결된 디바이스 객체 (런타임 바인딩용)
        /// <para>ComboBox에서 선택된 디바이스를 바인딩합니다.</para>
        /// </summary>
        public IBaseDeviceModel? LinkedDevice
        {
            get { return (IBaseDeviceModel?)GetValue(LinkedDeviceProperty); }
            set { SetValue(LinkedDeviceProperty, value); }
        }

        public static readonly DependencyProperty LinkedDeviceProperty =
            DependencyProperty.Register("LinkedDevice", typeof(IBaseDeviceModel),
                typeof(GMapPropertyPidsControl),
                new PropertyMetadata(null, OnLinkedDeviceChanged));

        /// <summary>
        /// DeviceType에 따라 필터링된 디바이스 목록 (ComboBox ItemsSource)
        /// </summary>
        public ObservableCollection<IBaseDeviceModel> FilteredDeviceList
        {
            get { return (ObservableCollection<IBaseDeviceModel>)GetValue(FilteredDeviceListProperty); }
            set { SetValue(FilteredDeviceListProperty, value); }
        }

        public static readonly DependencyProperty FilteredDeviceListProperty =
            DependencyProperty.Register("FilteredDeviceList", typeof(ObservableCollection<IBaseDeviceModel>),
                typeof(GMapPropertyPidsControl),
                new PropertyMetadata(null));

        /// <summary>
        /// 탐지 범위 (미터)
        /// </summary>
        public double DetectionRange
        {
            get { return (double)GetValue(DetectionRangeProperty); }
            set { SetValue(DetectionRangeProperty, value); }
        }

        public static readonly DependencyProperty DetectionRangeProperty =
            DependencyProperty.Register("DetectionRange", typeof(double),
                typeof(GMapPropertyPidsControl),
                new PropertyMetadata(30.0, OnDetectionRangeChanged, CoerceDoubleValue));

        /// <summary>
        /// 탐지 각도 (도)
        /// </summary>
        public double DetectionAngle
        {
            get { return (double)GetValue(DetectionAngleProperty); }
            set { SetValue(DetectionAngleProperty, value); }
        }

        public static readonly DependencyProperty DetectionAngleProperty =
            DependencyProperty.Register("DetectionAngle", typeof(double),
                typeof(GMapPropertyPidsControl),
                new PropertyMetadata(90.0, OnDetectionAngleChanged, CoerceDoubleValue));

        /// <summary>
        /// 탐지 방향 (도)
        /// </summary>
        public double DetectionBearing
        {
            get { return (double)GetValue(DetectionBearingProperty); }
            set { SetValue(DetectionBearingProperty, value); }
        }

        public static readonly DependencyProperty DetectionBearingProperty =
            DependencyProperty.Register("DetectionBearing", typeof(double),
                typeof(GMapPropertyPidsControl),
                new PropertyMetadata(0.0, OnDetectionBearingChanged, CoerceDoubleValue));

        /// <summary>
        /// 기준 방향 각도 (카메라 물리적 설치 방향, 도)
        /// </summary>
        public double BaseBearing
        {
            get { return (double)GetValue(BaseBearingProperty); }
            set { SetValue(BaseBearingProperty, value); }
        }

        public static readonly DependencyProperty BaseBearingProperty =
            DependencyProperty.Register("BaseBearing", typeof(double),
                typeof(GMapPropertyPidsControl),
                new PropertyMetadata(0.0, OnBaseBearingChanged, CoerceDoubleValue));

        /// <summary>
        /// FOV 표시 여부 — bool?(3상태): null=그룹 Pending(값 서로 다름, indeterminate 표시).
        /// </summary>
        public bool? ShowFOV
        {
            get { return (bool?)GetValue(ShowFOVProperty); }
            set { SetValue(ShowFOVProperty, value); }
        }

        public static readonly DependencyProperty ShowFOVProperty =
            DependencyProperty.Register("ShowFOV", typeof(bool?),
                typeof(GMapPropertyPidsControl),
                new PropertyMetadata(false, OnShowFOVChanged));

        /// <summary>
        /// FOV 색상
        /// </summary>
        public EnumColorType FOVColor
        {
            get { return (EnumColorType)GetValue(FOVColorProperty); }
            set { SetValue(FOVColorProperty, value); }
        }

        public static readonly DependencyProperty FOVColorProperty =
            DependencyProperty.Register("FOVColor", typeof(EnumColorType),
                typeof(GMapPropertyPidsControl),
                new PropertyMetadata(EnumColorType.Purple, OnFOVColorChanged));

        /// <summary>
        /// FOV 투명도
        /// </summary>
        public double FOVOpacity
        {
            get { return (double)GetValue(FOVOpacityProperty); }
            set { SetValue(FOVOpacityProperty, value); }
        }

        public static readonly DependencyProperty FOVOpacityProperty =
            DependencyProperty.Register("FOVOpacity", typeof(double),
                typeof(GMapPropertyPidsControl),
                new PropertyMetadata(0.8, OnFOVOpacityChanged, CoerceDoubleValue));

        #endregion

        #region - Property Changed Methods -

        private static void OnLinkedDeviceIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnLinkedDeviceIdChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsControl control &&
                control.SelectedMarker is IPidsEditableMarker pidsMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                if (control.IsGroupMode) return;   // 그룹 편집: 디바이스 연결은 심볼별 고유 → 변경 금지(콤보 비활성)
                pidsMarker.LinkedDeviceId = (int)e.NewValue;
                // 주의: OnMarkerPropertyChanged 호출하지 않음
                // LinkedDevice 변경 시 LinkedDeviceId가 자동 동기화되므로,
                // LinkedDevice 변경 핸들러에서만 DB 업데이트를 수행합니다.
                // 이중 업데이트로 인한 MySQL 동시성 충돌 방지
            }
        }

        private static void OnLinkedDeviceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnLinkedDeviceChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsControl control &&
                control.SelectedMarker is IPidsEditableMarker pidsMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                if (control.IsGroupMode) return;   // 그룹 편집: 디바이스 연결은 심볼별 고유 → 변경 금지(콤보 비활성)
                pidsMarker.LinkedDevice = (IBaseDeviceModel?)e.NewValue;
                control.OnMarkerPropertyChanged("LinkedDevice", e.OldValue, e.NewValue);
            }
        }

       

        private static void OnDetectionRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnDetectionRangeChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsControl control &&
                control.SelectedMarker is IPidsEditableMarker pidsMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                // DetectionRange는 런타임 전용 (DB 저장 안 함). 그룹=전원 직접 반영(무DB·무undo — 단일과 동일 의미).
                if (double.IsNaN((double)e.NewValue)) return;   // 그룹 Pending sentinel
                if (control.IsGroupMode)
                    foreach (var gm in control.GroupMarkers!.OfType<IPidsEditableMarker>()) gm.DetectionRange = (double)e.NewValue;
                else pidsMarker.DetectionRange = (double)e.NewValue;
                // OnMarkerPropertyChanged 호출 안 함 (DB UPDATE 트리거 방지)
            }
        }

        private static void OnDetectionAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnDetectionAngleChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsControl control &&
                control.SelectedMarker is IPidsEditableMarker pidsMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                // DetectionAngle은 런타임 전용 (DB 저장 안 함). 그룹=전원 직접 반영.
                if (double.IsNaN((double)e.NewValue)) return;   // 그룹 Pending sentinel
                if (control.IsGroupMode)
                    foreach (var gm in control.GroupMarkers!.OfType<IPidsEditableMarker>()) gm.DetectionAngle = (double)e.NewValue;
                else pidsMarker.DetectionAngle = (double)e.NewValue;
                // OnMarkerPropertyChanged 호출 안 함 (DB UPDATE 트리거 방지)
            }
        }

        private static void OnDetectionBearingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnDetectionBearingChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsControl control &&
                control.SelectedMarker is IPidsEditableMarker pidsMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                // DetectionBearing은 런타임 전용 (DB 저장 안 함). 그룹=전원 직접 반영.
                if (double.IsNaN((double)e.NewValue)) return;   // 그룹 Pending sentinel
                if (control.IsGroupMode)
                    foreach (var gm in control.GroupMarkers!.OfType<IPidsEditableMarker>()) gm.DetectionBearing = (double)e.NewValue;
                else pidsMarker.DetectionBearing = (double)e.NewValue;
                // OnMarkerPropertyChanged 호출 안 함 (DB UPDATE 트리거 방지)
            }
        }

        private static void OnBaseBearingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnBaseBearingChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsControl control &&
                control.SelectedMarker is IPidsEditableMarker pidsMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                if (double.IsNaN((double)e.NewValue)) return;   // 그룹 Pending sentinel — 미전파
                if (!control.IsGroupMode) pidsMarker.BaseBearing = (double)e.NewValue;
                control.OnMarkerPropertyChanged("BaseBearing", e.OldValue, e.NewValue);
            }
        }

        private static void OnShowFOVChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnShowFOVChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsControl control && 
                control.SelectedMarker is IPidsEditableMarker pidsMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                if (e.NewValue is not bool nsFov) return;   // null=그룹 Pending(indeterminate) — 마커 미전파
                if (!control.IsGroupMode) pidsMarker.ShowFOV = nsFov;   // 그룹=VM이 전원 일괄 적용
                control.OnMarkerPropertyChanged("ShowFOV", e.OldValue, nsFov);
            }
        }

        private static void OnFOVColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnFOVColorChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsControl control && 
                control.SelectedMarker is IPidsEditableMarker pidsMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                if ((int)(EnumColorType)e.NewValue < 0) return;   // 그룹 Pending sentinel — 미전파
                if (!control.IsGroupMode) pidsMarker.FOVColor = (EnumColorType)e.NewValue;
                control.OnMarkerPropertyChanged("FOVColor", e.OldValue, e.NewValue);
            }
        }

        private static void OnFOVOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"OnFOVOpacityChanged: {e.OldValue} → {e.NewValue}");

            if (d is GMapPropertyPidsControl control && 
                control.SelectedMarker is IPidsEditableMarker pidsMarker &&
                !control._isInitializing && !control._isClearingBindings)
            {
                if (double.IsNaN((double)e.NewValue)) return;   // 그룹 Pending sentinel — 미전파
                if (!control.IsGroupMode) pidsMarker.FOVOpacity = (double)e.NewValue;
                control.OnMarkerPropertyChanged("FOVOpacity", e.OldValue, e.NewValue);
            }
        }

        #endregion
    }
}
