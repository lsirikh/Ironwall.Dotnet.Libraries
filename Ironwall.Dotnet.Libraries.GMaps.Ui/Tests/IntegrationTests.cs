using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/****************************************************************************
   Purpose      : Image Overlay 통합 테스트 (Phase 27)
   Created By   : Claude Code
   Created On   : 2025-12-03
   PRD Reference: Docs/PRD/PRD_ImageOverlay_Feature.md
****************************************************************************/

/// <summary>
/// Image Overlay 통합 테스트
/// - MarkerFactory: CreateImageMarker 테스트
/// - PropertyPanelFactory: GMapPropertyImageControl 생성 테스트
/// - 전체 흐름 테스트
/// </summary>
public class IntegrationTests
{
    private readonly Mock<ILogService> _mockLog;
    private readonly Mock<DeviceProvider> _mockDeviceProvider;

    public IntegrationTests()
    {
        _mockLog = new Mock<ILogService>();
        _mockDeviceProvider = new Mock<DeviceProvider>();
    }

    #region Helper Methods

    /// <summary>
    /// 테스트용 ImageModel 생성
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

    #region Phase 27.1: Factory 통합 테스트

    /// <summary>
    /// TEST-IMG-7.1: MarkerFactory - CreateImageMarker 메서드 존재 및 동작 확인
    ///
    /// MarkerFactory에 CreateImageMarker가 있으면 GMapImageMarker를 생성해야 함
    /// </summary>
    [Fact(DisplayName = "TEST-IMG-7.1: MarkerFactory - CreateImageMarker로 GMapImageMarker 생성")]
    public void TEST_IMG_7_1_MarkerFactory_CreateImageMarker_ShouldReturnGMapImageMarker()
    {
        // Arrange
        var factory = new MarkerFactory(_mockLog.Object, _mockDeviceProvider.Object);
        var imageModel = CreateTestImageModel();

        // Act
        var marker = factory.CreateImageMarker(imageModel);

        // Assert
        Assert.NotNull(marker);
        Assert.IsType<GMapImageMarker>(marker);
        Assert.Equal("Test Image", marker.Title);
    }

