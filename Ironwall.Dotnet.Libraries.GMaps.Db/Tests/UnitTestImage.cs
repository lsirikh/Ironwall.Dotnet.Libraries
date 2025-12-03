using Caliburn.Micro;
using Dapper;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Db.Models;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using MySql.Data.MySqlClient;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Db.Tests;

/****************************************************************************
   Purpose      : Image CRUD 단위 테스트 (Phase 22)
   Created By   : Claude Code
   Created On   : 2025-12-03
   PRD Reference: Docs/PRD/PRD_ImageOverlay_Feature.md
****************************************************************************/

/// <summary>
/// GMapDbSymbolService Image 전용 Fixture – DB 한 번만 세팅 &amp; 해제
/// </summary>
public sealed class GMapDbImageFixture : GMapBaseSymbolFixture
{
    #region - Properties -
    /// <summary>삽입된 Image ID 목록</summary>
    public List<int> InsertedImageIds { get; } = new();

    /// <summary>테스트용 Image 생성 개수</summary>
    public int ImageCount { get; set; } = 5;
    #endregion

    #region - Private Methods -
    /// <summary>
    /// DB 내부 테이블만 삭제
    /// </summary>
    /// <returns>Task</returns>
    protected override async Task DropTablesAsync()
    {
        var csb = new MySqlConnectionStringBuilder
        {
            Server = _setup.IpDbServer,
            Port = (uint)_setup.PortDbServer,
            UserID = _setup.UidDbServer,
            Password = _setup.PasswordDbServer,
            Database = _setup.DbDatabase,
            SslMode = MySqlSslMode.Disabled
        };

        await using var conn = new MySqlConnection(csb.ToString());
        await conn.OpenAsync();

        // Images 테이블 삭제
        await conn.ExecuteAsync("DROP TABLE IF EXISTS `Images`;");
    }
    #endregion

    #region - Seed Data Methods -
    /// <summary>
    /// Image 시드 데이터 생성
    /// </summary>
    /// <returns>생성된 Image ID 목록</returns>
    public async Task<List<int>> SeedImagesAsync(int count = 5)
    {
        var random = new Random();
        var createdIds = new List<int>();

        for (int i = 1; i <= count; i++)
        {
            var image = new ImageModel
            {
                Title = $"테스트_이미지_{i:00}",
                FilePath = $"C:\\Images\\test_image_{i}.png",
                Left = 126.0 + random.NextDouble() * 0.5,
                Right = 127.0 + random.NextDouble() * 0.5,
                Top = 38.0 + random.NextDouble() * 0.5,
                Bottom = 37.0 + random.NextDouble() * 0.5,
                Latitude = 37.5 + random.NextDouble() * 0.1,
                Longitude = 126.9 + random.NextDouble() * 0.1,
                Altitude = random.Next(0, 100),
                Width = 200 + random.Next(0, 300),
                Height = 150 + random.Next(0, 250),
                Opacity = 0.5 + random.NextDouble() * 0.5, // 0.5 ~ 1.0
                Rotation = random.NextDouble() * 360,
                Visibility = random.Next(2) == 0,
                HasGeoReference = random.Next(2) == 0,
                CoordinateSystem = "EPSG:4326"
            };

            int id = await Svc.InsertImageAsync(image);
            createdIds.Add(id);
            InsertedImageIds.Add(id);
        }

        return createdIds;
    }
    #endregion
}

/// <summary>
/// xUnit 컬렉션 정의
/// </summary>
[CollectionDefinition(nameof(GMapDbImageCollection))]
public sealed class GMapDbImageCollection : ICollectionFixture<GMapDbImageFixture> { }

/*======================================================================
 *  TEST-IMG-2.x: Image CRUD 기본 테스트
 *====================================================================*/
[Collection(nameof(GMapDbImageCollection))]
public class GMapDbImage_CrudTests
{
    private readonly GMapDbImageFixture _fx;

    /// <summary>
    /// 테스트 클래스 생성자
    /// </summary>
    /// <param name="fx">픽스처</param>
    public GMapDbImage_CrudTests(GMapDbImageFixture fx) => _fx = fx;

