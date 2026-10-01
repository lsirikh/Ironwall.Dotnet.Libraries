using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Signals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;

/// <summary>개념도 노드 하나(fence-wiring-editor FR-12) — 체인 위치 · 번호 · IP 표지 · 신호등.</summary>
/// <param name="Position">Ch1(A) 쪽에서 센 자리(1부터).</param>
/// <param name="Number">장비 번호(<c>number_device</c> · 저장 대기 값 포함).</param>
/// <param name="IsNumberChanged">번호가 서버 값과 다른가(저장 대기).</param>
public sealed record ConceptNodeInfo(int Key, int Id, int Position, int Number, string Name, bool IsIp, string Address,
                                     SignalLevel Signal, bool IsSelected, bool IsNumberChanged, bool IsDraft);

/// <summary>
/// 개념도(FR-12) · 통신 신호등(FR-14) · 케이블 보기 토글 — 결선 창 뷰모델의 면.
/// </summary>
/// <remarks>
/// <para>개념도는 체인(= 위치 순서)을 <b>Ch1(A) → … → Ch2(B)</b> 한 줄(가로 띠 · 기본) 또는 원으로 그린다. 모든 제어기가 같은 링 그림이다(§1-0).
/// 개념도에서 순서를 바꾸면 보드의 체인 편집 한 길(<see cref="FencePlace"/>)로 가고, 펜스 위 자리가 따라간다(보드가 맞춘다).</para>
/// <para><b>제어기 신호등</b>은 창이 열린 동안(활성화 ~ 닫힘) 5초마다 ping(<see cref="ControllerPingMonitor"/>) · <b>센서 신호등</b>은
/// 매니저가 보고한 <c>NETWORK_INTERFACE</c> 부품 health(스마트복합센서2 의 IP 는 제어기 뒤 내부망이라 GIS 가 ping 하지 않는다).</para>
/// </remarks>
public sealed partial class WiringViewModel
{
    private bool _isConceptRing;
    private bool _showCables;
    private ControllerPingMonitor? _pingMonitor;
    private SynchronizationContext? _uiContext;