    /// <summary>
    /// TEST-IMG-7.2: PropertyPanelFactory - GMapImageMarker 매핑 등록 확인 (리플렉션 사용)
    ///
    /// _markerToPanelMap에 GMapImageMarker → GMapPropertyImageControl 매핑이 있어야 함
    /// WPF 컨트롤은 STA 스레드에서만 생성 가능하므로 리플렉션으로 매핑만 확인
    /// </summary>
    [Fact(DisplayName = "TEST-IMG-7.2: PropertyPanelFactory - GMapImageMarker 매핑 등록 확인")]
    public void TEST_IMG_7_2_PropertyPanelFactory_ShouldHaveImageMarkerMapping()
    {
        // Arrange
        var factory = new PropertyPanelFactory(_mockLog.Object, _mockDeviceProvider.Object);

        // Act - 리플렉션으로 _markerToPanelMap 접근
        var mapField = typeof(PropertyPanelFactory)
            .GetField("_markerToPanelMap", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(mapField);

        var map = mapField.GetValue(factory) as Dictionary<Type, Type>;
        Assert.NotNull(map);

        // Assert - GMapImageMarker → GMapPropertyImageControl 매핑이 있어야 함
        Assert.True(map.ContainsKey(typeof(GMapImageMarker)));
        Assert.Equal(typeof(GMapPropertyImageControl), map[typeof(GMapImageMarker)]);
    }

    /// <summary>
    /// TEST-IMG-7.3: PropertyPanelFactory - 모든 마커 타입에 대한 매핑 확인
    ///
    /// GMapImageMarker를 포함한 모든 마커 타입이 등록되어 있어야 함
    /// </summary>
    [Fact(DisplayName = "TEST-IMG-7.3: PropertyPanelFactory - 전체 마커 매핑 확인")]
    public void TEST_IMG_7_3_PropertyPanelFactory_ShouldHaveAllMarkerMappings()
    {
        // Arrange
        var factory = new PropertyPanelFactory(_mockLog.Object, _mockDeviceProvider.Object);

        // Act - 리플렉션으로 _markerToPanelMap 접근
        var mapField = typeof(PropertyPanelFactory)
            .GetField("_markerToPanelMap", BindingFlags.NonPublic | BindingFlags.Instance);
        var map = mapField!.GetValue(factory) as Dictionary<Type, Type>;

        // Assert - 8개 마커 타입 매핑 확인 (Image 포함)
        Assert.NotNull(map);
        Assert.Equal(8, map.Count);
        Assert.Contains(typeof(GMapImageMarker), map.Keys);
    }

    #endregion

    #region Phase 27.2: 전체 흐름 테스트

    /// <summary>
    /// TEST-IMG-7.4: ImageModel → GMapImageMarker 마커 생성 + Factory 매핑 확인
    ///
    /// ImageModel로 마커 생성 후 PropertyPanelFactory가 올바른 타입을 반환하는지 확인
    /// (WPF 컨트롤 생성은 STA 스레드 필요하므로 매핑 확인만 수행)
    /// </summary>
    [Fact(DisplayName = "TEST-IMG-7.4: ImageModel → Marker 생성 + Factory 매핑 확인")]
    public void TEST_IMG_7_4_FullFlow_ImageModelToMarkerWithFactoryMapping()
    {
        // Arrange
        var markerFactory = new MarkerFactory(_mockLog.Object, _mockDeviceProvider.Object);
        var propertyFactory = new PropertyPanelFactory(_mockLog.Object, _mockDeviceProvider.Object);
        var imageModel = CreateTestImageModel();

        // Act - Step 1: MarkerFactory로 마커 생성
        var marker = markerFactory.CreateImageMarker(imageModel);

        // Act - Step 2: PropertyPanelFactory의 매핑 확인 (리플렉션)
        var mapField = typeof(PropertyPanelFactory)
            .GetField("_markerToPanelMap", BindingFlags.NonPublic | BindingFlags.Instance);
        var map = mapField!.GetValue(propertyFactory) as Dictionary<Type, Type>;

        // Assert - 마커 생성 및 타입 매핑 확인
        Assert.NotNull(marker);
        Assert.IsType<GMapImageMarker>(marker);
        Assert.NotNull(map);
        Assert.True(map.TryGetValue(marker.GetType(), out var panelType));
        Assert.Equal(typeof(GMapPropertyImageControl), panelType);
    }

    /// <summary>
    /// TEST-IMG-7.5: GMapImageMarker는 IEditableMarker이므로 기존 Adorner 시스템과 호환
    ///
    /// IEditableMarker로 캐스팅 가능하므로 기존 MarkerEditAdorner와 호환됨
    /// </summary>
    [Fact(DisplayName = "TEST-IMG-7.5: GMapImageMarker - IEditableMarker 호환성")]
    public void TEST_IMG_7_5_GMapImageMarker_ShouldBeCompatibleWithAdornerSystem()
    {
        // Arrange
        var markerFactory = new MarkerFactory(_mockLog.Object, _mockDeviceProvider.Object);
        var imageModel = CreateTestImageModel();

        // Act
        var marker = markerFactory.CreateImageMarker(imageModel);

        // Assert - IEditableMarker로 캐스팅 가능해야 Adorner 시스템과 호환
        Assert.IsAssignableFrom<IEditableMarker>(marker);
        Assert.IsAssignableFrom<IImageEditableMarker>(marker);

        // Adorner에서 사용하는 주요 메서드들 확인
        var editableMarker = marker as IEditableMarker;
        Assert.NotNull(editableMarker);

        // UpdateLocation, UpdateSize, UpdateRotation은 IEditableMarker 인터페이스에서 제공
        editableMarker.UpdateLocation(new GMap.NET.PointLatLng(37.5, 126.5));
        editableMarker.UpdateSize(2.0, 1.0);
        editableMarker.UpdateRotation(45.0);

        Assert.Equal(45.0, editableMarker.Bearing);
    }

    #endregion

    #region Phase 30: DB 저장/로드 통합 테스트

    /// <summary>
    /// TEST-IMG-8.1: GMapImageMarker.ImageModel을 통해 DB 서비스에 전달 가능
    ///
    /// GMapImageMarker의 ImageModel 속성이 IImageModel로 DB 서비스에 전달될 수 있음을 확인
    /// </summary>
    [Fact(DisplayName = "TEST-IMG-8.1: GMapImageMarker.ImageModel - DB 서비스 전달 가능")]
    public void TEST_IMG_8_1_GMapImageMarker_ImageModel_ShouldBePassableToDbService()
    {
        // Arrange
        var marker = CreateTestImageMarker();

        // Act - ImageModel 속성을 IImageModel로 캐스팅
        var imageModel = marker.ImageModel as IImageModel;

        // Assert
        Assert.NotNull(imageModel);
        Assert.Equal("Test Image", imageModel.Title);
        Assert.Equal("C:\\test.png", imageModel.FilePath);
        Assert.Equal(0.8, imageModel.Opacity);
    }

    /// <summary>
    /// TEST-IMG-8.2: Mock DB 서비스로 InsertImageAsync 호출 시나리오 확인
    ///
    /// GMapImageMarker.ImageModel을 InsertImageAsync에 전달할 수 있음을 확인
    /// </summary>
    [Fact(DisplayName = "TEST-IMG-8.2: Mock DB 서비스 - InsertImageAsync 호출 시나리오")]
    public async Task TEST_IMG_8_2_MockDbService_InsertImageAsync_Scenario()
    {
        // Arrange
        var mockDbService = new Mock<IGMapDbSymbolService>();
        mockDbService.Setup(s => s.InsertImageAsync(It.IsAny<IImageModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(100); // DB에서 생성된 ID 반환

        var marker = CreateTestImageMarker();

        // Act - DB 서비스로 저장 시나리오
        var savedId = await mockDbService.Object.InsertImageAsync(marker.ImageModel);

        // Assert
        Assert.Equal(100, savedId);
        mockDbService.Verify(s => s.InsertImageAsync(
            It.Is<IImageModel>(m => m.Title == "Test Image" && m.FilePath == "C:\\test.png"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// TEST-IMG-8.3: Mock DB 서비스로 UpdateImageAsync 호출 시나리오 확인
    ///
    /// GMapImageMarker의 속성 변경 후 ImageModel을 UpdateImageAsync에 전달
    /// </summary>
    [Fact(DisplayName = "TEST-IMG-8.3: Mock DB 서비스 - UpdateImageAsync 호출 시나리오")]
    public async Task TEST_IMG_8_3_MockDbService_UpdateImageAsync_Scenario()
    {
        // Arrange
        var mockDbService = new Mock<IGMapDbSymbolService>();
        var updatedModel = CreateTestImageModel();
        updatedModel.Opacity = 0.5;
        updatedModel.Rotation = 45.0;

        mockDbService.Setup(s => s.UpdateImageAsync(It.IsAny<IImageModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedModel);

        var marker = CreateTestImageMarker();

        // Act - 마커 속성 변경
        marker.Opacity = 0.5;
        marker.UpdateRotation(45.0);

        // Act - DB 서비스로 업데이트 시나리오
        var result = await mockDbService.Object.UpdateImageAsync(marker.ImageModel);

        // Assert
        Assert.NotNull(result);
        mockDbService.Verify(s => s.UpdateImageAsync(
            It.Is<IImageModel>(m => m.Opacity == 0.5),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// TEST-IMG-8.4: Mock DB 서비스로 DeleteImageAsync 호출 시나리오 확인
    ///
    /// GMapImageMarker.ImageModel.Id를 DeleteImageAsync에 전달
    /// </summary>
    [Fact(DisplayName = "TEST-IMG-8.4: Mock DB 서비스 - DeleteImageAsync 호출 시나리오")]
    public async Task TEST_IMG_8_4_MockDbService_DeleteImageAsync_Scenario()
    {
        // Arrange
        var mockDbService = new Mock<IGMapDbSymbolService>();
        mockDbService.Setup(s => s.DeleteImageAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var marker = CreateTestImageMarker();

        // Act - DB 서비스로 삭제 시나리오
        var deleted = await mockDbService.Object.DeleteImageAsync(marker.ImageModel.Id);

        // Assert
        Assert.True(deleted);
        mockDbService.Verify(s => s.DeleteImageAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// TEST-IMG-8.5: Mock DB 서비스로 FetchImagesAsync 후 마커 생성 시나리오
    ///
    /// DB에서 이미지 목록을 가져와서 GMapImageMarker로 변환하는 전체 흐름 확인
    /// </summary>
    [Fact(DisplayName = "TEST-IMG-8.5: Mock DB 서비스 - FetchImagesAsync + 마커 생성 시나리오")]
    public async Task TEST_IMG_8_5_MockDbService_FetchImages_CreateMarkers_Scenario()
    {
        // Arrange
        var mockDbService = new Mock<IGMapDbSymbolService>();
        var testImages = new List<IImageModel>
        {
            CreateTestImageModel(),
            new ImageModel
            {
                Id = 2,
                Title = "Test Image 2",
                FilePath = "C:\\test2.png",
                Left = 127.0,
                Right = 128.0,
                Top = 39.0,
                Bottom = 38.0,
                Opacity = 0.6,
                Rotation = 90.0,
                Latitude = 38.5,
                Longitude = 127.5
            }
        };

        mockDbService.Setup(s => s.FetchImagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(testImages);

        var markerFactory = new MarkerFactory(_mockLog.Object, _mockDeviceProvider.Object);

        // Act - DB에서 이미지 목록 가져오기
        var images = await mockDbService.Object.FetchImagesAsync();

        // Act - 마커 생성
        var markers = new List<GMapImageMarker>();
        foreach (var img in images!)
        {
            var marker = markerFactory.CreateImageMarker(img);
            markers.Add(marker);
        }

        // Assert
        Assert.Equal(2, markers.Count);
        Assert.Equal("Test Image", markers[0].Title);
        Assert.Equal("Test Image 2", markers[1].Title);
        Assert.Equal(0.8, markers[0].Opacity);
        Assert.Equal(0.6, markers[1].Opacity);
    }

    #endregion
}
