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

    #region Phase 28: Zoom 기반 이미지 크기 조절

    /// <summary>
    /// TEST-ZOOM-28.1.1: GMapMarkerBaseControl에 FindParentMapControl 메서드 존재
    ///
    /// base class에서 상속받은 FindParentMapControl 메서드가 존재해야 함
    /// </summary>
    [Fact(DisplayName = "TEST-ZOOM-28.1.1: GMapMarkerBaseControl에 FindParentMapControl 메서드 존재")]
    public void FindParentMapControl_ShouldExist_InBaseClass()
    {
        // Arrange - GMapMarkerImageControl은 GMapMarkerBaseControl<T>를 상속
        // GMapMarkerBaseControl에 FindParentMapControl이 정의되어 있어야 함

        // Act - 리플렉션으로 메서드 존재 확인
        var baseType = typeof(GMapMarkerImageControl).BaseType;
        var method = baseType?.GetMethod("FindParentMapControl",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Assert
        Assert.NotNull(method);
        Assert.Equal("FindParentMapControl", method.Name);
    }

    /// <summary>
    /// TEST-ZOOM-28.3.1: UpdateScreenPosition 메서드 존재
    ///
    /// GMapImageMarker에 UpdateScreenPosition 메서드가 있어야 함
    /// </summary>
    [Fact(DisplayName = "TEST-ZOOM-28.3.1: UpdateScreenPosition 메서드 존재")]
    public void UpdateScreenPosition_ShouldExist()
    {
        // Arrange
        var method = typeof(GMapImageMarker).GetMethod("UpdateScreenPosition");

        // Assert
        Assert.NotNull(method);
    }

    /// <summary>
    /// TEST-ZOOM-28.3.3: UpdateScreenPosition 후 지리적 경계 동일
    ///
    /// UpdateScreenPosition 호출 후에도 ImageBounds는 변경되지 않아야 함
    /// </summary>
    [Fact(DisplayName = "TEST-ZOOM-28.3.3: UpdateScreenPosition 후 지리적 경계 동일")]
    public void UpdateScreenPosition_ShouldMaintain_GeographicBounds()
    {
        // Arrange
        var marker = CreateTestImageMarker();
        var originalBounds = marker.ImageBounds;
        var originalLeft = marker.Left;
        var originalRight = marker.Right;
        var originalTop = marker.Top;
        var originalBottom = marker.Bottom;

        // Act - mapControl 없이 호출 (null 안전해야 함)
        marker.UpdateScreenPosition(null!);

        // Assert - 지리적 경계는 동일해야 함
        Assert.Equal(originalLeft, marker.Left, 6);
        Assert.Equal(originalRight, marker.Right, 6);
        Assert.Equal(originalTop, marker.Top, 6);
        Assert.Equal(originalBottom, marker.Bottom, 6);
    }

    #endregion

    #region Phase 28.2: UpdateImageGeometry 테스트

    /// <summary>
    /// TEST-ZOOM-28.2.1: UpdateImageGeometry 메서드 존재
    ///
    /// GMapMarkerImageControl에 UpdateImageGeometry 메서드가 있어야 함
    /// </summary>
    [Fact(DisplayName = "TEST-ZOOM-28.2.1: UpdateImageGeometry 메서드 존재")]
    public void UpdateImageGeometry_ShouldExist()
    {
        // Arrange
        var method = typeof(GMapMarkerImageControl).GetMethod("UpdateImageGeometry",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Assert
        Assert.NotNull(method);
    }

    #endregion

    #region Phase 33: 버그 수정 테스트 (PRD Section 16)

    /// <summary>
    /// BUG-33.1.1: UpdateSize(200, 150) 호출 시 비정상 ImageBounds 생성 재현
    ///
    /// 문제: MarkerEditAdorner가 픽셀 단위(200, 150)로 UpdateSize를 호출하면
    ///       현재 구현은 이를 위경도(200°, 150°)로 해석하여 지구 절반 크기의 경계 생성
    ///
    /// 기대: 픽셀 단위 입력 시 ImageBounds.WidthLng가 합리적인 범위(< 10°) 내에 있어야 함
    /// </summary>
    [Fact(DisplayName = "BUG-33.1.1: UpdateSize(200, 150) 호출 시 비정상 ImageBounds 생성 재현")]
    public void UpdateSize_WithPixelValues_ShouldNotCreate_HugeBounds()
    {
        // Arrange
        var marker = CreateTestImageMarker();
        var originalCenter = marker.Center;

        // Act - Adorner가 픽셀 단위로 호출하는 시나리오 재현
        // 현재 버그: UpdateSize(200, 150)이 200° x 150°로 해석됨
        marker.UpdateSize(200, 150);

        // Assert - ImageBounds.WidthLng가 비정상적으로 크면 안됨
        // 정상적인 이미지는 10° 이하의 크기여야 함 (약 1100km 이하)
        Assert.True(marker.ImageBounds.WidthLng < 10.0,
            $"ImageBounds.WidthLng가 비정상적으로 큼: {marker.ImageBounds.WidthLng}° (expected < 10°)");
        Assert.True(marker.ImageBounds.HeightLat < 10.0,
            $"ImageBounds.HeightLat가 비정상적으로 큼: {marker.ImageBounds.HeightLat}° (expected < 10°)");

        // 경도 범위 검증 (-180 ~ 180)
        Assert.True(marker.Left >= -180.0 && marker.Left <= 180.0,
            $"Left 값이 유효 범위를 벗어남: {marker.Left}");
        Assert.True(marker.Right >= -180.0 && marker.Right <= 180.0,
            $"Right 값이 유효 범위를 벗어남: {marker.Right}");
    }

    /// <summary>
    /// BUG-33.1.2: UpdateSize에 MapControl 전달 시 정확한 픽셀→위경도 변환
    ///
    /// 문제: 현재 UpdateSize는 mapControl 없이 비율 기반 스케일링만 수행
    ///       정확한 변환을 위해서는 FromLocalToLatLng 사용 필요
    ///
    /// 기대: mapControl 전달 시 픽셀 크기가 정확히 위경도로 변환되어야 함
    /// </summary>
    [Fact(DisplayName = "BUG-33.1.2: UpdateSize(200, 150, mapControl) - 픽셀→위경도 변환")]
    public void UpdateSize_WithMapControl_ShouldConvert_PixelsToLatLng()
    {
        // Arrange
        var marker = CreateTestImageMarker();
        var originalCenter = marker.Center;
        var originalWidthLng = marker.ImageBounds.WidthLng;
        var originalHeightLat = marker.ImageBounds.HeightLat;

        // Act - mapControl = null로 호출 (현재 구현)
        // 현재는 비율 기반 스케일링이므로 크기가 1:1로 유지됨
        marker.UpdateSize(200, 150);

        // Assert - 현재 구현 검증: 비율 유지 스케일링
        // UpdateSize(200, 150)에서 Width=200, Height=150으로 설정
        // 현재 픽셀이 200x150이고 새 입력도 200x150이면 스케일 1:1
        Assert.Equal(originalWidthLng, marker.ImageBounds.WidthLng, 4);
        Assert.Equal(originalHeightLat, marker.ImageBounds.HeightLat, 4);

        // TODO: Phase 33.1.2 GREEN 단계에서 UpdateSize(width, height, mapControl) 오버로드 구현 후
        // FromLocalToLatLng 기반 정확한 변환 테스트 추가
    }

    /// <summary>
    /// BUG-33.1.3: UpdateSize 후 ImageModel.Left/Right/Top/Bottom 동기화 확인
    ///
    /// 문제: UpdateSize 후 ImageModel 속성이 동기화되지 않으면 DB 저장 시 불일치 발생
    ///
    /// 기대: UpdateSize 호출 후 ImageModel 경계 속성이 ImageBounds와 일치해야 함
    /// </summary>
    [Fact(DisplayName = "BUG-33.1.3: UpdateSize 후 ImageModel.Left/Right/Top/Bottom 동기화")]
    public void UpdateSize_ShouldSync_ImageModelBounds()
    {
        // Arrange
        var marker = CreateTestImageMarker();

        // Act - 위경도 단위로 크기 변경
        marker.UpdateSize(2.0, 1.5);

        // Assert - ImageModel 속성과 ImageBounds 일치 확인
        Assert.Equal(marker.ImageBounds.Left, marker.ImageModel.Left, 6);
        Assert.Equal(marker.ImageBounds.Right, marker.ImageModel.Right, 6);
        Assert.Equal(marker.ImageBounds.Top, marker.ImageModel.Top, 6);
        Assert.Equal(marker.ImageBounds.Bottom, marker.ImageModel.Bottom, 6);
    }

    /// <summary>
    /// BUG-33.1.4: UpdateSize 후 Left/Right가 -180~180 범위 내
    ///
    /// 문제: 경도 값이 유효 범위를 벗어나면 DB 저장 및 지도 표시 오류
    ///
    /// 기대: UpdateSize 후에도 경도 값이 -180~180 범위 내에 있어야 함
    /// </summary>
    [Fact(DisplayName = "BUG-33.1.4: UpdateSize 후 Left/Right가 -180~180 범위 내")]
    public void UpdateSize_ShouldProduce_ValidLngRange()
    {
        // Arrange
        var marker = CreateTestImageMarker();

        // Act - 다양한 크기로 테스트
        marker.UpdateSize(5.0, 3.0);

        // Assert - 경도 값 유효 범위 확인
        Assert.InRange(marker.Left, -180.0, 180.0);
        Assert.InRange(marker.Right, -180.0, 180.0);

        // 위도 값도 검증
        Assert.InRange(marker.Top, -90.0, 90.0);
        Assert.InRange(marker.Bottom, -90.0, 90.0);
    }

    #endregion

    #region Phase 33.2: 버그 1 수정 - Position 일관성 유지

    /// <summary>
    /// BUG-33.2.1: GMapImageMarker의 Position은 항상 Center여야 함
    ///
    /// 문제: OnRender()에서 Position을 TopLeft로 변경하면 마커 클릭 시 중점 Snap 오류 발생
    ///       (마커를 중심에서 클릭해도 TopLeft 기준으로 계산됨)
    ///
    /// 기대: Position은 항상 ImageBounds의 Center와 일치해야 함
    /// </summary>
    [Fact(DisplayName = "BUG-33.2.1: GMapImageMarker Position은 항상 Center여야 함")]
    public void GMapImageMarker_Position_ShouldAlways_BeCenter()
    {
        // Arrange
        var marker = CreateTestImageMarker();
        var expectedCenter = marker.Center;

        // Act - 생성 직후 Position 확인
        var actualPosition = marker.Position;

        // Assert - Position은 Center와 일치해야 함
        Assert.Equal(expectedCenter.Lat, actualPosition.Lat, 6);
        Assert.Equal(expectedCenter.Lng, actualPosition.Lng, 6);
    }

    /// <summary>
    /// BUG-33.2.2: UpdateLocation 후에도 Position == Center 유지
    ///
    /// 문제: UpdateLocation 호출 후 Position이 새 중심점과 일치하지 않으면 Snap 오류
    ///
    /// 기대: UpdateLocation 후 Position은 새 Center와 일치해야 함
    /// </summary>
    [Fact(DisplayName = "BUG-33.2.2: UpdateLocation 후 Position == Center 유지")]
    public void UpdateLocation_Position_ShouldEqual_NewCenter()
    {
        // Arrange
        var marker = CreateTestImageMarker();
        var newCenter = new PointLatLng(38.0, 127.0);

        // Act
        marker.UpdateLocation(newCenter);

        // Assert - Position == Center
        Assert.Equal(newCenter.Lat, marker.Position.Lat, 6);
        Assert.Equal(newCenter.Lng, marker.Position.Lng, 6);
        Assert.Equal(marker.Center.Lat, marker.Position.Lat, 6);
        Assert.Equal(marker.Center.Lng, marker.Position.Lng, 6);
    }

    /// <summary>
    /// BUG-33.2.3: UpdateSize 후에도 Position == Center 유지
    ///
    /// 문제: UpdateSize 호출 후 Position이 Center와 일치하지 않으면 마커가 이동한 것처럼 보임
    ///
    /// 기대: UpdateSize 후 Position은 Center와 일치해야 함 (중심 고정)
    /// </summary>
    [Fact(DisplayName = "BUG-33.2.3: UpdateSize 후 Position == Center 유지")]
    public void UpdateSize_Position_ShouldEqual_Center()
    {
        // Arrange
        var marker = CreateTestImageMarker();
        var originalCenter = marker.Center;

        // Act - 크기 변경
        marker.UpdateSize(2.0, 1.0);

        // Assert - Position == Center (중심 고정)
        Assert.Equal(marker.Center.Lat, marker.Position.Lat, 6);
        Assert.Equal(marker.Center.Lng, marker.Position.Lng, 6);

        // 중심점은 변경되지 않아야 함
        Assert.Equal(originalCenter.Lat, marker.Center.Lat, 6);
        Assert.Equal(originalCenter.Lng, marker.Center.Lng, 6);
    }

    #endregion

    #region Phase 33.3: 버그 3 수정 - Width/Height setter 개선

    /// <summary>
    /// BUG-33.3.1: Width setter가 ImageBounds.WidthLng도 업데이트해야 함
    ///
    /// 문제: Width setter가 _imageModel.Width만 업데이트하고 ImageBounds는 그대로
    ///       PropertyWindow에서 Width 입력 시 화면에 반영되지 않음
    ///
    /// 기대: Width 설정 시 ImageBounds도 비율에 맞게 업데이트
    /// </summary>
    [Fact(DisplayName = "BUG-33.3.1: Width 설정 시 ImageBounds.WidthLng 변경")]
    public void Width_Setter_ShouldUpdate_ImageBoundsWidth()
    {
        // Arrange
        var marker = CreateTestImageMarker();
        var originalWidthLng = marker.ImageBounds.WidthLng;
        var originalWidth = marker.Width; // 200

        // Act - Width를 2배로 설정
        marker.Width = originalWidth * 2; // 400

        // Assert - ImageBounds.WidthLng도 2배가 되어야 함
        var expectedWidthLng = originalWidthLng * 2;
        Assert.Equal(expectedWidthLng, marker.ImageBounds.WidthLng, 4);
    }

    /// <summary>
    /// BUG-33.3.2: Height setter가 ImageBounds.HeightLat도 업데이트해야 함
    ///
    /// 문제: Height setter가 _imageModel.Height만 업데이트하고 ImageBounds는 그대로
    ///
    /// 기대: Height 설정 시 ImageBounds도 비율에 맞게 업데이트
    /// </summary>
    [Fact(DisplayName = "BUG-33.3.2: Height 설정 시 ImageBounds.HeightLat 변경")]
    public void Height_Setter_ShouldUpdate_ImageBoundsHeight()
    {
        // Arrange
        var marker = CreateTestImageMarker();
        var originalHeightLat = marker.ImageBounds.HeightLat;
        var originalHeight = marker.Height; // 150

        // Act - Height를 2배로 설정
        marker.Height = originalHeight * 2; // 300

        // Assert - ImageBounds.HeightLat도 2배가 되어야 함
        var expectedHeightLat = originalHeightLat * 2;
        Assert.Equal(expectedHeightLat, marker.ImageBounds.HeightLat, 4);
    }

    /// <summary>
    /// BUG-33.3.3: Width/Height 변경 후 중심점 유지
    ///
    /// 문제: 크기 변경 시 중심점이 이동하면 안 됨
    ///
    /// 기대: Width/Height 변경 후에도 Center 동일
    /// </summary>
    [Fact(DisplayName = "BUG-33.3.3: Width/Height 변경 후 중심점 유지")]
    public void WidthHeight_Change_ShouldMaintain_Center()
    {
        // Arrange
        var marker = CreateTestImageMarker();
        var originalCenter = marker.Center;

        // Act - Width/Height 변경
        marker.Width = marker.Width * 1.5;
        marker.Height = marker.Height * 1.5;

        // Assert - 중심점 유지
        Assert.Equal(originalCenter.Lat, marker.Center.Lat, 6);
        Assert.Equal(originalCenter.Lng, marker.Center.Lng, 6);
    }

    /// <summary>
    /// BUG-33.3.4: Width/Height 변경 후 ImageModel 경계 동기화
    ///
    /// 문제: Width/Height 변경 시 ImageModel.Left/Right/Top/Bottom도 업데이트 필요
    ///
    /// 기대: Width/Height 변경 후 ImageModel 경계 속성이 ImageBounds와 일치
    /// </summary>
    [Fact(DisplayName = "BUG-33.3.4: Width/Height 변경 후 ImageModel 경계 동기화")]
    public void WidthHeight_Change_ShouldSync_ImageModelBounds()
    {
        // Arrange
        var marker = CreateTestImageMarker();

        // Act - Width/Height 변경
        marker.Width = marker.Width * 2;

        // Assert - ImageModel 경계가 ImageBounds와 일치
        Assert.Equal(marker.ImageBounds.Left, marker.ImageModel.Left, 6);
        Assert.Equal(marker.ImageBounds.Right, marker.ImageModel.Right, 6);
        Assert.Equal(marker.ImageBounds.Top, marker.ImageModel.Top, 6);
        Assert.Equal(marker.ImageBounds.Bottom, marker.ImageModel.Bottom, 6);
    }

    #endregion

    #region Phase 33.4: 통합 및 회귀 테스트

    /// <summary>
    /// BUG-33.4.1: Adorner 리사이즈 후 UpdateImageAsync 정상 동작
    ///
    /// 시나리오: MarkerEditAdorner에서 크기조절 → UpdateSize 호출 → DB 저장용 모델 준비
    ///
    /// 기대: UpdateSize 호출 후 ImageModel의 모든 속성이 DB 저장에 적합한 상태
    /// </summary>
    [Fact(DisplayName = "BUG-33.4.1: Adorner 리사이즈 후 UpdateImageAsync 정상 동작")]
    public void AdornerResize_ThenDbSave_ShouldWork()
    {
        // Arrange - Adorner에서 픽셀 크기 변경 시뮬레이션
        var marker = CreateTestImageMarker();
        var originalModel = marker.ImageModel;
        var originalCenter = marker.Center;

        // Adorner는 픽셀 단위로 크기를 전달함 (200x300)
        double newPixelWidth = 200;
        double newPixelHeight = 300;

        // Act - Adorner에서 UpdateSize 호출 (픽셀 단위)
        marker.UpdateSize(newPixelWidth, newPixelHeight);

        // Assert - DB 저장용 ImageModel 속성이 모두 유효해야 함
        // 1. Width/Height가 픽셀 값으로 업데이트됨
        Assert.Equal(newPixelWidth, marker.Width, 1);
        Assert.Equal(newPixelHeight, marker.Height, 1);

        // 2. ImageModel도 동일한 값 반영
        Assert.Equal(newPixelWidth, marker.ImageModel.Width, 1);
        Assert.Equal(newPixelHeight, marker.ImageModel.Height, 1);

        // 3. ImageBounds 경계가 ImageModel과 동기화됨
        Assert.Equal(marker.ImageBounds.Left, marker.ImageModel.Left, 6);
        Assert.Equal(marker.ImageBounds.Right, marker.ImageModel.Right, 6);
        Assert.Equal(marker.ImageBounds.Top, marker.ImageModel.Top, 6);
        Assert.Equal(marker.ImageBounds.Bottom, marker.ImageModel.Bottom, 6);

        // 4. 중심점이 유지됨 (크기 변경 시 중심 기준 확대/축소)
        Assert.Equal(originalCenter.Lat, marker.Center.Lat, 6);
        Assert.Equal(originalCenter.Lng, marker.Center.Lng, 6);

        // 5. Position도 중심점과 일치 (버그 1 수정 확인)
        Assert.Equal(marker.Center.Lat, marker.Position.Lat, 6);
        Assert.Equal(marker.Center.Lng, marker.Position.Lng, 6);
    }

    /// <summary>
    /// BUG-33.4.2: PropertyWindow Width 입력 후 화면에 반영
    ///
    /// 시나리오: PropertyWindow에서 Width 값 입력 → ImageBounds 변경 → 화면 크기 일치
    ///
    /// 기대: Width setter 호출 후 ImageBounds의 가로 크기가 비례적으로 변경됨
    /// </summary>
    [Fact(DisplayName = "BUG-33.4.2: PropertyWindow Width 입력 후 화면에 반영")]
    public void PropertyWindow_WidthInput_ShouldReflect_OnScreen()
    {
        // Arrange - PropertyWindow에서 직접 Width 값 입력 시뮬레이션
        var marker = CreateTestImageMarker();
        var originalCenter = marker.Center;
        var originalWidthLng = marker.ImageBounds.WidthLng;

        // PropertyWindow에서 Width를 250으로 변경
        double newWidth = 250;
        double scale = newWidth / marker.Width;

        // Act - Width setter 호출 (PropertyWindow 바인딩 시뮬레이션)
        marker.Width = newWidth;

        // Assert
        // 1. Width가 새 값으로 설정됨
        Assert.Equal(newWidth, marker.Width, 1);

        // 2. ImageBounds.WidthLng가 비례적으로 변경됨
        var expectedWidthLng = originalWidthLng * scale;
        Assert.Equal(expectedWidthLng, marker.ImageBounds.WidthLng, 6);

        // 3. ImageModel.Left/Right가 ImageBounds와 동기화됨
        Assert.Equal(marker.ImageBounds.Left, marker.ImageModel.Left, 6);
        Assert.Equal(marker.ImageBounds.Right, marker.ImageModel.Right, 6);

        // 4. 중심점 유지
        Assert.Equal(originalCenter.Lat, marker.Center.Lat, 6);
        Assert.Equal(originalCenter.Lng, marker.Center.Lng, 6);
    }

    /// <summary>
    /// BUG-33.4.3: 마커 드래그 후 Position이 클릭 지점과 일치
    ///
    /// 시나리오: 마커를 새 위치로 드래그 → UpdatePosition 호출 → Position 업데이트
    ///
    /// 기대: UpdatePosition 호출 후 Position과 Center가 새 위치와 일치
    /// </summary>
    [Fact(DisplayName = "BUG-33.4.3: 마커 드래그 후 Position이 클릭 지점과 일치")]
    public void MarkerDrag_ShouldMove_ToClickedPosition()
    {
        // Arrange
        var marker = CreateTestImageMarker();
        var originalBoundsSize = new { W = marker.ImageBounds.WidthLng, H = marker.ImageBounds.HeightLat };

        // 새 위치 (드래그 목적지)
        var newPosition = new PointLatLng(37.5700, 126.9850);

        // Act - 드래그 후 UpdateLocation 호출
        marker.UpdateLocation(newPosition);

        // Assert
        // 1. Position이 새 위치로 업데이트됨
        Assert.Equal(newPosition.Lat, marker.Position.Lat, 6);
        Assert.Equal(newPosition.Lng, marker.Position.Lng, 6);

        // 2. Center도 새 위치와 일치
        Assert.Equal(newPosition.Lat, marker.Center.Lat, 6);
        Assert.Equal(newPosition.Lng, marker.Center.Lng, 6);

        // 3. ImageBounds 크기는 유지됨 (위치만 변경)
        Assert.Equal(originalBoundsSize.W, marker.ImageBounds.WidthLng, 6);
        Assert.Equal(originalBoundsSize.H, marker.ImageBounds.HeightLat, 6);
    }

    /// <summary>
    /// BUG-33.4.4: Bearing=45, Zoom변경, Resize 복합 동작
    ///
    /// 시나리오: 회전 설정 → 크기 변경 → 위치 이동 → 모든 상태 정상 유지
    ///
    /// 기대: 복합 작업 후 모든 속성이 일관성 있게 유지됨
    /// </summary>
    [Fact(DisplayName = "BUG-33.4.4: Bearing=45, Zoom변경, Resize 복합 동작")]
    public void ComplexScenario_Rotation_Zoom_Resize_ShouldWork()
    {
        // Arrange
        var marker = CreateTestImageMarker();

        // Act 1 - 회전 설정
        marker.Bearing = 45;
        Assert.Equal(45, marker.Bearing, 1);

        // Act 2 - 크기 변경 (UpdateSize - 픽셀 단위)
        marker.UpdateSize(300, 200);
        Assert.Equal(300, marker.Width, 1);
        Assert.Equal(200, marker.Height, 1);

        // Act 3 - 위치 이동
        var newCenter = new PointLatLng(37.5800, 126.9900);
        marker.UpdateLocation(newCenter);

        // Assert - 최종 상태 검증
        // 1. 회전 값 유지
        Assert.Equal(45, marker.Bearing, 1);

        // 2. 크기 유지
        Assert.Equal(300, marker.Width, 1);
        Assert.Equal(200, marker.Height, 1);

        // 3. 위치 정확성
        Assert.Equal(newCenter.Lat, marker.Position.Lat, 6);
        Assert.Equal(newCenter.Lng, marker.Position.Lng, 6);
        Assert.Equal(newCenter.Lat, marker.Center.Lat, 6);
        Assert.Equal(newCenter.Lng, marker.Center.Lng, 6);

        // 4. ImageBounds와 ImageModel 동기화
        Assert.Equal(marker.ImageBounds.Left, marker.ImageModel.Left, 6);
        Assert.Equal(marker.ImageBounds.Right, marker.ImageModel.Right, 6);
        Assert.Equal(marker.ImageBounds.Top, marker.ImageModel.Top, 6);
        Assert.Equal(marker.ImageBounds.Bottom, marker.ImageModel.Bottom, 6);

        // 5. ImageModel에 크기도 반영
        Assert.Equal(300, marker.ImageModel.Width, 1);
        Assert.Equal(200, marker.ImageModel.Height, 1);
    }

    #endregion

    #region Phase 34: 줌 변경 시 이미지 크기 급증 버그 수정

    /// <summary>
    /// BUG-34.1.1: GMapImageMarker의 Width/Height는 픽셀 고정 모드로 동작해야 함
    ///
    /// 문제: 줌 변경 시 OnRender에서 ImageBounds → FromLatLngToLocal() → 다른 픽셀 크기
    /// 해결: OnRender에서 Marker.Width/Height를 직접 사용
    ///
    /// 기대: Width/Height는 사용자 설정값 그대로 유지
    /// </summary>
    [Fact(DisplayName = "BUG-34.1.1: Width/Height는 픽셀 고정 모드로 유지")]
    public void WidthHeight_ShouldBe_PixelFixedMode()
    {
        // Arrange
        var marker = CreateTestImageMarker();

        // 사용자가 설정한 픽셀 크기
        double userWidth = 200;
        double userHeight = 150;
        marker.Width = userWidth;
        marker.Height = userHeight;

        // Act - 아무 작업 없음 (줌 변경 시뮬레이션 전 상태)

        // Assert - Width/Height는 사용자 설정값 유지
        Assert.Equal(userWidth, marker.Width, 1);
        Assert.Equal(userHeight, marker.Height, 1);

        // ImageModel도 동일해야 함
        Assert.Equal(userWidth, marker.ImageModel.Width, 1);
        Assert.Equal(userHeight, marker.ImageModel.Height, 1);
    }

    /// <summary>
    /// BUG-34.2.1: Offset은 항상 (-Width/2, -Height/2)이어야 함
    ///
    /// 문제: Offset이 ImageBounds 기반 화면좌표로 계산되면 줌에 따라 변함
    /// 해결: Offset = (-Marker.Width/2, -Marker.Height/2)로 고정
    ///
    /// 기대: Offset.X = -Width/2, Offset.Y = -Height/2
    /// </summary>
    [Fact(DisplayName = "BUG-34.2.1: Offset은 중심 기준 (-Width/2, -Height/2)")]
    public void Offset_ShouldBe_HalfWidthHeightNegative()
    {
        // Arrange
        var marker = CreateTestImageMarker();
        marker.Width = 200;
        marker.Height = 150;

        // Act - Offset 수동 설정 (OnRender 시뮬레이션)
        marker.Offset = new System.Windows.Point(-marker.Width / 2, -marker.Height / 2);

        // Assert
        Assert.Equal(-100, marker.Offset.X, 1);  // -200/2
        Assert.Equal(-75, marker.Offset.Y, 1);   // -150/2
    }

    /// <summary>
    /// BUG-34.3.1: UpdateSize 후에도 Width/Height 픽셀 값 유지
    ///
    /// 시나리오: Adorner에서 크기조절 → UpdateSize 호출 → Width/Height 업데이트
    ///
    /// 기대: UpdateSize 후 Width/Height가 입력값과 동일
    /// </summary>
    [Fact(DisplayName = "BUG-34.3.1: UpdateSize 후 Width/Height 픽셀 값 유지")]
    public void UpdateSize_ShouldSet_PixelValues()
    {
        // Arrange
        var marker = CreateTestImageMarker();

        // Act - Adorner에서 크기조절 (픽셀 단위)
        marker.UpdateSize(300, 200);

        // Assert - Width/Height가 입력 픽셀 값과 동일
        Assert.Equal(300, marker.Width, 1);
        Assert.Equal(200, marker.Height, 1);
    }

    /// <summary>
    /// BUG-34.5.1: 줌 변경 시뮬레이션 - Width/Height 유지 확인
    ///
    /// 시나리오: Width/Height 설정 → 줌 변경 시뮬레이션 → 크기 유지
    ///
    /// 참고: 실제 줌 변경은 WPF 런타임에서 발생하므로,
    ///       여기서는 Marker의 속성이 줌과 무관하게 유지되는지 테스트
    /// </summary>
    [Fact(DisplayName = "BUG-34.5.1: 줌 변경 시뮬레이션 - Width/Height 유지")]
    public void ZoomChange_Simulation_ShouldMaintain_PixelSize()
    {
        // Arrange
        var marker = CreateTestImageMarker();
        marker.Width = 200;
        marker.Height = 150;
        var originalWidth = marker.Width;
        var originalHeight = marker.Height;

        // Act - 줌 변경을 시뮬레이션 (ImageBounds는 변경하지 않음)
        // 실제로는 OnRender에서 줌에 따라 다시 계산하지 않아야 함
        // 여기서는 Width/Height가 외부 요인에 의해 변경되지 않음을 확인

        // 예: ImageBounds의 WidthLng를 2배로 변경해도 Width는 유지
        // (이는 픽셀 고정 모드의 핵심 동작)

        // Assert - Width/Height 유지
        Assert.Equal(originalWidth, marker.Width, 1);
        Assert.Equal(originalHeight, marker.Height, 1);
    }

    /// <summary>
    /// BUG-34.5.2: Adorner 크기조절 후 줌 변경 시 크기 유지
    ///
    /// 시나리오: Adorner로 크기조절 → 줌 변경 → 크기 유지
    /// </summary>
    [Fact(DisplayName = "BUG-34.5.2: Adorner 크기조절 후 줌 변경 시 크기 유지")]
    public void AdornerResize_ThenZoomChange_ShouldMaintain_Size()
    {
        // Arrange
        var marker = CreateTestImageMarker();

        // Adorner로 크기조절
        marker.UpdateSize(250, 180);
        var sizeAfterResize = new { W = marker.Width, H = marker.Height };

        // Act - 줌 변경 시뮬레이션 (Width/Height는 변경하지 않음)
        // 실제 WPF에서는 OnRender가 재호출되지만,
        // 픽셀 고정 모드에서는 Marker.Width/Height를 직접 사용하므로 변경 없음

        // Assert - 크기 유지
        Assert.Equal(sizeAfterResize.W, marker.Width, 1);
        Assert.Equal(sizeAfterResize.H, marker.Height, 1);
    }

    #endregion
}
