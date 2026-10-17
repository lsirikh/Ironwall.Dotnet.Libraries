using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Servers;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;

/// <summary>
/// 장비 등록 · 상세 폼의 "관리 서버" 선택지와 기본값(순수 함수 — 화면 없이 단위 테스트한다).
/// </summary>
/// <remarks>
/// <para><b>왜</b>(GIS 실창 WP-2 SC-DEV-015, 2026-09-27): 스피커 등록 폼이 서버 목록의 첫 서버(최소 Id)를 기본값으로 골랐는데
/// 그것이 VMS 서버였다 — 서버는 <c>422 'server_id' 2 의 유형은 VMS 입니다 — speaker 의 관리 서버는 ['SPEAKER_API'] 여야 합니다(D4)</c>
/// 로 거절했다. 선택지도 모든 서버를 늘어놓아 운영자가 고를 수 없는 서버를 고르게 했다.</para>
/// <para><b>허용 표는 하나</b> — 서버 드롭과 같은 <see cref="ServerDropRules.AllowedServerTypes"/> 를 쓴다(표를 두 벌 두지 않는다).</para>
/// <para><b>6.3</b>: 응답에 유형(<c>category_server</c>)이 없어 <see cref="ServerModel.CategoryServer"/> 가 비면 거를 근거가 없다 —
/// 그때는 예전 동작(전부 보이고 첫 서버를 기본값)을 그대로 둔다. 유형을 아는 서버가 하나라도 있으면 유형으로 거른다.</para>
/// </remarks>
public static class DeviceServerChoice
{
    /// <summary>서버 유형 — 모르면 <c>null</c>(6.3 응답 · 아직 판별자를 받지 못한 서버).</summary>
    public static EnumServerType? TypeOf(IServerModel? server)
        => server is ServerModel { CategoryServer: { Length: > 0 } code } ? ServerTypeCatalog.ParseType(code) : null;

    /// <summary>유형으로 거를 수 있는가 — 유형을 아는 서버가 하나라도 있다.</summary>
    public static bool CanJudge(IEnumerable<IServerModel?>? servers)
        => servers?.Any(s => TypeOf(s) is not null) == true;

    /// <summary>이 카테고리의 관리 서버로 고를 수 있는 서버(Id 순). 거를 근거가 없으면 전부.</summary>
    public static IReadOnlyList<IServerModel> Allowed(IEnumerable<IServerModel?>? servers, EnumDeviceCategory category)
    {
        var list = servers?.Where(s => s is not null).Select(s => s!).OrderBy(s => s.Id).ToList() ?? new List<IServerModel>();
        if (!CanJudge(list)) return list;
        return list.Where(s => ServerDropRules.Accepts(TypeOf(s), category)).ToList();
    }

    /// <summary>
    /// 새 장비의 관리 서버 기본값 — 고를 수 있는 서버가 <b>꼭 하나</b>면 그것, 아니면 <c>null</c>(운영자가 고른다 · 서버 없이 등록도 된다).
    /// 거를 근거가 없는 6.3 은 예전대로 첫 서버.
    /// </summary>
    public static IServerModel? DefaultFor(IEnumerable<IServerModel?>? servers, EnumDeviceCategory category)
    {
        var list = servers?.Where(s => s is not null).Select(s => s!).ToList() ?? new List<IServerModel>();
        if (!CanJudge(list)) return list.OrderBy(s => s.Id).FirstOrDefault();
        var allowed = Allowed(list, category);
        return allowed.Count == 1 ? allowed[0] : null;
    }
}
