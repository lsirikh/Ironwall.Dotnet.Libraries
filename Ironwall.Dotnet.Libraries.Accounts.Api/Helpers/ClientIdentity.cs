using Ironwall.Dotnet.Libraries.Api.Helpers;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;

/// <summary>
/// 로그인 요청 본문의 <c>client_id</c> — 명세 §9.2.2 / §9.8.
///
/// <para><b>이 클래스는 얇은 위임이다.</b> 값의 정본은
/// <see cref="ClientIdResolver"/>(= 설치 고유값 <c>%ProgramData%\Ironwall\Gis\client-id</c>) 하나이고,
/// 같은 값이 <c>X-Client-Id</c> 헤더로도 나간다.</para>
///
/// <para><b>왜 하나여야 하는가</b> — 서버는 헤더를 먼저 보고 본문은 헤더가 없을 때만 본다
/// (<c>auth.py</c>: <c>request.headers.get("X-Client-Id") or login_data.client_id</c>).
/// 두 곳에 다른 값을 실으면 본문 값이 <b>조용히 버려져</b> 세션 축이 우리 의도와 달라진다.</para>
///
/// <para><b>이력(2026-09-30 정정)</b> — 이전 구현은 <c>gis-monitoring:{머신명}</c> 을 만들어
/// <b>본문에만</b> 실었다. 두 가지가 틀렸다:
/// ① 헤더에는 설정값(기본 <c>central-ui</c>)이 나가 <b>본문 값이 버려졌다</b>
/// ② 축을 머신명으로 잡아 설치기가 이미 만들어 두는 설치 고유값(FR-20)과 어긋났다.
/// 머신명은 이미지 복제·이름 변경에 약하고, 설치 고유값은 <c>%ProgramData%</c> 에 보존되어
/// 재설치·업그레이드에도 유지된다.</para>
/// </summary>
public static class ClientIdentity
{
    /// <summary>
    /// 이 설치 인스턴스의 <c>client_id</c>. 프로세스 수명 동안 불변이며 헤더 값과 같다.
    /// </summary>
    /// <remarks>
    /// 설정값을 모르는 호출부를 위한 무인자 진입점이다. 설정값이 있는 경로
    /// (<c>ApiService</c>)는 <see cref="ClientIdResolver.Resolve"/> 를 직접 부른다.
    /// 리졸버는 <b>호출 순서에 안전</b>하게 만들어져, 이 무인자 경로가 먼저 불려도
    /// 뒤에 오는 유효한 설정값이 이긴다(관리자 지정값을 삼키지 않는다).
    /// </remarks>
    public static string Current => ClientIdResolver.Resolve(configuredId: null);
}
