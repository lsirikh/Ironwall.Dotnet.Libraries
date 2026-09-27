using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/****************************************************************************
   Purpose      : TEST-03 — 부대 콘솔 장비 항목이 목록의 status 를 버리지 않는다(FR-03, L2 "▲오류 N" 의 원천)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : 시나리오 해당 없음(데이터 보존 — 표시는 레인 A · C). NFR-10: 기존 생성 지점은 기본값으로 컴파일된다.
****************************************************************************/
public class UnitDeviceItemStatusTests
{
    [Theory]
    [InlineData("ACTIVATED", UnitDeviceStatus.Normal)]
    [InlineData("ERROR", UnitDeviceStatus.Error)]
    [InlineData("DEACTIVATED", UnitDeviceStatus.Deactivated)]
    [InlineData("error", UnitDeviceStatus.Error)]
    [InlineData(" Deactivated ", UnitDeviceStatus.Deactivated)]
    [InlineData(null, UnitDeviceStatus.Normal)]
    [InlineData("", UnitDeviceStatus.Normal)]
    [InlineData("MAINTENANCE", UnitDeviceStatus.Normal)]
    public void should_map_server_status_word_when_parsing(string? raw, UnitDeviceStatus expected)
    {
        Assert.Equal(expected, UnitDeviceText.StatusOf(raw));
    }

    [Fact]
    public void should_default_to_normal_when_existing_call_sites_omit_status()
    {
        // 기존 생성 지점(어댑터 · 시험 픽스처)은 다섯 인자로 만든다 — 그대로 컴파일되고 정상으로 본다.
        var item = new UnitDeviceItem(1001, 1, "장비0001", EnumDeviceCategory.Camera, 7);

        Assert.Equal(UnitDeviceStatus.Normal, item.Status);
    }

    [Fact]
    public async Task should_carry_list_status_into_items_when_adapter_loads_all()
    {
        // Arrange — 제어기 3대(정상 · 오류 · 비활성) + 카메라 1대(상태 모름)
        var api = new MockDeviceApiService();
        api.ControllerResponses.Add(ApiListResponse<ControllerDeviceDto>.CreateSuccess(new List<ControllerDeviceDto>
        {
            new() { Id = 11, NumberDevice = 1, NameDevice = "제어기1", Status = "ACTIVATED", UnitId = 7 },
            new() { Id = 12, NumberDevice = 2, NameDevice = "제어기2", Status = "ERROR", UnitId = 7 },
            new() { Id = 13, NumberDevice = 3, NameDevice = "제어기3", Status = "DEACTIVATED", UnitId = 7 },
        }));
        api.CameraResponses.Add(ApiListResponse<CameraDeviceDto>.CreateSuccess(new List<CameraDeviceDto>
        {
            new() { Id = 21, NumberDevice = 1, NameDevice = "카메라1", Status = "SOMETHING_NEW", UnitId = 7 },
        }));
        var adapter = new UnitDeviceApiAdapter(api, new V8Probe());

        // Act
        var result = await adapter.LoadAllAsync();

        // Assert
        var byId = result.Items.ToDictionary(i => i.Id, i => i.Status);
        Assert.Equal(UnitDeviceStatus.Normal, byId[11]);
        Assert.Equal(UnitDeviceStatus.Error, byId[12]);
        Assert.Equal(UnitDeviceStatus.Deactivated, byId[13]);
        Assert.Equal(UnitDeviceStatus.Normal, byId[21]);
    }

    [Fact]
    public void should_keep_status_out_of_value_identity_changes_when_record_is_copied()
    {
        var item = new UnitDeviceItem(1, 1, "a", EnumDeviceCategory.Lamp, 3, UnitDeviceStatus.Error);

        var moved = item with { UnitId = 4 };

        Assert.Equal(UnitDeviceStatus.Error, moved.Status);   // 소속만 바꾼 사본도 상태를 잃지 않는다
    }

    private sealed class V8Probe : IServerContractProbe
    {
        public EnumServerContract Contract => EnumServerContract.V8_0;
        public string? RawVersion => "8.0.3";
        public bool IsResolved => true;
        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    }
}
