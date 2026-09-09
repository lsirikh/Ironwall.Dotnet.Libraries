using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Door;

/****************************************************************************
   Purpose      : 통문·함체 개폐 명령 게이트(순수 상태기계) — PRD symbol-detail-and-door-control FR-03/04, NFR-02
   Created By   : Claude Code
   Created On   : 2026-09-08
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>개폐 버튼이 화면에 보여야 하는 상태.</summary>
public enum DoorUiState
{
    /// <summary>상태를 아직 못 받았다 — 두 버튼 비활성 + "상태 미수신(?)"(FR-05).</summary>
    Unknown,
    /// <summary>닫힘 확정.</summary>
    Closed,
    /// <summary>열림 확정.</summary>
    Open,
    /// <summary>명령을 보냈고 <c>OPERATION_EVENT</c> 를 기다리는 중 — 두 버튼 비활성(FR-04).</summary>
    Pending,
}

/// <summary>
/// 개폐 명령의 화면 상태를 관리하는 <b>순수</b> 상태기계(WPF·시간 API 무의존 — 시각은 호출자가 주입).
///
/// <para><b>왜 필요한가</b>: 서버는 개폐 명령을 받아 NATS 로 전파만 하고 <b>상태를 바꾸지 않는다</b>.
/// *"명령 접수만으로 상태를 낙관적으로 기록하면 구동이 실패했을 때 DB 가 거짓말을 하고 GIS 는 열리지도
/// 않은 통문을 열렸다고 표시한다"*(서버 <c>gates.py:304</c>). 따라서 버튼을 눌러도 문짝을 움직이면 안 되고,
/// 실제 전이는 담당 매니저 보고 → <c>OPERATION_EVENT</c> 로만 온다.</para>
///
/// <para><b>설계</b>: <see cref="DoorUiState.Pending"/> 은 <b>화면 전용</b> 상태다 — 모델의
/// <c>DoorState</c>(접점·SYNC_DEVICE·OPERATION_EVENT 3채널 수렴)에는 넣지 않는다. 이 클래스는
/// "확정 상태"(<see cref="ReportedState"/>)와 "대기 여부"를 분리해 들고, 화면에 보일 값만 합성한다.</para>
/// </summary>
public sealed class DoorCommandGate
{
    /// <summary>응답 대기 한도(초). 매니저가 명령을 못 받으면 Pending 이 영원히 안 풀리므로 반드시 필요하다(PRD R-2).</summary>
    public const double DefaultTimeoutSec = 15.0;

    private readonly double _timeoutSec;
    private DateTime? _pendingSince;

    public DoorCommandGate(double timeoutSec = DefaultTimeoutSec)
        => _timeoutSec = timeoutSec > 0 ? timeoutSec : DefaultTimeoutSec;

    /// <summary>마지막으로 <b>보고받은</b> 확정 상태. 명령으로는 절대 바뀌지 않는다.</summary>
    public DoorUiState ReportedState { get; private set; } = DoorUiState.Unknown;

    /// <summary>대기 중 보낸 명령(재클릭 무시·로그용). 대기 중이 아니면 null.</summary>
    public string? PendingCommand { get; private set; }

    /// <summary>응답 대기 중인가.</summary>
    public bool IsPending => _pendingSince.HasValue;

    /// <summary>직전 대기가 타임아웃으로 끝났는가 — 화면 경고 표기용. 다음 명령·보고에서 해제된다.</summary>
    public bool LastCommandTimedOut { get; private set; }

    /// <summary>화면에 보일 상태 — 대기 중이면 <see cref="DoorUiState.Pending"/>, 아니면 확정 상태.</summary>
    public DoorUiState DisplayState => IsPending ? DoorUiState.Pending : ReportedState;

    /// <summary>
    /// 버튼을 눌러도 되는가 — 대기 중이거나 상태 미수신이면 안 된다(FR-04·FR-05).
    /// 장비 미연결·권한 없음은 이 클래스가 모르므로 호출자가 <c>AND</c> 로 결합한다(FR-06·FR-07).
    /// </summary>
    public bool CanSend => !IsPending && ReportedState != DoorUiState.Unknown;

    /// <summary>
    /// 명령 발행을 시작한다. 수용되면 true(호출자가 REST 를 호출한다), 거부되면 false.
    /// <para>거부 조건: 대기 중(연타) · 상태 미수신 · 이미 그 상태(닫힘인데 CLOSE) — 불필요한 왕복을 막는다.</para>
    /// </summary>
    public bool TryBegin(string command, DateTime now)
    {
        if (!CanSend) return false;
        var target = ToState(command);
        if (target == DoorUiState.Unknown) return false;      // 알 수 없는 명령어
        if (target == ReportedState) return false;            // 이미 그 상태 — no-op
        _pendingSince = now;
        PendingCommand = command;
        LastCommandTimedOut = false;
        return true;
    }

    /// <summary>
    /// 상태 보고 수신(<c>OPERATION_EVENT</c> · <c>SYNC_DEVICE</c> · 접점). 대기 여부와 무관하게 <b>항상</b> 반영한다 —
    /// 서버/현장이 권위이므로 우리가 보낸 명령과 다른 값이 와도 그대로 따른다.
    /// </summary>
    public void OnStateReported(DoorUiState reported)
    {
        ReportedState = reported;
        _pendingSince = null;
        PendingCommand = null;
        LastCommandTimedOut = false;
    }

    /// <summary>
    /// 시각 경과를 알린다(타이머 틱). 대기가 한도를 넘었으면 대기를 풀고 true 를 돌려준다 —
    /// 호출자는 경고를 표시한다. <b>확정 상태는 건드리지 않는다</b>(모르는 것을 지어내지 않는다).
    /// </summary>
    public bool Tick(DateTime now)
    {
        if (_pendingSince is not DateTime since) return false;
        if ((now - since).TotalSeconds < _timeoutSec) return false;
        _pendingSince = null;
        PendingCommand = null;
        LastCommandTimedOut = true;
        return true;
    }

    /// <summary>마커 교체·패널 닫힘 — 대기와 경고를 버린다(확정 상태는 다음 로드에서 다시 받는다).</summary>
    public void Reset()
    {
        _pendingSince = null;
        PendingCommand = null;
        LastCommandTimedOut = false;
        ReportedState = DoorUiState.Unknown;
    }

    /// <summary>서버 명령 문자열("OPEN"/"CLOSE") → 그 명령이 노리는 상태. 그 외는 <see cref="DoorUiState.Unknown"/>.</summary>
    public static DoorUiState ToState(string? command)
        => command?.Trim().ToUpperInvariant() switch
        {
            "OPEN" => DoorUiState.Open,
            "CLOSE" or "CLOSED" => DoorUiState.Closed,
            _ => DoorUiState.Unknown,
        };
}
