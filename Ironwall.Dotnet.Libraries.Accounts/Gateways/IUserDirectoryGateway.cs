using Ironwall.Dotnet.Monitoring.Models.Accounts;

namespace Ironwall.Dotnet.Libraries.Accounts.Gateways;

/****************************************************************************
   Purpose      : 관리자 계정 디렉터리 게이트웨이 (목록/생성/삭제/잠금)
   Notes        : AccountManagerPanel + Register/Editor/Delete 다이얼로그가 의존.
                  RemoveAccountAsync는 currentPassword 검증을 흡수(Db=로컬, Api=서버).
****************************************************************************/
public interface IUserDirectoryGateway
{
    /// <summary>전체 계정 목록.</summary>
    Task<List<IAccountModel>?> GetAllAccountsAsync(CancellationToken ct = default);

    /// <summary>신규 계정 생성. 반환=PK 채워진 모델(실패 시 null).</summary>
    Task<IAccountModel?> CreateAccountAsync(IAccountModel acc, CancellationToken ct = default);

    /// <summary>관리자에 의한 계정 정보 수정(비밀번호 제외).</summary>
    Task<IAccountModel?> UpdateAccountAsync(IAccountModel acc, CancellationToken ct = default);

    /// <summary>계정 삭제. currentPassword가 비어있지 않으면 검증 후 삭제.</summary>
    Task<bool> RemoveAccountAsync(IAccountModel acc, string currentPassword, CancellationToken ct = default);

    /// <summary>아이디 중복 여부.</summary>
    Task<bool> IsUsernameTakenAsync(string username, CancellationToken ct = default);

    /// <summary>관리자 강제 비밀번호 초기화(현재 비밀번호 검증 없음).</summary>
    Task<IAccountModel?> ResetAccountPasswordAsync(IAccountModel acc, string newPassword, CancellationToken ct = default);

    /// <summary>계정 잠금 해제(ADMIN). Api=POST /users/{id}/unlock, Db=미지원(no-op false). 성공 여부 반환.</summary>
    Task<bool> UnlockAccountAsync(int id, CancellationToken ct = default)
        => Task.FromResult(false);
}
