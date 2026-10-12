using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Caliburn.Micro;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 시험이 잠시 끼우는 Caliburn <see cref="PlatformProvider"/> — 만든 STA 스레드에서 부른 것만 그 디스패처로 보내고,
/// 다른 시험의 실행 흐름에서 부른 것은 기본 제공자(<see cref="DefaultPlatformProvider"/>)처럼 그 자리에서 실행한다.
/// </summary>
/// <remarks>
/// <para><b>왜</b>: <see cref="PlatformProvider.Current"/> 는 프로세스 전역이다. 맨 <see cref="XamlPlatformProvider"/> 를 끼우면
/// 그 사이 나란히 도는 다른 시험 클래스의 STA 스레드까지 <c>NotifyOfPropertyChange</c> 를 <b>이 시험 스레드의 디스패처</b>로 보낸다 —
/// 남의 객체 접근(InvalidOperationException) · 닫힌 디스패처(TaskCanceledException)로 순서에 따라 깨졌다
/// (WIR-100 · WIR-158 · ASM-007 · ASM-009 · ASM-021, 2026-10-11 전체 실행).</para>
/// <para>호출 스레드: 끼운 시험의 STA 스레드(만들 때) — 이후 어느 스레드에서 불려도 된다.</para>
/// <para><b>"이 시험" 의 판정은 스레드가 아니라 실행 흐름</b>이다(<see cref="AsyncLocal{T}"/>) — 끼운 시험이 띄운 작업 스레드
/// (패널 재조회가 ConfigureAwait(false) 뒤 목록을 고치는 길)도 <b>이 디스패처로</b> 보내야 앱과 같다. 스레드로만 가르면 그 작업이
/// 그 자리에서 돌아 UI 스레드의 목록 열거와 겹쳤다(2026-10-12 전체 실행: OnSourceBusyEnded 에서 "Collection was modified" 로 시험 호스트 중단).</para>
/// </remarks>
internal sealed class OwnerThreadPlatformProvider : XamlPlatformProvider
{
    private readonly Dispatcher _owner = Dispatcher.CurrentDispatcher;
    private readonly DefaultPlatformProvider _inline = new();
    private readonly AsyncLocal<bool> _mine = new();

    /// <summary>만든 STA 스레드의 실행 흐름에 표지를 단다 — 이후 그 흐름에서 이어진 작업(Task · await 뒤)은 전부 "이 시험" 이다.</summary>
    public OwnerThreadPlatformProvider() => _mine.Value = true;

    private bool IsMine => _owner.CheckAccess() || _mine.Value;

    public override void BeginOnUIThread(System.Action action)
    {
        if (IsMine) base.BeginOnUIThread(action);
        else _inline.BeginOnUIThread(action);
    }

    public override Task OnUIThreadAsync(Func<Task> action)
        => IsMine ? base.OnUIThreadAsync(action) : _inline.OnUIThreadAsync(action);

    public override void OnUIThread(System.Action action)
    {
        if (IsMine) base.OnUIThread(action);
        else _inline.OnUIThread(action);
    }
}
