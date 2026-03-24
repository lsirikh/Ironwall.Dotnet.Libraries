using Caliburn.Micro;
using Dapper;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Db.Models;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using MySql.Data.MySqlClient;
using System;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Db.Tests;

/// <summary>
/// GMapDbSymbolService PidsSymbol 전용 Fixture
/// </summary>
public sealed class GMapDbPidsSymbolFixture : GMapBaseSymbolFixture
{
    #region - Private Methods -
    /// <summary>
    /// DB 내부 테이블 삭제 (CASCADE 순서 고려)
    /// </summary>
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

        // Foreign Key 때문에 순서 중요: 자식 → 부모
        foreach (var t in _tables)
            await conn.ExecuteAsync($"DROP TABLE IF EXISTS `{t}`;");
    }
    #endregion

    #region - Seed Data Methods -
    /// <summary>
    /// PidsSymbol 시드 데이터 생성
    /// </summary>
    [Fact(DisplayName = "PidsSymbol DB Insert Service")]
    public async Task SeedPidsSymbolsAsync()
    {
        var random = new Random();
        var deviceTypes = Enum.GetValues<EnumDeviceType>();
        var operationStates = Enum.GetValues<EnumOperationState>();
        var eventStatuses = Enum.GetValues<EnumEventStatus>();
        var colorTypes = Enum.GetValues<EnumColorType>();

        for (int i = 1; i <= SymbolCount; i++)
        {
            var pidsSymbol = new PidsSymbolModel
            {
                Pid = 10000 + i,
                Title = $"PIDS_{i:00}",
                OperationState = operationStates[random.Next(operationStates.Length)],
                Latitude = 37.4 + random.NextDouble() * 0.4, // 서울 남쪽
                Longitude = 126.8 + random.NextDouble() * 0.4,
                Altitude = random.Next(0, 500),
                Bearing = random.NextDouble() * 360,
                Width = 25 + random.NextDouble() * 25,
                Height = 25 + random.NextDouble() * 25,
                Category = EnumMarkerCategory.PIDS_EQUIPMENT,
                ShowShape = random.Next(2) == 0,
                ShowTitle = random.Next(2) == 0,
                FillColor = colorTypes[random.Next(colorTypes.Length)],
                StrokeColor = colorTypes[random.Next(colorTypes.Length)],
                StrokeThickness = 1.0 + random.NextDouble() * 3.0,  // 1.0 ~ 4.0
                // PidsSymbol 전용 속성
                LinkedDeviceId = 20000 + i,
                DeviceType = deviceTypes[random.Next(deviceTypes.Length)],
                ShowFOV = random.Next(2) == 0,
                FOVColor = colorTypes[random.Next(colorTypes.Length)],
                FOVOpacity = 0.2 + random.NextDouble() * 0.6, // 0.2 ~ 0.8
                EventStatus = eventStatuses[random.Next(eventStatuses.Length)]
                // DetectionRange, DetectionAngle, DetectionBearing는 실시간 데이터이므로 저장하지 않음
            };

            int id = await Svc.InsertPidsSymbolAsync(pidsSymbol);
            InsertedSymbolIds.Add(id);
        }
    }

    /// <summary>
    /// 특정 DeviceType PidsSymbol 시드 데이터 생성
    /// </summary>
    /// <param name="deviceType">생성할 DeviceType</param>
    /// <param name="count">생성 개수</param>
    /// <returns>생성된 PidsSymbol ID 목록</returns>
    public async Task<List<int>> SeedPidsSymbolsByDeviceTypeAsync(EnumDeviceType deviceType, int count = 15)
    {
        var random = new Random();
        var createdIds = new List<int>();

        for (int i = 1; i <= count; i++)
        {
            var pidsSymbol = new PidsSymbolModel
            {
                Pid = 11000 + i,
                Title = $"{deviceType}_장비_{i:00}",
                OperationState = EnumOperationState.ACTIVATED,
                Latitude = 37.55 + random.NextDouble() * 0.05,
                Longitude = 126.95 + random.NextDouble() * 0.05,
                Altitude = 0,
                Bearing = i * 45, // 45도씩 회전
                Width = 30,
                Height = 30,
                Category = EnumMarkerCategory.PIDS_EQUIPMENT,
                ShowShape = true,
                ShowTitle = true,
                FillColor = EnumColorType.Blue,
                StrokeColor = EnumColorType.White,
                StrokeThickness = 2.0,
                // PidsSymbol 전용 속성
                LinkedDeviceId = 21000 + i,
                DeviceType = deviceType,
                ShowFOV = deviceType == EnumDeviceType.IpCamera, // 카메라만 FOV 표시
                FOVColor = EnumColorType.Red,
                FOVOpacity = 0.5,
                EventStatus = EnumEventStatus.Normal
            };

            int id = await Svc.InsertPidsSymbolAsync(pidsSymbol);
            InsertedSymbolIds.Add(id);
            createdIds.Add(id);
        }

        return createdIds;
    }
    #endregion
}

