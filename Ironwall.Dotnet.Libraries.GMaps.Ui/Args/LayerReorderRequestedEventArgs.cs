using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using System;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Args;

/// <summary>
/// 오버레이 레이어 순서 바꾸기 요청(D-36) — 끌기 · Alt+↑↓ · 우클릭 '위로/아래로' 가 전부 이 하나로 온다.
/// </summary>
/// <remarks>
/// <see cref="NewOrder"/> 는 <see cref="Section"/> 의 자식 <b>전부</b>를 새 순서로 늘어놓은 것이다(옮긴 행만이 아니다).
/// 받는 쪽은 이 순서를 제자리에서 적용하고 ZOrder 를 한 번에 기록한다.
/// </remarks>
public class LayerReorderRequestedEventArgs : EventArgs
{
    public LayerReorderRequestedEventArgs(LayerTreeNode section, IReadOnlyList<LayerTreeNode> newOrder, string source)
    {
        Section = section;
        NewOrder = newOrder;
        Source = source;
    }

    /// <summary>순서를 바꾸는 오버레이 섹션(OVERLAY MAP · OVERLAY IMAGE).</summary>
    public LayerTreeNode Section { get; }

    /// <summary>섹션 자식 전부의 새 순서(위→아래 = ZOrder 오름차순).</summary>
    public IReadOnlyList<LayerTreeNode> NewOrder { get; }

    /// <summary>어느 경로로 왔는지(로그용) — "list"(끌기 · Alt+↑↓, 커널이 같은 담당을 부른다) · "menu"(우클릭 위로/아래로).</summary>
    public string Source { get; }
}
