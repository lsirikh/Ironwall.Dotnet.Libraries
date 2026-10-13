namespace Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;

/// <summary>
/// 대화창 몸 안에서 끌기를 하는 요소(그림 · 캔버스) — 끄는 중이면 <see cref="ConsoleDialogFrame"/> 가 ESC · Enter 를 닫기 · 저장으로 먹지 않고
/// 그 요소로 흘려보낸다(터널 다음 단계에서 그 요소가 원위치 · 확정한다 · drag-first ③). 끌지 않을 때 ESC 는 지금처럼 창 닫기.
/// </summary>
public interface IDialogDragSource
{
    /// <summary>지금 끄는 중인가(데드존을 넘은 뒤).</summary>
    bool IsDragging { get; }
}