/// <summary>
/// xUnit 컬렉션 정의 - PidsSymbol
/// </summary>
[CollectionDefinition(nameof(GMapDbPidsSymbolCollection))]
public sealed class GMapDbPidsSymbolCollection : ICollectionFixture<GMapDbPidsSymbolFixture> { }

/*======================================================================
 *  PidsSymbol CRUD 기본 테스트
 *====================================================================*/
[Collection(nameof(GMapDbPidsSymbolCollection))]
public class GMapDbPidsSymbol_BasicCrudTests
{
    private readonly GMapDbPidsSymbolFixture _fx;

    /// <summary>
    /// 테스트 클래스 생성자
    /// </summary>
    /// <param name="fx">픽스처</param>
    public GMapDbPidsSymbol_BasicCrudTests(GMapDbPidsSymbolFixture fx) => _fx = fx;

    /// <summary>
    /// PidsSymbol 삽입 및 조회 테스트
    /// </summary>
    [Fact(DisplayName = "PidsSymbols – Insert & Fetch")]
    public async Task Insert_And_Fetch_PidsSymbols()
    {
        await _fx.SeedPidsSymbolsByDeviceTypeAsync(EnumDeviceType.IpCamera);

        /* 1) FetchPidsSymbolsAsync → 전체 개수 일치 */
        var all = await _fx.Svc.FetchPidsSymbolsAsync();
        Assert.NotNull(all);
        Assert.True(all!.Count >= _fx.SymbolCount);

        /* 2) 각각 FetchPidsSymbolAsync로 필드 검증 */
        foreach (var id in _fx.InsertedSymbolIds)
        {
            var one = await _fx.Svc.FetchPidsSymbolAsync(id);
            Assert.NotNull(one);
            Assert.Equal(id, one!.Id);
            Assert.True(one.Pid > 0);
            Assert.NotEmpty(one.Title);
            Assert.True(Enum.IsDefined(typeof(EnumOperationState), one.OperationState));
            Assert.Equal(EnumMarkerCategory.PIDS_EQUIPMENT, one.Category);

            // PidsSymbol 전용 속성 검증
            Assert.True(one.LinkedDeviceId > 0);
            Assert.True(Enum.IsDefined(typeof(EnumDeviceType), one.DeviceType));
            Assert.True(Enum.IsDefined(typeof(EnumEventStatus), one.EventStatus));
            Assert.True(one.FOVOpacity >= 0.0 && one.FOVOpacity <= 1.0);

            // 위치 범위 검증
            Assert.True(one.Latitude >= -90 && one.Latitude <= 90);
            Assert.True(one.Longitude >= -180 && one.Longitude <= 180);
            Assert.True(one.Width > 0);
            Assert.True(one.Height > 0);
        }
    }

