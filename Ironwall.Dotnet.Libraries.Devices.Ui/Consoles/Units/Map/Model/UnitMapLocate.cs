using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;

/****************************************************************************
   Purpose      : [지도에서 보기] — 부대(+예하)의 장비 서버 id 모으기 (FR-44)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>모은 장비 id(오름차순 · 겹침 없음)와, 비어 있으면 단추를 끄는 사유.</summary>
public sealed record UnitMapLocateSet(IReadOnlyList<int> DeviceIds, string? DisabledReason)
{
    public bool IsEmpty => DeviceIds.Count == 0;
}

/// <summary>부대 → 장비 id 풀이(순수). 지도는 장비 id 만 받는다(D-7 · R-21).</summary>
public static class UnitMapLocate
{
    /// <summary>장비가 하나도 없을 때의 단추 사유(FR-44 · SIM-M001).</summary>
    public const string NoDevicesReason = "이 부대에 속한 장비가 없습니다";

    /// <summary>
    /// 그 부대(+예하)에 속한 장비의 서버 id — 오름차순 · 겹침 없음. 편제에 없는 부대 · 미배치 · 미등록(id ≤ 0) 장비는 넣지 않는다.
    /// </summary>
    public static UnitMapLocateSet CollectDeviceIds(UnitTreeModel tree, IEnumerable<UnitDeviceItem> devices, int unitId, bool includeDescendants)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(devices);

        if (tree.Find(unitId) is null) return new UnitMapLocateSet(Array.Empty<int>(), NoDevicesReason);

        var units = new HashSet<int> { unitId };
        if (includeDescendants) units.UnionWith(tree.DescendantIds(unitId));

        var ids = devices.Where(d => d is not null && d.Id > 0 && d.UnitId is int u && units.Contains(u))
                         .Select(d => d.Id)
                         .Distinct()
                         .OrderBy(id => id)
                         .ToList();
        return new UnitMapLocateSet(ids, ids.Count == 0 ? NoDevicesReason : null);
    }
}
