using Xunit;
using Moq;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Monitoring.Models.Events;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : 탐지 카드 썸네일 in-place 갱신(ApplyThumbnailUpdate)의 부분 detail 방어 검증 —
                  빈 썸네일로 기존 유효 썸네일을 지우지 않고, 새 썸네일은 반영.
   Created By   : GHLee
   Created On   : 2026-07-31
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class DetectionEventCardViewModelTests
{
    // 파라미터리스 base ctor는 IoC.Get을 호출(헤드리스 미초기화) → DI ctor(ea/log 주입)로 생성.
    private static DetectionEventCardViewModel CreateCard(DetectionEventModel model)
        => new(new Mock<IEventAggregator>().Object, new Mock<ILogService>().Object, model);

    [Fact]
    public void should_preserve_existing_thumbnail_when_update_has_null_thumbnail()
    {
        // Arrange — 유효 썸네일이 이미 표시 중인 카드
        var model = new DetectionEventModel { Thumbnail = "/api/thumbnails/good.jpg" };
        var card = CreateCard(model);

        // Act — 썸네일과 무관한 UPDATE(빈 썸네일, frame만 존재) 반영
        card.ApplyThumbnailUpdate(null, 1920, 1080);

        // Assert — 기존 썸네일 보존(지워지지 않음), frame은 갱신
        Assert.Equal("/api/thumbnails/good.jpg", model.Thumbnail);
        Assert.Equal(1920, model.FrameWidth);
        Assert.Equal(1080, model.FrameHeight);
    }

    [Fact]
    public void should_preserve_existing_thumbnail_when_update_has_empty_thumbnail()
    {
        // Arrange
        var model = new DetectionEventModel { Thumbnail = "/api/thumbnails/good.jpg" };
        var card = CreateCard(model);

        // Act — 빈 문자열도 무관 UPDATE로 간주(보존)
        card.ApplyThumbnailUpdate(string.Empty, null, null);

        // Assert
        Assert.Equal("/api/thumbnails/good.jpg", model.Thumbnail);
    }

    [Fact]
    public void should_replace_thumbnail_when_update_has_new_thumbnail()
    {
        // Arrange
        var model = new DetectionEventModel { Thumbnail = "/api/thumbnails/old.jpg" };
        var card = CreateCard(model);

        // Act — 회전 후 새 썸네일
        card.ApplyThumbnailUpdate("/api/thumbnails/rotated.jpg", null, null);

        // Assert
        Assert.Equal("/api/thumbnails/rotated.jpg", model.Thumbnail);
    }
}