    /// <summary>
    /// PidsSymbol 업데이트 테스트
    /// </summary>
    [Fact(DisplayName = "PidsSymbols – Update")]
    public async Task Update_PidsSymbol_Works()
    {
        await _fx.SeedPidsSymbolsByDeviceTypeAsync(EnumDeviceType.Fence);
        var pidsSymbol = await _fx.Svc.FetchPidsSymbolAsync(_fx.InsertedSymbolIds.First());

        /* 수정 */
        pidsSymbol!.Title = "업데이트된_PIDS장비";
        pidsSymbol.OperationState = EnumOperationState.ERROR;
        pidsSymbol.Latitude = 35.654321;
        pidsSymbol.Longitude = 129.123456;
        pidsSymbol.Bearing = 270.0;
        pidsSymbol.Width = 45;
        pidsSymbol.Height = 45;
        pidsSymbol.ShowShape = false;
        pidsSymbol.ShowTitle = true;
        pidsSymbol.FillColor = EnumColorType.Yellow;
        pidsSymbol.StrokeColor = EnumColorType.Black;
        pidsSymbol.StrokeThickness = 2.5;
        // PidsSymbol 전용 속성 수정
        pidsSymbol.LinkedDeviceId = 99999;
        pidsSymbol.DeviceType = EnumDeviceType.IpCamera;
        pidsSymbol.ShowFOV = true;
        pidsSymbol.FOVColor = EnumColorType.Green;
        pidsSymbol.FOVOpacity = 0.7;
        pidsSymbol.EventStatus = EnumEventStatus.Detecting;

        var updated = await _fx.Svc.UpdatePidsSymbolAsync(pidsSymbol);

        Assert.NotNull(updated);
        Assert.Equal(pidsSymbol.Id, updated!.Id);
        Assert.Equal("업데이트된_PIDS장비", updated.Title);
        Assert.Equal(EnumOperationState.ERROR, updated.OperationState);
        Assert.Equal(35.654321, updated.Latitude);
        Assert.Equal(129.123456, updated.Longitude);
        Assert.Equal(270.0, updated.Bearing);
        Assert.Equal(45, updated.Width);
        Assert.Equal(45, updated.Height);
        Assert.False(updated.ShowShape);
        Assert.True(updated.ShowTitle);

        // PidsSymbol 전용 속성 검증
        Assert.Equal(99999, updated.LinkedDeviceId);
        Assert.Equal(EnumDeviceType.IpCamera, updated.DeviceType);
        Assert.True(updated.ShowFOV);
        Assert.Equal(EnumColorType.Green, updated.FOVColor);
        Assert.Equal(0.7, updated.FOVOpacity, 2); // 소수점 2자리까지 비교
        Assert.Equal(EnumEventStatus.Detecting, updated.EventStatus);
    }

    /// <summary>
    /// PidsSymbol 삭제 테스트 (CASCADE 확인)
    /// </summary>
    [Fact(DisplayName = "PidsSymbols – Delete (CASCADE)")]
    public async Task Delete_PidsSymbol_Works()
    {
        await _fx.SeedPidsSymbolsAsync();
        var pidsSymbol = await _fx.Svc.FetchPidsSymbolAsync(_fx.InsertedSymbolIds.First());

        /* 삭제 */
        bool ok = await _fx.Svc.DeletePidsSymbolAsync(pidsSymbol!);
        Assert.True(ok);

        /* 실제로 사라졌는지 확인 (JOIN 쿼리로 확인) */
        var fetched = await _fx.Svc.FetchPidsSymbolAsync(pidsSymbol!.Id);
        Assert.Null(fetched);

        /* 기본 Symbol도 같이 삭제되었는지 확인 */
        var baseSymbol = await _fx.Svc.FetchSymbolAsync(pidsSymbol.Id);
        Assert.Null(baseSymbol);
    }

    /// <summary>
    /// LinkedDeviceId로 PidsSymbol 조회 및 삭제 테스트
    /// </summary>
    [Fact(DisplayName = "PidsSymbols – Fetch & Delete By DeviceId")]
    public async Task Fetch_And_Delete_By_DeviceId_Works()
    {
        var deviceIds = await _fx.SeedPidsSymbolsByDeviceTypeAsync(EnumDeviceType.PIR, 3);
        var firstId = deviceIds.First();
        var pidsSymbol = await _fx.Svc.FetchPidsSymbolAsync(firstId);

        /* LinkedDeviceId로 조회 */
        var fetchedByDeviceId = await _fx.Svc.FetchPidsSymbolByDeviceIdAsync(pidsSymbol!.LinkedDeviceId);
        Assert.NotNull(fetchedByDeviceId);
        Assert.Equal(pidsSymbol.Id, fetchedByDeviceId!.Id);
        Assert.Equal(pidsSymbol.LinkedDeviceId, fetchedByDeviceId.LinkedDeviceId);

        /* LinkedDeviceId로 삭제 */
        bool deleted = await _fx.Svc.DeletePidsSymbolByDeviceIdAsync(pidsSymbol.LinkedDeviceId);
        Assert.True(deleted);

        /* 삭제 확인 */
        var deletedSymbol = await _fx.Svc.FetchPidsSymbolByDeviceIdAsync(pidsSymbol.LinkedDeviceId);
        Assert.Null(deletedSymbol);
    }
}

