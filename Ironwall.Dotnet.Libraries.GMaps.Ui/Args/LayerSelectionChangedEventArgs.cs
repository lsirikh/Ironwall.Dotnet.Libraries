using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using System;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Args;

/// <summary>
/// 레이어 패널에서 사용자가 선택을 바꿨다(클릭 · Ctrl · Shift · 키보드). 프로그램이 바꾼 선택(지도 → 패널 동기)에는 나지 않는다 — 되먹임 없음.
/// </summary>
public class LayerSelectionChangedEventArgs : EventArgs
{
    public LayerSelectionChangedEventArgs(IReadOnlyList<LayerTreeNode> selected) => Selected = selected;

    /// <summary>지금 선택된 잎 전부(트리 순서).</summary>
    public IReadOnlyList<LayerTreeNode> Selected { get; }
}
