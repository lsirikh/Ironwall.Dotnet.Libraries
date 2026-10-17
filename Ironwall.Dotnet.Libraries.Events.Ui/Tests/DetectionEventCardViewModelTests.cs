using Xunit;
using Moq;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System.IO;

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

    [Fact]
    public void should_return_null_thumbnail_when_model_has_no_thumbnail()
    {
        // Arrange
        var card = CreateCard(new DetectionEventModel());

        // Act
        var image = card.Thumbnail;

        // Assert — 뷰는 뒤의 기본 아이콘을 보인다
        Assert.Null(image);
        Assert.False(card.HasThumbnail);
    }

    [Fact]
    public void should_notify_thumbnail_image_when_update_has_new_thumbnail()
    {
        // Arrange — 카드 뷰는 Image.Source 를 Thumbnail(ImageSource)에 묶는다 → 그 이름이 반드시 울려야 다시 그린다
        var card = CreateCard(new DetectionEventModel { Thumbnail = "/api/thumbnails/old.jpg" });
        var raised = new List<string?>();
        card.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        // Act
        card.ApplyThumbnailUpdate("/api/thumbnails/rotated.jpg", null, null);

        // Assert
        Assert.Contains(nameof(DetectionEventCardViewModel.Thumbnail), raised);
    }

    [Fact]
    public void should_expose_decoded_image_when_thumbnail_file_loads()
    {
        // Arrange — 1×1 PNG 를 임시 파일로 두고 절대 file URI 를 썸네일로 준다(로컬은 동기 로드)
        var path = Path.Combine(Path.GetTempPath(), $"card-thumb-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="));
        try
        {
            var card = CreateCard(new DetectionEventModel { Thumbnail = new Uri(path).AbsoluteUri });

            // Act — 첫 읽기는 로드를 걸고, 다음 읽기는 캐시에서 받는다
            _ = card.Thumbnail;
            var image = card.Thumbnail;

            // Assert — Uri 가 아니라 디코드된 이미지가 뷰로 간다
            Assert.NotNull(image);
            Assert.True(image!.IsFrozen);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