/*======================================================================
 *  PidsSymbol 특화 기능 테스트 (DeviceType 기반)
 *====================================================================*/
[Collection(nameof(GMapDbPidsSymbolCollection))]
public class GMapDbPidsSymbol_SpecializedTests
{
    private readonly GMapDbPidsSymbolFixture _fx;

    /// <summary>
    /// 테스트 클래스 생성자
    /// </summary>
    /// <param name="fx">픽스처</param>
    public GMapDbPidsSymbol_SpecializedTests(GMapDbPidsSymbolFixture fx) => _fx = fx;

    /// <summary>
    /// DeviceType별 PidsSymbol 조회 테스트
    /// </summary>
    [Fact(DisplayName = "PidsSymbols – Fetch By DeviceType")]
    public async Task Fetch_PidsSymbols_By_DeviceType_Works()
    {
        // 특정 DeviceType으로 PidsSymbol 생성
        var testDeviceType = EnumDeviceType.IpCamera;
        var cameraSymbolIds = await _fx.SeedPidsSymbolsByDeviceTypeAsync(testDeviceType, 5);

        /* DeviceType별 조회 */
        var symbolsByDeviceType = await _fx.Svc.FetchPidsSymbolsByDeviceTypeAsync(testDeviceType);

        Assert.NotNull(symbolsByDeviceType);
        Assert.True(symbolsByDeviceType!.Count >= 5);

        // 모든 PidsSymbol이 해당 DeviceType인지 확인
        Assert.All(symbolsByDeviceType, s => Assert.Equal(testDeviceType, s.DeviceType));

        // 생성한 PidsSymbol이 모두 포함되어 있는지 확인
        foreach (var createdId in cameraSymbolIds)
        {
            Assert.Contains(symbolsByDeviceType, s => s.Id == createdId);
        }

        // 카메라 타입은 FOV가 활성화되어 있는지 확인
        Assert.All(symbolsByDeviceType, s => Assert.True(s.ShowFOV));
    }

    /// <summary>
    /// EventStatus별 PidsSymbol 조회 테스트
    /// </summary>
    [Fact(DisplayName = "PidsSymbols – Fetch By EventStatus")]
    public async Task Fetch_PidsSymbols_By_EventStatus_Works()
    {
        // 다양한 EventStatus로 PidsSymbol 생성
        var eventStatuses = new[] { EnumEventStatus.Normal, EnumEventStatus.Detecting, EnumEventStatus.Fault };
        var createdIdsByStatus = new Dictionary<EnumEventStatus, List<int>>();

        foreach (var status in eventStatuses)
        {
            var ids = new List<int>();
            for (int i = 1; i <= 3; i++)
            {
                var pidsSymbol = new PidsSymbolModel
                {
                    Pid = 12000 + i,
                    Title = $"상태테스트_{status}_{i}",
                    OperationState = EnumOperationState.ACTIVATED,
                    Latitude = 37.5,
                    Longitude = 126.9,
                    Category = EnumMarkerCategory.PIDS_EQUIPMENT,
                    LinkedDeviceId = 22000 + i,
                    DeviceType = EnumDeviceType.Fence,
                    EventStatus = status,
                    FOVOpacity = 0.5
                };

                int id = await _fx.Svc.InsertPidsSymbolAsync(pidsSymbol);
                ids.Add(id);
            }
            createdIdsByStatus[status] = ids;
        }

        /* 각 EventStatus별로 조회 및 검증 */
        foreach (var status in eventStatuses)
        {
            var symbolsByStatus = await _fx.Svc.FetchPidsSymbolsByEventStatusAsync(status);

            Assert.NotNull(symbolsByStatus);
            Assert.True(symbolsByStatus!.Count >= 3);
            Assert.All(symbolsByStatus, s => Assert.Equal(status, s.EventStatus));

            // 생성한 ID들이 모두 포함되어 있는지 확인
            var expectedIds = createdIdsByStatus[status];
            foreach (var expectedId in expectedIds)
            {
                Assert.Contains(symbolsByStatus, s => s.Id == expectedId);
            }
        }
    }

