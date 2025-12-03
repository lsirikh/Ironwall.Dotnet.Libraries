using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/****************************************************************************
   Purpose      : GMapImageMarker 단위 테스트 (Phase 23)
   Created By   : Claude Code
   Created On   : 2025-12-03
   PRD Reference: Docs/PRD/PRD_ImageOverlay_Feature.md
****************************************************************************/

/// <summary>
/// GMapImageMarker 관련 단위 테스트
/// - 생성자 초기화
/// - UpdateLocation: 위치 변경
/// - UpdateSize: 크기 변경
/// - UpdateRotation: 회전 변경
/// - Contains: 경계 포함 확인
/// </summary>
public class GMapImageMarkerTests
{
    private readonly Mock<ILogService> _mockLog;

    public GMapImageMarkerTests()
    {
        _mockLog = new Mock<ILogService>();
    }

    #region Helper Methods

    /// <summary>
    /// 테스트용 ImageModel 생성 (Bounds: 126-127, 37-38)
    /// </summary>
    private ImageModel CreateTestImageModel()
    {
        return new ImageModel
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
    }

    /// <summary>
    /// 테스트용 GMapImageMarker 생성
    /// </summary>
    private GMapImageMarker CreateTestImageMarker()
    {
        var model = CreateTestImageModel();
        return new GMapImageMarker(_mockLog.Object, model);
    }

    #endregion

    #region Phase 23.2: GMapImageMarker 테스트

    /// <summary>
    /// TEST-IMG-3.1: GMapImageMarker 생성자 - ImageModel로 초기화
    ///
    /// ImageModel을 전달하면 Title, ImageBounds가 올바르게 초기화되어야 함
    /// </summary>
    [Fact(DisplayName = "TEST-IMG-3.1: GMapImageMarker 생성자 - ImageModel로 초기화")]
    public void TEST_IMG_3_1_GMapImageMarker_Constructor_ShouldInitializeWithImageModel()
    {
        // Arrange
        var imageModel = new ImageModel
        {
            Title = "Test",
            FilePath = "C:\\test.png",
            Left = 126.0,
            Right = 127.0,
            Top = 38.0,
            Bottom = 37.0
        };

        // Act
        var marker = new GMapImageMarker(_mockLog.Object, imageModel);

        // Assert
        Assert.Equal("Test", marker.Title);
        Assert.NotNull(marker.ImageBounds);
        Assert.Equal(126.0, marker.Left);
        Assert.Equal(127.0, marker.Right);
        Assert.Equal(38.0, marker.Top);
        Assert.Equal(37.0, marker.Bottom);
    }

    /// <summary>
    /// TEST-IMG-3.2: GMapImageMarker UpdateLocation - 경계 이동
    ///
    /// 새 중심점으로 UpdateLocation을 호출하면 Center가 업데이트되어야 함
    /// </summary>
    [Fact(DisplayName = "TEST-IMG-3.2: GMapImageMarker UpdateLocation - 경계 이동")]
    public void TEST_IMG_3_2_GMapImageMarker_UpdateLocation_ShouldMoveBounds()
    {
        // Arrange
        var marker = CreateTestImageMarker();
        var newPosition = new PointLatLng(38.0, 127.0);

        // Act
        marker.UpdateLocation(newPosition);

        // Assert
        Assert.Equal(newPosition.Lat, marker.Center.Lat, 5);
        Assert.Equal(newPosition.Lng, marker.Center.Lng, 5);
    }

    /// <summary>
    /// TEST-IMG-3.3: GMapImageMarker UpdateSize - 경계 크기 변경
    ///
    /// 새 크기로 UpdateSize를 호출하면 ImageBounds.WidthLng가 업데이트되어야 함
    /// </summary>
    [Fact(DisplayName = "TEST-IMG-3.3: GMapImageMarker UpdateSize - 경계 크기 변경")]
    public void TEST_IMG_3_3_GMapImageMarker_UpdateSize_ShouldResizeBounds()
    {
        // Arrange
        var marker = CreateTestImageMarker();

        // Act
        marker.UpdateSize(2.0, 1.0); // width, height in degrees

        // Assert
        Assert.Equal(2.0, marker.ImageBounds.WidthLng, 5);
        Assert.Equal(1.0, marker.ImageBounds.HeightLat, 5);
    }

    /// <summary>
    /// TEST-IMG-3.4: GMapImageMarker UpdateRotation - 회전 설정
    ///
    /// UpdateRotation을 호출하면 Bearing이 업데이트되어야 함
    /// </summary>
    [Fact(DisplayName = "TEST-IMG-3.4: GMapImageMarker UpdateRotation - 회전 설정")]
    public void TEST_IMG_3_4_GMapImageMarker_UpdateRotation_ShouldSetRotation()
    {
        // Arrange
        var marker = CreateTestImageMarker();

        // Act
        marker.UpdateRotation(45.0);

        // Assert
        Assert.Equal(45.0, marker.Bearing);
    }

    /// <summary>
    /// TEST-IMG-3.5: GMapImageMarker Contains - 경계 포함 확인
    ///
    /// Bounds 내부 점: true, 외부 점: false
    /// 기본 Bounds: 126-127, 37-38
    /// </summary>
    [Fact(DisplayName = "TEST-IMG-3.5: GMapImageMarker Contains - 경계 포함 확인")]
    public void TEST_IMG_3_5_GMapImageMarker_Contains_ShouldCheckPointInBounds()
    {
        // Arrange
        var marker = CreateTestImageMarker(); // Bounds: 126-127, 37-38

        // Act & Assert
        Assert.True(marker.Contains(new PointLatLng(37.5, 126.5)));  // Inside (center)
        Assert.True(marker.Contains(new PointLatLng(37.5, 126.0)));  // Left edge
        Assert.True(marker.Contains(new PointLatLng(38.0, 126.5)));  // Top edge
        Assert.False(marker.Contains(new PointLatLng(40.0, 130.0))); // Outside
        Assert.False(marker.Contains(new PointLatLng(36.0, 125.0))); // Outside
    }

    #endregion
}
