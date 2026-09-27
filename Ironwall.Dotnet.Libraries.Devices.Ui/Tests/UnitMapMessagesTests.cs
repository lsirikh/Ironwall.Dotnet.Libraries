using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/****************************************************************************
   Purpose      : TEST-16(메시지 절반) — 관계도 ↔ 지도 ↔ 콘솔 공용 메시지 값 검사
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : 시나리오 SIM-M001 · M003 · M010 · M031 · N072 · N074.
****************************************************************************/
public class UnitMapMessagesTests
{
    [Fact]
    public void should_keep_positive_ids_once_in_ascending_order_when_building_locate_request()
    {
        var request = MapLocateRequest.For("7중대", new[] { 5, 3, 5, 0, -1, 12 });

        Assert.NotNull(request);
        Assert.Equal(new[] { 3, 5, 12 }, request!.DeviceIds);
        Assert.Equal("7중대", request.Title);
        Assert.NotEqual(Guid.Empty, request.RequestId);
    }

    [Fact]
    public void should_return_null_when_no_valid_device_remains()
    {
        Assert.Null(MapLocateRequest.For("7중대", Array.Empty<int>()));      // SIM-M001 — 보낼 것이 없다
        Assert.Null(MapLocateRequest.For("7중대", new[] { 0, -3 }));
        Assert.Null(MapLocateRequest.For("7중대", null));
    }

    [Fact]
    public void should_issue_a_new_request_id_each_time_when_the_same_devices_are_asked_again()
    {
        var first = MapLocateRequest.For("7중대", new[] { 1 })!;
        var second = MapLocateRequest.For("7중대", new[] { 1 })!;

        Assert.NotEqual(first.RequestId, second.RequestId);                // 회신을 요청마다 가른다(SIM-M010)
    }

    [Fact]
    public void should_use_an_empty_title_when_title_is_missing()
    {
        Assert.Equal(string.Empty, MapLocateRequest.For(null!, new[] { 1 })!.Title);
    }

    [Fact]
    public void should_build_device_unit_change_only_for_positive_ids()
    {
        Assert.Equal(new DeviceUnitChangedMessage(12, 7), DeviceUnitChangedMessage.For(12, 7));
        Assert.Null(DeviceUnitChangedMessage.For(0, 7));
        Assert.Null(DeviceUnitChangedMessage.For(12, 0));
        Assert.Null(DeviceUnitChangedMessage.For(-1, -1));
    }

    [Fact]
    public void should_compare_by_value_when_messages_carry_the_same_facts()
    {
        var id = Guid.NewGuid();

        Assert.Equal(new MapLocateResult(id, 11, 5), new MapLocateResult(id, 11, 5));
        Assert.Equal(new OpenUnitConsoleRequest(27, OpenMap: true), new OpenUnitConsoleRequest(27, true));
        Assert.Equal(new UnitLayoutChangedMessage(14), new UnitLayoutChangedMessage(14));
    }

    [Theory]
    [InlineData(14L, null, 14L)]     // 클라 요청서 모양 {action, version}          — SIM-N074
    [InlineData(null, 14L, 14L)]     // 서버 PRD 초안 모양 {action, resource_id:<v>} — SIM-N073
    [InlineData(15L, 14L, 15L)]      // 둘 다 오면 version 이 이긴다
    [InlineData(0L, 14L, 14L)]       // version 이 비정상이면 resource_id 로
    public void should_read_the_layout_version_from_either_body_key(long? version, long? resourceId, long expected)
    {
        Assert.Equal(expected, UnitLayoutChangedMessage.FromBody(version, resourceId)!.Version);
    }

    [Fact]
    public void should_return_null_when_the_layout_notice_carries_no_version()
    {
        Assert.Null(UnitLayoutChangedMessage.FromBody(null, null));
        Assert.Null(UnitLayoutChangedMessage.FromBody(0, -1));
    }
}
