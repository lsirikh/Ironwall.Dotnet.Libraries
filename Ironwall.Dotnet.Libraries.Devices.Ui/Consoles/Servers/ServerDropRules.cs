using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
/****************************************************************************
   Purpose      : 장비 → 서버 배정 판정 (N-12, 드래그 우선 · 허용 유형만)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>배정 한 번의 계획 — 무엇을 몇 번 보내고 무엇을 왜 뺐는가.</summary>
/// <param name="ServerId">대상 서버.</param>
/// <param name="DeviceIds">실제로 보낼 장비 Id(저장된 것 · 허용 유형 · 아직 그 서버가 아닌 것).</param>
/// <param name="DraftExcluded">아직 서버에 없어(Id≤0) 뺀 수.</param>
/// <param name="AlreadyOn">이미 그 서버에 붙어 있어 뺀 수.</param>
/// <param name="Ineligible">그 서버 유형이 받지 않는 장비라 뺀 수.</param>
/// <param name="BlockReason">한 건도 보낼 수 없는 까닭. 보낼 수 있으면 <c>null</c>.</param>
public sealed record ServerAssignPlan(
    int ServerId,
    IReadOnlyList<int> DeviceIds,
    int DraftExcluded,
    int AlreadyOn,
    int Ineligible,
    string? BlockReason)
{
    public bool CanSend => BlockReason is null && DeviceIds.Count > 0;

    /// <summary>서버 호출 횟수 — 장비 한 대당 <b>쓰기 1회</b>다(배치 입구가 없다).</summary>
    public int WriteCount => DeviceIds.Count;

    /// <summary>두 건 이상이면 확인을 받는다 — 폭발반경을 문장으로 먼저 말한다.</summary>
    public bool NeedsConfirm => WriteCount > 1;

    /// <summary>확인 문구 — 호출 횟수를 반드시 포함한다.</summary>
    public string ConfirmText(string serverName)
        => $"'{serverName}' 에 {WriteCount}대를 배정합니다 — 서버 쓰기 {WriteCount}회가 나갑니다. 계속할까요?";
}

/// <summary>
/// 장비 → 서버 드롭의 판정(순수 함수) — 화면 없이 단위 테스트한다.
/// </summary>
/// <remarks>
/// <para><b>허용 유형만</b>(드래그 와이어프레임 L371-372 "카테고리별 허용 서버 유형이 아니면 드롭 불가",
/// L572 "센서는 서버에 배정할 수 없어 드롭 불가로 막힙니다").</para>
/// <para><b>왜 스피커뿐인가</b> — 장비 쓰기 계약에서 <c>server_id</c> 를 가진 DTO 는
/// <c>SpeakerDeviceDto</c> <b>하나뿐</b>이다(전수 확인). 다른 카테고리에 서버를 붙이는 입구는 서버에 없다 —
/// 끌 수 있게 만들면 저장되지 않는 거짓 UI 가 된다. 서버가 축을 넓히면 <see cref="Accepts"/> 한 곳만 고친다.</para>
/// <para>이 판정은 끄는 동안 <b>매 프레임</b> 불린다 — 서버 호출도, <c>Keyboard.Modifiers</c> 읽기도 없다.</para>
/// </remarks>
public static class ServerDropRules
{
    /// <summary>드롭존 종류 — 목록의 서버 <b>행</b>이 드롭존이다.</summary>
    public const string ZoneKey = "server-row";

    /// <summary>그 서버 유형이 이 카테고리의 장비를 받는가.</summary>
    public static bool Accepts(EnumServerType? serverType, EnumDeviceCategory deviceCategory)
        => serverType == EnumServerType.SPEAKER_API && deviceCategory == EnumDeviceCategory.Speaker;

    /// <summary>못 받는 까닭 한 줄. 받을 수 있으면 <c>null</c>.</summary>
    public static string? RefusalReason(EnumServerType? serverType, EnumDeviceCategory deviceCategory)
    {
        if (Accepts(serverType, deviceCategory)) return null;
        if (serverType != EnumServerType.SPEAKER_API)
            return $"'{ServerTypeCatalog.TypeLabel(serverType)}' 유형 서버에는 장비를 배정할 수 없습니다 — 스피커 서버만 받습니다";
        return $"{CategoryLabel(deviceCategory)} 은(는) 서버에 배정할 수 없습니다 — 스피커만 받습니다";
    }

    /// <summary>끌어 온 장비들을 그 서버에 배정하는 계획. 서버를 부르지 않는다.</summary>
    public static ServerAssignPlan Plan(int serverId, EnumServerType? serverType, IEnumerable<IBaseDeviceModel>? devices)
    {
        var list = devices?.Where(d => d is not null).ToList() ?? new List<IBaseDeviceModel>();

        if (serverId <= 0)
            return Blocked(serverId, "아직 서버에 등록되지 않은 행입니다 — 먼저 등록하십시오");
        if (list.Count == 0)
            return Blocked(serverId, "끌어 온 장비가 없습니다");

        var ineligible = list.Count(d => !Accepts(serverType, DeviceAxesMapper.CategoryOf(d)));
        var eligible = list.Where(d => Accepts(serverType, DeviceAxesMapper.CategoryOf(d))).ToList();

        if (eligible.Count == 0)
        {
            var reason = RefusalReason(serverType, list.Select(DeviceAxesMapper.CategoryOf).First());
            return Blocked(serverId, reason ?? "이 서버가 받지 않는 장비입니다", ineligible: ineligible);
        }

        var drafts = eligible.Count(d => d.Id <= 0);
        var saved = eligible.Where(d => d.Id > 0).ToList();
        var already = saved.Count(d => ServerIdOf(d) == serverId);
        var ids = saved.Where(d => ServerIdOf(d) != serverId).Select(d => d.Id).Distinct().ToList();

        string? block = null;
        if (ids.Count == 0)
            block = saved.Count == 0 ? "아직 등록되지 않은 장비입니다 — 장비를 먼저 등록하십시오" : "이미 이 서버에 배정돼 있습니다";

        return new ServerAssignPlan(serverId, ids, drafts, already, ineligible, block);
    }

    /// <summary>상태 띠에 남길 한 줄.</summary>
    public static string ResultLine(string serverName, ServerAssignPlan plan, int assigned, int failed)
    {
        var parts = new List<string> { $"'{serverName}' 에 {assigned}대를 배정했습니다(서버 쓰기 {assigned}회)" };
        if (failed > 0) parts.Add($"{failed}대에서 멈췄습니다");
        if (plan.AlreadyOn > 0) parts.Add($"이미 붙어 있던 {plan.AlreadyOn}대는 보내지 않았습니다");
        if (plan.Ineligible > 0) parts.Add($"받지 않는 {plan.Ineligible}대는 뺐습니다");
        if (plan.DraftExcluded > 0) parts.Add($"등록 전 {plan.DraftExcluded}대는 뺐습니다");
        return string.Join(" · ", parts);
    }

    /// <summary>그 장비가 지금 붙어 있는 서버 Id. 스피커가 아니거나 없으면 <c>null</c>.</summary>
    public static int? ServerIdOf(IBaseDeviceModel? device)
        => device is ISpeakerDeviceModel speaker && speaker.Server is { Id: > 0 } server ? server.Id : null;

    internal static string CategoryLabel(EnumDeviceCategory category) => category switch
    {
        EnumDeviceCategory.Controller => "제어기",
        EnumDeviceCategory.Sensor => "센서",
        EnumDeviceCategory.Camera => "카메라",
        EnumDeviceCategory.Speaker => "스피커",
        EnumDeviceCategory.Enclosure => "함체",
        EnumDeviceCategory.Lamp => "경광등",
        EnumDeviceCategory.Gate => "통문",
        _ => "이 장비",
    };

    private static ServerAssignPlan Blocked(int serverId, string reason, int ineligible = 0)
        => new(serverId, Array.Empty<int>(), 0, 0, ineligible, reason);
}