    /// <summary>
    /// 모든 DeviceType 테스트
    /// </summary>
    [Fact(DisplayName = "PidsSymbols – All DeviceTypes")]
    public async Task All_DeviceTypes_Work()
    {
        var deviceTypes = Enum.GetValues<EnumDeviceType>();
        var createdSymbolIds = new Dictionary<EnumDeviceType, List<int>>();

        /* 각 DeviceType별로 PidsSymbol 생성 */
        foreach (var deviceType in deviceTypes)
        {
            var ids = await _fx.SeedPidsSymbolsByDeviceTypeAsync(deviceType, 2);
            createdSymbolIds[deviceType] = ids;
        }

        /* 각 DeviceType별로 조회 및 검증 */
        foreach (var deviceType in deviceTypes)
        {
            var symbols = await _fx.Svc.FetchPidsSymbolsByDeviceTypeAsync(deviceType);

            Assert.NotNull(symbols);
            Assert.True(symbols!.Count >= 2);
            Assert.All(symbols, s => Assert.Equal(deviceType, s.DeviceType));

            // 생성한 ID들이 모두 포함되어 있는지 확인
            var expectedIds = createdSymbolIds[deviceType];
            foreach (var expectedId in expectedIds)
            {
                Assert.Contains(symbols, s => s.Id == expectedId);
            }
        }
    }

    /// <summary>
    /// FOV 투명도(FOVOpacity) 경계값 테스트
    /// </summary>
    [Fact(DisplayName = "PidsSymbols – FOVOpacity Boundary Test")]
    public async Task FOVOpacity_Boundary_Test()
    {
        // 경계값 테스트: 0.0, 0.5, 1.0
        var opacityValues = new[] { 0.0, 0.5, 1.0 };
        var createdIds = new List<int>();

        for (int i = 0; i < opacityValues.Length; i++)
        {
            var pidsSymbol = new PidsSymbolModel
            {
                Pid = 13000 + i,
                Title = $"FOV투명도테스트_{opacityValues[i]:F1}",
                OperationState = EnumOperationState.ACTIVATED,
                Latitude = 37.5,
                Longitude = 126.9,
                Category = EnumMarkerCategory.PIDS_EQUIPMENT,
                LinkedDeviceId = 23000 + i,
                DeviceType = EnumDeviceType.IpCamera,
                ShowFOV = true,
                FOVColor = EnumColorType.Red,
                FOVOpacity = opacityValues[i],
                EventStatus = EnumEventStatus.Normal
            };

            int id = await _fx.Svc.InsertPidsSymbolAsync(pidsSymbol);
            createdIds.Add(id);
        }

        /* 생성된 PidsSymbol들의 FOV 투명도 검증 */
        for (int i = 0; i < createdIds.Count; i++)
        {
            var fetched = await _fx.Svc.FetchPidsSymbolAsync(createdIds[i]);
            Assert.NotNull(fetched);
            Assert.Equal(opacityValues[i], fetched!.FOVOpacity, 2); // 소수점 2자리까지 비교
        }
    }

    /// <summary>
    /// 잘못된 매개변수 예외 처리 테스트
    /// </summary>
    [Fact(DisplayName = "PidsSymbols – Invalid Parameters")]
    public async Task Invalid_Parameters_Throw_Exceptions()
    {
        /* 잘못된 ID로 조회 */
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _fx.Svc.FetchPidsSymbolAsync(-1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _fx.Svc.FetchPidsSymbolAsync(0));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _fx.Svc.FetchPidsSymbolByDeviceIdAsync(-1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _fx.Svc.FetchPidsSymbolByDeviceIdAsync(0));

        /* 잘못된 ID로 삭제 시도 */
        var invalidPidsSymbol = new PidsSymbolModel { Id = -1 };
        await Assert.ThrowsAsync<ArgumentException>(
            () => _fx.Svc.DeletePidsSymbolAsync(invalidPidsSymbol));

        var zeroIdPidsSymbol = new PidsSymbolModel { Id = 0 };
        await Assert.ThrowsAsync<ArgumentException>(
            () => _fx.Svc.DeletePidsSymbolAsync(zeroIdPidsSymbol));

        await Assert.ThrowsAsync<ArgumentException>(
            () => _fx.Svc.DeletePidsSymbolByDeviceIdAsync(-1));
        await Assert.ThrowsAsync<ArgumentException>(
            () => _fx.Svc.DeletePidsSymbolByDeviceIdAsync(0));
    }
}

