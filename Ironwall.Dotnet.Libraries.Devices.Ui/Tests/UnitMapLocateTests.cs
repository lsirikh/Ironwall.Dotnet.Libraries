using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Enums;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/****************************************************************************
   Purpose      : TEST-17 — [지도에서 보기] 장비 id 수집(FR-44)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : 시나리오 SIM-M001 · M002 · M003 · M008 · H-08.
                  편제는 섞임 픽스처 — 1대대(10) › 1중대(11: 소초 14 · 15) · 2중대(12) · 3중대(13: 소초 16).
****************************************************************************/
public class UnitMapLocateTests
{
    private static readonly UnitMapFixture Org = UnitMapTestData.Mixed();

    private static UnitDeviceItem Device(int id, int? unitId) => new(id, id, $"장비{id}", EnumDeviceCategory.Camera, unitId);

    private static readonly List<UnitDeviceItem> Devices = new()
    {
        Device(105, 11),
        Device(101, 11),
        Device(301, 14),
        Device(302, 15),
        Device(401, 16),
        Device(501, 12),
        Device(901, null),      // 미배치
        Device(902, 999),       // 편제 밖 부대
    };

    [Fact]
    public void should_collect_only_own_devices_when_descendants_are_excluded()
    {
        var set = UnitMapLocate.CollectDeviceIds(Org.Tree, Devices, 11, includeDescendants: false);

        Assert.Equal(new[] { 101, 105 }, set.DeviceIds);
        Assert.Null(set.DisabledReason);
    }

    [Fact]
    public void should_add_descendant_devices_when_descendants_are_included()
    {
        var set = UnitMapLocate.CollectDeviceIds(Org.Tree, Devices, 11, includeDescendants: true);

        Assert.Equal(new[] { 101, 105, 301, 302 }, set.DeviceIds);
    }

    [Fact]
    public void should_gather_the_whole_subtree_sorted_when_the_root_is_chosen()
    {
        var set = UnitMapLocate.CollectDeviceIds(Org.Tree, Devices, 10, includeDescendants: true);

        Assert.Equal(new[] { 101, 105, 301, 302, 401, 501 }, set.DeviceIds);   // 정렬 · 미배치 · 편제 밖 제외
    }

    [Fact]
    public void should_be_empty_with_a_reason_when_the_unit_has_no_devices()
    {
        var own = UnitMapLocate.CollectDeviceIds(Org.Tree, Devices, 10, includeDescendants: false);   // SIM-M002 — 대대 자신은 0

        Assert.True(own.IsEmpty);
        Assert.Equal(UnitMapLocate.NoDevicesReason, own.DisabledReason);
    }

    [Fact]
    public void should_ignore_duplicates_when_the_same_device_appears_twice()
    {
        var doubled = Devices.Concat(new[] { Device(101, 11) });

        var set = UnitMapLocate.CollectDeviceIds(Org.Tree, doubled, 11, includeDescendants: false);

        Assert.Equal(new[] { 101, 105 }, set.DeviceIds);
    }

    [Fact]
    public void should_be_empty_when_the_unit_is_outside_the_graph()
    {
        var set = UnitMapLocate.CollectDeviceIds(Org.Tree, Devices, 999, includeDescendants: true);

        Assert.True(set.IsEmpty);                                          // 편제 밖 id 로는 모으지 않는다
        Assert.Equal(UnitMapLocate.NoDevicesReason, set.DisabledReason);
    }

    [Fact]
    public void should_skip_unregistered_devices_without_a_server_id()
    {
        var set = UnitMapLocate.CollectDeviceIds(Org.Tree, new[] { Device(0, 12), Device(-3, 12), Device(7, 12) }, 12, false);

        Assert.Equal(new[] { 7 }, set.DeviceIds);
    }

    [Fact]
    public void should_collect_two_thousand_devices_under_a_division_when_the_org_is_full_size()
    {
        var org = UnitMapTestData.Standard200();
        var devices = UnitMapTestData.Devices(org.Tree).Items;

        var set = UnitMapLocate.CollectDeviceIds(org.Tree, devices, org.IdOf("제○○사단"), includeDescendants: true);

        Assert.Equal(devices.Count(d => d.UnitId.HasValue), set.DeviceIds.Count);   // SIM-M008 — 미배치만 빠진다
        Assert.Equal(set.DeviceIds.OrderBy(i => i), set.DeviceIds);
    }
}
