namespace Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;

/****************************************************************************
   Purpose      : 지금 캡처 드래그가 진행 중인가 — 목록을 다시 읽어도 되는지 묻는 쪽(콘솔 뷰모델)이 본다
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 캡처 드래그(<see cref="CaptureDragBehavior"/>)가 손잡이를 잡은 순간부터 놓거나 취소할 때까지 <see cref="IsActive"/> 다.
/// </summary>
/// <remarks>
/// <para>왜 필요한가 — 서버 동기화 알림으로 목록을 다시 읽으면 컨테이너가 바뀌어 <b>끌고 있던 행이 사라진다</b>.
/// 알림을 받은 콘솔은 이 값을 보고 다시 읽기를 미룬다.</para>
/// <para>프로세스 전역 · 스레드 안전(<see cref="Interlocked"/>). 뷰모델은 이 정적 값을 직접 보지 말고
/// <c>Func&lt;bool&gt;</c> 로 주입받아 시험에서 바꿔 끼운다.</para>
/// </remarks>
public static class DragSession
{
    private static int _active;

    /// <summary>진행 중인 캡처 드래그가 하나라도 있다(눌림 포함 — 데드존을 넘기 전에도 곧 끌기가 된다).</summary>
    public static bool IsActive => Volatile.Read(ref _active) > 0;

    internal static void Enter() => Interlocked.Increment(ref _active);

    internal static void Exit()
    {
        // 짝이 안 맞아도 음수로 내려가지 않는다 — 음수면 이후 끌기가 영영 "진행 중 아님" 으로 읽힌다.
        if (Interlocked.Decrement(ref _active) < 0) Interlocked.Exchange(ref _active, 0);
    }
}
