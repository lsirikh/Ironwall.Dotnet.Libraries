using Ironwall.Dotnet.Monitoring.Models.Accounts;

namespace Ironwall.Dotnet.Libraries.Accounts.Gateways;

/****************************************************************************
   Purpose      : 인증 결과 계약 (계정 + 토큰 + 만료 + role + permissions)
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : Direct(Db) 어댑터는 Token=로컬 TokenGenerator, Role=Level.ToString(),
                  Permissions=빈 배열로 채운다. 후속 GOP-00의 Api 어댑터는 서버 JWT/
                  role/permissions를 그대로 채운다. 계약을 Direct 모양으로 좁히지 말 것.
****************************************************************************/
public sealed record AuthResult(
    IAccountModel Account,
    string Token,
    DateTime ExpiresAt,
    string Role,
    IReadOnlyList<string> Permissions);
