using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Accounts;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Helpers;

/****************************************************************************
   Purpose      : 관리자 계정 삭제 정책 (DeleteAccountDialog 2중 루프 버그 수정)
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : 기존 IsAdminDeletable()는 첫 foreach의 ADMIN case가 빈 블록이라 adminCount를
                  세지 않고 userCount도 결정에 미사용 → return (adminCount>1)만 반환(조건2 누락).
                  단일 패스로 admin/user를 집계하고 설계 의도 3조건을 모두 반영한다.
****************************************************************************/
public static class AccountDeletionPolicy
{
    /// <summary>
    /// 관리자(ADMIN) 계정 삭제 가능 여부.
    /// <br/>- 복수 관리자 → 가능
    /// <br/>- 유일 관리자 + 일반계정 없음(단일 계정) → 가능
    /// <br/>- 유일 관리자 + 일반계정 1개 이상 → 불가
    /// </summary>
    public static bool CanDeleteAdmin(IEnumerable<IAccountModel> accounts)
    {
        int admin = 0, user = 0;
        foreach (var a in accounts)
        {
            if (a.Level == EnumLevelType.ADMIN) admin++;
            else if (a.Level == EnumLevelType.USER) user++;
        }
        return admin > 1 || (admin == 1 && user == 0);
    }
}
