using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
/****************************************************************************
   Purpose      : 서버 모니터 레일 어휘 — 유형 판별자 → 레일 칸 (N-12)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>레일 한 칸의 선언 — 순수 값이다(아이콘은 이름만 쥔다).</summary>
/// <param name="Key">자동화 식별자와 전환 판정의 키(<c>Console.Servers.Rail.{Key}</c>).</param>
/// <param name="Label">사람이 읽는 이름.</param>
/// <param name="Icon">MaterialDesign <c>PackIconKind</c> 이름.</param>
/// <param name="ShowCount">개수 배지를 낼 것인가 — "시스템 이벤트" 는 서버 목록이 아니라 내지 않는다.</param>
/// <param name="HasSeparatorAbove">위에 구분선을 긋는다.</param>
public sealed record ServerRailSpec(string Key, string Label, string Icon, bool ShowCount, bool HasSeparatorAbove);

/// <summary>
/// 서버 유형(<see cref="EnumServerType"/>) 과 레일 칸의 대응 — <b>순수 함수</b>. 화면·서버 호출 없이 테스트한다.
/// </summary>
/// <remarks>
/// <para>정본: <c>docs/design/window-layout-system-storyboard.html</c> L1340-1342 —
/// 레일 184 는 "전체 · PROXY · NVR · 스피커 · 함체 … · ─ · 시스템 이벤트" 순이다.
/// "…" 는 나머지 유형 전부를 받는 <b>기타</b> 칸으로 굳힌다 — 26개 유형마다 칸을 내면 레일이 목록이 된다.</para>
/// <para><b>필터는 클라이언트에서 한다.</b> 서버 필터는 계약마다 키가 다르고(6.3 <c>category_id</c> 정수 /
/// 7.0 <c>category_server</c> 문자열) <b>기타</b> 처럼 여러 유형을 묶은 칸은 한 번의 질의로 표현할 수 없다.
/// 모르는 값은 서버가 422 라 실패 폭이 "목록 전체 0건" 이다 — 전건을 받아 여기서 가른다
/// (서버 수는 수십 단위라 비용이 문제되지 않는다).</para>
/// </remarks>
public static class ServerTypeCatalog
{
    public const string AllKey = "all";
    public const string ProxyKey = "proxy";
    public const string NvrKey = "nvr";
    public const string SpeakerKey = "speaker";
    public const string EnclosureKey = "enclosure";
    public const string EtcKey = "etc";
    public const string SystemEventsKey = "system-events";

    /// <summary>레일은 서버가 0대여도 늘 이 순서로 같은 칸이 떠 있다(빈 칸도 "지금 비었다"는 정보다).</summary>
    public static readonly IReadOnlyList<ServerRailSpec> RailOrder = new[]
    {
        new ServerRailSpec(AllKey,          "전체",        "ServerNetwork",     ShowCount: true,  HasSeparatorAbove: false),
        new ServerRailSpec(ProxyKey,        "PROXY",       "SwapHorizontal",    ShowCount: true,  HasSeparatorAbove: true),
        new ServerRailSpec(NvrKey,          "NVR",         "Cctv",              ShowCount: true,  HasSeparatorAbove: false),
        new ServerRailSpec(SpeakerKey,      "스피커",      "Bullhorn",          ShowCount: true,  HasSeparatorAbove: false),
        new ServerRailSpec(EnclosureKey,    "함체",        "PackageVariant",    ShowCount: true,  HasSeparatorAbove: false),
        new ServerRailSpec(EtcKey,          "기타",        "DotsHorizontal",    ShowCount: true,  HasSeparatorAbove: false),
        new ServerRailSpec(SystemEventsKey, "시스템 이벤트", "Timeline",          ShowCount: false, HasSeparatorAbove: true),
    };

    /// <summary>서버 목록을 그리는 칸인가 — "시스템 이벤트" 만 아니다.</summary>
    public static bool IsServerList(string? railKey)
        => !string.Equals(railKey, SystemEventsKey, StringComparison.Ordinal);

    /// <summary>유형 → 레일 칸. 아는 넷 말고는 전부 <see cref="EtcKey"/>.</summary>
    public static string RailKeyOf(EnumServerType? type) => type switch
    {
        EnumServerType.PROXY => ProxyKey,
        EnumServerType.NVR_API => NvrKey,
        EnumServerType.SPEAKER_API => SpeakerKey,
        EnumServerType.ENCLOSURE_API => EnclosureKey,
        _ => EtcKey,
    };

    /// <summary>그 칸이 이 유형을 담는가. <see cref="AllKey"/> 는 전부 담는다.</summary>
    public static bool Matches(string? railKey, EnumServerType? type)
        => string.Equals(railKey, AllKey, StringComparison.Ordinal)
           || string.Equals(railKey, RailKeyOf(type), StringComparison.Ordinal);

    /// <summary>
    /// 카테고리의 판별자 문자열(<see cref="CategoryDto.TypeServer"/>) → <see cref="EnumServerType"/>.
    /// </summary>
    /// <remarks>
    /// 모르는 값이면 <c>null</c> 이다 — <b>던지지 않는다</b>. 서버 열거는 값이 늘 수 있고,
    /// 여기서 던지면 모르는 유형 한 대가 목록 전체를 죽인다(레포 교훈: 제대 문자열 · 카테고리 판별자).
    /// </remarks>
    public static EnumServerType? ParseType(string? typeServer)
        => Enum.TryParse<EnumServerType>((typeServer ?? string.Empty).Trim(), ignoreCase: true, out var parsed)
           && Enum.IsDefined(typeof(EnumServerType), parsed)
            ? parsed
            : null;

    /// <summary>목록 "유형" 열에 찍는 글자. 모르는 값이면 와이어 원문을 그대로 보인다(감추지 않는다).</summary>
    public static string TypeLabel(EnumServerType? type, string? rawTypeServer = null)
    {
        if (type is null)
            return string.IsNullOrWhiteSpace(rawTypeServer) ? "—" : rawTypeServer!.Trim();
        return Labels.TryGetValue(type.Value, out var label) ? label : type.Value.ToString();
    }

    /// <summary>레일 칸 선언 하나를 키로 찾는다. 없으면 <c>null</c>.</summary>
    public static ServerRailSpec? SpecOf(string? railKey)
        => RailOrder.FirstOrDefault(r => string.Equals(r.Key, railKey, StringComparison.Ordinal));

    /// <summary>유형별 한글 이름. 없는 값은 열거 이름을 그대로 쓴다.</summary>
    private static readonly IReadOnlyDictionary<EnumServerType, string> Labels = new Dictionary<EnumServerType, string>
    {
        [EnumServerType.VMS] = "VMS",
        [EnumServerType.NVR_API] = "NVR",
        [EnumServerType.STREAMING] = "스트리밍",
        [EnumServerType.TRANSCODER] = "트랜스코더",
        [EnumServerType.MEDIA] = "미디어",
        [EnumServerType.RECORDING] = "녹화",
        [EnumServerType.PLAYBACK] = "재생",
        [EnumServerType.STORAGE] = "저장소",
        [EnumServerType.AI_ANALYSIS] = "AI 분석",
        [EnumServerType.AI_TRAINING] = "AI 학습",
        [EnumServerType.AI_INFERENCE] = "AI 추론",
        [EnumServerType.ANALYTICS] = "통계",
        [EnumServerType.DB_API] = "DB",
        [EnumServerType.SPEAKER_API] = "스피커",
        [EnumServerType.ENCLOSURE_API] = "함체",
        [EnumServerType.PIDS_API] = "PIDS",
        [EnumServerType.WEB] = "웹",
        [EnumServerType.AUTH] = "인증",
        [EnumServerType.PROXY] = "PROXY",
        [EnumServerType.BROKER] = "브로커",
        [EnumServerType.GATEWAY] = "게이트웨이",
        [EnumServerType.PUSH] = "푸시",
        [EnumServerType.LOG] = "로그",
        [EnumServerType.BACKUP] = "백업",
        [EnumServerType.MONITORING] = "모니터링",
        [EnumServerType.ETC] = "기타",
    };
}
