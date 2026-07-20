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

    /// <summary>본인 프로필 정보 저장(비밀번호 제외).</summary>
    Task<IAccountModel?> UpdateProfileAsync(IAccountModel acc, CancellationToken ct = default);

    /// <summary>본인 비밀번호 변경(현재 비밀번호 검증 후). 검증 실패 시 null.</summary>
    Task<IAccountModel?> ChangePasswordAsync(IAccountModel acc, string currentPassword, string newPassword, CancellationToken ct = default);

    /// <summary>프로필 사진 업로드. API=서버 업로드 후 photo_url(절대 URL) 반환, DB=미지원(null). 실패 시 null.</summary>
    Task<string?> UploadPhotoAsync(string filePath, CancellationToken ct = default);

    /// <summary>본인 프로필 사진 삭제(idempotent). API=서버 DELETE /users/me/photo 성공 여부, DB=미지원(false). 실패 시 false. — MyPage_SelfPhoto_Delete_Fix</summary>
    Task<bool> DeletePhotoAsync(CancellationToken ct = default);
}
