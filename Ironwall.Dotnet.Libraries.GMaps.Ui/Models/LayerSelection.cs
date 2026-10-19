using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Models;

/// <summary>
/// 레이어 패널 다중 선택 판정(탐색기 관례) — 클릭 = 그 행만 · Ctrl+클릭 = 더하기/빼기 · Shift+클릭 = 기준 행부터 범위 ·
/// Ctrl+Shift+클릭 = 범위 더하기 · Ctrl+묶음 머리 = 그 묶음 전부 더하기, 키보드 폴백 ↑/↓ · Shift+↑/↓ · Ctrl+↑/↓(초점만) ·
/// Space · Ctrl+Space · Ctrl+A · Esc.
/// 선택 상태는 노드(<see cref="LayerTreeNode.IsSelected"/>)에 둔다 — 화면 밖 · 접힌 묶음 안의 행도 데이터로 남는다.
/// 범위 · 이동 순서는 「보이는 트리 순서」(접힌 묶음 안은 건너뜀)의 <b>데이터 목록</b>에서 찾는다(컨테이너 인덱스 산술 없음).
/// </summary>
public sealed class LayerSelection
{
    [ThreadStatic] private static int _applying;

    /// <summary>프로그램이 선택을 바꾸는 중인가 — 목록(ListBox)의 선택 바인딩 되먹임을 사용자 조작과 가르는 기준.</summary>
    public static bool IsApplying => _applying > 0;

    /// <summary>Shift 범위의 기준 행(마지막으로 클릭 · Ctrl+클릭 · ↑/↓ 한 행).</summary>
    public LayerTreeNode? Anchor { get; private set; }

    /// <summary>키보드 초점 행.</summary>
    public LayerTreeNode? Current { get; private set; }

    /// <summary>선택을 바꾸는 코드는 이 안에서 돈다(<see cref="IsApplying"/>).</summary>
    public static void Apply(Action change)
    {
        _applying++;
        try { change(); }
        finally { _applying--; }
    }

    /// <summary>보이는 트리 순서의 잎 — 접힌 섹션 · 그룹 · 카테고리 안은 넣지 않는다.</summary>
    public static List<LayerTreeNode> VisibleLeaves(IEnumerable<LayerTreeNode> roots)
    {
        var list = new List<LayerTreeNode>();
        void Walk(IEnumerable<LayerTreeNode> nodes)
        {
            foreach (var n in nodes)
            {
                if (n.NodeType == LayerNodeType.Leaf) list.Add(n);
                else if (n.IsExpanded) Walk(n.Children);
            }
        }
        Walk(roots);
        return list;
    }

    /// <summary>선택된 잎 전부(트리 순서, 접힌 묶음 안 포함).</summary>
    public static List<LayerTreeNode> SelectedLeaves(IEnumerable<LayerTreeNode> roots)
        => LayerTreeBuilder.Flatten(roots).Where(n => n.IsSelected).ToList();

    /// <summary>재조회(트리 재구성) 뒤 같은 항목을 찾는 열쇠 — 심볼 Id · 레이어 Id. 둘 다 없으면 null.</summary>
    public static string? KeyOf(LayerTreeNode node)
        => node.Symbol != null ? $"S:{node.Symbol.Id}"
         : node.Model != null ? $"L:{node.Model.LayerType}:{node.Model.Id}"
         : null;

    /// <summary>접힌 조상(카테고리 · 그룹 · 섹션)을 펼쳐 그 행들이 보이게 한다.</summary>
    public static void ExpandToShow(IEnumerable<LayerTreeNode> nodes)
    {
        foreach (var n in nodes)
            for (var p = n.Parent; p != null; p = p.Parent)
                if (!p.IsExpanded) p.IsExpanded = true;
    }

    /// <summary>행 클릭. 반환 = 선택이 바뀌었는가.</summary>
    public bool Click(IEnumerable<LayerTreeNode> roots, LayerTreeNode node, ModifierKeys mods)
    {
        if (node.NodeType != LayerNodeType.Leaf) return false;
        bool ctrl = (mods & ModifierKeys.Control) != 0, shift = (mods & ModifierKeys.Shift) != 0;
        var before = Snapshot(roots);
        Apply(() =>
        {
            if (shift) SelectRange(roots, node, add: ctrl);
            else if (ctrl) { node.IsSelected = !node.IsSelected; Anchor = node; }
            else { ClearAll(roots); node.IsSelected = true; Anchor = node; }
        });
        SetCurrent(node);
        return !before.SetEquals(Snapshot(roots));
    }

    /// <summary>Ctrl+묶음 머리(그룹 · 카테고리) — 그 안 잎 전부를 더한다(접혀 있어도). 반환 = 선택이 바뀌었는가.</summary>
    public bool AddGroup(IEnumerable<LayerTreeNode> roots, LayerTreeNode header)
    {
        if (header.NodeType is not (LayerNodeType.Group or LayerNodeType.Category)) return false;
        var before = Snapshot(roots);
        Apply(() => { foreach (var leaf in LayerTreeBuilder.Flatten(header.Children)) leaf.IsSelected = true; });
        return !before.SetEquals(Snapshot(roots));
    }