    #region - Cables toggle (FR-12) -
    /// <summary>[케이블 보기] — 펜스 뷰에 리턴케이블 · 함체 · A·B 번호를 겹친다(기본 꺼짐 · 연결은 개념도가 맡는다).</summary>
    public bool ShowCables
    {
        get => _showCables;
        set
        {
            if (_showCables == value) return;
            _showCables = value;
            NotifyOfPropertyChange();
            FenceChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ToggleCables() => ShowCables = !ShowCables;
    #endregion

    #region - Concept (FR-12) -
    /// <summary>원형으로 그리는가(기본은 가로 띠).</summary>
    public bool IsConceptRing
    {
        get => _isConceptRing;
        set
        {
            if (_isConceptRing == value) return;
            _isConceptRing = value;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(IsConceptStrip));
            FenceChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsConceptStrip => !_isConceptRing;

    public void ChooseConceptStrip() => IsConceptRing = false;
    public void ChooseConceptRing() => IsConceptRing = true;

    /// <summary>"개념도 · 링 (Ch1(A) → #1 … #13 → Ch2(B))".</summary>
    public string ConceptTitle => _board.Chain.Count == 0
        ? $"개념도 · 링 ({WiringValidation.PORT_1} → {WiringValidation.PORT_2}) — 붙은 센서가 없습니다"
        : $"개념도 · 링 ({WiringValidation.PORT_1} → #1 … #{_board.Chain.Count} → {WiringValidation.PORT_2})";

    /// <summary>IP 센서가 있는가 — "센서마다 IP(제어기 뒤 내부망)" 표지.</summary>
    public bool HasIpSensors => _board.Chain.Keys.Any(IsIpSensor);

    public const string CONCEPT_IP_NOTE = "스마트복합센서2 = 센서마다 IP (제어기 뒤 내부망)";

    /// <summary>개념도 노드 — 체인 순서(Ch1(A) 쪽 끝이 1).</summary>
    public IReadOnlyList<ConceptNodeInfo> ConceptNodes()
        => _board.Chain.Keys.Select((key, i) =>
        {
            var row = _board.Find(key)!;
            return new ConceptNodeInfo(key, row.Id, i + 1, row.Facts.Number, row.Display, IsIpSensor(key), AddressTextOf(key), SensorSignal(key),
                                       IsFenceSelected(key), row.Facts.Number != row.Baseline.Number,
                                       row.IsNew || row.FactsChanged || !WiringSpec.SameWiring(_board.PlacementOf(key), row.BaselinePlacement));
        }).ToList();

    /// <summary>개념도에서 끌어 놓기 — 체인 틈 <paramref name="gap"/>(옮기기 전 기준)에. 펜스 위 자리 · 번호가 따라간다. 되돌리기 한 걸음.</summary>
    public bool ConceptMove(IReadOnlyList<int> keys, int gap) => FencePlace(keys, WiringSpec.LINE_PRIMARY, gap);

    /// <summary>끄는 동안 알약 글자 — "위치 5 · 번호 105 → 5".</summary>
    public string ConceptDropLabel(IReadOnlyList<int> keys, int gap) => FenceDropLabel(keys, WiringSpec.LINE_PRIMARY, gap);
    #endregion

    #region - Signals (FR-14) -
    /// <summary>제어기 신호등 — ping 이 없거나(주소 없음 · 창 닫힘) 표본이 없으면 모름.</summary>
    public SignalLevel ControllerSignal => _pingMonitor?.Level ?? SignalLevel.Unknown;

    /// <summary>"정상 · 평균 12ms · 손실 0/5" · "모름 — 제어기 주소가 없습니다".</summary>
    public string ControllerSignalText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Controller.Address)) return "모름 — 제어기 주소가 없습니다";
            if (_ping is null) return "모름 — 이 창에서는 ping 을 쓰지 않습니다";
            var summary = _pingMonitor is null ? string.Empty : SignalMath.Summary(_pingMonitor.Samples);
            return summary.Length == 0 ? SignalMath.Text(ControllerSignal) : $"{SignalMath.Text(ControllerSignal)} · {summary}";
        }
    }

    /// <summary>제어기 ping 을 시작한다(창이 열릴 때) — 주소 · ping 이 없으면 하지 않는다. UI 스레드에서 부른다.</summary>
    public void StartSignals()
    {
        if (_ping is null || string.IsNullOrWhiteSpace(Controller.Address)) return;
        _uiContext ??= SynchronizationContext.Current;
        if (_pingMonitor is null)
        {
            _pingMonitor = CreatePingMonitor(_ping);
        }
        _pingMonitor.Start();
    }

    /// <summary>멈춘다(창이 닫힐 때 · 즉시).</summary>
    public void StopSignals() => _pingMonitor?.Stop();

    /// <summary>ping 이 돌고 있는가(시험).</summary>
    internal bool IsPinging => _pingMonitor?.IsRunning == true;

    /// <summary>시계 없이 ping 한 번(시험) — 모니터가 없으면 만든다(돌리지는 않는다).</summary>
    internal async Task<bool> PingControllerOnceAsync()
    {
        if (_ping is null || string.IsNullOrWhiteSpace(Controller.Address)) return false;
        _pingMonitor ??= CreatePingMonitor(_ping);
        return await _pingMonitor.PingOnceAsync();
    }

    /// <summary>모니터 — 로그는 앱 로거로(상태가 "응답 없음"으로 바뀔 때 한 줄), 상태 변화 · 표본마다 UI 로 알린다.</summary>
    private ControllerPingMonitor CreatePingMonitor(IPingProbe ping)
    {
        var monitor = new ControllerPingMonitor(ping, Controller.Address, message => _log?.Warning(message));
        monitor.Changed += OnControllerSignal;
        monitor.Sampled += OnControllerSample;
        return monitor;
    }

    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        StartSignals();
    }

    protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        if (close) StopSignals();
        return base.OnDeactivateAsync(close, cancellationToken);
    }

    /// <summary>모니터가 작업 스레드에서 알린다 — UI 로 넘겨 신호등을 다시 그린다.</summary>
    private void OnControllerSignal(object? sender, SignalLevel level)
    {
        void Raise()
        {
            NotifyOfPropertyChange(nameof(ControllerSignal));
            NotifyOfPropertyChange(nameof(ControllerSignalText));
            FenceChanged?.Invoke(this, EventArgs.Empty);
        }
        if (_uiContext is { } ui && SynchronizationContext.Current != ui) ui.Post(_ => Raise(), null);
        else Raise();
    }

    /// <summary>표본마다(5초에 한 번) — 상태가 같아도 평균 · 손실 글자는 바뀐다. 그림은 다시 그리지 않는다(상태가 바뀔 때만).</summary>
    private void OnControllerSample(object? sender, SignalLevel level)
    {
        void Raise() => NotifyOfPropertyChange(nameof(ControllerSignalText));
        if (_uiContext is { } ui && SynchronizationContext.Current != ui) ui.Post(_ => Raise(), null);
        else Raise();
    }
    #endregion
}
