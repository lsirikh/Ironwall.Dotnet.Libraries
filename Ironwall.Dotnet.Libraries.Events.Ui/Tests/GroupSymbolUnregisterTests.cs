using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using System.Collections.Generic;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : 구역선(그룹 심볼) 등록 해제 — 그룹의 마지막 장비가 빠지면 조회표에서 내린다.
                  종전엔 해제 통로가 없어 빈 그룹의 구역선이 옛 소속의 이벤트 색을 계속 받았다.
   Created By   : GHLee
   Created On   : 2026-09-27
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class GroupSymbolUnregisterTests
{
    private static SymbolEventManager CreateManager()
        => new(new Mock<IEventAggregator>().Object,
               new Mock<ILogService>().Object,
               new EventSetupModel(new Mock<IEventSetupModel>().Object));

    private static SensorDeviceModel Sensor(int id, params int[] groups)
        => new() { Id = id, DeviceType = EnumDeviceType.Fence, DeviceGroups = new List<int>(groups) };

    [Fact]
    public void should_remove_group_lookup_when_group_symbol_is_unregistered()
    {
        // Arrange
        var manager = CreateManager();
        var line = new PidsGroupSymbolModel { LinkedDeviceGroup = 7 };
        manager.RegisterGroupSymbol(7, Sensor(1, 7), line);

        // Act
        var removed = manager.UnregisterGroupSymbol(7);

        // Assert — 조회표에서 빠졌고, 이후 그룹 이벤트는 그 선에 닿지 않는다
        Assert.True(removed);
        Assert.False(manager.HasGroupSymbol(7));
        manager.SetGroupDetecting(7, EnumEventType.Intrusion);
        Assert.Equal(EnumCompositeEventStatus.Normal, line.CompositeStatus);
    }

    [Fact]
    public void should_restore_line_to_normal_when_unregistered_while_coloured()
    {
        // Arrange — 탐지 색이 칠해진 채 소속이 비었다: 해제 뒤에는 누구도 이 선을 복원하지 않는다
        var manager = CreateManager();
        var line = new PidsGroupSymbolModel { LinkedDeviceGroup = 7 };
        manager.RegisterGroupSymbol(7, Sensor(1, 7), line);
        manager.SetGroupDetecting(7, EnumEventType.Intrusion);
        Assert.Equal(EnumCompositeEventStatus.Detecting, line.CompositeStatus);

        // Act
        manager.UnregisterGroupSymbol(7);

        // Assert
        Assert.Equal(EnumCompositeEventStatus.Normal, line.CompositeStatus);
    }

    [Fact]
    public void should_keep_line_colour_when_same_line_is_still_registered_under_another_group()
    {
        // Arrange — 속성창에서 구역선의 연결 그룹을 5 → 7 로 바꾼 직후: 옛 그룹 5 의 조회는 같은 선을 가리킨다
        var manager = CreateManager();
        var line = new PidsGroupSymbolModel { LinkedDeviceGroup = 7 };
        manager.RegisterGroupSymbol(5, Sensor(1, 5), line);
        manager.RegisterGroupSymbol(7, Sensor(2, 7), line);
        manager.SetGroupDetecting(7, EnumEventType.Intrusion);

        // Act — 옛 그룹만 내린다
        manager.UnregisterGroupSymbol(5);

        // Assert — 새 그룹의 색은 그대로
        Assert.Equal(EnumCompositeEventStatus.Detecting, line.CompositeStatus);
        Assert.True(manager.HasGroupSymbol(7));
    }

    [Fact]
    public void should_return_false_when_group_was_not_registered()
    {
        var manager = CreateManager();

        Assert.False(manager.UnregisterGroupSymbol(42));
    }
}