    /// <summary>
    /// TEST-IMG-2.1: InsertImageAsync 테스트
    ///
    /// Image 삽입 및 조회 테스트
    /// </summary>
    /// <returns>Task</returns>
    [Fact(DisplayName = "TEST-IMG-2.1: InsertImageAsync – Insert & Fetch")]
    public async Task TEST_IMG_2_1_Insert_And_Fetch_Images()
    {
        // Arrange & Act: 이미지 시드 데이터 생성
        var createdIds = await _fx.SeedImagesAsync(_fx.ImageCount);

        // Assert 1: 모든 이미지 조회
        var all = await _fx.Svc.FetchImagesAsync();
        Assert.NotNull(all);
        Assert.True(all!.Count >= _fx.ImageCount);

        // Assert 2: 각 이미지 개별 조회 및 필드 검증
        foreach (var id in createdIds)
        {
            var one = await _fx.Svc.FetchImageAsync(id);
            Assert.NotNull(one);
            Assert.Equal(id, one!.Id);
            Assert.NotEmpty(one.FilePath);

            // 위치 범위 검증
            Assert.True(one.Latitude >= -90 && one.Latitude <= 90);
            Assert.True(one.Longitude >= -180 && one.Longitude <= 180);
            Assert.True(one.Left <= one.Right);
            Assert.True(one.Bottom <= one.Top);

            // 속성 범위 검증
            Assert.True(one.Opacity >= 0 && one.Opacity <= 1);
            Assert.True(one.Rotation >= 0 && one.Rotation < 360);
            Assert.True(one.Width >= 0);
            Assert.True(one.Height >= 0);
        }
    }

    /// <summary>
    /// TEST-IMG-2.2: GetAllImagesAsync 테스트
    ///
    /// 전체 이미지 목록 조회 테스트
    /// </summary>
    /// <returns>Task</returns>
    [Fact(DisplayName = "TEST-IMG-2.2: FetchImagesAsync – Get All Images")]
    public async Task TEST_IMG_2_2_FetchAll_Images()
    {
        // Arrange: 추가 이미지 생성
        await _fx.SeedImagesAsync(3);

        // Act
        var all = await _fx.Svc.FetchImagesAsync();

        // Assert
        Assert.NotNull(all);
        Assert.True(all!.Count > 0);

        // 모든 이미지에 필수 속성이 있는지 확인
        Assert.All(all, img =>
        {
            Assert.True(img.Id > 0);
            Assert.NotEmpty(img.FilePath);
        });
    }

    /// <summary>
    /// TEST-IMG-2.3: UpdateImageAsync 테스트
    ///
    /// 이미지 업데이트 테스트
    /// </summary>
    /// <returns>Task</returns>
    [Fact(DisplayName = "TEST-IMG-2.3: UpdateImageAsync – Update Works")]
    public async Task TEST_IMG_2_3_Update_Image_Works()
    {
        // Arrange: 이미지 생성
        var createdIds = await _fx.SeedImagesAsync(1);
        var image = await _fx.Svc.FetchImageAsync(createdIds.First());
        Assert.NotNull(image);

        // Act: 수정
        image!.Title = "UPDATED_IMAGE_TITLE";
        image.FilePath = "C:\\Updated\\new_path.png";
        image.Left = 125.5;
        image.Right = 128.5;
        image.Top = 39.0;
        image.Bottom = 36.0;
        image.Opacity = 0.75;
        image.Rotation = 90.0;
        image.Visibility = false;
        image.HasGeoReference = true;
        image.CoordinateSystem = "EPSG:32652";

        var updated = await _fx.Svc.UpdateImageAsync(image);

        // Assert
        Assert.NotNull(updated);
        Assert.Equal(image.Id, updated!.Id);
        Assert.Equal("UPDATED_IMAGE_TITLE", updated.Title);
        Assert.Equal("C:\\Updated\\new_path.png", updated.FilePath);
        Assert.Equal(125.5, updated.Left);
        Assert.Equal(128.5, updated.Right);
        Assert.Equal(39.0, updated.Top);
        Assert.Equal(36.0, updated.Bottom);
        Assert.Equal(0.75, updated.Opacity);
        Assert.Equal(90.0, updated.Rotation);
        Assert.False(updated.Visibility);
        Assert.True(updated.HasGeoReference);
        Assert.Equal("EPSG:32652", updated.CoordinateSystem);
    }