/*======================================================================
 *  PidsSymbol Integration 테스트 - 복합 시나리오
 *====================================================================*/
[Collection(nameof(GMapDbPidsSymbolCollection))]
public class GMapDbPidsSymbol_IntegrationTests
{
    private readonly GMapDbPidsSymbolFixture _fx;

    /// <summary>
    /// 테스트 클래스 생성자
    /// </summary>
    /// <param name="fx">픽스처</param>
    public GMapDbPidsSymbol_IntegrationTests(GMapDbPidsSymbolFixture fx) => _fx = fx;

    /// <summary>
    /// 완전한 PidsSymbol 워크플로우 테스트
    /// </summary>
    [Fact(DisplayName = "Complete PidsSymbol Workflow")]
    public async Task Complete_PidsSymbol_Workflow()
    {
        // 1. PidsSymbol 생성
        var pidsSymbol = new PidsSymbolModel
        {
            Pid = 14000,
            Title = "통합테스트_PIDS장비",
            OperationState = EnumOperationState.DEACTIVATED,
            Latitude = 37.5665,
            Longitude = 126.9780,
            Altitude = 100,
            Bearing = 90.0,
            Width = 40,
            Height = 40,
            Category = EnumMarkerCategory.PIDS_EQUIPMENT,
            ShowShape = true,
            ShowTitle = false,
            FillColor = EnumColorType.Blue,
            StrokeColor = EnumColorType.White,
            StrokeThickness = 2.0,
            // PidsSymbol 전용 속성
            LinkedDeviceId = 24000,
            DeviceType = EnumDeviceType.Fence,
            ShowFOV = false,
            FOVColor = EnumColorType.Red,
            FOVOpacity = 0.3,
            EventStatus = EnumEventStatus.Normal
        };

        int symbolId = await _fx.Svc.InsertPidsSymbolAsync(pidsSymbol);
        Assert.True(symbolId > 0);

        // 2. 생성된 PidsSymbol 검증
        var fetchedSymbol = await _fx.Svc.FetchPidsSymbolAsync(symbolId);
        Assert.NotNull(fetchedSymbol);
        Assert.Equal("통합테스트_PIDS장비", fetchedSymbol!.Title);
        Assert.Equal(14000, fetchedSymbol.Pid);
        Assert.Equal(EnumOperationState.DEACTIVATED, fetchedSymbol.OperationState);
        Assert.Equal(EnumMarkerCategory.PIDS_EQUIPMENT, fetchedSymbol.Category);
        Assert.Equal(24000, fetchedSymbol.LinkedDeviceId);
        Assert.Equal(EnumDeviceType.Fence, fetchedSymbol.DeviceType);
        Assert.False(fetchedSymbol.ShowFOV);
        Assert.Equal(0.3, fetchedSymbol.FOVOpacity, 2);
        Assert.Equal(EnumEventStatus.Normal, fetchedSymbol.EventStatus);

        // 3. PidsSymbol 변경 (Fence → IpCamera, 상태 변경)
        fetchedSymbol.OperationState = EnumOperationState.ACTIVATED;
        fetchedSymbol.Title = "활성화된_카메라";
        fetchedSymbol.ShowTitle = true;
        fetchedSymbol.DeviceType = EnumDeviceType.IpCamera;
        fetchedSymbol.ShowFOV = true;
        fetchedSymbol.FOVColor = EnumColorType.Green;
        fetchedSymbol.FOVOpacity = 0.7;
        fetchedSymbol.EventStatus = EnumEventStatus.Detecting;

        var updatedSymbol = await _fx.Svc.UpdatePidsSymbolAsync(fetchedSymbol);
        Assert.Equal(EnumOperationState.ACTIVATED, updatedSymbol!.OperationState);
        Assert.Equal("활성화된_카메라", updatedSymbol.Title);
        Assert.True(updatedSymbol.ShowTitle);
        Assert.Equal(EnumDeviceType.IpCamera, updatedSymbol.DeviceType);
        Assert.True(updatedSymbol.ShowFOV);
        Assert.Equal(EnumColorType.Green, updatedSymbol.FOVColor);
        Assert.Equal(0.7, updatedSymbol.FOVOpacity, 2);
        Assert.Equal(EnumEventStatus.Detecting, updatedSymbol.EventStatus);

        // 4. DeviceType별 조회 확인
        var cameraSymbols = await _fx.Svc.FetchPidsSymbolsByDeviceTypeAsync(EnumDeviceType.IpCamera);
        Assert.NotNull(cameraSymbols);
        Assert.Contains(cameraSymbols, s => s.Id == updatedSymbol.Id);

        // 5. EventStatus별 조회 확인
        var detectingSymbols = await _fx.Svc.FetchPidsSymbolsByEventStatusAsync(EnumEventStatus.Detecting);
        Assert.NotNull(detectingSymbols);
        Assert.Contains(detectingSymbols, s => s.Id == updatedSymbol.Id);

        // 6. LinkedDeviceId로 조회 확인
        var symbolByDeviceId = await _fx.Svc.FetchPidsSymbolByDeviceIdAsync(updatedSymbol.LinkedDeviceId);
        Assert.NotNull(symbolByDeviceId);
        Assert.Equal(updatedSymbol.Id, symbolByDeviceId!.Id);

        // 7. 정리 (삭제)
        bool deleted = await _fx.Svc.DeletePidsSymbolAsync(updatedSymbol);
        Assert.True(deleted);

        // 8. 삭제 확인 (PidsSymbol과 기본 Symbol 모두)
        var deletedPidsSymbol = await _fx.Svc.FetchPidsSymbolAsync(symbolId);
        Assert.Null(deletedPidsSymbol);

        var deletedBaseSymbol = await _fx.Svc.FetchSymbolAsync(symbolId);
        Assert.Null(deletedBaseSymbol);
    }

