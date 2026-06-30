namespace Ironwall.Dotnet.Libraries.Accounts.Api.Services;

/// <summary>강제 로그아웃 사유 코드(FR-FL-08). 클라가 고정 문구로 매핑(자유 텍스트 미수신).</summary>
public enum EnumRevokeReason
{
    /// <summary>사용자 수동 로그아웃.</summary>
    Manual,
    /// <summary>관리자 강제 로그아웃(서버 세션 revoke).</summary>
    SessionRevoked,
    /// <summary>인증 실패/토큰 무효(401, refresh 실패).</summary>
    Unauthorized,
    /// <summary>토큰 만료.</summary>
    TokenExpired,
}

/// <summary>
/// 강제 로그아웃 단일 멱등 진입점(FR-FL-04). NATS revoke · 401 폴백 · 수동 로그아웃 세 경로를 수렴해
/// 토큰·권한을 1회만 폐기하고 <see cref="ForceLogoutRequested"/> 를 발화한다.
/// <para>once-guard 로 동시/연속 트리거에도 1회만 수행(이중 CloseAllWindows/팝업 방지). 새 로그인 시 <see cref="ResetForLogin"/> 으로 재무장.</para>
/// <para>GIS(MapViewModel) 및 셸이 <see cref="ForceLogoutRequested"/> 를 구독해 PTZ 정지·스트림 해제·가림막·로그인 화면 전환을 수행한다
/// (Caliburn 의존 회피 위해 IEventAggregator 대신 자체 이벤트 — IPermissionService.PermissionsChanged 패턴 미러).</para>
/// </summary>
public interface ISessionLifecycle
{
    /// <summary>토큰·권한 폐기 + <see cref="ForceLogoutRequested"/> 1회 발화(멱등). 이미 진행 중이면 무시.</summary>
    void ForceLogoutOnce(EnumRevokeReason reason);

    /// <summary>로그인 성공 시 once-guard 재무장(이후 강제 로그아웃이 다시 동작하도록).</summary>
    void ResetForLogin();

    /// <summary>강제 로그아웃 1회 발화(reason 동반). lock 밖에서 발화(재진입 방지). 구독자=GIS/셸.</summary>
    event Action<EnumRevokeReason>? ForceLogoutRequested;

    /// <summary>로그인 성공 직후 발화(B 연계: GIS init·Device fetch 트리거). 토큰·권한 적용 완료 후.</summary>
    event Action? LoginSucceeded;
    /// <summary>로그인 성공 통지 — ApiAccountGateway가 ResetForLogin 직후 호출. 구독자(GIS)가 데이터 로드 시작.</summary>
    void NotifyLoginSucceeded();
}
