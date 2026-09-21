using Caliburn.Micro;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Dialogs;

/// <summary>
/// 진행 창 — <b>취소 버튼이 있는</b> T4 · S 400.
/// </summary>
/// <remarks>
/// <para>정본: 스토리보드 #h-dlg T5 표 L1450 — 지금의 공용 진행 팝업은 <c>500×500</c> 에 <b>닫기 요소가 0개</b>라
/// 예외가 나면 전체 화면이 잠겨 재시작 말고는 나갈 길이 없다. 이번 판은 <b>S 400 + 취소 버튼 필수 · 예외 시 자동 해제</b>다.</para>
/// <para>취소는 <see cref="CancellationTokenSource"/> 를 건드릴 뿐 일을 되돌리지 않는다 — 이미 끝난 것은 끝난 것이라
/// 끝맺음은 "중단 — m건 완료"로 적는다. 한 번 누르면 버튼이 꺼진다(두 번 눌러도 할 일이 없다).</para>
/// <para>호출 스레드: <see cref="Report"/> 는 작업 스레드에서 올 수 있다 — 화면에 닿는 값은 <see cref="IPlatformProvider"/> 로 UI 에 올린다.</para>
/// </remarks>
public sealed class ProgressDialogViewModel : Screen, IDisposable
{
    #region - Ctors -
    public ProgressDialogViewModel(string title, string? kind = null)
    {
        DisplayName = title;
        Kind = kind ?? string.Empty;
        _cts = new CancellationTokenSource();
        _message = "시작하는 중…";
    }
    #endregion

    #region - Properties -
    public string Kind { get; }

    /// <summary>일하는 쪽이 받아 가는 취소 토큰.</summary>
    public CancellationToken Token => _cts.Token;

    public int Done { get => _done; private set { _done = value; Notify(); } }
    public int Total { get => _total; private set { _total = value; Notify(); } }

    /// <summary>"7 / 16" — 몇 건 가운데 몇 건.</summary>
    public string CountText => _total > 0 ? $"{_done} / {_total}" : $"{_done}";

    /// <summary>0~100. 총량을 모르면 0 — 화면은 그때 물결 막대를 쓴다.</summary>
    public double Percent => _total > 0 ? Math.Clamp(_done * 100d / _total, 0d, 100d) : 0d;

    public bool IsIndeterminate => _total <= 0 && !IsDone;

    public string Message { get => _message; private set { _message = value ?? string.Empty; NotifyOfPropertyChange(); } }

    public bool IsCancelRequested { get => _cancelRequested; private set { _cancelRequested = value; Notify(); } }
    public bool IsDone { get => _isDone; private set { _isDone = value; Notify(); } }

    /// <summary>누를 수 있는가 — 한 번 누르면 꺼진다.</summary>
    public bool CanCancel => !_cancelRequested && !_isDone;

    /// <summary>끝났으면 창을 닫을 수 있다. 끝나기 전에도 <b>닫기 · ESC 는 언제나 취소</b>로 살아 있다.</summary>
    public bool IsClosable => _isDone;
    #endregion

    #region - Processes -
    /// <summary>진행을 알린다. 작업 스레드에서 불러도 된다.</summary>
    public void Report(int done, int total, string? current = null)
    {
        OnUi(() =>
        {
            _done = done;
            _total = total;
            Message = string.IsNullOrWhiteSpace(current) ? $"{CountText} 진행 중…" : $"{CountText} · {current}";
            Notify();
        });
    }

    /// <summary>[취소] · ESC · 머리의 닫기. 토큰을 끊고 버튼을 끈다 — 이미 끝난 일은 되돌리지 않는다.</summary>
    public void RequestCancel()
    {
        if (_cancelRequested || _isDone) return;
        IsCancelRequested = true;
        Message = "멈추는 중… (보낸 것은 되돌리지 않습니다)";
        try { _cts.Cancel(); }
        catch (ObjectDisposedException) { /* 이미 끝났다 */ }
    }

    /// <summary>
    /// 일이 끝났다(성공 · 실패 · 중단 모두). 끝맺음 문구를 정하고 버튼을 닫기로 바꾼다.
    /// </summary>
    public void Finish(string? summary = null)
    {
        OnUi(() =>
        {
            IsDone = true;
            Message = summary ?? (_cancelRequested ? $"중단 — {_done}건 완료" : $"끝 — {_done}건 완료");
            Notify();
        });
    }

    /// <summary>예외가 새 나와도 창은 반드시 풀린다 — 잠긴 전체 화면을 다시 만들지 않는다.</summary>
    public void Fail(string reason)
    {
        // 사람에게는 까닭만 — 날 예외 글을 그대로 내보내지 않는다.
        OnUi(() => { IsDone = true; Message = reason; Notify(); });
    }

    public Task CloseAsync()
    {
        if (!_isDone) RequestCancel();
        return TryCloseAsync(_isDone && !_cancelRequested);
    }

    public void Dispose()
    {
        try { _cts.Dispose(); } catch (Exception) { /* 두 번 풀려도 할 일이 없다 */ }
    }
    #endregion

    #region - Internals -
    private static void OnUi(System.Action action)
    {
        var platform = PlatformProvider.Current;
        if (platform is null) { action(); return; }
        platform.OnUIThread(action);
    }

    private void Notify()
    {
        NotifyOfPropertyChange(nameof(Done));
        NotifyOfPropertyChange(nameof(Total));
        NotifyOfPropertyChange(nameof(CountText));
        NotifyOfPropertyChange(nameof(Percent));
        NotifyOfPropertyChange(nameof(IsIndeterminate));
        NotifyOfPropertyChange(nameof(IsCancelRequested));
        NotifyOfPropertyChange(nameof(IsDone));
        NotifyOfPropertyChange(nameof(CanCancel));
        NotifyOfPropertyChange(nameof(IsClosable));
        NotifyOfPropertyChange(nameof(PrimaryText));
        NotifyOfPropertyChange(nameof(SecondaryText));
    }

    /// <summary>끝나기 전에는 주 동작이 없다 — 취소만 있다.</summary>
    public string PrimaryText => _isDone ? "닫기" : string.Empty;

    /// <summary>끝난 뒤에는 취소할 것이 없다 — 버튼이 사라지고 [닫기] 하나만 남는다.</summary>
    public string SecondaryText => _isDone ? string.Empty : "취소";
    #endregion

    #region - Attributes -
    private readonly CancellationTokenSource _cts;
    private int _done;
    private int _total;
    private string _message;
    private bool _cancelRequested;
    private bool _isDone;
    #endregion
}
