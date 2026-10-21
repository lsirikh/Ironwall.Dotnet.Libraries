using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 스피커 연동 칸 음원그룹 후보 — 장비 API(GET /api/file-groups)를 워크벤치 포트로 잇는다
   Created By   : Claude
   Created On   : 2026-10-21
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <see cref="IDeviceApiService.GetFileGroupsAsync"/> 위의 <see cref="IMappingFileGroupSource"/> (GOP API 8.0.15 대조 G25).
/// </summary>
public sealed class DeviceApiMappingFileGroupSource : IMappingFileGroupSource
{
    private const int PAGE_LIMIT = 100;     // 서버 상한
    private const int MAX_PAGES = 50;       // 5천 그룹 — 무한 루프 금지

    /// <summary>권한이 없거나 경로가 없는 서버일 때 화면에 보일 말.</summary>
    public const string Unavailable = "음원 목록을 볼 수 없습니다. 음원 번호를 직접 넣으세요.";

    /// <summary>그 밖의 실패.</summary>
    public const string LoadFailed = "음원 목록을 불러오지 못했습니다. 음원 번호를 직접 넣으세요.";

    private readonly IDeviceApiService _api;
    private readonly ILogService? _log;

    /// <summary>생성자.</summary>
    public DeviceApiMappingFileGroupSource(IDeviceApiService api, ILogService? log = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _log = log;
    }

    /// <inheritdoc/>
    public async Task<MappingCallResult<IReadOnlyList<MappingFileGroupInfo>>> ListFileGroupsAsync(CancellationToken token = default)
    {
        var all = new List<MappingFileGroupInfo>();
        try
        {
            for (var page = 1; page <= MAX_PAGES; page++)
            {
                var resp = await _api.GetFileGroupsAsync(page: page, limit: PAGE_LIMIT, token: token).ConfigureAwait(false);
                if (token.IsCancellationRequested)
                    return MappingCallResult<IReadOnlyList<MappingFileGroupInfo>>.Fail("취소되었습니다.", "cancelled", 0);
                if (resp is null || !resp.Success)
                {
                    var status = resp?.StatusCode ?? 0;
                    var raw = $"{resp?.Error?.Code} {resp?.Error?.Message}".Trim();
                    _log?.Warning($"[MappingWorkbench] 음원 목록 실패 {status} — {raw}");
                    return MappingCallResult<IReadOnlyList<MappingFileGroupInfo>>.Fail(
                        status is 403 or 404 ? Unavailable : LoadFailed, raw, status);
                }

                var rows = resp.Data ?? new();
                all.AddRange(rows.Where(g => g is not null && g.Id > 0)
                    .Select(g => new MappingFileGroupInfo(g.Id, string.IsNullOrWhiteSpace(g.Name) ? $"#{g.Id}" : g.Name.Trim())));

                // 끝 판정: 쪽 정보가 있으면 그것으로, 없으면 "받은 수 < 한도".
                var more = resp.HasMorePages ?? rows.Count >= PAGE_LIMIT;
                if (!more || rows.Count == 0) break;
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"[MappingWorkbench] 음원 목록 예외 — {ex.Message}");
            return MappingCallResult<IReadOnlyList<MappingFileGroupInfo>>.Fail(LoadFailed, ex.Message, 0);
        }

        var list = all.GroupBy(g => g.Id).Select(g => g.First())
            .OrderBy(g => g.Name, StringComparer.CurrentCulture).ThenBy(g => g.Id)
            .ToList();
        return MappingCallResult<IReadOnlyList<MappingFileGroupInfo>>.Ok(list);
    }
}
