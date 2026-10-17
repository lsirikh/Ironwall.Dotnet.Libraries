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

    /// <summary>
    /// 관리자에 의한 계정 정보 <b>부분</b> 수정 — 손댄 칸(서버 필드 이름: name · email · department · position ·
    /// employee_number · phone · role · is_active)만 보낸다. 비운 칸은 비운다.
    /// 기본구현=<see cref="UpdateAccountAsync"/>(전체 모델 저장 — DB 모드는 행 전체를 쓰므로 결과가 같다).
    /// Api 구현은 PUT /users/{id} 에 그 키들만 싣는다(바꾸지 않은 role 이 딸려 가 비-ADMIN 편집자가 403 받던 결함 방지).
    /// </summary>
    Task<IAccountModel?> UpdateAccountFieldsAsync(IAccountModel acc, IReadOnlyCollection<string> changedFields, CancellationToken ct = default)
        => UpdateAccountAsync(acc, ct);

    /// <summary>
    /// 로컬 계정 목록이 비어 있으면 "첫 등록자 = ADMIN" 으로 정해도 되는가. 기본=true(DB 모드 — 로컬 DB 가 곧 계정 전체다).
    /// Api 구현은 false — 서버 모드의 로컬 목록은 서버 목록을 불러온 <b>사본</b>이라 아직 안 불렀거나
    /// 불러오기에 실패해도(예: users:edit 만 있고 users:view 는 없는 운영자) 비어 있다. 그 빈 목록으로 ADMIN 을 추론하면
    /// 관리자가 만든 일반 계정이 조용히 ADMIN 이 되고, 비-ADMIN 운영자의 등록은 403 으로 막혔다(라이브 실측 2026-09-26).
    /// 서버 모드에서 역할은 서버 기본값(USER)이거나 관리자가 명시한 값뿐이다.
    /// </summary>
    bool CanInferFirstAccountAdmin => true;

    /// <summary>계정 삭제. currentPassword가 비어있지 않으면 검증 후 삭제.</summary>
    Task<bool> RemoveAccountAsync(IAccountModel acc, string currentPassword, CancellationToken ct = default);

    /// <summary>아이디 중복 여부.</summary>
    Task<bool> IsUsernameTakenAsync(string username, CancellationToken ct = default);

    /// <summary>관리자 강제 비밀번호 초기화(현재 비밀번호 검증 없음).</summary>
    Task<IAccountModel?> ResetAccountPasswordAsync(IAccountModel acc, string newPassword, CancellationToken ct = default);

    /// <summary>계정 잠금 해제(ADMIN). Api=POST /users/{id}/unlock, Db=미지원(no-op false). 성공 여부 반환.</summary>
    Task<bool> UnlockAccountAsync(int id, CancellationToken ct = default)
        => Task.FromResult(false);

    /// <summary>관리자: 대상 계정 프로필 사진 업로드. Api=POST /users/{id}/photo(성공 시 photo_url 반환), Db=미지원(null). 실패=null. — Admin_Photo_Upload</summary>
    Task<string?> UploadPhotoAsync(int userId, string filePath, CancellationToken ct = default)
        => Task.FromResult<string?>(null);

    /// <summary>관리자: 대상 계정 프로필 사진 삭제. Api=DELETE /users/{id}/photo(성공 true), Db=미지원(false). idempotent. — Admin_Photo_Upload</summary>
    Task<bool> DeletePhotoAsync(int userId, CancellationToken ct = default)
        => Task.FromResult(false);
}
