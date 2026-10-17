using Ironwall.Dotnet.Monitoring.Models.Accounts;

namespace Ironwall.Dotnet.Libraries.Accounts.Gateways;

/****************************************************************************
   Purpose      : 본인 프로필 게이트웨이 (조회/저장/비밀번호 변경)
   Notes        : MyPagePanel + ResetPassDialog(본인)가 의존.
                  ChangePasswordAsync는 currentPassword 검증을 흡수(Db=로컬, Api=서버).
****************************************************************************/
public interface IProfileGateway
{
    /// <summary>PK 기준 본인 계정 조회.</summary>
    Task<IAccountModel?> GetProfileAsync(int accountId, CancellationToken ct = default);

    /// <summary>
    /// 본인이 사원번호(군번)를 바꿀 수 있는가. 기본=true(DB 모드 — 행 전체를 저장한다).
    /// Api 구현은 false — 서버 본인 수정(PUT /users/me) 본문에는 <c>employee_number</c> 가 없고(보내면 422),
    /// 사원번호는 관리자 경로(PUT /users/{id})로만 바뀐다.
    /// </summary>
    bool CanSelfEditEmployeeNumber => true;

    /// <summary>
    /// 내 정보에서 본인 계정을 (비밀번호를 확인한 뒤) 지울 수 있는가. 기본=true(DB 모드 — 게이트웨이가 로컬 해시로 비밀번호를 확인한다).
    /// Api 구현은 false — ① 서버 <c>DELETE /users/{id}</c> 는 비밀번호를 받지 않고 ② 비밀번호만 확인하는 엔드포인트가 없으며
    /// ③ <c>POST /auth/login</c> 으로 확인하면 단일 세션 정책에서 지금 세션이 쫓겨나고 틀린 입력이 잠금 횟수에 쌓이고
    /// ④ 서버는 본인 계정 삭제 자체를 409 로 거절한다. 확인할 수 없는 비밀번호를 받는 척하지 않는다.
    /// </summary>
    bool CanSelfDeleteAccount => true;

    /// <summary>본인 프로필 정보 저장(비밀번호 제외).</summary>
    Task<IAccountModel?> UpdateProfileAsync(IAccountModel acc, CancellationToken ct = default);

    /// <summary>본인 비밀번호 변경(현재 비밀번호 검증 후). 검증 실패 시 null.</summary>
    Task<IAccountModel?> ChangePasswordAsync(IAccountModel acc, string currentPassword, string newPassword, CancellationToken ct = default);

    /// <summary>프로필 사진 업로드. API=서버 업로드 후 photo_url(절대 URL) 반환, DB=미지원(null). 실패 시 null.</summary>
    Task<string?> UploadPhotoAsync(string filePath, CancellationToken ct = default);

    /// <summary>본인 프로필 사진 삭제(idempotent). API=서버 DELETE /users/me/photo 성공 여부, DB=미지원(false). 실패 시 false. — MyPage_SelfPhoto_Delete_Fix</summary>
    Task<bool> DeletePhotoAsync(CancellationToken ct = default);
}
