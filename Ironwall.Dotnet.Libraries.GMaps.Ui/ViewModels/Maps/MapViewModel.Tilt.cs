using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using System;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 지도 카드 틸트(2.5D) 뷰모델 배선 — map-tilt-25d PRD v1.1 FR-07(VM 복원·영속) · FR-08(툴바 토글/각도 팝업/
                  보기 메뉴/단축키 커맨드) · FR-09(축척바 옆 배지). MapViewModel partial 분리 — 본체 파일 충돌 최소화(다중세션).
   Note         : ★ 상태의 SSOT 는 GMapCustomControl(IsTiltFeatureEnabled·RequestedTiltDeg·TiltDeg·TiltState/Reason).
                    VM 은 읽기 래퍼 + 통지(TiltDecided 구독) + 영속만 담당한다. 표시 문구는 TiltUiText(순수).
                  ★ 복원 중 저장 억제(_suppressTiltSave) — 회전 영속 자가오염(44bc6fc)·나침반(_suppressCompassSave) 교훈:
                    복원 경로에서 change-핸들러가 반적용 상태를 영속화하지 않게 항상 억제 플래그.
                  ★ 복원은 ConfigureCommonMapSettings 의 SetInitialHomePosition 이후(홈 줌 적용 뒤 FlushTiltReevaluation — FR-03).
                  ★ 저장: 토글 즉시 · 각도 debounce 1 s(회전 QueueRotationStateSave 선례) · 비활성화 시 플러시.
                  ★ MainMap.TiltDecided 구독은 ConfigureCommonMapSettings 마다 −=/+= 로 멱등(본체의 OnMapZoomChanged 등과 동형).
                    VM(SingleInstance)·MainMap 모두 앱 수명이라 생성자 구독/Deactivate 해제 비대칭 함정 없음.
   Created On   : 2026-09-08 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel
{
    private bool _suppressTiltSave;
    private System.Windows.Threading.DispatcherTimer? _tiltSaveTimer;
    private bool _lastSavedTiltEnabled;
    private double _lastSavedTiltAngle = double.NaN;
    private bool _isTiltAnglePopupOpen;
    /// <summary>직전 커밋 판정의 게이트 ON 여부(TiltDecision.Active) — 툴팁 "적용 중/유지 밴드" 판정 원천.</summary>
    private bool _lastTiltActive;

    private RelayCommand? _toggleTiltFeatureCommand;
    private RelayCommand? _stepTiltAngleCommand;

    // ── 표시 상태(컨트롤 읽기 래퍼) ──

    /// <summary>틸트 kill-switch(툴바 토글 IsChecked OneWay · 보기 메뉴 IsCheckable TwoWay). 세터는 메뉴 경로 —
    /// 컨트롤 DP 를 바꾸고 즉시 저장한다. 툴바/단축키는 <see cref="ToggleTiltFeatureCommand"/>.</summary>
    public bool IsTiltFeatureEnabled
    {
        get => MainMap?.IsTiltFeatureEnabled ?? false;
        set
        {
            if (MainMap == null || MainMap.IsTiltFeatureEnabled == value) return;
            if (!IsTiltToggleEnabled)
            {
                _log?.Info("[Tilt] 토글 잠금 — 사이트 고정(앵커 A모드) 중(앵커 해제 후 변경 가능)");
                NotifyOfPropertyChange(nameof(IsTiltFeatureEnabled));   // 메뉴 체크 원복
                return;
            }
            MainMap.IsTiltFeatureEnabled = value;
            AfterTiltFeatureChanged(openPopup: false, cause: "menu");
        }
    }

    /// <summary>사용자 요청 각(도, 슬라이더 TwoWay). 세터 → 컨트롤 RequestedTiltDeg(코어스 0~35, 라이브는 컨트롤이 재대입) + 1 s debounce 저장.</summary>
    public double TiltAngleDeg
    {
        get => MainMap?.RequestedTiltDeg ?? MapTiltModel.DefaultAngleDeg;
        set
        {
            if (MainMap == null) return;
            double max = TiltMaxAngleDeg;
            double next = TiltMath.ClampAngle(value, max);
            if (Math.Abs(next - MainMap.RequestedTiltDeg) < TiltOverscanMath.AngleEpsilon)
            {
                NotifyOfPropertyChange(nameof(TiltAngleDeg));   // 범위 밖 입력 → 슬라이더 원복
                return;
            }
            MainMap.RequestedTiltDeg = next;
            NotifyOfPropertyChange(nameof(TiltAngleDeg));
            StepTiltAngleCommand.RaiseCanExecuteChanged();
            QueueTiltAngleSave();
        }
    }

    /// <summary>슬라이더 상한 = TiltSettings.MaxAngleDeg(기본 35, 하드 캡 35).</summary>
    public double TiltMaxAngleDeg
        => TiltMath.ClampAngle(MainMap?.TiltSettings.MaxAngleDeg ?? MapTiltModel.HardMaxAngleDeg, MapTiltModel.HardMaxAngleDeg);

    /// <summary>적용 φ(도, 읽기) — 판정 결과. 배지·툴팁 원천.</summary>
    public double TiltAppliedDeg => MainMap?.TiltDeg ?? 0.0;

    /// <summary>축척바 옆 배지 문구 "기울임 20°"(FR-09) — φ_applied &gt; 0.1° 만.</summary>
    public string TiltBadgeText => TiltUiText.BadgeText(TiltAppliedDeg);

    /// <summary>배지 표시 여부(FR-09).</summary>
    public bool IsTiltBadgeVisible => TiltUiText.IsBadgeVisible(TiltAppliedDeg);

    /// <summary>툴바 토글/메뉴 활성 — 앵커 A모드(정북 고정)가 아닐 때(FR-08 ①, G3). 앵커 적용 경로(ApplyMapAnchor)가 <see cref="NotifyTiltToggleState"/> 로 통지.</summary>
    public bool IsTiltToggleEnabled
        => TiltUiText.IsToggleEnabled(MainMap?.IsAnchorActive ?? false, _setupModel?.MapAnchor?.AllowRotation ?? true);

    /// <summary>툴바 토글 툴팁(ShowOnDisabled — 잠금·미달 사유 포함). 사유 키 → 한국어 매핑은 TiltUiText.</summary>
    public string TiltToggleToolTip
        => TiltUiText.ToggleToolTip(
            IsTiltFeatureEnabled,
            MainMap?.TiltReason,
            _lastTiltActive,                                   // 게이트 ON 여부는 decision.Active(State==Active 비교 금지 — Hold 도 Active 일 수 있다)
            TiltAppliedDeg,
            MainMap?.EffectiveZoom ?? double.NaN,
            MainMap?.TiltSettings.MinZoom ?? MapTiltModel.DefaultMinZoom,
            !IsTiltToggleEnabled);

    /// <summary>각도 팝업(슬라이더) 열림 — 툴바 토글 ON 시 자동 열림, OFF 시 닫힘, 각도 버튼 TwoWay.</summary>
    public bool IsTiltAnglePopupOpen
    {
        get => _isTiltAnglePopupOpen;
        set
        {
            if (_isTiltAnglePopupOpen == value) return;
            _isTiltAnglePopupOpen = value && IsTiltFeatureEnabled;   // OFF 상태에선 열지 않는다
            NotifyOfPropertyChange(nameof(IsTiltAnglePopupOpen));
        }
    }

    // ── 커맨드 ──

    /// <summary>툴바 토글·Ctrl+Shift+T — 컨트롤 ToggleTiltFeature(단일 진입점) 경유. 앵커 A모드면 잠금.</summary>
    public RelayCommand ToggleTiltFeatureCommand
        => _toggleTiltFeatureCommand ??= new RelayCommand(_ =>
        {
            if (MainMap == null) return;
            if (!IsTiltToggleEnabled)
            {
                _log?.Info("[Tilt] 토글 잠금 — 사이트 고정(앵커 A모드) 중(앵커 해제 후 변경 가능)");
                NotifyOfPropertyChange(nameof(IsTiltFeatureEnabled));
                return;
            }
            MainMap.ToggleTiltFeature();
            AfterTiltFeatureChanged(openPopup: MainMap.IsTiltFeatureEnabled, cause: "toolbar");
        }, _ => MainMap != null && IsTiltToggleEnabled);

    /// <summary>각도 ±스텝(팝업 버튼, 파라미터 "5"/"-5"/"1"/"-1") — 컨트롤 StepTiltAngle(0~MaxAngleDeg 클램프) 경유 + debounce 저장.</summary>
    public RelayCommand StepTiltAngleCommand
        => _stepTiltAngleCommand ??= new RelayCommand(p =>
        {
            if (MainMap == null || !TryParseStep(p, out double delta)) return;
            MainMap.StepTiltAngle(delta);
            NotifyOfPropertyChange(nameof(TiltAngleDeg));
            StepTiltAngleCommand.RaiseCanExecuteChanged();
            QueueTiltAngleSave();
        }, p => MainMap != null && IsTiltFeatureEnabled && TryParseStep(p, out double delta)
                && Math.Abs(TiltUiText.StepAngle(MainMap.RequestedTiltDeg, delta, TiltMaxAngleDeg) - MainMap.RequestedTiltDeg) > TiltOverscanMath.AngleEpsilon);

    private RelayCommand? _toggleTiltAnglePopupCommand;
    /// <summary>각도 팝업 열기/닫기(팝업 내 닫기 버튼).</summary>
    public RelayCommand ToggleTiltAnglePopupCommand
        => _toggleTiltAnglePopupCommand ??= new RelayCommand(_ => IsTiltAnglePopupOpen = !IsTiltAnglePopupOpen);

    private static bool TryParseStep(object? parameter, out double delta)
    {
        switch (parameter)
        {
            case double d: delta = d; return double.IsFinite(d) && d != 0;
            case int i: delta = i; return i != 0;
            case string s when double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v):
                delta = v; return double.IsFinite(v) && v != 0;
            default: delta = 0; return false;
        }
    }

    // ── 복원(FR-07) ──

    /// <summary>부팅/맵 전환 복원 — ConfigureCommonMapSettings 에서 SetInitialHomePosition 이후 호출(홈 줌 적용 뒤 판정, FR-03).
    /// 키 부재 → 기본값(SIM-P003), 범위 밖 → Normalize 클램프+경고(SIM-P002). 저장 억제 내장.</summary>
    private void LoadMapTiltFromSettings()
    {
        if (MainMap == null) return;
        SubscribeTiltDecided();

        var tilt = _setupModel?.MapTilt ?? new MapTiltModel();
        var warnings = tilt.Normalize(MainMap.MinZoom, MainMap.MaxZoom, _log);
        _suppressTiltSave = true;
        try
        {
            MainMap.TiltSettings = tilt;                 // 정착기 재생성 + 재평가 예약
            MainMap.RequestedTiltDeg = tilt.AngleDeg;
            MainMap.IsTiltFeatureEnabled = tilt.IsEnabled;
            MainMap.FlushTiltReevaluation();             // 홈 줌 적용 뒤 즉시 커밋(부팅 복원 규칙)
            _lastSavedTiltEnabled = tilt.IsEnabled;
            _lastSavedTiltAngle = tilt.AngleDeg;
            if (_setupModel != null) _setupModel.MapTilt = tilt;   // 정규화된 값을 인메모리 SSOT 에도 반영(다음 저장 시 클램프값 기록)
        }
        finally { _suppressTiltSave = false; }
        IsTiltAnglePopupOpen = false;
        NotifyTiltProperties();
        _log?.Info($"[Tilt] 상태 복원: {tilt}{(warnings.Count > 0 ? $" (경고 {warnings.Count}건 — 클램프 적용)" : string.Empty)}");
    }

    private void SubscribeTiltDecided()
    {
        if (MainMap == null) return;
        MainMap.TiltDecided -= OnMainMapTiltDecided;   // 멱등(ConfigureCommonMapSettings 재호출)
        MainMap.TiltDecided += OnMainMapTiltDecided;
        // RequestedTiltDeg 는 컨트롤 키보드 경로(Ctrl+↑/↓)로도 바뀐다 — 게이트 비활성(Below)이면 TiltDecided 가 안 뜨므로 DP 변경으로 직접 debounce 저장(FR-07, 리뷰 잔여)
        var dpd = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(GMapCustoms.GMapCustomControl.RequestedTiltDegProperty, typeof(GMapCustoms.GMapCustomControl));
        dpd?.RemoveValueChanged(MainMap, OnMainMapRequestedTiltChanged);
        dpd?.AddValueChanged(MainMap, OnMainMapRequestedTiltChanged);
    }

    private void OnMainMapRequestedTiltChanged(object? sender, EventArgs e)
    {
        NotifyOfPropertyChange(nameof(TiltAngleDeg));
        StepTiltAngleCommand.RaiseCanExecuteChanged();
        QueueTiltAngleSave();   // _suppressTiltSave 가드는 내부에서
    }

    /// <summary>코얼레싱 판정 커밋 통지(프레임당 ≤1) — 표시 갱신 + 키보드/외부 경로로 바뀐 kill-switch 즉시 저장.</summary>
    private void OnMainMapTiltDecided(object? sender, TiltDecision decision)
    {
        _lastTiltActive = decision.Active;
        NotifyTiltProperties();
        if (MainMap != null && !MainMap.IsTiltFeatureEnabled) IsTiltAnglePopupOpen = false;
        if (!_suppressTiltSave && MainMap != null && MainMap.IsTiltFeatureEnabled != _lastSavedTiltEnabled)
            _ = SaveMapTiltState();
        else
            QueueTiltAngleSave();   // 컨트롤 키보드 경로(Ctrl(+Shift)+↑/↓ → StepTiltAngle)로 바뀐 요청각도 1 s debounce 저장(FR-07) — 저장각과 같으면 내부에서 무동작
    }

    private void NotifyTiltProperties()
    {
        NotifyOfPropertyChange(nameof(IsTiltFeatureEnabled));
        NotifyOfPropertyChange(nameof(TiltAngleDeg));
        NotifyOfPropertyChange(nameof(TiltMaxAngleDeg));
        NotifyOfPropertyChange(nameof(TiltAppliedDeg));
        NotifyOfPropertyChange(nameof(TiltBadgeText));
        NotifyOfPropertyChange(nameof(IsTiltBadgeVisible));
        NotifyOfPropertyChange(nameof(TiltToggleToolTip));
        NotifyOfPropertyChange(nameof(IsTiltToggleEnabled));
        ToggleTiltFeatureCommand.RaiseCanExecuteChanged();
        StepTiltAngleCommand.RaiseCanExecuteChanged();
    }

    /// <summary>앵커 적용/해제(ApplyMapAnchor) 뒤 호출 — A모드 잠금 상태·툴팁 동기(회전 IsRotationToggleEnabled 통지와 동형).</summary>
    private void NotifyTiltToggleState()
    {
        NotifyOfPropertyChange(nameof(IsTiltToggleEnabled));
        NotifyOfPropertyChange(nameof(TiltToggleToolTip));
        ToggleTiltFeatureCommand.RaiseCanExecuteChanged();
    }

    private void AfterTiltFeatureChanged(bool openPopup, string cause)
    {
        NotifyTiltProperties();
        IsTiltAnglePopupOpen = openPopup && IsTiltFeatureEnabled;
        if (!_suppressTiltSave) _ = SaveMapTiltState();   // 토글 즉시 저장(FR-07)
        _log?.Info($"[Tilt] kill-switch {(IsTiltFeatureEnabled ? "ON" : "OFF")} via {cause}");
    }

    // ── 영속(FR-07) ──

    /// <summary>각도 저장 debounce — 슬라이더/키보드 연속 조작 중 매 스텝 파일쓰기 방지(1 s 정지 후 1회, 회전 선례 :2658).</summary>
    private void QueueTiltAngleSave()
    {
        if (_suppressTiltSave || MainMap == null) return;
        if (double.IsFinite(_lastSavedTiltAngle) && Math.Abs(MainMap.RequestedTiltDeg - _lastSavedTiltAngle) < 0.01) return;
        if (_tiltSaveTimer == null)
        {
            _tiltSaveTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.0) };
            _tiltSaveTimer.Tick += (_, __) => { _tiltSaveTimer!.Stop(); _ = SaveMapTiltState(); };
        }
        _tiltSaveTimer.Stop();
        _tiltSaveTimer.Start();   // 재시작 = debounce
    }

    /// <summary>비활성화(종료) 경로 — debounce 대기 중인 각도 저장을 즉시 플러시(회전 선례, 파일쓰기 절단 방지를 위해 await).</summary>
    private async Task FlushPendingTiltSaveAsync()
    {
        if (_tiltSaveTimer?.IsEnabled != true) return;
        _tiltSaveTimer.Stop();
        await SaveMapTiltState();
    }

    /// <summary>현재 컨트롤 상태(kill-switch·요청각·게이트 설정)를 AppSettings.MapTilt 에 저장. 일반 경로는 fire-and-forget, 종료 플러시는 await.</summary>
    private Task SaveMapTiltState()
    {
        if (_suppressTiltSave || MainMap == null) return Task.CompletedTask;
        try
        {
            var settings = MainMap.TiltSettings;
            var model = new MapTiltModel
            {
                IsEnabled = MainMap.IsTiltFeatureEnabled,
                AngleDeg = MainMap.RequestedTiltDeg,
                MinZoom = settings.MinZoom,
                HysteresisSteps = settings.HysteresisSteps,
                MaxAngleDeg = settings.MaxAngleDeg,
            };
            _lastSavedTiltEnabled = model.IsEnabled;
            _lastSavedTiltAngle = model.AngleDeg;
            if (_setupModel != null) _setupModel.MapTilt = model;
            var task = MapSettingsHelper.SaveMapTiltAsync(model, _log);
            _log?.Info($"[Tilt] 상태 저장: {model}");
            return task;
        }
        catch (Exception ex)
        {
            _log?.Error($"[Tilt] 상태 저장 실패: {ex.Message}");
            return Task.CompletedTask;
        }
    }
}
