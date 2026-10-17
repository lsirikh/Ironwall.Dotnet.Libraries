namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;

/// <summary>
/// 권한 저장 한 번의 결과. <b>void 로 삼키면</b> 실패한 저장 뒤에도 화면이 "저장됨" 으로 굳어
/// 미적용 표시가 사라지고 서버는 옛 값을 쥔 채 남는다.
/// </summary>
/// <param name="Success">서버가 받아들였는가.</param>
/// <param name="Reason">실패 사유(성공이면 null).</param>
/// <param name="IsDrift">그 사이 다른 곳에서 바뀌어 <b>아무것도 보내지 않았는가</b>.</param>
public sealed record PermissionSaveOutcome(bool Success, string? Reason, bool IsDrift = false)
{
    public static PermissionSaveOutcome Ok() => new(true, null);
    public static PermissionSaveOutcome Fail(string reason, bool isDrift = false) => new(false, reason, isDrift);
}
