using Ironwall.Dotnet.Libraries.Api.Services;
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
/// <param name="Devices">실제로 보낼 장비(저장된 것 · 허용 유형 · 아직 그 서버가 아닌 것).</param>
/// <param name="DraftExcluded">아직 서버에 없어(Id≤0) 뺀 수.</param>
/// <param name="AlreadyOn">이미 그 서버에 붙어 있어 뺀 수.</param>
/// <param name="Ineligible">그 서버 유형이 받지 않는 장비라 뺀 수.</param>
/// <param name="BlockReason">한 건도 보낼 수 없는 까닭. 보낼 수 있으면 <c>null</c>.</param>
public sealed record ServerAssignPlan(
    int ServerId,
    IReadOnlyList<IBaseDeviceModel> Devices,
    int DraftExcluded,
    int AlreadyOn,
    int Ineligible,
    string? BlockReason)
{
    public bool CanSend => BlockReason is null && Devices.Count > 0;

    public IReadOnlyList<int> DeviceIds => Devices.Select(d => d.Id).ToList();

    /// <summary>서버 호출 횟수 — 장비 한 대당 <b>쓰기 1회</b>다(배치 입구가 없다).</summary>
    public int WriteCount => Devices.Count;

    /// <summary>두 건 이상은 즉시 보내지 않고 Draft 트레이에 쌓는다(콘솔 공통 규칙).</summary>
    public bool IsMultiCall => WriteCount > 1;
}

/// <summary>
/// 장비 → 서버 드롭의 판정(순수 함수) — 화면 없이 단위 테스트한다.
/// </summary>
/// <remarks>
/// <para><b>허용 표는 서버가 정한다</b>(읽기 전용 서버 소스 <c>app/schemas/device.py:85-93</c>
/// <c>SERVER_CATEGORIES_BY_DEVICE</c>): 제어기·경광등 → <c>PROXY</c> · 카메라 → <c>NVR_API</c> ·
/// 스피커 → <c>SPEAKER_API</c> · 함체 → <c>ENCLOSURE_API</c> ·
/// 통문 → <c>PROXY</c> 또는 <c>ENCLOSURE_API</c>(결선에 따라) · <b>센서는 없다</b>
/// (<c>device.py:645-647</c> — 소속 제어기를 따른다). 어긋나면 서버가 422 다
/// (<c>app/services/device_axes_io.py:404-452</c>).</para>
/// <para><b>해제는 서버가 지원한다</b> — <c>server_id: null</c> = 관계 해제
/// (<c>device.py:568 · 683 · 740</c>). 6.3 에서만 그 입구가 없다.</para>
/// <para>이 판정은 끄는 동안 <b>매 프레임</b> 불린다 — 서버 호출도, <c>Keyboard.Modifiers</c> 읽기도 없다.</para>
/// </remarks>
public static class ServerDropRules
{
    /// <summary>드롭존 종류 — 목록의 서버 <b>행</b>이 드롭존이다.</summary>
    public const string ZoneKey = "server-row";

    /// <summary>카테고리 → 그 장비를 중계할 수 있는 서버 유형(서버 표를 그대로 옮긴 것).</summary>
    public static readonly IReadOnlyDictionary<EnumDeviceCategory, IReadOnlyList<EnumServerType>> AllowedServerTypes =
        new Dictionary<EnumDeviceCategory, IReadOnlyList<EnumServerType>>
        {
            [EnumDeviceCategory.Controller] = new[] { EnumServerType.PROXY },
            [EnumDeviceCategory.Lamp] = new[] { EnumServerType.PROXY },
            [EnumDeviceCategory.Camera] = new[] { EnumServerType.NVR_API },
            [EnumDeviceCategory.Speaker] = new[] { EnumServerType.SPEAKER_API },
            [EnumDeviceCategory.Enclosure] = new[] { EnumServerType.ENCLOSURE_API },
            [EnumDeviceCategory.Gate] = new[] { EnumServerType.PROXY, EnumServerType.ENCLOSURE_API },
        };

    /// <summary>그 서버 유형이 이 카테고리의 장비를 받는가.</summary>
    public static bool Accepts(EnumServerType? serverType, EnumDeviceCategory deviceCategory)
        => serverType is not null
           && AllowedServerTypes.TryGetValue(deviceCategory, out var allowed)
           && allowed.Contains(serverType.Value);

    /// <summary>못 받는 까닭 한 줄. 받을 수 있으면 <c>null</c>.</summary>
    public static string? RefusalReason(EnumServerType? serverType, EnumDeviceCategory deviceCategory)
    {
        if (Accepts(serverType, deviceCategory)) return null;

        if (!AllowedServerTypes.TryGetValue(deviceCategory, out var allowed))
            return $"{CategoryLabel(deviceCategory)}에는 관리 서버가 없습니다. 소속 제어기를 따릅니다.";

        var names = string.Join(" · ", allowed.Select(t => ServerTypeCatalog.TypeLabel(t)));
        // 상태 줄이 TextBox 라 화면 쪽 조사 고르기(KoreanParticles)가 닿지 않는다 — 여기서 고른다.
        return Ironwall.Dotnet.Libraries.Utils.Consoles.KoreanParticles.Resolve(
            $"{CategoryLabel(deviceCategory)}은(는) {names} 서버에만 배정할 수 있습니다. "
            + $"'{ServerTypeCatalog.TypeLabel(serverType)}' 서버에는 놓을 수 없습니다.");
    }

