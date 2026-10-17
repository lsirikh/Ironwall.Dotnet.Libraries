using Xunit;
using Moq;
using Ironwall.Dotnet.Libraries.Events.Models;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : EventSetupModel 복사 생성자가 장애 전용 필드를 복사하는지 검증
                  (수기 얕은복사라 누락 시 복사본이 false/0으로 유실 — 회귀 방지).
   Created By   : GHLee
   Created On   : 2026-07-31
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class EventSetupModelTests
{
    [Fact]
    public void should_copy_malfunction_fields_when_constructed_from_model()
    {
        // Arrange
        var mock = new Mock<IEventSetupModel>();
        mock.Setup(s => s.IsMalfunctionAutoEventDiscard).Returns(true);
        mock.Setup(s => s.MalfunctionTimeDiscardSec).Returns(33);
        mock.Setup(s => s.IsAutoEventDiscard).Returns(true);
        mock.Setup(s => s.TimeDiscardSec).Returns(20);

        // Act
        var copy = new EventSetupModel(mock.Object);

        // Assert — 장애 전용 필드 복사됨
        Assert.True(copy.IsMalfunctionAutoEventDiscard);
        Assert.Equal(33, copy.MalfunctionTimeDiscardSec);
        // 회귀 방지 — 탐지 필드도 정상 복사
        Assert.True(copy.IsAutoEventDiscard);
        Assert.Equal(20, copy.TimeDiscardSec);
    }
}
