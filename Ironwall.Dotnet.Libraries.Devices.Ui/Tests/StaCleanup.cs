using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 시험용 STA 스레드가 끝나기 전에 그 스레드의 <see cref="Dispatcher"/> 를 닫는다.
/// </summary>
/// <remarks>
/// <para><b>왜</b>: 시험 도우미(<c>OnSta</c> · <c>RunSta</c>)는 시험마다 STA 스레드를 새로 만들고 그 안에서 창을 띄운다.
/// 디스패처를 닫지 않고 스레드가 끝나면 창(HWND)과 디스패처의 숨은 창은 WPF 가 아니라 <b>OS 가 스레드 종료 때</b> 치운다 —
/// <c>HwndSource</c> · <c>HwndSubclass</c> 는 정리되지 못한 채 남는다. 같은 모양을 시험 밖에서 되풀이하면(스레드 7,200개 + GC)
/// 닫지 않은 쪽만 프로세스가 죽었다(6/6) — 닫은 쪽은 끝까지 돌았다.</para>
/// <para><see cref="Dispatcher.InvokeShutdown"/> 은 그 스레드의 <c>HwndSource</c> 를 전부 Dispose 한다(창이 WPF 길로 닫힌다).
/// 디스패처를 만든 적 없는 스레드에서 불러도 된다(그 자리에서 만들어 곧바로 닫는다).</para>
/// <para>호출 스레드: 시험용 STA 스레드 자신 — 본문이 끝난 뒤 <c>finally</c> 에서.</para>
/// </remarks>
internal static class StaCleanup
{
    public static void ShutdownDispatcher() => Dispatcher.CurrentDispatcher.InvokeShutdown();
}
