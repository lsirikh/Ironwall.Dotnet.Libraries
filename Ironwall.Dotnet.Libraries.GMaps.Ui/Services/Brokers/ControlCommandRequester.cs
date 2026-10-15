using Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Brokers;

/****************************************************************************
   Purpose      : 제어 명령 공통 선택 칸 채우기 — requested_by · requested_client_id
                  (브로커 연동설계 v2.0.12 §7 머리, 이슈 #7 G7-4).
   Created By   : Claude
   Created On   : 2026-10-15
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// GIS 가 보내는 제어 명령 body 에 발신자 · 단말을 싣는다. 값이 없으면 null 로 두어 직렬화에서 빠진다.
/// </summary>
public static class ControlCommandRequester
{
    /// <summary>지금 로그인한 운영자 계정(login_id). 미로그인이면 null.</summary>
    public static string? LoginId(IPermissionService? permission)
    {
        var id = permission?.LoginId;
        return string.IsNullOrWhiteSpace(id) ? null : id!.Trim();
    }

    /// <summary>
    /// 이 단말의 통제DB 세션 client_id — 로그인 헤더 X-Client-Id 와 같은 값(설치마다 다르다).
    /// SSO(OIDC) client_id 는 쓰지 않는다.
    /// </summary>
    public static string? ClientId()
    {
        var id = ClientIdentity.Current;
        return string.IsNullOrWhiteSpace(id) ? null : id;
    }

    /// <summary>body 의 두 칸을 채워 그대로 돌려준다.</summary>
    public static T Stamp<T>(T body, IPermissionService? permission) where T : ControlCommandBodyDto
    {
        body.RequestedBy = LoginId(permission);
        body.RequestedClientId = ClientId();
        return body;
    }
}