    /// <summary>
    /// TEST-IMG-2.4: DeleteImageAsync 테스트
    ///
    /// 이미지 삭제 테스트
    /// </summary>
    /// <returns>Task</returns>
    [Fact(DisplayName = "TEST-IMG-2.4: DeleteImageAsync – Delete Works")]
    public async Task TEST_IMG_2_4_Delete_Image_Works()
    {
        // Arrange: 이미지 생성
        var createdIds = await _fx.SeedImagesAsync(1);
        var imageId = createdIds.First();
        var image = await _fx.Svc.FetchImageAsync(imageId);
        Assert.NotNull(image);

        // Act: 삭제
        bool ok = await _fx.Svc.DeleteImageAsync(imageId);

        // Assert
        Assert.True(ok);

        // 실제로 사라졌는지 확인
        var fetched = await _fx.Svc.FetchImageAsync(imageId);
        Assert.Null(fetched);
    }

    /// <summary>
    /// 잘못된 매개변수 예외 처리 테스트
    /// </summary>
    /// <returns>Task</returns>
    [Fact(DisplayName = "Images – Invalid Parameters")]
    public async Task Invalid_Parameters_Throw_Exceptions()
    {
        // 잘못된 ID로 조회
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _fx.Svc.FetchImageAsync(-1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _fx.Svc.FetchImageAsync(0));

        // 잘못된 ID로 삭제
        await Assert.ThrowsAsync<ArgumentException>(
            () => _fx.Svc.DeleteImageAsync(-1));
        await Assert.ThrowsAsync<ArgumentException>(
            () => _fx.Svc.DeleteImageAsync(0));

        // 잘못된 ID로 업데이트
        var invalidImage = new ImageModel { Id = -1, FilePath = "test.png" };
        await Assert.ThrowsAsync<ArgumentException>(
            () => _fx.Svc.UpdateImageAsync(invalidImage));
    }

    /// <summary>
    /// 완전한 Image 워크플로우 테스트
    /// </summary>
    /// <returns>Task</returns>
    [Fact(DisplayName = "Complete Image Workflow")]
    public async Task Complete_Image_Workflow()
    {
        // 1. Image 생성
        var image = new ImageModel
        {
            Title = "통합테스트_이미지",
            FilePath = "C:\\workflow\\test.png",
            Left = 126.0,
            Right = 127.0,
            Top = 38.0,
            Bottom = 37.0,
            Latitude = 37.5,
            Longitude = 126.5,
            Altitude = 0,
            Width = 256,
            Height = 256,
            Opacity = 0.8,
            Rotation = 0,
            Visibility = true,
            HasGeoReference = true,
            CoordinateSystem = "EPSG:4326"
        };

        int imageId = await _fx.Svc.InsertImageAsync(image);
        Assert.True(imageId > 0);

        // 2. 생성된 Image 검증
        var fetchedImage = await _fx.Svc.FetchImageAsync(imageId);
        Assert.NotNull(fetchedImage);
        Assert.Equal("통합테스트_이미지", fetchedImage!.Title);
        Assert.Equal("C:\\workflow\\test.png", fetchedImage.FilePath);
        Assert.Equal(126.0, fetchedImage.Left);
        Assert.Equal(127.0, fetchedImage.Right);

        // 3. Image 속성 변경
        fetchedImage.Opacity = 0.5;
        fetchedImage.Rotation = 45.0;
        fetchedImage.Title = "수정된_이미지";

        var updatedImage = await _fx.Svc.UpdateImageAsync(fetchedImage);
        Assert.Equal(0.5, updatedImage!.Opacity);
        Assert.Equal(45.0, updatedImage.Rotation);
        Assert.Equal("수정된_이미지", updatedImage.Title);

        // 4. 정리 (삭제)
        bool deleted = await _fx.Svc.DeleteImageAsync(imageId);
        Assert.True(deleted);

        // 5. 삭제 확인
        var deletedImage = await _fx.Svc.FetchImageAsync(imageId);
        Assert.Null(deletedImage);
    }
}
