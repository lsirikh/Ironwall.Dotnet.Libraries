using Autofac;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Tests.LiveApi;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Tests;
/****************************************************************************
   Purpose      : 서버 도메인 라이브 시험 공용 도구 — 자기 카테고리 · 서버를 만들고 지운다
   Created By   : Claude
   Created On   : 10/12/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.

   Description  : ① 7.0+ 에서 서버 등록 · 수정은 평면 ServerDto 로 보낼 수 없다(AXIS_SHAPE_REQUIRED, D-22) —
                     서버 콘솔이 실제로 쓰는 IServerAxisApiService 를 같은 로그인 세션에서 꺼내 쓴다.
                  ② 서버는 그 유형(category_server)의 카테고리 행이 있어야 등록된다
                     (서버 app/routers/servers.py:198-208 _require_category_row).
                  ③ 카테고리 유형은 UNIQUE 다(app/routers/server_categories.py:186-203, 409) — 시험마다
                     시드에 없는 유형을 따로 쓰고, 앞선 중단 실행이 남긴 "자기 접두어" 행만 먼저 지운다.
                     시드 행(VMS · AI_ANALYSIS · STREAMING · TRANSCODER · BROKER · DB_API · NVR_API ·
                     SPEAKER_API · ENCLOSURE_API · PROXY — app/utils/init_server_data.py)은 건드리지 않는다.
****************************************************************************/
internal static class ServerTestKit
{
    /// <summary>ServerApiIntegrationTest(DEP-202)가 만드는 자료의 이름 접두어.</summary>
    public const string MigrationPrefix = "MIG_";

    /// <summary>ServerApiServiceTests(DEP-203)가 만드는 자료의 이름 접두어.</summary>
    public const string ServicePrefix = "LIVE-SRV-";

    /// <summary>
    /// 로그인 세션의 <see cref="IApiService"/> · <see cref="ApiSetupModel"/> · 계약 프로브로 축 통로를 만든다
    /// (DeviceApiModule 이 <see cref="LiveApiSession.Name"/> 으로 셋 다 등록한다 — ApiModule.cs:36-48).
    /// </summary>
    public static async Task<IServerAxisApiService> AxisAsync()
    {
        var container = await LiveApiSession.GetAsync();
        return new ServerAxisApiService(
            container.ResolveNamed<IApiService>(LiveApiSession.Name),
            container.ResolveNamed<ApiSetupModel>(LiveApiSession.Name),
            container.ResolveNamed<IServerContractProbe>(LiveApiSession.Name),
            LiveApiSession.Log);
    }

    /// <summary>축 계약(7.0+)인가 — 평면 쓰기 차단 · 상태 보고 · proxy-settings 묘비가 이 판정을 따른다.</summary>
    public static bool IsAxisEra(IServerAxisApiService axis) => axis.Contract >= EnumServerContract.V7_0;

    /// <summary>서버 <c>name</c> 은 UNIQUE 다(servers.py:211-218, 409) — 실행마다 다른 이름을 쓴다.</summary>
    public static string UniqueName(string stem) => $"{stem}-{Guid.NewGuid().ToString("N").Substring(0, 8)}";

