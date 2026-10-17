using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;

/// <summary>키 하나가 무엇을 뜻하는가.</summary>
public enum DialogKeyAction
{
    /// <summary>이 창의 일이 아니다 — 그대로 흘려보낸다.</summary>
    None,
    /// <summary>주 동작(저장 · 확인).</summary>
    Primary,
    /// <summary>취소 — 창이 닫힌다.</summary>
    Cancel,
}

/// <summary>
/// ESC · Enter 판정 — <b>순수 함수</b>. 창 없이 시험한다.
/// </summary>
/// <remarks>
/// <para><b>Enter</b> 는 주 동작이 켜져 있고(<paramref name="isPrimaryEnabled"/>) 포커스가 <b>여러 줄 입력칸이 아닐 때만</b> 주 동작이다 —
/// 메모 칸에서 줄을 바꾸려는 Enter 가 저장으로 새는 것을 막는다.</para>
/// <para><b>ESC</b> 는 언제나 취소다. 단 창이 <paramref name="isBusy"/> 면 주 동작만 막고 취소는 남긴다 —
/// 진행 팝업에 닫기 요소가 없어 재시작 말고는 나갈 길이 없던 결함(스토리보드 L1450)을 되풀이하지 않는다.</para>
/// <para><c>IsCancel=True</c> 와 클릭 처리기를 같이 걸면 두 번 닫힌다 — 그래서 닫기는 이 한 규칙으로만 판정한다.</para>
/// </remarks>
public static class DialogKeyRules
{
    /// <summary>
    /// <see cref="Decide(Key, bool, bool, bool)"/> 에 "몸 안 요소가 마우스를 잡고 끄는 중" 을 더한 판정 — 끄는 중의 ESC · Enter 는 창의 일이 아니다
    /// (끄는 요소가 받아 원위치 · 확정한다, drag-first ③). 끌지 않을 때는 지금과 같다.
    /// </summary>
    public static DialogKeyAction Decide(Key key, bool isPrimaryEnabled, bool focusIsMultiLineText, bool isBusy, bool innerDragActive)
    {
        if (innerDragActive && key is Key.Escape or Key.Enter) return DialogKeyAction.None;
        return Decide(key, isPrimaryEnabled, focusIsMultiLineText, isBusy);
    }

    public static DialogKeyAction Decide(Key key, bool isPrimaryEnabled, bool focusIsMultiLineText, bool isBusy)
    {
        if (key == Key.Escape) return DialogKeyAction.Cancel;
        if (key != Key.Enter) return DialogKeyAction.None;
        if (isBusy || !isPrimaryEnabled || focusIsMultiLineText) return DialogKeyAction.None;
        return DialogKeyAction.Primary;
    }
}

/// <summary>처음 포커스가 어디에 가는가.</summary>
public enum DialogFocusTarget
{
    /// <summary>줄 것이 없다 — 뿌리에 둔다.</summary>
    Root,
    Primary,
    Secondary,
    Close,
}

/// <summary>
/// 첫 포커스 — T4 는 <b>취소가 기본</b>이다(스토리보드 T4 정의 L1502: "기본 포커스는 취소").
/// 파괴적 동작이 Enter 한 번에 나가지 않게 하는 규칙이라 규격이 아니라 안전이다.
/// </summary>
public static class DialogFocusRules
{
    public static DialogFocusTarget InitialTarget(bool hasSecondary, bool hasPrimary, bool hasClose)
    {
        if (hasSecondary) return DialogFocusTarget.Secondary;
        if (hasClose) return DialogFocusTarget.Close;
        if (hasPrimary) return DialogFocusTarget.Primary;
        return DialogFocusTarget.Root;
    }
}
