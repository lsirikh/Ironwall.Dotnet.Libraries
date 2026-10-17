using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/****************************************************************************
   Purpose      : MarkerEditAdorner 단위 테스트 (Phase 25)
   Created By   : Claude Code
   Created On   : 2025-12-03
   PRD Reference: Docs/PRD/PRD_ImageOverlay_Feature.md

   Note: Adorner는 WPF UI 요소를 필요로 하므로,
         IImageEditableMarker가 IEditableMarker와 호환되는지 검증하는 테스트 작성
****************************************************************************/

/// <summary>
/// MarkerEditAdorner 관련 단위 테스트
/// - IImageEditableMarker가 IEditableMarker 인터페이스 호환
/// - GMapImageMarker가 Adorner 시스템에서 사용 가능함을 검증
/// </summary>
public class MarkerEditAdornerTests
{
    private readonly Mock<ILogService> _mockLog;

    public MarkerEditAdornerTests()
    {
        _mockLog = new Mock<ILogService>();
    }

    #region Helper Methods

    /// <summary>
    /// 테스트용 GMapImageMarker 생성
    /// </summary>
    private GMapImageMarker CreateTestImageMarker()
    {
        var model = new ImageModel
        {
            Id = 1,
            Title = "Test Image",
            FilePath = "C:\\test.png",
            Left = 126.0,
            Right = 127.0,
            Top = 38.0,
            Bottom = 37.0,
            Opacity = 0.8,
            Rotation = 0.0,
            Latitude = 37.5,
            Longitude = 126.5,
            Width = 200,
            Height = 150
        };
        return new GMapImageMarker(_mockLog.Object, model);
    }

    #endregion

    #region Phase 25.1: MarkerEditAdorner IImageEditableMarker 지원 테스트

    /// <summary>
    /// TEST-IMG-5.1: GMapImageMarker는 IEditableMarker 인터페이스를 구현해야 함
    ///
    /// IImageEditableMarker : IEditableMarker 상속으로 Adorner 시스템 호환
    /// </summary>
    [Fact(DisplayName = "TEST-IMG-5.1: GMapImageMarker는 IEditableMarker 인터페이스를 구현해야 함")]
    public void TEST_IMG_5_1_GMapImageMarker_ShouldImplementIEditableMarker()
    {
        // Arrange
        var imageMarker = CreateTestImageMarker();

        // Act & Assert
        Assert.IsAssignableFrom<IEditableMarker>(imageMarker);
        Assert.IsAssignableFrom<IImageEditableMarker>(imageMarker);
    }

    /// <summary>
    /// TEST-IMG-5.2: IImageEditableMarker를 IEditableMarker로 캐스팅 가능해야 함
    ///
    /// MarkerEditAdorner에서 IEditableMarker로 처리할 수 있도록
    /// </summary>
    [Fact(DisplayName = "TEST-IMG-5.2: IImageEditableMarker를 IEditableMarker로 캐스팅 가능")]
    public void TEST_IMG_5_2_IImageEditableMarker_ShouldBeCastableToIEditableMarker()
    {
        // Arrange
        IImageEditableMarker imageMarker = CreateTestImageMarker();

        // Act
        IEditableMarker editableMarker = imageMarker;

        // Assert
        Assert.NotNull(editableMarker);
        Assert.Equal("Test Image", editableMarker.Title);
        Assert.Equal(200, editableMarker.Width);
        Assert.Equal(150, editableMarker.Height);
    }

    /// <summary>
    /// TEST-IMG-5.3: IEditableMarker로 캐스팅 후 UpdateLocation 호출 가능해야 함
    /// </summary>
    [Fact(DisplayName = "TEST-IMG-5.3: IEditableMarker로 캐스팅 후 UpdateLocation 호출 가능")]
    public void TEST_IMG_5_3_IEditableMarker_UpdateLocation_ShouldWork()
    {
        // Arrange
        IEditableMarker marker = CreateTestImageMarker();
        var newPosition = new PointLatLng(38.0, 127.0);

        // Act
        marker.UpdateLocation(newPosition);

        // Assert
        Assert.Equal(newPosition.Lat, marker.Position.Lat, 5);
        Assert.Equal(newPosition.Lng, marker.Position.Lng, 5);
    }

    /// <summary>
    /// TEST-IMG-5.4: IEditableMarker로 캐스팅 후 UpdateSize 호출 가능해야 함
    /// </summary>
    [Fact(DisplayName = "TEST-IMG-5.4: IEditableMarker로 캐스팅 후 UpdateSize 호출 가능")]
    public void TEST_IMG_5_4_IEditableMarker_UpdateSize_ShouldWork()
    {
        // Arrange
        IEditableMarker marker = CreateTestImageMarker();
        var originalCenter = ((IImageEditableMarker)marker).Center;

        // Act
        marker.UpdateSize(2.0, 1.5);

        // Assert - 크기 변경 후에도 이미지마커의 Center 보존됨
        var newCenter = ((IImageEditableMarker)marker).Center;
        Assert.Equal(originalCenter.Lat, newCenter.Lat, 4);
        Assert.Equal(originalCenter.Lng, newCenter.Lng, 4);
    }

    /// <summary>
    /// TEST-IMG-5.5: IEditableMarker로 캐스팅 후 UpdateRotation 호출 가능해야 함
    /// </summary>
    [Fact(DisplayName = "TEST-IMG-5.5: IEditableMarker로 캐스팅 후 UpdateRotation 호출 가능")]
    public void TEST_IMG_5_5_IEditableMarker_UpdateRotation_ShouldWork()
    {
        // Arrange
        IEditableMarker marker = CreateTestImageMarker();

        // Act
        marker.UpdateRotation(90.0);

        // Assert
        Assert.Equal(90.0, marker.Bearing);
    }

    /// <summary>
    /// TEST-IMG-5.6: IEditableMarker로 다운캐스팅하여 Image 전용 속성 접근 가능
    /// </summary>
    [Fact(DisplayName = "TEST-IMG-5.6: IEditableMarker에서 IImageEditableMarker로 다운캐스팅 가능")]
    public void TEST_IMG_5_6_IEditableMarker_ShouldDowncastToIImageEditableMarker()
    {
        // Arrange
        IEditableMarker marker = CreateTestImageMarker();

        // Act
        var canDowncast = marker is IImageEditableMarker;
        var imageMarker = marker as IImageEditableMarker;

        // Assert
        Assert.True(canDowncast);
        Assert.NotNull(imageMarker);
        Assert.NotNull(imageMarker.ImageBounds);
        Assert.Equal(126.0, imageMarker.Left);
        Assert.Equal(127.0, imageMarker.Right);
    }

    #endregion
}