    /// <summary>
    /// Provider 통합 테스트 (PidsSymbol 포함)
    /// </summary>
    [Fact(DisplayName = "Provider Integration Test with PidsSymbols")]
    public async Task Provider_Integration_Test_With_PidsSymbols()
    {
        // Provider가 비어있는 상태에서 시작
        _fx.SymbolProvider.Clear();

        // DB에 PidsSymbol 데이터 삽입
        await _fx.SeedPidsSymbolsByDeviceTypeAsync(EnumDeviceType.IpCamera, _fx.SymbolCount);

        // FetchInstance로 Provider에 로드 (PidsSymbol 포함)
        await _fx.Svc.FetchInstanceAsync();

        // Provider 검증
        Assert.True(_fx.SymbolProvider.Count >= _fx.SymbolCount);

        // Provider 내 PidsSymbol 데이터 검증
        var pidsSymbols = _fx.SymbolProvider.CollectionEntity
            .OfType<PidsSymbolModel>()
            .ToList();

        Assert.True(pidsSymbols.Count >= _fx.SymbolCount);
        Assert.All(pidsSymbols, s =>
        {
            Assert.True(s.Id > 0);
            Assert.True(s.Pid > 0);
            Assert.NotEmpty(s.Title);
            Assert.Equal(EnumMarkerCategory.PIDS_EQUIPMENT, s.Category);
            Assert.True(s.LinkedDeviceId > 0);
            Assert.True(Enum.IsDefined(typeof(EnumDeviceType), s.DeviceType));
            Assert.True(Enum.IsDefined(typeof(EnumEventStatus), s.EventStatus));
            Assert.True(s.FOVOpacity >= 0.0 && s.FOVOpacity <= 1.0);
        });
    }