    /// <summary>6.3 에서 이 배정을 보낼 수 있는가 — 그 판본의 클라 계약은 스피커만 서버를 쓴다.</summary>
    public static bool IsSupportedOnLegacy(EnumDeviceCategory deviceCategory)
        => deviceCategory == EnumDeviceCategory.Speaker;

    /// <summary>끌어 온 장비들을 그 서버에 배정하는 계획. 서버를 부르지 않는다.</summary>
    public static ServerAssignPlan Plan(
        int serverId, EnumServerType? serverType, IEnumerable<IBaseDeviceModel>? devices,
        EnumServerContract contract = EnumServerContract.V8_0)
    {
        var list = devices?.Where(d => d is not null).ToList() ?? new List<IBaseDeviceModel>();

        if (serverId <= 0) return Blocked(serverId, "아직 등록되지 않은 서버입니다. 먼저 등록하세요.");
        if (list.Count == 0) return Blocked(serverId, "끌어 온 장비가 없습니다.");

        var eligible = new List<IBaseDeviceModel>();
        var ineligible = 0;
        string? firstRefusal = null;

        foreach (var device in list)
        {
            var category = DeviceAxesMapper.CategoryOf(device);
            var reason = RefusalReason(serverType, category)
                         ?? (contract < EnumServerContract.V7_0 && !IsSupportedOnLegacy(category)
                             ? $"현재 서버에서는 {CategoryLabel(category)} 장비를 서버에 배정할 수 없습니다."
                             : null);

            if (reason is null) { eligible.Add(device); continue; }
            ineligible++;
            firstRefusal ??= reason;
        }

        if (eligible.Count == 0)
            return Blocked(serverId, firstRefusal ?? "이 서버에는 배정할 수 없는 장비입니다.", ineligible);

        var drafts = eligible.Count(d => d.Id <= 0);
        var saved = eligible.Where(d => d.Id > 0).ToList();
        var already = saved.Count(d => ServerIdOf(d) == serverId);
        var sending = saved.Where(d => ServerIdOf(d) != serverId)
                           .GroupBy(d => d.Id).Select(g => g.First()).ToList();

        string? block = null;
        if (sending.Count == 0)
            block = saved.Count == 0 ? "아직 등록되지 않은 장비입니다. 장비를 먼저 등록하세요." : "이미 이 서버에 배정되어 있습니다.";

        return new ServerAssignPlan(serverId, sending, drafts, already, ineligible, block);
    }

    /// <summary>확인 문구 — 몇 대를 어디에 배정하는지 말한다(요청 횟수 같은 구현어는 쓰지 않는다 — U-18 D-7 7.4).</summary>
    public static string ConfirmText(string serverName, int writeCount)
        => $"'{serverName}'에 장비 {writeCount}대를 배정할까요?";

    /// <summary>배정 대기 목록을 한꺼번에 저장할 때의 확인 문구.</summary>
    public static string TrayConfirmText(int count)
        => $"대기 중인 배정 {count}건을 저장할까요?";

    /// <summary>확인 창에서 [취소] 를 눌렀을 때.</summary>
    public const string CancelledText = "취소했습니다. 저장하지 않았습니다.";

    /// <summary>상태 띠에 남길 한 줄.</summary>
    public static string ResultLine(string serverName, ServerAssignPlan plan, int assigned, int failed)
    {
        var parts = new List<string> { $"'{serverName}'에 {assigned}대를 배정했습니다" };
        if (failed > 0) parts.Add($"{failed}대는 배정하지 못했습니다");
        if (plan.AlreadyOn > 0) parts.Add($"이미 배정된 {plan.AlreadyOn}대는 그대로 두었습니다");
        if (plan.Ineligible > 0) parts.Add($"배정할 수 없는 {plan.Ineligible}대는 뺐습니다");
        if (plan.DraftExcluded > 0) parts.Add($"등록 전 {plan.DraftExcluded}대는 뺐습니다");
        return string.Join(" · ", parts) + ".";
    }

    /// <summary>
    /// 그 장비가 지금 붙어 있는 서버 Id.
    /// </summary>
    /// <remarks>
    /// 모델에 서버 축을 가진 것은 스피커뿐이다(<c>Messages</c> 범위 밖이라 넓히지 않았다) —
    /// 다른 카테고리는 <c>null</c> 이고, 그래서 "이미 그 서버" 판정이 <b>보수적</b>으로 동작한다
    /// (같은 서버에 다시 보내도 서버가 멱등하게 처리한다).
    /// </remarks>
    public static int? ServerIdOf(IBaseDeviceModel? device)
        => device is ISpeakerDeviceModel speaker && speaker.Server is { Id: > 0 } server ? server.Id : null;

    public static string CategoryLabel(EnumDeviceCategory category) => category switch
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
        => new(serverId, Array.Empty<IBaseDeviceModel>(), 0, 0, ineligible, reason);
}
