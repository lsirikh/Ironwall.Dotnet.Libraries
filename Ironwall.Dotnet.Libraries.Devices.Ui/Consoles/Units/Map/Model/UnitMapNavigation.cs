using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;

/****************************************************************************
   Purpose      : 관계도 화살표 선택 이동 — ↑ 상위 · ↓ 첫 하위 · ←/→ 같은 깊이 이웃 · Home 내 부대 (FR-36)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 화살표 선택 이동의 <b>순수 판정</b>(FR-36) — 좌표 없이 전부 된다(키보드 폴백 · UIA 단언의 바탕).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>↑ = 상위(뿌리 · 편제 밖 상위를 가리키는 부대는 제자리) · ↓ = 첫 하위(코드 순 — 트리의 자식 순서) · 끝 부대는 제자리.</item>
/// <item>←/→ = 같은 깊이의 이웃을 <b>화면 위치</b>(Δ 가 입혀진 월드 좌표) x 순, 같은 x 면 y 순으로 — 사용자가 옮긴 그림대로 움직인다(SIM-K153).
/// 위치가 없는 부대는 트리 순서로 뒤에 선다. 가장자리는 제자리.</item>
/// <item><c>Home</c> = 내 부대(모르거나 편제에 없으면 제자리 — 띠 문구는 뷰모델 몫).</item>
/// <item>선택이 없거나 편제에서 사라졌으면 화살표는 첫 뿌리를 고른다.</item>
/// </list>
/// </remarks>
public static class UnitMapNavigation
{
    /// <summary>다음 선택. 이동할 곳이 없으면 <paramref name="current"/> 를 그대로(편제가 비었으면 <c>null</c>).</summary>
    public static int? Next(
        UnitTreeModel tree,
        IReadOnlyDictionary<int, Point> positions,
        int? current,
        UnitMapKeyCommand key,
        int? myUnitId)
    {
        ArgumentNullException.ThrowIfNull(tree);
        positions ??= new Dictionary<int, Point>();

        if (key == UnitMapKeyCommand.Home)
            return myUnitId is int mine && tree.Find(mine) is not null ? mine : current;

        if (key is not (UnitMapKeyCommand.Up or UnitMapKeyCommand.Down or UnitMapKeyCommand.Left or UnitMapKeyCommand.Right))
            return current;

        var node = current is int id ? tree.Find(id) : null;
        if (node is null)
            return tree.Ordered.FirstOrDefault(n => !n.ParentId.HasValue)?.Id ?? tree.Ordered.FirstOrDefault()?.Id;

        switch (key)
        {
            case UnitMapKeyCommand.Up:
                return node.ParentId is int parentId && tree.Find(parentId) is not null ? parentId : node.Id;

            case UnitMapKeyCommand.Down:
                foreach (var childId in node.ChildIds)
                    if (tree.Find(childId) is not null) return childId;
                return node.Id;

            default:
                var row = SameDepthInScreenOrder(tree, positions, node.Depth);
                var index = row.IndexOf(node.Id);
                var step = key == UnitMapKeyCommand.Right ? 1 : -1;
                var target = index + step;
                return target >= 0 && target < row.Count ? row[target] : node.Id;
        }
    }

    /// <summary>같은 깊이의 부대들 — 위치 있는 것은 (x, y) 순, 없는 것은 그 뒤에 트리 순서로.</summary>
    private static List<int> SameDepthInScreenOrder(UnitTreeModel tree, IReadOnlyDictionary<int, Point> positions, int depth)
    {
        var sameDepth = tree.Ordered.Where(n => n.Depth == depth).Select((n, order) => (n.Id, Order: order)).ToList();

        var placed = sameDepth.Where(n => positions.ContainsKey(n.Id))
                              .OrderBy(n => positions[n.Id].X)
                              .ThenBy(n => positions[n.Id].Y)
                              .ThenBy(n => n.Order)
                              .Select(n => n.Id);
        var unplaced = sameDepth.Where(n => !positions.ContainsKey(n.Id)).OrderBy(n => n.Order).Select(n => n.Id);
        return placed.Concat(unplaced).ToList();
    }
}
