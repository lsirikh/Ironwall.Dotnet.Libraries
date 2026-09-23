using Ironwall.Dotnet.Libraries.Accounts.Api.Services;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Helpers;

/// <summary>
/// 조치보고(<c>POST /api/events/actions</c>) 를 보내기 전에 클라가 먼저 보는 규칙 — 권한 한 줄 · 내용 한 줄.
/// <para>
/// <b>권한</b>: 서버는 조치보고 생성을 <c>events:edit</c> 로 막는다(api-test-server <c>app/security/permission_map.py</c>
/// <c>("POST","/api/events/actions") → ("events","edit")</c>, 라우트 가드 <c>routers/actions.py</c> 도 같다).
/// 이 규칙은 <b>v5.2 부터 바뀐 적이 없다</b> — 운영 판본 <c>v6.3.2</c> 태그와 현재 8.0.2 가 같은 값이다.
/// 그래서 판본 프로브로 가를 필요가 없다.
/// 종전 클라는 <c>events:control</c> 을 봤다(FR-EN-10 "ACK=control" 가정). 서버 운영자 프리셋은 events 가 RC
/// (view+control, edit 없음)라 운영자는 클라 관문을 지나 서버에서 403 을 받고, 창은 그것을
/// "네트워크/서버 상태를 확인" 이라는 엉뚱한 안내로 보여 줬다(실서버 왕복 E1a 로 확인).
/// </para>
/// <para>
/// <b>내용</b>: 서버 <c>ActionEventCreate.content</c> 는 <c>min_length=1, max_length=500</c> 이고 빈 문자열은
/// <c>EMPTY_STRING</c> 422 다. 공백만 있는 문자열도 사람에게는 빈 조치다.
/// </para>
/// </summary>
public static class ActionReportRules
{
    /// <summary>서버 <c>ActionEventCreate.content</c> 최대 길이.</summary>
    public const int MAX_CONTENT_LENGTH = 500;

    /// <summary>권한 없음 안내 — 어느 창이든 같은 문장.</summary>
    public const string NO_PERMISSION_TEXT = "조치보고 권한(이벤트 편집)이 없습니다.";

    /// <summary>
    /// 이 사용자가 조치보고를 보낼 수 있는가 — 서버 계약 <c>events:edit</c>.
    /// 권한 서비스가 없으면(미등록 · 테스트) 막지 않는다 — 최종 방어는 서버 403 이다(기존 폴백 유지).
    /// </summary>
    public static bool CanReport(IPermissionService? permission) => permission?.CanEdit("events") ?? true;

    /// <summary>
    /// 보낼 조치 내용이 서버가 받을 모양인지. 받을 수 있으면 <c>null</c>, 아니면 사람이 읽을 이유.
    /// </summary>
    public static string? ValidateContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return "조치 내용을 입력하세요.";
        if (content.Length > MAX_CONTENT_LENGTH) return $"조치 내용은 {MAX_CONTENT_LENGTH}자까지 보낼 수 있습니다(지금 {content.Length}자).";
        return null;
    }
}
