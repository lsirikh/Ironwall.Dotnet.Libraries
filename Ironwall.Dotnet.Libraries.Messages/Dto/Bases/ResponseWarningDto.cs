using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Bases;

/// <summary>
/// 성공 봉투 최상위 <c>warnings[]</c> 한 항목 — <b>3키</b> <c>{field, code, message}</c>(명세 §3.2·§12.1.2).
/// <para>
/// <b>왜 필요한가</b> — 장비(§5)·서버(§8)·이벤트(§6)의 <b>쓰기·보고</b> 응답은 거부하지 않은 사실을
/// 이 배열로 알린다("저장은 됐지만 무시된 키가 있다" · "관측 시각을 수신 시각으로 대체했다" ·
/// "이 임계치는 어느 부품 메트릭에도 걸리지 않는다"). 우리 봉투에 받을 자리가 없어서
/// <c>MissingMemberHandling.Ignore</c> 가 <b>조용히 버려</b> 운영자가 영구히 모르는 상태였다.
/// </para>
/// <para>
/// <see cref="Code"/> 는 서버 <b>닫힌 4종</b>(<see cref="Defines.Apis.ApiWarningCodes"/>)이지만
/// <b>모르는 코드는 무시</b>한다 — 경고는 거부가 아니다. 그래서 enum 이 아니라 문자열로 받는다.
/// </para>
/// <para>배포 실측(2026-09-18, 로컬 <c>8.0.1</c>): Swagger <c>ResponseWarning</c> =
/// <c>{field, code, message}</c> 3키 전부 required · <c>additionalProperties: false</c>.</para>
/// </summary>
public class ResponseWarningDto
{
    /// <summary>대상 필드 경로. 예: <c>observed_at</c> · <c>thresholds.dust</c> · <c>device_config.thresholds.network</c>.</summary>
    [JsonProperty("field", Order = 1)]
    public string? Field { get; set; }

    /// <summary>경고 코드(닫힌 4종 — <see cref="Defines.Apis.ApiWarningCodes"/>). 어휘 밖 값도 그대로 받는다.</summary>
    [JsonProperty("code", Order = 2)]
    public string? Code { get; set; }

    /// <summary>사람이 읽는 설명 — 운영자 화면에 그대로 보여도 되는 문장(서버 계약 S-7).</summary>
    [JsonProperty("message", Order = 3)]
    public string? Message { get; set; }
}
