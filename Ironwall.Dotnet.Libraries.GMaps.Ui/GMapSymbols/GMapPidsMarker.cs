using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

/****************************************************************************
  Purpose      : PIDS 장비 전용 마커 (최적화된 버전)
  Created By   : GHLee                                                
  Created On   : 8/26/2025                                                    
  Department   : SW Team                                                   
  Company      : Sensorway Co., Ltd.                                       
  Email        : lsirikh@naver.com                                         
****************************************************************************/
public class GMapPidsMarker : GMapBaseMarker<IPidsSymbolModel>, IPidsEditableMarker
{
    #region - Ctors -
    public GMapPidsMarker(ILogService log, IPidsSymbolModel pidsModel)
        : base(log, pidsModel)
    {
        pidsModel.Update += PidsModel_Update;
    }
    #endregion

    #region - Animation System -
    /// <summary>
    /// 모델 갱신 신호(<c>SetUpdate</c>) — 통지는 <b>항상 UI 스레드에서</b> 낸다.
    /// </summary>
    /// <remarks>
    /// <para>(A4) <c>DeviceSymbolLookupModel.SyncFromDevice</c> 는 SYNC_DEVICE 를 받은 NATS 스레드에서 곧장 <c>SetUpdate</c> 를 부른다
    /// (다른 경로는 <c>MarshalUpdate</c> 로 UI 스레드에 넘긴다). 그 스레드에서 마커 <c>PropertyChanged(DoorState)</c> 가 나가면
    /// 열려 있는 속성창(<c>GMapPropertyPidsControl.SyncGateFromMarker</c>)이 WPF 의존 속성을 쓰다 교차 스레드 예외로 죽고,
    /// 호스트의 SYNC_DEVICE 처리 전체(문 상태 · 제어기 자동복구 · 소속 통지)가 그 예외에 끊긴다.
    /// → 여기서 한 번에 막는다: UI 스레드가 아니면 합쳐서(<see cref="_updatePending"/>) 한 번만 넘긴다. 넘어간 콜백은 모델의 <b>최신값</b>을 읽는다.</para>
    /// </remarks>
    private void PidsModel_Update(object? sender, EventArgs e)
    {
        var dispatcher = Shape?.Dispatcher ?? System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess() && !dispatcher.HasShutdownStarted)
        {
            if (System.Threading.Interlocked.Exchange(ref _updatePending, 1) == 1) return;   // 이미 넘겨 둔 콜백이 최신값을 읽는다
            dispatcher.BeginInvoke(new Action(() =>
            {
                System.Threading.Interlocked.Exchange(ref _updatePending, 0);
                ApplyModelUpdate();
            }), System.Windows.Threading.DispatcherPriority.Background);
            return;
        }
        ApplyModelUpdate();
    }

    private void ApplyModelUpdate()
    {
        if (_model is null) return;   // Dispose 뒤 늦게 도착한 콜백
        // (A7) 갱신마다 Info 를 남기면 이벤트 1건이 로그 여러 줄이 된다(버퍼링 appender · 수백 개 마커). ILogService 에 Debug 수준이 없어
        //   디버그 빌드 출력으로만 내린다 — 상태 전이 자체는 SymbolEventManager 가 이미 Info 로 남긴다.
        System.Diagnostics.Debug.WriteLine($"[SYNC→Symbol] PidsModel_Update: '{_model.Title}' OperationState={_model.OperationState}, EventStatus={_model.EventStatus}");

        OnPropertyChanged(nameof(EventStatus));
        OnPropertyChanged(nameof(CompositeStatus));
        OnPropertyChanged(nameof(OperationState));
        OnPropertyChanged(nameof(DetectionRange));
        OnPropertyChanged(nameof(DetectionAngle));
        OnPropertyChanged(nameof(DetectionBearing));
        OnPropertyChanged(nameof(DoorState));   // FR-15: 이벤트 경로(DeviceSymbolLookupModel.ApplyDoorState → SetUpdate)는 모델에만 쓴다 — 통지가 없으면 3D 문짝이 안 움직인다(2026-09-08 00:27 실기 결함)

        // SYNC_DEVICE 재조회는 장비 모델의 축 묶음(Axes)을 참조째 갈고 SyncFromDevice → SetUpdate 로 여기 온다 — 부품 요약도 그때 다시 만든다.
        RefreshComponentSummary(force: false);
    }
    #endregion

    #region - Component health (runtime only) -
    /// <summary>
    /// 연결 장비의 부품 요약 — <b>런타임 전용</b>(직렬화 · 영속 · Undo 대상 아님, 진실은 서버).
    /// 연결 장비의 축 묶음 참조가 바뀔 때만 다시 만든다.
    /// </summary>
    public Helpers.Components.ComponentHealthSummary ComponentSummary
    {
        get => _componentSummary;
        private set
        {
            if (ReferenceEquals(_componentSummary, value)) return;
            _componentSummary = value;
            OnPropertyChanged(nameof(ComponentSummary));   // 지도(GMapCustomControl)가 이 통지를 받아 칸 줄 밀도를 다시 센다
        }
    }

    /// <summary>
    /// 연결 장비의 축 묶음(<c>LinkedDevice.Axes</c>)으로 부품 요약을 다시 만든다. <paramref name="force"/>=false 면 참조가 같을 때 건너뛴다.
    /// </summary>
    public void RefreshComponentSummary(bool force = false)
    {
        if (_model is null) return;
        var axes = _liveDetached ? null : _model.LinkedDevice?.Axes;
        if (!force && ReferenceEquals(axes, _summarySource) && _summaryBuilt) return;
        _summarySource = axes;
        _summaryBuilt = true;
        ComponentSummary = Helpers.Components.ComponentHealthSummary.Build(axes, Helpers.Components.MapComponentCatalog.Labels, _model.DeviceType);   // FR-07 카탈로그 한글 · 종류별 대표 칸
    }

    /// <summary>
    /// 지도가 센 칸 줄 밀집 여부(FR-04) — <b>런타임 전용</b>. 지도(<c>GMapCustomControl.RefreshComponentStripDensity</c>)가 값이 바뀔 때만
    /// 모든 PIDS 심볼에 내려 주고, 마커 컨트롤이 이 값을 부품 층으로 넘긴다.
    /// </summary>
    public bool ComponentStripCrowded
    {
        get => _componentStripCrowded;
        set
        {
            if (_componentStripCrowded == value) return;
            _componentStripCrowded = value;
            OnPropertyChanged(nameof(ComponentStripCrowded));
        }
    }

    /// <summary>조립 카드(L3)가 이 심볼에 열려 있다 — <b>런타임 전용</b>. 칸 줄을 밀집 중에도 그리게 한다.</summary>
    public bool IsComponentCardOpen
    {
        get => _isComponentCardOpen;
        set
        {
            if (_isComponentCardOpen == value) return;
            _isComponentCardOpen = value;
            OnPropertyChanged(nameof(IsComponentCardOpen));
        }
    }

    /// <summary>
    /// 연결 장비가 서버에서 삭제됐다 — 실시간 상태(이벤트 색 · 동작 · 문 · 부품)를 비운다. 저장된 연결(LinkedDeviceId)은 건드리지 않는다.
    /// 호출 스레드: UI.
    /// </summary>
    public void ResetLiveState()
    {
        if (_model is null) return;
        _liveDetached = true;
        _model.CompositeStatus = EnumCompositeEventStatus.Normal;   // EventStatus 도 함께 Normal
        _model.DoorState = EnumDoorState.Unknown;
        OperationState = EnumOperationState.NONE;
        RefreshComponentSummary(force: true);
        ApplyModelUpdate();
    }
    #endregion

    #region - Overrides -
    /// <summary>
    /// PIDS 마커 전용 컨트롤 생성
    /// </summary>
    private static bool s_symbol3DLogged;

    protected override UIElement CreateMarkerControl()
    {
        if (!s_symbol3DLogged)
        {
            s_symbol3DLogged = true;
            var on = Utils.Symbol3DFeature.IsEnabled;   // Lazy 평가 후 진단 문자열이 채워진다
            _log?.Info($"[Symbol3D] {(on ? "ON → 3D 하우징" : "OFF → 2D")} · {Utils.Symbol3DFeature.LastDiagnostic}");
        }
        GMapMarkerPidsControl markerControl = Utils.Symbol3DFeature.IsEnabled && Symbols3D.HousingModels.DeviceKey(_model.DeviceType) != null
            ? new GMapMarker3DHousingControl(this)
            : GMapMarkerPidsFallbackControl.NeedsFallback(_model.DeviceType) ? new GMapMarkerPidsFallbackControl(this) : new GMapMarkerPidsControl(this);
        _log?.Info($"GMapMarkerPidsControl 생성: {_model.Title} · DeviceType={_model.DeviceType} · ModelVariant={_model.ModelVariant ?? "-"} → {markerControl.GetType().Name}");
        return markerControl;
    }

    protected override void ConfigureMarkerControl(UIElement marker)
    {
        base.ConfigureMarkerControl(marker);
    }

    protected override void OnStatusChanged(EnumOperationState status)
    {
        base.OnStatusChanged(status);
        // EventStatus는 CompositeStatus setter를 통해서만 설정됨 — 여기서 직접 설정 금지 (SSOT)
    }

    /// <summary>
    /// 리소스 정리
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _model.Update -= PidsModel_Update;
        }
        base.Dispose(disposing);
    }
    #endregion

    #region - FOV Management -
    /// <summary>
    /// FOV 설정 업데이트
    /// </summary>
    public void UpdateFOVSettings(double range, double angle, double bearing)
    {
        try
        {
            _model.DetectionRange = range;
            _model.DetectionAngle = angle;
            _model.DetectionBearing = bearing;

            _log?.Info($"FOV 업데이트: 범위={range}m, 각도={angle}°, 방향={bearing}°");
        }
        catch (Exception ex)
        {
            _log?.Error($"FOV 업데이트 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// FOV 표시 토글 (프리미티브). ShowFOV 프로퍼티 경유로 PropertyChanged를 발화해 바인딩·렌더가 동기화되게 한다.
    /// <para>FOV 표시는 기본적으로 속성창 체크박스(SSOT)로 제어한다.</para>
    /// </summary>
    public void ToggleFOVDisplay()
    {
        // ShowFOV 프로퍼티 setter 경유(OnPropertyChanged 발화) — _model 직접 반전 시 TwoWay 바인딩이
        // 갱신되지 않아 컨트롤 DP·FOV 렌더가 반영 안 되는 문제를 차단.
        ShowFOV = !ShowFOV;
        _log?.Info($"FOV 표시 토글: {ShowFOV}");
    }

    
    #endregion

    #region - Properties -
    /// <summary>
    /// 연결된 장치 ID (하위 호환성 유지)
    /// </summary>
    public int LinkedDeviceId
    {
        get => _model.LinkedDeviceId;
        set
        {
            _model.LinkedDeviceId = value;
            OnPropertyChanged(nameof(LinkedDeviceId));
        }
    }

    /// <summary>
    /// 연결된 디바이스 객체 (런타임 바인딩용)
    /// <para>설정 시 LinkedDeviceId가 자동 동기화됩니다.</para>
    /// </summary>
    public IBaseDeviceModel? LinkedDevice
    {
        get => _model.LinkedDevice;
        set
        {
            _model.LinkedDevice = value;
            _liveDetached = false;   // 새로 연결됐다 — 삭제로 비웠던 실시간 상태를 다시 받는다
            OnPropertyChanged(nameof(LinkedDevice));
            OnPropertyChanged(nameof(LinkedDeviceId));  // ID도 동기화되므로 알림
            RefreshComponentSummary(force: true);       // 부팅 재바인딩 · 속성창 연결 변경 — 부품 요약을 새 장비로
        }
    }

    /// <summary>
    /// 장치 타입
    /// </summary>
    public EnumDeviceType DeviceType
    {
        get => _model.DeviceType;
        set
        {
            _model.DeviceType = value;
            OnPropertyChanged(nameof(DeviceType));
            RefreshComponentSummary(force: true);   // 칸 줄의 종류별 대표 순서가 바뀐다
        }
    }

    public string? ModelVariant
    {
        get => _model.ModelVariant;
        set { if (_model.ModelVariant == value) return; _model.ModelVariant = value; OnPropertyChanged(nameof(ModelVariant)); }
    }

    /// <summary>
    /// 이벤트 상태 (애니메이션 트리거)
    /// </summary>
    public EnumEventStatus EventStatus
    {
        get => _model.EventStatus;
        set
        {
            _model.EventStatus = value;
            OnPropertyChanged(nameof(EventStatus));
            // PropertyChanged 이벤트로 자동 애니메이션 처리됨
        }
    }

    public EnumCompositeEventStatus CompositeStatus
    {
        get => _model.CompositeStatus;
        set
        {
            _model.CompositeStatus = value;
            OnPropertyChanged(nameof(CompositeStatus));
        }
    }

    public double DetectionRange
    {
        get => _model.DetectionRange;
        set
        {
            _model.DetectionRange = value;
            OnPropertyChanged(nameof(DetectionRange));
        }
    }

    public double DetectionAngle
    {
        get => _model.DetectionAngle;
        set
        {
            _model.DetectionAngle = value;
            OnPropertyChanged(nameof(DetectionAngle));
        }
    }

    public double DetectionBearing
    {
        get => _model.DetectionBearing;
        set
        {
            _model.DetectionBearing = value;
            OnPropertyChanged(nameof(DetectionBearing));
        }
    }

    public double BaseBearing
    {
        get => _model.BaseBearing;
        set
        {
            _model.BaseBearing = value;
            OnPropertyChanged(nameof(BaseBearing));
        }
    }

    public bool ShowFOV
    {
        get => _model.ShowFOV;
        set
        {
            _model.ShowFOV = value;
            OnPropertyChanged(nameof(ShowFOV));
        }
    }

    public EnumColorType FOVColor
    {
        get => _model.FOVColor;
        set
        {
            _model.FOVColor = value;
            OnPropertyChanged(nameof(FOVColor));
        }
    }

    public double FOVOpacity
    {
        get => _model.FOVOpacity;
        set
        {
            _model.FOVOpacity = value;
            OnPropertyChanged(nameof(FOVOpacity));
        }
    }

    // ── 통문·함체 개폐(FR-12) — 형태 축은 EventStatus(색 축)와 독립 ──
    public double? GateWidthM
    {
        get => _model.GateWidthM;
        set { if (Nullable.Equals(_model.GateWidthM, value)) return; _model.GateWidthM = value; OnPropertyChanged(nameof(GateWidthM)); }
    }

    public bool OpenOnContactOn
    {
        get => _model.OpenOnContactOn;
        set { if (_model.OpenOnContactOn == value) return; _model.OpenOnContactOn = value; OnPropertyChanged(nameof(OpenOnContactOn)); }
    }

    /// <summary>개폐 형태(런타임) — 3D 문짝 각도 트리거. 값이 바뀔 때만 통지한다.</summary>
    public EnumDoorState DoorState
    {
        get => _model.DoorState;
        set { if (_model.DoorState == value) return; _model.DoorState = value; OnPropertyChanged(nameof(DoorState)); }
    }

    /// <summary>
    /// 방송(음원/TTS) 동작 중 여부 — XAML Opacity 펄스 애니메이션 트리거
    /// </summary>
    public bool IsBroadcasting
    {
        get => _isBroadcasting;
        set
        {
            _isBroadcasting = value;
            OnPropertyChanged(nameof(IsBroadcasting));
        }
    }

    #endregion

    #region - Attributes -
    private bool _isBroadcasting;
    private int _updatePending;   // 0=없음, 1=UI 스레드로 넘긴 갱신 대기 중(Interlocked)
    private Helpers.Components.ComponentHealthSummary _componentSummary = Helpers.Components.ComponentHealthSummary.None;
    private Ironwall.Dotnet.Monitoring.Models.Devices.IDeviceAxesModel? _summarySource;
    private bool _summaryBuilt;
    private bool _liveDetached;
    private bool _isComponentCardOpen;
    private bool _componentStripCrowded;
    #endregion
}
