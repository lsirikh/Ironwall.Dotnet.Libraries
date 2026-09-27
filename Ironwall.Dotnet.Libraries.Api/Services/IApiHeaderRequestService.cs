using System.Net.Http;

namespace Ironwall.Dotnet.Libraries.Api.Services;
/****************************************************************************
   Purpose      : 요청 헤더를 한 건에만 싣는 JSON 호출 — 조건부 쓰기(If-Match)용 선택 능력
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// <see cref="IApiService"/> 와 <b>같은 전송 파이프라인</b>(인증 처리기 · 기본 주소 · 시간 제한 · X-Client-Id)으로
/// 요청 <b>한 건에만</b> 헤더를 더해 JSON 본문을 보낸다.
/// </summary>
/// <remarks>
/// <para>쓰는 곳: 부대 관계도 공유 배치 <c>PATCH /api/units/layout</c> 의 <c>If-Match: "&lt;version&gt;"</c>
/// (낙관적 동시성 — 헤더가 없으면 서버는 428, 다르면 412).</para>
/// <para><b>왜 <see cref="IApiService"/> 에 멤버를 더하지 않는가</b> — 그 인터페이스의 가짜 · 캡처 구현이
/// 여러 시험 프로젝트에 흩어져 있어, 멤버 하나가 전부를 컴파일 오류로 만든다(메모리
/// <c>interface_widening_mocks_and_build_blindspot</c>). 부르는 쪽은 <c>apiService as IApiHeaderRequestService</c> 로
/// 이 능력을 찾고, 없으면 조건부 쓰기를 <b>보내지 않는다</b>(헤더 없는 쓰기는 말없는 덮어쓰기가 된다).</para>
/// <para>본문 직렬화는 <see cref="ApiService"/> 의 다른 쓰기와 같은 설정이다.</para>
/// </remarks>
public interface IApiHeaderRequestService
{
    /// <summary>
    /// <paramref name="method"/> 로 <paramref name="endpoint"/> 에 <paramref name="body"/>(없으면 본문 없음)를 보내며
    /// <paramref name="headers"/> 를 이 요청에만 싣는다.
    /// </summary>
    /// <returns>실패도 예외가 아니라 상태 코드로 돌려준다(시간 초과 504 · 연결 실패 503 — 다른 호출과 같은 합성).</returns>
    Task<HttpResponseMessage> SendJsonAsync(
        HttpMethod method,
        string endpoint,
        object? body,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken token = default);
}
