using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Defines.Apis;

/// <summary>
/// 메타데이터 DTO - 모든 API 응답에 포함되는 메타 정보
/// <para>
/// ⚠ <b>키 개수가 경로마다 다르다</b>(명세 §3.2 · 실측 2026-09-18 로컬 <c>8.0.1</c>):
/// <list type="bullet">
///   <item><b>장비·서버</b> 목록·단건·쓰기 = <b>4키</b> — <c>timestamp</c>·<c>request_id</c> + <c>view</c>·<c>sections</c></item>
///   <item>그 밖 전부(축 <c>/config</c>·<c>/component-status</c>·<c>/spec</c>·<c>by-component</c>·이벤트·계정 …) =
///         <b>2키</b>(<c>view</c>·<c>sections</c> 가 <b>없다</b>)</item>
/// </list>
/// 그래서 <see cref="View"/>·<see cref="Sections"/> 는 <b>nullable</b> 이다 — 모든 경로에서 필수로 가정한 파서는 반대로 깨진다.
/// </para>
/// </summary>
public class MetaDto
{
    /// <summary>
    /// 응답 생성 시각 (ISO 8601 with Korea offset)
    /// <para>서버 D19(명세 §3.4): <b>오프셋 + 마이크로초 6자리 고정</b> — 예 <c>"2026-09-18T09:42:53.104788+09:00"</c>.
    /// 소수부 유무·자릿수에 영향받지 않도록 <b>문자열</b>로 받는다(<c>DateParseHandling.None</c> 과 한 쌍).</para>
    /// </summary>
    [JsonProperty("timestamp", Order = 1)]
    public string Timestamp { get; set; } = KoreaTimeHelper.GetKoreaTimeIso8601();

    /// <summary>
    /// 요청 추적용 UUID. 응답 헤더 <c>X-Request-ID</c> · 서버 로그 id 와 <b>같은 값</b>이다(500 응답 포함).
    /// </summary>
    [JsonProperty("request_id", Order = 2)]
    public string? RequestId { get; set; }

    /// <summary>
    /// 표현 프로필 — <c>"basic"</c> | <c>"full"</c>. <b>장비·서버 경로에만</b> 실린다(그 밖은 <c>null</c>).
    /// <para>기본값이 <c>basic</c> 이라 <c>?view=full</c> 을 붙이지 않으면
    /// <c>device_config</c>·<c>device_status</c>·<c>hardware_spec</c> 이 <b>키째 오지 않는다</b>(실측).
    /// 그 상태를 "설정 안 됨"으로 오독하지 않으려면 이 값을 읽어야 한다.</para>
    /// </summary>
    [JsonProperty("view", Order = 3)]
    public string? View { get; set; }

    /// <summary>
    /// 이 응답이 <b>실제로 실은</b> 섹션(알파벳순). <b>장비·서버 경로에만</b> 실린다(그 밖은 <c>null</c>).
    /// <para>실측: <c>view=basic</c> → <c>["connection"]</c> ·
    /// <c>view=full</c> → <c>["components","connection","device_config","device_status","hardware_spec"]</c>.</para>
    /// <para>요청하지 않은 섹션은 <b>키째 빠진다</b>(<c>null</c> 이 아니다) —
    /// <b>"값이 없다"와 "안 실었다"를 가르는 유일한 근거</b>가 이 목록이다.</para>
    /// </summary>
    [JsonProperty("sections", Order = 4)]
    public List<string>? Sections { get; set; }

    /// <summary>
    /// 이 <c>meta</c> 가 표현 프로필 정보를 실었는가(= 장비·서버 경로인가). 직렬화 대상 아님.
    /// </summary>
    [JsonIgnore]
    public bool HasViewInfo => !string.IsNullOrWhiteSpace(View) || Sections != null;

    /// <summary><c>view=full</c> 로 응답했는가(대소문자 무시). 정보가 없으면 <c>false</c>.</summary>
    [JsonIgnore]
    public bool IsFullView => string.Equals(View, "full", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 주어진 섹션이 이 응답에 <b>실렸는지</b>. <c>sections</c> 가 없으면(장비·서버 밖 경로) <c>false</c>.
    /// <para>"필드가 <c>null</c> 이다"와 "섹션을 안 실었다"를 구분하는 판정에 쓴다.</para>
    /// </summary>
    public bool HasSection(string section)
    {
        if (string.IsNullOrWhiteSpace(section) || Sections == null) return false;
        foreach (var s in Sections)
            if (string.Equals(s, section, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