    /// <summary>
    /// 유형 <paramref name="typeServer"/> 의 카테고리가 <paramref name="ownedPrefix"/> 로 시작하는 이름이면
    /// (=이 시험이 앞서 만들고 못 지운 행) 지운다. 소속 서버도 이름이 같은 접두어일 때만 먼저 지운다.
    /// 남의 행이면 손대지 않는다 — 뒤이은 등록이 409 로 원인을 드러낸다.
    /// </summary>
    public static async Task PurgeLeftoverCategoryAsync(
        IServerApiService svc, string typeServer, string ownedPrefix, ITestOutputHelper? output = null)
    {
        var list = await svc.GetCategoriesAsync(page: 1, limit: 100);
        Assert.True(list.Success, $"카테고리 목록 조회 실패: {list.Message}");

        var leftover = list.Data?.FirstOrDefault(c => c.TypeServer == typeServer);
        if (leftover is null) return;

        if (!leftover.Name.StartsWith(ownedPrefix, StringComparison.Ordinal))
        {
            output?.WriteLine($"[정리] 유형 {typeServer} 카테고리 {leftover.Id}:'{leftover.Name}' 는 이 시험의 행이 아니다 — 손대지 않음");
            return;
        }

        // 카테고리 상세의 servers[] 는 참조 {id, category_server} 뿐이라(server.py:848-854) 이름은 단건으로 읽는다.
        var detail = await svc.GetCategoryByIdAsync(leftover.Id);
        foreach (var reference in detail.Data?.Servers ?? new())
        {
            var server = await svc.GetServerByIdAsync(reference.Id);
            if (server.Success && server.Data?.Name?.StartsWith(ownedPrefix, StringComparison.Ordinal) == true)
            {
                var removed = await svc.DeleteServerAsync(reference.Id);
                output?.WriteLine($"[정리] 잔재 서버 {reference.Id}:'{server.Data.Name}' 삭제: {removed.Success}");
            }
        }

        var deleted = await svc.DeleteCategoryAsync(leftover.Id);
        output?.WriteLine($"[정리] 잔재 카테고리 {leftover.Id}:'{leftover.Name}'({typeServer}) 삭제: {deleted.Success} {deleted.Message}");
    }

    /// <summary>잔재를 정리한 뒤 카테고리를 등록하고 id 를 돌려준다(실패는 단언으로 멈춘다).</summary>
    public static async Task<int> CreateCategoryAsync(
        IServerApiService svc, string typeServer, string name, string ownedPrefix,
        ITestOutputHelper? output = null, string? description = null, int sortOrder = 0)
    {
        Assert.StartsWith(ownedPrefix, name);
        await PurgeLeftoverCategoryAsync(svc, typeServer, ownedPrefix, output);

        var response = await svc.CreateCategoryAsync(new CategoryDto
        {
            TypeServer = typeServer,
            Name = name,
            Description = description,
            SortOrder = sortOrder
        });
        output?.WriteLine($"[선행] 카테고리 {typeServer} 등록: Success={response.Success}, Id={response.Data?.Id}, Msg={response.Message}");
        Assert.True(response.Success, $"선행 카테고리({typeServer}) 등록 실패: {response.Message} {response.Error?.Message}");
        Assert.True(response.Data!.Id > 0);
        return response.Data.Id;
    }

    /// <summary>축 통로로 서버를 등록하고 id 를 돌려준다(실패는 단언으로 멈춘다).</summary>
    public static async Task<int> CreateServerAsync(
        IServerAxisApiService axis, string typeServer, int categoryId, string name,
        string ipAddress, int port, string? hostname = null)
    {
        var created = await axis.CreateServerAsync(new ServerWriteIntent
        {
            TypeServer = typeServer,      // 7.0+ 판별자
            CategoryId = categoryId,      // 6.3 분류 id(축 계약에서는 싣지 않는다)
            Name = name,
            IpAddress = ipAddress,
            Port = port,
            Hostname = hostname
        });
        Assert.True(created.IsSuccess, $"선행 서버 등록 실패: {created.Message} (HTTP {created.StatusCode})");
        Assert.True(created.Id > 0);
        return created.Id;
    }

    /// <summary>
    /// 정리 — 서버를 먼저 지우고(계측은 CASCADE, servers.py:670) 카테고리를 지운다
    /// (소속 서버가 남으면 카테고리 삭제는 409, server_categories.py:316-327).
    /// </summary>
    public static async Task DeleteServerThenCategoryAsync(
        IServerApiService svc, int serverId, int categoryId, ITestOutputHelper? output = null)
    {
        if (serverId > 0)
        {
            var server = await svc.DeleteServerAsync(serverId);
            output?.WriteLine($"[정리] 서버 {serverId} 삭제: {server.Success} {server.Message}");
        }
        if (categoryId > 0)
        {
            var category = await svc.DeleteCategoryAsync(categoryId);
            output?.WriteLine($"[정리] 카테고리 {categoryId} 삭제: {category.Success} {category.Message}");
        }
    }
}
