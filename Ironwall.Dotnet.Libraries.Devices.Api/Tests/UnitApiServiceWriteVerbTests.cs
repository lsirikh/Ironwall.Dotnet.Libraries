using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Tests;
/****************************************************************************
   Purpose      : 부대 쓰기 동사 — 부분 수정은 PATCH, PUT 은 parent_id 를 빠뜨려 루트로 옮기지 않는다
   Created By   : GHLee
   Created On   : 9/29/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : 서버 회신 2026-09-29 ⑦ · REST §11-A.6 — PUT /api/units/{id} 에서 parent_id 를 빼면 루트로 옮긴 것이라
                  편제가 바뀌고 그 부대의 관계도 배치 행도 정리된다. 일부 필드만 고칠 때는 PATCH 를 쓴다.
****************************************************************************/
public class UnitApiServiceWriteVerbTests
{
    private static (UnitApiService Service, CapturingApiService Http) Create()
    {
        var http = new CapturingApiService();
        var service = new UnitApiService(null, http, new ApiSetupModel { Url = "https://gop.test/api" },
                                         new FixedContractProbe(EnumServerContract.V8_0));
        return (service, http);
    }

    private static UnitDto Current(int id, int? parentId) => new()
    {
        Id = id,
        Code = "c0107",
        Name = "7중대",
        EchelonRaw = "Company",
        ParentId = parentId,
        Description = "설명",
        IsEnable = true,
        AdjacentUnitIds = new List<int> { 8 },
    };

    [Fact]
    public async Task should_send_patch_with_only_changed_field_when_editing_unit_name()
    {
        var (service, http) = Create();

        await service.PatchUnitAsync(7, new UnitUpdateDto { Name = "7중대(개편)" });

        Assert.Equal("PATCH", http.Method);
        Assert.EndsWith("/units/7", http.Endpoint);
        Assert.False(http.Body!.ContainsKey("parent_id"));      // 보내지 않은 상위는 그대로다(RFC 7396)
    }

    [Fact]
    public async Task should_refuse_put_without_network_when_parent_id_is_omitted_and_root_is_not_intended()
    {
        // 부분 수정 의도의 PUT — 상위를 빠뜨리면 서버가 루트로 옮기고 배치 행까지 지운다(⑦). 네트워크 전에 막는다.
        var (service, http) = Create();
        var dto = new UnitReplaceDto { Name = "7중대", Echelon = EnumUnitEchelon.Company, AdjacentUnitIds = new List<int>() };

        var result = await service.ReplaceUnitAsync(7, dto);

        Assert.False(result.Success);
        Assert.Equal("VALIDATION_ERROR", result.Error?.Code);
        Assert.Null(http.Method);
    }

    [Fact]
    public async Task should_send_put_with_parent_id_when_replacing_from_current_child_unit()
    {
        var (service, http) = Create();

        await service.ReplaceUnitAsync(7, UnitReplaceDto.FromCurrent(Current(7, parentId: 3))!);

        Assert.Equal("PUT", http.Method);
        Assert.Equal(3, (int)http.Body!["parent_id"]!);
    }

    [Fact]
    public async Task should_send_put_without_parent_id_when_current_unit_is_root()
    {
        // 지금 최상위인 부대의 전체 교체 — 루트 그대로(이동 아님). FromCurrent 가 의도를 밝힌다.
        var (service, http) = Create();

        var dto = UnitReplaceDto.FromCurrent(Current(1, parentId: null))!;
        await service.ReplaceUnitAsync(1, dto);

        Assert.True(dto.IsRootIntended);
        Assert.Equal("PUT", http.Method);
        Assert.False(http.Body!.ContainsKey("parent_id"));
        Assert.False(http.Body!.ContainsKey("is_root_intended"));   // 전송하지 않는 표지
    }
}