    /// <summary>
    /// 트랜잭션 롤백 테스트
    /// </summary>
    [Fact(DisplayName = "Transaction Rollback Test")]
    public async Task Transaction_Rollback_Test()
    {
        // 삽입 전 PidsSymbol 개수 확인
        var beforeCount = (await _fx.Svc.FetchPidsSymbolsAsync())?.Count ?? 0;

        // 유효하지 않은 데이터로 삽입 시도 (트랜잭션 실패 유도)
        var invalidPidsSymbol = new PidsSymbolModel
        {
            Pid = -1, // 유효하지 않은 Pid
            Title = "", // 빈 제목
            Latitude = 200, // 유효하지 않은 위도
            Longitude = 400, // 유효하지 않은 경도
            Category = EnumMarkerCategory.PIDS_EQUIPMENT,
            LinkedDeviceId = -1, // 유효하지 않은 DeviceId
            DeviceType = EnumDeviceType.Fence,
            FOVOpacity = 0.5,
            EventStatus = EnumEventStatus.Normal
        };

        // 유효하지 않은 데이터 삽입 시도 (예외 발생 예상)
        await Assert.ThrowsAnyAsync<Exception>(
            () => _fx.Svc.InsertPidsSymbolAsync(invalidPidsSymbol));

        // 삽입 후 PidsSymbol 개수가 변하지 않았는지 확인 (롤백 확인)
        var afterCount = (await _fx.Svc.FetchPidsSymbolsAsync())?.Count ?? 0;
        Assert.Equal(beforeCount, afterCount);
    }

    /// <summary>
    /// 실시간 데이터 필드 비저장 확인 테스트
    /// </summary>
    [Fact(DisplayName = "Real-time Data Fields Not Stored")]
    public async Task Realtime_Data_Fields_Not_Stored()
    {
        // 실시간 데이터 필드가 포함된 PidsSymbol 생성
        var pidsSymbol = new PidsSymbolModel
        {
            Pid = 15000,
            Title = "실시간데이터테스트",
            Category = EnumMarkerCategory.PIDS_EQUIPMENT,
            LinkedDeviceId = 25000,
            DeviceType = EnumDeviceType.IpCamera,
            FOVOpacity = 0.5,
            EventStatus = EnumEventStatus.Normal,
            // 실시간 데이터 (저장되지 않아야 함)
            DetectionRange = 150.0,
            DetectionAngle = 90.0,
            DetectionBearing = 45.0
        };

        int symbolId = await _fx.Svc.InsertPidsSymbolAsync(pidsSymbol);

        // DB에서 다시 조회
        var fetchedSymbol = await _fx.Svc.FetchPidsSymbolAsync(symbolId);
        Assert.NotNull(fetchedSymbol);

        // 실시간 데이터는 기본값으로 설정되어야 함 (DB에서 로드되지 않음)
        Assert.Equal(100.0, fetchedSymbol!.DetectionRange); // 기본값
        Assert.Equal(80.0, fetchedSymbol.DetectionAngle);   // 기본값
        Assert.Equal(0.0, fetchedSymbol.DetectionBearing);  // 기본값

        // 다른 속성들은 정상적으로 저장/로드되어야 함
        Assert.Equal("실시간데이터테스트", fetchedSymbol.Title);
        Assert.Equal(25000, fetchedSymbol.LinkedDeviceId);
        Assert.Equal(EnumDeviceType.IpCamera, fetchedSymbol.DeviceType);
    }
}

#region - EnumParseHelper Unit Tests -

public sealed class GMapDbEnumParseHelperTests
{
    [Fact]
    public void TryParseEnum_ValidValue_ParsesCorrectly()
    {
        // Arrange + Act
        var result = EnumParseHelper.TryParseEnum("IpCamera", EnumDeviceType.NONE);

        // Assert
        Assert.Equal(EnumDeviceType.IpCamera, result);
    }

    [Fact]
    public void TryParseEnum_UnknownValue_ReturnsFallback()
    {
        // Arrange + Act
        var result = EnumParseHelper.TryParseEnum("UnknownType", EnumDeviceType.NONE);

        // Assert — 예외 없이 fallback 반환
        Assert.Equal(EnumDeviceType.NONE, result);
    }

    [Fact]
    public void TryParseEnum_EmptyString_ReturnsFallback()
    {
        // Arrange + Act
        var result = EnumParseHelper.TryParseEnum("", EnumDeviceType.NONE);

        // Assert — 빈 문자열도 fallback 반환
        Assert.Equal(EnumDeviceType.NONE, result);
    }
}

#endregion