    /// <summary>선택 해제. 반환 = 선택이 바뀌었는가.</summary>
    public bool Clear(IEnumerable<LayerTreeNode> roots)
    {
        var before = Snapshot(roots);
        Apply(() => ClearAll(roots));
        return before.Count > 0;
    }

    /// <summary>
    /// 키보드 폴백. 반환 = 이 키를 받았는가(<paramref name="changed"/> = 선택이 바뀌었는가).
    /// ↑/↓ = 한 행 이동 + 그 행만 · Shift+↑/↓ = 기준 행부터 범위 · Ctrl+↑/↓ = 초점만 이동 · Space = 초점 행만 ·
    /// Ctrl+Space = 초점 행 더하기/빼기 · Ctrl+A = 보이는 잎 전부 · Esc = 해제(고른 것이 없으면 받지 않는다).
    /// </summary>
    public bool HandleKey(IEnumerable<LayerTreeNode> roots, Key key, ModifierKeys mods, out bool changed)
    {
        changed = false;
        bool ctrl = (mods & ModifierKeys.Control) != 0, shift = (mods & ModifierKeys.Shift) != 0;
        if ((mods & ModifierKeys.Alt) != 0) return false;    // Alt+↑/↓ = 순서 바꾸기(ReorderKeyboardBehavior)

        switch (key)
        {
            case System.Windows.Input.Key.Up:
            case System.Windows.Input.Key.Down:
            {
                var visible = VisibleLeaves(roots);
                if (visible.Count == 0) return false;
                int i = Current == null ? -1 : visible.IndexOf(Current);
                int next = i < 0
                    ? (key == System.Windows.Input.Key.Down ? 0 : visible.Count - 1)
                    : Math.Clamp(i + (key == System.Windows.Input.Key.Down ? 1 : -1), 0, visible.Count - 1);
                var target = visible[next];
                if (ctrl && !shift) { SetCurrent(target); return true; }
                changed = Click(roots, target, shift ? (ctrl ? ModifierKeys.Control | ModifierKeys.Shift : ModifierKeys.Shift) : ModifierKeys.None);
                return true;
            }
            case System.Windows.Input.Key.Space:
                if (Current == null) return false;
                changed = Click(roots, Current, ctrl ? ModifierKeys.Control : ModifierKeys.None);
                return true;
            case System.Windows.Input.Key.A when ctrl:
            {
                var before = Snapshot(roots);
                var visible = VisibleLeaves(roots);
                Apply(() =>
                {
                    ClearAll(roots);
                    foreach (var n in visible) n.IsSelected = true;
                });
                changed = !before.SetEquals(Snapshot(roots));
                return true;
            }
            case System.Windows.Input.Key.Escape:
                if (ctrl || shift) return false;
                changed = Clear(roots);
                return changed;
        }
        return false;
    }

    /// <summary>트리를 다시 만든 뒤 기준 · 초점 행을 같은 항목(<see cref="KeyOf"/>)으로 옮긴다. 없으면 비운다.</summary>
    public void Remap(IEnumerable<LayerTreeNode> roots)
    {
        var leaves = LayerTreeBuilder.Flatten(roots).ToList();
        LayerTreeNode? Find(LayerTreeNode? old)
        {
            if (old == null) return null;
            if (leaves.Contains(old)) return old;
            var key = KeyOf(old);
            return key == null ? null : leaves.FirstOrDefault(n => KeyOf(n) == key);
        }
        Anchor = Find(Anchor);
        var current = Find(Current);
        if (Current != null) Current.IsCurrent = false;
        Current = null;
        if (current != null) SetCurrent(current);
    }

    private void SelectRange(IEnumerable<LayerTreeNode> roots, LayerTreeNode node, bool add)
    {
        var visible = VisibleLeaves(roots);
        int b = visible.IndexOf(node);
        int a = Anchor == null ? -1 : visible.IndexOf(Anchor);
        if (!add) ClearAll(roots);
        if (a < 0 || b < 0)
        {
            // 기준 행이 없거나(처음) 접힌 묶음 안으로 숨었다 → 그 행 하나에서 다시 시작
            node.IsSelected = true;
            Anchor = node;
            return;
        }
        for (int i = Math.Min(a, b); i <= Math.Max(a, b); i++) visible[i].IsSelected = true;
    }

    private void SetCurrent(LayerTreeNode node)
    {
        if (ReferenceEquals(Current, node)) { node.IsCurrent = true; return; }
        if (Current != null) Current.IsCurrent = false;
        Current = node;
        node.IsCurrent = true;
    }

    private static void ClearAll(IEnumerable<LayerTreeNode> roots)
    {
        foreach (var n in LayerTreeBuilder.Flatten(roots)) n.IsSelected = false;
    }

    private static HashSet<LayerTreeNode> Snapshot(IEnumerable<LayerTreeNode> roots)
        => new(LayerTreeBuilder.Flatten(roots).Where(n => n.IsSelected));
}
