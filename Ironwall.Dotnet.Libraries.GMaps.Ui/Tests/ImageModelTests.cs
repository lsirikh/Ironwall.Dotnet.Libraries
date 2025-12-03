using System;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/****************************************************************************
   Purpose      : ImageModel 단위 테스트 (Phase 21)
   Created By   : Claude Code
   Created On   : 2025-12-03
   PRD Reference: Docs/PRD/PRD_ImageOverlay_Feature.md
****************************************************************************/

/// <summary>
/// ImageModel 관련 단위 테스트
/// - ToRect(): 경계 좌표 → RectLatLng 변환
/// - Deconstruct(): RectLatLng → 경계 좌표 설정
/// - 복사 생성자
/// </summary>
public class ImageModelTests
{
    #region Phase 21.1: ImageModel 기본 기능 테스트

    /// <summary>
    /// TEST-IMG-1.1: ImageModel.ToRect()가 올바른 RectLatLng을 반환하는지 검증
    ///
    /// ImageModel의 Left, Right, Top, Bottom 속성을 RectLatLng으로 변환
    /// RectLatLng 생성자: RectLatLng(top, left, widthLng, heightLat)
    /// </summary>
    [Fact]
    public void TEST_IMG_1_1_ImageModel_ToRect_ShouldReturnCorrectRectLatLng()
    {
        // Arrange
        var model = new ImageModel
        {
            Left = 126.0,
            Right = 127.0,
            Top = 38.0,
            Bottom = 37.0
        };

        // Act
        var rect = model.ToRect();

        // Assert
        Assert.Equal(38.0, rect.Top);
        Assert.Equal(37.0, rect.Bottom);
        Assert.Equal(126.0, rect.Left);
        Assert.Equal(127.0, rect.Right);
        Assert.Equal(1.0, rect.WidthLng, 5);
        Assert.Equal(1.0, rect.HeightLat, 5);
    }

    /// <summary>
    /// TEST-IMG-1.2: ImageModel.Deconstruct()가 RectLatLng에서 경계 좌표를 올바르게 설정하는지 검증
    ///
    /// RectLatLng의 값을 ImageModel의 Left, Right, Top, Bottom으로 분해
    /// </summary>
    [Fact]
    public void TEST_IMG_1_2_ImageModel_Deconstruct_ShouldSetBoundsFromRectLatLng()
    {
        // Arrange
        var model = new ImageModel();
        // RectLatLng: (top, left, widthLng, heightLat)
        var rect = new RectLatLng(38.0, 126.0, 1.0, 1.0);

        // Act
        model.Deconstruct(rect);

        // Assert
        Assert.Equal(38.0, model.Top);
        Assert.Equal(126.0, model.Left);
        Assert.Equal(127.0, model.Right);  // Left + WidthLng
        Assert.Equal(37.0, model.Bottom);  // Top - HeightLat
    }

    /// <summary>
    /// TEST-IMG-1.3: ImageModel 복사 생성자가 모든 속성을 올바르게 복사하는지 검증
    ///
    /// ID는 복사하는 것이 기존 동작이므로 그대로 유지
    /// </summary>
    [Fact]
    public void TEST_IMG_1_3_ImageModel_CopyConstructor_ShouldCopyAllProperties()
    {
        // Arrange
        var original = new ImageModel
        {
            Id = 1,
            Title = "Test Image",
            FilePath = "C:\\test.png",
            Left = 126.0,
            Right = 127.0,
            Top = 38.0,
            Bottom = 37.0,
            Opacity = 0.8,
            Rotation = 45.0,
            Latitude = 37.5,
            Longitude = 126.5,
            Width = 200,
            Height = 150,
            Visibility = true,
            HasGeoReference = true,
            CoordinateSystem = "EPSG:4326"
        };

        // Act
        var copy = new ImageModel(original);

        // Assert - 모든 속성이 복사되었는지 확인
        Assert.Equal(original.Id, copy.Id);
        Assert.Equal(original.Title, copy.Title);
        Assert.Equal(original.FilePath, copy.FilePath);
        Assert.Equal(original.Left, copy.Left);
        Assert.Equal(original.Right, copy.Right);
        Assert.Equal(original.Top, copy.Top);
        Assert.Equal(original.Bottom, copy.Bottom);
        Assert.Equal(original.Opacity, copy.Opacity);
        Assert.Equal(original.Rotation, copy.Rotation);
        Assert.Equal(original.Latitude, copy.Latitude);
        Assert.Equal(original.Longitude, copy.Longitude);
        Assert.Equal(original.Width, copy.Width);
        Assert.Equal(original.Height, copy.Height);
        Assert.Equal(original.Visibility, copy.Visibility);
        Assert.Equal(original.HasGeoReference, copy.HasGeoReference);
        Assert.Equal(original.CoordinateSystem, copy.CoordinateSystem);
    }

    #endregion
}
